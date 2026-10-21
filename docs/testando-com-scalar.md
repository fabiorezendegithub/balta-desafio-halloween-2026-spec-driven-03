# Testando o Encurtador com o Scalar

Este guia mostra como testar a API do encurtador usando a interface Scalar, gerada a partir do documento OpenAPI da aplicação.

## 1. Pré-requisitos

- .NET SDK 10.0.x (`dotnet --version` deve começar com `10.`).
- Um navegador.
- O projeto restaurado e compilável.

A documentação fica habilitada automaticamente quando a aplicação roda no ambiente `Development`.

## 2. Subir a aplicação

Na raiz do repositório, execute:

```bash
dotnet run --project src/Encurtador.Api
```

Use o endereço exibido no terminal. Com o perfil HTTP padrão, ele é `http://localhost:5016`.

O banco SQLite (`encurtador.db`) e suas migrações são preparados automaticamente na inicialização.

Abra no navegador:

```text
http://localhost:5016/api/docs/
```

Também estão disponíveis:

| Recurso | URL |
|---|---|
| Interface Scalar | `http://localhost:5016/api/docs/` |
| Documento OpenAPI | `http://localhost:5016/api/openapi/v1.json` |

`/api/docs` sem a barra final redireciona para `/api/docs/`.

> O valor de `shortUrl` é montado com `Links:PublicBaseUrl`, que por padrão é `http://localhost:5000`. Para que esse valor funcione diretamente no navegador, suba a aplicação nessa porta:
>
> ```bash
> dotnet run --project src/Encurtador.Api --urls http://localhost:5000
> ```
>
> Se usar a porta `5016`, teste o redirecionamento pela própria URL da API e use o campo `code` retornado pela criação.

## 3. Como usar o Scalar

1. Abra a interface em `/api/docs/`.
2. Escolha uma operação, como `POST /api/links`.
3. Clique em **Try it** ou **Test Request**.
4. Preencha os parâmetros e o corpo JSON quando solicitado.
5. Clique em **Send Request**.
6. Confira o status, os cabeçalhos e o corpo da resposta na própria operação.

O Scalar carrega o documento em `/api/openapi/v1.json`; não é necessário importar uma coleção ou configurar o Thunder Client.

## 4. Criar um link

Na operação `POST /api/links`, envie:

```json
{
  "targetUrl": "https://exemplo.com/pagina-longa"
}
```

Resultado esperado:

- status `201 Created`;
- cabeçalho `Location` com a URL curta;
- `code` com 7 caracteres;
- `shortUrl`, `targetUrl`, `createdAt` e `managementKey` na resposta;
- `expiresAt` igual a `null` quando não houver expiração.

Guarde o `code` e a `managementKey`. A chave é exibida somente na resposta de criação.

Para testar um código personalizado, envie:

```json
{
  "targetUrl": "https://exemplo.com/outra",
  "customCode": "minhapagina"
}
```

O resultado deve ser `201`, com `code` igual a `minhapagina`. Repetir a mesma requisição deve retornar `400`, pois o código já está em uso.

## 5. Testar o redirecionamento

Na operação `GET /{code}`, informe o código obtido na criação e envie a requisição.

Para inspecionar o redirecionamento em vez de abrir o destino, desative no Scalar a opção de seguir redirecionamentos, quando ela estiver disponível na configuração da requisição.

Resultado esperado:

- status `302 Found`;
- cabeçalho `Location` apontando para `targetUrl`;
- cabeçalho `Cache-Control: no-store`.

Envie a operação algumas vezes para gerar acessos. Cada resposta `302` para um link ativo incrementa o contador em um.

## 6. Consultar estatísticas

Na operação `GET /api/links/{code}`:

1. informe o mesmo `code`;
2. abra a seção de cabeçalhos;
3. adicione `X-Management-Key` com a `managementKey` guardada;
4. envie a requisição.

Resultado esperado: `200 OK`, com `clickCount` igual ao número de acessos válidos realizados.

Teste também os erros:

| Teste | Resultado esperado |
|---|---|
| remover `X-Management-Key` | `400 Bad Request` |
| usar uma chave malformada, como `abc` | `400 Bad Request` |
| usar uma chave bem formada, mas incorreta | `401 Unauthorized` |
| informar um código inexistente | `404 Not Found` |

## 7. Testar expiração

Crie um link com uma data futura próxima:

```json
{
  "targetUrl": "https://exemplo.com/temporario",
  "expiresAt": "2026-10-20T23:59:00Z"
}
```

Substitua o valor por um horário UTC futuro no momento do teste. Depois que a data passar, use `GET /{code}`.

Resultado esperado: `410 Gone`, sem redirecionamento. A consulta de estatísticas continua disponível com a chave do link e não deve aumentar o contador após a expiração.

Uma data no passado deve ser rejeitada com `400 Bad Request`.

## 8. Validações e limite de criações

Na operação `POST /api/links`, os exemplos abaixo devem retornar `400 Bad Request`:

```json
{ "targetUrl": "ftp://exemplo.com" }
```

```json
{ "targetUrl": "https://exemplo.com", "customCode": "abc" }
```

```json
{ "targetUrl": "https://exemplo.com", "customCode": "abc-123" }
```

O limite padrão é de 20 criações por hora por origem. A criação seguinte deve retornar `429 Too Many Requests`.

## 9. Status esperados

| Status | Situação |
|---|---|
| `201` | link criado |
| `200` | estatísticas consultadas com chave válida |
| `302` | link ativo redirecionado |
| `400` | entrada inválida, código em uso ou chave ausente/malformada |
| `401` | chave válida no formato, mas incorreta |
| `404` | código inexistente |
| `410` | link expirado |
| `429` | limite de criações excedido |

As respostas de erro usam `application/problem+json`, com `title`, `status` e `detail` em português.

## 10. Desligar ou habilitar a documentação explicitamente

Em produção, a documentação fica desligada por padrão:

```bash
ASPNETCORE_ENVIRONMENT=Production dotnet run --project src/Encurtador.Api --urls http://localhost:5017
```

Para habilitá-la explicitamente:

```bash
ASPNETCORE_ENVIRONMENT=Production Documentation__Enabled=true dotnet run --project src/Encurtador.Api --urls http://localhost:5017
```

Nesse caso, abra `http://localhost:5017/api/docs/`.

## 11. Persistência

Depois de criar um link e registrar acessos:

1. consulte as estatísticas e anote `clickCount`;
2. pare a aplicação com `Ctrl+C`;
3. inicie-a novamente;
4. consulte as estatísticas usando a mesma chave.

O `code`, o destino e o contador devem permanecer iguais.
