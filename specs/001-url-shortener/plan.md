# Plano de Implementação: Encurtador de URLs

**Branch**: `001-url-shortener` | **Data**: 2026-10-19 | **Spec**: [spec.md](./spec.md)

**Entrada**: Especificação da funcionalidade em `specs/001-url-shortener/spec.md`

**Nota**: Este plano é preenchido pelo comando `/speckit-plan`. Artefatos da Fase 0 e da Fase 1: [research.md](./research.md), [data-model.md](./data-model.md), [contracts/openapi.yaml](./contracts/openapi.yaml) e [quickstart.md](./quickstart.md).

## Resumo

Um serviço HTTP que recebe uma URL longa e devolve um link curto. O código é gerado automaticamente (7 caracteres) ou escolhido pelo criador (4 a 20 caracteres alfanuméricos). Visitantes são redirecionados para o destino com `302` e cada acesso incrementa um contador. O criador consulta o total apresentando a chave secreta de gerenciamento, que é entregue uma única vez na criação e armazenada apenas como hash.

Abordagem técnica:

- **Unicidade e concorrência**: a unicidade do código é garantida por índice único no banco, sobre a forma normalizada (minúscula) do código. A checagem no `LinkService` existe para dar mensagem clara; o índice é a garantia real.
- **Contador**: incrementado por `UPDATE ... SET ClickCount = ClickCount + 1` com filtro de validade na mesma instrução. A aplicação nunca lê o valor para reescrevê-lo.
- **Chave de gerenciamento**: 256 bits aleatórios, entregues em base64url. O banco guarda somente o SHA-256 em hexadecimal, comparado em tempo constante.
- **Alfabeto do código gerado**: `23456789abcdefghjkmnpqrstuvwxyz` (31 caracteres, sem `0`, `1`, `i`, `l`, `o`).
- **Regras de negócio** ficam no `LinkService` (camada de aplicação). Endpoints apenas leem a requisição, chamam o serviço e mapeiam o resultado.
- **Persistência**: EF Core com SQLite nesta versão. A escolha do provider fica em um único ponto de composição na camada de infraestrutura.

## Contexto Técnico

**Linguagem/Versão**: C# 14 sobre .NET 10 (SDK 10.0.x)

**Dependências principais**:
- ASP.NET Core Minimal APIs (framework)
- Limitação de taxa do ASP.NET Core, `Microsoft.AspNetCore.RateLimiting` (framework, sem pacote novo)
- EF Core 10 (`Microsoft.EntityFrameworkCore`) com provider `Microsoft.EntityFrameworkCore.Sqlite`

Os pacotes novos estão justificados na seção "Dependências Novas" abaixo, conforme o Princípio VII.

**Armazenamento**: SQLite, em arquivo, via EF Core. Migrações versionadas em `Infrastructure/Migrations/Sqlite`.

**Testes**: xUnit. Projeto de testes unitários separado do projeto de testes de integração. A integração usa `WebApplicationFactory` contra SQLite em arquivo temporário.

**Plataforma alvo**: Linux, instância única do serviço (container ou processo).

**Tipo de projeto**: serviço web (API HTTP e redirecionamento).

**Metas de desempenho**:
- SC-001: o link curto é devolvido em menos de 30 segundos após o envio. Essa meta é folgada para o escopo. Verificada pelo passo 13 do `quickstart.md`, por medição manual, sem teste automatizado.
- Não há meta de latência de redirecionamento nesta versão. A especificação não a define.

**Restrições**:
- Contagem exata sob acessos simultâneos (REQ-015, SC-004).
- Links e contadores persistem após reinício (REQ-014, SC-003).
- Redirecionamento sem cache de navegador ou intermediário (REQ-016).
- Limite de criação por endereço de origem, em memória, para instância única (REQ-017).
- Confiança no `RemoteIpAddress` do Kestrel. Nesta versão não há proxy reverso. Se houver, `ForwardedHeaders` deve ser configurado antes de a limitação de taxa valer.

**Escala/Escopo**: até cerca de 100 mil acessos por dia (≈ 1,2 requisições por segundo em média, com pico estimado de 10× ≈ 12 por segundo). Pequena escala. O volume de links não foi especificado. O modelo é indexado para milhões de linhas.

## Constitution Check

*GATE: deve passar antes da Fase 0. Reavaliado após o desenho da Fase 1.*

| Princípio | Conformidade | Como o plano atende |
|-----------|--------------|---------------------|
| I. Endpoints não acessam banco diretamente | Conforme | Endpoints recebem apenas `LinkService`. Nenhum endpoint recebe `DbContext`, `DbSet` ou consulta EF. |
| II. Regra de negócio fora do HTTP | Conforme | Validação de URL, código, reservadas, expiração, chave e limite de criação vivem no `LinkService` ou em classes de domínio na camada de aplicação. Os endpoints apenas mapeiam o `LinkResult`. |
| III. Persistência com EF Core e provider trocável | Conforme | EF Core com LINQ e `ExecuteUpdateAsync`, sem SQL manual. Provider escolhido em `Infrastructure/DependencyInjection.cs`, por configuração `Database:Provider`. Ver "Pendências para o provider PostgreSQL". |
| IV. Regra de negócio com teste unitário xUnit | Conforme | Tarefas de teste unitário para cada regra de `LinkService`, `ShortCodeGenerator`, `ManagementKey` e `UrlValidator`. Sem HTTP, banco ou sistema de arquivos. |
| V. Endpoint com teste de integração de caminho feliz | Conforme | Teste de integração de caminho feliz para `POST /api/links`, `GET /{code}` e `GET /api/links/{code}`, exercitados pela pilha HTTP com `WebApplicationFactory`. |
| VI. Erro de validação é 400 com ProblemDetails | Conforme, com interpretação explícita | Toda falha de validação de entrada retorna 400 `application/problem+json`. Ver "Decisões de Resposta HTTP" para código em uso, reservado e formato de chave. |
| VII. Nenhuma dependência sem justificativa | Conforme | Pacotes novos listados na seção "Dependências Novas", cada um com motivo e alternativa. |
| VIII. Nomes em inglês, mensagens em português | Conforme | Código em inglês. `title` e `detail` do ProblemDetails e mensagens de validação em português. |
| IX. Commits rastreáveis ao requisito | Conforme | Commits no formato `tipo(REQ-NNN): descrição`, conforme REQ-001 a REQ-018. |

**Restrições Técnicas**: .NET 10 e C#, Minimal APIs, EF Core com provider configurável, xUnit com projetos separados e ProblemDetails. Todas atendidas.

**Fluxo de Desenvolvimento**: toda tarefa de endpoint terá tarefa de teste de integração de caminho feliz. Toda tarefa de regra de negócio terá tarefa de teste unitário xUnit. Ambas serão geradas em `tasks.md`.

**Resultado**: nenhuma violação. Não há entradas em "Rastreamento de Complexidade".

## Decisões Registradas

### D1. Unicidade por índice único no banco, além da checagem no serviço

**Decisão**: `Link.CodeKey` (código em minúscula) tem índice único. O `LinkService` consulta antes para dar mensagem clara, mas a inserção sempre é tentada, e a violação do índice é tratada como "código em uso".

**Justificativa**: a checagem "consulta, depois insere" tem janela de corrida. Duas criações simultâneas com o mesmo código passam pela consulta e as duas inserem. Só o índice único, avaliado pelo banco, garante que apenas uma inserção vence (caso de borda da spec e SC-006). Ver [research.md](./research.md) R2.

### D2. Contador incrementado por update atômico

**Decisão**: a contagem usa `ExecuteUpdateAsync(s => s.SetProperty(l => l.ClickCount, l => l.ClickCount + 1))` com filtro de validade na mesma instrução. A aplicação não lê o contador para reescrevê-lo.

**Justificativa**: ler, somar e gravar em duas etapas perde incrementos sob concorrência (REQ-015). A instrução única é atômica no banco. O filtro de expiração na mesma instrução impede contar acesso a link expirado mesmo em corrida com o instante de expiração (REQ-010). Ver [research.md](./research.md) R3.

### D3. Chave de gerenciamento armazenada como hash

**Decisão**: a chave tem 256 bits gerados por `RandomNumberGenerator`, em base64url (43 caracteres). O banco guarda `SHA-256` em hexadecimal (64 caracteres). A verificação usa `CryptographicOperations.FixedTimeEquals`.

**Justificativa**: a chave nunca é recuperável (REQ-013). Um hash rápido basta porque a chave tem entropia alta. Funções lentas como bcrypt e Argon2 existem para senhas de baixa entropia, e exigiriam pacote novo sem ganho aqui. Ver [research.md](./research.md) R4.

### D4. Alfabeto do código gerado sem caracteres ambíguos

**Decisão**: o código gerado automaticamente usa o alfabeto `23456789abcdefghjkmnpqrstuvwxyz`, de 31 caracteres. Ficam de fora `0`, `1`, `i`, `l` e `o`, por serem visualmente confundíveis. O código tem 7 caracteres, com espaço de ≈ 2,7 × 10¹⁰ combinações.

**Justificativa**: o criador copia e digita o link à mão. Caracteres ambíguos geram erros de digitação e acesso a outro link. O alfabeto é em minúscula porque a comparação é insensível a maiúsculas (REQ-004). Códigos personalizados seguem REQ-002 (letras e números), sem restrição de alfabeto, já que o criador escolhe o código conscientemente.

### D5. Código comparado sem diferenciar maiúsculas

**Decisão**: `CodeKey = código.ToLowerInvariant()`. Esse valor é o único usado em índice único e em busca. O código original é guardado só para exibição.

**Justificativa**: SQLite só oferece `COLLATE NOCASE` e PostgreSQL usa outra sintaxe. Uma coluna normalizada funciona igual nos dois providers e não depende de collation (Princípio III).

### D6. Redirecionamento com 302 e sem cache

**Decisão**: o redirecionamento responde `302 Found` com `Cache-Control: no-store`.

**Justificativa**: `301` é cacheado pelo navegador e por intermediários. Um acesso seguinte poderia não passar pelo sistema e deixaria de ser contado (REQ-016).

### D7. Limite de criação por endereço de origem

**Decisão**: o limite de criações por origem é uma regra de negócio. Fica atrás da porta `ICreationLimiter`, na camada de aplicação, com 20 criações por hora por origem, configurável em `Links:CreationLimitPerHour`. O `LinkService` consulta a porta antes de qualquer validação. Quando o limite é excedido, retorna `RateLimited`, que o `ProblemMapper` converte em `429` com ProblemDetails em português. O adaptador `FixedWindowCreationLimiter`, em `Api/RateLimiting/`, usa o `FixedWindowRateLimiter` do ASP.NET Core. É o único ponto que conhece o framework.

**Justificativa**: REQ-017 é regra de negócio, e o Princípio II exige que regras vivam na camada de aplicação. A porta permite teste unitário com um limitador falso. O framework continua fazendo a contagem por janela, sem pacote novo. O uso esperado é de criação manual por pessoas, e vinte por hora cobre o uso legítimo e barra abuso automatizado. O contador fica em memória, o que basta para a instância única desta versão. Compartilhar o limite entre instâncias exigiria armazenamento externo, fora de escopo.

### D8. Palavras reservadas

**Decisão**: lista fixa, comparada sem diferenciar maiúsculas: `api`, `health`, `healthz`, `ready`, `live`, `swagger`, `openapi`, `admin`, `links`, `metrics`. Fica em `Application/Links/ReservedCodes.cs`.

**Justificativa**: são os caminhos que o próprio sistema expõe (REQ-018). Um código reservado criaria link que colidiria com uma rota.

### D9. Expiração em UTC

**Decisão**: `ExpiresAtUtc` e `CreatedAtUtc` são guardados como `DateTimeOffset` normalizados para UTC. O instante é obtido por `TimeProvider`.

**Justificativa**: no SQLite, comparações SQL sobre `DateTimeOffset` com offsets diferentes são feitas como texto e ficam erradas. Normalizar para UTC antes de gravar e de comparar corrige isso. `TimeProvider` permite testar expiração sem `Thread.Sleep`. Ver [research.md](./research.md) R5.

**Ajuste de implementação**: o provider SQLite do EF Core não traduz comparações de `DateTimeOffset` em LINQ, e a condição `ExpiresAtUtc > agora` do D2 falhava na tradução. `ExpiresAtUtc` é guardado como ticks UTC (`long`, via `ValueConverter` em `LinkConfiguration`). O tipo no domínio continua `DateTimeOffset`, e a comparação segue dentro do banco, na mesma instrução do contador.

### D10. Migrações EF Core

**Decisão**: esquema versionado por migrações EF Core. A aplicação aplica as migrações na inicialização.

**Justificativa**: o esquema precisa sobreviver a reinícios e evoluir (SC-003). `EnsureCreated` não evolui esquema existente e não serve à troca de provider.

## Decisões de Resposta HTTP

| Situação | Status | Corpo | Origem |
|----------|--------|-------|--------|
| Criação com sucesso | `201 Created` + `Location` | JSON com código, link curto, expiração e chave (única vez) | REQ-001, REQ-002, REQ-006 |
| URL inválida (esquema, tamanho, formato) | `400` | ProblemDetails | REQ-005, Princípio VI |
| Código personalizado fora do formato | `400` | ProblemDetails | REQ-002, Princípio VI |
| Código personalizado reservado | `400` | ProblemDetails | REQ-018, Princípio VI |
| Código personalizado já em uso | `400` | ProblemDetails | REQ-003 |
| Data de expiração no passado | `400` | ProblemDetails | REQ-007, Princípio VI |
| Limite de criações excedido | `429` | ProblemDetails | REQ-017 |
| Redirecionamento de link ativo | `302` + `Cache-Control: no-store` | vazio | REQ-008, REQ-016 |
| Link expirado | `410 Gone` | ProblemDetails com "link expirou" | REQ-010 |
| Código sem link | `404` | ProblemDetails "link não encontrado" | REQ-011 |
| Consulta sem cabeçalho de chave, ou com chave em formato inválido | `400` | ProblemDetails | Princípio VI |
| Consulta com chave de formato válido que não confere | `401` | ProblemDetails | REQ-013 |
| Consulta com chave correta | `200` | JSON com código, destino, criação, expiração e total | REQ-013 |

**Interpretações a confirmar**:
- O código de "já em uso" e o de "reservado" são `400`, e não `409`. O Princípio VI não admite exceção para `422`, e o caso é tratado como código inválido para criação. Se preferir `409`, a emenda deve ser feita antes, pela regra de governança.
- Código fora do formato em `GET /{code}` responde `404`, e não `400`. Exceção ao Princípio VI, aceita pelo responsável do projeto. Motivo: a rota faz busca, não recebe entrada de validação, e nenhum link pode existir com código fora do formato.
- A chave ausente ou em formato inválido é `400` (validação de entrada). A chave em formato válido que não confere é `401` (autenticação).

## Dependências Novas

| Pacote | Versão | Projeto | Motivo | Alternativa considerada | Por que a alternativa não atende |
|--------|--------|---------|--------|-------------------------|----------------------------------|
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.x (linha 10.0, mesma do .NET 10; patch fixado no primeiro commit de implementação) | Infrastructure | Provider SQLite exigido pela entrada do usuário | `Microsoft.Data.Sqlite` puro, sem EF | Violaria o Princípio III, que exige EF Core. |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.x (mesma linha) | Infrastructure e Api (`PrivateAssets=all`; o `dotnet ef` exige o pacote no projeto de inicialização, que é a Api) | Gerar migrações com `dotnet ef migrations add` | `EnsureCreated` | Não gera migrações e não evolui esquema existente (D10). |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.x (mesma linha) | IntegrationTests | `WebApplicationFactory` para exercitar endpoints pela pilha HTTP (Princípio V) | `Microsoft.AspNetCore.TestHost` direto | Obriga a reimplementar host, configuração e conteúdo raiz que o `Mvc.Testing` já resolve. |

**Não adicionados, de propósito**:
- Limitação de taxa: é o `Microsoft.AspNetCore.RateLimiting` do próprio framework.
- Hash: `System.Security.Cryptography` faz parte da BCL.
- Geração de chave e código: `RandomNumberGenerator` da BCL.
- Biblioteca de asserções: as asserções do xUnit são suficientes.
- Swagger UI: o contrato é escrito à mão em `contracts/openapi.yaml`, sem pacote de interface.

**Base do stack, sem justificativa adicional**: .NET 10, ASP.NET Core, EF Core, xUnit.

## Pendências para o Provider PostgreSQL

Não implementadas nesta versão. Registradas para que a troca não quebre o desenho:
- Migrações são específicas de provider. Ficarão em pasta própria, por exemplo `Infrastructure/Migrations/PostgreSql`, selecionada pelo mesmo ponto de composição.
- `Infrastructure/Persistence/UniqueConstraintDetector` é o único lugar que conhece o código de erro de violação de unicidade de cada provider (SQLite: `SqliteExtendedErrorCode` `2067`, `SQLITE_CONSTRAINT_UNIQUE`; PostgreSQL: `23505`). O resto do código só vê `bool`.
- `DateTimeOffset` com UTC (D9) funciona sem mudança em PostgreSQL `timestamptz`.

## Estrutura do Projeto

### Documentação desta funcionalidade

```text
specs/001-url-shortener/
├── spec.md              # Especificação (entrada)
├── plan.md              # Este arquivo (saída de /speckit-plan)
├── research.md          # Saída da Fase 0
├── data-model.md        # Saída da Fase 1
├── quickstart.md        # Saída da Fase 1
├── contracts/
│   └── openapi.yaml     # Saída da Fase 1
├── checklists/
└── tasks.md             # Saída de /speckit-tasks (não criado aqui)
```

### Código-fonte

```text
src/
├── Encurtador.Api/                          # Minimal APIs, composição da aplicação
│   ├── Program.cs                           # Host, composição, MapLinkEndpoints
│   ├── Endpoints/
│   │   └── LinkEndpoints.cs                 # Único arquivo de endpoints do recurso "link"
│   ├── Http/
│   │   └── ProblemMapper.cs                 # ErrorKind → status HTTP e ProblemDetails
│   ├── RateLimiting/
│   │   └── FixedWindowCreationLimiter.cs    # Adaptador da porta com FixedWindowRateLimiter
│   ├── Models/
│   │   ├── CreateLinkRequest.cs
│   │   └── LinkResponses.cs
│   └── appsettings.json                     # Database:Provider, Links:PublicBaseUrl, Links:CreationLimitPerHour
│
├── Encurtador.Application/                  # Regras de negócio, sem dependência de EF ou HTTP
│   └── Links/
│       ├── Link.cs                          # Entidade de domínio
│       ├── LinkService.cs                   # Dona das regras de negócio
│       ├── LinkResult.cs                    # Resultado com erro tipado, sem exceção para fluxo
│       ├── AccessOutcome.cs                 # Redirected, Expired ou NotFound
│       ├── CreateLinkCommand.cs             # Entrada de criação, inclui Origin
│       ├── CreatedLink.cs                   # Saída de criação, inclui chave em texto
│       ├── LinkStats.cs                     # Saída da consulta de acessos
│       ├── ILinkRepository.cs               # Porta de persistência
│       ├── ICreationLimiter.cs              # Porta do limite de criação (D7)
│       ├── IShortCodeGenerator.cs
│       ├── ShortCodeGenerator.cs            # Alfabeto de D4
│       ├── ReservedCodes.cs                 # Lista de D8
│       ├── UrlValidator.cs
│       ├── ManagementKey.cs                 # Geração e hash de D3
│       └── LinkOptions.cs                   # Record simples, preenchido pela Api
│
└── Encurtador.Infrastructure/               # Único lugar com EF Core e SQL
    ├── DependencyInjection.cs               # Ponto único de escolha de provider
    ├── Persistence/
    │   ├── AppDbContext.cs
    │   ├── LinkConfiguration.cs             # Índice único em CodeKey, check de ClickCount >= 0
    │   ├── EfLinkRepository.cs              # Insert com retorno bool e increment atômico
    │   └── UniqueConstraintDetector.cs      # Código de erro por provider
    └── Migrations/
        └── Sqlite/

tests/
├── Encurtador.UnitTests/                    # Sem HTTP, banco ou arquivo
│   ├── Fakes/
│   │   ├── InMemoryLinkRepository.cs
│   │   └── FixedTimeProvider.cs
│   ├── LinkTests.cs                         # IsExpiredAt
│   ├── LinkServiceCreateTests.cs            # Regras de criação e limite
│   ├── LinkServiceRedirectTests.cs          # Redirecionamento e contagem
│   ├── LinkServiceStatsTests.cs             # Consulta com chave
│   ├── ShortCodeGeneratorTests.cs
│   ├── ManagementKeyTests.cs
│   └── UrlValidatorTests.cs
└── Encurtador.IntegrationTests/             # Pela pilha HTTP, SQLite em arquivo temporário
    ├── LinkApiFactory.cs                    # WebApplicationFactory com arquivo temporário
    ├── DatabaseConstraintsTests.cs          # Unicidade e check no banco
    ├── LinkCreationEndpointTests.cs         # Caminho feliz e 400 com ProblemDetails
    ├── RedirectEndpointTests.cs             # Caminho feliz, expirado, não encontrado
    ├── LinkStatsEndpointTests.cs            # Caminho feliz, 400, 401
    ├── ConcurrentCreationTests.cs           # Criação simultânea com mesmo código
    ├── ConcurrentAccessTests.cs             # 100 acessos simultâneos
    ├── PersistenceTests.cs                  # Reinício da aplicação
    └── RateLimitTests.cs                    # Limite de criação e fiação da porta
```

**Decisão de estrutura**: cinco projetos (três de produção e dois de teste), em vez de um só.
- `Application` sem EF e sem ASP.NET garante por compilação que as regras não dependem de infraestrutura (Princípios II e III).
- `Infrastructure` isolado é o único ponto onde o provider é trocado.
- Testes unitários e de integração em projetos separados (Restrições Técnicas).

## Rastreamento de Complexidade

| Violação | Por que é necessária | Alternativa mais simples rejeitada porque |
|----------|----------------------|-------------------------------------------|
| Nenhuma | - | - |
