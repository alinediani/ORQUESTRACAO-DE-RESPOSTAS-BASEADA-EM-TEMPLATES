using System.Text.Json;
using System.Text.Json.Nodes;

namespace Orquestracao.Core.Diagramas;

/// <summary>
/// Um diagrama guardado em <c>diagram.json</c> (<c>{rev, diagram}</c>). Cada mudança passa por
/// <see cref="Commit"/>: aplica a operação, sobe o <c>rev</c>, salva e avisa quem está ouvindo só
/// com o evento daquela mudança. Equivale ao <c>createApi</c> de legado/server/api.ts.
/// </summary>
public sealed class DiagramaArmazenado
{
    private static readonly JsonSerializerOptions Indentado = new(Json.Opcoes) { WriteIndented = true };

    private readonly string _arquivo;
    private readonly Lock _trava = new();
    private Diagram? _diagrama;

    public Assinantes Ouvintes { get; } = new();

    /// <summary>Chamado depois de cada mudança (ex.: para a lista de diagramas se atualizar).</summary>
    public Action? AoMudar { get; init; }

    /// <summary>Chamado quando um retângulo é removido (ex.: para guardar a persona dele).</summary>
    public Action<string>? AoRemoverNo { get; init; }

    public int Rev { get; private set; }

    public DiagramaArmazenado(string arquivo)
    {
        _arquivo = arquivo;
        (Rev, _diagrama) = Carregar(arquivo);
    }

    /// <summary>Uma cópia do diagrama atual (vazio se ainda não há).</summary>
    public Diagram Diagrama
    {
        get
        {
            lock (_trava) return _diagrama?.Clone() ?? new Diagram();
        }
    }

    /// <summary>True quando ainda não há diagrama nenhum (o GET do código devolve 204).</summary>
    public bool Vazio
    {
        get
        {
            lock (_trava) return _diagrama is null;
        }
    }

    /// <summary>Executa uma operação, salva e avisa os ouvintes. Devolve o evento e o rev novo.</summary>
    public (DiagramEvent Evento, int Rev) Commit(string source, Func<Diagram, DiagramEvent> op)
    {
        DiagramEvent ev;
        int rev;
        lock (_trava)
        {
            // A operação trabalha numa cópia: se ela falhar no meio, o estado não fica pela metade.
            var d = _diagrama?.Clone() ?? new Diagram();
            ev = op(d);
            _diagrama = ev is DiagramReplaced r ? r.Diagram : d;
            Rev++;
            rev = Rev;
            Salvar();
            Ouvintes.Publicar(ComRevESource(ev, rev, source));
        }
        AoMudar?.Invoke();
        if (ev is NodeRemoved removido) AoRemoverNo?.Invoke(removido.Id);
        return (ev, rev);
    }

    /// <summary>Manda um evento aos ouvintes sem mudar o diagrama nem o rev (ex.: <c>edge.pulse</c>).</summary>
    public void Transmitir(object evento) => Ouvintes.Publicar(JsonSerializer.Serialize(evento, Json.Opcoes));

    /// <summary>Assina os eventos; a primeira mensagem é o snapshot (<c>{type:"snapshot", rev, diagram}</c>).</summary>
    public Assinatura Assinar()
    {
        lock (_trava)
        {
            var snapshot = new JsonObject
            {
                ["type"] = "snapshot",
                ["rev"] = Rev,
                ["diagram"] = JsonSerializer.SerializeToNode(_diagrama, Json.Opcoes),
                ["source"] = "server",
            };
            return Ouvintes.Assinar(snapshot.ToJsonString(Json.Opcoes));
        }
    }

    /// <summary>O estado como fica no arquivo: <c>{rev, diagram}</c>.</summary>
    public JsonObject Estado()
    {
        lock (_trava)
            return new JsonObject { ["rev"] = Rev, ["diagram"] = JsonSerializer.SerializeToNode(_diagrama, Json.Opcoes) };
    }

    public (int Nodes, int Edges, int Rev) Info()
    {
        lock (_trava) return (_diagrama?.Nodes.Count ?? 0, _diagrama?.Edges.Count ?? 0, Rev);
    }

    public void Fechar() => Ouvintes.Fechar();

    /// <summary>O evento com <c>rev</c> e <c>source</c> (quem fez a mudança: o id da aba ou "api").</summary>
    private static string ComRevESource(DiagramEvent ev, int rev, string source)
    {
        var obj = JsonSerializer.SerializeToNode(ev, Json.Opcoes)!.AsObject();
        obj["rev"] = rev;
        obj["source"] = source;
        return obj.ToJsonString(Json.Opcoes);
    }

    private void Salvar() => File.WriteAllText(_arquivo, Estado().ToJsonString(Indentado) + "\n");

    public static string Serializar(int rev, Diagram? d) =>
        new JsonObject { ["rev"] = rev, ["diagram"] = JsonSerializer.SerializeToNode(d, Json.Opcoes) }.ToJsonString(Indentado) + "\n";

    /// <summary>Lê o estado salvo. Sem JSON, migra o <c>.mmd</c> de mesmo nome (formato antigo), se existir.</summary>
    private static (int Rev, Diagram? Diagrama) Carregar(string arquivo)
    {
        if (File.Exists(arquivo))
        {
            var raiz = Entrada.Ler(File.ReadAllText(arquivo));
            var rev = raiz.TryGetProperty("rev", out var r) && r.ValueKind == JsonValueKind.Number ? r.GetInt32() : 0;
            var d = raiz.TryGetProperty("diagram", out var dj) && dj.ValueKind == JsonValueKind.Object ? Entrada.Diagrama(dj) : null;
            return (rev, d);
        }
        var antigo = Path.ChangeExtension(arquivo, ".mmd");
        if (antigo != arquivo && File.Exists(antigo))
        {
            var d = Mermaid.Ler(File.ReadAllText(antigo)).Diagram;
            File.WriteAllText(arquivo, Serializar(0, d));
            return (0, d);
        }
        return (0, null);
    }
}
