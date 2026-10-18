using System;
using System.Globalization;
using System.Text;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

/// <summary>
/// Serviço de formatação de cupom não-fiscal para bobina térmica (80mm ou 58mm).
/// Gera texto puro alinhado com separadores e campos padronizados.
/// </summary>
public class CupomFormatadorService
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public string GerarTextoCupom(PedidoVenda pedido, DadosEmpresa empresa, ConfiguracaoImpressora config)
    {
        int colunas = config.LarguraColunas >= 40 ? 48 : 32;
        string linhaDiv = new('-', colunas);
        string linhaDupla = new('=', colunas);

        var sb = new StringBuilder();

        // 1. Cabeçalho da Empresa
        if (!string.IsNullOrWhiteSpace(empresa.NomeFantasia))
            sb.AppendLine(Centralizar(empresa.NomeFantasia.ToUpperInvariant(), colunas));

        if (!string.IsNullOrWhiteSpace(empresa.RazaoSocial) && !empresa.RazaoSocial.Equals(empresa.NomeFantasia, StringComparison.OrdinalIgnoreCase))
            sb.AppendLine(Centralizar(empresa.RazaoSocial, colunas));

        if (!string.IsNullOrWhiteSpace(empresa.Cnpj))
        {
            string docInfo = $"CNPJ: {FormatarCnpj(empresa.Cnpj)}";
            if (!string.IsNullOrWhiteSpace(empresa.InscricaoEstadual))
                docInfo += $"  IE: {empresa.InscricaoEstadual}";
            sb.AppendLine(Centralizar(docInfo, colunas));
        }

        if (!string.IsNullOrWhiteSpace(empresa.EnderecoLinha1))
            sb.AppendLine(Centralizar(empresa.EnderecoLinha1, colunas));

        if (!string.IsNullOrWhiteSpace(empresa.EnderecoLinha2))
            sb.AppendLine(Centralizar(empresa.EnderecoLinha2, colunas));

        if (!string.IsNullOrWhiteSpace(empresa.Telefone))
            sb.AppendLine(Centralizar($"FONE: {empresa.Telefone}", colunas));

        sb.AppendLine(linhaDupla);

        // 2. Identificação do Documento
        string tipoDoc = pedido.IsOrcamento ? "ORCAMENTO DE VENDA" : "PEDIDO DE VENDA";
        sb.AppendLine(Centralizar($"*** {tipoDoc} ***", colunas));
        
        int numPedido = pedido.Pedido > 0 ? pedido.Pedido : pedido.Controle;
        sb.AppendLine(Centralizar($"NUMERO: {numPedido:D6}", colunas));
        sb.AppendLine(linhaDiv);

        // 3. Dados de Emissão, Vendedor e Caixa
        string dataHora = $"{pedido.Data:dd/MM/yyyy}  {pedido.HoraIni}";
        sb.AppendLine(AlinharEsquerdaDireita($"EMISSAO: {dataHora}", $"STATUS: {(pedido.IsOrcamento ? "ORC" : "PED")}", colunas));
        
        string vendedor = !string.IsNullOrWhiteSpace(pedido.Vendedor) ? pedido.Vendedor : "BALCAO";
        sb.AppendLine($"VENDEDOR: {Truncar(vendedor, colunas - 10)}");
        sb.AppendLine(linhaDiv);

        // 4. Identificação do Cliente
        string nomeCliente = !string.IsNullOrWhiteSpace(pedido.Cliente) ? pedido.Cliente : "CONSUMIDOR / BALCAO";
        sb.AppendLine($"CLIENTE: {Truncar(nomeCliente, colunas - 9)}");

        if (!string.IsNullOrWhiteSpace(pedido.CPF_CNPJ))
            sb.AppendLine($"CPF/CNPJ: {FormatarCpfCnpj(pedido.CPF_CNPJ)}");

        if (!string.IsNullOrWhiteSpace(pedido.Telefone))
            sb.AppendLine($"FONE: {pedido.Telefone}");

        if (!string.IsNullOrWhiteSpace(pedido.Endereco))
        {
            string end = pedido.Endereco;
            if (!string.IsNullOrWhiteSpace(pedido.Numero)) end += $", {pedido.Numero}";
            if (!string.IsNullOrWhiteSpace(pedido.Bairro)) end += $" - {pedido.Bairro}";
            sb.AppendLine($"END: {Truncar(end, colunas - 5)}");
        }

        sb.AppendLine(linhaDiv);

        // 5. Tabela de Itens
        if (colunas >= 48)
        {
            sb.AppendLine("ITEM  CODIGO    DESCRICAO");
            sb.AppendLine("      QTD x VL.UNIT(R$)             TOTAL(R$)");
        }
        else
        {
            sb.AppendLine("COD/DESC");
            sb.AppendLine("QTD x UNIT              TOTAL");
        }
        sb.AppendLine(linhaDiv);

        int seq = 1;
        foreach (var item in pedido.Itens)
        {
            string cod = !string.IsNullOrWhiteSpace(item.CodigoExibicao) ? item.CodigoExibicao.Trim() : item.LkProduto.ToString();
            string desc = Truncar(item.Produto?.Trim() ?? "PRODUTO", colunas >= 48 ? 32 : 22);

            if (colunas >= 48)
            {
                sb.AppendLine($"{seq:D3}   {cod,-8}  {desc}");
                string qtdUnit = $"{item.Quantidade:N3} {item.Unidade} x {item.VlUnitario:N2}";
                string totItem = item.Total.ToString("N2", PtBr);
                sb.AppendLine(AlinharEsquerdaDireita($"      {qtdUnit}", totItem, colunas));
            }
            else
            {
                sb.AppendLine($"{seq:D2} {cod} {desc}");
                string qtdUnit = $"{item.Quantidade:N2} {item.Unidade} x {item.VlUnitario:N2}";
                string totItem = item.Total.ToString("N2", PtBr);
                sb.AppendLine(AlinharEsquerdaDireita(qtdUnit, totItem, colunas));
            }
            seq++;
        }

        sb.AppendLine(linhaDiv);

        // 6. Resumo Financeiro
        int totalQtdItens = pedido.Itens.Count;
        sb.AppendLine(AlinharEsquerdaDireita("QTD. TOTAL DE ITENS:", totalQtdItens.ToString(), colunas));
        sb.AppendLine(AlinharEsquerdaDireita("SUBTOTAL (R$):", pedido.Subtotal.ToString("N2", PtBr), colunas));

        if (pedido.Desconto > 0)
            sb.AppendLine(AlinharEsquerdaDireita("DESCONTO (R$):", $"- {pedido.Desconto:N2}", colunas));

        if (pedido.Acrescimo > 0)
            sb.AppendLine(AlinharEsquerdaDireita("ACRESCIMO / FRETE (R$):", $"+ {pedido.Acrescimo:N2}", colunas));

        sb.AppendLine(linhaDupla);
        sb.AppendLine(AlinharEsquerdaDireita("VALOR TOTAL (R$):", pedido.Total.ToString("N2", PtBr), colunas));
        sb.AppendLine(linhaDupla);

        // 7. Formas de Pagamento
        if (pedido.Pagamentos.Count > 0)
        {
            sb.AppendLine(Centralizar("PLANO DE PAGAMENTO", colunas));
            sb.AppendLine(linhaDiv);

            foreach (var pag in pedido.Pagamentos)
            {
                string descPag = pag.Recebimento ?? "OUTROS";
                if (pag.Parcela > 0)
                    descPag += $" (Parc. {pag.Parcela})";

                string valorPag = pag.Valor.ToString("N2", PtBr);
                sb.AppendLine(AlinharEsquerdaDireita(descPag, valorPag, colunas));

                if (pag.Vencimento.Date > DateTime.Today)
                {
                    sb.AppendLine($"   Vencimento: {pag.Vencimento:dd/MM/yyyy}");
                }
            }
            sb.AppendLine(linhaDiv);
        }

        // 8. Observações
        if (!string.IsNullOrWhiteSpace(pedido.Obs))
        {
            sb.AppendLine("OBSERVACOES:");
            sb.AppendLine(QuebrarTexto(pedido.Obs.Trim(), colunas));
            sb.AppendLine(linhaDiv);
        }

        // 9. Rodapé
        if (!string.IsNullOrWhiteSpace(config.MensagemRodape))
            sb.AppendLine(Centralizar(config.MensagemRodape, colunas));

        sb.AppendLine(Centralizar("CompuServe Sistemas", colunas));
        sb.AppendLine(new string(' ', colunas)); // Espaço final para avanço do papel
        sb.AppendLine(new string(' ', colunas));

        return sb.ToString();
    }

    private static string Centralizar(string texto, int largura)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        if (texto.Length >= largura) return texto.Substring(0, largura);
        int espacos = (largura - texto.Length) / 2;
        return texto.PadLeft(texto.Length + espacos).PadRight(largura);
    }

    private static string AlinharEsquerdaDireita(string esquerda, string direita, int largura)
    {
        if (esquerda.Length + direita.Length >= largura)
        {
            int maxEsq = largura - direita.Length - 1;
            if (maxEsq > 0) esquerda = esquerda.Substring(0, maxEsq);
        }
        int espacos = largura - esquerda.Length - direita.Length;
        return esquerda + new string(' ', Math.Max(1, espacos)) + direita;
    }

    private static string Truncar(string texto, int max)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        return texto.Length <= max ? texto : texto.Substring(0, max);
    }

    private static string QuebrarTexto(string texto, int largura)
    {
        var sb = new StringBuilder();
        var palavras = texto.Split(' ');
        var linha = new StringBuilder();

        foreach (var p in palavras)
        {
            if (linha.Length + p.Length + 1 > largura)
            {
                sb.AppendLine(linha.ToString());
                linha.Clear();
            }
            if (linha.Length > 0) linha.Append(' ');
            linha.Append(p);
        }
        if (linha.Length > 0) sb.AppendLine(linha.ToString());
        return sb.ToString().TrimEnd();
    }

    private static string FormatarCnpj(string cnpj)
    {
        string d = System.Text.RegularExpressions.Regex.Replace(cnpj, @"\D", "");
        if (d.Length == 14)
            return $"{d.Substring(0, 2)}.{d.Substring(2, 3)}.{d.Substring(5, 3)}/{d.Substring(8, 4)}-{d.Substring(12, 2)}";
        return cnpj;
    }

    private static string FormatarCpfCnpj(string doc)
    {
        string d = System.Text.RegularExpressions.Regex.Replace(doc, @"\D", "");
        if (d.Length == 11)
            return $"{d.Substring(0, 3)}.{d.Substring(3, 3)}.{d.Substring(6, 3)}-{d.Substring(9, 2)}";
        if (d.Length == 14)
            return $"{d.Substring(0, 2)}.{d.Substring(2, 3)}.{d.Substring(5, 3)}/{d.Substring(8, 4)}-{d.Substring(12, 2)}";
        return doc;
    }
}
