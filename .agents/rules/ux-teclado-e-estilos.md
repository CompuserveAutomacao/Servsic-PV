# Regra de Interface: Experiência Focada em Teclado e Design System

## 1. Prioridade Absoluta: Uso via Teclado (Keyboard-First POS)
Em uma aplicação de Ponto de Venda e Frente de Loja, a agilidade do operador depende do teclado:
- **Atalhos Globais:**
  - `F3`: Dispara busca/consulta de clientes a partir de qualquer ponto da janela.
  - `F2`: Cria novo pedido ou orçamento a qualquer momento.
  - `Esc`: Fecha modais e cancela ações.
- **Fluxo do Modal de Clientes:**
  - O modal deve abrir com o foco diretamente na grade de resultados (`ListView`), com o primeiro item pré-selecionado.
  - As setas `↑` e `↓` devem navegar imediatamente pelas linhas.
  - Pressionar `Enter` sobre uma linha deve selecionar o cliente, fechar o modal e posicionar o cursor no campo de código do produto (`TxtCodigoProduto`).
  - Pressionar a seta `↓` enquanto no campo de texto de busca do modal deve transferir o foco para a grade.

## 2. Design System ServSic
- **Cores Principais:**
  - Azul Corporativo: `#0284C7` (fundo principal de ações) e `#0369A1` (hover / destaque).
  - Azul Escuro Navy: `#0B192C` (barra superior e card de valor total).
  - Fundo da Janela: `#F8FAFC` (cinza-gelo suave).
  - Cards: `#FFFFFF` com bordas `#E2E8F0` e cantos arredondados (`CornerRadius="8"`).
- **Proibição Estrita da Cor Menta:**
  - A cor menta (`#00D2C4`) é EXCLUSIVA da marca "CompuServe". Nunca use verde ou menta em botões ou cartões da UI operacional.
- **Tipografia e Ícones:**
  - Fontes padrão do sistema (`Segoe UI`, `Segoe UI Semibold`).
  - Utilizar Fluent Icons (`using:FluentIcons.WinUI` com `<ic:SymbolIcon .../>`).
  - Não duplicar caracteres textuais com ícones (ex: usar apenas `Novo Cliente` ao invés de `+ Novo Cliente`).
- **Estrutura dos Cards:**
  - Todos os cards devem ter cabeçalho com ícone Fluent moderno e título semântico.
  - Resumo financeiro deve ocupar a coluna vertical inteira à direita (`Width="350"`).
- **Sistema de Notificações / Mensagens:**
  - Notificações de status não podem ficar estáticas para sempre. Devem possuir temporizador de auto-dismiss (3 a 4 segundos) e botão de fechar manual (`X`).
  - Ao remover um cliente ou limpar campos, a notificação deve ser limpa imediatamente.
