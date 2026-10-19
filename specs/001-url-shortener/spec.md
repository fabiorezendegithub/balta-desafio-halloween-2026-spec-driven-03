# Especificação de Funcionalidade: Encurtador de URLs

**Diretório da funcionalidade**: `specs/001-url-shortener`

**Criada em**: 2026-10-19

**Status**: Rascunho

**Entrada**: Descrição do usuário: "Um encurtador de links. O criador informa uma URL longa e recebe um link curto para compartilhar. Quem clica no link curto é redirecionado ao destino, e cada acesso é contado. O criador consegue consultar quantos acessos o link dele recebeu."

## Cenários de Usuário e Testes *(obrigatório)*

### História de Usuário 1 - Criar link curto a partir de uma URL longa (Prioridade: P1)

O criador informa uma URL longa e recebe um link curto para compartilhar. Ele pode deixar o código ser gerado automaticamente ou escolher o próprio código. Também pode definir uma data de expiração para o link.

**Por que esta prioridade**: sem a criação do link curto não existe nenhum outro valor do produto. É a base de todas as demais funcionalidades.

**Teste independente**: criar um link com uma URL válida e verificar que o link curto recebido é retornado ao criador, com o código no formato esperado.

**Cenários de aceitação**:

1. **Dado** uma URL `http` ou `https` válida com até 2048 caracteres, **quando** o criador a envia sem escolher código, **então** recebe um link curto com código gerado automaticamente de 7 caracteres alfanuméricos.
2. **Dado** um código personalizado de 4 a 20 caracteres alfanuméricos que ainda não está em uso, **quando** o criador envia a URL com esse código, **então** o link curto é criado usando exatamente esse código.
3. **Dado** um código personalizado que já está em uso por qualquer link do sistema, **quando** o criador tenta usá-lo, **então** a criação é recusada com mensagem explicando que o código já existe, e o link existente não é alterado.
4. **Dado** uma URL que não usa `http` nem `https` (por exemplo, `ftp://` ou `javascript:`), **quando** o criador a envia, **então** a criação é recusada com mensagem explicativa.
5. **Dado** uma URL com mais de 2048 caracteres, **quando** o criador a envia, **então** a criação é recusada com mensagem explicativa.
6. **Dado** um código personalizado com menos de 4 caracteres, com mais de 20 caracteres ou com caracteres que não são letras ou números, **quando** o criador o envia, **então** a criação é recusada com mensagem explicativa.
7. **Dado** uma data de expiração no passado, **quando** o criador a informa, **então** a criação é recusada com mensagem explicativa.
8. **Dado** que o criador não informa data de expiração, **quando** o link é criado, **então** o link não expira.

---

### História de Usuário 2 - Acessar o link curto e ser redirecionado, com contagem (Prioridade: P2)

Quem clica no link curto é levado automaticamente à URL de destino. Cada acesso ao link é registrado na contagem do link.

**Por que esta prioridade**: é o uso principal do link curto depois de criado, e é a fonte dos dados de acesso.

**Teste independente**: criar um link, acessar o código curto e verificar que o visitante chegou ao destino e que o contador do link aumentou em 1.

**Cenários de aceitação**:

1. **Dado** um link ativo, **quando** um visitante acessa o link curto, **então** é redirecionado à URL de destino e o contador de acessos do link aumenta em exatamente 1.
2. **Dado** um link ativo, **quando** o mesmo visitante acessa o link várias vezes, **então** cada acesso é contado separadamente.
3. **Dado** um link expirado, **quando** um visitante acessa o link curto, **então** não é redirecionado, o contador não é alterado e o visitante vê uma mensagem informando que o link expirou.
4. **Dado** um código que não corresponde a nenhum link, **quando** um visitante acessa esse código, **então** vê uma mensagem informando que o link não foi encontrado, e nenhum contador é alterado.

---

### História de Usuário 3 - Consultar o número de acessos do próprio link (Prioridade: P3)

O criador consulta quantos acessos o link dele recebeu, para acompanhar o alcance do que compartilhou.

**Por que esta prioridade**: é uma funcionalidade de acompanhamento. O link já funciona sem ela, mas perde valor para o criador sem ela.

**Teste independente**: criar um link, gerar alguns acessos e consultar o total pelo mecanismo de consulta. O total deve corresponder ao número de acessos feitos.

**Cenários de aceitação**:

1. **Dado** um link que recebeu N acessos ativos, **quando** o criador consulta o link, **então** o total informado é N.
2. **Dado** um link que já recebeu acessos e depois expirou, **quando** o criador consulta o link, **então** o total informado é o mesmo de antes da expiração.
3. **Dado** um link cujo total já foi consultado, **quando** novos acessos são feitos e o criador consulta de novo, **então** o total aumentou exatamente pelo número de novos acessos válidos, e nunca diminuiu.
4. **Dado** um link que recebeu acessos antes de a aplicação ser reiniciada, **quando** o criador consulta o link depois do reinício, **então** o total é o mesmo de antes do reinício.

---

### Casos de Borda

- O que acontece quando dois visitantes acessam o mesmo link ao mesmo tempo? Os dois acessos devem ser contados e nenhum deve ser perdido.
- O que acontece quando duas pessoas tentam criar, ao mesmo tempo, um link com o mesmo código personalizado? Apenas uma criação deve ser bem-sucedida; a outra deve ser recusada como código já em uso.
- O que acontece com um link no instante exato de sua expiração? A partir do instante informado como expiração, o link é considerado expirado.
- O que acontece se a URL de destino não estiver mais disponível? O redirecionamento continua acontecendo e o acesso é contado; o sistema não verifica se o destino responde.
- O que acontece se o mesmo código personalizado for digitado com letras maiúsculas e minúsculas diferentes? Os dois são tratados como o mesmo código (ver Premissas).
- O que acontece se a aplicação for reiniciada logo após a criação de um link? Um link cuja criação foi confirmada ao criador deve continuar existindo depois do reinício.

## Requisitos *(obrigatório)*

### Requisitos Funcionais

- **REQ-001**: O sistema DEVE gerar automaticamente um código de 7 caracteres alfanuméricos quando o criador não informar um código.
- **REQ-002**: O sistema DEVE permitir que o criador escolha o próprio código, com 4 a 20 caracteres alfanuméricos (letras e números, sem espaços nem símbolos).
- **REQ-003**: O sistema DEVE garantir que cada código seja único em todo o sistema, independentemente de quem criou o link. Um código personalizado já em uso DEVE ser recusado, e um código gerado automaticamente NUNCA DEVE coincidir com um código já existente.
- **REQ-004**: O sistema DEVE tratar os códigos sem diferenciar letras maiúsculas de minúsculas, tanto na unicidade quanto na busca pelo visitante.
- **REQ-005**: A URL de destino DEVE começar com `http://` ou `https://` e ter no máximo 2048 caracteres. Uma URL fora dessas regras DEVE ser recusada com mensagem explicativa em português.
- **REQ-006**: O criador PODE definir uma data e hora de expiração para o link. Quando não for informada, o link NÃO DEVE expirar.
- **REQ-007**: Uma data de expiração que já passou no momento da criação DEVE ser recusada com mensagem explicativa.
- **REQ-008**: Ao acessar o código de um link ativo, o visitante DEVE ser redirecionado para a URL de destino.
- **REQ-009**: Cada acesso a um link ativo DEVE aumentar o contador de acessos do link em exatamente 1.
- **REQ-010**: Um link expirado NÃO DEVE redirecionar o visitante e NÃO DEVE alterar o contador. O visitante DEVE ver uma mensagem informando que o link expirou.
- **REQ-011**: Um código que não corresponde a nenhum link DEVE exibir mensagem de link não encontrado, sem alterar nenhum contador.
- **REQ-012**: O contador de acessos NUNCA DEVE diminuir nem ser zerado, em nenhuma circunstância, inclusive após a expiração do link.
- **REQ-013**: O criador DEVE conseguir consultar o total de acessos do próprio link. [NEEDS CLARIFICATION: como o criador prova que é o dono do link para consultar o total, já que não há cadastro nem login? Opções: (A) chave secreta de gerenciamento entregue ao criador na criação; (B) consulta pública pelo próprio código; (C) código de consulta separado, gerado junto com o link curto]
- **REQ-014**: Links, seus destinos, datas de expiração e contadores DEVEM continuar válidos e consultáveis após a aplicação ser reiniciada.
- **REQ-015**: A contagem DEVE ser precisa mesmo com acessos simultâneos ao mesmo link: nenhum acesso válido pode deixar de ser contado.
- **REQ-016**: O redirecionamento NÃO DEVE ser memorizado pelo navegador nem por intermediários de forma que acessos seguintes deixem de passar pelo sistema e deixem de ser contados.

### Entidades-Chave *(incluir se a funcionalidade envolve dados)*

- **Link**: representa um link curto criado. Atributos: código, URL de destino, data de criação, data de expiração (opcional) e total de acessos. O total de acessos só cresce.
- **Acesso**: cada visita válida a um link ativo. Para esta versão, interessa apenas a contagem; o sistema não precisa guardar detalhes individuais de cada acesso.

## Critérios de Sucesso *(obrigatório)*

### Resultados Mensuráveis

- **SC-001**: O criador recebe o link curto em menos de 30 segundos, desde o envio da URL até a exibição do link.
- **SC-002**: 100% dos acessos a links ativos resultam em redirecionamento para o destino correto e em contagem de exatamente 1 acesso.
- **SC-003**: 100% dos links e contadores criados antes de um reinício continuam acessíveis, com o mesmo código, destino e total, depois do reinício.
- **SC-004**: Em um teste com 100 acessos simultâneos ao mesmo link, o total final reflete exatamente os 100 acessos.
- **SC-005**: Nenhum acesso a um link expirado redireciona o visitante ou altera o contador.
- **SC-006**: 100% das entradas inválidas (URL fora das regras, código fora do formato, código já em uso, data no passado) são recusadas com mensagem explicativa, sem criar nenhum link.

## Premissas

- A criação de links é anônima: não há cadastro nem login de usuários. Cada criação gera um link novo, mesmo que a mesma URL de destino já tenha sido encurtada antes.
- Quem consulta o total de acessos deve ser o criador; a forma de provar essa autoria está pendente na pergunta do REQ-013.
- Um código personalizado e um código gerado automaticamente seguem as mesmas regras de unicidade e de busca.
- A data de expiração é informada com data e hora e deve ter fuso horário explícito.
- Um link expirado continua existindo no sistema, e seu total de acessos continua consultável pelo criador.
- O sistema não verifica se a URL de destino está disponível no momento do cadastro nem durante o redirecionamento.
- Estão fora de escopo: cadastro e login de usuários, domínio personalizado, relatório geográfico e edição da URL de destino de um link já criado.
