# ── Build Stage ───────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files first (layer caching for NuGet restore)
COPY ["src/CodeReview.Domain/CodeReview.Domain.csproj", "CodeReview.Domain/"]
COPY ["src/CodeReview.Application/CodeReview.Application.csproj", "CodeReview.Application/"]
COPY ["src/CodeReview.Infrastructure/CodeReview.Infrastructure.csproj", "CodeReview.Infrastructure/"]
COPY ["src/CodeReview.API/CodeReview.API.csproj", "CodeReview.API/"]

RUN dotnet restore "CodeReview.API/CodeReview.API.csproj"

# Copy all source code
COPY src/ .

# Build in Release mode
RUN dotnet build "CodeReview.API/CodeReview.API.csproj" -c Release -o /app/build --no-restore

# ── Publish Stage ─────────────────────────────────────────────────────────────
FROM build AS publish
RUN dotnet publish "CodeReview.API/CodeReview.API.csproj" \
    -c Release \
    -o /app/publish \
    --no-restore \
    /p:UseAppHost=false \
    /p:PublishReadyToRun=true

# ── Runtime Stage ─────────────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime

# Security: run as non-root
RUN addgroup --system --gid 1001 appgroup && \
    adduser --system --uid 1001 --ingroup appgroup appuser

WORKDIR /app

# Copy published output
COPY --from=publish --chown=appuser:appgroup /app/publish .

USER appuser

EXPOSE 8080
EXPOSE 8081

ENV ASPNETCORE_URLS="http://+:8080"
ENV DOTNET_RUNNING_IN_CONTAINER=true
ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false

ENTRYPOINT ["dotnet", "CodeReview.API.dll"]
