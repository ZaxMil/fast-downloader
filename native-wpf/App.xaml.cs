using System.IO;
using System.Windows;
using System.Windows.Media;

namespace FastDownloader.Native;

public partial class App : Application
{
    internal static string RequestedTheme { get; private set; } = "dark";

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var theme = e.Args.FirstOrDefault(x => x.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase));
        RequestedTheme = theme is not null ? theme.Split('=', 2)[1] : LoadSavedTheme();
        SetTheme(RequestedTheme);
        var preview = new MainWindow();
        MainWindow = preview;
        var captureArg = Array.FindIndex(e.Args, x => x == "--capture");
        if (captureArg >= 0 && captureArg + 1 < e.Args.Length)
            preview.CapturePath = e.Args[captureArg + 1];
        preview.Show();
    }

    private static string LoadSavedTheme()
    {
        try
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FastDownloader", "native-preview-theme.txt");
            return File.Exists(file) ? File.ReadAllText(file).Trim() : "dark";
        }
        catch { return "dark"; }
    }

    internal static void SetTheme(string theme)
    {
        RequestedTheme = theme == "light" ? "light" : "dark";
        var resource = Current.Resources;
        void Set(string key, string hex) => resource[key] =
            new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        if (RequestedTheme == "light")
        {
            Set("CanvasBrush", "#F5F8FB");
            Set("SidebarBrush", "#FFFFFF");
            Set("SurfaceBrush", "#FFFFFF");
            Set("RaisedBrush", "#ECF2F5");
            Set("LineBrush", "#DCE5EC");
            Set("MainTextBrush", "#142637");
            Set("MutedTextBrush", "#687D8E");
            Set("AccentBrush", "#078963");
            Set("AccentTextBrush", "#FFFFFF");
            Set("InputBrush", "#F7F9FB");
            Set("WarningBrush", "#BD7939");
        }
        else
        {
            Set("CanvasBrush", "#0B1018");
            Set("SidebarBrush", "#101823");
            Set("SurfaceBrush", "#151E2A");
            Set("RaisedBrush", "#1C2835");
            Set("LineBrush", "#283747");
            Set("MainTextBrush", "#F2F6F8");
            Set("MutedTextBrush", "#91A3B2");
            Set("AccentBrush", "#30D8A5");
            Set("AccentTextBrush", "#082A20");
            Set("InputBrush", "#101A25");
            Set("WarningBrush", "#F6C675");
        }
        try
        {
            var file = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "FastDownloader", "native-preview-theme.txt");
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, RequestedTheme);
        }
        catch { }
    }
}