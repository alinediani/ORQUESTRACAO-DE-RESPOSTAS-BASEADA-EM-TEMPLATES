using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Orquestracao.Core.Diagramas;

namespace Orquestracao.Web;

/// <summary>Ajudantes de HTTP comuns às rotas: respostas JSON/texto, corpo da requisição e SSE.</summary>
public static class Http
{
    private static readonly JsonSerializerOptions Indentado = new(Json.Opcoes) { WriteIndented = true };

    /// <summary>Quem fez a mudança: o id da aba (cabeçalho X-Client-Id) ou "api".</summary>
    public static string Source(HttpContext ctx) =>
        ctx.Request.Headers["X-Client-Id"] is { Count: > 0 } h && !string.IsNullOrEmpty(h[0]) ? h[0]! : "api";

    public static async Task Enviar(HttpContext ctx, int status, object? corpo = null, int? rev = null)
    {
        ctx.Response.StatusCode = status;
        if (rev is not null) ctx.Response.Headers["X-Rev"] = rev.Value.ToString();
        switch (corpo)
        {
            case null:
                return;
            case string texto:
                ctx.Response.ContentType = "text/plain; charset=utf-8";
                await ctx.Response.WriteAsync(texto.EndsWith('\n') ? texto : texto + "\n");
                return;
            default:
                ctx.Response.ContentType = "application/json; charset=utf-8";
                // Eventos vão pelo tipo base, para levar o campo "type".
                var tipo = corpo is DiagramEvent ? typeof(DiagramEvent) : corpo.GetType();
                await ctx.Response.WriteAsync(JsonSerializer.Serialize(corpo, tipo, Indentado) + "\n");
                return;
        }
    }

    public static async Task<string> LerTexto(HttpContext ctx, int limite = 1024 * 1024)
    {
        var bytes = await LerBytes(ctx, limite);
        return Encoding.UTF8.GetString(bytes);
    }

    public static async Task<byte[]> LerBytes(HttpContext ctx, int limite)
    {
        using var ms = new MemoryStream();
        var buffer = new byte[81920];
        int lidos;
        while ((lidos = await ctx.Request.Body.ReadAsync(buffer, ctx.RequestAborted)) > 0)
        {
            if (ms.Length + lidos > limite) throw new OpError(400, $"O arquivo passa do limite de {limite / 1024 / 1024} MB.");
            ms.Write(buffer, 0, lidos);
        }
        return ms.ToArray();
    }

    /// <summary>O corpo como JSON; vazio vale <c>{}</c>.</summary>
    public static async Task<JsonElement> LerJson(HttpContext ctx)
    {
        var texto = await LerTexto(ctx);
        if (string.IsNullOrWhiteSpace(texto)) texto = "{}";
        try
        {
            using var doc = JsonDocument.Parse(texto);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new OpError(400, "Corpo não é um JSON válido.");
        }
    }

    /// <summary>Server-Sent Events: cada mensagem da assinatura vira um <c>data: …</c>.</summary>
    public static async Task Sse(HttpContext ctx, Assinatura assinatura)
    {
        using (assinatura)
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
            await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
            try
            {
                await foreach (var msg in assinatura.Mensagens.ReadAllAsync(ctx.RequestAborted))
                {
                    await ctx.Response.WriteAsync($"data: {msg}\n\n", ctx.RequestAborted);
                    await ctx.Response.Body.FlushAsync(ctx.RequestAborted);
                }
            }
            catch (OperationCanceledException)
            {
                // A aba fechou.
            }
        }
    }
}
