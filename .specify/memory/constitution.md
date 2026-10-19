<!--
RELATÓRIO DE IMPACTO DE SINCRONIZAÇÃO (rascunho para revisão humana; remover antes do commit)

Mudança de versão: 1.0.0 → 1.1.0
Justificativa do bump: nova orientação materialmente expandida (idioma dos artefatos gerados). MINOR.

Princípios modificados: nenhum.
Adição no Fluxo de Desenvolvimento: idioma dos artefatos gerados pelo Spec Kit.
Princípios adicionados:
  - I. Endpoints Não Acessam Banco Diretamente
  - II. Regra de Negócio Fora do HTTP
  - III. Persistência com EF Core e Provider Trocável
  - IV. Regra de Negócio Coberta por Teste Unitário xUnit
  - V. Endpoint Coberto por Teste de Integração de Caminho Feliz
  - VI. Erro de Validação é 400 com ProblemDetails
  - VII. Nenhuma Dependência Sem Justificativa no Plano
  - VIII. Nomes em Inglês, Mensagens de Erro em Português
  - IX. Commits Rastreáveis ao Requisito

Seções adicionadas: Restrições Técnicas, Fluxo de Desenvolvimento.
Seções removidas: nenhuma. Os placeholders do template foram todos substituídos.

TODOs adiados: nenhum.
Interpretações a confirmar:
  - Data de ratificação: 2026-10-19 (primeira adoção, sem histórico anterior).
  - Nome do projeto "Encurtador de URLs", inferido do README.
  - Princípio VI: exige teste de integração para o 400 em cada endpoint que valida entrada.
  - Princípio IX: REQ-NNN vai no escopo do commit, por exemplo feat(REQ-001): ...
-->

# Constituição do Projeto Encurtador de URLs

## Princípios Fundamentais

### I. Endpoints Não Acessam Banco Diretamente

- Endpoints DEVEM obter e persistir dados somente por meio de um serviço de aplicação.
- Endpoints NÃO DEVEM receber `DbContext`, `DbSet` ou qualquer consulta EF Core como dependência.
- Racional: o endpoint é apenas o ponto de entrada HTTP. Manter o acesso a dados atrás do serviço
  de aplicação permite testar o fluxo sem subir HTTP nem banco.

### II. Regra de Negócio Fora do HTTP

- Regras de negócio DEVEM viver em classes de domínio ou em serviços de aplicação.
- Regras de negócio NÃO DEVEM ser escritas em lambdas de endpoint nem em handlers HTTP.
- Um endpoint DEVE apenas: ler a requisição, invocar o serviço de aplicação e mapear o resultado
  para a resposta HTTP.

### III. Persistência com EF Core e Provider Trocável

- A persistência DEVE usar EF Core.
- O provider de banco DEVE ser escolhido por configuração, em um único ponto de composição na
  camada de infraestrutura.
- O código da aplicação DEVE usar LINQ e a API do EF Core. SQL escrito à mão (`FromSqlRaw`,
  `ExecuteSqlRaw`, strings SQL) NÃO DEVE aparecer fora da camada de infraestrutura. Mesmo lá, só é
  aceito com justificativa no plano.
- Trocar o provider NÃO DEVE exigir alteração em endpoints, serviços de aplicação ou regras de
  negócio.

### IV. Regra de Negócio Coberta por Teste Unitário xUnit

- Toda regra de negócio DEVE ter ao menos um teste unitário em xUnit que falhe quando a regra for
  violada.
- Testes unitários NÃO DEVEM depender de HTTP, de banco de dados real ou de sistema de arquivos.
- Racional: as regras de negócio são o núcleo do produto, e a cobertura delas precisa ser verificável
  sem subir a aplicação.

### V. Endpoint Coberto por Teste de Integração de Caminho Feliz

- Todo endpoint DEVE ter ao menos um teste de integração cobrindo o caminho feliz, com status e corpo
  de sucesso verificados.
- Testes de integração DEVEM exercitar o endpoint pela pilha HTTP, e não chamar handlers
  diretamente.

### VI. Erro de Validação é 400 com ProblemDetails

- Toda falha de validação DEVE retornar HTTP 400 com corpo `application/problem+json`
  (ProblemDetails), sem exceção.
- Falha de validação NÃO DEVE ser respondida com 422 nem com corpo em texto livre ou formato próprio.
- Cada endpoint que valida entrada DEVE ter ao menos um teste de integração verificando o 400 e o
  tipo de conteúdo ProblemDetails.

### VII. Nenhuma Dependência Sem Justificativa no Plano

- Nenhum pacote NuGet ou dependência externa nova DEVE ser adicionado sem entrada no `plan.md`
  contendo: nome, versão, motivo, alternativa considerada e por que a alternativa não atende.
- O pacote de provider do EF Core conta como dependência nova e segue a mesma regra.
- As dependências base do stack (.NET 10, ASP.NET Core, EF Core, xUnit) já estão aprovadas e não
  exigem justificativa adicional.

### VIII. Nomes em Inglês, Mensagens de Erro em Português

- Nomes de classes, interfaces, métodos, propriedades, namespaces e arquivos DEVEM estar em inglês.
- Mensagens de erro retornadas pela API (`title` e `detail` do ProblemDetails e mensagens de
  validação) DEVEM estar em português.

### IX. Commits Rastreáveis ao Requisito

- Cada commit DEVE seguir o formato `tipo(escopo): descrição`, em que `tipo` é um de: `feat`, `fix`,
  `test`, `refactor`, `docs` ou `chore`.
- O escopo DEVE conter o identificador do requisito no formato `REQ-NNN`, conforme definido em
  `spec.md`. Exemplo: `feat(REQ-001): criar encurtamento de URL`.

## Restrições Técnicas

- Runtime e linguagem: .NET 10 e C#.
- API HTTP: ASP.NET Core Minimal APIs.
- Persistência: EF Core, com provider configurável (Princípio III).
- Testes: xUnit, com projeto de testes unitários separado do projeto de testes de integração.
- Erros de validação: ProblemDetails (Princípio VI).

## Fluxo de Desenvolvimento

- Todo `plan.md` DEVE incluir a seção `## Constitution Check`, que liste cada princípio e confirme a
  conformidade ou justifique o desvio.
- Se o plano contradizer um princípio, o plano DEVE ser alterado. O princípio NÃO DEVE ser
  afrouxado para acomodar o plano.
- Toda tarefa que cria endpoint DEVE incluir a tarefa de teste de integração do caminho feliz.
- Toda tarefa que cria regra de negócio DEVE incluir a tarefa de teste unitário xUnit.
- Todo PR DEVE verificar a conformidade com os princípios I a IX antes do merge.
- Os artefatos gerados pelos comandos Spec Kit (`spec.md`, `plan.md`, `tasks.md`, `checklists/`)
  DEVEM ser escritos em português.
- Marcadores e cabeçalhos que os comandos reconhecem DEVEM permanecer em inglês, sem tradução:
  `[NEEDS CLARIFICATION: ...]`, `## Clarifications`, `## Constitution Check` e o formato de tarefa
  `- [ ]`.
- Os arquivos de template em `.specify/templates/` NÃO DEVEM ser traduzidos, para preservar a
  compatibilidade com os comandos.

## Governança

- Esta constituição prevalece sobre as demais práticas do projeto.
- Emendas DEVEM ser propostas por escrito, com justificativa, em PR próprio. O PR DEVE atualizar
  o Relatório de Impacto, a versão e a data da última emenda.
- Versionamento segue SemVer:
  - MAJOR: remoção ou redefinição incompatível de princípio.
  - MINOR: novo princípio ou seção, ou expansão material de orientação.
  - PATCH: esclarecimentos, correções de texto e refinamentos sem efeito semântico.
- A conformidade DEVE ser verificada em cada `plan.md` (seção `## Constitution Check`) e em cada PR.
  Violação bloqueia o merge até ser corrigida ou formalmente emendada.

**Versão**: 1.1.0 | **Ratificada**: 2026-10-19 | **Última emenda**: 2026-10-19
