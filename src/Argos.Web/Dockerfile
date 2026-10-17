# Web do ARGOS: Blazor WebAssembly estático servido pelo nginx, que também faz o proxy para a API (plan 7.1, 9.1).
# Build com contexto na raiz: docker build -f src/Argos.Web/Dockerfile .

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# Restauração em camada separada: só muda quando um .csproj ou a gestão de pacotes muda.
COPY global.json Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/Argos.Web/Argos.Web.csproj src/Argos.Web/
COPY src/Argos.Contracts/Argos.Contracts.csproj src/Argos.Contracts/
RUN dotnet restore src/Argos.Web/Argos.Web.csproj

COPY src/ src/
RUN dotnet publish src/Argos.Web/Argos.Web.csproj -c Release -o /app/publish --no-restore

FROM nginx:alpine AS final
COPY src/Argos.Web/nginx/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/publish/wwwroot /usr/share/nginx/html
EXPOSE 80
