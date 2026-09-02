using System.Globalization;
using System.Windows;
using System.Windows.Data;
using ClienteConsulta.App.ViewModels;

namespace ClienteConsulta.App.Converters;

/// <summary>Resolve um <see cref="InfoIcon"/> para o Geometry "Icon.{nome}" definido em Resources/Icons.xaml.</summary>
public sealed class InfoIconToGeometryConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not InfoIcon icon) return null;
        return Application.Current.TryFindResource($"Icon.{icon}");
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
