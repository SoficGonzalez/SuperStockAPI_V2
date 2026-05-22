# Etapa 1: Compilación (SDK completo)
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build-env
WORKDIR /app

# Copiar archivos de proyecto y restaurar dependencias de forma eficiente
COPY *.slnx ./
COPY SuperStock.API/*.csproj ./SuperStock.API/
COPY SuperStock.Infrastructure/*.csproj ./SuperStock.Infrastructure/
COPY SuperStock.Domain/*.csproj ./SuperStock.Domain/
COPY SuperStock.Application/*.csproj ./SuperStock.Application/

RUN dotnet restore SuperStock.slnx

# Copiar el resto del código y compilar la aplicación
COPY . ./
RUN dotnet publish SuperStock.API/SuperStock.API.csproj -c Release -o /out

# Etapa 2: Imagen de ejecución (Ultra ligera - Chiseled optimizada para Web/API)
FROM mcr.microsoft.com/dotnet/aspnet:10.0-noble-chiseled AS runtime-env
WORKDIR /app
COPY --from=build-env /out .

# Configurar variables de entorno optimizadas para .NET en contenedores
ENV DOTNET_EnableDiagnostics=0 \
    ASPNETCORE_HTTP_PORTS=8080

EXPOSE 8080
ENTRYPOINT ["dotnet", "SuperStock.API.dll"]