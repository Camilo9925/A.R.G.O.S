# A.R.G.O.S.

**Antecipação, Rastreamento e Gestão de Operações em Segurança** — *os cem olhos do PROMETHEUS.*

O ARGOS é uma plataforma de gestão de equipamentos e operações de segurança urbana de uma cidade fictícia, **Prometeia/SP**. É construído em três camadas incrementais:

1. **Inventário Inteligente:** o que a cidade tem e onde está?
2. **Central de Operações:** o que está acontecendo agora?
3. **Cidade Preditiva:** o que pode acontecer amanhã?

> **Estado atual: Fase 2 — Inventário (cadastros de apoio e equipamentos).** O esqueleto da Fase 0 atravessa todas as camadas e contêineres, e a Fase 1 colocou login, perfis (Administrador, Operador e Gestor) e autorização em tudo. A Fase 2 entrega a primeira metade do Inventário Inteligente: a carga de Prometeia com **300 equipamentos**, a pesquisa e os filtros, o cadastro e a edição de equipamentos com sugestão de endereço por CEP e escolha do ponto no mapa, a situação de cada equipamento e as manutenções, e os cadastros de apoio (órgãos, regiões, bairros e bases). Agentes, linha do tempo, inativação e tipos personalizados vêm na Fase 3; telemetria, Central e previsões, nas seguintes.

> **Dados simulados e fictícios.** Na ausência de dispositivos reais, o ARGOS opera com dados **simulados**, e a Web mostra no cabeçalho, o tempo todo, o selo **"Dados simulados"**. Prometeia/SP e todos os nomes, ruas, bairros, CEPs, marcas, números de série, pessoas, matrículas e placas são **fictícios**. A geometria das ruas vem da malha viária de uma cidade real do interior paulista (OpenStreetMap), exibida sem nenhum nome real.

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

Os serviços sobem nesta ordem: `db` → `migrator` (aplica as migrations, cria ou atualiza os usuários de demonstração, carrega os parâmetros de negócio e o Inventário de Prometeia e termina; veja [Carga inicial](#carga-inicial-e-versões-do-seed)) → `api` → `simulator` e `web`. A subida termina quando `docker compose ps` mostra os serviços como `healthy` e o `migrator` como `Exited (0)`. A primeira subida compila as imagens e leva alguns minutos; a carga em si leva poucos segundos.

Para rodar em segundo plano, use `docker compose up --build -d`; os logs (em JSON) ficam em `docker compose logs -f <serviço>`.

## Entrar no ARGOS

Abra <http://localhost:8080>. Sem login, qualquer página leva à tela de login.

O ambiente do Compose é o **ambiente de demonstração**: a tela de login mostra um cartão para cada usuário de demonstração, com o perfil e o login, e o botão **Preencher**, que põe o login e a senha no formulário (a senha só aparece no campo mascarado). Depois é só clicar em **Entrar**. Com o `.env.example` sem alteração, os usuários são:

| Perfil | Login | O que vê no menu |
|---|---|---|
| Administrador | `admin` | Central; Inventário (Equipamentos e Cadastros de apoio); Preditiva |
| Operador | `operador` | Central |
| Gestor | `gestor` | Central; Inventário (Equipamentos, só consulta); Preditiva |

As senhas são as do seu `.env` (`DEMO_*_PASSWORD`). O cabeçalho mostra o nome e o perfil de quem entrou, e **Sair** volta à tela de login. Uma página fora do perfil (por exemplo, o Operador em `/inventory/equipment`) mostra "Acesso negado". A sessão dura 8 horas e termina ao fechar a aba do navegador; ao vencer, o ARGOS volta ao login com "Sua sessão expirou.".

O preenchimento rápido existe **só no ambiente de demonstração**: fora dele (imagem da Web sem `ARGOS_ENVIRONMENT=Demo`, ou `dotnet run`), a tela mostra só o formulário, e nenhuma credencial é exibida. A Preditiva ainda mostra "Em construção", e a Central mostra só o mapa e o estado da conexão.

## Inventário

O menu **Inventário → Equipamentos** abre a lista dos equipamentos de Prometeia. O Administrador consulta e altera; o Gestor só consulta (os botões de escrita não aparecem, e a API recusa a escrita com `403`); o Operador não vê o Inventário.

### O que a carga traz

| O quê | Quantidade |
|---|---|
| Município | Prometeia/SP, com o contorno desenhado no mapa |
| Regiões | 5 (Centro, Zona Norte, Zona Sul, Zona Leste e Zona Oeste) |
| Bairros | 30, todos com nomes inventados |
| CEPs | 597, um por logradouro em cada bairro, todos começando por `16985` |
| Órgãos | 5: Secretaria Municipal de Mobilidade (SMM), Guarda Civil Municipal (GCM), Polícia Militar (PM) e duas concessionárias fictícias (ROTAPRO e ÁGORA) |
| Bases operacionais | 9 |
| Tipos de equipamento | 10 (radar, câmera, balança, cancela, totem, veículo autônomo, veículo de transporte, patinete, moto de agente de trânsito e viatura), cada um com as suas características |
| Equipamentos | **300**: 200 fixos (com endereço) e 100 móveis (com base operacional), a maioria operacional e alguns em mau funcionamento, em manutenção, ainda não instalados, desativados ou furtados, com manutenções vencidas e próximas |

Com a semente padrão (`SIMULATION_SEED=42`), os dados são sempre os mesmos. As datas da carga (cadastro há cerca de seis meses, mudanças de situação nos últimos dias, datas de manutenção) são contadas a partir do dia em que a carga roda. Tudo o que a carga grava tem o autor `simulação`.

### Pesquisar e filtrar

- **Pesquisa:** número de série, marca, modelo, rua ou bairro, sem diferença de acentos e de maiúsculas. Por exemplo, `crepusculo` encontra os equipamentos da Vila Crepúsculo e `bussola`, os da Avenida da Bússola.
- **Filtros:** tipo, situação, órgão, região, bairro, necessidade de manutenção e fixo ou móvel. Vários valores no mesmo filtro somam ("Câmera" ou "Radar"); filtros diferentes se combinam ("Câmera" **e** "Mau funcionamento" **e** "Centro"). Os filtros ativos aparecem numa barra, com **Limpar filtros**.
- A pesquisa, os filtros e a página ficam no endereço do navegador: dá para voltar à mesma lista ou compartilhar o link.

### Cadastrar um equipamento

Entre como **Administrador** e abra **Inventário → Equipamentos → Novo equipamento**. Para cadastrar uma câmera:

1. **Identificação:** tipo **Câmera de videomonitoramento**, órgão (por exemplo, **GCM · Guarda Civil Municipal**), número de série, marca e modelo (qualquer texto; marca e número de série não podem repetir os de outro equipamento).
2. **Características:** aparecem conforme o tipo. Para a câmera: **Resolução**, **Visão noturna**, **Modo de cor** e **Giro e zoom (PTZ)**; o **Campo de visão** é opcional.
3. **Localização:** digite o CEP **16985-001** e saia do campo; o ARGOS sugere a rua (Avenida dos Sabiás), o bairro (Jardim Poente) e a região e centraliza o mapa. Cidade e UF são sempre Prometeia/SP. Preencha a **referência** (texto livre, como "número 181", "oposto ao número 181" ou "S/N") e clique no mapa, dentro do contorno tracejado, para marcar o ponto exato (ou digite a latitude e a longitude).
4. **Situação e manutenção inicial:** a situação inicial já vem como Operacional (as opções são Ainda não instalado, Operacional, Mau funcionamento e Em manutenção). Se quiser, registre uma manutenção já no cadastro.
5. Clique em **Cadastrar equipamento**. O ARGOS abre os detalhes do equipamento, com o registro inicial no histórico de situação.

Se algo estiver errado, a mensagem aparece no próprio campo (campo obrigatório vazio, ponto fora do município, característica obrigatória do tipo, como a velocidade máxima de um radar na modalidade Velocidade). Se a marca e o número de série já existirem, o aviso traz o link **Abrir equipamento existente**. Equipamentos móveis (viaturas, motos, patinetes etc.) não têm endereço: escolha a **base operacional**.

### Situação e manutenção

Nos detalhes do equipamento:

- **Alterar situação** mostra só as situações permitidas a partir da atual. Furtado e Desativado pedem justificativa. Passar para Mau funcionamento abre uma manutenção imediata; passar para Desativado ou Furtado cancela as manutenções abertas.
- **Reativar** um equipamento desativado (Desativado → Operacional) é **só do Administrador**, com justificativa. O Operador tem a permissão de mudar situações, mas a API recusa a reativação feita por ele com `403` e o motivo.
- **Registrar manutenção:** imediata, agendada (com data prevista) ou periódica (com data prevista e periodicidade em dias). As abertas mostram o selo **Próxima** (até 7 dias) ou **Vencida**.
- **Concluir** registra a data, o resultado e as observações. Com sucesso, o equipamento volta a Operacional; sem sucesso, passa para (ou continua em) Mau funcionamento, e uma nova manutenção imediata é aberta se não houver outra. A conclusão não muda a situação de um equipamento Ainda não instalado. Concluir uma periódica programa a próxima automaticamente.
- O **histórico de situação** e as manutenções encerradas ficam nos detalhes, com data, hora e autor.

Nada no Inventário é excluído: não existe opção de exclusão em nenhuma tela nem rota `DELETE` na API. A inativação de cadastros chega na Fase 3.

### Cadastros de apoio

**Inventário → Cadastros de apoio** (só o Administrador) tem as abas Órgãos, Regiões, Bairros (com o centro escolhido no mapa) e Bases (com localização e sugestão por CEP), cada uma com criação, edição, inativação e o filtro **Mostrar inativos**. A inativação é recusada, com o motivo, quando ainda há algo ativo ligado ao cadastro (por exemplo, um bairro com equipamentos).

## Carga inicial e versões do seed

A cada subida, o `migrator` aplica as migrations e a carga inicial, sem duplicar nada. Cada parte da carga é uma **versão do seed**, gravada no banco junto com a semente usada, na mesma transação dos dados:

| Versão | O que carrega |
|---|---|
| `foundation` | Só o registro de fundação do ambiente, sem dados |
| `inventory-reference-v1` | Município, regiões, bairros, CEPs, órgãos, bases e os 10 tipos |
| `inventory-catalog-v1` | Os 300 equipamentos, com situações e manutenções |

Na primeira subida, o log do `migrator` mostra `Carga inventory-reference-v1 aplicada com a semente 42: ...` e `Carga inventory-catalog-v1 aplicada com a semente 42: ...`, com as quantidades. Nas seguintes, mostra `... já aplicada; nada a fazer.` e não grava nada no Inventário. Se uma carga falhar no meio, nada dela fica gravado, e a próxima subida tenta de novo.

Para ver o log: `docker compose logs migrator`.

**Trocar a semente:** se o banco já tem uma carga feita com outra semente, o `migrator` para com a mensagem `A carga ... já foi aplicada com a semente 42. Para usar a semente 7, recomece o banco (docker compose down -v).` Para usar outra semente, [recomece do zero](#recomeçar-do-zero) com a nova `SIMULATION_SEED` no `.env`.

### Parâmetros de negócio (`BusinessParameters`)

Os parâmetros de negócio ajustáveis pelo Administrador ficam no banco. Nesta fase existe um: `maintenance.upcoming-window-days`, quantos dias antes da data prevista uma manutenção passa a ser **Próxima** (padrão 7, de 1 a 60). O valor inicial vem da seção `BusinessParameters` de `src/Argos.Migrator/appsettings.json`:

```json
"BusinessParameters": {
  "maintenance.upcoming-window-days": 7
}
```

O `migrator` só cria os parâmetros que ainda não existem no banco e **nunca sobrescreve um valor já gravado**. Assim, mudar o arquivo vale para um banco novo: altere o valor e rode `docker compose down -v` e `docker compose up --build` (o `--build` leva o arquivo novo para a imagem do `migrator`). Uma chave desconhecida ou um valor fora da faixa faz o `migrator` terminar com código 1 e a mensagem da chave no log, sem subir a API. A tela para o Administrador editar os parâmetros chega numa fase seguinte.

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

Roda os testes unitários e os de integração, incluindo login, `401`/`403`, a matriz de permissões por perfil, o recorte do tempo real por perfil, as regras do Inventário e todos os endpoints do Inventário. Os testes usam chaves, usuários e dados próprios, sem o `.env` e sem a carga de Prometeia. Os testes de integração sobem um PostgreSQL 17 descartável com o Testcontainers, então **o Docker precisa estar em execução**. Para rodar um projeto só:

```bash
dotnet test --project src/tests/Argos.UnitTests
dotnet test --project src/tests/Argos.IntegrationTests
```

## Recomeçar do zero

```bash
docker compose down -v
```

Remove os contêineres, a rede e o volume do banco (`argos-pgdata`), **com tudo o que foi cadastrado ou alterado pela interface**. O próximo `docker compose up --build` recria tudo, incluindo as migrations e a carga inicial de Prometeia. É o caminho para trocar a semente (`SIMULATION_SEED`) ou o valor inicial de um parâmetro de negócio.

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
