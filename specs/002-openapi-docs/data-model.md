# Modelo de Dados: Documentação OpenAPI

**Branch**: `002-openapi-docs` | **Plano**: [plan.md](./plan.md)

Não se aplica. A especificação declara "Entidades-Chave: não se aplica", porque a funcionalidade descreve a API existente e não cria nem altera dados. Os modelos de requisição e resposta (`CreateLinkRequest`, `CreatedLinkResponse`, `LinkStatsResponse`) já existem na camada `Api` e são apenas refletidos no documento OpenAPI. Os esquemas de erro seguem o `ProblemDetails` padrão, sem tipo próprio.

Única configuração nova, sem persistência:

| Chave | Tipo | Padrão | Ambiente |
|-------|------|--------|----------|
| `Documentation:Enabled` | `bool?` | `true` em Development, `false` nos demais | Opcional. Nenhum arquivo versionado define a chave. Quando presente, sobrescreve o padrão nos dois sentidos |
