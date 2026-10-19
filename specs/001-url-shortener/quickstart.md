# Guia de Validação Rápida: Encurtador de URLs

Roteiro para subir o serviço e provar, de ponta a ponta, que a funcionalidade funciona. Cada passo aponta para o requisito que valida. Contratos: [contracts/openapi.yaml](./contracts/openapi.yaml). Modelo: [data-model.md](./data-model.md).

## Pré-requisitos

- .NET SDK 10.0.x (`dotnet --version` deve começar com `10.`).
- `curl` e `jq`, para os exemplos.
- Nenhum servidor de banco. O SQLite é um arquivo local criado na primeira execução.
- Opcional, para aplicar migrações manualmente: `dotnet tool install --global dotnet-ef --version 10.0.*`.

## 1. Restaurar, compilar e testar

Na raiz do repositório:

```bash
dotnet restore
dotnet build
dotnet test tests/Encurtador.UnitTests
dotnet test tests/Encurtador.IntegrationTests
```

Resultado esperado: os dois projetos de teste passam. Os testes unitários não sobem HTTP nem banco. Os de integração usam arquivo SQLite temporário.

## 2. Subir o serviço

```bash
dotnet run --project src/Encurtador.Api
```

Na primeira execução, as migrações são aplicadas e o arquivo de banco é criado em `Database:ConnectionString` (ver `appsettings.json`). Anote a URL exibida, por exemplo `http://localhost:5000`. Os exemplos abaixo usam `BASE=http://localhost:5000`.

```bash
export BASE=http://localhost:5000
```

## 3. Criar link com código gerado (US1, REQ-001, REQ-006, REQ-013)

```bash
curl -s -X POST "$BASE/api/links" \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"https://exemplo.com/pagina-longa"}' | tee /tmp/criacao.json | jq
```

Resultado esperado:
- Status `201` com `Location`.
- `code` com 7 caracteres, somente do alfabeto `23456789abcdefghjkmnpqrstuvwxyz`.
- `expiresAt` nulo.
- `managementKey` com 43 caracteres.

Guarde o código e a chave:

```bash
export CODE=$(jq -r .code /tmp/criacao.json)
export KEY=$(jq -r .managementKey /tmp/criacao.json)
```

> Em produção, não use `/tmp` para guardar a chave. Este exemplo é só para validação local.

## 4. Criar link com código personalizado (US1, REQ-002, REQ-003)

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST "$BASE/api/links" \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"https://exemplo.com/outra","customCode":"minhapagina"}'
```

Resultado esperado: `201`.

Repita o mesmo comando. Resultado esperado: `400` com `application/problem+json` e mensagem de código já em uso. O link existente não muda.

Variação com maiúsculas, que deve ser tratada como o mesmo código (REQ-004):

```bash
curl -s -X POST "$BASE/api/links" \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"https://exemplo.com/x","customCode":"MinhaPagina"}'
```

Resultado esperado: `400`, código já em uso.

## 5. Redirecionamento e contagem (US2, REQ-008, REQ-009, REQ-016)

```bash
curl -s -o /dev/null -D - "$BASE/$CODE" | grep -iE '^(HTTP|location|cache-control)'
```

Resultado esperado:
- `HTTP/1.1 302`
- `Location: https://exemplo.com/pagina-longa`
- `Cache-Control: no-store`

Acesse três vezes:

```bash
for i in 1 2 3; do curl -s -o /dev/null "$BASE/$CODE"; done
```

## 6. Consultar acessos (US3, REQ-013)

```bash
curl -s "$BASE/api/links/$CODE" -H "X-Management-Key: $KEY" | jq
```

Resultado esperado: `200`, com `clickCount` igual a 4 (um acesso no passo 5 e três neste passo).

Sem chave ou com chave em formato inválido, espere `400`:

```bash
curl -s -o /dev/null -w '%{http_code}\n' "$BASE/api/links/$CODE"
```

Com chave bem formada que não confere, espere `401`:

```bash
curl -s -o /dev/null -w '%{http_code}\n' "$BASE/api/links/$CODE" \
  -H 'X-Management-Key: AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA'
```

## 7. Expiração (US1 cenário 7, US2 cenário 3, REQ-006, REQ-007, REQ-010)

Data no passado, deve ser recusada com `400`:

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST "$BASE/api/links" \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"https://exemplo.com/x","expiresAt":"2020-01-01T00:00:00Z"}'
```

Data futura, próxima, para observar a expiração:

```bash
EXP=$(date -u -d '+1 minute' +%Y-%m-%dT%H:%M:%SZ)
curl -s -X POST "$BASE/api/links" \
  -H 'Content-Type: application/json' \
  -d "{\"targetUrl\":\"https://exemplo.com/temporario\",\"expiresAt\":\"$EXP\"}" | tee /tmp/temp.json | jq -r .code
```

Espere um minuto e acesse o código. Resultado esperado: `410 Gone`, sem redirecionamento. Consultar o total com a chave ainda deve funcionar e mostrar o mesmo total de antes da expiração (US3 cenário 2).

## 8. Código inexistente e palavra reservada (US2 cenário 4, REQ-011, REQ-018)

```bash
curl -s -o /dev/null -w '%{http_code}\n' "$BASE/zzzzzzzz"
```

Resultado esperado: `404`.

```bash
curl -s -o /dev/null -w '%{http_code}\n' -X POST "$BASE/api/links" \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"https://exemplo.com/x","customCode":"Health"}'
```

Resultado esperado: `400`, código reservado.

## 9. Persistência após reinício (REQ-014, SC-003)

1. Anote o `clickCount` atual do link:
   ```bash
   curl -s "$BASE/api/links/$CODE" -H "X-Management-Key: $KEY" | jq .clickCount
   ```
2. Encerre o serviço (`Ctrl+C`) e suba de novo com `dotnet run --project src/Encurtador.Api`.
3. Acesse o link e consulte de novo. O código, o destino e o total devem ser os mesmos de antes, somados ao acesso feito agora.

## 10. Validação de entrada (Princípio VI)

Cada um destes deve responder `400` com `application/problem+json`:

```bash
# URL sem esquema http/https
curl -s -X POST "$BASE/api/links" -H 'Content-Type: application/json' -d '{"targetUrl":"ftp://exemplo.com"}'

# URL com mais de 2048 caracteres
LONGA=$(printf 'https://exemplo.com/%*s' 2100 '' | tr ' ' a)
curl -s -X POST "$BASE/api/links" -H 'Content-Type: application/json' -d "{\"targetUrl\":\"$LONGA\"}"

# Código curto demais e código com símbolo
curl -s -X POST "$BASE/api/links" -H 'Content-Type: application/json' -d '{"targetUrl":"https://exemplo.com","customCode":"abc"}'
curl -s -X POST "$BASE/api/links" -H 'Content-Type: application/json' -d '{"targetUrl":"https://exemplo.com","customCode":"abc-123"}'
```

Confira o header `Content-Type` da resposta: deve ser `application/problem+json`.

## 11. Limite de criações (REQ-017)

Com o limite padrão de 20 por hora por origem, a 21ª criação seguida deve responder `429`:

```bash
for i in $(seq 1 21); do
  curl -s -o /dev/null -w '%{http_code} ' -X POST "$BASE/api/links" \
    -H 'Content-Type: application/json' -d '{"targetUrl":"https://exemplo.com/limite"}'
done; echo
```

Resultado esperado: vinte `201` e um `429` no final. Nenhum link extra é criado. Para testar mais rápido, reduza `Links:CreationLimitPerHour` em `appsettings.Development.json`.

## 12. Concorrência (REQ-015, SC-004)

Os testes de integração `ConcurrentAccessTests` cobrem 100 acessos simultâneos, e `ConcurrentCreationTests` cobre criações simultâneas com o mesmo código. Para repetir manualmente:

```bash
for i in $(seq 1 100); do curl -s -o /dev/null "$BASE/$CODE" & done; wait
curl -s "$BASE/api/links/$CODE" -H "X-Management-Key: $KEY" | jq .clickCount
```

Resultado esperado: o total aumentou exatamente 100 em relação ao valor anterior.

## 13. Tempo de criação (SC-001)

Medir o tempo entre o envio da URL e a resposta com o link curto. Resultado esperado: menos de 30 segundos. Na prática leva milissegundos, mas o critério é medido aqui.

```bash
time curl -s -o /dev/null -w '%{http_code}\n' -X POST "$BASE/api/links" \
  -H 'Content-Type: application/json' \
  -d '{"targetUrl":"https://exemplo.com/medicao"}'
```

Resultado esperado: status `201` e tempo `real` abaixo de 30 segundos.

## Critérios de aceite

- [ ] `dotnet test` passa nos dois projetos de teste.
- [ ] Criação automática e personalizada funcionam conforme os passos 3 e 4.
- [ ] Redirecionamento é `302` com `no-store` e incrementa o contador em exatamente 1 (passo 5).
- [ ] Consulta exige chave e retorna o total correto (passo 6).
- [ ] Expiração bloqueia redirecionamento sem alterar o contador (passo 7).
- [ ] Dados sobrevivem ao reinício (passo 9).
- [ ] Toda entrada inválida retorna `400` com ProblemDetails (passo 10).
- [ ] Criação de link leva menos de 30 segundos (passo 13, SC-001).
