using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace FastDownloader.Native;

public partial class NewDownloadDialog : Window
{
    private readonly BridgeClient bridge;
    private bool audio;
    private bool sending;

    public NewDownloadDialog(BridgeClient bridge, string folder)
    {
        this.bridge = bridge;
        InitializeComponent();
        LocationLabel.Text = string.IsNullOrWhiteSpace(folder)
            ? "Uses the download engine's configured folder"
            : folder;
        Loaded += (_, _) => UrlBox.Focus();
        UpdateMode();
    }

    private void UpdateMode()
    {
        var accent = (Brush)Application.Current.Resources["AccentBrush"];
        var accentText = (Brush)Application.Current.Resources["AccentTextBrush"];
        var secondary = (Brush)Application.Current.Resources["RaisedBrush"];
        var normalText = (Brush)Application.Current.Resources["MainTextBrush"];
        VideoMode.Background = audio ? secondary : accent;
        VideoMode.Foreground = audio ? normalText : accentText;
        AudioMode.Background = audio ? accent : secondary;
        AudioMode.Foreground = audio ? accentText : normalText;
    }

    private void VideoMode_Click(object sender, RoutedEventArgs e) { audio = false; UpdateMode(); }
    private void AudioMode_Click(object sender, RoutedEventArgs e) { audio = true; UpdateMode(); }
    private void UrlBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (InlineStatus is not null)
            InlineStatus.Text = "Use a supported download URL. No browser tab will open.";
    }

    private static string SafeFilename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Trim().Select(c =>
            invalid.Contains(c) || "\\/:*?\"<>|".Contains(c) || c < 32 ? '-' : c).ToArray();
        var safe = new string(chars).Trim().TrimEnd('.', ' ');
        string stem = safe.Split('.')[0];
        if (System.Text.RegularExpressions.Regex.IsMatch(stem,
            @"^(CON|PRN|AUX|NUL|COM[1-9]|LPT[1-9])$", System.Text.RegularExpressions.RegexOptions.IgnoreCase))
            safe = "_" + safe;
        return safe;
    }

    private async Task SubmitAsync(bool startNow)
    {
        if (sending) return;
        string link = UrlBox.Text.Trim();
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            InlineStatus.Text = "Enter a valid HTTP or HTTPS download link.";
            InlineStatus.Foreground = (Brush)Application.Current.Resources["WarningBrush"];
            UrlBox.Focus();
            return;
        }
        var filename = SafeFilename(FileNameBox.Text);
        if (filename != FileNameBox.Text.Trim())
            FileNameBox.Text = filename;
        sending = true;
        StartButton.IsEnabled = false;
        LaterButton.IsEnabled = false;
        InlineStatus.Foreground = (Brush)Application.Current.Resources["MutedTextBrush"];
        InlineStatus.Text = "Sending to your download engine…";
        try
        {
            await bridge.AddAsync(link, filename, audio, startNow);
            DialogResult = true;
            Close();
        }
        catch (Exception error)
        {
            InlineStatus.Text = "Cannot add download: " + error.Message;
            InlineStatus.Foreground = (Brush)Application.Current.Resources["WarningBrush"];
        }
        finally
        {
            sending = false;
            StartButton.IsEnabled = true;
            LaterButton.IsEnabled = true;
        }
    }

    private async void Start_Click(object sender, RoutedEventArgs e) => await SubmitAsync(true);
    private async void Later_Click(object sender, RoutedEventArgs e) => await SubmitAsync(false);
    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }
    private void Header_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
}