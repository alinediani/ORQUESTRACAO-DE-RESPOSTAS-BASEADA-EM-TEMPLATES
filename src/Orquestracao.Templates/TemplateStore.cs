using System.Collections.Concurrent;
using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Orquestracao.Templates;

/// <summary>R02/R04: a base chave-valor — chave da estrutura → template + peso.</summary>
public interface ITemplateStore
{
    TemplateResposta? Obter(string chave);

    /// <summary>Insere ou substitui o template da chave.</summary>
    void Salvar(TemplateResposta t);

    void Remover(string chave);

    IReadOnlyList<TemplateResposta> Listar();
}

internal static class Agora
{
    public static string Iso() => DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}

/// <summary>A base em memória (testes e experimento).</summary>
public sealed class TemplateStoreMemoria : ITemplateStore
{
    private readonly ConcurrentDictionary<string, TemplateResposta> _t = new();

    public TemplateResposta? Obter(string chave) => _t.GetValueOrDefault(chave);

    public void Salvar(TemplateResposta t) => _t[t.Chave] = t with { CriadoEm = t.CriadoEm.Length > 0 ? t.CriadoEm : Agora.Iso(), AtualizadoEm = Agora.Iso() };

    public void Remover(string chave) => _t.TryRemove(chave, out _);

    public IReadOnlyList<TemplateResposta> Listar() => _t.Values.OrderBy(t => t.Chave, StringComparer.Ordinal).ToList();
}

/// <summary>Os totais gravados, para recompor as estatísticas (R07) depois de reiniciar.</summary>
public sealed record TotaisRegistrados(
    int Perguntas, int AcertosTemplate, int ChamadasGeracao, int ChamadasParser, long TokensEntrada, long TokensSaida,
    double Custo, double LatenciaTotalMs, int TemplatesSalvos, int FeedbacksBons, int FeedbacksRuins, int TemplatesDescartados);

/// <summary>O histórico do orquestrador: cada pergunta respondida e cada feedback (a prova do experimento).</summary>
public interface IRegistroDePerguntas
{
    void Registrar(ResultadoOrquestrador r);

    void RegistrarFeedback(string chave, bool bom, double? pesoNovo, bool descartou);

    TotaisRegistrados Totais();
}

/// <summary>
/// A base em SQLite (o app): um arquivo com a tabela <c>templates</c> (a base chave-valor) e o histórico,
/// <c>perguntas</c> e <c>feedbacks</c>, de onde as estatísticas são recompostas ao reiniciar.
/// </summary>
public sealed class TemplateStoreSqlite : ITemplateStore, IRegistroDePerguntas
{
    private readonly string _conexao;

    public TemplateStoreSqlite(string arquivo)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(arquivo))!);
        _conexao = new SqliteConnectionStringBuilder { DataSource = arquivo, Pooling = false }.ToString();
        Executar("""
            CREATE TABLE IF NOT EXISTS templates (
                chave TEXT PRIMARY KEY,
                template TEXT NOT NULL,
                peso REAL NOT NULL,
                usos INTEGER NOT NULL DEFAULT 0,
                bons INTEGER NOT NULL DEFAULT 0,
                ruins INTEGER NOT NULL DEFAULT 0,
                criado_em TEXT NOT NULL,
                atualizado_em TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS perguntas (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                em TEXT NOT NULL,
                pergunta TEXT NOT NULL,
                resposta TEXT NOT NULL,
                caminho TEXT NOT NULL,
                chave TEXT NOT NULL,
                entidade TEXT,
                template TEXT,
                peso REAL,
                motivo TEXT,
                template_salvo INTEGER NOT NULL,
                chamadas_geracao INTEGER NOT NULL,
                chamadas_parser INTEGER NOT NULL,
                tokens_entrada INTEGER NOT NULL,
                tokens_saida INTEGER NOT NULL,
                custo REAL NOT NULL,
                latencia_ms REAL NOT NULL
            );
            CREATE TABLE IF NOT EXISTS feedbacks (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                em TEXT NOT NULL,
                chave TEXT NOT NULL,
                bom INTEGER NOT NULL,
                peso_novo REAL,
                descartado INTEGER NOT NULL
            )
            """);
    }

    private SqliteConnection Abrir()
    {
        var c = new SqliteConnection(_conexao);
        c.Open();
        return c;
    }

    private void Executar(string sql, params (string Nome, object? Valor)[] parametros)
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (nome, valor) in parametros) cmd.Parameters.AddWithValue(nome, valor ?? DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static TemplateResposta Ler(SqliteDataReader r) => new()
    {
        Chave = r.GetString(0), Template = r.GetString(1), Peso = r.GetDouble(2), Usos = r.GetInt32(3),
        FeedbacksBons = r.GetInt32(4), FeedbacksRuins = r.GetInt32(5), CriadoEm = r.GetString(6), AtualizadoEm = r.GetString(7),
    };

    private const string Colunas = "chave, template, peso, usos, bons, ruins, criado_em, atualizado_em";

    public TemplateResposta? Obter(string chave)
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {Colunas} FROM templates WHERE chave = $chave";
        cmd.Parameters.AddWithValue("$chave", chave);
        using var r = cmd.ExecuteReader();
        return r.Read() ? Ler(r) : null;
    }

    public void Salvar(TemplateResposta t) => Executar(
        $"INSERT INTO templates ({Colunas}) VALUES ($chave, $template, $peso, $usos, $bons, $ruins, $criado, $atualizado) " +
        "ON CONFLICT(chave) DO UPDATE SET template = excluded.template, peso = excluded.peso, usos = excluded.usos, bons = excluded.bons, ruins = excluded.ruins, atualizado_em = excluded.atualizado_em",
        ("$chave", t.Chave), ("$template", t.Template), ("$peso", t.Peso), ("$usos", t.Usos), ("$bons", t.FeedbacksBons), ("$ruins", t.FeedbacksRuins),
        ("$criado", t.CriadoEm.Length > 0 ? t.CriadoEm : Agora.Iso()), ("$atualizado", Agora.Iso()));

    public void Remover(string chave) => Executar("DELETE FROM templates WHERE chave = $chave", ("$chave", chave));

    public IReadOnlyList<TemplateResposta> Listar()
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = $"SELECT {Colunas} FROM templates ORDER BY chave";
        using var r = cmd.ExecuteReader();
        var lista = new List<TemplateResposta>();
        while (r.Read()) lista.Add(Ler(r));
        return lista;
    }

    // ---- o histórico (IRegistroDePerguntas) -----------------------------------

    public void Registrar(ResultadoOrquestrador r) => Executar(
        "INSERT INTO perguntas (em, pergunta, resposta, caminho, chave, entidade, template, peso, motivo, template_salvo, chamadas_geracao, chamadas_parser, tokens_entrada, tokens_saida, custo, latencia_ms) " +
        "VALUES ($em, $pergunta, $resposta, $caminho, $chave, $entidade, $template, $peso, $motivo, $salvo, $geracao, $parser, $entrada, $saida, $custo, $latencia)",
        ("$em", Agora.Iso()), ("$pergunta", r.Pergunta), ("$resposta", r.Texto), ("$caminho", r.Caminho), ("$chave", r.Chave), ("$entidade", r.Entidade),
        ("$template", r.Template), ("$peso", r.Peso), ("$motivo", r.Motivo), ("$salvo", r.TemplateSalvo ? 1 : 0), ("$geracao", r.ChamadasGeracao),
        ("$parser", r.ChamadasParser), ("$entrada", r.TokensEntrada), ("$saida", r.TokensSaida), ("$custo", r.Custo), ("$latencia", r.LatenciaMs));

    public void RegistrarFeedback(string chave, bool bom, double? pesoNovo, bool descartou) => Executar(
        "INSERT INTO feedbacks (em, chave, bom, peso_novo, descartado) VALUES ($em, $chave, $bom, $peso, $descartado)",
        ("$em", Agora.Iso()), ("$chave", chave), ("$bom", bom ? 1 : 0), ("$peso", pesoNovo), ("$descartado", descartou ? 1 : 0));

    public TotaisRegistrados Totais()
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*), COALESCE(SUM(caminho = 'template'), 0), COALESCE(SUM(chamadas_geracao), 0), COALESCE(SUM(chamadas_parser), 0),
                   COALESCE(SUM(tokens_entrada), 0), COALESCE(SUM(tokens_saida), 0), COALESCE(SUM(custo), 0), COALESCE(SUM(latencia_ms), 0),
                   COALESCE(SUM(template_salvo), 0),
                   (SELECT COALESCE(SUM(bom), 0) FROM feedbacks), (SELECT COALESCE(SUM(1 - bom), 0) FROM feedbacks),
                   (SELECT COALESCE(SUM(descartado), 0) FROM feedbacks)
            FROM perguntas
            """;
        using var r = cmd.ExecuteReader();
        r.Read();
        return new TotaisRegistrados(r.GetInt32(0), r.GetInt32(1), r.GetInt32(2), r.GetInt32(3), r.GetInt64(4), r.GetInt64(5),
            r.GetDouble(6), r.GetDouble(7), r.GetInt32(8), r.GetInt32(9), r.GetInt32(10), r.GetInt32(11));
    }
}
