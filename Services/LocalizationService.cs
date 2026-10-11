using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;

namespace DexBridge.Services;

/// <summary>
/// Localizes user-facing WPF chrome without modifying preset IDs, ADB arguments or
/// application package names. English strings in XAML are the stable translation keys.
/// </summary>
public static class LocalizationService
{
    private static readonly Dictionary<string, string> Indonesian = LoadIndonesian();
    private static readonly ConditionalWeakTable<DependencyObject, Dictionary<string, string>> Originals = new();

    public static string Language { get; private set; } = "id";
    public static event EventHandler? LanguageChanged;

    public static void SetLanguage(string? language)
    {
        var normalized = language == "en" ? "en" : "id";
        if (normalized == Language) return;
        Language = normalized;
        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    public static string T(string? english)
    {
        if (english is null) return string.Empty;
        return Language == "id" && Indonesian.TryGetValue(english, out var translated) ? translated : english;
    }

    public static string F(string english, params object?[] arguments)
        => string.Format(System.Globalization.CultureInfo.CurrentCulture, T(english), arguments);

    // Call after InitializeComponent(), and when the language changes. The original
    // English strings are cached per control (never stored in Tag: presets rely on Tag).
    public static void Apply(Window window)
    {
        var visited = new HashSet<DependencyObject>(ReferenceEqualityComparer.Instance);
        Visit(window, visited);
    }

    private static void Visit(DependencyObject element, HashSet<DependencyObject> visited)
    {
        if (!visited.Add(element)) return;
        if (element is Window window) ApplyString(element, "Title", window.Title, s => window.Title = s);
        if (element is TextBlock text && !BindingOperations.IsDataBound(text, TextBlock.TextProperty))
            ApplyString(element, "Text", text.Text, s => text.Text = s);
        if (element is HeaderedContentControl headed && !BindingOperations.IsDataBound(headed, HeaderedContentControl.HeaderProperty) && headed.Header is string heading)
            ApplyString(element, "Header", heading, s => headed.Header = s);
        if (element is ContentControl content && !BindingOperations.IsDataBound(content, ContentControl.ContentProperty) && content.Content is string label)
            ApplyString(element, "Content", label, s => content.Content = s);
        if (element is FrameworkElement fe && fe.ToolTip is string tooltip)
            ApplyString(element, "ToolTip", tooltip, s => fe.ToolTip = s);
        if (element is ComboBox combo)
            foreach (var item in combo.Items)
                if (item is DependencyObject child) Visit(child, visited);
        if (element is ListView list && list.View is GridView grid)
            foreach (var column in grid.Columns)
                if (column.Header is string header)
                    ApplyString(column, "Header", header, s => column.Header = s);

        // WPF template and list items can be either logical or visual children.
        foreach (var child in LogicalTreeHelper.GetChildren(element))
            if (child is DependencyObject dep) Visit(dep, visited);
        if (element is Visual || element is System.Windows.Media.Media3D.Visual3D)
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
                Visit(VisualTreeHelper.GetChild(element, i), visited);
    }

    private static void ApplyString(DependencyObject item, string property, string current, Action<string> setter)
    {
        var originals = Originals.GetOrCreateValue(item);
        if (!originals.TryGetValue(property, out var english) ||
            (current != english && current != Indonesian.GetValueOrDefault(english, english)))
        {
            // The view-model can change a status message after a previous refresh.
            english = current;
            originals[property] = english;
        }
        var translated = T(english);
        if (translated != current) setter(translated);
    }

    private static Dictionary<string, string> LoadIndonesian()
    {
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("DexBridge.Languages.id-ID.json")
            ?? throw new InvalidOperationException("The Indonesian translation resource is missing.");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? throw new InvalidDataException("The Indonesian translation resource is invalid.");
    }
}
