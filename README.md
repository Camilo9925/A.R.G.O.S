# A.R.G.O.S.

**Antecipação, Rastreamento e Gestão de Operações em Segurança** — *os cem olhos do PROMETHEUS.*

O ARGOS é uma plataforma de gestão de equipamentos e operações de segurança urbana de uma cidade fictícia, **Prometeia/SP**. É construído em três camadas incrementais:

1. **Inventário Inteligente:** o que a cidade tem e onde está?
2. **Central de Operações:** o que está acontecendo agora?
3. **Cidade Preditiva:** o que pode acontecer amanhã?

> **Estado atual: Fase 0 — Fundação.** O esqueleto atravessa todas as camadas e contêineres: o simulador envia um sinal de vida técnico à API, a API o repassa em tempo real (SignalR) à Web, e a Web mostra a casca do ARGOS com o mapa e o estado da conexão. Ainda não há regras de negócio nem login.

> **Dados simulados e fictícios.** Na ausência de dispositivos reais, o ARGOS opera com dados **simulados**. Prometeia/SP e todos os nomes, ruas, bairros, CEPs, pessoas, matrículas e placas são **fictícios**. A geometria das ruas vem da malha viária de uma cidade real do interior paulista (OpenStreetMap), exibida sem nenhum nome.

## Pré-requisitos

- **Docker** com Docker Compose (por exemplo, Docker Desktop). É tudo o que é preciso para subir a demonstração.
- **.NET 10 SDK**, só para desenvolver e rodar os testes.

## Como subir

Na raiz do repositório:

```bash
cp .env.example .env          # no PowerShell: Copy-Item .env.example .env
docker compose up --build
```

O `.env` traz valores fictícios, prontos para uso local. Se a porta 5432 já estiver ocupada na sua máquina, troque `POSTGRES_HOST_PORT` no `.env`.

Os serviços sobem nesta ordem: `db` → `migrator` (aplica as migrations e termina) → `api` → `simulator` e `web`. A subida termina quando `docker compose ps` mostra os serviços como `healthy` e o `migrator` como `Exited (0)`.

Para rodar em segundo plano, use `docker compose up --build -d`; os logs (em JSON) ficam em `docker compose logs -f <serviço>`.

## Endereços

| O quê | Endereço |
|---|---|
| **Web** (entrada da demonstração) | <http://localhost:8080> |
| API: documentação OpenAPI interativa (Scalar) | <http://localhost:8081/scalar> |
| API: prontidão (API e banco) | <http://localhost:8081/health/ready> (também em <http://localhost:8080/health/ready>, pelo proxy da Web) |
| Simulador: vivo | <http://localhost:8082/health/live> |
| PostgreSQL (ferramentas locais) | `127.0.0.1:5432` (ou a porta de `POSTGRES_HOST_PORT`), com usuário, senha e banco do `.env` |

A Web fala com a API pela mesma origem: o nginx da Web repassa `/api`, `/hubs` (WebSocket) e `/health` para a API.

## Testes

```bash
dotnet test
```

Roda os testes unitários e os de integração. Os testes de integração sobem um PostgreSQL 17 descartável com o Testcontainers, então **o Docker precisa estar em execução**. Para rodar um projeto só:

```bash
dotnet test --project src/tests/Argos.UnitTests
dotnet test --project src/tests/Argos.IntegrationTests
```

## Recomeçar do zero

```bash
docker compose down -v
```

Remove os contêineres, a rede e o volume do banco (`argos-pgdata`). O próximo `docker compose up --build` recria tudo, incluindo as migrations e a carga inicial.

## Desenvolvimento sem Docker (opcional)

Com o banco do Compose no ar (`docker compose up -d db`), cada aplicação roda com `dotnet run` no ambiente `Development`. A connection string vem da variável `ConnectionStrings__Argos` (exemplo em PowerShell, com os valores do `.env.example`):

```powershell
$env:ConnectionStrings__Argos = "Host=localhost;Port=5432;Database=argos;Username=argos;Password=troque-esta-senha-local"
dotnet run --project src/Argos.Migrator
dotnet run --project src/Argos.Api        # http://localhost:5081
dotnet run --project src/Argos.Simulator  # http://localhost:5082
dotnet run --project src/Argos.Web        # http://localhost:5080
```

Em `Development`, a Web chama a API em `http://localhost:5081`, e a API libera CORS só para a origem da Web local.

## Estrutura da solução

| Projeto | Papel |
|---|---|
| `src/Argos.Domain` | Entidades e regras de negócio. Não depende de nada. |
| `src/Argos.Application` | Casos de uso e portas (interfaces) implementadas pelas outras camadas. |
| `src/Argos.Contracts` | DTOs trocados por HTTP e SignalR, compartilhados por API, Web e Simulador. |
| `src/Argos.Infrastructure` | EF Core/Npgsql, `DbContext`, migrations e interceptadores de persistência. |
| `src/Argos.Api` | ASP.NET Core com controllers, hub SignalR, `ProblemDetails`, health checks e OpenAPI. |
| `src/Argos.Web` | Blazor WebAssembly com MudBlazor e mapa Leaflet, servido por nginx. |
| `src/Argos.Simulation` | Motor de simulação determinístico (a mesma semente gera os mesmos dados). |
| `src/Argos.Simulator` | Faz o papel do PROMETHEUS: envia os dados simulados à API. |
| `src/Argos.Migrator` | Execução única: aplica as migrations e a carga inicial, sem duplicar dados. |
| `src/tests/Argos.UnitTests` | Testes unitários (xUnit). |
| `src/tests/Argos.IntegrationTests` | Testes de integração da API contra PostgreSQL real (xUnit + Testcontainers). |

Cada aplicação tem seu `Dockerfile` na própria pasta; o ambiente completo está em `docker-compose.yml`.

## Atribuições e licenças

- **Mapa:** © [OpenStreetMap](https://www.openstreetmap.org/copyright) contributors. As ruas do mapa base (`src/Argos.Web/wwwroot/data/streets.json`) são derivadas do OpenStreetMap e estão sob a [Open Database License (ODbL)](https://opendatacommons.org/licenses/odbl/1-0/); a origem e a conversão estão em `src/Argos.Web/wwwroot/data/VERSION`. O mapa funciona sem internet, sem tiles externos e sem chave.
- **Bibliotecas JavaScript** empacotadas em `src/Argos.Web/wwwroot/lib`: Leaflet 1.9.4 (BSD-2-Clause) e Leaflet.markercluster 1.5.3 (MIT), cada uma com sua licença.
- **Código do ARGOS:** licença MIT (arquivo `LICENSE`).
