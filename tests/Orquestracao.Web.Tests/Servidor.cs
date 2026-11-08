using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Orquestracao.Web.Tests;

/// <summary>
/// O servidor de verdade (o Program do Orquestracao.Web) numa pasta temporária, com um cliente HTTP
/// e ajudantes para chamar a API e ler o SSE. Cada teste cria o seu e o descarta no fim.
/// </summary>
public sealed class Servidor : IDisposable
{
    public string Dir { get; } = Directory.CreateTempSubdirectory("orquestracao-").FullName;
    public string Diagramas => Path.Combine(Dir, "diagrams");

    private readonly WebApplicationFactory<Program> _fabrica;
    public HttpClient Cliente { get; }

    public Servidor(string? arquivoAntigo = null)
    {
        _fabrica = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Diagramas:Pasta", Diagramas);
            if (arquivoAntigo is not null) b.UseSetting("Diagramas:ArquivoAntigo", arquivoAntigo);
        });
        Cliente = _fabrica.CreateClient();
    }

    public sealed record Resposta(int Status, int? Rev, string Texto, JsonNode? Json, byte[] Bytes, HttpResponseMessage Http);

    /// <summary>Chama a API. <paramref name="corpo"/>: texto (vai como está), bytes, ou objeto (vira JSON).</summary>
    public async Task<Resposta> Call(string metodo, string rota, object? corpo = null, string? clientId = null)
    {
        var req = new HttpRequestMessage(new HttpMethod(metodo), "/api" + rota);
        req.Content = corpo switch
        {
            null => null,
            string s => new StringContent(s, Encoding.UTF8, "application/json"),
            byte[] b => new ByteArrayContent(b),
            _ => new StringContent(System.Text.Json.JsonSerializer.Serialize(corpo), Encoding.UTF8, "application/json"),
        };
        if (clientId is not null) req.Headers.Add("X-Client-Id", clientId);
        var res = await Cliente.SendAsync(req);
        var bytes = await res.Content.ReadAsByteArrayAsync();
        var texto = Encoding.UTF8.GetString(bytes);
        JsonNode? json = null;
        try
        {
            json = texto.Length > 0 ? JsonNode.Parse(texto) : null;
        }
        catch (System.Text.Json.JsonException) { }
        int? rev = res.Headers.TryGetValues("X-Rev", out var v) ? int.Parse(v.First()) : null;
        return new Resposta((int)res.StatusCode, rev, texto, json, bytes, res);
    }

    /// <summary>Abre um SSE; <see cref="Eventos.Proximo"/> devolve a próxima mensagem já em JSON.</summary>
    public async Task<Eventos> Ouvir(string rota)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, "/api" + rota);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/event-stream"));
        var res = await Cliente.SendAsync(req, HttpCompletionOption.ResponseHeadersRead);
        return new Eventos(res, new StreamReader(await res.Content.ReadAsStreamAsync()));
    }

    public sealed class Eventos(HttpResponseMessage res, StreamReader leitor) : IDisposable
    {
        public HttpResponseMessage Http { get; } = res;

        public async Task<JsonObject> Proximo(int timeoutMs = 5000)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var dados = new StringBuilder();
            while (true)
            {
                var linha = await leitor.ReadLineAsync(cts.Token) ?? throw new EndOfStreamException("SSE fechou.");
                if (linha.Length == 0 && dados.Length > 0) return JsonNode.Parse(dados.ToString())!.AsObject();
                if (linha.StartsWith("data: ")) dados.Append(linha["data: ".Length..]);
            }
        }

        public void Dispose()
        {
            leitor.Dispose();
            Http.Dispose();
        }
    }

    public void Dispose()
    {
        Cliente.Dispose();
        _fabrica.Dispose();
        try
        {
            Directory.Delete(Dir, recursive: true);
        }
        catch (IOException) { }
    }
}
