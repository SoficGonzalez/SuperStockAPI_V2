# ── Build stage ────────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Crear un NuGet.Config limpio que apunte solo a nuget.org
# Esto sobreescribe cualquier NuGet.Config con rutas Windows del proyecto
RUN echo '<?xml version="1.0" encoding="utf-8"?>\n\
<configuration>\n\
  <packageSources>\n\
    <clear />\n\
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />\n\
  </packageSources>\n\
  <fallbackPackageFolders>\n\
    <clear />\n\
  </fallbackPackageFolders>\n\
</configuration>' > /src/NuGet.Config

# Copiar solo csproj primero para aprovechar cache de Docker
COPY SuperStock.API/SuperStock.API.csproj SuperStock.API/
COPY SuperStock.Application/SuperStock.Application.csproj SuperStock.Application/
COPY SuperStock.Domain/SuperStock.Domain.csproj SuperStock.Domain/
COPY SuperStock.Infrastructure/SuperStock.Infrastructure.csproj SuperStock.Infrastructure/

RUN dotnet restore SuperStock.API/SuperStock.API.csproj --configfile /src/NuGet.Config

# Copiar el resto del codigo (SIN traer ningun NuGet.Config local del proyecto)
COPY . .

# Re-crear el NuGet.Config DESPUES del COPY . . para que prevalezca sobre el del proyecto
RUN echo '<?xml version="1.0" encoding="utf-8"?>\n\
<configuration>\n\
  <packageSources>\n\
    <clear />\n\
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />\n\
  </packageSources>\n\
  <fallbackPackageFolders>\n\
    <clear />\n\
  </fallbackPackageFolders>\n\
</configuration>' > /src/NuGet.Config

RUN dotnet publish SuperStock.API/SuperStock.API.csproj \
    -c Release \
    -o /app/publish \
    --configfile /src/NuGet.Config

# ── Runtime stage ──────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "SuperStock.API.dll"]