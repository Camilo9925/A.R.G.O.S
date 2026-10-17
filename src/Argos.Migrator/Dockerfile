# Migrator do ARGOS: execução única (plan 7.1, 7.4). Build com contexto na raiz: docker build -f src/Argos.Migrator/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restauração em camada separada: só muda quando um .csproj ou a gestão de pacotes muda.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Argos.Migrator/Argos.Migrator.csproj src/Argos.Migrator/
COPY src/Argos.Application/Argos.Application.csproj src/Argos.Application/
COPY src/Argos.Contracts/Argos.Contracts.csproj src/Argos.Contracts/
COPY src/Argos.Domain/Argos.Domain.csproj src/Argos.Domain/
COPY src/Argos.Infrastructure/Argos.Infrastructure.csproj src/Argos.Infrastructure/
COPY src/Argos.Simulation/Argos.Simulation.csproj src/Argos.Simulation/
RUN dotnet restore src/Argos.Migrator/Argos.Migrator.csproj

COPY src/ src/
RUN dotnet publish src/Argos.Migrator/Argos.Migrator.csproj -c Release -o /app/publish --no-restore -p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
USER $APP_UID
ENTRYPOINT ["dotnet", "Argos.Migrator.dll"]
