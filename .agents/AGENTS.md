# SERVSIC PV - DIRETRIZES GERAIS E LEIS DO PROJETO

Este documento define as regras fundamentais, leis de arquitetura, padrões de interface e fluxos de desenvolvimento para o projeto **SERVSIC PV** (Ponto de Venda / Pré-Venda / Orçamentos em Windows App SDK / WinUI 3 com .NET 10 e SQL Server).

---

## 1. Princípio da Simplicidade ("Do Jeito Simples")
- **Sem Over-Engineering:** Não crie interfaces duplicadas com prefixo `I...` (ex: `IClienteService`, `IPedidoCabService`) a menos que expressamente solicitado pelo usuário ou estritamente necessário.
- **Classes Concretas Diretas:** Os serviços devem ser classes concretas bem delimitadas, objetivas e fáceis de manter.
- **Responsabilidade Única no Fluxo de Pedidos:**
  - `PedidoCabService`: Responsável exclusivo pelo cabeçalho da venda/orçamento (`TabEst3A`).
  - `PedidoItemService`: Responsável exclusivo pelos itens do pedido (`TabEst3B`), garantindo a amarração do `LkVendedor` do usuário autenticado.
  - `PedidoPagService`: Responsável exclusivo pelo plano de pagamento e parcelas (`TabEst3C`).
  - `PedidoService`: Orquestrador dos serviços acima para garantir atomicidade e consistência.

---

## 2. Foco Operacional: Teclado em Primeiro Lugar (Keyboard-First POS)
O SERVSIC PV é uma aplicação de alta produtividade para frente de caixa e pré-venda. O operador deve conseguir realizar 100% da rotina operacional **sem encostar no mouse**:
- **Atalhos Globais:**
  - <kbd>F2</kbd>: Iniciar novo pedido ou orçamento.
  - <kbd>F3</kbd>: Abrir busca/consulta rápida de clientes.
  - <kbd>Esc</kbd>: Fechar qualquer modal ou diálogo aberto.
- **Navegação em Grades e Modais:**
  - Ao abrir o modal de consulta de clientes, o foco deve ir imediatamente para a grade de resultados com a primeira linha selecionada.
  - As setas <kbd>↑</kbd> e <kbd>↓</kbd> navegam diretamente pelas linhas.
  - Ao pressionar <kbd>Enter</kbd> sobre qualquer linha, o cliente é selecionado, o modal fecha e o cursor vai automaticamente para o campo de bipe de produto (`TxtCodigoProduto`).
  - Se o foco estiver no campo de busca do modal, pressionar <kbd>↓</kbd> transfere o foco para a grade.
- **Inserção de Produtos:**
  - Bipe ou digite o código/código de barras e tecle <kbd>Enter</kbd>.

---

## 3. Identidade Visual e Design System ServSic
- **Paleta de Cores:**
  - **Azul Primário Corporativo:** `#0284C7` (Hover: `#0369A1`).
  - **Fundo Gelo:** `#F8FAFC`.
  - **Superfícies de Cards:** Branco puro `#FFFFFF` com bordas sutis `#E2E8F0`.
  - **Barra Superior & Destaque de Totais:** Azul Escuro Navy `#0B192C`.
- **Regra do Menta / Verde:**
  - **TERMINANTEMENTE PROIBIDO** o uso de cor menta/verde na interface operacional do sistema.
  - A cor menta (`#00D2C4`) é de uso restrito e exclusivo do logotipo "serve" da marca CompuServe.
- **Regra de Foco e Estados de Interação (JAMAIS USAR FOCO BRANCO):**
  - **TERMINANTEMENTE PROIBIDO** o uso de foco branco ou fundo desbotado/fantasma em botões e componentes interativos.
  - O estado de foco (<kbd>Tab</kbd> / navegação por teclado) e de hover/apontamento de botões de ação e botões principais deve obrigatoriamente utilizar o **Azul Escuro Navy padrão do sistema (`#0B192C`)** com texto e ícones em **Branco Puro (`#FFFFFF`)**, garantindo alto contraste, legibilidade imediata e presença visual firme.
  - Nunca permitir que estilos padrão do WinUI 3 (ex: `ButtonBackgroundPointerOver`, `ButtonBackgroundFocused`) transformem o botão em branco ou apaguem o contraste do texto.
- **Ícones Modernos:**
  - Usar exclusivamente Fluent Icons (`using:FluentIcons.WinUI` com `<ic:SymbolIcon Symbol="..." />`).
  - Nunca duplicar símbolos textuais com ícones (ex: botões com ícone de `Add` devem exibir apenas o texto `Novo Cliente`, nunca `+ Novo Cliente`).
- **Layout Estrutural:**
  - **Coluna Esquerda (`*`):** Identificação do Cliente, Grade de Produtos/Itens e Barra de Ações Inferior.
  - **Coluna Direita Vertical (`350px`):** Resumo Financeiro ocupando toda a altura vertical da tela com totais em grande destaque.
  - **Títulos dos Cards:** Todos os cards principais devem possuir cabeçalho com ícone Fluent moderno e título semântico (`Cliente`, `Produtos / Itens do Pedido`, `Resumo Financeiro`).
- **Sistema de Mensagens / Notificações:**
  - Notificações de status nunca devem ficar travadas na tela.
  - Implementar auto-dismiss automático (3 a 4 segundos) e botão de fechar manual (<kbd>X</kbd>).
  - Ao remover ou limpar o cliente da tela, a notificação de seleção deve ser limpa imediatamente.

---

## 4. Banco de Dados e Persistência Direta (SQL Server)
- **Persistência Direta:** Gravação imediata no SQL Server através de repositórios/serviços com Dapper. Não há gravação em arquivos intermediários.
- **Tabelas Principais:**
  - `TabEst3A`: Cabeçalho do pedido/orçamento (`Controle`, `Codigo`, `Status`, `LkCliente`, `Cliente`, `LkUsuario`, `LkVendedor`, `Subtotal`, `Total`, etc.).
  - `TabEst3B`: Itens do pedido (`LkCab`, `LkProduto`, `Quantidade`, `VlUnitario`, `Desconto`, `Total`, `LkVendedor`).
  - `TabEst3C`: Parcelas e formas de pagamento.
  - `TabCli`: Cadastro completo de clientes.
  - `TabEst1`: Cadastro de produtos.
  - `SERV`: Cadastro de usuários e operadores.
- **Migrações Automáticas:**
  - A aplicação deve verificar e criar colunas necessárias automaticamente na inicialização (`DatabaseMigrationService`).
  - Campos essenciais: `TabEst1.PerDescMax`, `SERV.LkVendedor`, `CFG_VENDA.VendedorPorUsuario`, `TabEst3B.LkVendedor`.
- **Stored Procedures Legadas:**
  - Usar `NewCod_TabEst3A` e `NewCod_TabCli` para geração de identificadores sequenciais.

---

## 5. Regras de Negócio e Políticas de Venda
- **Preço Unitário Bloqueado:** O operador não altera preço unitário de produto. O valor vem fixo da tabela de preços.
- **Desconto:** Permitida a aplicação de desconto em reais no item ou no total, respeitando o teto de desconto (`TabEst1.PerDescMax`).
- **Vendedor Automático por Usuário:** O vendedor responsável pela venda é atrelado diretamente ao `LkVendedor` do usuário logado na tabela `SERV`. Não há seleção manual de vendedor na tela principal.
