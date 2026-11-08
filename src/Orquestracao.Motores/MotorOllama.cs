using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Orquestracao.Core.Conversas;

namespace Orquestracao.Motores;

/// <summary>
/// Motor Ollama: a persona fala por um modelo local (ex.: qwen2.5:3b), pela API HTTP do Ollama
/// (<c>POST /api/chat</c> em streaming, <c>GET /api/tags</c> para os modelos). Sem custo, mas na CPU de um
/// notebook é lento: um pedido por vez, e a tela mostra os tokens por segundo.
/// </summary>
public sealed class MotorOllama(IReadOnlyList<string>? urls = null, HttpClient? http = null) : IMotor
{
    private readonly IReadOnlyList<string> _urls = urls ?? UrlsPadrao();
    private readonly HttpClient _http = http ?? new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
    private readonly SemaphoreSlim _fila = new(1, 1);
    private string? _achada;

    public string Nome => "ollama";

    /// <summary>O IP do Windows visto do WSL (o gateway padrão), onde o Ollama do Windows escuta.</summary>
    public static string? GatewayDoWsl(string? rotas = null)
    {
        if (rotas is null)
        {
            if (Environment.GetEnvironmentVariable("WSL_DISTRO_NAME") is null || !File.Exists("/proc/net/route")) return null;
            rotas = File.ReadAllText("/proc/net/route");
        }
        foreach (var linha in rotas.Split('\n').Skip(1))
        {
            var c = linha.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (c.Length > 2 && c[1] == "00000000" && c[2].Length == 8)
            {
                var bytes = Enumerable.Range(0, 4).Select(i => int.Parse(c[2].Substring(i * 2, 2), NumberStyles.HexNumber)).Reverse();
                return $"http://{string.Join('.', bytes)}:11434";
            }
        }
        return null;
    }

    /// <summary>Onde procurar o Ollama: OLLAMA_URL, o localhost e (no WSL) o Windows.</summary>
    public static List<string> UrlsPadrao() =>
        new[] { Environment.GetEnvironmentVariable("OLLAMA_URL"), "http://localhost:11434", GatewayDoWsl() }.OfType<string>().Where(u => u.Length > 0).ToList();

    private async Task<List<string>> Tags(string url, CancellationToken ct)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(1500);
        var j = await _http.GetFromJsonAsync<JsonObject>($"{url}/api/tags", cts.Token);
        return j?["models"]?.AsArray().Select(m => (string?)m?["name"]).OfType<string>().ToList() ?? [];
    }

    /// <summary>A primeira URL que responde (e fica guardada).</summary>
    private async Task<string> Url(CancellationToken ct)
    {
        if (_achada is not null) return _achada;
        foreach (var u in _urls)
        {
            try
            {
                await Tags(u, ct);
                return _achada = u;
            }
            catch (Exception e) when (e is HttpRequestException or OperationCanceledException or JsonException) { }
        }
        throw new InvalidOperationException(
            $"Ollama fora do ar (procurei em {string.Join(", ", _urls)}). Instale no Windows (winget install Ollama.Ollama), abra o Ollama e baixe um modelo (ollama pull qwen2.5:3b).");
    }

    public async Task<Disponibilidade> Disponivel(CancellationToken ct = default)
    {
        try
        {
            var u = await Url(ct);
            var m = await Tags(u, ct);
            return new Disponibilidade(true, m.Count > 0 ? $"{u} · modelos: {string.Join(", ", m)}" : $"{u} · nenhum modelo baixado (ollama pull qwen2.5:3b)");
        }
        catch (Exception e)
        {
            return new Disponibilidade(false, e.Message);
        }
    }

    public async Task<List<string>> ModelosDisponiveis(CancellationToken ct = default)
    {
        try
        {
            return await Tags(await Url(ct), ct);
        }
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>Um pedido por vez: na CPU, dois ao mesmo tempo só deixam os dois mais lentos.</summary>
    public async Task<Resposta> Atender(Pedido p, Action<string> delta, CancellationToken ct)
    {
        await _fila.WaitAsync(ct);
        try
        {
            return await Conversar(p, delta, ct);
        }
        finally
        {
            _fila.Release();
        }
    }

    private async Task<Resposta> Conversar(Pedido p, Action<string> delta, CancellationToken ct)
    {
        var baseUrl = await Url(ct);
        var corpo = new
        {
            model = p.Persona.Modelo,
            stream = true,
            messages = new[] { new { role = "system", content = p.System }, new { role = "user", content = p.Contexto } },
        };
        HttpResponseMessage res;
        try
        {
            var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/api/chat") { Content = JsonContent.Create(corpo) };
            res = await _http.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ct);
        }
        catch (HttpRequestException e)
        {
            _achada = null;
            throw new InvalidOperationException($"Ollama fora do ar em {baseUrl}: {e.Message}");
        }
        using (res)
        {
            if ((int)res.StatusCode == 404) throw new InvalidOperationException($"o modelo \"{p.Persona.Modelo}\" não está no Ollama: rode `ollama pull {p.Persona.Modelo}`");
            if (!res.IsSuccessStatusCode) throw new InvalidOperationException($"Ollama respondeu {(int)res.StatusCode}: {await res.Content.ReadAsStringAsync(ct)}");
            return await LerStream(await res.Content.ReadAsStreamAsync(ct), delta, ct);
        }
    }

    /// <summary>Lê o NDJSON do /api/chat: o texto aos poucos e, no fim, a contagem de tokens.</summary>
    public static async Task<Resposta> LerStream(Stream corpo, Action<string> delta, CancellationToken ct)
    {
        var texto = new StringBuilder();
        JsonObject? fim = null;
        using var leitor = new StreamReader(corpo, Encoding.UTF8);
        while (await leitor.ReadLineAsync(ct) is { } linha)
        {
            if (string.IsNullOrWhiteSpace(linha)) continue;
            var ev = JsonNode.Parse(linha)!.AsObject();
            if (ev["error"] is { } erro) throw new InvalidOperationException($"Ollama: {erro}");
            var t = (string?)ev["message"]?["content"] ?? "";
            if (t.Length > 0)
            {
                texto.Append(t);
                delta(t);
            }
            if ((bool?)ev["done"] == true) fim = ev;
        }
        var saida = (long?)fim?["eval_count"] ?? 0;
        var segundos = ((double?)fim?["eval_duration"] ?? 0) / 1e9;
        return new Resposta
        {
            Texto = texto.ToString(),
            Uso = new Uso
            {
                Entrada = (long?)fim?["prompt_eval_count"] ?? 0, Saida = saida, Custo = 0,
                TokensPorSegundo = segundos > 0 ? Math.Round(saida / segundos, 1) : null,
            },
        };
    }
}
