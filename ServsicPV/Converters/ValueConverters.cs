using System;
using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace ServsicPV.Converters;

public class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool val && val;
        if (Invert) b = !b;
        return b ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; } = false;

    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool isNull = value == null;
        if (Invert) isNull = !isNull;
        return isNull ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        bool b = value is bool val && val;
        // True => Verde suave ou Azul escuro, False => Vermelho perigo
        return b
            ? new SolidColorBrush(Color.FromArgb(255, 15, 23, 42))     // #0F172A
            : new SolidColorBrush(Color.FromArgb(255, 239, 68, 68));    // #EF4444
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class CurrencyConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is decimal dec)
            return dec.ToString("C2", new CultureInfo("pt-BR"));
        if (value is double dbl)
            return dbl.ToString("C2", new CultureInfo("pt-BR"));
        return "R$ 0,00";
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class StatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool b)
        {
            return b 
                ? new SolidColorBrush(Color.FromArgb(255, 2, 132, 199))   // #0284C7 Blue when active
                : new SolidColorBrush(Color.FromArgb(255, 30, 41, 59));   // #1E293B Dark Slate when inactive
        }

        string status = value?.ToString() ?? "";
        return status.ToUpperInvariant() switch
        {
            "O" or "ORÇAMENTO" => new SolidColorBrush(Color.FromArgb(255, 2, 132, 199)),    // #0284C7 Azul Claro
            "V" or "VENDA" or "PEDIDO DE VENDA" => new SolidColorBrush(Color.FromArgb(255, 3, 105, 161)), // #0369A1 Azul Corporativo
            "CANCELADO" => new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)),            // Vermelho
            _ => new SolidColorBrush(Color.FromArgb(255, 100, 116, 139))                      // Cinza
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class NotificationBackgroundConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isSucesso && !isSucesso)
            return new SolidColorBrush(Color.FromArgb(255, 220, 38, 38)); // #DC2626 Vermelho Danger
        return new SolidColorBrush(Color.FromArgb(255, 11, 25, 44));     // #0B192C Navy Oficial ServSic
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class NotificationBorderConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is bool isSucesso && !isSucesso)
            return new SolidColorBrush(Color.FromArgb(255, 239, 68, 68)); // #EF4444 Borda Danger
        return new SolidColorBrush(Color.FromArgb(255, 2, 132, 199));     // #0284C7 Borda Azul ServSic
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

public class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is bool b ? !b : true;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language) => throw new NotImplementedException();
}

