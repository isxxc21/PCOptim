using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using PCOpti.Models;

namespace PCOpti.Views;

/// <summary>true -> Collapsed, false -> Visible.</summary>
public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is true ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is Visibility.Collapsed;
}

/// <summary>Visible when the value is a non-empty string / non-null object.</summary>
public sealed class NotEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type t, object p, CultureInfo c) =>
        value is null || value is string { Length: 0 } ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>Visible when an int is greater than zero.</summary>
public sealed class PositiveToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is int i && i > 0 ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is not true;
    public object ConvertBack(object value, Type t, object p, CultureInfo c) => value is not true;
}

/// <summary>RiskLevel -> badge colour. Parameter "back" returns a translucent background.</summary>
public sealed class RiskToBrushConverter : IValueConverter
{
    private static readonly Color Safe = Color.FromRgb(0x3F, 0xB9, 0x50);
    private static readonly Color Moderate = Color.FromRgb(0xE3, 0xA2, 0x1A);
    private static readonly Color Advanced = Color.FromRgb(0xF8, 0x51, 0x49);

    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var color = value switch
        {
            RiskLevel.Moderate => Moderate,
            RiskLevel.Advanced => Advanced,
            _ => Safe,
        };
        if (p as string == "back") color = Color.FromArgb(0x22, color.R, color.G, color.B);
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>StartupImpact -> colour.</summary>
public sealed class ImpactToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var color = value switch
        {
            StartupImpact.High => Color.FromRgb(0xF8, 0x51, 0x49),
            StartupImpact.Medium => Color.FromRgb(0xE3, 0xA2, 0x1A),
            StartupImpact.Low => Color.FromRgb(0x3F, 0xB9, 0x50),
            _ => Color.FromRgb(0x8C, 0x96, 0xA5),
        };
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

/// <summary>ChangeAction -> colour for the change log.</summary>
public sealed class ActionToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        var color = value switch
        {
            ChangeAction.Applied => Color.FromRgb(0x00, 0xD1, 0xFF),
            ChangeAction.Reverted => Color.FromRgb(0xA7, 0x8B, 0xFA),
            ChangeAction.Cleaned or ChangeAction.Ran => Color.FromRgb(0x3F, 0xB9, 0x50),
            ChangeAction.Failed => Color.FromRgb(0xF8, 0x51, 0x49),
            _ => Color.FromRgb(0x8C, 0x96, 0xA5),
        };
        var b = new SolidColorBrush(color);
        b.Freeze();
        return b;
    }

    public object ConvertBack(object value, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
