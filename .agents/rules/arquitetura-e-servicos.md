# Regra de Arquitetura e Serviços: Simplicidade e Responsabilidade Única

## 1. Princípio "Do Jeito Simples"
- Não criar interfaces com prefixo `I` (como `IPedidoService`, `IClienteService`, etc.) por padrão.
- Manter o código direto, conciso e legível. A preferência do projeto é por serviços concretos injetados ou instanciados diretamente.

## 2. Divisão Especializada do Pedido
O fluxo de pedidos e orçamentos é dividido em 3 serviços especializados:
1. **`PedidoCabService`**:
   - Responsabilidade: gerenciar a tabela `TabEst3A` (cabeçalho da venda/orçamento).
   - Atribui `Controle`, `Codigo`, `LkCliente`, `Cliente`, `LkUsuario`, `LkVendedor`, `Status` ("O" para Orçamento, "V" para Venda).
2. **`PedidoItemService`**:
   - Responsabilidade: gerenciar a tabela `TabEst3B` (itens do pedido).
   - Obrigatoriamente vincula o `LkVendedor` do usuário autenticado a cada item inserido.
3. **`PedidoPagService`**:
   - Responsabilidade: gerenciar a tabela `TabEst3C` (pagamentos e parcelas).
4. **`PedidoService`**:
   - Responsabilidade: orquestrar os serviços acima, assegurando integridade e transação ao salvar ou converter pedidos.

## 3. Padrão de Modelos e ViewModels
- Modelos herdam de `ModelBase` ou utilizam notificações INotifyPropertyChanged quando necessário para binding no WinUI 3.
- ViewModels herdam de `ViewModelBase`.
- Manter ViewModels limpas, delegando consultas e persistência aos serviços correspondentes.
