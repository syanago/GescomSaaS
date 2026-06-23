using Serilog.Context;

namespace GescomSaas.Web.Middleware;

/// <summary>
/// Etablit un identifiant de correlation par requete et le propage a la fois
/// dans les logs (via LogContext) et dans la reponse HTTP (header X-Correlation-Id).
///
/// L'identifiant peut venir de l'amont (header X-Correlation-Id transmis par un
/// reverse proxy / load balancer) ou etre genere localement. Dans tous les cas
/// il est aligne avec context.TraceIdentifier pour etre visible dans le middleware
/// d'exception et dans les logs Serilog.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ResolveCorrelationId(context);
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        // Tous les logs emis dans la suite du pipeline porteront automatiquement
        // la propriete CorrelationId, ce qui permet de relier tous les events
        // d'une meme requete dans Seq / Elastic / Loki.
        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var fromHeader)
            && !string.IsNullOrWhiteSpace(fromHeader))
        {
            return fromHeader.ToString();
        }

        return context.TraceIdentifier;
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
        => app.UseMiddleware<CorrelationIdMiddleware>();
}
