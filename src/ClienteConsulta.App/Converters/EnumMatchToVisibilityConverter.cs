using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClienteConsulta.App.Converters;

/// <summary>Visible quando value.ToString() == ConverterParameter (ex: comparar um enum de modo de tela).</summary>
public sealed class EnumMatchToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null || parameter is null) return Visibility.Collapsed;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
