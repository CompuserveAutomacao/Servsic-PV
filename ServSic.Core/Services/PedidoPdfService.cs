using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ServSic.Core.Models;

namespace ServSic.Core.Services;

public class PedidoPdfService
{
    static PedidoPdfService()
    {
        // Define a licença gratuita comunitária para o QuestPDF 2025/2026
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;
        QuestPDF.Settings.ThrowOnMissingFontFamilies = false;
    }

    /// <summary>
    /// Gera o documento PDF em memória e retorna o array de bytes.
    /// </summary>
    public byte[] GerarPdfBytes(PedidoVenda pedido, DadosEmpresa empresa)
    {
        var document = new PedidoDocument(pedido, empresa);
        return document.GeneratePdf();
    }

    /// <summary>
    /// Gera e grava o documento PDF diretamente no arquivo especificado.
    /// </summary>
    public async Task SalvarPdfArquivoAsync(PedidoVenda pedido, DadosEmpresa empresa, string caminhoArquivo)
    {
        var bytes = GerarPdfBytes(pedido, empresa);
        var dir = Path.GetDirectoryName(caminhoArquivo);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }
        await File.WriteAllBytesAsync(caminhoArquivo, bytes);
    }
}

internal class PedidoDocument : IDocument
{
    private readonly PedidoVenda _pedido;
    private readonly DadosEmpresa _empresa;
    private readonly CultureInfo _ptBr = new("pt-BR");

    // Paleta Corporativa ServSic (Azul Navy + Azul Primário + Cinza Suave)
    private const string CorNavy = "#0B192C";
    private const string CorAzul = "#0284C7";
    private const string CorTexto = "#1E293B";
    private const string CorTextoSubtle = "#64748B";
    private const string CorBorda = "#E2E8F0";
    private const string CorFundoZebrado = "#F8FAFC";
    private const string CorFundoCabecalhoSecao = "#F1F5F9";

    public PedidoDocument(PedidoVenda pedido, DadosEmpresa empresa)
    {
        _pedido = pedido;
        _empresa = empresa;
    }

    public DocumentMetadata GetMetadata() => new()
    {
        Title = $"{TituloDocumento} Nº {_pedido.Pedido}",
        Author = _empresa.NomeFantasia,
        Subject = $"{TituloDocumento} - Cliente: {_pedido.Cliente}",
        CreationDate = DateTime.Now
    };

    private string TituloDocumento => _pedido.IsOrcamento ? "ORÇAMENTO DE VENDA" : "PEDIDO DE VENDA";

    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginTop(20);
            page.MarginBottom(20);
            page.MarginLeft(24);
            page.MarginRight(24);
            page.DefaultTextStyle(x => x.FontFamily("Segoe UI").FontSize(8.5f).FontColor(CorTexto));

            page.Header().Element(ComposeHeader);
            page.Content().Element(ComposeContent);
            page.Footer().Element(ComposeFooter);
        });
    }

    private void ComposeHeader(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Row(row =>
            {
                // Coluna Esquerda: Dados da Empresa
                row.RelativeItem(3).Column(c =>
                {
                    string nomeFantasia = !string.IsNullOrWhiteSpace(_empresa.NomeFantasia) 
                        ? _empresa.NomeFantasia 
                        : "COMPUSERVE SISTEMAS";

                    c.Item().Text(nomeFantasia)
                        .FontSize(14)
                        .Bold()
                        .FontColor(CorNavy);

                    if (!string.IsNullOrWhiteSpace(_empresa.RazaoSocial) && _empresa.RazaoSocial != _empresa.NomeFantasia)
                    {
                        c.Item().Text(_empresa.RazaoSocial).FontSize(8).FontColor(CorTextoSubtle);
                    }

                    // CNPJ e Inscrição Estadual
                    string cnpjIe = string.Empty;
                    if (!string.IsNullOrWhiteSpace(_empresa.Cnpj)) cnpjIe += $"CNPJ: {_empresa.Cnpj}";
                    if (!string.IsNullOrWhiteSpace(_empresa.InscricaoEstadual))
                    {
                        if (cnpjIe.Length > 0) cnpjIe += "  •  ";
                        cnpjIe += $"IE: {_empresa.InscricaoEstadual}";
                    }
                    if (cnpjIe.Length > 0)
                    {
                        c.Item().Text(cnpjIe).FontSize(8).FontColor(CorTextoSubtle);
                    }

                    // Endereço e Cidade
                    string endLinha = _empresa.EnderecoLinha1;
                    if (!string.IsNullOrWhiteSpace(endLinha))
                    {
                        c.Item().Text(endLinha).FontSize(8).FontColor(CorTextoSubtle);
                    }
                    string cidLinha = _empresa.EnderecoLinha2;
                    if (!string.IsNullOrWhiteSpace(cidLinha))
                    {
                        c.Item().Text(cidLinha).FontSize(8).FontColor(CorTextoSubtle);
                    }

                    // Telefone
                    if (!string.IsNullOrWhiteSpace(_empresa.Telefone))
                    {
                        c.Item().Text($"Telefone: {_empresa.Telefone}").FontSize(8).Bold().FontColor(CorAzul);
                    }
                });

                // Coluna Direita: Box do Pedido / Orçamento em Navy
                row.RelativeItem(2).AlignRight().Width(190).Border(1).BorderColor(CorBorda).CornerRadius(6).Background(CorNavy).Padding(8).Column(c =>
                {
                    c.Item().Text(TituloDocumento)
                        .FontSize(10)
                        .Bold()
                        .FontColor("#FFFFFF")
                        .AlignCenter();

                    c.Item().Text($"Nº {_pedido.Pedido}")
                        .FontSize(16)
                        .Bold()
                        .FontColor("#FFFFFF")
                        .AlignCenter();

                    c.Item().LineHorizontal(1).LineColor("#334155");

                    c.Item().PaddingTop(3).Row(r =>
                    {
                        r.RelativeItem().Text("Emissão:").FontSize(7.5f).FontColor("#94A3B8");
                        r.RelativeItem().Text($"{_pedido.DataFormatada} {_pedido.HoraIni}".Trim())
                            .FontSize(7.5f).Bold().FontColor("#FFFFFF").AlignRight();
                    });

                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Vendedor:").FontSize(7.5f).FontColor("#94A3B8");
                        string vend = !string.IsNullOrWhiteSpace(_pedido.Vendedor) ? _pedido.Vendedor : "BALCÃO";
                        r.RelativeItem().Text(vend).FontSize(7.5f).Bold().FontColor("#FFFFFF").AlignRight();
                    });

                    c.Item().Row(r =>
                    {
                        r.RelativeItem().Text("Situação:").FontSize(7.5f).FontColor("#94A3B8");
                        r.RelativeItem().Text(_pedido.SituacaoDescricao)
                            .FontSize(7.5f).Bold().FontColor("#FFFFFF").AlignRight();
                    });
                });
            });

            // Linha divisória suave
            col.Item().PaddingTop(8).PaddingBottom(6).LineHorizontal(1.5f).LineColor(CorAzul);
        });
    }

    private void ComposeContent(IContainer container)
    {
        container.Column(col =>
        {
            // 1. Dados do Cliente
            col.Item().Element(ComposeCliente);

            // 2. Dados de Entrega (se aplicável)
            if (_pedido.TemItensEntrega || !string.IsNullOrWhiteSpace(_pedido.Endereco))
            {
                col.Item().PaddingTop(6).Element(ComposeEntrega);
            }

            // 3. Tabela de Itens
            col.Item().PaddingTop(8).Element(ComposeTabelaItens);

            // 4. Formas de Pagamento (se houver)
            if (_pedido.Pagamentos.Count > 0)
            {
                col.Item().PaddingTop(8).Element(ComposePagamentos);
            }

            // 5. Bloco de Observações e Resumo de Totais
            col.Item().PaddingTop(8).Element(ComposeTotaisEObservacoes);

            // 6. Bloco de Assinaturas
            col.Item().PaddingTop(18).Element(ComposeAssinaturas);
        });
    }

    private void ComposeCliente(IContainer container)
    {
        container.Border(1).BorderColor(CorBorda).CornerRadius(4).Column(c =>
        {
            // Cabeçalho da seção
            c.Item().Background(CorFundoCabecalhoSecao).Padding(4).PaddingLeft(8).Row(r =>
            {
                r.RelativeItem().Text("DADOS DO CLIENTE").FontSize(8.5f).Bold().FontColor(CorNavy);
            });

            // Corpo
            c.Item().Padding(6).Column(dados =>
            {
                dados.Item().Row(r =>
                {
                    string codCli = !string.IsNullOrWhiteSpace(_pedido.Codigo) 
                        ? _pedido.Codigo 
                        : (_pedido.LkCliente.HasValue ? _pedido.LkCliente.Value.ToString() : "-");

                    r.RelativeItem(5).Text(t =>
                    {
                        t.Span("Cliente / Razão: ").Bold();
                        t.Span(!string.IsNullOrWhiteSpace(_pedido.Cliente) ? _pedido.Cliente : "Consumidor Final");
                    });

                    r.RelativeItem(2).Text(t =>
                    {
                        t.Span("Cód: ").Bold();
                        t.Span(codCli);
                    });

                    r.RelativeItem(3).AlignRight().Text(t =>
                    {
                        t.Span("CPF/CNPJ: ").Bold();
                        t.Span(!string.IsNullOrWhiteSpace(_pedido.CPF_CNPJ) ? _pedido.CPF_CNPJ : "-");
                    });
                });

                dados.Item().PaddingTop(2).Row(r =>
                {
                    r.RelativeItem(5).Text(t =>
                    {
                        t.Span("Endereço: ").Bold();
                        string end = _pedido.EnderecoCompletoFormatado;
                        t.Span(!string.IsNullOrWhiteSpace(end) ? end : "Não informado");
                    });

                    r.RelativeItem(2).Text(t =>
                    {
                        t.Span("Telefone: ").Bold();
                        t.Span(!string.IsNullOrWhiteSpace(_pedido.Telefone) ? _pedido.Telefone : "-");
                    });

                    r.RelativeItem(3).AlignRight().Text(t =>
                    {
                        t.Span("E-mail: ").Bold();
                        t.Span(!string.IsNullOrWhiteSpace(_pedido.Email) ? _pedido.Email : "-");
                    });
                });
            });
        });
    }

    private void ComposeEntrega(IContainer container)
    {
        container.Border(1).BorderColor(CorBorda).CornerRadius(4).Column(c =>
        {
            c.Item().Background(CorFundoCabecalhoSecao).Padding(3).PaddingLeft(8).Row(r =>
            {
                r.RelativeItem().Text("DADOS DE ENTREGA / TRANSPORTE").FontSize(8).Bold().FontColor(CorNavy);
                if (_pedido.DataEntrega.HasValue)
                {
                    r.RelativeItem().Text($"Previsão de Entrega: {_pedido.DataEntrega.Value:dd/MM/yyyy}").FontSize(8).Bold().FontColor(CorAzul).AlignRight();
                }
            });

            c.Item().Padding(4).PaddingLeft(8).PaddingRight(8).Row(r =>
            {
                r.RelativeItem().Text(t =>
                {
                    t.Span("Local de Entrega: ").Bold();
                    t.Span(_pedido.EnderecoCompletoFormatado);
                });
            });
        });
    }

    private void ComposeTabelaItens(IContainer container)
    {
        container.Column(col =>
        {
            col.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(22); // #
                    columns.ConstantColumn(58); // Código
                    columns.RelativeColumn(6);  // Descrição
                    columns.ConstantColumn(30); // UN
                    columns.ConstantColumn(45); // Qtd
                    columns.ConstantColumn(60); // Unitário
                    columns.ConstantColumn(50); // Desconto
                    columns.ConstantColumn(68); // Total
                });

                // Header da Tabela em Navy
                table.Header(header =>
                {
                    header.Cell().Background(CorNavy).Padding(4).Text("#").FontSize(8).Bold().FontColor("#FFFFFF").AlignCenter();
                    header.Cell().Background(CorNavy).Padding(4).Text("CÓDIGO").FontSize(8).Bold().FontColor("#FFFFFF");
                    header.Cell().Background(CorNavy).Padding(4).Text("DESCRIÇÃO DO PRODUTO").FontSize(8).Bold().FontColor("#FFFFFF");
                    header.Cell().Background(CorNavy).Padding(4).Text("UN").FontSize(8).Bold().FontColor("#FFFFFF").AlignCenter();
                    header.Cell().Background(CorNavy).Padding(4).Text("QTD").FontSize(8).Bold().FontColor("#FFFFFF").AlignRight();
                    header.Cell().Background(CorNavy).Padding(4).Text("UNIT (R$)").FontSize(8).Bold().FontColor("#FFFFFF").AlignRight();
                    header.Cell().Background(CorNavy).Padding(4).Text("DESC (R$)").FontSize(8).Bold().FontColor("#FFFFFF").AlignRight();
                    header.Cell().Background(CorNavy).Padding(4).Text("TOTAL (R$)").FontSize(8).Bold().FontColor("#FFFFFF").AlignRight();
                });

                // Linhas de Produtos
                int index = 1;
                foreach (var item in _pedido.Itens)
                {
                    string fundo = (index % 2 == 0) ? CorFundoZebrado : "#FFFFFF";

                    table.Cell().Background(fundo).BorderBottom(1).BorderColor(CorBorda).Padding(4)
                        .Text(index.ToString()).FontSize(8).FontColor(CorTextoSubtle).AlignCenter();

                    string cod = !string.IsNullOrWhiteSpace(item.CodInterno) ? item.CodInterno : item.LkProduto.ToString();
                    table.Cell().Background(fundo).BorderBottom(1).BorderColor(CorBorda).Padding(4)
                        .Text(cod).FontSize(8);

                    table.Cell().Background(fundo).BorderBottom(1).BorderColor(CorBorda).Padding(4)
                        .Text(item.Produto).FontSize(8).Bold();

                    string unid = !string.IsNullOrWhiteSpace(item.Unidade) ? item.Unidade : "UN";
                    table.Cell().Background(fundo).BorderBottom(1).BorderColor(CorBorda).Padding(4)
                        .Text(unid).FontSize(8).AlignCenter();

                    table.Cell().Background(fundo).BorderBottom(1).BorderColor(CorBorda).Padding(4)
                        .Text(item.Quantidade.ToString("N2", _ptBr)).FontSize(8).AlignRight();

                    table.Cell().Background(fundo).BorderBottom(1).BorderColor(CorBorda).Padding(4)
                        .Text(item.VlUnitario.ToString("N2", _ptBr)).FontSize(8).AlignRight();

                    string descStr = item.Desconto > 0 ? item.Desconto.ToString("N2", _ptBr) : "-";
                    table.Cell().Background(fundo).BorderBottom(1).BorderColor(CorBorda).Padding(4)
                        .Text(descStr).FontSize(8).FontColor(item.Desconto > 0 ? "#DC2626" : CorTextoSubtle).AlignRight();

                    table.Cell().Background(fundo).BorderBottom(1).BorderColor(CorBorda).Padding(4)
                        .Text(item.Total.ToString("N2", _ptBr)).FontSize(8).Bold().AlignRight();

                    index++;
                }
            });
        });
    }

    private void ComposePagamentos(IContainer container)
    {
        container.Border(1).BorderColor(CorBorda).CornerRadius(4).Column(c =>
        {
            c.Item().Background(CorFundoCabecalhoSecao).Padding(3).PaddingLeft(8).Row(r =>
            {
                r.RelativeItem().Text("PLANO DE PAGAMENTO / RECEBIMENTOS").FontSize(8).Bold().FontColor(CorNavy);
            });

            c.Item().Padding(4).Table(table =>
            {
                table.ColumnsDefinition(cols =>
                {
                    cols.ConstantColumn(50);
                    cols.RelativeColumn(3);
                    cols.RelativeColumn(2);
                    cols.RelativeColumn(2);
                });

                table.Header(h =>
                {
                    h.Cell().BorderBottom(1).BorderColor(CorBorda).Padding(3).Text("PARC").FontSize(7.5f).Bold();
                    h.Cell().BorderBottom(1).BorderColor(CorBorda).Padding(3).Text("FORMA DE PAGAMENTO").FontSize(7.5f).Bold();
                    h.Cell().BorderBottom(1).BorderColor(CorBorda).Padding(3).Text("VENCIMENTO").FontSize(7.5f).Bold().AlignCenter();
                    h.Cell().BorderBottom(1).BorderColor(CorBorda).Padding(3).Text("VALOR").FontSize(7.5f).Bold().AlignRight();
                });

                int pIdx = 1;
                foreach (var pag in _pedido.Pagamentos)
                {
                    string parcDesc = pag.Parcela > 0 ? $"{pag.Parcela}ª" : $"{pIdx}ª";
                    string vencDesc = pag.Vencimento != default ? pag.Vencimento.ToString("dd/MM/yyyy") : DateTime.Today.ToString("dd/MM/yyyy");

                    table.Cell().Padding(3).Text(parcDesc).FontSize(7.5f);
                    table.Cell().Padding(3).Text(pag.Recebimento ?? "DINHEIRO").FontSize(7.5f).Bold();
                    table.Cell().Padding(3).Text(vencDesc).FontSize(7.5f).AlignCenter();
                    table.Cell().Padding(3).Text(pag.Valor.ToString("C2", _ptBr)).FontSize(7.5f).Bold().AlignRight();
                    pIdx++;
                }
            });
        });
    }

    private void ComposeTotaisEObservacoes(IContainer container)
    {
        container.Row(row =>
        {
            // Observações e Validade à esquerda
            row.RelativeItem(3).Column(obsCol =>
            {
                if (!string.IsNullOrWhiteSpace(_pedido.Obs))
                {
                    obsCol.Item().Border(1).BorderColor(CorBorda).CornerRadius(4).Column(oc =>
                    {
                        oc.Item().Background(CorFundoCabecalhoSecao).Padding(3).PaddingLeft(6).Text("OBSERVAÇÕES DO PEDIDO").FontSize(7.5f).Bold().FontColor(CorNavy);
                        oc.Item().Padding(5).Text(_pedido.Obs).FontSize(7.5f);
                    });
                }

                if (_pedido.IsOrcamento)
                {
                    obsCol.Item().PaddingTop(4).Text("• Este orçamento tem validade de 3 (três) dias úteis a contar de sua emissão. Os preços e estoques estão sujeitos a confirmação no fechamento.")
                        .FontSize(7).Italic().FontColor(CorTextoSubtle);
                }
                else
                {
                    obsCol.Item().PaddingTop(4).Text("• Pedido de balcão / pré-venda sujeito à conferência e faturamento no caixa.")
                        .FontSize(7).Italic().FontColor(CorTextoSubtle);
                }
            });

            row.ConstantItem(12); // Espaço entre as colunas

            // Caixa de Totais à direita
            row.RelativeItem(2).Border(1).BorderColor(CorBorda).CornerRadius(4).Column(tCol =>
            {
                // Subtotal
                tCol.Item().Padding(4).PaddingLeft(8).PaddingRight(8).Row(r =>
                {
                    r.RelativeItem().Text("Subtotal dos Itens:").FontSize(8).FontColor(CorTextoSubtle);
                    r.RelativeItem().Text(_pedido.Subtotal.ToString("C2", _ptBr)).FontSize(8).Bold().AlignRight();
                });

                // Desconto
                if (_pedido.Desconto > 0)
                {
                    tCol.Item().PaddingHorizontal(8).PaddingBottom(4).Row(r =>
                    {
                        r.RelativeItem().Text("(-) Desconto:").FontSize(8).FontColor("#DC2626");
                        r.RelativeItem().Text($"- {_pedido.Desconto.ToString("C2", _ptBr)}").FontSize(8).Bold().FontColor("#DC2626").AlignRight();
                    });
                }

                // Acréscimo / Frete
                if (_pedido.Acrescimo > 0)
                {
                    tCol.Item().PaddingHorizontal(8).PaddingBottom(4).Row(r =>
                    {
                        r.RelativeItem().Text("(+) Acrésc./Frete:").FontSize(8).FontColor(CorTextoSubtle);
                        r.RelativeItem().Text(_pedido.Acrescimo.ToString("C2", _ptBr)).FontSize(8).Bold().AlignRight();
                    });
                }

                // Valor Total em destaque Navy
                tCol.Item().Background(CorNavy).Padding(6).PaddingLeft(8).PaddingRight(8).Row(r =>
                {
                    r.RelativeItem().AlignMiddle().Text("VALOR TOTAL:").FontSize(9.5f).Bold().FontColor("#FFFFFF");
                    r.RelativeItem().AlignRight().Text(_pedido.Total.ToString("C2", _ptBr)).FontSize(12).Bold().FontColor("#FFFFFF");
                });
            });
        });
    }

    private void ComposeAssinaturas(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Column(c =>
            {
                c.Item().LineHorizontal(1).LineColor(CorBorda);
                c.Item().PaddingTop(3).Text("Assinatura do Cliente").FontSize(7.5f).FontColor(CorTextoSubtle).AlignCenter();
            });

            row.ConstantItem(40);

            row.RelativeItem().Column(c =>
            {
                c.Item().LineHorizontal(1).LineColor(CorBorda);
                string vendNome = !string.IsNullOrWhiteSpace(_pedido.Vendedor) ? _pedido.Vendedor : "Vendedor Responsável";
                c.Item().PaddingTop(3).Text(vendNome).FontSize(7.5f).FontColor(CorTextoSubtle).AlignCenter();
            });
        });
    }

    private void ComposeFooter(IContainer container)
    {
        container.BorderTop(1).BorderColor(CorBorda).PaddingTop(4).Row(row =>
        {
            row.RelativeItem().Text(t =>
            {
                t.Span("SERVSIC PV • CompuServe Automação Comercial  •  ").FontSize(7).FontColor(CorTextoSubtle);
                t.Span($"Gerado em {DateTime.Now:dd/MM/yyyy HH:mm:ss}").FontSize(7).FontColor(CorTextoSubtle);
            });

            row.RelativeItem().AlignRight().Text(t =>
            {
                t.Span("Página ").FontSize(7).FontColor(CorTextoSubtle);
                t.CurrentPageNumber().FontSize(7).Bold().FontColor(CorNavy);
                t.Span(" de ").FontSize(7).FontColor(CorTextoSubtle);
                t.TotalPages().FontSize(7).Bold().FontColor(CorNavy);
            });
        });
    }
}
