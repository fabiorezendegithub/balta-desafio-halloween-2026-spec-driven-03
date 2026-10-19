# Pesquisa da Fase 0: Encurtador de URLs

Todas as decisões de `NEEDS CLARIFICATION` estão resolvidas. As escolhas estão resumidas em [plan.md](./plan.md), seção "Decisões Registradas". Este arquivo guarda a justificativa e as alternativas.

## R1. Geração e formato do código

**Decisão**: código gerado com 7 caracteres do alfabeto `23456789abcdefghjkmnpqrstuvwxyz` (31 caracteres), usando `RandomNumberGenerator.GetInt32`. Em caso de colisão, a inserção é repetida até 5 vezes.

**Justificativa**: 31^7 ≈ 2,7 × 10¹⁰ combinações. Com até 100 mil acessos por dia, o volume de links é pequeno em relação a esse espaço, e colisões são raras. O alfabeto evita `0`, `1`, `i`, `l` e `o`, que se confundem ao digitar.

**Alternativas consideradas**:
- Base62 completo (`A-Za-z0-9`): mais curto em entropia por caractere, mas com caracteres ambíguos e incompatível com a comparação sem diferenciar maiúsculas.
- Hash da URL de destino: a spec diz que cada criação gera um link novo, mesmo para a mesma URL (Premissas). Hash determinístico colidiria.
- Contador sequencial codificado em base: previsível, permite enumerar links alheios.

## R2. Unicidade sob concorrência

**Decisão**: índice único em `CodeKey` no banco. A inserção é tentada diretamente. Violação de unicidade retorna `false` do repositório, detectada por `UniqueConstraintDetector` por provider.

**Justificativa**: consulta seguida de inserção tem janela de corrida. Duas criações simultâneas com o mesmo código passariam pela consulta. Somente o índice único, avaliado pelo banco, garante que apenas uma inserção vence (spec, caso de borda; SC-006). A consulta prévia do `LinkService` continua existindo para dar mensagem clara sem tentar a inserção.

**Alternativas consideradas**:
- Bloqueio em memória (`lock` ou `SemaphoreSlim`): não funciona com mais de uma instância e não cobre reinício.
- Transação serializável com consulta: depende do nível de isolamento de cada provider e é mais lenta.

## R3. Contador atômico

**Decisão**: `ExecuteUpdateAsync` com `SetProperty(l => l.ClickCount, l => l.ClickCount + 1)` e filtro `WHERE CodeKey = @k AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > @now)` na mesma instrução. Retorna o número de linhas afetadas. Se for `1`, a URL de destino é lida (imutável nesta versão, então a leitura posterior é segura). Se for `0`, uma segunda consulta distingue "não encontrado" de "expirado".

**Justificativa**: `ClickCount = ClickCount + 1` executa dentro do banco, de forma atômica. Ler o valor, somar na aplicação e gravar de volta perderia incrementos sob concorrência (REQ-015). Colocar o filtro de expiração na mesma instrução elimina a corrida entre verificar expiração e incrementar (REQ-010, SC-005).

**Alternativas consideradas**:
- Ler, verificar e gravar com controle de concorrência otimista (coluna de versão): correto, mas exige novas tentativas sob contenção. Desnecessário para um contador.
- Fila de eventos de acesso com agregação posterior: contagem eventualmente consistente. A spec exige que o total cresça exatamente pelo número de acessos válidos, sem atraso.
- Contador em cache (Redis, memória): perderia dados no reinício (REQ-014).

## R4. Chave de gerenciamento

**Decisão**: 32 bytes aleatórios de `RandomNumberGenerator`, codificados em base64url (43 caracteres, sem padding). Armazenado: SHA-256 em hexadecimal, 64 caracteres. Verificação: `CryptographicOperations.FixedTimeEquals` sobre os bytes do hash.

**Justificativa**: a chave tem 256 bits de entropia. Não há ataque de dicionário viável, então uma função de hash rápida é suficiente e segura. Funções lentas (bcrypt, Argon2, PBKDF2) servem para senhas, que têm entropia baixa. Usá-las aqui custaria latência e um pacote novo sem ganho.

**Alternativas consideradas**:
- bcrypt ou Argon2: pacote novo, custo de latência em toda consulta, sem ganho de segurança para chave aleatória.
- Guardar a chave em texto puro: rejeitado pela entrada do usuário e por REQ-013.
- Guardar um identificador do link com a chave como segredo compartilhado: não resolve, porque o identificador ainda seria usado para validar.
- Token JWT: adiciona assinatura, rotação e gestão de chave de assinatura sem necessidade.

## R5. Datas e SQLite

**Decisão**: `DateTimeOffset` normalizado para UTC antes de gravar e de comparar. O instante vem de `TimeProvider`.

**Justificativa**: o provider EF Core do SQLite guarda `DateTimeOffset` como texto com o offset. Comparações SQL em texto só são corretas se todos os valores tiverem o mesmo offset. Normalizar para UTC garante isso. `TimeProvider` (BCL) permite fixar o relógio em testes de expiração.

**Alternativas consideradas**:
- Guardar como inteiro Unix em milissegundos: correto, mas perde legibilidade no banco e precisa de conversão em cada consulta.
- Guardar `DateTime` sem zona: ambíguo para a spec, que exige fuso explícito (Premissas).

**Pendência**: um teste de integração deve provar a comparação correta com datas de offsets diferentes, de modo que uma regressão nessa conversão seja detectada.

## R6. Resposta do redirecionamento

**Decisão**: `302 Found` com `Cache-Control: no-store`.

**Justificativa**: `301` é cacheado pelo navegador e por intermediários, e acessos seguintes deixariam de ser contados (REQ-016).

**Alternativas consideradas**:
- `301` Moved Permanently: rejeitado pelo motivo acima.
- `307` Temporary Redirect: também seria correto, mas `302` é o usual para encurtadores e não altera o método.

## R7. Limitação de taxa na criação

**Decisão**: `FixedWindowRateLimiter` do ASP.NET Core, particionado por `RemoteIpAddress`, 20 criações por hora. Quando excedido, responde `429` com ProblemDetails em português. Valor em `Links:CreationLimitPerHour`.

**Justificativa**: vem no próprio framework, sem pacote novo (Princípio VII). O valor cobre uso manual legítimo e barra abuso automatizado. Em memória é suficiente para instância única.

**Alternativas consideradas**:
- Biblioteca externa de limitação (AspNetCoreRateLimit): pacote novo sem necessidade, pois o framework cobre o caso.
- Limite no banco: exigiria escrita extra por requisição de leitura e o contador perderia desempenho sem ganho de escopo.
- Limite no serviço: misturaria transporte (IP) com regra de negócio. O IP é dado HTTP, então o limite fica na borda, e o `LinkService` recebe só o resultado.

**Pendência**: se houver proxy reverso, `ForwardedHeaders` deve ser configurado para que o IP de origem seja o real. Fora de escopo nesta versão.

## R8. Migrações

**Decisão**: migrações EF Core, aplicadas na inicialização com `Database.MigrateAsync()`.

**Justificativa**: o esquema sobrevive a reinícios e evolui (SC-003). Também separa o esquema por provider, pois cada um tem sua pasta de migrações.

**Alternativas consideradas**:
- `EnsureCreated`: não evolui esquema existente e não separa por provider.
- Script SQL manual: fere o Princípio III, que proíbe SQL manual fora da infraestrutura. Mesmo dentro dela, perde a rastreabilidade das migrações.

## R9. Estrutura de projetos

**Decisão**: `Encurtador.Api`, `Encurtador.Application`, `Encurtador.Infrastructure`, `Encurtador.UnitTests`, `Encurtador.IntegrationTests`.

**Justificativa**: `Application` não referencia EF nem ASP.NET, o que torna a separação verificável em tempo de compilação. `Infrastructure` concentra a troca de provider.

**Alternativas consideradas**:
- Um projeto só com pastas: nada impede que um endpoint importe `DbContext` (Princípio I) ou que regras dependam de EF.
- Dois projetos (Api e Core): mistura persistência com regra de negócio, contrariando o Princípio III.

## R10. Palavras reservadas

**Decisão**: `api`, `health`, `healthz`, `ready`, `live`, `swagger`, `openapi`, `admin`, `links`, `metrics`. Comparação sem diferenciar maiúsculas.

**Justificativa**: são caminhos que o próprio sistema expõe. Código reservado com esses nomes criaria link que colidiria com rota, e o redirecionamento de `GET /{code}` esconderia a rota.

**Alternativas consideradas**:
- Verificar colisão com todas as rotas registradas em tempo de execução: frágil e acoplado ao framework.
- Prefixar rotas de sistema (`/_api/...`): muda a superfície da API sem necessidade, e a spec pede lista fixa no plano (REQ-018).
