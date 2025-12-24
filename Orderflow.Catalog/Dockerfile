# Build stage
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src

# Copy solution and project files
COPY ["Orderflow.Catalog/Orderflow.Catalog.csproj", "Orderflow.Catalog/"]
COPY ["Orderflow.ServiceDefaults/Orderflow.ServiceDefaults.csproj", "Orderflow.ServiceDefaults/"]
COPY ["Orderflow.Shared/Orderflow.Shared.csproj", "Orderflow.Shared/"]
COPY ["Directory.Packages.props", "./"]
COPY ["Directory.Build.props", "./"]

# Restore dependencies
RUN dotnet restore "Orderflow.Catalog/Orderflow.Catalog.csproj"

# Copy source code
COPY ["Orderflow.Catalog/", "Orderflow.Catalog/"]
COPY ["Orderflow.ServiceDefaults/", "Orderflow.ServiceDefaults/"]
COPY ["Orderflow.Shared/", "Orderflow.Shared/"]

# Build
WORKDIR /src/Orderflow.Catalog
RUN dotnet build -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:10.0-preview AS final
WORKDIR /app

# Create non-root user
RUN addgroup --system --gid 1000 appgroup \
    && adduser --system --uid 1000 --ingroup appgroup --shell /bin/false appuser

# Copy published app
COPY --from=publish /app/publish .

# Set ownership
RUN chown -R appuser:appgroup /app

# Switch to non-root user
USER appuser

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "Orderflow.Catalog.dll"]
