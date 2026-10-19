<img width="100%" alt="Halloween 2026" src="https://baltaio.blob.core.windows.net/static/images/v4/challenges/halloween-2026/banner.jpg" />

## 🎃 Halloween - Desafio 3

Oi, eu sou o Fabio Rezende e este é o espaço onde compartilho minha jornada de aprendizado durante o desafio **Halloween 2026**, realizado pelo [balta.io](https://balta.io). 👻

Aqui você vai encontrar projetos, exercícios e códigos que estou desenvolvendo durante o desafio.

### Sobre este desafio
Neste desafio o objetivo é criar um encurtador de URLs completo utilizando os conceitos de Spec-Driven Development via SpecKit

#### O que foi implementado
- **Criação de links** (`POST /api/links`): gera código automático de 7 caracteres ou aceita código personalizado de 4 a 20 caracteres alfanuméricos, com validação de URL `http`/`https` (até 2048 caracteres) e data de expiração opcional.
- **Redirecionamento** (`GET /{code}`): redireciona com `302` para o destino e incrementa o contador de acessos em exatamente 1. Links expirados e códigos inexistentes retornam mensagem explicativa, sem alterar contador.
- **Consulta de acessos** (`GET /api/links/{code}`): o criador consulta o total apresentando a chave secreta de gerenciamento recebida na criação. A chave é guardada somente como hash SHA-256.
- **Unicidade e concorrência**: códigos são únicos sem diferenciar maiúsculas de minúsculas, garantidos por índice único no banco. O contador é incrementado com `UPDATE ... SET ClickCount = ClickCount + 1`, sem leitura prévia, para não perder acessos simultâneos.
- **Proteções**: limite de criação por endereço de origem, lista de palavras reservadas (como `api` e `health`) e respostas de erro no formato `ProblemDetails`, em português.
- **Persistência** com EF Core e SQLite: links e contadores sobrevivem a reinícios da aplicação.
- **Testes**: 61 testes unitários (xUnit) e 30 de integração, incluindo testes de acessos concorrentes e de persistência após reinício. Ambos passando.

Tecnologias: C# 14, .NET 10, ASP.NET Core Minimal APIs, EF Core 10 com SQLite e xUnit.

Neste processo eu aprendi:
* ✅ Escrever a especificação antes do código, com requisitos numerados e critérios de aceitação testáveis.
* ✅ Usar as perguntas de clarificação para resolver ambiguidades antes do planejamento, como a forma de provar a posse de um link sem login.
* ✅ Quebrar o plano em tarefas ordenadas por dependência e por história de usuário, com testes escritos antes da implementação.
* ✅ Usar a constituição do projeto como critério de revisão, verificando o plano e as tarefas contra os princípios definidos.
* ✅ Deixar regras de concorrência com a garantia no banco (índice único e atualização atômica), e não apenas na aplicação.

## Bagde
<img src="https://baltaio.blob.core.windows.net/static/images/v4/challenges/halloween-2026/03.png" width="200" />

## Problema
Encurtar URLs longas em links curtos fáceis de compartilhar e permitir que o criador acompanhe quantas vezes o link foi acessado. Não há cadastro nem login: o controle de posse do link é feito por uma chave secreta entregue uma única vez na criação. Por isso, se a chave for perdida, não há recuperação.

## Sobre o Halloween 2026
O desafio **Halloween 2026** consiste em implementar implementar o modelo Spec Driven Development de ponta a ponta, criando apps completas com IA.

### Veja meu progresso no desafio
[Incluir link para o repositório central]
