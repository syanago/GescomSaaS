# syntax=docker/dockerfile:1.7

# ===== Stage 1 : restore =====================================================
# Cache des dependances NuGet : tant que les .csproj ne changent pas, ce
# layer est reutilise meme quand le code change.
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS restore
WORKDIR /src

COPY GescomSaas.sln Directory.Build.props ./
COPY src/GescomSaas.Domain/GescomSaas.Domain.csproj          src/GescomSaas.Domain/
COPY src/GescomSaas.Application/GescomSaas.Application.csproj src/GescomSaas.Application/
COPY src/GescomSaas.Infrastructure/GescomSaas.Infrastructure.csproj src/GescomSaas.Infrastructure/
COPY src/GescomSaas.Web/GescomSaas.Web.csproj                src/GescomSaas.Web/
COPY tests/GescomSaas.Tests/GescomSaas.Tests.csproj          tests/GescomSaas.Tests/

RUN dotnet restore GescomSaas.sln

# ===== Stage 2 : build + tests + publish =====================================
FROM restore AS build
ARG BUILD_CONFIGURATION=Release
ARG SKIP_TESTS=false
WORKDIR /src

COPY src/ src/
COPY tests/ tests/

RUN dotnet build GescomSaas.sln \
    --configuration ${BUILD_CONFIGURATION} \
    --no-restore

# Tests dans l'image - desactivable via SKIP_TESTS=true pour les images locales rapides.
RUN if [ "$SKIP_TESTS" != "true" ]; then \
        dotnet test GescomSaas.sln \
            --configuration ${BUILD_CONFIGURATION} \
            --no-build \
            --logger "trx;LogFileName=test-results.trx"; \
    fi

RUN dotnet publish src/GescomSaas.Web/GescomSaas.Web.csproj \
    --configuration ${BUILD_CONFIGURATION} \
    --no-build \
    --output /app/publish

# ===== Stage 3 : runtime =====================================================
# Image runtime minimale (~110 Mo). On utilise le tag explicite pour eviter
# les surprises lors d'un nouveau release de la base.
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Utilisateur non-root pour respecter les politiques k8s pod security.
RUN groupadd -r app && useradd -r -g app -u 10001 -m -d /home/app app
WORKDIR /app

COPY --from=build --chown=app:app /app/publish ./

# Repertoires modifiables a runtime (data protection keys, logs, sqlite, etc.).
# Doivent etre montes en volume en production pour persister.
RUN mkdir -p App_Data App_Data/logs .keys \
    && chown -R app:app App_Data .keys

USER app

ENV ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_HTTP_PORTS=8080 \
    DOTNET_RUNNING_IN_CONTAINER=true \
    DOTNET_NOLOGO=true \
    DOTNET_CLI_TELEMETRY_OPTOUT=true

EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD wget --quiet --tries=1 --spider http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "GescomSaas.Web.dll"]
