using System.Text;
using System.Text.Json;
using GescomSaas.Domain.Exceptions;
using GescomSaas.Web.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace GescomSaas.Tests.Middleware;

/// <summary>
/// Tests unitaires sur GlobalExceptionMiddleware : verifient que chaque
/// AppException est correctement transformee en ProblemDetails RFC 7807
/// avec le bon statut HTTP, le bon errorCode et les metadonnees attendues.
/// </summary>
public class GlobalExceptionMiddlewareTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private static GlobalExceptionMiddleware CreateMiddleware(RequestDelegate next, string envName = "Production")
    {
        var envMock = new Mock<IHostEnvironment>();
        envMock.SetupGet(x => x.EnvironmentName).Returns(envName);
        return new GlobalExceptionMiddleware(next, NullLogger<GlobalExceptionMiddleware>.Instance, envMock.Object);
    }

    private static HttpContext BuildContext(string path = "/api/test", string accept = "application/json")
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Path = path;
        ctx.Request.Headers.Accept = accept;
        ctx.Response.Body = new MemoryStream();
        return ctx;
    }

    private static async Task<JsonElement> ReadProblemDetailsAsync(HttpContext ctx)
    {
        ctx.Response.Body.Position = 0;
        using var reader = new StreamReader(ctx.Response.Body, Encoding.UTF8);
        var body = await reader.ReadToEndAsync();
        return JsonSerializer.Deserialize<JsonElement>(body, JsonOptions);
    }

    [Fact]
    public async Task NotFoundException_ProduitProblemDetails404AvecErrorCode()
    {
        var middleware = CreateMiddleware(_ => throw new NotFoundException("BusinessPartner", 42));
        var ctx = BuildContext();

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(404);
        ctx.Response.ContentType.Should().Be("application/problem+json");

        var doc = await ReadProblemDetailsAsync(ctx);
        doc.GetProperty("status").GetInt32().Should().Be(404);
        doc.GetProperty("errorCode").GetString().Should().Be("NOT_FOUND");
        doc.GetProperty("entity").GetString().Should().Be("BusinessPartner");
        doc.GetProperty("title").GetString().Should().Be("Ressource introuvable");
        doc.GetProperty("correlationId").GetString().Should().NotBeNullOrEmpty();
        doc.GetProperty("instance").GetString().Should().Be("/api/test");
    }

    [Fact]
    public async Task BusinessRuleException_Produit422AvecCodePersonnalise()
    {
        var middleware = CreateMiddleware(_ => throw new BusinessRuleException(
            "Transition interdite.",
            errorCode: "DOC_INVALID_TRANSITION"));
        var ctx = BuildContext();

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(422);
        var doc = await ReadProblemDetailsAsync(ctx);
        doc.GetProperty("errorCode").GetString().Should().Be("DOC_INVALID_TRANSITION");
        doc.GetProperty("detail").GetString().Should().Be("Transition interdite.");
    }

    [Fact]
    public async Task QuotaExceededException_Produit402AvecMetadonneesQuota()
    {
        var middleware = CreateMiddleware(_ => throw new QuotaExceededException("users", limit: 10, current: 11));
        var ctx = BuildContext();

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(402);
        var doc = await ReadProblemDetailsAsync(ctx);
        doc.GetProperty("errorCode").GetString().Should().Be("QUOTA_EXCEEDED");
        doc.GetProperty("quotaName").GetString().Should().Be("users");
        doc.GetProperty("limit").GetInt32().Should().Be(10);
        doc.GetProperty("current").GetInt32().Should().Be(11);
        doc.GetProperty("title").GetString().Should().Be("Quota du plan depasse");
    }

    [Fact]
    public async Task TenantAccessDeniedException_Produit403()
    {
        var middleware = CreateMiddleware(_ => throw new TenantAccessDeniedException());
        var ctx = BuildContext();

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(403);
        var doc = await ReadProblemDetailsAsync(ctx);
        doc.GetProperty("errorCode").GetString().Should().Be("TENANT_ACCESS_DENIED");
    }

    [Fact]
    public async Task ValidationException_Produit400ValidationProblemDetails()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["Email"] = new[] { "Format invalide.", "Domaine non autorise." },
            ["Name"] = new[] { "Le nom est requis." },
        };
        var middleware = CreateMiddleware(_ => throw new ValidationException(errors));
        var ctx = BuildContext();

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(400);
        var doc = await ReadProblemDetailsAsync(ctx);
        doc.GetProperty("status").GetInt32().Should().Be(400);
        doc.GetProperty("title").GetString().Should().Be("Donnees invalides");

        var errorsNode = doc.GetProperty("errors");
        errorsNode.GetProperty("Email").GetArrayLength().Should().Be(2);
        errorsNode.GetProperty("Email").EnumerateArray().First().GetString().Should().Be("Format invalide.");
        errorsNode.GetProperty("Name").EnumerateArray().Single().GetString().Should().Be("Le nom est requis.");
    }

    [Fact]
    public async Task ExceptionNonAppException_SurApi_Produit500AvecInternalError()
    {
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Boom interne"));
        var ctx = BuildContext(path: "/api/foo");

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(500);
        var doc = await ReadProblemDetailsAsync(ctx);
        doc.GetProperty("errorCode").GetString().Should().Be("INTERNAL_ERROR");
        // Detail generique en Production : pas de fuite de stack trace
        doc.GetProperty("detail").GetString().Should().NotContain("InvalidOperationException");
    }

    [Fact]
    public async Task ExceptionNonAppException_EnDevelopment_ExposeStackTrace()
    {
        var middleware = CreateMiddleware(
            _ => throw new InvalidOperationException("Boom interne"),
            envName: "Development");
        var ctx = BuildContext(path: "/api/foo");

        await middleware.InvokeAsync(ctx);

        var doc = await ReadProblemDetailsAsync(ctx);
        doc.GetProperty("detail").GetString().Should().Contain("InvalidOperationException");
        doc.GetProperty("detail").GetString().Should().Contain("Boom interne");
    }

    [Fact]
    public async Task ExceptionRazor_SansAcceptJson_NonGeree_PropageVersExceptionHandlerEnAval()
    {
        // Sur un chemin Razor non-API et sans Accept: application/json,
        // le middleware doit laisser passer l'exception pour que UseExceptionHandler
        // ("/Error") en aval prenne le relais.
        var middleware = CreateMiddleware(_ => throw new InvalidOperationException("Boom"));
        var ctx = BuildContext(path: "/Pages/Index", accept: "text/html");

        var act = async () => await middleware.InvokeAsync(ctx);

        await act.Should().ThrowAsync<InvalidOperationException>();
        // Et la reponse n'a pas ete touchee
        ctx.Response.StatusCode.Should().Be(200); // valeur par defaut
    }

    [Fact]
    public async Task AppException_SurCheminRazor_EstQuandMemeGeree()
    {
        // Une AppException doit etre geree partout, pas seulement sur /api,
        // car elle a une semantique metier explicite.
        var middleware = CreateMiddleware(_ => throw new NotFoundException("Tenant", Guid.NewGuid()));
        var ctx = BuildContext(path: "/Pages/Tenants", accept: "text/html");

        await middleware.InvokeAsync(ctx);

        ctx.Response.StatusCode.Should().Be(404);
        var doc = await ReadProblemDetailsAsync(ctx);
        doc.GetProperty("errorCode").GetString().Should().Be("NOT_FOUND");
    }

    [Fact]
    public async Task SansException_LeMiddlewareEstTransparent()
    {
        var nextWasCalled = false;
        RequestDelegate next = ctx =>
        {
            nextWasCalled = true;
            ctx.Response.StatusCode = 200;
            return Task.CompletedTask;
        };
        var middleware = CreateMiddleware(next);
        var ctx = BuildContext();

        await middleware.InvokeAsync(ctx);

        nextWasCalled.Should().BeTrue();
        ctx.Response.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task TimestampEtCorrelationId_SontInclusDansChaqueReponse()
    {
        var middleware = CreateMiddleware(_ => throw new BusinessRuleException("X"));
        var ctx = BuildContext();
        ctx.TraceIdentifier = "trace-abc-123";

        await middleware.InvokeAsync(ctx);

        var doc = await ReadProblemDetailsAsync(ctx);
        doc.GetProperty("correlationId").GetString().Should().Be("trace-abc-123");
        doc.TryGetProperty("timestamp", out var timestamp).Should().BeTrue();
        timestamp.GetDateTimeOffset().Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }
}
