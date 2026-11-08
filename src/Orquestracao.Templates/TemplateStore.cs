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

/// <summary>A base em SQLite (o app): um arquivo, uma tabela <c>templates</c>.</summary>
public sealed class TemplateStoreSqlite : ITemplateStore
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
            )
            """);
    }

    private SqliteConnection Abrir()
    {
        var c = new SqliteConnection(_conexao);
        c.Open();
        return c;
    }

    private void Executar(string sql, params (string Nome, object Valor)[] parametros)
    {
        using var c = Abrir();
        using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        foreach (var (nome, valor) in parametros) cmd.Parameters.AddWithValue(nome, valor);
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
}
