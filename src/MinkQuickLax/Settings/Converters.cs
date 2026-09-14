using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace MinkQuickLax.Settings;

/// <summary>Binds an enum to a radio button: checked when the value equals the ConverterParameter.</summary>
public sealed class EnumIsConverter : IValueConverter
{
    public static EnumIsConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => Equals(value, parameter);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true && parameter is not null ? parameter : Binding.DoNothing;
}

/// <summary>Visible when the value is true, a non-empty string, a positive number or any other non-null object; <c>Invert</c> flips it.</summary>
public sealed class VisibleWhenConverter : IValueConverter
{
    public static VisibleWhenConverter Instance { get; } = new();

    public static VisibleWhenConverter Inverted { get; } = new() { Invert = true };

    public bool Invert { get; set; }

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var shown = value switch
        {
            bool flag => flag,
            string text => text.Length > 0,
            int count => count > 0,
            _ => value is not null,
        };
        if (parameter is not null)
        {
            // With a parameter: visible when the value equals it (for example the current style).
            shown = Equals(value, parameter);
        }
        return shown != Invert ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => Binding.DoNothing;
}

public sealed class NotConverter : IValueConverter
{
    public static NotConverter Instance { get; } = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}
