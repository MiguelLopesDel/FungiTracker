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
| `GET` | `/api/transfers/high-energy` | Lista transferências acima do limite configurado |
| `GET` | `/api/transfers/{id}` | Consulta uma transferência |
| `POST` | `/api/transfers` | Registra uma transferência |

Listagens aceitam `page` e `pageSize` — o limite máximo é 100. Redes aceitam `scientificName` e `soilType`; sensores aceitam `isActive`; transferências aceitam `minimumCarbonMg`, `sourceNodeId`, `targetNodeId`, `from` e `to`.

Exemplos executáveis estão em [EcoMyceliumTracker.http](EcoMyceliumTracker/EcoMyceliumTracker.http).

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
| `Transfers__HighEnergyThresholdMg` | Limite de alta energia | `500` |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Endpoint do collector OpenTelemetry | desabilitado |

Em produção, os logs são emitidos como JSON. Ao configurar `OTEL_EXPORTER_OTLP_ENDPOINT`, logs, traces HTTP, traces do PostgreSQL e métricas de runtime são enviados por OTLP.

## Testes e qualidade

```bash
dotnet format EcoMyceliumTracker.sln --verify-no-changes
dotnet build EcoMyceliumTracker.sln
dotnet test EcoMyceliumTracker.sln
dotnet list EcoMyceliumTracker/EcoMyceliumTracker.csproj package --vulnerable --include-transitive
```

Os testes de integração usam a variável `TEST_POSTGRES_CONNECTION`. Quando ela não está definida, eles são marcados como ignorados; a CI fornece automaticamente um PostgreSQL dedicado e executa todo o cenário HTTP.

## Estrutura

```text
EcoMyceliumTracker/
├── Contracts/          # Entradas da API
├── Endpoints/          # Rotas agrupadas por domínio
├── Infrastructure/     # Erros, health checks e migrations
├── Models/             # Modelos persistidos e paginação
├── Repositories/       # Acesso ao PostgreSQL e regras transacionais
├── Security/           # Autenticação por API key
└── Validation/         # Validadores sem dependência HTTP
tests/
├── EcoMyceliumTracker.UnitTests/
└── EcoMyceliumTracker.IntegrationTests/
```
