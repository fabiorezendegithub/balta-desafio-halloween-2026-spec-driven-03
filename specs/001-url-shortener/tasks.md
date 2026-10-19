# Tarefas: Encurtador de URLs

**Entrada**: documentos de design em `specs/001-url-shortener/`

**Pré-requisitos**: [plan.md](./plan.md) (obrigatório), [spec.md](./spec.md) (obrigatório para histórias), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/openapi.yaml](./contracts/openapi.yaml), [quickstart.md](./quickstart.md)

**Testes**: obrigatórios. A Constituição exige teste unitário xUnit para cada regra de negócio (Princípio IV) e teste de integração de caminho feliz para cada endpoint, com verificação do 400 em ProblemDetails nos endpoints que validam entrada (Princípios V e VI). Cada história escreve os testes antes da implementação, e eles devem falhar antes dela.

**Organização**: tarefas agrupadas por história de usuário, para implementação e teste independentes.

## Formato: `[ID] [P?] [Story] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tarefa incompleta)
- **[Story]**: história a que a tarefa pertence (US1, US2, US3)
- Caminhos de arquivo exatos na descrição

## Convenções de caminho

Projetos conforme [plan.md](./plan.md), seção "Estrutura do Projeto":
- `src/Encurtador.Api/`, `src/Encurtador.Application/`, `src/Encurtador.Infrastructure/`
- `tests/Encurtador.UnitTests/`, `tests/Encurtador.IntegrationTests/`

---

## Fase 1: Setup (infraestrutura compartilhada)

**Propósito**: criar a solução, os projetos e as dependências aprovadas no plano.

- [ ] T001 Criar a solução `Encurtador.sln` na raiz do repositório e os cinco projetos com `net10.0`: `src/Encurtador.Api/Encurtador.Api.csproj` (web), `src/Encurtador.Application/Encurtador.Application.csproj` (classlib), `src/Encurtador.Infrastructure/Encurtador.Infrastructure.csproj` (classlib), `tests/Encurtador.UnitTests/Encurtador.UnitTests.csproj` e `tests/Encurtador.IntegrationTests/Encurtador.IntegrationTests.csproj`
- [ ] T002 Criar `Directory.Build.props` na raiz com `Nullable` habilitado, `ImplicitUsings` habilitado e `TreatWarningsAsErrors` como `true`. Nenhuma dependência nova
- [ ] T003 Configurar referências de projeto em `src/Encurtador.Api/Encurtador.Api.csproj` (→ Application e Infrastructure), `src/Encurtador.Infrastructure/Encurtador.Infrastructure.csproj` (→ Application), `tests/Encurtador.UnitTests/Encurtador.UnitTests.csproj` (→ Application) e `tests/Encurtador.IntegrationTests/Encurtador.IntegrationTests.csproj` (→ Api). Depende de T001
- [ ] T004 [P] Adicionar `Microsoft.EntityFrameworkCore.Sqlite` 10.0.x e `Microsoft.EntityFrameworkCore.Design` 10.0.x com `PrivateAssets=all` em `src/Encurtador.Infrastructure/Encurtador.Infrastructure.csproj`. Justificativa em `plan.md`, seção "Dependências Novas". Depende de T001
- [ ] T005 [P] Adicionar xUnit (base do stack, sem justificativa extra), `xunit.runner.visualstudio` e `Microsoft.NET.Test.Sdk` em `tests/Encurtador.UnitTests/Encurtador.UnitTests.csproj`. Depende de T001
- [ ] T006 [P] Adicionar `Microsoft.AspNetCore.Mvc.Testing` 10.0.x, xUnit e `Microsoft.NET.Test.Sdk` em `tests/Encurtador.IntegrationTests/Encurtador.IntegrationTests.csproj`. Não adicionar `Microsoft.Extensions.TimeProvider.Testing`, que não foi aprovado no plano. Depende de T001
- [ ] T007 [P] Garantir que `.gitignore` na raiz ignore `bin/`, `obj/` e arquivos de banco local (`*.db`, `*.db-shm`, `*.db-wal`)

**Checkpoint**: a solução compila com `dotnet build`, sem nenhuma regra de negócio ainda.

---

## Fase 2: Fundação (pré-requisitos bloqueantes)

**Propósito**: entidade, persistência, composição do provider e infraestrutura de teste. **Nenhuma história pode começar antes desta fase terminar.**

**⚠️ CRÍTICO**: nenhum trabalho de história começa antes do checkpoint desta fase.

- [ ] T008 [P] Criar `src/Encurtador.Application/Links/Link.cs`, a entidade de domínio conforme `data-model.md`. Propriedades e restrições:
  - `Code`: "texto, até 20 caracteres", usado só para exibição
  - `CodeKey`: "`Code` em minúsculas. **Índice único.**", até 20 caracteres
  - `TargetUrl`: "texto, até 2048 caracteres", "Imutável nesta versão"
  - `CreatedAtUtc`: "data e hora com offset, normalizada para UTC", obrigatório
  - `ExpiresAtUtc`: "normalizada para UTC", "Nulo significa que o link não expira"
  - `ClickCount`: "inteiro longo", padrão 0, "Só cresce"
  - `ManagementKeyHash`: "texto, 64 caracteres", SHA-256 em hexadecimal
  - Método `IsExpiredAt(DateTimeOffset nowUtc)`: retorna `true` quando `ExpiresAtUtc <= nowUtc`. "No instante exato de ExpiresAtUtc, o link já é expirado"
- [ ] T009 [P] Criar `src/Encurtador.Application/Links/AccessOutcome.cs` com os resultados de acesso: `Redirected(string TargetUrl)`, `Expired` e `NotFound`
- [ ] T010 [P] Criar `src/Encurtador.Application/Links/LinkResult.cs`, um resultado tipado com `ErrorKind` (`Validation`, `NotFound`, `Expired`, `Unauthorized`, `RateLimited`) e mensagem em português. Sem exceção para fluxo de erro
- [ ] T011 [P] Criar `src/Encurtador.Application/Links/LinkOptions.cs` como record `LinkOptions(string PublicBaseUrl, int CreationLimitPerHour)`, sem dependência de `Microsoft.Extensions.Options`
- [ ] T012 Criar `src/Encurtador.Application/Links/ILinkRepository.cs` com:
  - `Task<bool> TryInsertAsync(Link link, CancellationToken ct)`: `false` quando há violação de unicidade de `CodeKey`
  - `Task<Link?> FindByCodeKeyAsync(string codeKey, CancellationToken ct)`
  - `Task<AccessOutcome> RegisterAccessAsync(string codeKey, DateTimeOffset nowUtc, CancellationToken ct)`

  Depende de T008, T009
- [ ] T013 Criar `src/Encurtador.Infrastructure/Persistence/AppDbContext.cs` com `DbSet<Link> Links` e aplicação de `LinkConfiguration`. Depende de T008
- [ ] T014 Criar `src/Encurtador.Infrastructure/Persistence/LinkConfiguration.cs` com:
  - índice único `UQ_Link_CodeKey` em `CodeKey` (defesa de unicidade, D1)
  - check `CK_Link_ClickCount_NonNegative`: "Restrição de banco: `ClickCount >= 0`" (REQ-012)
  - comprimento máximo: `Code` e `CodeKey` 20, `TargetUrl` 2048, `ManagementKeyHash` 64
  - `ExpiresAtUtc` e `CreatedAtUtc` como `DateTimeOffset`
  - nenhum índice em `ExpiresAtUtc`

  Depende de T013
- [ ] T015 [P] Criar `src/Encurtador.Infrastructure/Persistence/UniqueConstraintDetector.cs`, que identifica violação de unicidade por provider. Para SQLite: `SqliteException` com `SqliteExtendedErrorCode == 2067` (`SQLITE_CONSTRAINT_UNIQUE`). Deixar espaço para o código do PostgreSQL (`23505`), sem implementá-lo agora. Depende de T004
- [ ] T016 Criar `src/Encurtador.Infrastructure/Persistence/EfLinkRepository.cs` implementando `ILinkRepository` somente com LINQ e a API do EF Core:
  - `TryInsertAsync`: `Add` e `SaveChangesAsync`. Retorna `false` apenas quando `UniqueConstraintDetector` confirmar a violação de unicidade
  - `FindByCodeKeyAsync`: `FirstOrDefaultAsync`
  - `RegisterAccessAsync`: `ExecuteUpdateAsync(s => s.SetProperty(l => l.ClickCount, l => l.ClickCount + 1))` com filtro `CodeKey == codeKey && (ExpiresAtUtc == null || ExpiresAtUtc > nowUtc)`, na mesma instrução. Se afetar 0 linhas, uma segunda consulta distingue `NotFound` de `Expired`. Se afetar 1, lê `TargetUrl` e retorna `Redirected`

  Proibido: SQL manual e leitura seguida de reescrita do contador (D2). Depende de T012, T014, T015
- [ ] T017 Criar `src/Encurtador.Infrastructure/DependencyInjection.cs` com `AddInfrastructure(IConfiguration)`. Lê `Database:Provider` (valor `Sqlite`) e `Database:ConnectionString`. É o único lugar que chama `UseSqlite`. Registra `AppDbContext` e `ILinkRepository` → `EfLinkRepository`. Provider desconhecido lança exceção com mensagem clara. Depende de T013, T016
- [ ] T018 Gerar a migração inicial com `dotnet ef migrations add InitialCreate --project src/Encurtador.Infrastructure --startup-project src/Encurtador.Api --output-dir Migrations/Sqlite`. Depende de T014, T017
- [ ] T019 Criar `src/Encurtador.Api/Program.cs` base: chamada a `AddInfrastructure`, `Database.MigrateAsync()` na inicialização, `AddProblemDetails()`, `TimeProvider.System` registrado como singleton, `LinkOptions` lido de `Links:PublicBaseUrl` e `Links:CreationLimitPerHour`. Declarar `public partial class Program { }` ao final, para os testes de integração. Depende de T017
- [ ] T020 [P] Criar `src/Encurtador.Api/appsettings.json` com `Database:Provider` = `Sqlite`, `Database:ConnectionString` = `Data Source=encurtador.db`, `Links:PublicBaseUrl` = `http://localhost:5000` e `Links:CreationLimitPerHour` = `20`. Criar também `src/Encurtador.Api/appsettings.Development.json` vazio, com a seção `Links`, para o passo 11 do quickstart
- [ ] T021 [P] Criar `src/Encurtador.Api/Http/ProblemMapper.cs`, que mapeia `LinkResult.ErrorKind` para status HTTP (`Validation` → 400, `NotFound` → 404, `Expired` → 410, `Unauthorized` → 401, `RateLimited` → 429) e produz corpo `application/problem+json` com `title` e `detail` em português. Depende de T010
- [ ] T022 Criar `tests/Encurtador.IntegrationTests/LinkApiFactory.cs`, uma `WebApplicationFactory<Program>` que substitui `Database:ConnectionString` por um arquivo SQLite temporário único por instância, apagado no `Dispose`. Aceita sobrescrever `Links:CreationLimitPerHour`. Depende de T019, T006
- [ ] T023 Criar `tests/Encurtador.IntegrationTests/DatabaseConstraintsTests.cs`, que prova a defesa no banco sem passar pelo serviço:
  - inserir dois `Link` com o mesmo `CodeKey` pelo `AppDbContext` falha por unicidade (D1)
  - inserir `ClickCount = -1` falha pelo check (REQ-012)

  Depende de T014, T022

**Checkpoint**: fundação pronta. O banco tem o índice único e o check, a composição do provider existe, e a factory de teste sobe a aplicação com SQLite em arquivo temporário. As histórias podem começar.

---

## Fase 3: História de Usuário 1, criar link curto (Prioridade: P1) 🎯 MVP

**Objetivo**: o criador envia uma URL longa e recebe um link curto, com código gerado ou personalizado, e uma chave de gerenciamento entregue uma única vez.

**Teste independente**: `POST /api/links` com uma URL válida retorna `201` com código no formato esperado e chave de 43 caracteres. Entradas inválidas retornam `400` em ProblemDetails, sem criar nenhum link.

### Testes da História 1 (obrigatórios: Princípios IV, V e VI) ⚠️

> **Escreva estes testes antes da implementação. Eles devem falhar até a tarefa de implementação correspondente.**

- [ ] T024 [P] [US1] Criar `tests/Encurtador.UnitTests/Fakes/InMemoryLinkRepository.cs`, implementação em memória de `ILinkRepository` que respeita unicidade de `CodeKey` e a mesma regra de expiração de `RegisterAccessAsync` (D2). Criar também `tests/Encurtador.UnitTests/Fakes/FixedTimeProvider.cs`, uma subclasse de `TimeProvider` com instante fixo, sem pacote novo
- [ ] T025 [P] [US1] Criar `tests/Encurtador.UnitTests/UrlValidatorTests.cs`. Aceita `http://` e `https://` com até 2048 caracteres. Recusa `ftp://`, `javascript:`, URL sem esquema e URL com 2049 caracteres (REQ-005, cenários 4 e 5 da história)
- [ ] T026 [P] [US1] Criar `tests/Encurtador.UnitTests/ShortCodeGeneratorTests.cs`. Gera exatamente 7 caracteres (REQ-001). Todos pertencem ao alfabeto `23456789abcdefghjkmnpqrstuvwxyz`. Em uma amostra de 10.000 códigos, nenhum contém `0`, `1`, `i`, `l` ou `o` (D4)
- [ ] T027 [P] [US1] Criar `tests/Encurtador.UnitTests/ManagementKeyTests.cs`. A chave gerada tem 43 caracteres base64url (REQ-013). O hash tem 64 caracteres hexadecimais. `Verify` aceita a chave correta e recusa outra chave de mesmo tamanho (D3)
- [ ] T028 [US1] Criar `tests/Encurtador.UnitTests/LinkServiceCreateTests.cs`, com as regras de criação no `LinkService`. Depende de T024:
  - código personalizado de 4 a 20 alfanuméricos aceito; 3 caracteres, 21 caracteres e `abc-123` recusados (REQ-002)
  - código reservado recusado sem diferenciar maiúsculas, por exemplo `Health` (REQ-018)
  - data de expiração no passado recusada (REQ-007); data futura aceita
  - sem data de expiração, `ExpiresAtUtc` é nulo (REQ-006)
  - código já em uso no repositório: `Validation` com mensagem de código em uso, e o link existente não é alterado (REQ-003, cenário 3)
  - colisão do código gerado: tenta novamente até 5 vezes e depois falha (R1)
- [ ] T029 [US1] Criar `tests/Encurtador.IntegrationTests/LinkCreationEndpointTests.cs`. Depende de T022, T039:
  - caminho feliz sem código: `201`, `Location`, `code` com 7 caracteres, `shortUrl`, `managementKey` com 43 caracteres, `expiresAt` nulo (V)
  - caminho feliz com código personalizado: `201` com o mesmo código
  - URL inválida, código fora do formato, código reservado, código em uso e data no passado: cada um retorna `400` com `Content-Type: application/problem+json` (VI)
- [ ] T030 [US1] Criar `tests/Encurtador.IntegrationTests/ConcurrentCreationTests.cs`. Vinte requisições simultâneas com o mesmo código personalizado produzem exatamente uma `201`, e as demais `400` em código em uso. Prova o caso de borda e a garantia do índice (D1). Depende de T022, T039
- [ ] T031 [US1] Criar `tests/Encurtador.IntegrationTests/RateLimitTests.cs`. Com `Links:CreationLimitPerHour` = 3 na factory, a quarta criação do mesmo IP retorna `429` com ProblemDetails em português, e nenhum link novo é criado (REQ-017). Depende de T022, T040

### Implementação da História 1

- [ ] T032 [P] [US1] Criar `src/Encurtador.Application/Links/UrlValidator.cs`. Valida esquema `http` ou `https`, URI absoluta e no máximo 2048 caracteres. Retorna `LinkResult` com mensagem em português (REQ-005)
- [ ] T033 [P] [US1] Criar `src/Encurtador.Application/Links/ReservedCodes.cs` com o conjunto fixo `api, health, healthz, ready, live, swagger, openapi, admin, links, metrics`, comparado com `StringComparer.OrdinalIgnoreCase` (REQ-018, D8)
- [ ] T034 [P] [US1] Criar `src/Encurtador.Application/Links/IShortCodeGenerator.cs` e `src/Encurtador.Application/Links/ShortCodeGenerator.cs`. Gera 7 caracteres com `RandomNumberGenerator.GetInt32`, do alfabeto `23456789abcdefghjkmnpqrstuvwxyz` (D4, R1)
- [ ] T035 [P] [US1] Criar `src/Encurtador.Application/Links/ManagementKey.cs`:
  - `Generate()`: 32 bytes aleatórios em base64url, 43 caracteres sem padding
  - `Hash(string key)`: SHA-256 em hexadecimal, 64 caracteres
  - `Verify(string presentedKey, string storedHash)`: `CryptographicOperations.FixedTimeEquals` sobre os bytes
  - `IsWellFormed(string key)`: 43 caracteres base64url

  Usada também pela história 3 (D3, R4)
- [ ] T036 [P] [US1] Criar `src/Encurtador.Application/Links/CreateLinkCommand.cs` e `src/Encurtador.Application/Links/CreatedLink.cs` como records. `CreatedLink` carrega código, destino, criação, expiração e chave em texto, esta última só para a resposta
- [ ] T037 [US1] Implementar `CreateAsync(CreateLinkCommand, CancellationToken)` em `src/Encurtador.Application/Links/LinkService.cs`. Todas as regras ficam aqui (Princípio II). Ordem:
  1. validar URL (T032)
  2. validar expiração contra `TimeProvider.GetUtcNow()` (REQ-007)
  3. código personalizado: formato (REQ-002) e reservado (T033)
  4. consulta prévia por `CodeKey`, só para mensagem clara
  5. gerar chave e `Link`, com `CodeKey` em minúsculas
  6. `TryInsertAsync`. `false` vira `Validation` "código já em uso" (REQ-003, D1)
  7. código gerado: repetir o passo 5 e 6 até 5 vezes em colisão (R1)

  Depende de T032 a T036
- [ ] T038 [P] [US1] Criar `src/Encurtador.Api/Models/CreateLinkRequest.cs` (campos `targetUrl`, `customCode`, `expiresAt`) e `src/Encurtador.Api/Models/LinkResponses.cs` com `CreatedLinkResponse` (`code`, `shortUrl`, `targetUrl`, `createdAt`, `expiresAt`, `managementKey`). Nomes em camelCase, conforme `contracts/openapi.yaml`
- [ ] T039 [US1] Criar `src/Encurtador.Api/Endpoints/LinkEndpoints.cs` com `MapLinkEndpoints(this IEndpointRouteBuilder app)` e a rota `POST /api/links` com `RequireRateLimiting("creation")`. O handler só lê a requisição, chama `LinkService.CreateAsync` e mapeia o resultado: `201` com `Location` e corpo, ou erro via `ProblemMapper` (T021). Sem regra de negócio no lambda (Princípio II) e sem `AppDbContext` (Princípio I). Depende de T037, T038
- [ ] T040 [US1] Atualizar `src/Encurtador.Api/Program.cs`: registrar `LinkService`, `ShortCodeGenerator` e `UrlValidator` como serviços; configurar `AddRateLimiter` com `FixedWindowRateLimiter` particionado por `RemoteIpAddress`, janela de 1 hora e limite `Links:CreationLimitPerHour`, aplicado à política `creation`. `OnRejected` responde 429 com ProblemDetails em português; chamar `app.MapLinkEndpoints()`. Depende de T037, T039

**Checkpoint**: a História 1 funciona de forma independente. Criação com código gerado e personalizado, validação e limite de criação passam nos testes.

---

## Fase 4: História de Usuário 2, acessar o link e ser redirecionado com contagem (Prioridade: P2)

**Objetivo**: o visitante do link curto é redirecionado, e cada acesso a um link ativo soma exatamente 1 ao contador. Links expirados e inexistentes não redirecionam e não contam.

**Teste independente**: criar um link, acessar `GET /{code}` e verificar `302` com destino correto e contador somado em 1. Link expirado retorna `410`, e código inexistente retorna `404`, ambos sem alterar contador.

### Testes da História 2 (obrigatórios: Princípios IV e V) ⚠️

- [ ] T041 [P] [US2] Criar `tests/Encurtador.UnitTests/LinkServiceRedirectTests.cs`, com `RegisterAccessAsync` no `LinkService` usando `InMemoryLinkRepository` (T024) e `FixedTimeProvider`:
  - link ativo: `Redirected` com destino (REQ-008)
  - link expirado: `Expired` e nenhum incremento (REQ-010)
  - código inexistente: `NotFound` e nenhum incremento (REQ-011)
  - instante exato de expiração: `Expired` (caso de borda da spec)
- [ ] T042 [P] [US2] Criar `tests/Encurtador.UnitTests/LinkTests.cs` para `Link.IsExpiredAt`: sem expiração nunca expira; antes da expiração está ativo; exatamente no instante de expiração está expirado; depois está expirado (regra de expiração, Princípio IV)
- [ ] T043 [US2] Criar `tests/Encurtador.IntegrationTests/RedirectEndpointTests.cs`. Depende de T022, T046:
  - caminho feliz: `GET /{code}` retorna `302`, `Location` com o destino e `Cache-Control: no-store` (V; REQ-008, REQ-016)
  - cada acesso soma exatamente 1 ao `ClickCount`, lido via `ILinkRepository.FindByCodeKeyAsync` (REQ-009); três acessos seguidos somam 3
  - código em maiúsculas redireciona para o mesmo link (REQ-004)
  - link expirado retorna `410` em `application/problem+json`, sem redirecionar e sem contar (REQ-010)
  - código inexistente retorna `404` em `application/problem+json`, sem contar (REQ-011)
  - expiração com offset diferente de UTC (`+03:00` e `-03:00`) é respeitada corretamente (pendência R5)
- [ ] T044 [US2] Criar `tests/Encurtador.IntegrationTests/ConcurrentAccessTests.cs`: cem acessos simultâneos a um link ativo, com `Task.WhenAll` sobre `HttpClient`, todos com `302`, e `ClickCount` final igual a 100 (SC-004, REQ-015). Depende de T022, T046

### Implementação da História 2

- [ ] T045 [US2] Implementar `RegisterAccessAsync(string code, CancellationToken)` em `src/Encurtador.Application/Links/LinkService.cs`. Normaliza para `CodeKey` (minúsculas), chama `ILinkRepository.RegisterAccessAsync` com `TimeProvider.GetUtcNow()` e retorna `AccessOutcome`. Código fora do formato resulta em `NotFound`, sem consulta ao banco. Depende de T037
- [ ] T046 [US2] Adicionar a rota `GET /{code}` em `src/Encurtador.Api/Endpoints/LinkEndpoints.cs`. `Redirected` retorna `Results.Redirect(url)` (302) com header `Cache-Control: no-store`. `Expired` e `NotFound` passam pelo `ProblemMapper` (410 e 404). Depende de T039, T045

**Checkpoint**: a História 2 funciona de forma independente. Redirecionamento, contagem exata, expiração e concorrência passam nos testes.

---

## Fase 5: História de Usuário 3, consultar o número de acessos (Prioridade: P3)

**Objetivo**: o criador consulta o total de acessos do próprio link apresentando a chave de gerenciamento. Links expirados continuam consultáveis, e o total sobrevive ao reinício.

**Teste independente**: criar um link, gerar acessos e consultar `GET /api/links/{code}` com a chave correta. O total deve ser igual ao número de acessos válidos.

### Testes da História 3 (obrigatórios: Princípios IV, V e VI) ⚠️

- [ ] T047 [P] [US3] Criar `tests/Encurtador.UnitTests/LinkServiceStatsTests.cs`, com `GetStatsAsync`:
  - chave correta: retorna código, destino, datas e total
  - chave incorreta: `Unauthorized` (REQ-013)
  - código inexistente: `NotFound`
  - link expirado: continua consultável, com o mesmo total de antes (US3, cenário 2)
  - total após novos acessos aumenta exatamente pelo número deles e nunca diminui (US3, cenário 3)
- [ ] T048 [US3] Criar `tests/Encurtador.IntegrationTests/LinkStatsEndpointTests.cs`. Escrever antes de T053; deve falhar até lá. Depende de T022:
  - caminho feliz com chave correta: `200` com `code`, `targetUrl`, `createdAt`, `expiresAt` e `clickCount` (V; US3, cenário 1)
  - sem cabeçalho `X-Management-Key`, e com chave em formato inválido: `400` em `application/problem+json` (VI)
  - chave em formato válido que não confere: `401` em `application/problem+json` (REQ-013)
  - código inexistente: `404`
  - após novos acessos, o total aumenta exatamente pelo número deles (US3, cenário 3)
- [ ] T049 [US3] Criar `tests/Encurtador.IntegrationTests/PersistenceTests.cs`. Cria um link e faz cinco acessos. Descarta a factory, sobe uma nova apontando para o mesmo arquivo SQLite e consulta: código, destino, expiração e total (5) são os mesmos. O link continua redirecionando após o reinício (REQ-014, SC-003, US3 cenário 4). Depende de T022, T053

### Implementação da História 3

- [ ] T050 [P] [US3] Criar `src/Encurtador.Application/Links/LinkStats.cs` como record com `Code`, `TargetUrl`, `CreatedAtUtc`, `ExpiresAtUtc` (nulo quando não expira) e `ClickCount`
- [ ] T051 [US3] Implementar `GetStatsAsync(string code, string managementKey, CancellationToken)` em `src/Encurtador.Application/Links/LinkService.cs`. Busca por `CodeKey` (NotFound se ausente), depois `ManagementKey.Verify` contra `ManagementKeyHash` (Unauthorized se não confere). Retorna `LinkStats`. Não altera o contador. Depende de T035, T045, T050
- [ ] T052 [US3] Adicionar `LinkStatsResponse` em `src/Encurtador.Api/Models/LinkResponses.cs`, com campos `code`, `targetUrl`, `createdAt`, `expiresAt` (nulo quando não expira) e `clickCount`, conforme `contracts/openapi.yaml`. Depende de T038
- [ ] T053 [US3] Adicionar a rota `GET /api/links/{code}` em `src/Encurtador.Api/Endpoints/LinkEndpoints.cs`:
  1. ler `X-Management-Key`. Ausente → `400`
  2. `ManagementKey.IsWellFormed` falso → `400`
  3. chamar `GetStatsAsync`
  4. mapear `200`, `401` ou `404` via `ProblemMapper`

  Depende de T046, T051, T052

**Checkpoint**: as três histórias funcionam de forma independente. Todos os requisitos funcionais estão cobertos.

---

## Fase N: Polimento e preocupações transversais

**Propósito**: verificações da Constituição e validação final.

- [ ] T054 [P] Verificar Princípio I: `grep` por `AppDbContext`, `DbSet` e `Microsoft.EntityFrameworkCore` em `src/Encurtador.Api/`. Resultado esperado: nenhuma ocorrência
- [ ] T055 [P] Verificar Princípio III: `grep` por `FromSqlRaw`, `ExecuteSqlRaw`, `FromSql` e `SELECT`/`UPDATE` em strings fora de `src/Encurtador.Infrastructure/`. Resultado esperado: nenhuma ocorrência. Confirmar que `EfLinkRepository.cs` não lê e regrava `ClickCount` (D2)
- [ ] T056 [P] Verificar Princípio VIII: todos os `title` e `detail` de ProblemDetails, em `src/Encurtador.Application/Links/LinkResult.cs`, `src/Encurtador.Application/Links/UrlValidator.cs` e `src/Encurtador.Api/Http/ProblemMapper.cs`, estão em português. Nomes de classes e métodos em inglês
- [ ] T057 [P] Verificar REQ-013 e segurança: a chave de gerenciamento em texto nunca é logada nem aparece em respostas além da criação, e `ManagementKeyHash` nunca é retornado. Revisar `src/Encurtador.Api/Program.cs` (logging) e `src/Encurtador.Api/Endpoints/LinkEndpoints.cs`. Confirmar que ProblemDetails não expõe detalhe interno de exceção
- [ ] T058 Sincronizar `contracts/openapi.yaml` com a implementação. Se algum status, campo ou header divergir, corrigir o contrato e registrar a divergência em `plan.md`, seção "Decisões de Resposta HTTP"
- [ ] T059 Executar `quickstart.md` do passo 1 ao 12 e marcar os critérios de aceite do arquivo
- [ ] T060 Executar `dotnet test tests/Encurtador.UnitTests` e `dotnet test tests/Encurtador.IntegrationTests`, e confirmar que todos os testes passam. Registrar a saída, sem omitir falhas
- [ ] T061 Verificar Princípio IX no histórico de commits (`git log`) da branch da funcionalidade: formato `tipo(REQ-NNN): descrição`, com tipo entre `feat`, `fix`, `test`, `refactor`, `docs` e `chore`

---

## Dependências e ordem de execução

### Dependências entre fases

- **Setup (Fase 1)**: sem dependências. Pode começar já.
- **Fundação (Fase 2)**: depende do Setup. **Bloqueia todas as histórias.**
- **Histórias (Fases 3 a 5)**: dependem da Fundação. Podem seguir em paralelo, ou em ordem de prioridade P1 → P2 → P3.
- **Polimento (Fase N)**: depende das histórias desejadas.

### Dependências entre histórias

- **US1 (P1)**: depende só da Fundação.
- **US2 (P2)**: depende da Fundação e de `LinkService` (T037) e da rota `POST` (T039, para os testes de integração que criam links). Não depende de US3.
- **US3 (P3)**: depende da Fundação, de `LinkService` (T045) e da rota da história 2 (T046), por causa do mesmo arquivo `LinkEndpoints.cs`. Usa `ManagementKey` (T035) da história 1.

Como `LinkService.cs` e `LinkEndpoints.cs` são compartilhados, as tarefas que os editam em cada história não são `[P]` entre si. As tarefas `[P]` de uma história podem rodar em paralelo com as de outra história desde que não toquem os mesmos arquivos.

### Dentro de cada história

- Testes escritos e falhando antes da implementação
- Entidades e portas antes dos serviços
- Serviços antes dos endpoints
- Endpoint antes da verificação de integração

---

## Exemplo de paralelismo: História 1

```bash
# Testes (arquivos diferentes, em paralelo):
Task: "T025 UrlValidatorTests.cs"
Task: "T026 ShortCodeGeneratorTests.cs"
Task: "T027 ManagementKeyTests.cs"

# Implementação (arquivos diferentes, em paralelo):
Task: "T032 UrlValidator.cs"
Task: "T033 ReservedCodes.cs"
Task: "T034 ShortCodeGenerator.cs"
Task: "T035 ManagementKey.cs"
Task: "T036 CreateLinkCommand.cs e CreatedLink.cs"
```

---

## Estratégia de implementação

### MVP primeiro (somente História 1)

1. Concluir Fase 1 (Setup)
2. Concluir Fase 2 (Fundação), que bloqueia tudo
3. Concluir Fase 3 (História 1)
4. **PARAR e VALIDAR**: criar links pela API e rodar os testes da história
5. Demonstrar, se estiver pronto

### Entrega incremental

1. Setup + Fundação → base pronta
2. História 1 → testar de forma independente → MVP
3. História 2 → testar de forma independente → redirecionamento com contagem
4. História 3 → testar de forma independente → consulta do total
5. Polimento → verificações da Constituição

---

## Resumo

| Fase | Tarefas | IDs |
|------|---------|-----|
| 1. Setup | 7 | T001–T007 |
| 2. Fundação | 16 | T008–T023 |
| 3. História 1 (P1, criar) | 17 | T024–T040 |
| 4. História 2 (P2, redirecionar) | 6 | T041–T046 |
| 5. História 3 (P3, consultar) | 7 | T047–T053 |
| N. Polimento | 8 | T054–T061 |
| **Total** | **61** | |

Tarefas de teste: 16 (T023; T024–T031 na história 1; T041–T044 na história 2; T047–T049 na história 3). Tarefas marcadas `[P]`: 29.

**Escopo do MVP sugerido**: Fases 1, 2 e 3 (T001–T040). Entregam criação com código gerado e personalizado, validação em ProblemDetails e limite de criação.

---

## Notas

- `[P]` = arquivos diferentes, sem dependência
- `[Story]` liga a tarefa à história, para rastreabilidade
- Cada história é concluível e testável de forma independente
- Verificar que os testes falham antes de implementar
- Commits no formato `tipo(REQ-NNN): descrição` (Princípio IX), um por tarefa ou grupo lógico
- Parar em cada checkpoint para validar a história isoladamente
- Evitar: tarefas vagas, conflito no mesmo arquivo e dependência entre histórias que quebre a independência
