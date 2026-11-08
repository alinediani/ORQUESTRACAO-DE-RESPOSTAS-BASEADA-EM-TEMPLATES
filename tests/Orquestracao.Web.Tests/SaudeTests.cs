using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Orquestracao.Web.Tests;

public class SaudeTests(WebApplicationFactory<Program> fabrica) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task ApiSaude_RespondeOk()
    {
        var resposta = await _cliente.GetAsync("/api/saude");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Contains("\"ok\":true", await resposta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Raiz_ServeOFrontDeWwwroot()
    {
        var resposta = await _cliente.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal("text/html", resposta.Content.Headers.ContentType?.MediaType);
    }
}
