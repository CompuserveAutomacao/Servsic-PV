using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Dapper;
using ServSic.Core.Data;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

/// <summary>
/// Serviço de formatação de cupom não-fiscal para bobina térmica (80mm ou 58mm).
/// Gera texto puro alinhado com separadores e campos padronizados, respeitando rigorosamente
/// a largura em colunas sem estourar linhas ou truncar descrições de produtos.
/// </summary>
public class CupomFormatadorService
{
    private static readonly CultureInfo PtBr = new("pt-BR");

    public string GerarTextoCupom(PedidoVenda pedido, DadosEmpresa empresa, ConfiguracaoImpressora config)
    {
        int colunas = config.LarguraColunas > 0 ? config.LarguraColunas : 42;
        if (colunas >= 48) colunas = 42; // Protege contra estouro em impressoras 80mm de 42 colunas (Epson TM-T20, etc.)
        if (colunas < 32) colunas = 32;

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
        bool isVendaConcluida = pedido.Status == "V" || pedido.Venda == 1;
        string tipoDoc = isVendaConcluida 
            ? "COMPROVANTE DE VENDA" 
            : (pedido.IsOrcamento ? "ORCAMENTO DE VENDA" : "PEDIDO DE VENDA");
        sb.AppendLine(Centralizar($"*** {tipoDoc} ***", colunas));
        
        int numPedido = pedido.Pedido > 0 ? pedido.Pedido : pedido.Controle;
        sb.AppendLine(Centralizar($"NUMERO: {numPedido:D6}", colunas));
        sb.AppendLine(linhaDiv);

        // 3. Dados de Emissão, Vendedor e Caixa
        string dataHora = $"{pedido.Data:dd/MM/yyyy}  {pedido.HoraIni}";
        string statusCupom = isVendaConcluida ? "CONCLUIDA" : (pedido.IsOrcamento ? "ORC" : "PED");
        sb.AppendLine(AlinharEsquerdaDireita($"EMISSAO: {dataHora}", $"STATUS: {statusCupom}", colunas));
        
        string vendedor = !string.IsNullOrWhiteSpace(pedido.Vendedor) ? pedido.Vendedor : "BALCAO";
        string telVendRaw = ObterTelefoneVendedor(pedido);
        string telVendFormatado = FormatarTelefone(telVendRaw);

        sb.AppendLine(QuebrarTextoComPrefixo("VENDEDOR: ", vendedor, colunas));
        if (!string.IsNullOrWhiteSpace(telVendFormatado))
        {
            sb.AppendLine($"FONE VENDEDOR: {telVendFormatado}");
        }
        sb.AppendLine(linhaDiv);

        // 4. Identificação do Cliente
        string nomeCliente = !string.IsNullOrWhiteSpace(pedido.Cliente) ? pedido.Cliente : "CONSUMIDOR / BALCAO";
        sb.AppendLine(QuebrarTextoComPrefixo("CLIENTE: ", nomeCliente, colunas));

        if (!string.IsNullOrWhiteSpace(pedido.CPF_CNPJ))
            sb.AppendLine($"CPF/CNPJ: {FormatarCpfCnpj(pedido.CPF_CNPJ)}");

        if (!string.IsNullOrWhiteSpace(pedido.Telefone))
            sb.AppendLine($"FONE: {FormatarTelefone(pedido.Telefone)}");

        if (!string.IsNullOrWhiteSpace(pedido.ClienteRefBan))
            sb.AppendLine(QuebrarTextoComPrefixo("REF: ", pedido.ClienteRefBan.Trim(), colunas));

        if (!string.IsNullOrWhiteSpace(pedido.ClienteObs))
            sb.AppendLine(QuebrarTextoComPrefixo("OBS CLIENTE: ", pedido.ClienteObs.Trim(), colunas));

        // Endereço de Entrega ou Endereço do Cliente
        if (pedido.TemEnderecoEntrega || pedido.Entrega == 1)
        {
            sb.AppendLine(linhaDiv);
            sb.AppendLine(Centralizar("*** ENDERECO PARA ENTREGA ***", colunas));
            string endCompleto = !string.IsNullOrWhiteSpace(pedido.EnderecoCompletoFormatado)
                ? pedido.EnderecoCompletoFormatado
                : (pedido.Endereco ?? string.Empty);

            if (!string.IsNullOrWhiteSpace(endCompleto))
            {
                sb.AppendLine(QuebrarTexto(endCompleto, colunas));
            }

            if (!string.IsNullOrWhiteSpace(pedido.Telefone))
            {
                sb.AppendLine($"CONTATO / FONE: {FormatarTelefone(pedido.Telefone)}");
            }
            if (!string.IsNullOrWhiteSpace(pedido.Obs))
            {
                sb.AppendLine(QuebrarTexto($"REF/OBS: {pedido.Obs.Trim()}", colunas));
            }
        }
        else if (!string.IsNullOrWhiteSpace(pedido.Endereco))
        {
            string end = pedido.Endereco;
            if (!string.IsNullOrWhiteSpace(pedido.Numero)) end += $", {pedido.Numero}";
            if (!string.IsNullOrWhiteSpace(pedido.Bairro)) end += $" - {pedido.Bairro}";
            sb.AppendLine(QuebrarTextoComPrefixo("END: ", end, colunas));
        }

        sb.AppendLine(linhaDiv);

        // 5. Tabela de Itens
        if (colunas >= 40)
        {
            sb.AppendLine("ITEM  CODIGO         DESCRICAO");
            sb.AppendLine(AlinharEsquerdaDireita("      QTD x VL.UNIT(R$)", "TOTAL(R$)", colunas));
        }
        else
        {
            sb.AppendLine("IT CODIGO");
            sb.AppendLine("DESCRICAO");
            sb.AppendLine(AlinharEsquerdaDireita("QTD x UNIT", "TOTAL", colunas));
        }
        sb.AppendLine(linhaDiv);

        int seq = 1;
        foreach (var item in pedido.Itens)
        {
            string cod = !string.IsNullOrWhiteSpace(item.CodigoExibicao) ? item.CodigoExibicao.Trim() : item.LkProduto.ToString();
            string nomeProd = item.Produto?.Trim() ?? "PRODUTO";

            if (colunas >= 40)
            {
                // Prefixo de 21 caracteres exatos: seq(3) + esp(3) + cod(13) + esp(2) = 21 caracteres
                string codFormatado = Truncar(cod, 13).PadRight(13);
                string prefixo = $"{seq:D3}   {codFormatado}  ";
                int largDesc = Math.Max(15, colunas - 21);

                var linhasDesc = QuebrarLinhas(nomeProd, largDesc);
                if (linhasDesc.Count == 0) linhasDesc.Add(nomeProd);

                // Primeira linha com número, código e início da descrição
                sb.AppendLine($"{prefixo}{linhasDesc[0]}");

                // Linhas subsequentes com continuação da descrição alinhada na coluna da descrição
                string indentDesc = new string(' ', 21);
                for (int i = 1; i < linhasDesc.Count; i++)
                {
                    sb.AppendLine($"{indentDesc}{linhasDesc[i]}");
                }

                // Linha com quantidade, valor unitário e total do item
                string qtdUnit = $"{item.Quantidade:N3} {item.Unidade} x {item.VlUnitario:N2}";
                string totItem = item.Total.ToString("N2", PtBr);
                string linhaQtd = $"      {qtdUnit}";

                // Se couber na mesma linha com folga segura (mínimo 5 espaços) e não for valor alto (>= 1000)
                if (item.Total < 1000m && item.Quantidade < 1000m && (linhaQtd.Length + totItem.Length + 5 <= colunas))
                {
                    sb.AppendLine(AlinharEsquerdaDireita(linhaQtd, totItem, colunas));
                }
                else
                {
                    // Para valores altos, quantidades grandes ou bobinas onde não há espaço seguro:
                    // Linha 1: Quantidade x Unitário
                    // Linha 2: Total do Item à direita com indicação clara, sem risco de corte
                    sb.AppendLine(linhaQtd);
                    sb.AppendLine(AlinharEsquerdaDireita("      TOTAL ITEM (R$):", totItem, colunas));
                }

                if (item.Desconto > 0)
                {
                    decimal percDesc = item.PercentualDesconto;
                    string descTexto = percDesc > 0
                        ? $"      DESCONTO ITEM ({percDesc:N2}%):"
                        : "      DESCONTO ITEM:";
                    sb.AppendLine(AlinharEsquerdaDireita(descTexto, $"- {item.Desconto:N2}", colunas));
                }
            }
            else
            {
                sb.AppendLine($"{seq:D2} {cod}");
                var linhasDesc = QuebrarLinhas(nomeProd, colunas - 3);
                if (linhasDesc.Count == 0) linhasDesc.Add(nomeProd);
                foreach (var ld in linhasDesc)
                {
                    sb.AppendLine($"   {ld}");
                }

                string qtdUnit = $"{item.Quantidade:N2} {item.Unidade} x {item.VlUnitario:N2}";
                string totItem = item.Total.ToString("N2", PtBr);
                string linhaQtd = $"   {qtdUnit}";

                if (item.Total < 1000m && item.Quantidade < 100m && (linhaQtd.Length + totItem.Length + 3 <= colunas))
                {
                    sb.AppendLine(AlinharEsquerdaDireita(linhaQtd, totItem, colunas));
                }
                else
                {
                    sb.AppendLine(linhaQtd);
                    sb.AppendLine(AlinharEsquerdaDireita("   TOTAL (R$):", totItem, colunas));
                }

                if (item.Desconto > 0)
                {
                    sb.AppendLine(AlinharEsquerdaDireita("   DESC. ITEM:", $"- {item.Desconto:N2}", colunas));
                }
            }
            seq++;
        }

        sb.AppendLine(linhaDiv);

        // 6. Resumo Financeiro
        int totalQtdItens = pedido.Itens.Count;
        sb.AppendLine(AlinharEsquerdaDireita("QTD. TOTAL DE ITENS:", totalQtdItens.ToString(), colunas));
        sb.AppendLine(AlinharEsquerdaDireita("SUBTOTAL (R$):", pedido.Subtotal.ToString("N2", PtBr), colunas));

        decimal totalDescontosItens = System.Linq.Enumerable.Sum(pedido.Itens, i => i.Desconto);
        if (totalDescontosItens > 0)
        {
            sb.AppendLine(AlinharEsquerdaDireita("DESC. ITENS (R$):", $"- {totalDescontosItens:N2}", colunas));
        }

        if (pedido.Desconto > 0)
            sb.AppendLine(AlinharEsquerdaDireita("DESCONTO GERAL (R$):", $"- {pedido.Desconto:N2}", colunas));

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

    public string GerarTextoCupomEntrega(PedidoVenda pedido, DadosEmpresa empresa, ConfiguracaoImpressora config)
    {
        int colunas = config.LarguraColunas > 0 ? config.LarguraColunas : 42;
        if (colunas >= 48) colunas = 42;
        if (colunas < 32) colunas = 32;

        string linhaDiv = new('-', colunas);
        string linhaDupla = new('=', colunas);

        var sb = new StringBuilder();

        // 1. Cabeçalho da Empresa
        if (!string.IsNullOrWhiteSpace(empresa.NomeFantasia))
            sb.AppendLine(Centralizar(empresa.NomeFantasia.ToUpperInvariant(), colunas));

        if (!string.IsNullOrWhiteSpace(empresa.Telefone))
            sb.AppendLine(Centralizar($"FONE: {empresa.Telefone}", colunas));

        sb.AppendLine(linhaDupla);

        // 2. Identificação da Ordem de Entrega
        sb.AppendLine(Centralizar("*** ORDEM DE ENTREGA / SEPARACAO ***", colunas));
        int numPedido = pedido.Pedido > 0 ? pedido.Pedido : pedido.Controle;
        sb.AppendLine(Centralizar($"PEDIDO: {numPedido:D6}", colunas));
        sb.AppendLine(linhaDiv);

        // 3. Dados de Emissão e Vendedor
        string dataHora = $"{pedido.Data:dd/MM/yyyy}  {pedido.HoraIni}";
        string vendEntrega = !string.IsNullOrWhiteSpace(pedido.Vendedor) ? pedido.Vendedor : "BALCAO";
        string telVendEnt = FormatarTelefone(ObterTelefoneVendedor(pedido));
        if (!string.IsNullOrWhiteSpace(telVendEnt))
        {
            vendEntrega += $" ({telVendEnt})";
        }
        sb.AppendLine(AlinharEsquerdaDireita($"EMISSAO: {dataHora}", $"VEND: {Truncar(vendEntrega, Math.Max(10, colunas - 24))}", colunas));

        // 4. Identificação do Cliente
        string nomeCli = !string.IsNullOrWhiteSpace(pedido.Cliente) ? pedido.Cliente : "CONSUMIDOR / BALCAO";
        sb.AppendLine(QuebrarTextoComPrefixo("CLIENTE: ", nomeCli, colunas));
        if (!string.IsNullOrWhiteSpace(pedido.Telefone))
            sb.AppendLine($"FONE: {FormatarTelefone(pedido.Telefone)}");

        sb.AppendLine(linhaDiv);

        // 5. LOCAL / ENDEREÇO DE ENTREGA
        sb.AppendLine(Centralizar("LOCAL / ENDERECO DE ENTREGA", colunas));
        string endCompleto = pedido.EnderecoCompletoFormatado;
        if (!string.IsNullOrWhiteSpace(endCompleto))
        {
            sb.AppendLine(QuebrarTexto(endCompleto, colunas));
        }
        else
        {
            sb.AppendLine("ENDERECO NAO INFORMADO NO PEDIDO");
        }

        if (!string.IsNullOrWhiteSpace(pedido.Telefone))
        {
            sb.AppendLine($"CONTATO / FONE: {FormatarTelefone(pedido.Telefone)}");
        }

        if (!string.IsNullOrWhiteSpace(pedido.Obs))
        {
            sb.AppendLine(QuebrarTexto($"OBS/REF: {pedido.Obs.Trim()}", colunas));
        }

        sb.AppendLine(linhaDiv);

        // 6. Itens Marcados para Entrega
        sb.AppendLine(Centralizar("ITENS PARA ENTREGA", colunas));
        if (colunas >= 40)
        {
            sb.AppendLine("ITEM  CODIGO         DESCRICAO");
            sb.AppendLine(AlinharEsquerdaDireita("      UN", "QUANTIDADE", colunas));
        }
        else
        {
            sb.AppendLine("IT CODIGO");
            sb.AppendLine("DESCRICAO / UN / QTD");
        }
        sb.AppendLine(linhaDiv);

        var itensEntrega = System.Linq.Enumerable.ToList(System.Linq.Enumerable.Where(pedido.Itens, i => i.Entrega == 1));
        if (itensEntrega.Count == 0) itensEntrega = System.Linq.Enumerable.ToList(pedido.Itens);

        int seq = 1;
        decimal totalQtd = 0m;
        foreach (var item in itensEntrega)
        {
            totalQtd += item.Quantidade;
            string cod = !string.IsNullOrWhiteSpace(item.CodigoExibicao) ? item.CodigoExibicao.Trim() : item.LkProduto.ToString();
            string nomeProd = item.Produto?.Trim() ?? "PRODUTO";
            string un = !string.IsNullOrWhiteSpace(item.Unidade) ? item.Unidade.Trim() : "UN";
            string qtd = item.Quantidade.ToString("N0", PtBr);

            if (colunas >= 40)
            {
                string codFormatado = Truncar(cod, 13).PadRight(13);
                string prefixo = $"{seq:D3}   {codFormatado}  ";
                int largDesc = Math.Max(15, colunas - 21);

                var linhasDesc = QuebrarLinhas(nomeProd, largDesc);
                if (linhasDesc.Count == 0) linhasDesc.Add(nomeProd);

                sb.AppendLine($"{prefixo}{linhasDesc[0]}");

                string indentDesc = new string(' ', 21);
                for (int i = 1; i < linhasDesc.Count; i++)
                {
                    sb.AppendLine($"{indentDesc}{linhasDesc[i]}");
                }

                sb.AppendLine(AlinharEsquerdaDireita($"      UN: {un}", $"QTD: {qtd}", colunas));
            }
            else
            {
                sb.AppendLine($"{seq:D2} {cod}");
                var linhasDesc = QuebrarLinhas(nomeProd, colunas - 3);
                if (linhasDesc.Count == 0) linhasDesc.Add(nomeProd);
                foreach (var ld in linhasDesc)
                {
                    sb.AppendLine($"   {ld}");
                }
                sb.AppendLine(AlinharEsquerdaDireita($"   UN: {un}", $"QTD: {qtd}", colunas));
            }
            seq++;
        }

        sb.AppendLine(linhaDiv);
        sb.AppendLine(AlinharEsquerdaDireita("TOTAL DE VOLUMES / ITENS:", totalQtd.ToString("N0", PtBr), colunas));
        sb.AppendLine(linhaDupla);

        // 7. Campo de Assinatura e Conferência
        sb.AppendLine();
        sb.AppendLine(Centralizar("RECEBIDO POR: ____________________________", colunas));
        sb.AppendLine();
        sb.AppendLine(Centralizar("DATA: _____/_____/_________  RG: _________", colunas));
        sb.AppendLine();

        return sb.ToString();
    }

    public static List<string> QuebrarLinhas(string? texto, int larguraMax)
    {
        var resultado = new List<string>();
        if (string.IsNullOrWhiteSpace(texto) || larguraMax <= 0)
            return resultado;

        // Normaliza quebras de linha existentes
        var paragrafos = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');

        foreach (var paragrafo in paragrafos)
        {
            if (string.IsNullOrWhiteSpace(paragrafo))
            {
                continue;
            }

            var palavras = paragrafo.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            var linhaAtual = new StringBuilder();

            foreach (var palavra in palavras)
            {
                // Se a palavra por si só for maior que a largura máxima, fatia a palavra
                if (palavra.Length > larguraMax)
                {
                    if (linhaAtual.Length > 0)
                    {
                        resultado.Add(linhaAtual.ToString());
                        linhaAtual.Clear();
                    }

                    int pos = 0;
                    while (pos < palavra.Length)
                    {
                        int pedacoLen = Math.Min(larguraMax, palavra.Length - pos);
                        string pedaco = palavra.Substring(pos, pedacoLen);
                        pos += pedacoLen;

                        if (pos < palavra.Length)
                        {
                            resultado.Add(pedaco);
                        }
                        else
                        {
                            linhaAtual.Append(pedaco);
                        }
                    }
                    continue;
                }

                int tamanhoProposto = linhaAtual.Length == 0 ? palavra.Length : linhaAtual.Length + 1 + palavra.Length;

                if (tamanhoProposto <= larguraMax)
                {
                    if (linhaAtual.Length > 0) linhaAtual.Append(' ');
                    linhaAtual.Append(palavra);
                }
                else
                {
                    resultado.Add(linhaAtual.ToString());
                    linhaAtual.Clear();
                    linhaAtual.Append(palavra);
                }
            }

            if (linhaAtual.Length > 0)
            {
                resultado.Add(linhaAtual.ToString());
            }
        }

        return resultado;
    }

    public static string QuebrarTexto(string? texto, int largura, string indentacao = "")
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        int larguraUtil = largura - indentacao.Length;
        if (larguraUtil <= 0) larguraUtil = largura;

        var linhas = QuebrarLinhas(texto, larguraUtil);
        if (linhas.Count == 0) return string.Empty;

        var sb = new StringBuilder();
        for (int i = 0; i < linhas.Count; i++)
        {
            if (i > 0 && !string.IsNullOrEmpty(indentacao))
            {
                sb.AppendLine($"{indentacao}{linhas[i]}");
            }
            else
            {
                sb.AppendLine(linhas[i]);
            }
        }
        return sb.ToString().TrimEnd();
    }

    public static string QuebrarTextoComPrefixo(string prefixo, string texto, int largura)
    {
        if (string.IsNullOrWhiteSpace(texto)) return prefixo.TrimEnd();
        string textoCompleto = prefixo + texto.Trim();
        if (textoCompleto.Length <= largura) return textoCompleto;

        string indentacao = new string(' ', prefixo.Length);
        int larguraDesc = largura - prefixo.Length;
        if (larguraDesc <= 5)
        {
            return QuebrarTexto(textoCompleto, largura);
        }

        var linhas = QuebrarLinhas(texto.Trim(), larguraDesc);
        if (linhas.Count == 0) return prefixo.TrimEnd();

        var sb = new StringBuilder();
        sb.AppendLine($"{prefixo}{linhas[0]}");
        for (int i = 1; i < linhas.Count; i++)
        {
            sb.AppendLine($"{indentacao}{linhas[i]}");
        }
        return sb.ToString().TrimEnd();
    }

    public static string Centralizar(string texto, int largura)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        if (texto.Length >= largura) return texto.Substring(0, largura);
        int espacos = (largura - texto.Length) / 2;
        return texto.PadLeft(texto.Length + espacos).PadRight(largura);
    }

    public static string AlinharEsquerdaDireita(string esquerda, string direita, int largura)
    {
        direita ??= string.Empty;
        esquerda ??= string.Empty;

        // Se ambos cabem na mesma linha com pelo menos 1 espaço de separação
        if (esquerda.Length + direita.Length + 1 <= largura)
        {
            int espacos = largura - esquerda.Length - direita.Length;
            return esquerda + new string(' ', espacos) + direita;
        }

        // Se NÃO cabem na mesma linha (ex: valores muito altos ou descrições/quantidades longas),
        // NUNCA corta os valores! Quebra em duas linhas para preservar a integridade total dos dados:
        // Linha 1: Texto da esquerda
        // Linha 2: Valor da direita alinhado à margem direita
        string direitaAlinhada = direita.Length >= largura ? direita : direita.PadLeft(largura);
        return $"{esquerda}{Environment.NewLine}{direitaAlinhada}";
    }

    public static string Truncar(string? texto, int max)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        return texto.Length <= max ? texto : texto.Substring(0, max);
    }

    public static string ObterTelefoneVendedor(PedidoVenda pedido)
    {
        string cel = pedido.VendedorCelular ?? string.Empty;
        string tel = pedido.VendedorTelefone ?? string.Empty;

        // Se ambos vazios mas houver LkVendedor, tenta buscar no banco via fallback rápido
        if (string.IsNullOrWhiteSpace(cel) && string.IsNullOrWhiteSpace(tel) && pedido.LkVendedor.HasValue && pedido.LkVendedor.Value > 0)
        {
            try
            {
                using var conn = DbConnectionFactory.CreateConnection();
                var dadosVend = conn.QueryFirstOrDefault<(string? Celular, string? Telefone)>(
                    "SELECT TOP 1 Celular, Telefone FROM TabVend WHERE CONTROLE = @lk",
                    new { lk = pedido.LkVendedor.Value });
                cel = dadosVend.Celular ?? string.Empty;
                tel = dadosVend.Telefone ?? string.Empty;
            }
            catch { }
        }

        string celDig = System.Text.RegularExpressions.Regex.Replace(cel, @"\D", "");
        string telDig = System.Text.RegularExpressions.Regex.Replace(tel, @"\D", "");

        if (celDig.Length >= 10) return celDig;
        if (telDig.Length >= 10) return telDig;

        if (telDig.Length == 2 && (celDig.Length == 8 || celDig.Length == 9))
            return telDig + celDig;

        if (celDig.Length >= 8) return celDig;
        if (telDig.Length >= 8) return telDig;

        return !string.IsNullOrWhiteSpace(cel) ? cel : tel;
    }

    public static string FormatarTelefone(string? tel)
    {
        if (string.IsNullOrWhiteSpace(tel)) return string.Empty;
        string d = System.Text.RegularExpressions.Regex.Replace(tel, @"\D", "");
        if (d.Length == 11)
            return $"({d.Substring(0, 2)}) {d.Substring(2, 5)}-{d.Substring(7, 4)}";
        if (d.Length == 10)
            return $"({d.Substring(0, 2)}) {d.Substring(2, 4)}-{d.Substring(6, 4)}";
        if (d.Length == 9)
            return $"{d.Substring(0, 5)}-{d.Substring(5, 4)}";
        if (d.Length == 8)
            return $"{d.Substring(0, 4)}-{d.Substring(4, 4)}";
        return tel.Trim();
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
