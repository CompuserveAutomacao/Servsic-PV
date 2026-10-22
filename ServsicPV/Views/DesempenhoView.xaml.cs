using System;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using ServsicPV.ViewModels;
using Windows.Foundation;

namespace ServsicPV.Views;

public partial class DesempenhoView : UserControl
{
    private DesempenhoViewModel? _viewModel;

    public DesempenhoView()
    {
        this.InitializeComponent();
        this.DataContextChanged += DesempenhoView_DataContextChanged;
        this.Loaded += DesempenhoView_Loaded;
    }

    private void DesempenhoView_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = args.NewValue as DesempenhoViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
            DesenharGraficoRaios();
        }
    }

    private void DesempenhoView_Loaded(object sender, RoutedEventArgs e)
    {
        DesenharGraficoRaios();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DesempenhoViewModel.Dados)
            || e.PropertyName == nameof(DesempenhoViewModel.PercentualVendas)
            || e.PropertyName == nameof(DesempenhoViewModel.PercentualPreVendas)
            || e.PropertyName == nameof(DesempenhoViewModel.PercentualOrcamentos))
        {
            DispatcherQueue.TryEnqueue(DesenharGraficoRaios);
        }
    }

    /// <summary>
    /// Renderiza os 3 anéis concêntricos modernos (Gráfico Tipo Raio / Activity Rings)
    /// baseando-se nos percentuais de Vendas, Pré-Vendas e Orçamentos.
    /// </summary>
    private void DesenharGraficoRaios()
    {
        if (CanvasRadialRings == null) return;
        CanvasRadialRings.Children.Clear();

        double cx = 110;
        double cy = 110;
        double espessura = 11;

        // Raio 1 (Externo - 92px): Vendas Concluídas (#0284C7)
        double percVendas = _viewModel?.PercentualVendas ?? 0;
        DesenharTrilhoEArco(cx, cy, 92, espessura, percVendas, Windows.UI.Color.FromArgb(255, 2, 132, 199));

        // Raio 2 (Médio - 74px): Pré-Vendas Balcão (#1E293B)
        double percPre = _viewModel?.PercentualPreVendas ?? 0;
        DesenharTrilhoEArco(cx, cy, 74, espessura, percPre, Windows.UI.Color.FromArgb(255, 30, 41, 59));

        // Raio 3 (Interno - 56px): Orçamentos (#38BDF8)
        double percOrc = _viewModel?.PercentualOrcamentos ?? 0;
        DesenharTrilhoEArco(cx, cy, 56, espessura, percOrc, Windows.UI.Color.FromArgb(255, 56, 189, 248));
    }

    private void DesenharTrilhoEArco(double cx, double cy, double radius, double thickness, double percentage, Windows.UI.Color cor)
    {
        // 1. Trilho de fundo suave (Cinzento #F1F5F9)
        var track = new Ellipse
        {
            Width = radius * 2,
            Height = radius * 2,
            Stroke = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 241, 245, 249)),
            StrokeThickness = thickness
        };
        Canvas.SetLeft(track, cx - radius);
        Canvas.SetTop(track, cy - radius);
        CanvasRadialRings.Children.Add(track);

        // 2. Arco Ativo com pontas arredondadas (Raio ativo)
        if (percentage > 0)
        {
            double sweepDegrees = Math.Min(359.99, (percentage / 100.0) * 360.0);
            if (sweepDegrees < 2.0) sweepDegrees = 2.0; // Mínimo visível

            var arcGeometry = CriarGeometriaArco(cx, cy, radius, 0, sweepDegrees);
            var path = new Microsoft.UI.Xaml.Shapes.Path
            {
                Data = arcGeometry,
                Stroke = new SolidColorBrush(cor),
                StrokeThickness = thickness,
                StrokeStartLineCap = PenLineCap.Round,
                StrokeEndLineCap = PenLineCap.Round
            };
            CanvasRadialRings.Children.Add(path);
        }
    }

    private static PathGeometry CriarGeometriaArco(double cx, double cy, double radius, double startAngleDegrees, double sweepDegrees)
    {
        double startAngleRad = (startAngleDegrees - 90.0) * Math.PI / 180.0;
        double endAngleRad = (startAngleDegrees - 90.0 + sweepDegrees) * Math.PI / 180.0;

        Point pStart = new Point(cx + radius * Math.Cos(startAngleRad), cy + radius * Math.Sin(startAngleRad));
        Point pEnd = new Point(cx + radius * Math.Cos(endAngleRad), cy + radius * Math.Sin(endAngleRad));

        bool isLargeArc = sweepDegrees > 180.0;

        var figure = new PathFigure
        {
            StartPoint = pStart,
            IsClosed = false
        };

        figure.Segments.Add(new ArcSegment
        {
            Point = pEnd,
            Size = new Size(radius, radius),
            IsLargeArc = isLargeArc,
            SweepDirection = SweepDirection.Clockwise
        });

        var geometry = new PathGeometry();
        geometry.Figures.Add(figure);
        return geometry;
    }

    private void BtnAtualizar_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.AtualizarCommand.CanExecute(null) == true)
        {
            _viewModel.AtualizarCommand.Execute(null);
        }
    }

    private void BtnVoltar_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel?.VoltarCommand.CanExecute(null) == true)
        {
            _viewModel.VoltarCommand.Execute(null);
        }
    }
}
