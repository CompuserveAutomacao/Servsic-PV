# Regra de Banco de Dados: Tabelas, Migrações e Regras de Negócio

## 1. Conexão e Persistência
- Banco de Dados: Microsoft SQL Server.
- Acesso a Dados: Dapper para consultas e comandos rápidos e performáticos.
- Gravação Direta: Não utilizar persistência intermediária ou caches locais que possam gerar dessincronia. Toda ação grava diretamente nas tabelas correspondentes.

## 2. Tabelas Centrais
- **`TabEst3A` (Cabeçalho da Venda/Orçamento):**
  - Chave Primária: `Controle (int)`
  - Código: `Codigo (varchar(6))`
  - Status: `"O"` para Orçamento, `"V"` para Pedido de Venda.
  - Vendedor e Usuário: `LkUsuario` (usuário logado) e `LkVendedor` (vendedor atrelado ao usuário).
- **`TabEst3B` (Itens do Pedido):**
  - Vinculado via `LkCab = TabEst3A.Controle`.
  - Obrigatoriamente grava `LkVendedor` igual ao do vendedor do operador logado.
  - Campos de valor: `VlUnitario` (tabela de preços), `Quantidade`, `Desconto`, `Total`.
- **`TabCli` (Clientes):**
  - Sequencial gerado via Stored Procedure `NewCod_TabCli`.
  - Campos: `Controle`, `Codigo`, `Nome`, `FANTASIA_APELIDO`, `Cpf`, `CGC`, `Endereco`, `Numero`, `Bairro`, `Cidade`, `Estado`, `Cep`, `Telefone`, `Celular`, `eMail`.
- **`SERV` (Usuários / Operadores):**
  - Contém a coluna `LkVendedor (int NULL)` para associação automática.

## 3. Migrações Automáticas (`DatabaseMigrationService`)
Na inicialização da aplicação, o serviço de migração deve garantir a existência de:
- `TabEst1.PerDescMax decimal(12,2) NULL default 0`
- `SERV.LkVendedor int NULL`
- `CFG_VENDA.VendedorPorUsuario Char(1) default 'N'`
- `TabEst3B.LkVendedor int NULL`

## 4. Regras de Negócio
- Preço Unitário fixo: O operador não altera preço unitário; apenas quantidade e desconto.
- Consulta de CNPJ Online: Integração automática através de `CnpjLookupService` (BrasilAPI com fallback para ReceitaWS) para agilizar o preenchimento de cadastro rápido.
