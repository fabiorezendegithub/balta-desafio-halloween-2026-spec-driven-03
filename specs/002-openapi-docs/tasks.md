# Tarefas: Documentação OpenAPI da API

**Entrada**: documentos de design em `specs/002-openapi-docs/`

**Pré-requisitos**: [plan.md](./plan.md) (obrigatório), [spec.md](./spec.md) (obrigatório para histórias), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/README.md](./contracts/README.md), [quickstart.md](./quickstart.md)

**Testes**: obrigatórios. O plano define `ApiDocumentationTests` em `tests/Encurtador.IntegrationTests/` como teste de integração da documentação. A Constituição exige teste de caminho feliz para cada endpoint (Princípio V). Os testes de cada história são escritos antes da implementação e devem falhar antes dela.

**Organização**: tarefas agrupadas por história de usuário, para implementação e teste independentes.

## Formato: `[ID] [P?] [Story] Descrição`

- **[P]**: pode rodar em paralelo (arquivos diferentes, sem dependência de tarefa incompleta)
- **[Story]**: história a que a tarefa pertence (US1, US2, US3)
- Caminhos de arquivo exatos na descrição

## Convenções de caminho

Projetos conforme [plan.md](./plan.md), seção "Estrutura do Projeto":
- `src/Encurtador.Api/` (endpoints, modelos, `Documentation/`, `Program.cs`, `appsettings*.json`)
- `src/Encurtador.Application/` e `src/Encurtador.Infrastructure/`: sem alterações nesta funcionalidade
- `tests/Encurtador.IntegrationTests/` (novo arquivo `ApiDocumentationTests.cs`)

Chave de configuração (de [data-model.md](./data-model.md)): `Documentation:Enabled`, tipo `bool`, padrão `false`, `true` apenas em `appsettings.Development.json`.

---

## Fase 1: Setup (infraestrutura compartilhada)

**Propósito**: adicionar as duas dependências aprovadas no plano e a chave de configuração.

- [ ] T001 Adicionar `PackageReference` de `Microsoft.AspNetCore.OpenApi` 10.0.13 e `Scalar.AspNetCore` 2.18.2 em `src/Encurtador.Api/Encurtador.Api.csproj`. Justificativa já registrada em `plan.md`, seção "Dependências Novas"
- [ ] T002 Restaurar e compilar a solução com `dotnet restore Encurtador.slnx` e `dotnet build Encurtador.slnx --no-restore`, sem avisos (`TreatWarningsAsErrors` ativo). Confirmar em `specs/002-openapi-docs/research.md` (R8) que não há conflito de versão com os pacotes `Microsoft.AspNetCore` 10.0.13 do projeto de testes. Depende de T001
- [ ] T003 [P] Adicionar `"Documentation": { "Enabled": false }` em `src/Encurtador.Api/appsettings.json` e `"Documentation": { "Enabled": true }` em `src/Encurtador.Api/appsettings.Development.json`. Tipo `bool`, sem outros valores

**Checkpoint**: a solução compila com as duas dependências e a chave `Documentation:Enabled` existe nos dois arquivos.

---

## Fase 2: Fundação (pré-requisitos bloqueantes)

**Propósito**: criar o ponto de composição da documentação e validar a interface antes de qualquer outra anotação, conforme `plan.md`, seção "Validação", item 1.

**⚠️ CRÍTICO**: nenhuma tarefa de história começa antes desta fase, e o spike T006 precisa passar antes de qualquer outra tarefa de implementação.

- [ ] T004 Criar `src/Encurtador.Api/Documentation/ApiDocumentationExtensions.cs` com a classe estática `ApiDocumentationExtensions`, contendo:
  - `AddApiDocumentation(this IServiceCollection services, IConfiguration configuration)`: chama `AddOpenApi()` do `Microsoft.AspNetCore.OpenApi`
  - `MapApiDocumentation(this WebApplication app)`: lê `Documentation:Enabled` (padrão `false`). Quando `false`, não mapeia nada. Quando `true`, mapeia `MapOpenApi("/api/openapi/v1.json")` e `MapScalar("/api/docs")` (Scalar, `Scalar.AspNetCore`)
  - Nenhuma regra de negócio e nenhum acesso a `DbContext` (Princípios I e II da constituição)
- [ ] T005 Alterar `src/Encurtador.Api/Program.cs`: chamar `builder.Services.AddApiDocumentation(builder.Configuration)` junto aos demais registros de serviços, e `app.MapApiDocumentation()` antes de `app.MapLinkEndpoints()`. Depende de T004
- [ ] T006 **Spike de R5 (gate)**: subir a aplicação com `ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Encurtador.Api`, abrir `http://localhost:5016/api/docs` e executar os passos 1 e 2 de `quickstart.md` (seção 2). Critério: a interface mostra `302` com o cabeçalho `Location` e não navega para o destino (REQ-028). Se falhar, PARAR e voltar ao usuário conforme o plano B de `research.md` R5. Não ocultar a operação de redirecionamento. Depende de T005

**Checkpoint**: a documentação liga em Development, a interface não segue redirecionamentos e o build passa sem avisos.

---

## Fase 3: História de Usuário 1 - Descobrir os endpoints e seus contratos (Prioridade: P1) 🎯 MVP

**Objetivo**: o documento OpenAPI descreve os três endpoints com rota, método, parâmetros, corpo, respostas de sucesso e todos os códigos de erro, com textos em português.

**Teste independente**: com `Documentation:Enabled=true`, `/api/openapi/v1.json` responde 200 e contém `CreateLink`, `GetLinkStats` e `RedirectLink`, cada uma com seus códigos de resposta.

### Testes da História 1 (escritos primeiro, devem falhar antes da implementação) ⚠️

> **NOTA: escrever estes testes antes de T010 a T014, e confirmar que eles falham.**

- [ ] T007 [US1] Criar `tests/Encurtador.IntegrationTests/ApiDocumentationTests.cs` com teste de caminho feliz: em Development, `GET /api/openapi/v1.json` retorna 200 com `application/json`, e `GET /api/docs` retorna 200 (Princípio V; `plan.md`, "Validação", item 2). Usar `WebApplicationFactory<Program>` como em `LinkApiFactory`
- [ ] T008 [US1] Em `tests/Encurtador.IntegrationTests/ApiDocumentationTests.cs`, teste que lê o documento e verifica as três operações pelo `operationId`: `CreateLink` (`POST /api/links`), `GetLinkStats` (`GET /api/links/{code}`) e `RedirectLink` (`GET /{code}`) (REQ-019, SC-003)
- [ ] T009 [US1] Em `tests/Encurtador.IntegrationTests/ApiDocumentationTests.cs`, teste de coerência com o contrato 001. Os dados esperados são a tabela de operações de `specs/002-openapi-docs/contracts/README.md` (operationId, método, rota, parâmetros e códigos de resposta), escrita diretamente no teste. Não ler `specs/001-url-shortener/contracts/openapi.yaml` com parser YAML, porque isso exigiria dependência nova não aprovada no plano (Princípio VII). Verificar: cada códigos de resposta esperado aparece na operação correspondente do documento gerado
- [ ] T010 [US1] Em `tests/Encurtador.IntegrationTests/ApiDocumentationTests.cs`, teste que verifica que `X-Management-Key` aparece como parâmetro de cabeçalho em `GetLinkStats` e que o documento não contém `securitySchemes` (`plan.md`, "Dependências"; `research.md` R6)

### Implementação da História 1

- [ ] T011 [P] [US1] Em `src/Encurtador.Api/Models/CreateLinkRequest.cs`, adicionar exemplo fictício em português para a URL de destino, o código personalizado opcional e a data de expiração opcional. Exemplos sem chave real (REQ-025, REQ-026)
- [ ] T012 [P] [US1] Em `src/Encurtador.Api/Models/LinkResponses.cs`, adicionar exemplos fictícios para `CreatedLinkResponse` e `LinkStatsResponse`, sem chave de gerenciamento real (REQ-025, REQ-026)
- [ ] T013 [US1] Em `src/Encurtador.Api/Endpoints/LinkEndpoints.cs`, anotar a rota `CreateLink` com `WithSummary`, `WithDescription` em português, `Produces<CreatedLinkResponse>(201)` e `ProducesProblem` para 400 e 429 (REQ-020; `research.md` R6)
- [ ] T014 [US1] Em `src/Encurtador.Api/Endpoints/LinkEndpoints.cs`, anotar `GetLinkStats` com resumo e descrição em português, `Produces<LinkStatsResponse>(200)` e `ProducesProblem` para 400, 401 e 404. Descrever o cabeçalho `X-Management-Key` (REQ-020)
- [ ] T015 [US1] Em `src/Encurtador.Api/Endpoints/LinkEndpoints.cs`, anotar `RedirectLink` com resumo e descrição em português, `Produces(302)` com cabeçalho `Location`, e `ProducesProblem` para 404 e 410. Verificar que o código fora do formato resulta em 404 sem validação de formato (`contracts/README.md`, nota final)
- [ ] T016 [US1] Confirmar em `LinkEndpoints.cs` que o `.WithName(...)` de cada rota é `CreateLink`, `GetLinkStats` e `RedirectLink`, e ajustar se necessário, para que os `operationId` do documento coincidam com `contracts/README.md`. Depende de T013, T014 e T015

**Checkpoint**: T007 a T010 passam. O documento tem as três operações, com seus códigos de resposta, e não há alteração nos endpoints além das anotações.

---

## Fase 4: História de Usuário 2 - Executar chamadas diretamente pela interface (Prioridade: P2)

**Objetivo**: a interface permite executar as três chamadas, mostra a resposta de redirecionamento sem segui-la e não altera o estado dos links ao ser aberta.

**Teste independente**: criar um link, abrir a documentação, consultar as estatísticas com a chave e confirmar `ClickCount = 0` (quickstart, seção 7).

### Testes da História 2 (escritos primeiro) ⚠️

- [ ] T017 [US2] Em `tests/Encurtador.IntegrationTests/ApiDocumentationTests.cs`, teste de efeito colateral (REQ-029, SC-006): criar um link por `POST /api/links`, chamar `GET /api/docs` e `GET /api/openapi/v1.json`, depois `GET /api/links/{code}` com a chave e verificar `ClickCount = 0`. Seguir o padrão de `LinkStatsEndpointTests.cs` para criação e leitura de estatísticas

### Validação da História 2

- [ ] T018 [US2] Executar manualmente, no navegador, os cenários 1 a 6 da História 2 de `spec.md` pela interface em `http://localhost:5016/api/docs`: criação, estatísticas, chave ausente (401), URL inválida (400), redirecionamento sem seguir (302) e cabeçalhos em destaque com lista recolhível. Registrar o resultado em `specs/002-openapi-docs/quickstart.md`, seção 2. Depende de T016

**Checkpoint**: T017 passa e o fluxo manual da T018 foi executado. A documentação não cria, altera nem consulta links ao ser aberta.

---

## Fase 5: História de Usuário 3 - Manter a documentação fora da produção (Prioridade: P3)

**Objetivo**: fora do ambiente local, a documentação responde 404 idêntico ao de rota inexistente, e só fica disponível com `Documentation:Enabled=true` explícito.

**Teste independente**: subir a aplicação em Production sem configuração e comparar `/api/docs`, `/api/openapi/v1.json` e `/api/rota-inexistente`.

> A lógica de ativação já está em T004 (`MapApiDocumentation`). Esta fase não cria código de produção novo. Ela prova o comportamento com testes.

### Testes da História 3

- [ ] T019 [US3] Em `tests/Encurtador.IntegrationTests/ApiDocumentationTests.cs`, teste com `WebApplicationFactory<Program>` configurado com `ASPNETCORE_ENVIRONMENT=Production` e sem `Documentation:Enabled`. Verificar que `GET /api/docs`, `GET /api/openapi/v1.json` e `GET /api/rota-inexistente` retornam o mesmo status e o mesmo corpo, byte a byte (REQ-023, SC-004; `research.md` R3)
- [ ] T020 [US3] Em `tests/Encurtador.IntegrationTests/ApiDocumentationTests.cs`, teste com Production e `Documentation:Enabled=true` configurado no `WebHostBuilder` (`UseSetting` ou `ConfigureAppConfiguration`). Verificar que `/api/docs` e `/api/openapi/v1.json` retornam 200 (REQ-023, história 3, cenário 3)

**Checkpoint**: T019 e T020 passam, com o código de T004 sem alteração.

---

## Fase N: Polimento e preocupações transversais

**Propósito**: validação final e verificação de requisitos transversais.

- [ ] T021 Executar `dotnet test Encurtador.slnx --no-build` e confirmar que todos os testes existentes passam sem alteração nos próprios testes, provando que os três endpoints mantêm status, cabeçalhos e corpos (SC-005; `quickstart.md`, seção 6)
- [ ] T022 [P] Confirmar em `src/Encurtador.Application/Links/ReservedCodes.cs` que o prefixo `api` está reservado (REQ-027). Se não estiver, adicionar a reserva e o teste correspondente em `tests/Encurtador.UnitTests/UrlValidatorTests.cs`
- [ ] T023 [P] Executar `curl -s http://localhost:5016/api/openapi/v1.json | grep -E 'ManagementKey|[A-Za-z0-9_-]{43}'` (`quickstart.md`, seção 8) e confirmar que o documento não contém chave real (REQ-026)
- [ ] T024 Executar o roteiro completo de `specs/002-openapi-docs/quickstart.md` (seções 1 a 8) e registrar o resultado de cada seção no próprio arquivo

---

## Dependências e ordem de execução

### Dependências entre fases

- **Setup (Fase 1)**: sem dependências. Pode começar já
- **Fundação (Fase 2)**: depende de Setup. BLOQUEIA todas as histórias. O spike T006 é o gate
- **História 1 (P1)**: depende da Fase 2
- **História 2 (P2)**: depende de T016 (anotações da História 1), porque a interface só tem o documento completo depois dela
- **História 3 (P3)**: depende da Fase 2 (T004 já contém a ativação). Pode rodar em paralelo com a História 2
- **Polimento (Fase N)**: depende das histórias desejadas

### Dependências dentro das histórias

- Testes antes da implementação, e confirmados como falhando (US1)
- Modelos com exemplos (T011, T012) antes das anotações de rota (T013 a T015)
- Anotações de rota antes da validação manual (T018)

### Oportunidades de paralelismo

- T003 em paralelo com T001 e T002, por ser arquivo diferente
- T011 e T012 em paralelo (arquivos de modelo diferentes)
- T022 e T023 em paralelo com T021
- Histórias 2 e 3 podem ser feitas em paralelo, depois da História 1, por pessoas diferentes. Ambas tocam `ApiDocumentationTests.cs`, então o merge precisa de cuidado

---

## Exemplo paralelo: História 1

```bash
# Exemplos de modelos, em paralelo (arquivos diferentes):
Tarefa: "T011 Exemplo fictício em CreateLinkRequest.cs em src/Encurtador.Api/Models/CreateLinkRequest.cs"
Tarefa: "T012 Exemplos fictícios em LinkResponses.cs em src/Encurtador.Api/Models/LinkResponses.cs"
```

---

## Estratégia de implementação

### MVP primeiro (somente História 1)

1. Concluir Fase 1: Setup
2. Concluir Fase 2: Fundação, incluindo o spike T006
3. Concluir Fase 3: História 1 (testes primeiro, depois anotações)
4. **PARAR e VALIDAR**: abrir `/api/docs` e `/api/openapi/v1.json` em Development
5. Entregar o MVP se estiver pronto

### Entrega incremental

1. Setup + Fundação: base pronta, com o spike validado
2. História 1: documento completo (MVP)
3. História 2: interface validada com as chamadas reais
4. História 3: prova de 404 e de ativação explícita em produção
5. Polimento: validação final pelo `quickstart.md`

---

## Notas

- Tarefas [P] são arquivos diferentes, sem dependência
- Rótulo [Story] liga cada tarefa à história da `spec.md` para rastreio
- Cada história pode ser verificada sozinha pelo seu "Teste independente"
- Commits no formato `tipo(REQ-NNN): descrição`, conforme o Princípio IX da constituição
- Pontos a confirmar com o usuário (de `plan.md`, "Interpretações"): (a) se o Princípio V vale para as rotas da documentação, o que este plano assume ao dar a elas teste de caminho feliz (T007); (b) o teste de coerência com o contrato 001 usa a tabela de `contracts/README.md`, não o YAML, para não criar dependência nova (T009)
