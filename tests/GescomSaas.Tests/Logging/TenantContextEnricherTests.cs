using System.Security.Claims;
using GescomSaas.Web.Logging;
using Microsoft.AspNetCore.Http;
using Serilog;
using Serilog.Events;

namespace GescomSaas.Tests.Logging;

/// <summary>
/// Tests sur TenantContextEnricher : verifient que TenantId, UserId et UserName
/// sont correctement extraits des claims de l'utilisateur authentifie.
/// </summary>
public class TenantContextEnricherTests
{
    private static (TenantContextEnricher enricher, IHttpContextAccessor accessor) BuildEnricher()
    {
        var accessor = new HttpContextAccessor();
        return (new TenantContextEnricher(accessor), accessor);
    }

    private static List<LogEvent> CaptureLogsWith(TenantContextEnricher enricher, string message)
    {
        var captured = new List<LogEvent>();
        using var logger = new LoggerConfiguration()
            .Enrich.With(enricher)
            .WriteTo.Sink(new InMemorySink(captured))
            .CreateLogger();

        logger.Information(message);
        return captured;
    }

    [Fact]
    public void Enrich_UtilisateurAuthentifieAvecTenant_AjouteTenantIdEtUserId()
    {
        var (enricher, accessor) = BuildEnricher();
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("tenant_id", "tenant-abc-123"),
            new Claim(ClaimTypes.NameIdentifier, "user-456"),
            new Claim(ClaimTypes.Name, "owner@demo.local"),
        }, authenticationType: "Test"));
        accessor.HttpContext = ctx;

        var logs = CaptureLogsWith(enricher, "test message");

        logs.Should().HaveCount(1);
        var props = logs[0].Properties;
        props["TenantId"].ToString().Trim('"').Should().Be("tenant-abc-123");
        props["UserId"].ToString().Trim('"').Should().Be("user-456");
        props["UserName"].ToString().Trim('"').Should().Be("owner@demo.local");
    }

    [Fact]
    public void Enrich_UtilisateurNonAuthentifie_NajoutePasDeProprietes()
    {
        var (enricher, accessor) = BuildEnricher();
        accessor.HttpContext = new DefaultHttpContext(); // User anonyme

        var logs = CaptureLogsWith(enricher, "test");

        logs[0].Properties.Should().NotContainKey("TenantId");
        logs[0].Properties.Should().NotContainKey("UserId");
    }

    [Fact]
    public void Enrich_AuthentifieSansTenantClaim_AjouteUserIdMaisPasTenantId()
    {
        var (enricher, accessor) = BuildEnricher();
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "platform-admin"),
            new Claim(ClaimTypes.Name, "platform@x.com"),
        }, authenticationType: "Test"));
        accessor.HttpContext = ctx;

        var logs = CaptureLogsWith(enricher, "test");

        logs[0].Properties.Should().ContainKey("UserId");
        logs[0].Properties.Should().NotContainKey("TenantId");
    }

    [Fact]
    public void Enrich_HttpContextAbsent_NeJettePasEtNajoutePasDeProprietes()
    {
        var enricher = new TenantContextEnricher(new HttpContextAccessor()); // HttpContext null

        var act = () => CaptureLogsWith(enricher, "test");

        var logs = act.Should().NotThrow().Subject;
        logs[0].Properties.Should().NotContainKey("TenantId");
    }

    private sealed class InMemorySink : Serilog.Core.ILogEventSink
    {
        private readonly List<LogEvent> _events;
        public InMemorySink(List<LogEvent> events) => _events = events;
        public void Emit(LogEvent logEvent) => _events.Add(logEvent);
    }
}
