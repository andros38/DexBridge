using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace DexBridge.Services;

public static class ThemeService
{
    private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
    public static string CurrentResolvedTheme { get; private set; } = "light";

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int dwAttribute, ref int pvAttribute, int cbAttribute);

    public static string ResolveTheme(string requested)
    {
        if (!requested.Equals("system", StringComparison.OrdinalIgnoreCase)) return requested.ToLowerInvariant();
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var value = key?.GetValue("AppsUseLightTheme");
            if (value is int i && i == 0) return "dark";
        }
        catch { }
        return "light";
    }

    public static void Apply(string requested, bool lowContrastNight = false)
    {
        var theme = ResolveTheme(requested);
        CurrentResolvedTheme = theme;
        var oled = theme == "oled";
        var dark = theme == "dark" || oled;

        var bg = oled ? "#000000" : dark ? "#141414" : "#F7F7F7";
        var surface = oled ? "#080808" : dark ? "#1E1E1E" : "#FFFFFF";
        var surface2 = oled ? "#101010" : dark ? "#282828" : "#F1F1F1";
        var border = oled ? "#242424" : dark ? "#3A3A3A" : "#D0D0D0";
        var primary = dark ? (lowContrastNight ? "#E8E8E8" : "#FAFAFA") : "#161616";
        var secondary = dark ? (lowContrastNight ? "#B3B3B3" : "#D0D0D0") : "#666666";
        var input = oled ? "#0D0D0D" : dark ? "#252525" : "#FFFFFF";
        // Enabled actions have a subtle blue tint; disabled ones remain neutral gray.
        // On OLED, an inactive action must not be brighter than an enabled action.
        var button = oled ? "#19293B" : dark ? "#2A3A50" : "#E1ECFA";
        // Disabled controls must look unmistakably inactive.  In earlier builds the native WPF
        // ComboBox template could still paint a light system background while the foreground had
        // already switched to the dark-theme color, producing almost invisible text.
        var disabledBg = oled ? "#0D0D0D" : dark ? "#202020" : "#F0F0F0";
        var disabledText = oled ? "#9A9A9A" : dark ? "#A5A5A5" : "#777777";
        var disabledBorder = oled ? "#292929" : dark ? "#383838" : "#D2D2D2";
        var selection = oled ? "#17355F" : dark ? "#244B7C" : "#DCEAFF";
        var accent = dark ? "#75A7FF" : "#245DA8";
        var tooltipBg = oled ? "#101010" : dark ? "#242424" : "#FFFFFF";
        var tooltipBorder = oled ? "#3A3A3A" : dark ? "#4A4A4A" : "#D0D0D0";

        SetBrush("AppBackgroundBrush", bg);
        SetBrush("SurfaceBrush", surface);
        SetBrush("SurfaceAltBrush", surface2);
        SetBrush("BorderBrush", border);
        SetBrush("PrimaryTextBrush", primary);
        SetBrush("SecondaryTextBrush", secondary);
        SetBrush("InputBackgroundBrush", input);
        SetBrush("InputHoverBrush", oled ? "#181818" : dark ? "#333333" : "#F5F8FD");
        SetBrush("ButtonBackgroundBrush", button);
        SetBrush("ButtonHoverBrush", oled ? "#243D59" : dark ? "#3D5472" : "#CCDFFA");
        SetBrush("ButtonPressedBrush", oled ? "#305070" : dark ? "#50688B" : "#B6D4F6");
        SetBrush("DisabledBackgroundBrush", disabledBg);
        SetBrush("DisabledTextBrush", disabledText);
        SetBrush("DisabledBorderBrush", disabledBorder);
        SetBrush("SelectionBrush", selection);
        SetBrush("AccentBrush", accent);
        SetBrush("ToolTipBackgroundBrush", tooltipBg);
        SetBrush("ToolTipBorderBrush", tooltipBorder);
        SetBrush("CriticalBrush", "#FF6B6B");
        SetBrush("WarningBrush", "#FFB84D");
        SetBrush("GoodBrush", dark ? "#66D487" : "#188038");

        // Button / ComboBox / CheckBox / TextBox now use explicit WPF ControlTemplates.
        // Do not override global SystemColors keys: doing so cannot reliably recolor
        // Windows-native ComboBox popups and can change unrelated controls.

    }

    public static void ApplyWindowPalette(Window window)
    {
        try
        {
            if (Application.Current.Resources["AppBackgroundBrush"] is Brush bg)
                window.Background = bg;
            if (Application.Current.Resources["PrimaryTextBrush"] is Brush fg)
                window.Foreground = fg;
        }
        catch { }
    }

    public static void ApplyWindowChrome(Window window)
    {
        try
        {
            var hwnd = new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) return;
            var enabled = CurrentResolvedTheme is "dark" or "oled" ? 1 : 0;
            DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref enabled, sizeof(int));
        }
        catch { }
    }

    private static void SetBrush(string key, string color)
        => Application.Current.Resources[key] = MakeBrush(color);

    private static SolidColorBrush MakeBrush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }
}
