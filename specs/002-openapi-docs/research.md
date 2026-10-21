# Pesquisa da Fase 0: Documentação OpenAPI

**Branch**: `002-openapi-docs` | **Data**: 2026-10-20 | **Plano**: [plan.md](./plan.md)

Este documento consolida as decisões técnicas do plano. Não há marcadores `[NEEDS CLARIFICATION]` restantes na especificação. Uma verificação prática (R5) fica como primeira tarefa de implementação, porque depende do comportamento da interface em execução.

## Estado atual do projeto (levantado em 2026-10-20)

| Item | Situação |
|------|----------|
| Runtime | .NET 10 (SDK 10.0.401, runtime Microsoft.AspNetCore.App 10.0.12) |
| Tipo de aplicação | `Microsoft.NET.Sdk.Web`, Minimal APIs, `WebApplication.CreateBuilder` |
| Registro dos endpoints | `Api/Endpoints/LinkEndpoints.cs`, método `MapLinkEndpoints`, chamado em `Program.cs` |
| Rotas | `POST /api/links`, `GET /api/links/{code}`, `GET /{code}` (catch-all de um segmento) |
| Documentação atual | Nenhuma. `AddProblemDetails()` já registrado. Sem `Microsoft.AspNetCore.OpenApi`, sem Swagger |
| Autenticação | Nenhum middleware de autenticação ou autorização. A chave de gerenciamento é o cabeçalho `X-Management-Key`, lido com `[FromHeader]` e validado no `LinkService` |
| Configuração | `appsettings.json` (provider, connection string, `Links:*`), `appsettings.Development.json` (apenas logging e `Links: {}`) |
| Ambiente | `launchSettings.json` define `ASPNETCORE_ENVIRONMENT=Development` |
| Testes | xUnit; `WebApplicationFactory<Program>` em `LinkApiFactory`, que usa o ambiente padrão de testes (Development) |
| Pacotes existentes | EF Core 10.0.13 (Sqlite e Design), `Microsoft.AspNetCore.Mvc.Testing` 10.0.13 |
| Pacotes NuGet disponíveis | `Microsoft.AspNetCore.OpenApi` 10.0.13 (publicado, alinhado ao restante). `Scalar.AspNetCore` 2.18.2 (última estável observada, dependências para net10.0) |

## R1. Geração do documento OpenAPI

**Decisão**: `Microsoft.AspNetCore.OpenApi` 10.0.13, com `AddOpenApi()` e `MapOpenApi`.

**Justificativa**: é o recurso nativo do ASP.NET Core para gerar OpenAPI a partir de Minimal APIs, e o plano pede priorizar recursos nativos. A versão acompanha os demais pacotes Microsoft.AspNetCore do projeto (10.0.13). Não está incluído no runtime compartilhado, então é necessário referenciá-lo.

**Alternativas consideradas**:
- Swashbuckle.AspNetCore: gera documento próprio, em paralelo ao recurso nativo. Mais pacotes e configuração extra, e o projeto perderia a fonte única do contrato. Rejeitado.
- Escrever o JSON à mão, em arquivo estático: viola REQ-024 (a documentação deixaria de acompanhar os endpoints). Rejeitado.

## R2. Interface interativa

**Decisão**: `Scalar.AspNetCore` 2.18.2, consumindo o documento de R1.

**Justificativa**: um único pacote de interface, que lê o documento nativo sem geração paralela. Atende REQ-021 (preencher parâmetros, enviar e ver a resposta, sem ferramenta externa).

**Alternativas consideradas**:
- Swagger UI via `Swashbuckle.AspNetCore.SwaggerUI`: exige também o documento do próprio Swashbuckle para ficar integrado, ou apontar para o documento nativo com configuração extra. Duas peças onde uma basta. Rejeitado, mas viável como plano B se R5 falhar nos dois.
- Sem interface, apenas o JSON: não atende REQ-021. Rejeitado.

**Risco aceito**: a interface pode carregar seus recursos estáticos de CDN. Isso só afeta o ambiente de desenvolvimento, mas precisa ser verificado (R5).

## R3. Rota da documentação e comportamento quando desligada

**Decisão**: a interface fica em `/api/docs` e o documento em `/api/openapi/v1.json`.

**Justificativa**: REQ-023 exige que, desligada, a documentação responda igual a uma rota inexistente. A rota `GET /{code}` captura qualquer segmento único. Se a interface ficasse em `/scalar` ou `/swagger`, quando desligada ela cairia no redirecionamento e responderia `404 application/problem+json` com "Link não encontrado", diferente da resposta de uma rota que não existe. Caminhos com dois ou mais segmentos sob `/api/` não casam com `/{code}` nem com `/api/links/{code}`, então quando desligados recebem o 404 padrão do framework, o mesmo de qualquer rota inexistente. O prefixo `api` já é palavra reservada (REQ-018, D8), então nenhum código personalizado colide com essas rotas e REQ-027 é atendido sem alterar a lista de reservadas.

**Alternativas consideradas**:
- `/scalar` ou `/swagger` (rotas de uma palavra): exigiria reservar mais um código e não produz 404 idêntico quando desligada. Rejeitado.
- Mapear `/{code}` com exceção para essas rotas quando desligadas: adiciona regra de roteamento especial só para simular um 404. Rejeitado.

**Verificação**: o teste de integração compara o corpo e o status de `GET /api/docs`, `GET /api/openapi/v1.json` e `GET /api/rota-inexistente` quando desligada (ver `quickstart.md`).

## R4. Ativação por configuração

**Decisão**: chave `Documentation:Enabled`. Padrão `false` em `appsettings.json`. `true` em `appsettings.Development.json`. Em produção ou teste compartilhado, o valor só muda por configuração explícita (variável de ambiente `Documentation__Enabled=true` ou equivalente).

**Justificativa**: REQ-022 e REQ-023 exigem que a regra seja decidida por configuração, não pelo nome do ambiente. Assim, a equipe liga a documentação em um ambiente compartilhado sem alterar código, e um ambiente novo nasce fechado por padrão. Um único ponto de decisão em `Program.cs` evita ramificações espalhadas.

**Alternativas consideradas**:
- `if (app.Environment.IsDevelopment())` puro: não permite habilitar em um ambiente de teste compartilhado sem mudar código. Rejeitado.
- Ativar sempre e proteger com autenticação: a spec exclui autenticação na documentação (premissas). Rejeitado.

## R5. Compatibilidade da interface com o redirecionamento (REQ-028) e com REQ-029

**Problema**: a interface executa as chamadas com `fetch` do navegador. Por padrão, `fetch` segue redirecionamentos, então o "Try it" de `GET /{code}` seguiria o `302` para o destino. REQ-028 exige mostrar o redirecionamento e a URL de destino sem segui-lo.

**Decisão**: tratar como primeira tarefa de implementação uma verificação prática (spike) com a interface escolhida em R2. Critério de aceite: o `302` aparece com `Location` e o corpo, e a interface não navega para o destino.

**Plano B, em ordem, se o spike falhar**:
1. Configuração da própria interface para não seguir redirecionamentos, se a versão escolhida expuser essa opção.
2. Trocar para `Swashbuckle.AspNetCore.SwaggerUI` e verificar o mesmo critério. Decisão que volta para o usuário, porque muda a escolha de R2.
3. Não é aceito ocultar a operação de redirecionamento, porque violaria SC-002 e REQ-021.

REQ-029 não tem risco: a interface só chama o servidor quando o usuário clica em executar. Abrir a página não dispara nenhuma requisição.

## R6. Documentação dos endpoints (REQ-020, REQ-024, REQ-025, REQ-026)

**Decisão**: anotar cada endpoint existente diretamente em `LinkEndpoints.cs`, com `WithSummary`, `WithDescription`, `Produces` para o sucesso e `ProducesProblem` para cada erro (400, 404, 410, 401, 429 conforme o endpoint). Os textos ficam em português. Exemplos com dados fictícios são definidos por transformadores de esquema ou de operação do `AddOpenApi`, sem `WithOpenApi` (obsoleto no .NET 10).

**Justificativa**: a anotação vive ao lado da rota, então uma mudança no endpoint também muda a documentação (REQ-024, SC-005). Os tipos de resposta já existem (`CreatedLinkResponse`, `LinkStatsResponse`), então o esquema sai do próprio código.

**Alternativa**: arquivo OpenAPI separado editado à mão. Rejeitado por violar REQ-024.

**Cabeçalho de chave**: `X-Management-Key` já aparece como parâmetro de cabeçalho pelo `[FromHeader]`. Não se usa `securitySchemes`, porque o projeto não tem esquema de autenticação e declará-lo sugeriria um middleware que não existe.

## R7. Coerência com o contrato da especificação 001

**Decisão**: o documento gerado é o contrato vivo da API. `specs/001-url-shortener/contracts/openapi.yaml` continua como contrato de planejamento. A divergência é verificada no teste de integração, que compara operações, parâmetros e códigos de resposta do documento gerado com o contrato 001 (ver `quickstart.md`).

**Justificativa**: a premissa da spec 002 exige que os dois documentos fiquem coerentes pelo processo de planejamento. Um teste automatizado é mais barato de manter do que revisão manual.

## R8. Dependências novas (Princípio VII)

Resumidas em `plan.md`, seção "Dependências Novas". Nenhuma outra biblioteca é necessária.
