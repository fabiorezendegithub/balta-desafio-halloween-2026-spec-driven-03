# Especificação de Funcionalidade: Documentação OpenAPI da API

**Diretório da funcionalidade**: `specs/002-openapi-docs`

**Criada em**: 2026-10-20

**Status**: Rascunho

**Entrada**: Descrição do usuário: "Adicionar documentação OpenAPI à Minimal API existente, permitindo que desenvolvedores visualizem os endpoints disponíveis, consultem seus contratos de requisição e resposta e executem chamadas HTTP por meio de uma interface web interativa."

## Clarifications

### Session 2026-10-20

- Q: Em quais ambientes a interface de documentação deve ficar ligada por padrão, além do ambiente local do desenvolvedor? → A: Só o ambiente local de desenvolvimento.
- Q: Os textos fixos da própria interface de documentação (botões e rótulos vindos da ferramenta) também devem ficar em português? → A: Não; só o conteúdo escrito pela equipe fica em português, e os rótulos fixos da ferramenta podem ficar em inglês.
- Q: Quando a documentação está desligada, qual resposta a aplicação deve dar ao acessar o caminho dela? → A: 404 idêntico ao de uma rota inexistente, com o mesmo corpo.
- Q: Quais cabeçalhos de resposta a interface deve mostrar depois que o desenvolvedor executa uma chamada? → A: Tipo do conteúdo e localização (quando houver) em destaque, com a lista completa dos cabeçalhos em seção recolhível.

## Cenários de Usuário e Testes *(obrigatório)*

### História de Usuário 1 - Descobrir os endpoints e seus contratos (Prioridade: P1)

Um desenvolvedor que vai integrar ou testar o encurtador abre a documentação da API no ambiente de desenvolvimento e vê todos os endpoints disponíveis: rota, método HTTP, parâmetros, corpo da requisição, formato da resposta e códigos de resposta possíveis. Ele entende o contrato sem precisar ler o código-fonte.

**Por que esta prioridade**: sem a descoberta dos endpoints e dos seus contratos, nenhum outro uso da documentação é possível. É a base da funcionalidade.

**Teste independente**: abrir a documentação no ambiente de desenvolvimento e verificar que os três endpoints existentes aparecem, cada um com seus parâmetros, schemas e códigos de resposta.

**Cenários de aceitação**:

1. **Dado** que a aplicação está em execução no ambiente de desenvolvimento, **quando** o desenvolvedor abre a documentação, **então** vê todos os endpoints existentes: criação de link, consulta de estatísticas de um link e redirecionamento pelo código curto.
2. **Dado** o endpoint de criação de link, **quando** o desenvolvedor consulta sua documentação, **então** encontra o formato do corpo da requisição (URL de destino, código personalizado opcional e data de expiração opcional) e o formato da resposta de sucesso.
3. **Dado** o endpoint de consulta de estatísticas, **quando** o desenvolvedor consulta sua documentação, **então** encontra o código do link na rota, o cabeçalho que carrega a chave de gerenciamento e o formato da resposta.
4. **Dado** qualquer endpoint, **quando** o desenvolvedor consulta sua documentação, **então** encontra todas as respostas de erro que ele pode devolver, cada uma com o status HTTP e o formato de erro usado pela API.
5. **Dado** que a documentação está aberta, **quando** o desenvolvedor lê as descrições, **então** elas estão em português, assim como as mensagens de erro da própria API.

---

### História de Usuário 2 - Executar chamadas diretamente pela interface (Prioridade: P2)

O desenvolvedor testa a API pela própria documentação: preenche os campos de cada endpoint, envia a requisição e vê a resposta recebida, sem usar ferramentas externas como cliente HTTP separado.

**Por que esta prioridade**: transforma a documentação em ferramenta de teste. Depende da descoberta dos contratos (História 1), mas é o que elimina a necessidade de ferramentas externas.

**Teste independente**: pela interface, criar um link, copiar a chave de gerenciamento recebida, consultar as estatísticas desse link e acessar o código curto, verificando as respostas em cada passo.

**Cenários de aceitação**:

1. **Dado** o formulário do endpoint de criação preenchido com uma URL válida, **quando** o desenvolvedor executa a chamada, **então** vê a resposta de sucesso com o link curto e a chave de gerenciamento.
2. **Dado** uma chave de gerenciamento válida e o código de um link existente, **quando** o desenvolvedor executa a consulta de estatísticas pela interface, **então** vê o total de acessos do link.
3. **Dado** uma chave de gerenciamento ausente ou incorreta, **quando** o desenvolvedor executa a consulta de estatísticas, **então** vê a resposta de não autorizado, no formato de erro da API.
4. **Dado** uma URL de destino inválida no formulário de criação, **quando** o desenvolvedor executa a chamada, **então** vê a resposta de dados inválidos com a mensagem explicativa.
5. **Dado** um código curto existente, **quando** o desenvolvedor executa o redirecionamento pela interface, **então** vê que a resposta é um redirecionamento e qual é a URL de destino, sem que a interface siga o redirecionamento automaticamente.
6. **Dado** que o desenvolvedor executou uma chamada, **quando** ele consulta a resposta, **então** vê o status HTTP, o tipo do conteúdo e a localização (quando houver) em destaque, a lista completa dos cabeçalhos em seção recolhível e o corpo recebido.

---

### História de Usuário 3 - Manter a documentação fora da produção (Prioridade: P3)

A equipe que opera o encurtador em produção não deixa a interface de documentação pública. Ela só fica disponível quando o ambiente é explicitamente configurado para isso.

**Por que esta prioridade**: é uma restrição de segurança e de superfície pública. Não agrega uso novo para o desenvolvedor, mas impede que a documentação seja exposta sem decisão explícita.

**Teste independente**: iniciar a aplicação no ambiente de produção com a configuração padrão e verificar que a interface de documentação não está acessível.

**Cenários de aceitação**:

1. **Dado** a aplicação em execução no ambiente de desenvolvimento, **quando** alguém acessa a interface de documentação, **então** ela está disponível.
2. **Dado** a aplicação em execução em produção com a configuração padrão, **quando** alguém tenta acessar a interface de documentação, **então** a interface não está disponível e a resposta é um 404 idêntico ao de uma rota inexistente, com o mesmo corpo.
3. **Dado** a aplicação em produção, **quando** a equipe habilita a documentação por configuração explícita, **então** a interface passa a ficar disponível.

---

### Casos de Borda

- O que acontece se um desenvolvedor tentar criar um link com um código personalizado igual ao caminho da própria documentação (por exemplo, `swagger`)? A criação deve ser recusada como palavra reservada, conforme REQ-018, e a documentação deve continuar acessível.
- O que acontece se a criação de links pela interface atingir o limite de criações por origem? A resposta de limite excedido deve aparecer na documentação e ser exibida na interface com a mensagem explicativa.
- O que acontece quando um endpoint é alterado? A documentação deve refletir a alteração sem que alguém precise editar um documento separado.
- O que acontece se a interface for usada para executar uma chamada a um código que expirou? A resposta de link expirado deve ser exibida como os demais erros.

## Requisitos *(obrigatório)*

### Requisitos Funcionais

- **REQ-019**: O sistema DEVE publicar um documento OpenAPI que descreva todos os endpoints HTTP existentes da API: criação de link, consulta de estatísticas e redirecionamento pelo código curto.
- **REQ-020**: Para cada endpoint, o documento DEVE informar a rota, o método HTTP, os parâmetros (código na rota e cabeçalho de chave de gerenciamento, quando houver), o corpo da requisição, os schemas da resposta de sucesso e os códigos de resposta possíveis, com seus formatos de erro.
- **REQ-021**: A documentação DEVE oferecer uma interface web interativa na qual o desenvolvedor pode preencher os parâmetros de um endpoint, enviar a chamada HTTP e ver a resposta recebida, sem instalar ferramentas externas.
- **REQ-022**: A interface de documentação e o documento OpenAPI DEVEM estar disponíveis no ambiente local de desenvolvimento.
- **REQ-023**: Fora do ambiente local de desenvolvimento (incluindo ambientes de teste compartilhados e produção), a interface de documentação NÃO DEVE estar disponível por padrão. Ela só PODE ser habilitada por configuração explícita. Quando não habilitada, o caminho da documentação DEVE responder 404 idêntico ao de uma rota inexistente, com o mesmo corpo, para não revelar que a documentação existe.
- **REQ-024**: A documentação DEVE refletir o comportamento atual dos endpoints. Uma mudança em um endpoint DEVE aparecer na documentação sem exigir edição manual separada do documento.
- **REQ-025**: As descrições, nomes de campos de exemplo e textos explicativos escritos pela equipe na documentação DEVEM estar em português, seguindo o idioma das mensagens de erro da API. Os rótulos fixos da ferramenta de interface (botões e campos padrão) PODEM permanecer em inglês.
- **REQ-026**: Os exemplos mostrados na documentação DEVEM usar dados fictícios e NÃO DEVEM conter chaves de gerenciamento reais de nenhum link.
- **REQ-027**: As rotas da própria documentação DEVEM ser reservadas, de modo que nenhum código personalizado possa coincidir com elas, conforme as regras de REQ-018.
- **REQ-028**: A interface de documentação NÃO DEVE seguir automaticamente redirecionamentos ao executar o endpoint de redirecionamento. Ela DEVE mostrar a resposta de redirecionamento e a URL de destino.
- **REQ-029**: A documentação NÃO DEVE criar, alterar nem consultar links por conta própria. Só as chamadas que o desenvolvedor executar explicitamente DEVEM produzir efeitos no sistema.

### Entidades-Chave

Não se aplica: esta funcionalidade descreve a API existente e não cria novos dados.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: Um desenvolvedor identifica os três endpoints existentes, com rota e método, em menos de 2 minutos, sem consultar o código-fonte.
- **SC-002**: Um desenvolvedor cria um link, consulta suas estatísticas e testa o redirecionamento usando somente a interface de documentação, em menos de 5 minutos, sem ferramentas externas.
- **SC-003**: 100% dos endpoints existentes aparecem na documentação, cada um com todos os códigos de resposta que ele pode devolver.
- **SC-004**: Em uma execução da aplicação com a configuração padrão de produção, a interface de documentação fica indisponível para 100% dos acessos, e a resposta não indica sua existência.
- **SC-005**: Após uma alteração em um endpoint, a documentação exibe a alteração na mesma entrega, sem edição manual separada.
- **SC-006**: Nenhuma chamada executada apenas por abrir a documentação altera o estado dos links.

## Premissas

- Esta funcionalidade documenta apenas os endpoints já definidos na especificação 001. Nenhum endpoint novo é criado.
- O documento OpenAPI gerado pela própria aplicação não substitui o contrato de planejamento já existente em `specs/001-url-shortener/contracts/`. Os dois devem ser mantidos coerentes pelo processo de planejamento.
- "Ambiente de desenvolvimento" é somente o ambiente local usado pelo desenvolvedor. Ambientes de teste compartilhados e o ambiente de produção ficam fora dele.
- A interface de documentação não tem controle de acesso próprio no ambiente de desenvolvimento. A proteção de produção é feita pela não exposição padrão (REQ-023).
- A chave de gerenciamento é informada manualmente pelo desenvolvedor em cada chamada que a exige. Não há login nem sessão na documentação.
- A numeração dos requisitos continua a partir de REQ-019, para não repetir identificadores da especificação 001.
- Restrições de arquitetura, escolha de bibliotecas e dependências novas são decididas no plano, conforme a constituição do projeto. Esta especificação não as define.
- A aparência da interface de documentação segue o padrão da ferramenta escolhida no plano, sem personalização de marca.
- Estão fora de escopo: autenticação na documentação, versionamento de múltiplas versões da API, geração de clientes de código a partir do documento e documentação pública em produção.
