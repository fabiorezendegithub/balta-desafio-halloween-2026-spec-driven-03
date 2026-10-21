# Guia de Validação Rápida: Documentação OpenAPI

**Branch**: `002-openapi-docs` | **Plano**: [plan.md](./plan.md)

Roteiro para provar que a documentação funciona, respeita a configuração e não altera os endpoints existentes. Os comandos assumem a raiz do repositório.

## Pré-requisitos

- .NET SDK 10.0.x (verificado: 10.0.401).
- Acesso à internet na primeira restauração, e para a interface carregar seus recursos (ver R2 em `research.md`).

## 1. Restaurar e compilar sem avisos

```bash
dotnet restore Encurtador.slnx
dotnet build Encurtador.slnx --no-restore
```

Esperado: build sem erros nem avisos (`TreatWarningsAsErrors` ativo).

## 2. Spike da interface (R5), antes das demais tarefas

Subir a aplicação em Development, com a documentação ligada pela variável `Documentation__Enabled=true`, e abrir `http://localhost:5016/api/docs`.

```bash
Documentation__Enabled=true ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Encurtador.Api
```

Passos:
1. Em "Create Link", enviar uma URL válida. Esperado: `201` com a chave de gerenciamento.
2. Em `RedirectLink`, usar o código recebido. Esperado: a interface mostra `302`, o cabeçalho `Location` e não navega para o destino.

Se o passo 2 falhar, parar e voltar ao usuário, conforme o plano B de R5. Não ocultar a operação.

## 3. Documentação ligada em desenvolvimento

Em Development a documentação já fica ligada por padrão. Sem a variável, o comando da seção 2 também serve.

```bash
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5016/api/openapi/v1.json   # esperado: 200
curl -s -o /dev/null -w "%{http_code}\n" http://localhost:5016/api/docs               # esperado: 200
```

Conferir no JSON que estão as três operações (`createLink`, `getLinkStats`, `redirectLink`), com `X-Management-Key` como parâmetro de cabeçalho e sem `securitySchemes`.

## 4. Documentação desligada (produção sem configuração)

```bash
ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/Encurtador.Api --urls http://localhost:5017
```

Comparar, com a mesma forma de resposta:

```bash
for p in /api/docs /api/openapi/v1.json /api/rota-inexistente; do
  curl -s -i http://localhost:5017$p | sed -n '1p;/^Content-Type/p' ; curl -s http://localhost:5017$p | wc -c
done
```

Esperado: os três retornam o mesmo status e o mesmo corpo (vazio), idênticos ao de rota inexistente. A verificação automatizada compara byte a byte.

## 5. Ativação explícita em produção

```bash
ASPNETCORE_ENVIRONMENT=Production Documentation__Enabled=true dotnet run --project src/Encurtador.Api --urls http://localhost:5017
```

Esperado: `/api/docs` e `/api/openapi/v1.json` respondem 200.

## 6. Endpoints existentes sem mudança

```bash
dotnet test Encurtador.slnx --no-build
```

Esperado: todos os testes atuais passam sem alteração nos próprios testes. Os novos testes em `ApiDocumentationTests` passam.

## 7. Abrir a documentação não tem efeito colateral (REQ-029, SC-006)

Criar um link, abrir `/api/docs` e `/api/openapi/v1.json`, depois consultar as estatísticas com a chave. Esperado: `ClickCount` igual a `0`.

## 8. Chave real fora da documentação (REQ-026)

```bash
curl -s http://localhost:5016/api/openapi/v1.json | grep -E 'ManagementKey|[A-Za-z0-9_-]{43}'
```

Esperado: nenhuma ocorrência de chave real. Os exemplos usam valores fictícios evidentes.

## 9. Textos em português revisados (REQ-025)

Revisar os resumos, descrições e exemplos anotados em T011 a T015 e marcar cada item revisado:

- [ ] `CreateLink`: resumo e descrição
- [ ] `GetLinkStats`: resumo, descrição e cabeçalho `X-Management-Key`
- [ ] `RedirectLink`: resumo e descrição
- [ ] Exemplos de `CreateLinkRequest`, `CreatedLinkResponse` e `LinkStatsResponse`

Rótulos fixos da interface Scalar podem permanecer em inglês (REQ-025).

## Critérios de aceite do guia

| Verificação | Requisito |
|-------------|-----------|
| Passo 2 | REQ-021, REQ-028 |
| Passos 3 e 5 | REQ-019, REQ-022, REQ-023 |
| T019b (teste de integração) | REQ-022 |
| Passo 4 | REQ-023, SC-004 |
| Passo 6 | SC-005 e preservação dos endpoints |
| Passo 7 | REQ-029, SC-006 |
| Passo 8 | REQ-026 |
| T006b (teste de integração) | REQ-021 |
| T009 e T009b (testes de integração) | REQ-020, REQ-024, SC-003 |
| T023 (teste de integração) | REQ-026 |
