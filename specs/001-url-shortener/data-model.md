# Modelo de Dados: Encurtador de URLs

Fonte: [spec.md](./spec.md), seção "Entidades-Chave". Decisões de persistência: [plan.md](./plan.md) (D1 a D5, D9) e [research.md](./research.md) (R2, R3, R5).

## Entidade: Link

Representa um link curto criado. Uma linha por criação. Duas criações da mesma URL de destino geram duas linhas.

| Campo | Tipo lógico | Obrigatório | Regra |
|-------|-------------|-------------|-------|
| `Id` | inteiro longo, chave primária gerada pelo banco | sim | Interno. Não aparece na API. |
| `Code` | texto, até 20 caracteres | sim | Código como escrito pelo criador, ou gerado. Usado apenas para exibição. |
| `CodeKey` | texto, até 20 caracteres | sim | `Code` em minúsculas. **Índice único.** Usado para unicidade e busca (REQ-003, REQ-004). |
| `TargetUrl` | texto, até 2048 caracteres | sim | Começa com `http://` ou `https://` (REQ-005). Imutável nesta versão (spec, Fora de escopo). |
| `CreatedAtUtc` | data e hora com offset, normalizada para UTC | sim | Definida na criação, via `TimeProvider`. |
| `ExpiresAtUtc` | data e hora com offset, normalizada para UTC | não | Nulo significa que o link não expira (REQ-006). Quando informada, deve ser posterior a `CreatedAtUtc` no momento da criação (REQ-007). |
| `ClickCount` | inteiro longo | sim | Padrão 0. Só cresce, por `UPDATE ... + 1` (REQ-009, REQ-012). Restrição de banco: `ClickCount >= 0`. |
| `ManagementKeyHash` | texto, 64 caracteres | sim | SHA-256 em hexadecimal da chave de gerenciamento (REQ-013). A chave em texto puro nunca é gravada. |

### Índices e restrições

- `UQ_Link_CodeKey`: índice único em `CodeKey`. Garante unicidade sob concorrência (D1).
- `CK_Link_ClickCount_NonNegative`: `ClickCount >= 0`. Complementa REQ-012.
- Não há índice em `ExpiresAtUtc`. Expiração é verificada na mesma busca por `CodeKey`, então um índice extra não ajuda.

### Regras de validação (no `LinkService`)

| Regra | Requisito | Momento |
|-------|-----------|---------|
| URL com esquema `http` ou `https`, até 2048 caracteres | REQ-005 | criação |
| Código personalizado com 4 a 20 caracteres, só letras e números | REQ-002 | criação, quando informado |
| Código personalizado fora da lista de palavras reservadas, sem diferenciar maiúsculas | REQ-018 | criação, quando informado |
| Código inexistente (`CodeKey` livre) | REQ-003 | criação, pelo índice único |
| Data de expiração posterior ao instante atual | REQ-007 | criação, quando informada |
| Limite de criações por origem não excedido | REQ-017 | antes de qualquer validação de conteúdo |

### Estados

Não há estado armazenado. "Ativo" e "expirado" são derivados:

```text
ExpiresAtUtc IS NULL                -> ativo, sem expiração
ExpiresAtUtc > agora (UTC)          -> ativo
ExpiresAtUtc <= agora (UTC)         -> expirado (permanente: nunca volta a ativo)
```

A fronteira segue a spec: no instante exato de `ExpiresAtUtc`, o link já é expirado. A condição de ativo é `ExpiresAtUtc > agora`.

Transições possíveis: nenhuma escrita altera o estado. Um link expirado continua existindo e consultável (Premissas).

### Operações de escrita

| Operação | Como | Garantia |
|----------|------|----------|
| Criar | `INSERT` com tentativa direta | Violação de `UQ_Link_CodeKey` vira "código em uso" (D1) |
| Registrar acesso | `UPDATE ... SET ClickCount = ClickCount + 1 WHERE CodeKey = @k AND (ExpiresAtUtc IS NULL OR ExpiresAtUtc > @agora)` | Atômica. Número de linhas afetadas `0` indica não encontrado ou expirado (D2, R3) |

Nenhuma operação lê `ClickCount` para reescrevê-lo. Nenhuma operação remove linhas.

## Entidade: Acesso

Não é persistida. Cada acesso válido é apenas um incremento em `Link.ClickCount` (spec, Entidades-Chave). Acessos a links expirados ou inexistentes não geram registro.

## Dados retornados ao criador

Não são persistidos. Existem só na resposta da criação e da consulta.

| Dado | Quando | Origem |
|------|--------|--------|
| Chave de gerenciamento em texto (base64url, 43 caracteres) | somente na criação | gerada, retornada uma vez, guardada só como hash |

## Relacionamentos

Nenhum. Um `Link` é independente. Não há usuário, cadastro ou login (Premissas).
