using System.IO.Compression;
using System.Text;
using Orquestracao.Core.Diagramas;

namespace Orquestracao.Web.Tests;

/// <summary>Portado de legado/server/diagrams.test.ts: vários diagramas, um por pasta.</summary>
public class DiagramasTests : IDisposable
{
    private readonly Servidor _s = new();

    public void Dispose() => _s.Dispose();

    private static byte[] Zip(params (string Nome, string Texto)[] arquivos)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (nome, texto) in arquivos)
            {
                using var s = zip.CreateEntry(nome).Open();
                s.Write(Encoding.UTF8.GetBytes(texto));
            }
        return ms.ToArray();
    }

    [Fact]
    public void Slug_SemAcentosNemEspacos()
    {
        Assert.Equal("fluxo-de-vendas", Workspace.Slugify("Fluxo de Vendas"));
        Assert.Equal("acao-reacao", Workspace.Slugify("  Ação & Reação!! "));
        Assert.Equal("diagrama", Workspace.Slugify(""));
    }

    [Fact]
    public async Task ComecaComUmDiagramaVazio_OuComODiagramJsonDeAntes()
    {
        Assert.Equal(["meu-diagrama"], (await _s.Call("GET", "/diagrams")).Json!.AsArray().Select(d => (string?)d!["slug"]));

        using var s = new Servidor();
        var antigo = Path.Combine(s.Dir, "diagram.json");
        File.WriteAllText(antigo, """{"rev":3,"diagram":{"nodes":[{"id":"n1","text":"Antigo","x":1,"y":2}],"edges":[]}}""");
        using var comAntigo = new Servidor(arquivoAntigo: antigo);
        var lista = (await comAntigo.Call("GET", "/diagrams")).Json!.AsArray();
        Assert.Single(lista);
        Assert.Equal(("diagrama", "Diagrama", 1), ((string?)lista[0]!["slug"], (string?)lista[0]!["name"], (int)lista[0]!["nodes"]!));
        // As rotas antigas, sem prefixo, continuam valendo para o primeiro diagrama.
        Assert.Equal("Antigo", (string?)(await comAntigo.Call("GET", "/nodes")).Json![0]!["text"]);
    }

    [Fact]
    public async Task CadaDiagramaTemAPropriaApiEmDSlug()
    {
        await _s.Call("POST", "/diagrams", new { name = "Vendas" });
        await _s.Call("POST", "/d/vendas/nodes", new { text = "Só aqui" });
        Assert.Single((await _s.Call("GET", "/d/vendas/nodes")).Json!.AsArray());
        Assert.Empty((await _s.Call("GET", "/d/meu-diagrama/nodes")).Json!.AsArray());
        Assert.Equal(404, (await _s.Call("GET", "/d/nada/nodes")).Status);
        Assert.Equal(404, (await _s.Call("GET", "/d/..%2F..%2Fetc/nodes")).Status);
        var vendas = (await _s.Call("GET", "/diagrams")).Json!.AsArray().First(d => (string?)d!["slug"] == "vendas")!;
        Assert.Equal(1, (int)vendas["nodes"]!);
    }

    [Fact]
    public async Task CriarRenomearDuplicarEApagar()
    {
        var criado = (await _s.Call("POST", "/diagrams", new { name = "Fluxo de Vendas" })).Json!;
        Assert.Equal(("fluxo-de-vendas", "Fluxo de Vendas"), ((string?)criado["slug"], (string?)criado["name"]));
        Assert.Equal("fluxo-de-vendas-2", (string?)(await _s.Call("POST", "/diagrams", new { name = "fluxo de vendas" })).Json!["slug"]);
        Assert.Equal(400, (await _s.Call("POST", "/diagrams", new { name = "  " })).Status);

        var renomeado = (await _s.Call("PATCH", "/diagrams/fluxo-de-vendas-2", new { name = "Onboarding" })).Json!;
        Assert.Equal(("onboarding", "Onboarding"), ((string?)renomeado["slug"], (string?)renomeado["name"]));
        Assert.True(File.Exists(Path.Combine(_s.Diagramas, "onboarding", "diagram.json")));

        await _s.Call("POST", "/d/onboarding/nodes", new { text = "Boas-vindas" });
        var copia = (await _s.Call("POST", "/diagrams/onboarding/duplicate")).Json!;
        Assert.Equal(("onboarding-copia", "Onboarding (cópia)", 1), ((string?)copia["slug"], (string?)copia["name"], (int)copia["nodes"]!));

        Assert.Equal(204, (await _s.Call("DELETE", "/diagrams/onboarding-copia")).Status);
        Assert.Single(Directory.GetDirectories(Path.Combine(_s.Diagramas, ".removidos")));
        Assert.Equal(404, (await _s.Call("DELETE", "/diagrams/onboarding-copia")).Status);

        // Apagar o último cria um novo: nunca fica sem diagrama.
        foreach (var d in (await _s.Call("GET", "/diagrams")).Json!.AsArray())
            await _s.Call("DELETE", $"/diagrams/{(string?)d!["slug"]}");
        Assert.Single((await _s.Call("GET", "/diagrams")).Json!.AsArray());
    }

    [Fact]
    public async Task ExportarEImportar_ZipEMmd()
    {
        await _s.Call("POST", "/d/meu-diagrama/nodes", new { text = "A", x = 10, y = 20 });
        await _s.Call("POST", "/d/meu-diagrama/nodes", new { text = "B" });
        await _s.Call("POST", "/d/meu-diagrama/edges", new { from = "A", to = "B", text = "vai" });

        var zip = await _s.Call("GET", "/diagrams/meu-diagrama/export");
        Assert.Equal("application/zip", zip.Http.Content.Headers.ContentType?.MediaType);
        using (var lido = new ZipArchive(new MemoryStream(zip.Bytes)))
            Assert.Equal(["diagram.json", "diagram.mmd", "meta.json"], lido.Entries.Select(e => e.FullName).Order());
        var importado = (await _s.Call("POST", "/diagrams/import?name=Trazido", zip.Bytes)).Json!;
        Assert.Equal(("trazido", 2, 1), ((string?)importado["slug"], (int)importado["nodes"]!, (int)importado["edges"]!));
        Assert.Equal("vai", (string?)(await _s.Call("GET", "/d/trazido/edges")).Json![0]!["text"]);

        var mmd = await _s.Call("GET", "/diagrams/meu-diagrama/export?format=mmd");
        Assert.Contains("-->|\"vai\"|", mmd.Texto);
        var doCodigo = (await _s.Call("POST", "/diagrams/import?name=Do código", mmd.Texto)).Json!;
        Assert.Equal(("do-codigo", 2), ((string?)doCodigo["slug"], (int)doCodigo["nodes"]!));

        Assert.Equal(400, (await _s.Call("POST", "/diagrams/import", "isso não é mermaid")).Status);
        Assert.Equal(400, (await _s.Call("POST", "/diagrams/import", Zip(("leia-me.txt", "oi")))).Status);
        // Só diagram.json/diagram.mmd/meta.json são lidos: um caminho malicioso no zip nunca é gravado.
        var malicioso = Zip(("../../fora.txt", "x"), ("diagram.mmd", "flowchart LR\n    a(\"A\")"));
        Assert.Equal(201, (await _s.Call("POST", "/diagrams/import", malicioso)).Status);
        Assert.False(File.Exists(Path.Combine(_s.Dir, "fora.txt")));
    }

    [Fact]
    public async Task SseDaLista_SnapshotEAListaNovaACadaMudanca()
    {
        using var sse = await _s.Ouvir("/events");
        Assert.Single((await sse.Proximo())["diagrams"]!.AsArray());
        await _s.Call("POST", "/diagrams", new { name = "Novo" });
        Assert.Equal(["Meu diagrama", "Novo"], (await sse.Proximo())["diagrams"]!.AsArray().Select(d => (string?)d!["name"]));
    }
}
