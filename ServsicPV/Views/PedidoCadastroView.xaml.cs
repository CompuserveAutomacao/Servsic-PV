using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ServsicPV.ViewModels;
using ServSic.Core.Models;

namespace ServsicPV.Views;

public partial class PedidoCadastroView : UserControl
{
    public PedidoCadastroView()
    {
        this.InitializeComponent();
        this.DataContextChanged += PedidoCadastroView_DataContextChanged;
        this.KeyDown += PedidoCadastroView_KeyDown;
    }

    private void PedidoCadastroView_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (args.NewValue is PedidoViewModel vm)
        {
            vm.OnClienteDefinido = () => FocarCodigoProduto();
            vm.OnProdutoDefinido = () => FocarQuantidade();
            vm.OnItemAdicionado = () => FocarCodigoProduto();
        }
    }

    public void FocarCodigoProduto()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(50);
            TxtCodigoProduto?.Focus(FocusState.Programmatic);
            TxtCodigoProduto?.SelectAll();
        });
    }

    public void FocarQuantidade()
    {
        DispatcherQueue.TryEnqueue(async () =>
        {
            await Task.Delay(80);
            TxtQuantidade?.Focus(FocusState.Programmatic);
            TxtQuantidade?.SelectAll();
        });
    }

    public void FocarDescontoItem()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            TxtDescontoItem?.Focus(FocusState.Programmatic);
            TxtDescontoItem?.SelectAll();
        });
    }

    public void FocarBotaoInserir()
    {
        DispatcherQueue.TryEnqueue(() =>
        {
            BtnInserirItem?.Focus(FocusState.Programmatic);
        });
    }

    private void PedidoCadastroView_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.F3)
        {
            if (this.DataContext is PedidoViewModel vm && vm.BuscarClienteRapidoCommand.CanExecute(null))
            {
                vm.BuscarClienteRapidoCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.F4)
        {
            if (this.DataContext is PedidoViewModel vm && vm.AbrirConsultaProdutoCommand.CanExecute(null))
            {
                vm.AbrirConsultaProdutoCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.F2)
        {
            if (this.DataContext is PedidoViewModel vm && vm.NovoCommand.CanExecute(null))
            {
                vm.NovoCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private async void CodigoProduto_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.F4)
        {
            if (this.DataContext is PedidoViewModel vm && vm.AbrirConsultaProdutoCommand.CanExecute(null))
            {
                vm.AbrirConsultaProdutoCommand.Execute(null);
                e.Handled = true;
            }
        }
        else if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (this.DataContext is PedidoViewModel vm)
            {
                if (string.IsNullOrWhiteSpace(TxtCodigoProduto?.Text))
                {
                    vm.AbrirConsultaProduto();
                }
                else
                {
                    await vm.BuscarProdutoRapidoAsync();
                    if (vm.ProdutoSelecionado != null)
                    {
                        FocarQuantidade();
                    }
                }
            }
        }
    }

    private void Quantidade_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            FocarDescontoItem();
        }
    }

    private void DescontoItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            FocarBotaoInserir();
        }
    }

    private void BtnInserirItem_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            e.Handled = true;
            if (this.DataContext is PedidoViewModel vm && vm.HasProdutoSelecionado)
            {
                vm.AdicionarItem();
                FocarCodigoProduto();
            }
        }
    }

    private void FiltroCliente_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter || e.Key == Windows.System.VirtualKey.F3)
        {
            if (this.DataContext is PedidoViewModel vm && vm.BuscarClienteRapidoCommand.CanExecute(null))
            {
                vm.BuscarClienteRapidoCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void ModalCadastroRapido_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Escape)
        {
            if (this.DataContext is PedidoViewModel vm && vm.FecharCadastroRapidoClienteCommand.CanExecute(null))
            {
                vm.FecharCadastroRapidoClienteCommand.Execute(null);
                e.Handled = true;
            }
        }
    }

    private void NovoClienteCnpj_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            if (this.DataContext is PedidoViewModel vm && vm.IsPessoaJuridica && vm.ConsultarCnpjCommand.CanExecute(null))
            {
                vm.ConsultarCnpjCommand.Execute(null);
            }
        }
    }

    private void BtnRemoverItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is PedidoItem item)
        {
            if (this.DataContext is PedidoViewModel vm)
            {
                vm.RemoverItem(item);
            }
        }
    }

    public Action? OnSolicitarHistorico { get; set; }

    private void BtnConsultarHistorico_Click(object sender, RoutedEventArgs e)
    {
        OnSolicitarHistorico?.Invoke();
    }

    #region Máscaras e Proteção de Caracteres no Cadastro Rápido

    private bool _isFormatting = false;

    private void MascaraNumerica_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        // Impede terminantemente a digitação de letras em campos numéricos/com máscara
        foreach (char c in args.NewText)
        {
            if (!char.IsDigit(c) && c != '.' && c != '-' && c != '/' && c != '(' && c != ')' && c != ' ')
            {
                args.Cancel = true;
                return;
            }
        }
    }

    private void Uf_BeforeTextChanging(TextBox sender, TextBoxBeforeTextChangingEventArgs args)
    {
        // Impede números ou caracteres especiais na UF, aceitando apenas letras
        foreach (char c in args.NewText)
        {
            if (!char.IsLetter(c))
            {
                args.Cancel = true;
                return;
            }
        }
    }

    private void FormatarComMascara(TextBox tb, Func<string, string> formatar)
    {
        if (_isFormatting) return;
        _isFormatting = true;
        try
        {
            int cursor = tb.SelectionStart;
            string textoAtual = tb.Text ?? string.Empty;
            string textoFormatado = formatar(textoAtual);

            if (textoAtual != textoFormatado)
            {
                tb.Text = textoFormatado;
                int novoCursor = cursor + (textoFormatado.Length - textoAtual.Length);
                tb.SelectionStart = Math.Max(0, Math.Min(textoFormatado.Length, novoCursor));
            }
        }
        finally
        {
            _isFormatting = false;
        }
    }

    private void Documento_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        bool isJuridica = (this.DataContext is PedidoViewModel vm) && vm.IsPessoaJuridica;
        FormatarComMascara(sender, texto =>
        {
            string digitos = new string(texto.Where(char.IsDigit).ToArray());
            if (isJuridica || digitos.Length > 11)
            {
                // CNPJ: 00.000.000/0000-00 (máximo 14 dígitos)
                if (digitos.Length > 14) digitos = digitos.Substring(0, 14);
                if (digitos.Length <= 2) return digitos;
                if (digitos.Length <= 5) return $"{digitos[..2]}.{digitos[2..]}";
                if (digitos.Length <= 8) return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..]}";
                if (digitos.Length <= 12) return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..]}";
                return $"{digitos[..2]}.{digitos[2..5]}.{digitos[5..8]}/{digitos[8..12]}-{digitos[12..]}";
            }
            else
            {
                // CPF: 000.000.000-00 (máximo 11 dígitos)
                if (digitos.Length > 11) digitos = digitos.Substring(0, 11);
                if (digitos.Length <= 3) return digitos;
                if (digitos.Length <= 6) return $"{digitos[..3]}.{digitos[3..]}";
                if (digitos.Length <= 9) return $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..]}";
                return $"{digitos[..3]}.{digitos[3..6]}.{digitos[6..9]}-{digitos[9..]}";
            }
        });
    }

    private void Telefone_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        FormatarComMascara(sender, texto =>
        {
            string digitos = new string(texto.Where(char.IsDigit).ToArray());
            if (digitos.Length > 10) digitos = digitos.Substring(0, 10);
            if (digitos.Length == 0) return string.Empty;
            if (digitos.Length <= 2) return $"({digitos}";
            if (digitos.Length <= 6) return $"({digitos[..2]}) {digitos[2..]}";
            return $"({digitos[..2]}) {digitos[2..6]}-{digitos[6..]}";
        });
    }

    private void Celular_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        FormatarComMascara(sender, texto =>
        {
            string digitos = new string(texto.Where(char.IsDigit).ToArray());
            if (digitos.Length > 11) digitos = digitos.Substring(0, 11);
            if (digitos.Length == 0) return string.Empty;
            if (digitos.Length <= 2) return $"({digitos}";
            if (digitos.Length <= 6) return $"({digitos[..2]}) {digitos[2..]}";
            if (digitos.Length <= 10) return $"({digitos[..2]}) {digitos[2..6]}-{digitos[6..]}";
            return $"({digitos[..2]}) {digitos[2..7]}-{digitos[7..]}";
        });
    }

    private void Cep_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        FormatarComMascara(sender, texto =>
        {
            string digitos = new string(texto.Where(char.IsDigit).ToArray());
            if (digitos.Length > 8) digitos = digitos.Substring(0, 8);
            if (digitos.Length <= 5) return digitos;
            return $"{digitos[..5]}-{digitos[5..]}";
        });
    }

    private void Uf_TextChanging(TextBox sender, TextBoxTextChangingEventArgs args)
    {
        FormatarComMascara(sender, texto =>
        {
            string letras = new string(texto.Where(char.IsLetter).ToArray()).ToUpperInvariant();
            if (letras.Length > 2) letras = letras.Substring(0, 2);
            return letras;
        });
    }

    #endregion
}
