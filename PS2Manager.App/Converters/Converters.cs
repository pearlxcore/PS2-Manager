using System.Collections.Concurrent;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PS2Manager.Core.Rename;

namespace PS2Manager.App.Converters;

/// <summary>
/// Loads an image file without locking it, decoded at a reduced width (ConverterParameter, default 300).
/// Cached by path + timestamp so replacing a cover shows the new one.
/// </summary>
public sealed class PathToImageConverter : IValueConverter
{
    private static readonly ConcurrentDictionary<string, WeakReference<BitmapSource>> Cache = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string path || string.IsNullOrEmpty(path)) return null;
        int width = parameter is string s && int.TryParse(s, out int w) ? w : 300;
        return Load(path, width);
    }

    public static BitmapSource? Load(string path, int decodeWidth)
    {
        try
        {
            var fi = new FileInfo(path);
            if (!fi.Exists) return null;
            string key = $"{path}|{fi.LastWriteTimeUtc.Ticks}|{decodeWidth}";
            if (Cache.TryGetValue(key, out var weak) && weak.TryGetTarget(out var cached)) return cached;

            var bmp = new BitmapImage();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
                if (decodeWidth > 0) bmp.DecodePixelWidth = decodeWidth;
                bmp.StreamSource = fs;
                bmp.EndInit();
            }
            bmp.Freeze();
            Cache[key] = new WeakReference<BitmapSource>(bmp);
            return bmp;
        }
        catch
        {
            return null;
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is true) ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        (value is Visibility.Visible) ^ Invert;
}

public sealed class NullToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool hasValue = value is not null && !(value is string s && s.Length == 0);
        return hasValue ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class ZeroToVisibilityConverter : IValueConverter
{
    public bool Invert { get; set; }
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        bool zero = value is 0 or 0L or null;
        return zero ^ Invert ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => value is not true;
}

/// <summary>For radio buttons bound to an enum: IsChecked = (value == parameter).</summary>
public sealed class EqualsToBoolConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not true || parameter is null) return Binding.DoNothing;
        return targetType.IsEnum ? Enum.Parse(targetType, parameter.ToString()!) : parameter;
    }
}

public sealed class EqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Enum → its [Description] or a spaced-out name.</summary>
public sealed class EnumDescriptionConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not Enum e) return value;
        var attr = e.GetType().GetField(e.ToString())?.GetCustomAttribute<DescriptionAttribute>();
        if (attr is not null) return attr.Description;
        return System.Text.RegularExpressions.Regex.Replace(e.ToString(), "(?<=[a-z])([A-Z])", " $1");
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class PlanStatusBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string key = value switch
        {
            PlanStatus.Ready => "InfoBrush",
            PlanStatus.Done => "SuccessBrush",
            PlanStatus.Unchanged => "MutedTextBrush",
            PlanStatus.Conflict or PlanStatus.SourceMissing => "WarningBrush",
            PlanStatus.Invalid or PlanStatus.Failed => "DangerBrush",
            _ => "MutedTextBrush",
        };
        return Application.Current.TryFindResource(key) as Brush ?? Brushes.Gray;
    }
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class MultiplyConverter : IValueConverter
{
    public double Factor { get; set; } = 1;
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? d * Factor : value is int i ? i * Factor : DependencyProperty.UnsetValue;
    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) => throw new NotSupportedException();
}
