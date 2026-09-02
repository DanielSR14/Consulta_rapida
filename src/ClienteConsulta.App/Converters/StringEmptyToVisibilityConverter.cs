using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ClienteConsulta.App.Converters;

/// <summary>String vazia/nula → Visible (útil para mostrar um placeholder); caso contrário → Collapsed.</summary>
public sealed class StringEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
