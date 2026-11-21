using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FastDownloader.Native;

public partial class MainWindow : Window
{
    private readonly BridgeClient bridge = new();
    private readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromSeconds(7) };
    private List<TransferItem> allTransfers = new();
    private BridgeSnapshot lastSnapshot = new();
    private string filter = "all";
    private bool updating;
    private bool connected;

    public string? CapturePath { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        poll.Tick += async (_, _) => await RefreshAsync();
        Loaded += async (_, _) =>
        {
            if (CapturePath is null)
            {
                poll.Start();
                await RefreshAsync();
            }
        };
        ContentRendered += (_, _) =>
        {
            if (CapturePath is null) return;
            Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(CapturePreview));
        };
        Closed += (_, _) => { poll.Stop(); bridge.Dispose(); };
        RefreshFilterStyles();
    }

    private void CapturePreview()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(CapturePath)) return;
            UpdateLayout();
            int width = (int)Math.Ceiling(ActualWidth);
            int height = (int)Math.Ceiling(ActualHeight);
            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(this);
            var png = new PngBitmapEncoder();
            png.Frames.Add(BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(CapturePath))!);
            using (var output = File.Create(CapturePath)) png.Save(output);
        }
        finally { Close(); }
    }

    private async Task RefreshAsync()
    {
        if (updating || CapturePath is not null || WindowState == WindowState.Minimized) return;
        updating = true;
        try
        {
            var snapshot = await bridge.SnapshotAsync();
            lastSnapshot = snapshot;
            allTransfers = snapshot.Transfers;
            connected = true;
            SidebarStatus.Text = "Engine connected";
            ConnectionDot.Fill = (Brush)Application.Current.Resources["AccentBrush"];
            FooterStatus.Text = "Live • Existing engine connected • No browser UI running inside this window";
            PauseAllButton.IsEnabled = true;
            ResumeAllButton.IsEnabled = true;
            PauseAllButton.Visibility = Visibility.Visible;
            ResumeAllButton.Visibility = Visibility.Visible;
            ActiveCount.Text = allTransfers.Count(t => t.IsActive).ToString();
            CompleteCount.Text = allTransfers.Count(t => t.IsCompleted).ToString();
            QueuedCount.Text = allTransfers.Count(t => t.IsQueued).ToString();
            SpeedValue.Text = BridgeClient.Bytes(allTransfers.Sum(t => t.Speed)) + "/s";
            poll.Interval = TimeSpan.FromSeconds(allTransfers.Any(t => t.IsActive) ? 3 : 8);
        }
        catch
        {
            connected = false;
            allTransfers = new();
            SidebarStatus.Text = "Engine not connected";
            ConnectionDot.Fill = (Brush)Application.Current.Resources["WarningBrush"];
            PauseAllButton.IsEnabled = false;
            ResumeAllButton.IsEnabled = false;
            PauseAllButton.Visibility = Visibility.Collapsed;
            ResumeAllButton.Visibility = Visibility.Collapsed;
            ActiveCount.Text = "—";
            CompleteCount.Text = "—";
            QueuedCount.Text = "—";
            SpeedValue.Text = "—";
            FooterStatus.Text = "Native preview • Start the existing FastDownloader engine to see real downloads";
            poll.Interval = TimeSpan.FromSeconds(7);
        }
        finally
        {
            RenderFilteredList();
            updating = false;
        }
    }

    private void RenderFilteredList()
    {
        var term = SearchBox?.Text.Trim() ?? "";
        IEnumerable<TransferItem> rows = allTransfers;
        if (filter == "active") rows = rows.Where(t => t.IsActive || t.IsQueued);
        if (filter == "completed") rows = rows.Where(t => t.IsCompleted);
        if (!string.IsNullOrEmpty(term))
            rows = rows.Where(t => t.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        var visible = rows.ToArray();
        DownloadList.ItemsSource = visible;
        ListCount.Text = connected ? visible.Length.ToString() : "";
        EmptyState.Visibility = visible.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
        if (!connected)
        {
            EmptyHeading.Text = "Connect your download engine";
            EmptyDetail.Text = "This is a working native Windows interface. It reads actual downloads from the existing FastDownloader app — no sample data.";
        }
        else if (allTransfers.Count == 0)
        {
            EmptyHeading.Text = "No downloads yet";
            EmptyDetail.Text = "Choose New download to add a transfer. Your files remain managed by the existing download engine.";
        }
        else
        {
            EmptyHeading.Text = "No matches";
            EmptyDetail.Text = "Try a different search or choose All downloads.";
        }
    }

    private void RefreshFilterStyles()
    {
        foreach (var button in new[] { AllFilter, ActiveFilter, CompletedFilter })
        {
            bool selected = button == AllFilter && filter == "all" ||
                button == ActiveFilter && filter == "active" ||
                button == CompletedFilter && filter == "completed";
            button.Background = selected ? (Brush)Application.Current.Resources["RaisedBrush"] :
                Brushes.Transparent;
            button.Foreground = selected ? (Brush)Application.Current.Resources["MainTextBrush"] :
                (Brush)Application.Current.Resources["MutedTextBrush"];
        }
    }

    private void SelectFilter(string next)
    {
        filter = next;
        RefreshFilterStyles();
        RenderFilteredList();
    }

    private void AllFilter_Click(object sender, RoutedEventArgs e) => SelectFilter("all");
    private void ActiveFilter_Click(object sender, RoutedEventArgs e) => SelectFilter("active");
    private void CompletedFilter_Click(object sender, RoutedEventArgs e) => SelectFilter("completed");
    private void Search_TextChanged(object sender, TextChangedEventArgs e) => RenderFilteredList();
    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        App.SetTheme(App.RequestedTheme == "dark" ? "light" : "dark");
        RefreshFilterStyles();
        ConnectionDot.Fill = (Brush)Application.Current.Resources[connected ? "AccentBrush" : "WarningBrush"];
    }

    private async void PauseAll_Click(object sender, RoutedEventArgs e) => await SendActionAsync("pauseAll");
    private async void ResumeAll_Click(object sender, RoutedEventArgs e) => await SendActionAsync("resumeAll");

    private async void TaskAction_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.Tag is not string id) return;
        var row = allTransfers.FirstOrDefault(t => t.Id == id);
        if (row is null) return;
        await SendActionAsync(row.IsActive ? "pause" : "resume", id);
    }

    private async Task SendActionAsync(string action, string id = "")
    {
        try
        {
            await bridge.ActionAsync(action, id);
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            FooterStatus.Text = "Action rejected: " + ex.Message;
        }
    }

    private async void NewDownload_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new NewDownloadDialog(bridge, lastSnapshot.DownloadFolder) { Owner = this };
        if (dialog.ShowDialog() == true) await RefreshAsync();
    }

    private void TitleBar_Drag(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Maximize_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}