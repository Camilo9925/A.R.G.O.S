# A.R.G.O.S.

**Antecipação, Rastreamento e Gestão de Operações em Segurança** — *os cem olhos do PROMETHEUS.*

O ARGOS é uma plataforma de gestão de equipamentos e operações de segurança urbana de uma cidade fictícia, **Prometeia/SP**. É construído em três camadas incrementais:

1. **Inventário Inteligente:** o que a cidade tem e onde está?
2. **Central de Operações:** o que está acontecendo agora?
3. **Cidade Preditiva:** o que pode acontecer amanhã?

> **Estado atual: entrega de 27/10/2026.** As três camadas funcionam sobre os dados simulados de Prometeia, com login e três perfis (Administrador, Operador e Gestor):
>
> - **Inventário:** 300 equipamentos e 80 agentes, com pesquisa, filtros, cadastro, situação, manutenções, vínculos entre agentes e viaturas, linha do tempo e inativação (nada é excluído).
> - **Central de Operações:** o mapa ao vivo, com equipamentos e agentes em movimento, indicadores, filtros, alertas e detalhes, alimentada pela telemetria simulada do PROMETHEUS.
> - **Cidade Preditiva:** o índice de risco de ocorrências por região, com a grade por dia e faixa horária, o mapa de risco e a explicação de cada previsão.
>
> O que ficou **fora da entrega** está em [O que não faz parte desta entrega](#o-que-não-faz-parte-desta-entrega). Para percorrer tudo, siga o [Roteiro de demonstração](#roteiro-de-demonstração).

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

Os serviços sobem nesta ordem: `db` → `migrator` (aplica as migrations, cria ou atualiza os usuários de demonstração, carrega os parâmetros de negócio e os dados de Prometeia e termina; veja [Carga inicial](#carga-inicial-e-versões-do-seed)) → `api` → `simulator` e `web`. A subida termina quando `docker compose ps` mostra os serviços como `healthy` e o `migrator` como `Exited (0)`. A primeira subida compila as imagens e leva alguns minutos; a carga em si leva poucos segundos.

Para rodar em segundo plano, use `docker compose up --build -d`; os logs (em JSON) ficam em `docker compose logs -f <serviço>`.

## Entrar no ARGOS

Abra <http://localhost:8080>. Sem login, qualquer página leva à tela de login.

O ambiente do Compose é o **ambiente de demonstração**: a tela de login mostra um cartão para cada usuário de demonstração, com o perfil e o login, e o botão **Preencher**, que põe o login e a senha no formulário (a senha só aparece no campo mascarado). Depois é só clicar em **Entrar**. Com o `.env.example` sem alteração, os usuários são:

| Perfil | Login | O que vê no menu |
|---|---|---|
| Administrador | `admin` | Central; Inventário (Equipamentos, Agentes e Cadastros de apoio); Preditiva |
| Operador | `operador` | Central |
| Gestor | `gestor` | Central; Inventário (Equipamentos e Agentes, só consulta); Preditiva |

As senhas são as do seu `.env` (`DEMO_*_PASSWORD`); com o `.env.example` sem alteração, são `troque-senha-admin`, `troque-senha-operador` e `troque-senha-gestor`. O cabeçalho mostra o nome e o perfil de quem entrou, e **Sair** volta à tela de login. Uma página fora do perfil (por exemplo, o Operador em `/inventory/equipment` ou em `/predictive`) mostra "Acesso negado". A sessão dura 8 horas e termina ao fechar a aba do navegador; ao vencer, o ARGOS volta ao login com "Sua sessão expirou.".

O preenchimento rápido existe **só no ambiente de demonstração**: fora dele (imagem da Web sem `ARGOS_ENVIRONMENT=Demo`, ou `dotnet run`), a tela mostra só o formulário, e nenhuma credencial é exibida.

## O que não faz parte desta entrega

Por causa do prazo de 27/10/2026, estas partes continuam especificadas (em `specs/`), mas **não foram implementadas**:

| O quê | Observação |
|---|---|
| **US-10**, tipos personalizados de equipamento | Só existem os 10 tipos da carga; não há tela para criar outros. |
| **US-16 a US-19**, na Central: manutenções, visão por região e classificação, equipamentos e agentes sem comunicação e mapa de calor | A Central mostra a última comunicação só nos detalhes. |
| **US-21**, recomendação de reforço de equipes | A Preditiva não traz recomendações para aceitar ou descartar; cada previsão só traz a sugestão de uma frase. |
| **US-22 a US-24**, anomalias, risco de falha, tendências e alertas preventivos | |
| **Taxa de acerto** da previsão (RN-32, CS-09) | Corte de contingência (b), adiantado: não há backtest nem taxa de acerto. |
| **Roteiro automático de demonstração** (RF-052) | A reação da Central é demonstrada pela própria interface: o Operador marca uma câmera como Furtado, e o alerta aparece. |
| **CS-07**, teste de capacidade | Não foi medida; a massa padrão é de cerca de 300 equipamentos e 80 agentes. |
| **Exceção temporária ao Art. VII.4** (ausência de sinal) | O ARGOS não verifica nem sinaliza equipamento ou agente sem comunicação. Fica como primeiro item da próxima rodada de planejamento. |

Também ficam para depois: a tela de edição dos parâmetros de negócio, os alertas de manutenção e de região, os alertas na linha do tempo, as ocorrências ao vivo pela telemetria (o histórico de ocorrências é simulado e fixo) e a consolidação da telemetria bruta.

## Inventário

O menu **Inventário** tem **Equipamentos** (a lista dos equipamentos de Prometeia), **Agentes** e, só para o Administrador, **Cadastros de apoio**. O Administrador consulta e altera; o Gestor só consulta (os botões de escrita não aparecem, e a API recusa a escrita com `403`); o Operador não vê o Inventário.

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
| Equipamentos | **300**: 200 fixos (com endereço) e 100 móveis (com base operacional), a maioria operacional e alguns em mau funcionamento, em manutenção, ainda não instalados, desativados ou furtados, com manutenções vencidas e próximas. **3** deles já vêm inativados |
| Agentes | **80**: 30 agentes de trânsito, 28 guardas municipais e 22 policiais militares, todos fictícios. **2** já vêm inativados (afastados), então a lista padrão mostra **78 ativos** |
| Vínculos | 48 vínculos entre agentes e motos ou viaturas |

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

Nada no Inventário é excluído: não existe opção de exclusão em nenhuma tela nem rota `DELETE` na API. O que sai de uso é **inativado** (veja [Inativação](#inativação)).

### Agentes

**Inventário → Agentes** lista os agentes (**78 ativos**) com pesquisa por nome ou matrícula e filtros por **Função**, **Órgão**, **Base**, **Região**, **Situação** e **Cadastros**. Os detalhes de um agente mostram os dados do cadastro, a base de atuação, o equipamento vinculado e a linha do tempo.

- **Novo agente** (só o Administrador) pede só o necessário à operação: nome completo, nome de exibição, matrícula, função, posto ou graduação (quando aplicável), órgão e base de atuação. Não há documento pessoal, endereço residencial nem dado de saúde. Um agente novo nasce **Fora de serviço**, e a mesma matrícula não pode repetir num mesmo órgão.
- **Alterar situação** (Em serviço, Em atendimento, Em pausa, Fora de serviço, Afastado) vale para qualquer situação diferente da atual, sem justificativa, e fica registrado com data, hora e autor. Na demonstração, o simulador (PROMETHEUS) também muda a situação dos agentes quando o turno começa ou termina; essas mudanças aparecem com o autor `PROMETHEUS`.

### Vínculos entre agentes e equipamentos móveis

Um agente pode ser vinculado a uma **moto de agente de trânsito** ou a uma **viatura** (patinetes e táxis não são opções). Nos detalhes do agente, **Vincular a equipamento** abre a lista de equipamentos vinculáveis (30 com a semente 42: 12 motos e 18 viaturas), com pesquisa; **Encerrar vínculo** desfaz o vínculo. Vincular um agente a outro equipamento encerra o vínculo anterior. Os detalhes de uma viatura ou moto mostram os **agentes vinculados**, e os de cada agente mostram o equipamento. Equipamento Desativado ou Furtado não aceita vínculo novo.

### Linha do tempo

**Ver histórico completo** (nos detalhes do equipamento e do agente, e **Ver histórico** na Central) abre a linha do tempo, em ordem cronológica e com data, hora e autor:

- do **equipamento**: cadastro, mudanças de situação, manutenções e alterações de cadastro;
- do **agente**: cadastro, mudanças de situação, alterações e vínculos.

Os alertas da Central não entram na linha do tempo nesta entrega.

### Inativação

**Inativar**, nos detalhes do equipamento ou do agente, tira o cadastro de uso sem apagar nada:

- um **equipamento** só pode ser inativado em **Ainda não instalado**, **Desativado** ou **Furtado**. Um equipamento Operacional não pode: a confirmação é recusada com a explicação. A inativação cancela as manutenções abertas e encerra os vínculos com agentes;
- um **agente** só pode ser inativado em **Fora de serviço** ou **Afastado**, e o vínculo dele é encerrado;
- uma **base** só pode ser inativada quando não tem equipamentos nem agentes ativos;
- o motivo é opcional. O cadastro inativo sai das listas padrão, **não é editado, não muda de situação e não volta a ficar ativo**, mas continua consultável, com todo o histórico, pelo filtro **Cadastros → Inativos** (ou **Ativos e inativos**) nas listas de equipamentos e de agentes. A marca e o número de série de um equipamento inativo continuam reservados.

Com a semente 42, a lista de agentes mostra **78** com **Cadastros: Ativos** (o padrão), **2** com **Inativos** e **80** com **Ativos e inativos**; a de equipamentos, 297 ativos e 3 inativos.

### Cadastros de apoio

**Inventário → Cadastros de apoio** (só o Administrador) tem as abas Órgãos, Regiões, Bairros (com o centro escolhido no mapa) e Bases (com localização e sugestão por CEP), cada uma com criação, edição, inativação e o filtro **Mostrar inativos**. A inativação é recusada, com o motivo, quando ainda há algo ativo ligado ao cadastro (por exemplo, um bairro com equipamentos).

## Central de Operações

A **Central** (menu **Central**, a página inicial) mostra o que está acontecendo agora em Prometeia. O retrato vem da API já filtrado e com os números calculados, e o tempo real (SignalR) atualiza as posições sem recarregar a página.

**O que cada perfil vê:**

| Perfil | Na Central |
|---|---|
| Administrador | Tudo: mapa com equipamentos **e agentes**, indicadores, filtros, alertas (**Reconhecer** e **Resolver**) e detalhes (**Alterar situação** e **Ver histórico**). |
| Operador | O mesmo que o Administrador: é quem trata os alertas e muda a situação pelo painel. |
| Gestor | Só a leitura: indicadores e equipamentos no mapa, **sem marcadores de agentes** (a posição individual não é dele), com a tabela **Agentes em atividade por região**, e **sem** Reconhecer, Resolver nem Alterar situação. |

**O que a tela traz:**

- **Mapa** de Prometeia com o ícone do tipo (ou da função do agente) em cada marcador e a situação em **cor e ícone** (Ainda não instalado aparece vazado e tracejado). Equipamentos móveis e agentes **andam** pelas ruas sem recarregar. Com o mapa afastado, os marcadores se agrupam e mostram a quantidade e a pior situação do grupo. **Legenda do mapa** fica sempre à mão.
- **Indicadores:** Total de equipamentos, Operacionais, Indisponíveis, Disponibilidade (operacionais ÷ (total − ainda não instalados − desativados), sem contar os inativos), Agentes em atividade e Alertas abertos por gravidade. Sem filtros, na carga: **297** equipamentos (198 fixos e 99 móveis, os 3 inativados ficam de fora), **252** operacionais, **27** indisponíveis e **90,3%** de disponibilidade.
- **Filtros** por Tipo, Situação, Necessidade de manutenção, Função do agente (só quem vê agentes), Órgão, Região e Bairro; vários valores no mesmo filtro somam, e filtros diferentes se combinam. Os filtros ativos aparecem como chips, com **Limpar filtros**, e ficam no endereço do navegador. Os indicadores e o mapa acompanham os filtros. Sem recurso, a tela diz "Nenhum recurso com esses filtros.".
- **Alertas abertos:** na carga, **18**, sendo **15 Altos** (equipamentos em mau funcionamento) e **3 Críticos** (as câmeras furtadas no Centro, na Zona Sul e na Zona Oeste). Os alertas seguem **ordenados por gravidade e horário**, e clicar num alerta centraliza o mapa no recurso e abre os detalhes. Os alertas que ficam fora dos filtros aparecem marcados com **Fora do filtro**.
- **Faixa de críticos**, fixa no topo e sempre visível, sem precisar de rolagem nem de clique: "N alertas críticos abertos", com o mais recente, **piscando enquanto houver crítico por reconhecer**. Ela continua à vista (sem piscar) até todos os críticos serem resolvidos, mesmo com filtros ativos.
- **Detalhes:** clicar num marcador abre o painel com tipo, identificação, situação, localização, órgão, características, manutenção, **Última comunicação** e alertas abertos (para o agente: nome, função, órgão, situação, equipamento vinculado e horário da última posição). **Ver histórico** leva à linha do tempo; **Alterar situação** abre o mesmo diálogo do Inventário.
- **Tempo real:** o cabeçalho mostra **Conectado** e **"Última telemetria: HH:mm:ss"**, que avança a cada poucos segundos. Com a API parada, a Central avisa "Tempo real interrompido" e, na volta, reconecta e busca um retrato novo.

**Alertas** (um por equipamento e causa): um equipamento **furtado** gera um alerta **Crítico**, e um em **mau funcionamento**, um **Alto**; as demais causas não fazem parte desta entrega. **Reconhecer** registra quem e quando e faz o alerta parar de piscar, sem tirá-lo da lista. **Resolver** pede uma **observação** (obrigatória) e tira o alerta da lista; um crítico só pode ser resolvido **depois** de reconhecido, e por isso o botão só aparece então. Se o equipamento voltar a falhar enquanto o alerta está aberto, o alerta conta mais uma ocorrência ("2 ocorrências"), em vez de abrir outro.

### Telemetria simulada

O **simulador** (`simulator`) faz o papel do PROMETHEUS: a cada ciclo, envia à API as posições dos equipamentos móveis e dos agentes em atividade, as trocas de turno e o sinal de vida dos equipamentos fixos (`POST /api/telemetry/batches`, com a chave de sistema). Os intervalos são variáveis do `.env`, e no ambiente `Demo` valem:

| Variável | Valor da demonstração | O que controla |
|---|---|---|
| `TELEMETRY_POSITION_INTERVAL_SECONDS` | `5` | Lotes de posições e trocas de turno |
| `TELEMETRY_HEARTBEAT_INTERVAL_SECONDS` | `60` | Sinal de vida dos equipamentos fixos |

Nesta entrega o sinal de vida é parte da telemetria; o antigo endpoint técnico `POST /api/diagnostics/heartbeats` **não existe mais** (a API responde `404`).

**Escala de referência:** cerca de 300 equipamentos e 80 agentes. Os agentes seguem uma **escala de três turnos**, no fuso de Prometeia: manhã (6h às 14h), tarde (14h às 22h) e noite (22h às 6h). **Agentes em atividade** muda com o turno: **27** de manhã, **26** à tarde e **25** à noite (com 12, 4, 4, 4 e 2 por região à tarde: Centro, Zona Leste, Zona Norte, Zona Oeste e Zona Sul). Ao entrar em serviço, o agente aparece na linha do tempo como "Fora de serviço → Em serviço", com autor `PROMETHEUS`.

**Como reproduzir o alerta crítico:** na Central, com o Operador (ou o Administrador), escolha uma câmera Operacional no mapa, clique em **Alterar situação**, escolha **Furtado** e escreva a justificativa. Em até 10 segundos a faixa do topo mostra um novo alerta crítico, piscando, os indicadores mudam (Operacionais e Disponibilidade caem, Indisponíveis sobe) e o alerta entra na lista. O passo a passo está no [Roteiro de demonstração](#roteiro-de-demonstração).

## Cidade Preditiva

O menu **Preditiva** (Administrador e Gestor; o Operador recebe "Acesso negado" na página e `403` na API) mostra o **índice de risco de ocorrências**, de **0 a 100** por região, dia da semana e faixa horária: 100 é a célula mais intensa da cidade. É uma **sugestão para apoiar a decisão**, nunca uma ordem.

- **De onde vêm os dados:** de um **histórico de 12 semanas de ocorrências simuladas** (3.898 com a semente 42: Centro 1.361, Zona Leste 919, Zona Norte 756, Zona Sul 701 e Zona Oeste 161), carregado com os dados de demonstração e que **não muda durante a demonstração**. A tela deixa isso claro: "ocorrências simuladas" e "Dados simulados".
- **Grade por região:** ao escolher uma região, a grade mostra **42 células** (7 dias × 6 faixas de 4 horas), cada uma com o número, o nível por extenso (Baixo, Moderado, Alto e Muito alto), a cor e o ícone. A legenda traz as faixas 0–24, 25–49, 50–74 e 75–100. A célula de índice 100 é a do **Centro, na sexta-feira, das 18h às 22h**, e é a única.
- **Mapa de risco:** com o **Horizonte do mapa** em **Próximas 24 horas** ou **Próximos 7 dias**, cada região é pintada pelo pico do índice no período, com o rótulo "{região} · {nível} ({índice})"; clicar numa região abre a grade dela. O número do mapa depende da hora em que você abre a tela, por isso varia entre uma demonstração e outra.
- **Explicação:** toda região e toda célula traz **o que é**, o **horizonte**, os **dados considerados** (quantas ocorrências simuladas e de que período), os **fatores** do índice, a **confiança** e a **recomendação**. Clicar numa célula da grade mostra a explicação dela; **Voltar à região** retorna à da região.
- **Dados insuficientes:** a **Zona Oeste** tem só 3 semanas de histórico, e a previsão exige pelo menos 4. Por isso ela aparece no mapa como "Zona Oeste · Dados insuficientes", e a tela diz "Dados insuficientes: a Zona Oeste tem 3 semanas de histórico, e a previsão exige pelo menos 4.", sem inventar um índice.

## Roteiro de demonstração

Um passo a passo de cerca de 15 minutos, perfil por perfil. Os números abaixo são os da **semente 42**, logo depois da carga; os horários variam com o dia em que o ambiente sobe.

**Antes de começar:** do zero, para o ambiente estar como na carga (os alertas e as situações que o roteiro altera só se desfazem com isto):

```bash
docker compose down -v
docker compose up --build -d
docker compose ps -a
```

Espere os serviços `healthy` e o `migrator` em `Exited (0)` (um minuto ou dois depois da construção). Abra <http://localhost:8080> e, em cada perfil, use o botão **Preencher** do cartão e **Entrar**; **Sair** (no cabeçalho) troca de perfil.
### 1. Operador (`operador`): a Central

1. **A Central ao vivo.** O cabeçalho mostra **Conectado** e **"Última telemetria: HH:mm:ss"**, que avança a cada ~5 s. Os indicadores mostram **297** equipamentos, **252** operacionais, **27** indisponíveis, **90,3%** de disponibilidade, **26** agentes em atividade (no turno da tarde, das 14h às 22h; 27 de manhã e 25 à noite) e **18** alertas (**3 Críticos** e **15 Altos**). A faixa vermelha do topo pisca: "**3 alertas críticos abertos**". No mapa, as motos, as viaturas e os agentes andam sem recarregar a página.
2. **Reconhecer os 3 críticos da carga.** Na lista **Alertas abertos (18)**, clique em **Reconhecer** nos três alertas **Crítico** ("Equipamento furtado", as câmeras do Centro, da Zona Sul e da Zona Oeste). Cada um passa a "Reconhecido por operador às HH:mm", e a faixa continua visível, **mas para de piscar**. Assim, o furto ao vivo, mais adiante, será o único crítico piscando.
3. **Filtrar.** Em **Tipo**, escolha **Câmera de videomonitoramento**; em **Situação**, **Mau funcionamento**; em **Região**, **Zona Oeste**. Os chips mostram os três filtros, e o mapa e os indicadores ficam só com as **2 câmeras** (Total **2**, Indisponíveis **2**, Disponibilidade 0,0%). Nos alertas aparecem os **2 Altos** delas e os **3 Críticos** com a marca **Fora do filtro**, e a faixa do topo continua com os 3 críticos. Clique em **Limpar filtros**: voltam os **297**.
4. **O furto ao vivo.** Escolha de novo **Câmera de videomonitoramento** e **Zona Oeste**, agora com **Situação: Operacional** (21 câmeras). Clique no marcador de uma câmera: o painel **Detalhes** mostra tipo, identificação, localização, órgão, características, manutenção, **Última comunicação** e alertas abertos. Clique em **Alterar situação**, escolha **Furtado**, escreva a justificativa (por exemplo, "Furto informado pela ronda") e confirme. **Em até 10 s**, sem recarregar:
   - a faixa do topo passa a "**4 alertas críticos abertos**", **piscando**, com "Mais recente: Equipamento furtado · Câmera de videomonitoramento · … (Zona Oeste)";
   - o alerta novo entra no topo da lista, como **Crítico** e **Aberto**;
   - tirados os filtros (**Limpar filtros**), os indicadores passam a **251** operacionais, **28** indisponíveis e **90,0%** de disponibilidade, com o Total ainda em **297**.
5. **Tratar o alerta.** No alerta novo, clique em **Reconhecer**: a faixa para de piscar, e o alerta continua na lista. Só então aparece o botão **Resolver**: clique, deixe a **Observação** vazia e confirme, e o erro aparece no próprio campo; escreva a observação (por exemplo, "Ocorrência registrada na GCM") e confirme: o alerta sai da lista.
6. **Ver histórico.** Clique no marcador da câmera furtada e em **Ver histórico**: a linha do tempo mostra "Operacional → Furtado", com a data, a hora, o autor (`operador`) e a justificativa, e a manutenção periódica que a câmera tinha aberta aparece como "Manutenção cancelada: equipamento furtado".
7. **O que o Operador não vê.** O menu só tem **Central**. Abrir `/predictive` ou `/inventory/equipment` mostra "Acesso negado".

### 2. Gestor (`gestor`): indicadores e Preditiva

Entre com **Sair** → **Preencher** no cartão do **Gestor** → **Entrar**.

1. **Central, só leitura.** Os mesmos indicadores (agora com a câmera furtada, se o item 4 do Operador foi feito: 251 operacionais) e o alerta na lista, **sem** os botões Reconhecer, Resolver e Alterar situação e **sem marcadores de agentes** no mapa. Abaixo do mapa, a tabela **Agentes em atividade por região** (no turno da tarde: Centro **12**, Zona Leste **4**, Zona Norte **4**, Zona Oeste **4**, Zona Sul **2**, ou seja, **26**).
2. **A Preditiva.** No menu, **Preditiva** abre a **Cidade Preditiva**, na aba **Risco por região**. Leia a legenda do **Índice de risco** (Baixo 0–24, Moderado 25–49, Alto 50–74, Muito alto 75–100) e o aviso de que a previsão é uma sugestão.
3. **A grade da Zona Leste.** Em **Região**, escolha **Zona Leste**: a **Grade de risco** mostra **42 células** (dias da semana × faixas de 4 horas), cada uma com número, nível por extenso, cor e ícone.
4. **Explicar uma célula.** Clique numa célula de nível **Alto** (por exemplo, terça-feira, 14h–18h): abre a **Explicação da célula**, com **O que é**, **Horizonte**, **Dados considerados** (as ocorrências simuladas da Zona Leste, 919, de 12 semanas), **Fatores**, **Confiança** e **Recomendação**. **Voltar à região** mostra a explicação da região.
5. **O pico da cidade.** Escolha **Centro**: a célula de **sexta-feira, 18h–22h** tem índice **100** ("Muito alto"), a única.
6. **O mapa de risco.** Em **Horizonte do mapa**, escolha **Próximas 24 horas** e depois **Próximos 7 dias**: o mapa mostra **4 regiões coloridas**, cada uma com o rótulo "{região} · {nível} ({índice})". Clicar numa região abre a grade dela.
7. **Dados insuficientes.** A **Zona Oeste** aparece no mapa como "Zona Oeste · Dados insuficientes". Escolha **Zona Oeste** em **Região**: a tela explica "Dados insuficientes: a Zona Oeste tem 3 semanas de histórico, e a previsão exige pelo menos 4." e a explicação traz as 161 ocorrências simuladas, sem índice.
8. **Só consulta.** Em **Inventário → Equipamentos** e **Agentes**, o Gestor consulta, mas não vê botões de escrita (Novo, Editar, Inativar, Alterar situação).

### 3. Administrador (`admin`): Inventário

Entre com o cartão do **Administrador**.

1. **Equipamentos.** **Inventário → Equipamentos** lista os 297 ativos (300 com os inativos). Pesquise `crepusculo` (os equipamentos da Vila Crepúsculo) e filtre (por exemplo, Tipo **Câmera de videomonitoramento**, Situação **Mau funcionamento** e Região **Centro**); **Limpar filtros** volta à lista inteira. Para o cadastro de um equipamento novo, siga [Cadastrar um equipamento](#cadastrar-um-equipamento).
2. **Agentes.** **Inventário → Agentes** mostra **78 agentes** (com o filtro padrão **Cadastros: Ativos**). Filtre por **Função**, **Órgão**, **Base**, **Região** ou **Situação**. Em **Cadastros**, escolha **Ativos e inativos**: são **80**.
3. **Os inativos.** Em **Cadastros**, escolha **Inativos**: aparecem os **2 agentes afastados** inativados na carga, marcados "Inativo". Na lista de **Equipamentos**, o mesmo filtro **Cadastros → Inativos** mostra os **3 equipamentos** desativados e inativados (um veículo de transporte e dois totens). Abra um deles: não há **Editar** nem **Alterar situação**, mas o histórico segue consultável.
4. **Vínculos.** Em **Equipamentos**, pesquise `95V3RLTG0UNZK80CY` (a viatura GCM-V06): os detalhes mostram, em **Agentes vinculados**, os guardas **Campos** e **Pinheiro**. Abra um agente (por exemplo, o Pinheiro): o equipamento vinculado é a viatura. **Vincular a equipamento** abre a lista de equipamentos vinculáveis (30 no total, só motos de agente de trânsito e viaturas); **Encerrar vínculo** (opcional: muda a lista de vinculáveis) desfaz o vínculo, que aparece na linha do tempo.
5. **Linha do tempo com o PROMETHEUS.** Em **Agentes**, filtre **Situação: Em serviço**, abra um agente, clique em **Ver histórico completo**: a linha do tempo mostra **"Fora de serviço → Em serviço"** com o autor **`PROMETHEUS`** (a entrada de cadastro, de meses atrás, tem o autor `simulação`).
6. **Inativar.** Abra a viatura do passo 4 (Operacional) e clique em **Inativar**, depois em **Inativar** na confirmação: a recusa explica: "Equipamento na situação Operacional não pode ser inativado. A inativação só é possível nas situações Ainda não instalado, Desativado ou Furtado." Para inativar de verdade, mude a situação para **Desativado** (com justificativa) e inative; o cadastro sai da lista padrão e aparece em **Cadastros → Inativos**, com os vínculos encerrados. Não existe nenhuma opção de excluir.
7. **Central e Preditiva.** O Administrador também usa a Central, como o Operador, e consulta a Preditiva, como o Gestor.

**Para repetir a demonstração**, volte ao começo: `docker compose down -v` e `docker compose up --build -d`.

## Carga inicial e versões do seed

A cada subida, o `migrator` aplica as migrations e a carga inicial, sem duplicar nada. Cada parte da carga é uma **versão do seed**, gravada no banco junto com a semente usada, na mesma transação dos dados:

| Versão | O que carrega |
|---|---|
| `foundation` | Só o registro de fundação do ambiente, sem dados |
| `inventory-reference-v1` | Município, regiões, bairros, CEPs, órgãos, bases e os 10 tipos |
| `inventory-catalog-v1` | Os 300 equipamentos, com situações e manutenções |
| `inventory-agents-v1` | Os 80 agentes e os 48 vínculos com motos e viaturas |
| `inventory-inactivations-v1` | A inativação de 3 equipamentos e 2 agentes de demonstração |
| `operations-duty-roster-v1` | A escala de turnos dos agentes (manhã 27, tarde 26, noite 25) |
| `operations-alerts-v1` | Os 18 alertas abertos da carga: 15 Altos e 3 Críticos |
| `predictive-occurrences-v1` | As 3.898 ocorrências simuladas de 12 semanas, usadas pela Preditiva |

Na primeira subida, o log do `migrator` mostra uma linha `Carga <versão> aplicada com a semente 42: ...` para cada versão, com as quantidades. Nas seguintes, mostra `... já aplicada; nada a fazer.` e não grava nada. Se uma carga falhar no meio, nada dela fica gravado, e a próxima subida tenta de novo.

**Subir sobre um banco que já existe:** cada versão do seed é aplicada uma única vez, e as novas entram sem apagar nada. Quem já tem o volume de uma subida anterior só precisa rodar `docker compose up --build` de novo (**sem `down -v`**): o `migrator` aplica as migrations e as cargas que faltam (agentes, inativações, escala, alertas e ocorrências) e deixa o que já existe como está. Com `down -v`, tudo é carregado de novo.

Para ver o log: `docker compose logs migrator`.

**Trocar a semente:** se o banco já tem uma carga feita com outra semente, o `migrator` para com a mensagem `A carga ... já foi aplicada com a semente 42. Para usar a semente 7, recomece o banco (docker compose down -v).` Para usar outra semente, [recomece do zero](#recomeçar-do-zero) com a nova `SIMULATION_SEED` no `.env`.

### Parâmetros de negócio (`BusinessParameters`)

Os parâmetros de negócio ajustáveis pelo Administrador ficam no banco. Nesta entrega existe um: `maintenance.upcoming-window-days`, quantos dias antes da data prevista uma manutenção passa a ser **Próxima** (padrão 7, de 1 a 60). O valor inicial vem da seção `BusinessParameters` de `src/Argos.Migrator/appsettings.json`:

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
| `TELEMETRY_POSITION_INTERVAL_SECONDS` | Intervalo entre os lotes de telemetria do simulador (posições e trocas de turno) | segundos; padrão `5` |
| `TELEMETRY_HEARTBEAT_INTERVAL_SECONDS` | Intervalo do sinal de vida dos equipamentos fixos simulados | segundos; padrão `60` |
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
curl -i http://localhost:8081/api/telemetry/resources \
  -H "Authorization: Bearer <accessToken>"

# O mesmo endpoint com a chave de sistema: 200
curl -i http://localhost:8081/api/telemetry/resources \
  -H "X-Argos-System-Key: troque-esta-chave-de-sistema-do-simulador"
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

Roda os testes unitários e os de integração, incluindo login, `401`/`403`, a matriz de permissões por perfil, o recorte do tempo real por perfil, as regras e os endpoints do Inventário, a telemetria, os alertas e a Central e a Preditiva (índice de risco, grade e previsão). A Web não tem testes automatizados: ela é conferida no navegador, pelo [Roteiro de demonstração](#roteiro-de-demonstração). Os testes usam chaves, usuários e dados próprios, sem o `.env` e sem a carga de Prometeia. Os testes de integração sobem um PostgreSQL 17 descartável com o Testcontainers, então **o Docker precisa estar em execução**. Para rodar um projeto só:

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
