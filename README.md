# A.R.G.O.S.

**Antecipação, Rastreamento e Gestão de Operações em Segurança** — *os cem olhos do PROMETHEUS.*

O ARGOS é uma plataforma de gestão de equipamentos e operações de segurança urbana de uma cidade fictícia, **Prometeia/SP**. É construído em três camadas incrementais:

1. **Inventário Inteligente:** o que a cidade tem e onde está?
2. **Central de Operações:** o que está acontecendo agora?
3. **Cidade Preditiva:** o que pode acontecer amanhã?

> **Estado atual: Fase 1 — Acesso.** O esqueleto da Fase 0 atravessa todas as camadas e contêineres: o simulador envia um sinal de vida técnico à API, a API o repassa em tempo real (SignalR) à Web, e a Web mostra a casca do ARGOS com o mapa e o estado da conexão. A Fase 1 colocou login, perfis (Administrador, Operador e Gestor) e autorização em tudo: a Central só abre com login, cada perfil vê o menu da sua função, a API e o hub exigem autenticação, e o simulador se autentica com uma chave de sistema. Inventário, telemetria e previsões vêm nas próximas fases.

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

O `.env` traz valores fictícios, prontos para uso local, incluindo as chaves de acesso e os três usuários de demonstração (veja [Variáveis do `.env`](#variáveis-do-env)). **Antes de mostrar o ambiente a alguém, troque as senhas e as chaves** por valores seus: o `.env.example` é público, então os valores dele não são segredo. Se a porta 5432 já estiver ocupada na sua máquina, troque `POSTGRES_HOST_PORT` no `.env`.

Os serviços sobem nesta ordem: `db` → `migrator` (aplica as migrations, cria ou atualiza os usuários de demonstração e termina) → `api` → `simulator` e `web`. A subida termina quando `docker compose ps` mostra os serviços como `healthy` e o `migrator` como `Exited (0)`.

Para rodar em segundo plano, use `docker compose up --build -d`; os logs (em JSON) ficam em `docker compose logs -f <serviço>`.

## Entrar no ARGOS

Abra <http://localhost:8080>. Sem login, qualquer página leva à tela de login.

O ambiente do Compose é o **ambiente de demonstração**: a tela de login mostra um cartão para cada usuário de demonstração, com o perfil e o login, e o botão **Preencher**, que põe o login e a senha no formulário (a senha só aparece no campo mascarado). Depois é só clicar em **Entrar**. Com o `.env.example` sem alteração, os usuários são:

| Perfil | Login | O que vê no menu |
|---|---|---|
| Administrador | `admin` | Central, Inventário e Preditiva |
| Operador | `operador` | Central |
| Gestor | `gestor` | Central, Inventário e Preditiva |

As senhas são as do seu `.env` (`DEMO_*_PASSWORD`). O cabeçalho mostra o nome e o perfil de quem entrou, e **Sair** volta à tela de login. Uma página fora do perfil (por exemplo, o Operador em `/inventory`) mostra "Acesso negado". A sessão dura 8 horas e termina ao fechar a aba do navegador; ao vencer, o ARGOS volta ao login com "Sua sessão expirou.".

O preenchimento rápido existe **só no ambiente de demonstração**: fora dele (imagem da Web sem `ARGOS_ENVIRONMENT=Demo`, ou `dotnet run`), a tela mostra só o formulário, e nenhuma credencial é exibida. Inventário e Preditiva ainda mostram "Em construção".

## Variáveis do `.env`

| Variável | Uso | Formato |
|---|---|---|
| `POSTGRES_DB`, `POSTGRES_USER`, `POSTGRES_PASSWORD` | Banco PostgreSQL | texto livre |
| `POSTGRES_HOST_PORT` | Porta do banco no host (só `127.0.0.1`) | número; padrão `5432` |
| `SIMULATION_SEED` | Semente da simulação: a mesma semente gera os mesmos dados | número |
| `HEARTBEAT_INTERVAL_SECONDS` | Intervalo do sinal de vida do simulador | segundos |
| `ARGOS_JWT_SIGNING_KEY` | Chave que assina o token de acesso dos usuários | **segredo**, no mínimo 32 caracteres |
| `ARGOS_SYSTEM_KEY` | Chave com que o simulador (PROMETHEUS) se autentica na API | **segredo**, no mínimo 32 caracteres |
| `DEMO_ADMIN_LOGIN`, `DEMO_OPERATOR_LOGIN`, `DEMO_MANAGER_LOGIN` | Login de cada usuário de demonstração | 3 a 50 caracteres entre `a-z`, `0-9`, `.`, `-` e `_` |
| `DEMO_ADMIN_PASSWORD`, `DEMO_OPERATOR_PASSWORD`, `DEMO_MANAGER_PASSWORD` | Senha de cada usuário de demonstração | **segredo**, 8 a 64 caracteres entre letras sem acento, `0-9`, `.`, `-` e `_` |

Todas são obrigatórias: se faltar uma, o `docker compose up` recusa a subida e diz qual. Um login ou uma senha fora do formato impede a subida do `migrator` e da `web`, com a mensagem do campo (nunca o valor).

Para trocar uma senha, edite o `.env` e rode `docker compose up -d` de novo: o `migrator` atualiza só aquele usuário, sem apagar o banco. Os nomes de exibição ("Administrador de Demonstração" etc.) ficam em `src/Argos.Migrator/appsettings.json`.

## Endereços

| O quê | Endereço |
|---|---|
| **Web** (entrada da demonstração) | <http://localhost:8080> |
| API: documentação OpenAPI interativa (Scalar) | <http://localhost:8081/scalar> |
| API: prontidão (API e banco) | <http://localhost:8081/health/ready> (também em <http://localhost:8080/health/ready>, pelo proxy da Web) |
| Simulador: vivo | <http://localhost:8082/health/live> |
| PostgreSQL (ferramentas locais) | `127.0.0.1:5432` (ou a porta de `POSTGRES_HOST_PORT`), com usuário, senha e banco do `.env` |

A Web fala com a API pela mesma origem: o nginx da Web repassa `/api`, `/hubs` (WebSocket) e `/health` para a API.

## Chamando a API

Fora `/health/*`, o login e a documentação, todo endpoint exige credencial. Sem ela, a API responde `401`; com uma credencial sem permissão, `403`. Os erros vêm em `ProblemDetails` (JSON), em português.

- **Usuários** usam o token de acesso devolvido pelo login, no cabeçalho `Authorization: Bearer <token>`.
- **O simulador (PROMETHEUS)** usa a chave de sistema no cabeçalho `X-Argos-System-Key`.

Exemplos em bash, com os valores do `.env.example` (troque pelos do seu `.env`):

```bash
# Login: devolve accessToken, expiresAt, user e permissions
curl -s -X POST http://localhost:8081/api/access/login \
  -H "Content-Type: application/json" \
  -d '{"login":"admin","password":"troque-senha-admin"}'

# Endpoint do simulador com o token de um usuário: 403 (ele aceita só a chave de sistema)
curl -i -X POST http://localhost:8081/api/diagnostics/heartbeats \
  -H "Authorization: Bearer <accessToken>" \
  -H "Content-Type: application/json" \
  -d '{"source":"manual","sequence":1,"sentAt":"2026-10-17T12:00:00Z"}'

# O mesmo endpoint com a chave de sistema: 202
curl -i -X POST http://localhost:8081/api/diagnostics/heartbeats \
  -H "X-Argos-System-Key: troque-esta-chave-de-sistema-do-simulador" \
  -H "Content-Type: application/json" \
  -d '{"source":"manual","sequence":1,"sentAt":"2026-10-17T12:00:00Z"}'
```

No PowerShell, `curl` é outro comando; use `Invoke-RestMethod`:

```powershell
$login = Invoke-RestMethod -Method Post -Uri http://localhost:8081/api/access/login `
  -ContentType "application/json" -Body '{"login":"admin","password":"troque-senha-admin"}'
$login.accessToken
```

### Autenticar no Scalar

Em <http://localhost:8081/scalar> (só no ambiente de demonstração):

1. Abra `POST /api/access/login`, envie o login e a senha e copie o `accessToken` da resposta.
2. Na seção de autenticação do Scalar, escolha **Bearer** e cole o token. Para o endpoint do simulador, escolha **SystemKey** e informe a chave de sistema.
3. As próximas requisições feitas pelo Scalar vão com essa credencial.

## Testes

```bash
dotnet test
```

Roda os testes unitários e os de integração, incluindo login, `401`/`403`, a matriz de permissões por perfil e o recorte do tempo real por perfil. Os testes usam chaves e usuários próprios, sem o `.env`. Os testes de integração sobem um PostgreSQL 17 descartável com o Testcontainers, então **o Docker precisa estar em execução**. Para rodar um projeto só:

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

Com o banco do Compose no ar (`docker compose up -d db`), cada aplicação roda com `dotnet run` no ambiente `Development`.

Os segredos ficam em **user-secrets**, fora do repositório, configurados uma vez por máquina. Use valores seus; os abaixo mostram só o formato (chaves com no mínimo 32 caracteres, e a mesma chave de sistema na API e no simulador):

```powershell
dotnet user-secrets set "Jwt:SigningKey" "uma-chave-de-assinatura-local-com-32-caracteres" --project src/Argos.Api
dotnet user-secrets set "SystemKey:Key" "uma-chave-de-sistema-local-com-32-caracteres" --project src/Argos.Api
dotnet user-secrets set "Simulator:SystemKey" "uma-chave-de-sistema-local-com-32-caracteres" --project src/Argos.Simulator

# Usuários de demonstração: opcionais fora do Compose, mas sem eles o Migrator pula o passo e não há com quem entrar
dotnet user-secrets set "DemoUsers:Administrator:Login" "admin" --project src/Argos.Migrator
dotnet user-secrets set "DemoUsers:Administrator:Password" "uma-senha-local" --project src/Argos.Migrator
dotnet user-secrets set "DemoUsers:Operator:Login" "operador" --project src/Argos.Migrator
dotnet user-secrets set "DemoUsers:Operator:Password" "outra-senha-local" --project src/Argos.Migrator
dotnet user-secrets set "DemoUsers:Manager:Login" "gestor" --project src/Argos.Migrator
dotnet user-secrets set "DemoUsers:Manager:Password" "mais-uma-senha-local" --project src/Argos.Migrator
```

Sem `Jwt:SigningKey` ou `SystemKey:Key`, a API não sobe e diz qual chave falta; o simulador faz o mesmo com `Simulator:SystemKey`.

A connection string vem da variável `ConnectionStrings__Argos` (exemplo em PowerShell, com os valores do `.env.example`):

```powershell
$env:ConnectionStrings__Argos = "Host=localhost;Port=5432;Database=argos;Username=argos;Password=troque-esta-senha-local"
dotnet run --project src/Argos.Migrator
dotnet run --project src/Argos.Api        # http://localhost:5081
dotnet run --project src/Argos.Simulator  # http://localhost:5082
dotnet run --project src/Argos.Web        # http://localhost:5080
```

Em `Development`, a Web chama a API em `http://localhost:5081`, e a API libera CORS só para a origem da Web local. A Web local não tem preenchimento rápido: digite o login e a senha configurados no Migrator. O Scalar não existe em `Development`.

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
