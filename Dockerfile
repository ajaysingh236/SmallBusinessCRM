# Multi-stage Dockerfile for SmallBusinessCRM Web API
# Stage 1: Build & Restore
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files and restore dependencies
COPY ["src/SmallBusinessCRM.Api/SmallBusinessCRM.Api.csproj", "src/SmallBusinessCRM.Api/"]
RUN dotnet restore "src/SmallBusinessCRM.Api/SmallBusinessCRM.Api.csproj"

# Copy entire source and build
COPY ["src/SmallBusinessCRM.Api/", "src/SmallBusinessCRM.Api/"]
WORKDIR "/src/src/SmallBusinessCRM.Api"
RUN dotnet build "SmallBusinessCRM.Api.csproj" -c Release -o /app/build

# Stage 2: Publish
FROM build AS publish
RUN dotnet publish "SmallBusinessCRM.Api.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Stage 3: Lightweight Slim Runtime with Non-Root Security
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app

# Create non-root user and group
RUN groupadd -g 10001 appuser && \
    useradd -u 10001 -g appuser -s /bin/sh appuser && \
    mkdir -p /app/data && chown -R appuser:appuser /app

# Install curl for health checking
RUN apt-get update && apt-get install -y --no-install-recommends curl && rm -rf /var/lib/apt/lists/*

COPY --from=publish --chown=appuser:appuser /app/publish .

# Environment Configuration
ENV ASPNETCORE_URLS=http://+:5000
ENV ASPNETCORE_ENVIRONMENT=Production
ENV DatabaseProvider=Sqlite
ENV ConnectionStrings__CrmDatabase_Sqlite="Data Source=/app/data/crm.db"

EXPOSE 5000

# Healthcheck endpoint
HEALTHCHECK --interval=20s --timeout=5s --start-period=10s --retries=3 \
  CMD curl -f http://localhost:5000/metrics || exit 1

# Drop root privileges
USER appuser

ENTRYPOINT ["dotnet", "SmallBusinessCRM.Api.dll"]
