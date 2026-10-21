# Plano de Implementação: Documentação OpenAPI da API

**Branch**: `002-openapi-docs` | **Data**: 2026-10-20 | **Spec**: [spec.md](./spec.md)

**Entrada**: Especificação da funcionalidade em `specs/002-openapi-docs/spec.md`

**Nota**: Este plano é preenchido pelo comando `/speckit-plan`. Artefatos: [research.md](./research.md), [data-model.md](./data-model.md), [contracts/README.md](./contracts/README.md) e [quickstart.md](./quickstart.md). Nenhum código de produção foi alterado nesta etapa.

## Resumo

Publicar, na própria aplicação, um documento OpenAPI que descreve os três endpoints existentes e uma interface web interativa que o consome. A documentação fica ligada por padrão no ambiente de desenvolvimento e desligada nos demais, salvo configuração explícita. Quando desligada, os caminhos respondem como uma rota inexistente. Não há mudança de comportamento nos endpoints nem nas regras de negócio.

Abordagem técnica:

- **Documento**: gerado pelo recurso nativo `Microsoft.AspNetCore.OpenApi` a partir das Minimal APIs (R1). Os textos e exemplos são anotados junto a cada rota.
- **Interface**: `Scalar.AspNetCore`, sobre o documento nativo (R2).
- **Rotas**: interface em `/api/docs`, documento em `/api/openapi/v1.json`. O prefixo `api` já é reservado, e caminhos de dois segmentos sob `/api/` não casam com `/{code}`, então o 404 quando desligada é o padrão do framework (R3).
- **Ativação**: chave `Documentation:Enabled` opcional. Quando ausente, o padrão é `true` em Development e `false` nos demais ambientes. Nenhum arquivo versionado define a chave (R4).
- **Ponto de composição**: métodos de extensão em uma classe nova `Api/Documentation/ApiDocumentationExtensions.cs`, chamados em `Program.cs`. Nenhuma lógica de negócio.
- **Risco principal**: a interface seguir o redirecionamento do `GET /{code}` (REQ-028). Verificado no primeiro passo da implementação (R5).

## Contexto Técnico

**Linguagem/Versão**: C# 14 sobre .NET 10 (SDK 10.0.401, runtime 10.0.12)

**Dependências principais**:
- ASP.NET Core Minimal APIs (framework, já em uso)
- `Microsoft.AspNetCore.OpenApi` 10.0.13 (pacote novo, ver "Dependências Novas")
- `Scalar.AspNetCore` 2.18.2 (pacote novo, ver "Dependências Novas")
- `YamlDotNet` 18.1.0 (pacote novo, só nos testes de integração, ver "Dependências Novas")

**Armazenamento**: não se aplica. A funcionalidade não cria nem altera dados.

**Testes**: xUnit. Testes de integração com `WebApplicationFactory<Program>`, já existentes em `tests/Encurtador.IntegrationTests`. Não há novos testes unitários de regra, porque nenhuma regra de negócio muda (ver "Constitution Check").

**Plataforma alvo**: Linux, instância única, como na 001.

**Tipo de projeto**: serviço web (API HTTP).

**Metas de desempenho**: não se aplica. A documentação não está no caminho de nenhum endpoint de negócio.

**Restrições**:
- Desligada por padrão fora do ambiente de desenvolvimento (REQ-023), com 404 idêntico ao de rota inexistente.
- Nenhuma requisição é feita à aplicação por abrir a documentação (REQ-029, SC-006).
- Redirecionamento não é seguido pela interface (REQ-028). Ver R5 em `research.md`.
- Exemplos fictícios, sem chave real (REQ-026).

**Escala/Escopo**: três endpoints existentes. Nenhum endpoint novo.

## Análise do Projeto Atual

Levantamento feito antes de definir a solução (detalhes em `research.md`, seção "Estado atual do projeto"):

- **Versão do .NET**: .NET 10, C# 14, SDK 10.0.401. Os projetos são `net10.0`.
- **Arquitetura**: três projetos de produção (`Api`, `Application`, `Infrastructure`) e dois de teste (`UnitTests`, `IntegrationTests`). Endpoints na camada `Api`, regras em `Application`, persistência em `Infrastructure`.
- **Convenções**: `ImplicitUsings` e `Nullable` ativos, `TreatWarningsAsErrors` em `Directory.Build.props`, namespaces por pasta, endpoints como métodos `static` em uma classe por recurso. `ProblemMapper` centraliza o erro em ProblemDetails.
- **Dependências**: EF Core 10.0.13 com Sqlite, `Microsoft.EntityFrameworkCore.Design` (somente design). Testes com xUnit 2.9.3, `Microsoft.AspNetCore.Mvc.Testing` 10.0.13 e coverlet.
- **Configuração**: `appsettings.json` com provider, connection string e `Links:*`. `appsettings.Development.json` com quase nada. Não há seção de documentação.
- **Registro dos endpoints**: `app.MapLinkEndpoints()` em `Program.cs`. Cada rota recebe `.WithName(...)`, sem resumo, descrição ou tipos de resposta.
- **Autenticação e autorização**: nenhum middleware. A chave de gerenciamento é um cabeçalho lido por `[FromHeader]` e validado em `LinkService`. Não há `UseAuthentication`, `UseAuthorization` nem políticas.
- **Tratamento de erro**: `AddProblemDetails()` e `ProblemMapper` (ProblemDetails em português).

## Constitution Check

*GATE: deve passar antes da Fase 0. Reavaliado após o desenho da Fase 1.*

| Princípio | Conformidade | Como o plano atende |
|-----------|--------------|---------------------|
| I. Endpoints não acessam banco diretamente | Conforme | Nenhum endpoint novo. A documentação não recebe `DbContext`. |
| II. Regra de negócio fora do HTTP | Conforme | Nenhuma regra nova. A configuração de ativação fica em `Program.cs` e em `appsettings`, não em lambda de endpoint. |
| III. Persistência com EF Core e provider trocável | Conforme | Não há acesso a dados nesta funcionalidade. |
| IV. Regra de negócio com teste unitário xUnit | Conforme | Nenhuma regra de negócio é criada ou alterada. A reserva da rota da documentação já é coberta pela regra de palavras reservadas (`api`), existente. |
| V. Endpoint com teste de integração de caminho feliz | Conforme, com interpretação | Os endpoints existentes mantêm seus testes. As rotas da documentação são infraestrutura, mas recebem teste de integração de caminho feliz (documento 200 e interface 200 quando ligada). Ver "Interpretações". |
| VI. Erro de validação é 400 com ProblemDetails | Conforme | Nenhuma validação nova. As anotações `ProducesProblem` documentam os 400 existentes. |
| VII. Nenhuma dependência sem justificativa | Conforme | Três pacotes novos, com justificativa na seção "Dependências Novas". |
| VIII. Nomes em inglês, mensagens em português | Conforme | Classes e métodos em inglês. Resumos, descrições e exemplos da documentação em português (REQ-025). Rótulos fixos da interface em inglês, como permite a spec. |
| IX. Commits rastreáveis ao requisito | Conforme | Commits no formato `tipo(REQ-NNN): descrição`, com o requisito de cada mudança. |

**Restrições Técnicas**: .NET 10 e C#, Minimal APIs, ProblemDetails. Todas atendidas.

**Fluxo de Desenvolvimento**: a tarefa que anota endpoints inclui teste de integração de caminho feliz do documento. Não há tarefa de endpoint novo.

**Resultado**: nenhuma violação. Não há entradas em "Rastreamento de Complexidade".

### Reavaliação após o desenho da Fase 1

Conforme. O desenho não introduziu regra de negócio, endpoint novo nem acesso a dados. Os pacotes novos estão justificados abaixo.

## Interpretações

- **Princípio V para rotas de infraestrutura**: a constituição fala de "endpoint", e as rotas da documentação são gerenciadas pelo próprio framework. Por cautela, elas recebem teste de caminho feliz. **Interpretação aceita pelo usuário em 2026-10-20**: as rotas da documentação contam como endpoint e recebem teste de caminho feliz (T006b, T007).
- **REQ-023 e a referência de "rota inexistente"**: a comparação usa `GET /api/rota-inexistente`, que é o 404 padrão do framework. Essa escolha é o que torna a resposta idêntica (R3).

## Decisões Registradas

### D11. Documento gerado pelo recurso nativo, não por biblioteca de terceiros

**Decisão**: `Microsoft.AspNetCore.OpenApi` 10.0.13.

**Justificativa**: a fonte do contrato é a própria Minimal API, sem geração paralela. Atende REQ-024 e SC-005. Ver [research.md](./research.md) R1.

### D12. Interface com um único pacote

**Decisão**: `Scalar.AspNetCore` 2.18.2. Plano B com Swagger UI em [research.md](./research.md) R5.

**Justificativa**: uma peça só, consumindo o documento nativo. Ver R2.

### D13. Rotas da documentação sob `/api/`

**Decisão**: `/api/docs` e `/api/openapi/v1.json`.

**Justificativa**: quando desligada, o 404 é idêntico ao de uma rota inexistente, e o prefixo `api` já é reservado (REQ-027). Ver R3.

### D14. Ativação por configuração

**Decisão**: `Documentation:Enabled` é opcional. Quando ausente, o padrão é `true` em Development e `false` nos demais ambientes. O valor explícito sobrescreve o padrão nos dois sentidos. Nenhum arquivo versionado define a chave.

**Justificativa**: o desenvolvedor local vê a documentação sem configuração (REQ-022). Em ambientes compartilhados e em produção, a documentação só aparece por decisão explícita, e um ambiente novo nasce fechado (REQ-023). Ver R4.

## Estrutura do Projeto

### Documentação desta funcionalidade

```text
specs/002-openapi-docs/
├── spec.md
├── plan.md              # Este arquivo
├── research.md          # Fase 0
├── data-model.md        # Fase 1 (não se aplica)
├── quickstart.md        # Fase 1, roteiro de validação
├── contracts/
│   └── README.md        # Fase 1, aponta o documento gerado e o contrato 001
├── checklists/
└── tasks.md             # Fase 2, gerado por /speckit-tasks (não criado aqui)
```

### Código-fonte (alterações previstas)

```text
src/Encurtador.Api/
├── Documentation/
│   └── ApiDocumentationExtensions.cs   # NOVO: AddApiDocumentation e MapApiDocumentation
├── Endpoints/
│   └── LinkEndpoints.cs                # ALTERADO: resumo, descrição, tipos de resposta
├── Models/
│   ├── CreateLinkRequest.cs            # ALTERADO: exemplos fictícios, se necessário
│   └── LinkResponses.cs                # ALTERADO: exemplos fictícios, se necessário
├── Program.cs                          # ALTERADO: chamada das duas extensões
└── Encurtador.Api.csproj               # ALTERADO: duas PackageReference

tests/Encurtador.IntegrationTests/
├── Encurtador.IntegrationTests.csproj  # ALTERADO: PackageReference YamlDotNet
└── ApiDocumentationTests.cs            # NOVO: ligada, desligada, produção, contrato 001
```

Nenhuma alteração em `Application` ou `Infrastructure`.

**Structure Decision**: a documentação é um recurso da camada `Api`, porque depende de ASP.NET Core. A pasta `Documentation/` segue o padrão de `RateLimiting/` e `Http/` (adaptadores e utilitários do framework dentro da `Api`). Não se cria projeto novo.

## Dependências Novas

| Pacote | Versão | Motivo | Alternativa considerada | Por que a alternativa não atende |
|--------|--------|--------|-------------------------|----------------------------------|
| `Microsoft.AspNetCore.OpenApi` | 10.0.13 | Gera o documento OpenAPI a partir das Minimal APIs, sem geração paralela. Não faz parte do runtime compartilhado. | `Swashbuckle.AspNetCore` | Gera documento próprio em paralelo ao nativo, com mais pacotes e configuração. Duas fontes de verdade, contra REQ-024. |
| `Scalar.AspNetCore` | 2.18.2 | Interface interativa sobre o documento nativo, em um único pacote (REQ-021). | `Swashbuckle.AspNetCore.SwaggerUI` | Funciona, mas exige configuração extra para consumir o documento nativo. Plano B se R5 reprovar a Scalar. |
| `YamlDotNet` | 18.1.0 | Lê `specs/001-url-shortener/contracts/openapi.yaml` no teste de coerência (REQ-024 e premissa da spec 002). Sem ele, o teste verificaria só uma cópia da tabela e não detectaria divergência com o contrato 001. Usado apenas em `tests/Encurtador.IntegrationTests`. | Tabela copiada no teste, a partir de `contracts/README.md`. | Não detecta mudança no contrato 001, que é a fonte de verdade declarada pela spec. Leitura por texto (regex) é frágil, porque depende da formatação do YAML. |

As versões são as observadas no NuGet em 2026-10-20. Confirmar no restore que as dependências transitivas estão em net10.0 e que nenhuma colide com as versões Microsoft.AspNetCore 10.0.13 já usadas pelo projeto de testes.

## Validação

Estratégia completa em [quickstart.md](./quickstart.md). Resumo:

1. **Spike de R5** antes de qualquer outra tarefa: confirmar que a interface mostra o `302` sem seguir o destino.
2. **Testes de integração** em `ApiDocumentationTests`:
   - ligada por configuração (`Documentation__Enabled=true`) em Development: documento e interface respondem 200;
   - Development sem configuração: `/api/docs` e `/api/openapi/v1.json` retornam 200, sem variável de ambiente (T019b);
   - desligada (ambiente Production, sem configuração): `/api/docs`, `/api/openapi/v1.json` e `/api/rota-inexistente` retornam o mesmo status e o mesmo corpo;
   - ligada explicitamente em Production por configuração: documento e interface respondem 200;
   - o documento contém as três operações, com os códigos de resposta de `contracts/README.md`;
   - o documento contém os parâmetros de cabeçalho e de rota, sem `securitySchemes`;
   - a interface é um HTML que referencia `/api/openapi/v1.json` (T006b); os schemas de `CreatedLinkResponse` e `LinkStatsResponse` coincidem com os records C# (T009b); a chave de gerenciamento não aparece no documento (T023).
3. **Segurança dos endpoints existentes**: a suíte de integração atual deve passar sem alteração, provando que os três endpoints e seus status, cabeçalhos e corpos não mudaram. Com a documentação desligada, nenhuma rota nova responde (teste acima).
4. **Sem efeito colateral** (REQ-029, SC-006): abrir a interface e buscar o documento não altera o contador de nenhum link. Teste de integração cria um link, consulta a documentação e confirma que `ClickCount` continua 0.
5. **Build sem avisos**: `TreatWarningsAsErrors` já está ativo.

## Complexity Tracking

> Não há violações a justificar.

| Violation | Why Needed | Simpler Alternative Rejected Because |
|-----------|------------|-------------------------------------|
| — | — | — |
