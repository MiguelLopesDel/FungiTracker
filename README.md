# EcoMyceliumTracker

API REST para registrar redes de micélio, sensores de umidade e transferências de carbono. Construída com ASP.NET Core Minimal APIs, Dapper e PostgreSQL.

## Funcionalidades

- CRUD de redes e sensores, incluindo ativação e desativação de sensores.
- Transferências imutáveis, com validação transacional dos sensores.
- Paginação e filtros em todas as listagens.
- Autenticação por API key, CORS restritivo e rate limiting.
- Migrations automáticas e versionadas.
- Swagger UI (fora de produção), health checks e respostas no padrão Problem Details.
- Logs estruturados e telemetria OpenTelemetry via OTLP.
- Testes unitários, testes de integração e pipeline de CI.

## Início rápido com Docker

```bash
API_KEY=uma-chave-local-segura docker compose up --build
```

A API estará em `http://localhost:5236`, e o Swagger UI em `http://localhost:5236/swagger`. O PostgreSQL fica disponível em `localhost:5432`. As migrations são executadas automaticamente na inicialização da API.

Para encerrar os containers:

```bash
docker compose down
```

Use `docker compose down -v` somente quando também quiser apagar definitivamente o banco local.

## Execução sem Docker

Pré-requisitos:

- SDK do .NET 10
- PostgreSQL

Na raiz do repositório:

```bash
cp .env.example .env
dotnet restore
dotnet run --project EcoMyceliumTracker
```

Edite o `.env` com sua connection string, uma API key longa e as origens permitidas. O arquivo é ignorado pelo Git. Execute o comando a partir da raiz do repositório para que ele seja carregado.

Todas as rotas em `/api` exigem o header:

```http
X-API-Key: sua-chave
```

## Endpoints

| Método | Rota | Descrição |
| --- | --- | --- |
| `GET` | `/health/live` | Confirma que o processo está respondendo |
| `GET` | `/health/ready` | Confirma conectividade com o PostgreSQL |
| `GET` | `/api/networks` | Lista e filtra redes |
| `GET` | `/api/networks/{id}` | Consulta uma rede |
| `POST` | `/api/networks` | Cria uma rede |
| `PUT` | `/api/networks/{id}` | Atualiza uma rede |
| `DELETE` | `/api/networks/{id}` | Exclui uma rede e seus dados dependentes |
| `GET` | `/api/networks/{id}/sensors` | Lista sensores de uma rede |
| `GET` | `/api/sensors/{id}` | Consulta um sensor |
| `POST` | `/api/sensors` | Cria um sensor |
| `PUT` | `/api/sensors/{id}` | Atualiza um sensor |
| `PATCH` | `/api/sensors/{id}/status` | Ativa ou desativa um sensor |
| `DELETE` | `/api/sensors/{id}` | Exclui um sensor e suas transferências |
| `GET` | `/api/transfers` | Lista e filtra transferências |
| `GET` | `/api/transfers/high-energy` | Lista transferências a partir do limite configurado (inclusive) |
| `GET` | `/api/transfers/{id}` | Consulta uma transferência |
| `POST` | `/api/transfers` | Registra uma transferência |

Listagens aceitam `page` e `pageSize` — o limite máximo é 100. Redes aceitam `scientificName` e `soilType`; sensores aceitam `isActive`; transferências aceitam `minimumCarbonMg`, `sourceNodeId`, `targetNodeId`, `from` e `to`.

Exemplos executáveis estão em [EcoMyceliumTracker.http](EcoMyceliumTracker/EcoMyceliumTracker.http).

## Regras das redes e sensores

- `scientificName` (até 200 caracteres) e `soilType` (até 100) são obrigatórios.
- `discoveredAt` é obrigatória e não pode estar no futuro.
- `location` usa o formato `x,y` ou `(x,y)`, com ponto como separador decimal.
- `moistureLevel` fica entre 0 e 100, inclusive.
- Excluir uma rede exclui seus sensores e as transferências deles.

## Regras das transferências

- Origem e destino devem ser sensores diferentes, ativos e pertencentes à mesma rede.
- `carbonAmountMg` deve ser positivo.
- `transferredAt` é opcional; quando omitido, a API usa o horário atual em UTC.
- Datas futuras são rejeitadas.
- Transferências são eventos imutáveis e, por isso, não possuem rotas de alteração ou exclusão.

## Configuração

| Chave | Descrição | Padrão |
| --- | --- | --- |
| `ConnectionStrings__PostgresConnection` | Connection string do PostgreSQL | obrigatório |
| `Authentication__ApiKey` | Chave enviada em `X-API-Key` | obrigatório para acessar a API |
| `Cors__AllowedOrigins__0` | Primeira origem web permitida | nenhuma |
| `RateLimit__PermitLimit` | Requisições permitidas por janela | `100` |
| `RateLimit__WindowSeconds` | Duração da janela | `60` |
| `RateLimit__QueueLimit` | Requisições que aguardam na fila em vez de receber 429 | `0` |
| `Database__RunMigrations` | Executa as migrations no startup. Desligue apenas quando o schema for aplicado por fora; com `false` a aplicação sobe contra um banco possivelmente desatualizado | `true` |
| `Transfers__HighEnergyThresholdMg` | Limite de alta energia | `500` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint do collector OpenTelemetry | desabilitado |

Em produção, os logs são emitidos como JSON. Ao configurar `OTEL_EXPORTER_OTLP_ENDPOINT`, logs, traces HTTP, traces do PostgreSQL e métricas de runtime são enviados por OTLP.

## Testes e qualidade

```bash
dotnet format EcoMyceliumTracker.sln --verify-no-changes
dotnet build EcoMyceliumTracker.sln
dotnet test EcoMyceliumTracker.sln --settings coverlet.runsettings --results-directory TestResults
python3 scripts/check-coverage.py --results TestResults --threshold 85
```

Os testes de integração usam a variável `TEST_POSTGRES_CONNECTION`. Sem ela, são ignorados — para que `dotnet test` funcione numa máquina sem banco. **Mas quando `CI=true`, a variável ausente faz os testes falharem em vez de pular**: um teste ignorado é invisível num pipeline verde, então uma secret renomeada poderia desligar a suíte de integração inteira sem ninguém perceber. A CI fornece um PostgreSQL dedicado e executa todo o cenário HTTP.

### Gates

| Gate | Onde | O que reprova |
| --- | --- | --- |
| Formatação | `dotnet format --verify-no-changes` | qualquer desvio do `.editorconfig` |
| Analisadores | build (`AnalysisMode=All`) | qualquer aviso, porque `TreatWarningsAsErrors` está ligado |
| Dependências | restore (`NuGetAudit`) | pacote vulnerável, via `NU1901`–`NU1904` promovidos a erro |
| Manutenibilidade | build (SonarAnalyzer + Roslynator) | limites da tabela abaixo |
| Cobertura | `scripts/check-coverage.py` | cobertura de linha abaixo de 85% |
| Mutação | `dotnet stryker` | score abaixo de 70% |

#### Orçamento de manutenibilidade

Os analisadores da Microsoft julgam correção e design de API, mas quase não
falam sobre legibilidade. Estas regras cobrem o que falta e **vêm desligadas
por padrão** — por isso nada impedia um método de 200 linhas de entrar:

| Limite | Regra | Valor |
| --- | --- | --- |
| Complexidade cognitiva | `S3776` | 15 |
| Complexidade ciclomática | `S1541` | 10 |
| Linhas por método | `S138` | 80 |
| Linhas por arquivo | `S104` | 1000 |
| Parâmetros por método | `S107` | 7 |
| Aninhamento de controle | `S134` | 3 |
| Literal repetida | `S1192` | 3 ocorrências |
| Número sem nome | `S109` | — |
| Código comentado | `S125` | — |

Todos são erro de compilação, não aviso.

O piso de cobertura é 85% porque a cobertura atual é 89,8%: ele existe para impedir regressão, não como meta. O mesmo vale para o piso de mutação.

As regras de analisador desligadas estão no fim do `.editorconfig`, cada uma com o motivo. A regra é: só se desliga o que não se aplica a uma aplicação ASP.NET Core — o que aponta defeito real se corrige no código.

### Mutation testing

```bash
dotnet tool restore
dotnet stryker
```

Roda sobre `Application/` e `Validation/`, as partes cobertas por testes unitários puros. No CI fica em workflow separado (manual e semanal), porque é lento demais para bloquear pull request.

### Hook de pre-commit

```bash
git config core.hooksPath .githooks
```

Roda formatação, build e testes unitários antes de cada commit. Os testes de integração ficam de fora porque exigem PostgreSQL. Para pular um commit específico: `git commit --no-verify`.

## Desempenho

As listagens foram medidas com 200 mil redes. Antes dos índices da migration
`002`, toda consulta era varredura sequencial da tabela inteira:

| Consulta | Antes | Depois |
| --- | --- | --- |
| Listagem, página 1 | 17 ms | **0,04 ms** |
| Filtro `ILIKE` | 54 ms | **7,5 ms** |
| `COUNT(*)` do filtro | 97 ms | **6,8 ms** |
| Paginação profunda (`OFFSET` 100k) | 131 ms | **34 ms** |

Uma listagem filtrada faz duas dessas consultas, então saiu de ~150 ms para
~14 ms de banco.

Os filtros de texto usam `ILIKE '%termo%'`. O curinga à esquerda impede o uso
de índice btree, então eles dependem de índices trigram (`pg_trgm`). Criar a
extensão exige um privilégio que uma instância gerenciada pode negar; se
falhar, a migration segue sem os índices trigram e os filtros voltam a fazer
varredura — funcionam, só mais devagar.

Paginação profunda continua custando `OFFSET`: o banco ainda percorre as
linhas puladas. Para catálogos grandes, paginação por cursor seria o próximo
passo.

Cada requisição faz um número fixo de consultas — não há N+1. As listagens
trazem página e total num único round trip, e a criação de transferência
aproveita os sensores que já lê sob `FOR SHARE` para montar a resposta, sem
consulta adicional.

## Estrutura

```text
EcoMyceliumTracker/
├── Application/        # Regras de negócio e orquestração (não conhece HTTP)
├── Contracts/          # Entradas da API
├── Endpoints/          # Rotas: traduzem HTTP <-> Application
├── Infrastructure/     # Erros, health checks e migrations
├── Models/             # Modelos persistidos e paginação
├── Repositories/       # Acesso ao PostgreSQL e regras transacionais
├── Security/           # Autenticação por API key
└── Validation/         # Validadores sem dependência HTTP
tests/
├── EcoMyceliumTracker.UnitTests/
└── EcoMyceliumTracker.IntegrationTests/
```

O fluxo é `Endpoints -> Application -> Repositories`. Cada camada só depende
das de baixo, e nada abaixo de `Endpoints` conhece HTTP.

### Por que existe a camada Application

Antes, uma regra de negócio podia estar em quatro lugares: no validador, solta
dentro do endpoint, dentro do repositório e como constraint no banco. Não havia
onde ler "as regras de uma transferência", e como as regras moravam atrás de
HTTP ou atrás de SQL, verificá-las exigia subir a aplicação inteira com um
PostgreSQL: **apenas 7,6% do código era exercitável sem banco**.

`Application/` concentra essas regras. Os endpoints só traduzem HTTP, e os
repositórios só falam com o banco. O erro de domínio (`DomainException`) deixou
de carregar status HTTP — carrega um `DomainErrorKind`, e o
`ApiExceptionHandler` é o único ponto que converte isso em status code.

O resultado é medível: **96,3% das regras de negócio agora são cobertas por
testes unitários, sem banco nenhum**, e os endpoints encolheram cerca de 70%.
Os testes de integração continuam existindo para provar que a ponta HTTP e o
SQL de fato funcionam.
