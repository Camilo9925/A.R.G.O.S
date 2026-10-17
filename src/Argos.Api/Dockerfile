# API do ARGOS (plan 7.1). Build com contexto na raiz: docker build -f src/Argos.Api/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restauração em camada separada: só muda quando um .csproj ou a gestão de pacotes muda.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Argos.Api/Argos.Api.csproj src/Argos.Api/
COPY src/Argos.Application/Argos.Application.csproj src/Argos.Application/
COPY src/Argos.Contracts/Argos.Contracts.csproj src/Argos.Contracts/
COPY src/Argos.Domain/Argos.Domain.csproj src/Argos.Domain/
COPY src/Argos.Infrastructure/Argos.Infrastructure.csproj src/Argos.Infrastructure/
RUN dotnet restore src/Argos.Api/Argos.Api.csproj

COPY src/ src/
RUN dotnet publish src/Argos.Api/Argos.Api.csproj -c Release -o /app/publish --no-restore -p:UseAppHost=false

# Imagem .NET padrão, não Alpine (D-15), com curl para o healthcheck do Compose.
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Argos.Api.dll"]
