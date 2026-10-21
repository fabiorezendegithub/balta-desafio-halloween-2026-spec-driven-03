# Contratos: Documentação OpenAPI

**Branch**: `002-openapi-docs` | **Plano**: [plan.md](../plan.md)

## Documento gerado pela aplicação (fonte viva)

Quando a documentação está ligada, a aplicação publica:

- `GET /api/openapi/v1.json`: documento OpenAPI 3.1 gerado a partir das Minimal APIs.
- `GET /api/docs`: interface interativa (Scalar) sobre o documento acima.

Quando desligada, ambos respondem como uma rota inexistente (REQ-023).

Este documento é a fonte viva do contrato. Não há arquivo OpenAPI escrito à mão nesta funcionalidade (REQ-024).

## Operações que o documento deve conter

Conferidas pelo teste de integração de coerência com o contrato 001 (ver [quickstart.md](../quickstart.md)).

| operationId | Método e rota | Parâmetros | Respostas |
|-------------|---------------|------------|-----------|
| `CreateLink` | `POST /api/links` | corpo `CreateLinkRequest` | `201` (`CreatedLinkResponse`, `Location`), `400` (ProblemDetails), `429` (ProblemDetails) |
| `GetLinkStats` | `GET /api/links/{code}` | rota `code`; cabeçalho `X-Management-Key` | `200` (`LinkStatsResponse`), `400`, `401`, `404` (ProblemDetails) |
| `RedirectLink` | `GET /{code}` | rota `code` | `302` (`Location`), `404` e `410` (ProblemDetails) |

Os códigos seguem a tabela "Decisões de Resposta HTTP" de `specs/001-url-shortener/plan.md`.

## Coerência com o contrato 001

O contrato de planejamento está em [`specs/001-url-shortener/contracts/openapi.yaml`](../../001-url-shortener/contracts/openapi.yaml). O documento gerado deve ter as mesmas operações, os mesmos parâmetros e os mesmos códigos de resposta. Diferenças de descrição e de exemplo são aceitas. Diferenças estruturais falham o teste.

Atenção a um ponto conhecido: o contrato 001 declara `GET /{code}` com código fora do formato resultando em 404, sem validação de formato. Isso deve ser respeitado pela anotação do endpoint.
