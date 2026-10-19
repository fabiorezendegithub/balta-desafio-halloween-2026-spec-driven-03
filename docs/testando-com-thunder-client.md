# Testando o Encurtador com o Thunder Client

Este guia mostra como testar a API do encurtador de URLs usando a extensão **Thunder Client** dentro do Visual Studio Code. Não é preciso usar `curl` nem linha de comando para as requisições; tudo é feito pela interface da extensão.

## 1. Pré-requisitos

- Visual Studio Code instalado.
- .NET SDK 10.0.x (`dotnet --version` deve começar com `10.`).
- Extensão **Thunder Client** instalada (ícone de raio na barra lateral do VS Code).

Para instalar a extensão: abra a aba de extensões (`Ctrl+Shift+X`), pesquise por `Thunder Client` e clique em **Install**. Depois, clique no ícone de raio na barra lateral.

## 2. Subir a aplicação

Na raiz do repositório, rode:

```bash
dotnet run --project src/Encurtador.Api
```

Na primeira execução, o banco SQLite (`encurtador.db`) é criado automaticamente com as migrações aplicadas.

Anote a URL exibida no terminal. Com o perfil padrão, ela é `http://localhost:5016`. Os exemplos deste guia usam essa URL.

> **Atenção:** o campo `shortUrl` da resposta de criação usa `Links:PublicBaseUrl` do `appsettings.json` (`http://localhost:5000`). Para que o link curto retornado funcione ao ser clicado, suba a aplicação na mesma porta:
>
> ```bash
> dotnet run --project src/Encurtador.Api --urls http://localhost:5000
> ```
>
> Se preferir manter a porta 5016, basta trocar `{{baseUrl}}` por `http://localhost:5016` nos testes de redirecionamento, ou usar apenas o campo `code` da resposta.

## 3. Criar uma coleção e um ambiente

Organizar as requisições em uma coleção facilita repetir os testes.

1. Na aba **Collections**, clique em **New Collection** e dê o nome `Encurtador`.
2. Na aba **Env**, clique em **New Env** e crie um ambiente chamado `Local` com as variáveis abaixo:

| Variável  | Valor inicial                | Para que serve                                   |
|-----------|------------------------------|--------------------------------------------------|
| `baseUrl` | `http://localhost:5000`      | Endereço da API                                  |
| `code`    | *(vazio)*                    | Código do link criado (preenchido nos testes)    |
| `key`     | *(vazio)*                    | Chave de gerenciamento (preenchida nos testes)   |

3. Selecione o ambiente `Local` no seletor de ambientes, no canto superior direito da extensão.

Nas requisições, use as variáveis com a sintaxe `{{nome}}`. Por exemplo, `{{baseUrl}}/api/links`.

## 4. Criar link com código gerado (US1)

1. Clique em **New Request** dentro da coleção `Encurtador`.
2. Método `POST`, URL: `{{baseUrl}}/api/links`.
3. Aba **Body**, selecione **JSON** e informe:

   ```json
   {
     "targetUrl": "https://exemplo.com/pagina-longa"
   }
   ```

4. Clique em **Send** (ou `Ctrl+Enter`).

**Resultado esperado:**
- Status `201 Created`.
- Header `Location` com a URL do link curto.
- Corpo com `code` de 7 caracteres, `shortUrl`, `targetUrl`, `createdAt`, `expiresAt` nulo e `managementKey` de 43 caracteres.

**Guarde o código e a chave.** Copie os valores de `code` e `managementKey` da resposta e cole no ambiente `Local`, nas variáveis `code` e `key`.

> A `managementKey` é mostrada **somente nesta resposta**. Se for perdida, não há como recuperá-la; será preciso criar outro link.

## 5. Criar link com código personalizado (US1)

Crie uma nova requisição `POST` para `{{baseUrl}}/api/links` com o corpo:

```json
{
  "targetUrl": "https://exemplo.com/outra",
  "customCode": "minhapagina"
}
```

**Resultado esperado:** `201 Created`, com `code` igual a `minhapagina`.

Clique em **Send** de novo, sem alterar nada.

**Resultado esperado:** `400 Bad Request`, com `Content-Type: application/problem+json` e mensagem de código já em uso. O link existente não muda.

Depois, teste a variação com maiúsculas. Os códigos não diferenciam maiúsculas de minúsculas (REQ-004):

```json
{
  "targetUrl": "https://exemplo.com/x",
  "customCode": "MinhaPagina"
}
```

**Resultado esperado:** `400`, código já em uso.

## 6. Redirecionamento e contagem (US2)

Crie uma requisição `GET` para `{{baseUrl}}/{{code}}`.

**Atenção:** por padrão, o Thunder Client segue redirecionamentos. Para ver o `302` em si, abra a configuração da requisição (ícone de engrenagem ou aba **Settings**) e desative **Follow redirects**.

**Resultado esperado:**
- Status `302 Found`.
- Header `Location: https://exemplo.com/pagina-longa`.
- Header `Cache-Control: no-store`.

Envie a mesma requisição três vezes. Cada envio é um acesso.

## 7. Consultar a contagem de acessos (US3)

Crie uma requisição `GET` para `{{baseUrl}}/api/links/{{code}}`.

Na aba **Headers**, adicione:

| Nome               | Valor     |
|--------------------|-----------|
| `X-Management-Key` | `{{key}}` |

**Resultado esperado:** `200 OK`, com `clickCount` igual ao número de acessos válidos feitos ao link. Se você fez um acesso na seção 6 e mais três envios, o valor deve ser `4`.

Em resumo: o `clickCount` deve ser igual ao número de acessos válidos feitos ao link até aquele momento.

### Casos de erro da consulta

| Cenário                                   | Como testar                                       | Resultado esperado |
|-------------------------------------------|---------------------------------------------------|--------------------|
| Sem header `X-Management-Key`             | Remova o header e envie                           | `400`              |
| Chave em formato inválido                 | Use `X-Management-Key: abc`                       | `400`              |
| Chave bem formada, mas errada             | Use `X-Management-Key: AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA` | `401`  |
| Código que não existe                     | Troque a URL por `{{baseUrl}}/api/links/zzzzzzzz` | `404`              |

## 8. Expiração (US1 e US2)

### 8.1 Data no passado é recusada

Crie uma requisição `POST` para `{{baseUrl}}/api/links`:

```json
{
  "targetUrl": "https://exemplo.com/x",
  "expiresAt": "2020-01-01T00:00:00Z"
}
```

**Resultado esperado:** `400`.

### 8.2 Link que expira em um minuto

Use uma data de expiração um minuto à frente. Para gerar o valor, use um horário UTC atual e some um minuto. Por exemplo, se agora são `2026-10-19T20:00:00Z`, use `2026-10-19T20:01:00Z`.

```json
{
  "targetUrl": "https://exemplo.com/temporario",
  "expiresAt": "2026-10-19T20:01:00Z"
}
```

Troque a data pelo horário de quando você está testando. Guarde o `code` e a `managementKey` desta resposta.

Espere passar o horário de expiração e acesse `{{baseUrl}}/{codigo}`.

**Resultado esperado:** `410 Gone`, sem redirecionamento, com a mensagem de link expirado.

Consulte a contagem com a chave desse link. Ela deve mostrar o mesmo total de antes da expiração.

## 9. Código inexistente e palavra reservada

**Código inexistente:** `GET {{baseUrl}}/zzzzzzzz`

**Resultado esperado:** `404`, sem alterar nenhum contador.

**Palavra reservada:** `POST {{baseUrl}}/api/links`

```json
{
  "targetUrl": "https://exemplo.com/x",
  "customCode": "Health"
}
```

**Resultado esperado:** `400`, com mensagem de código reservado (a comparação ignora maiúsculas).

## 10. Validação de entrada

Crie requisições `POST` para `{{baseUrl}}/api/links`. Cada uma deve responder `400` com `application/problem+json`.

| Corpo                                                                 | Motivo                              |
|-----------------------------------------------------------------------|-------------------------------------|
| `{"targetUrl": "ftp://exemplo.com"}`                                  | Esquema diferente de http/https     |
| `{"targetUrl": "https://exemplo.com", "customCode": "abc"}`           | Código com menos de 4 caracteres    |
| `{"targetUrl": "https://exemplo.com", "customCode": "abc-123"}`       | Código com símbolo                  |

Para testar uma URL com mais de 2048 caracteres, cole no campo `targetUrl` uma URL longa, por exemplo `https://exemplo.com/` seguido de 2100 letras `a`.

## 11. Limite de criações

O limite padrão é de **20 criações por hora** por origem. A 21ª criação seguida deve responder `429 Too Many Requests`.

Para testar sem enviar 21 requisições manualmente, use o **Runner** do Thunder Client: selecione a requisição de criação, clique no ícone de execução da coleção e ajuste o número de iterações para 21.

> Para testar mais rápido, reduza `Links:CreationLimitPerHour` no `appsettings.Development.json` e reinicie a aplicação.

## 12. Persistência após reinício

1. Consulte um link com `GET {{baseUrl}}/api/links/{{code}}` e anote o `clickCount`.
2. Pare a aplicação (`Ctrl+C` no terminal) e suba de novo com `dotnet run --project src/Encurtador.Api`.
3. Repita a consulta.

**Resultado esperado:** o `code`, o `targetUrl` e o `clickCount` são os mesmos de antes.

## 13. Resumo dos status esperados

| Status | Quando acontece                                              |
|--------|--------------------------------------------------------------|
| `201`  | Link criado                                                  |
| `302`  | Redirecionamento de link ativo                               |
| `200`  | Consulta de contagem com chave válida                        |
| `400`  | Dados inválidos, código em uso, código reservado, chave ausente ou malformada |
| `401`  | Chave bem formada que não corresponde ao link                |
| `404`  | Código ou link inexistente                                   |
| `410`  | Link expirado                                                |
| `429`  | Limite de criações por hora excedido                         |

Erros são retornados no formato `application/problem+json`, com `title`, `status` e `detail` em português.

## 14. Dicas do Thunder Client

- **Ctrl+Enter** envia a requisição aberta.
- Aba **Response** mostra o corpo e os headers; aba **Timeline** mostra o tempo de resposta.
- Para salvar a coleção no projeto, exporte-a pelo menu da coleção. Como a pasta `.vscode/` está no `.gitignore`, evite colocar a coleção nela se quiser versioná-la; salve, por exemplo, em `docs/`.
- Não versione o ambiente com valores de `key` reais. Chaves de gerenciamento são segredos.
