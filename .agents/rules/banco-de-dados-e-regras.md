# Regra de Banco de Dados: Tabelas, Migrações e Regras de Negócio

## 1. Conexão e Persistência
- Banco de Dados: Microsoft SQL Server.
- Acesso a Dados: Dapper para consultas e comandos rápidos e performáticos.
- Gravação Direta: Não utilizar persistência intermediária ou caches locais que possam gerar dessincronia. Toda ação grava diretamente nas tabelas correspondentes.

## 2. Tabelas Centrais
- **`TabEst3A` (Cabeçalho da Venda/Orçamento):**
  - Chave Primária: `Controle (int)` e `Pedido (int)`
  - Código: `Codigo (varchar(6))`
  - Status: `'O'` para Orçamento, `'P'` para Pré-Venda / Pedido de Balcão, `'V'` para Venda Concluída no Caixa (com `Venda = 1`), `'C'` para Cancelada.
  - Vendedor e Usuário: `LkUsuario` (usuário logado) e `LkVendedor` (vendedor atrelado ao usuário).
  - Subtotal: Representa a soma bruta dos itens (`TabEst3B.Total`). Nunca deve ser zerado.
- **`TabEst3B` (Itens do Pedido):**
  - Vinculado via `LkEst3A = TabEst3A.Controle` e `PEDIDO = TabEst3A.Pedido`.
  - Obrigatoriamente grava `LkVendedor` igual ao do vendedor do operador logado.
  - Campos de valor: `VlUnitario` (tabela de preços), `Quantidade`, `Desconto`, `Total`.
- **`TabEst3C` (Parcelas e Formas de Pagamento):**
  - Vinculado via `PEDIDO = TabEst3A.Pedido`.
  - Campos: `Controle`, `PEDIDO`, `RECEBIMENTO`, `LkTipo`, `VALOR`, `GERACONTA`.
- **`TabCli` (Clientes):**
  - Sequencial gerado via Stored Procedure `NewCod_TabCli`.
  - Campos: `Controle`, `Codigo`, `Nome`, `FANTASIA_APELIDO`, `Cpf`, `CGC`, `Endereco`, `Numero`, `Bairro`, `Cidade`, `Estado`, `Cep`, `Telefone`, `Celular`, `eMail`.
- **`SERV` (Usuários / Operadores):**
  - Contém a coluna `LkVendedor (int NULL)` para associação automática.

## 3. Atenção com Tipos `BIT` no Dapper
- Colunas `BIT` do SQL Server (`Venda`, `Cancelada`, `GERACONTA`) retornam `bool` no C#.
- **Nunca** realize cast direto para `(int)` a partir de variáveis dinâmicas (`dynamic`), pois lançará `Cannot convert type 'bool' to 'int'`.
- Sempre utilize `Convert.ToBoolean(...)` ou `Convert.ToInt32(...)`.

## 4. Mapeamento de Status e Bloqueio de Edição
- **Orçamento (`Status = 'O'`, `Venda = 0`):** 100% editável.
- **Pré-Venda (`Status = 'P'`, `Venda = 0`):** 100% editável pelo vendedor, mesmo após totalização/gravação de pagamentos.
- **Venda Concluída (`Status = 'V'`, `Venda = 1`):** Concretizada exclusivamente pelo Caixa / PDV Fiscal. Bloqueada para qualquer edição (Modo Somente Leitura).
- **Cancelada (`Status = 'C'` ou `Cancelada = 1`):** Bloqueada para edição.
- O SERVSIC PV **jamais salva com `Venda = 1` nem com `Status = 'V'`**.

## 5. Migrações Automáticas (`DatabaseMigrationService`)
Na inicialização da aplicação, o serviço de migração deve garantir a existência de:
- `TabEst1.PerDescMax decimal(12,2) NULL default 0`
- `SERV.LkVendedor int NULL`
- `CFG_VENDA.VendedorPorUsuario Char(1) default 'N'`
- `TabEst3B.LkVendedor int NULL`

## 6. Regras de Negócio e Políticas de Venda
- Preço Unitário fixo: O operador não altera preço unitário; apenas quantidade e desconto.
- Desconto em reais: Respeita o teto de desconto (`TabEst1.PerDescMax`).
- Consulta de CNPJ Online: Integração automática através de `CnpjLookupService` (BrasilAPI com fallback para ReceitaWS) para agilizar o preenchimento de cadastro rápido.
