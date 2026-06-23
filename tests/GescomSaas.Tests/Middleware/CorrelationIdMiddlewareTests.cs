using GescomSaas.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Serilog.Context;

namespace GescomSaas.Tests.Middleware;

/// <summary>
/// Tests sur CorrelationIdMiddleware : verifient que l'identifiant est
/// correctement etabli, propage en header de reponse, et qu'il accepte
/// les valeurs entrantes (reverse proxy).
/// </summary>
public class CorrelationIdMiddlewareTests
{
    private static HttpContext BuildContext(string? incomingHeader = null)
    {
        var ctx = new DefaultHttpContext();
        if (incomingHeader is not null)
        {
            ctx.Request.Headers[CorrelationIdMiddleware.HeaderName] = incomingHeader;
        }
        return ctx;
    }

    [Fact]
    public async Task SansHeaderEntrant_GenereUnCorrelationIdLocal()
    {
        var ctx = BuildContext();
        string? observedTraceId = null;
        var middleware = new CorrelationIdMiddleware(c =>
        {
            observedTraceId = c.TraceIdentifier;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(ctx);

        observedTraceId.Should().NotBeNullOrEmpty();
        ctx.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            .Should().Be(observedTraceId);
    }

    [Fact]
    public async Task AvecHeaderEntrant_LeReutilise()
    {
        var ctx = BuildContext(incomingHeader: "external-trace-xyz-42");
        string? observedTraceId = null;
        var middleware = new CorrelationIdMiddleware(c =>
        {
            observedTraceId = c.TraceIdentifier;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(ctx);

        observedTraceId.Should().Be("external-trace-xyz-42");
        ctx.TraceIdentifier.Should().Be("external-trace-xyz-42");
        ctx.Response.Headers[CorrelationIdMiddleware.HeaderName].ToString()
            .Should().Be("external-trace-xyz-42");
    }

    [Fact]
    public async Task PendantLeNext_LogContextContientLeCorrelationId()
    {
        var ctx = BuildContext(incomingHeader: "trace-pendant-next");

        // On capture la valeur de la propriete CorrelationId visible dans le LogContext
        // au moment ou le pipeline aval s'execute. Serilog la relit depuis ce contexte
        // pour enrichir tous les events emis dans cette portee.
        string? capturedFromLogContext = null;
        var middleware = new CorrelationIdMiddleware(c =>
        {
            // LogContext stocke en AsyncLocal : on peut sonder via un logger ephemere
            using var sink = new CaptureSink();
            using var logger = new Serilog.LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Sink(sink)
                .CreateLogger();

            logger.Information("ping");
            if (sink.Events.Count > 0
                && sink.Events[0].Properties.TryGetValue("CorrelationId", out var prop))
            {
                capturedFromLogContext = prop.ToString().Trim('"');
            }
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(ctx);

        capturedFromLogContext.Should().Be("trace-pendant-next");
    }

    [Fact]
    public async Task ApresLeNext_LogContextEstNettoye()
    {
        var ctx = BuildContext(incomingHeader: "trace-cleanup");
        var middleware = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await middleware.InvokeAsync(ctx);

        // Hors de la portee de la requete, on ne doit pas voir CorrelationId persister
        using var sink = new CaptureSink();
        using var logger = new Serilog.LoggerConfiguration()
            .Enrich.FromLogContext()
            .WriteTo.Sink(sink)
            .CreateLogger();

        logger.Information("apres requete");

        sink.Events.Should().HaveCount(1);
        sink.Events[0].Properties.Should().NotContainKey("CorrelationId");
    }

    private sealed class CaptureSink : Serilog.Core.ILogEventSink, IDisposable
    {
        public List<Serilog.Events.LogEvent> Events { get; } = new();
        public void Emit(Serilog.Events.LogEvent logEvent) => Events.Add(logEvent);
        public void Dispose() { }
    }
}
