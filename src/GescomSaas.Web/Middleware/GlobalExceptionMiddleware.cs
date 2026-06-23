using System.Text.Json;
using GescomSaas.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace GescomSaas.Web.Middleware;

/// <summary>
/// Middleware global qui transforme les exceptions metier (AppException) et techniques
/// en reponses ProblemDetails RFC 7807 pour les endpoints API/JSON.
/// Les requetes HTML continuent a passer par UseExceptionHandler("/Error") en aval.
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (ShouldHandle(context, ex))
        {
            await HandleAsync(context, ex);
        }
    }

    private static bool ShouldHandle(HttpContext context, Exception ex)
    {
        if (context.Response.HasStarted)
        {
            return false;
        }

        // On gere uniquement les exceptions metier ici, ou les requetes API/JSON.
        if (ex is AppException)
        {
            return true;
        }

        var path = context.Request.Path.Value ?? string.Empty;
        if (path.StartsWith("/api", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var accept = context.Request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private async Task HandleAsync(HttpContext context, Exception ex)
    {
        var (status, problem) = BuildProblemDetails(context, ex);

        if (status >= 500)
        {
            _logger.LogError(ex, "Exception non geree (correlation {CorrelationId})", problem.Extensions["correlationId"]);
        }
        else
        {
            _logger.LogWarning(ex, "Exception metier (correlation {CorrelationId})", problem.Extensions["correlationId"]);
        }

        context.Response.Clear();
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/problem+json";

        // IMPORTANT : passer problem.GetType() pour que System.Text.Json serialise
        // selon le type runtime. Sans ca, les sous-types comme ValidationProblemDetails
        // perdraient leurs proprietes specifiques (Errors) lors de la serialisation.
        await context.Response.WriteAsync(
            JsonSerializer.Serialize(problem, problem.GetType(), JsonOptions),
            context.RequestAborted);
    }

    private (int Status, ProblemDetails Problem) BuildProblemDetails(HttpContext context, Exception ex)
    {
        var correlationId = context.TraceIdentifier;

        ProblemDetails problem;
        int status;

        switch (ex)
        {
            case ValidationException validation:
                status = validation.HttpStatusCode;
                problem = new ValidationProblemDetails(validation.Errors.ToDictionary(e => e.Key, e => e.Value))
                {
                    Title = "Donnees invalides",
                    Detail = validation.Message,
                    Status = status,
                };
                break;

            case AppException app:
                status = app.HttpStatusCode;
                problem = new ProblemDetails
                {
                    Title = TitleFor(status),
                    Detail = app.Message,
                    Status = status,
                };
                problem.Extensions["errorCode"] = app.ErrorCode;
                AddExtraDetails(problem, app);
                break;

            default:
                status = StatusCodes.Status500InternalServerError;
                problem = new ProblemDetails
                {
                    Title = "Erreur interne",
                    Detail = _environment.IsDevelopment() ? ex.ToString() : "Une erreur est survenue. Reessayez plus tard.",
                    Status = status,
                };
                problem.Extensions["errorCode"] = "INTERNAL_ERROR";
                break;
        }

        problem.Type = $"https://httpstatuses.com/{status}";
        problem.Instance = context.Request.Path;
        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["timestamp"] = DateTimeOffset.UtcNow;

        return (status, problem);
    }

    private static void AddExtraDetails(ProblemDetails problem, AppException ex)
    {
        switch (ex)
        {
            case QuotaExceededException quota:
                problem.Extensions["quotaName"] = quota.QuotaName;
                problem.Extensions["limit"] = quota.Limit;
                problem.Extensions["current"] = quota.Current;
                break;
            case NotFoundException notFound:
                problem.Extensions["entity"] = notFound.EntityName;
                break;
        }
    }

    private static string TitleFor(int status) => status switch
    {
        400 => "Donnees invalides",
        402 => "Quota du plan depasse",
        403 => "Acces refuse",
        404 => "Ressource introuvable",
        422 => "Regle metier violee",
        _ => "Erreur",
    };
}

public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandling(this IApplicationBuilder app)
        => app.UseMiddleware<GlobalExceptionMiddleware>();
}
