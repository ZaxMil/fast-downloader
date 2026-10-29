using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;

namespace FastDownloader.Installer
{
    public class WpfSetupWizard : Application
    {
        private const string AppName = "FastDownloader Pro";
        private const string AppVersion = "0.1.3";
        private const string AppPublisher = "ZaxMil";
        private const string NativeHostName = "com.fastdownloader.app";
        private const string GitHubUrl = "https://github.com/ZaxMil/fast-downloader";
        private const string KofiUrl = "https://ko-fi.com/zaxmi";

        [STAThread]
        public static void Main(string[] args)
        {
            if (args != null && args.Length > 0)
            {
                string arg = args[0].ToLowerInvariant();
                if (arg == "/uninstall" || arg == "-uninstall" || arg == "/u")
                {
                    PerformUninstall();
                    return;
                }
            }

            WpfSetupWizard app = new WpfSetupWizard();
            app.Run(new MainWindow());
        }

        public static void PerformUninstall()
        {
            MessageBoxResult res = MessageBox.Show(
                "Are you sure you want to completely uninstall FastDownloader Pro and remove all components?",
                AppName + " Uninstall",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question
            );

            if (res != MessageBoxResult.Yes) return;

            try
            {
                foreach (Process p in Process.GetProcessesByName("FastDownloader")) { try { p.Kill(); } catch { } }
                foreach (Process p in Process.GetProcessesByName("aria2c")) { try { p.Kill(); } catch { } }

                string dLnk = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), AppName + ".lnk");
                if (File.Exists(dLnk)) File.Delete(dLnk);

                string pLnk = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");
                if (File.Exists(pLnk)) File.Delete(pLnk);

                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FastDownloader", false); } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Google\Chrome\NativeMessagingHosts\" + NativeHostName, false); } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Edge\NativeMessagingHosts\" + NativeHostName, false); } catch { }
                try { Registry.CurrentUser.DeleteSubKeyTree(@"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\" + NativeHostName, false); } catch { }
                try
                {
                    using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (k != null) k.DeleteValue("FastDownloader", false);
                    }
                }
                catch { }

                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", string.Format("/c timeout /t 2 /nobreak >nul & rmdir /s /q \"{0}\"", currentDir.TrimEnd('\\')))
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);

                MessageBox.Show(AppName + " was successfully removed from your computer.", "Uninstall Finished", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Uninstall completed with notes: " + ex.Message, "Uninstall", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    // =========================================================================
    // Modern Interactive Option Toggle Card
    // =========================================================================
    public class ModernOptionCard : Border
    {
        private bool isChecked;
        private Border switchPill;
        private Ellipse knob;
        private TextBlock statusLbl;

        private static readonly Brush CardBg = new SolidColorBrush(Color.FromRgb(18, 24, 38));
        private static readonly Brush CardBorder = new SolidColorBrush(Color.FromRgb(40, 50, 70));
        private static readonly Brush Emerald = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        private static readonly Brush TextWhite = new SolidColorBrush(Color.FromRgb(248, 250, 252));
        private static readonly Brush TextMuted = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        private static readonly Brush TextDim = new SolidColorBrush(Color.FromRgb(100, 116, 139));

        public bool IsChecked
        {
            get { return isChecked; }
            set
            {
                isChecked = value;
                UpdateVisual();
            }
        }

        public ModernOptionCard(string icon, string title, string subtitle, bool defaultChecked)
        {
            this.Height = 64;
            this.CornerRadius = new CornerRadius(10);
            this.BorderThickness = new Thickness(1);
            this.Margin = new Thickness(0, 5, 0, 5);
            this.Padding = new Thickness(16, 0, 16, 0);
            this.Cursor = Cursors.Hand;

            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(46) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });

            // Icon Badge
            Border iconBadge = new Border
            {
                Width = 38,
                Height = 38,
                CornerRadius = new CornerRadius(8),
                Background = new SolidColorBrush(Color.FromArgb(28, 255, 255, 255)),
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            TextBlock iconText = new TextBlock
            {
                Text = icon,
                FontSize = 18,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            iconBadge.Child = iconText;
            Grid.SetColumn(iconBadge, 0);

            // Text
            StackPanel textSp = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(6, 0, 10, 0)
            };
            TextBlock t = new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = TextWhite,
                Margin = new Thickness(0, 0, 0, 2)
            };
            TextBlock s = new TextBlock
            {
                Text = subtitle,
                FontSize = 11,
                Foreground = TextMuted
            };
            textSp.Children.Add(t);
            textSp.Children.Add(s);
            Grid.SetColumn(textSp, 1);

            // Modern Pill Toggle Switch
            StackPanel toggleSp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            switchPill = new Border
            {
                Width = 46,
                Height = 24,
                CornerRadius = new CornerRadius(12),
                ClipToBounds = true
            };

            Canvas knobCanvas = new Canvas { Width = 46, Height = 24 };
            knob = new Ellipse
            {
                Width = 18,
                Height = 18,
                Fill = Brushes.White
            };
            Canvas.SetTop(knob, 3);
            knobCanvas.Children.Add(knob);
            switchPill.Child = knobCanvas;

            statusLbl = new TextBlock
            {
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 0, 0),
                Width = 30
            };

            toggleSp.Children.Add(switchPill);
            toggleSp.Children.Add(statusLbl);
            Grid.SetColumn(toggleSp, 2);

            grid.Children.Add(iconBadge);
            grid.Children.Add(textSp);
            grid.Children.Add(toggleSp);
            this.Child = grid;

            // Click anywhere to toggle
            this.MouseDown += (sender, args) =>
            {
                if (args.ChangedButton == MouseButton.Left)
                {
                    IsChecked = !IsChecked;
                }
            };

            // Hover effects
            this.MouseEnter += (sender, args) =>
            {
                if (!isChecked)
                {
                    this.BorderBrush = new SolidColorBrush(Color.FromRgb(65, 80, 110));
                    this.Background = new SolidColorBrush(Color.FromRgb(25, 34, 52));
                }
            };
            this.MouseLeave += (sender, args) =>
            {
                UpdateVisual();
            };

            IsChecked = defaultChecked;
        }

        private void UpdateVisual()
        {
            if (isChecked)
            {
                this.Background = new SolidColorBrush(Color.FromArgb(42, 16, 185, 129));
                this.BorderBrush = Emerald;
                switchPill.Background = Emerald;
                Canvas.SetLeft(knob, 25);
                statusLbl.Text = "ON";
                statusLbl.Foreground = Emerald;
            }
            else
            {
                this.Background = CardBg;
                this.BorderBrush = CardBorder;
                switchPill.Background = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                Canvas.SetLeft(knob, 3);
                statusLbl.Text = "OFF";
                statusLbl.Foreground = TextDim;
            }
        }
    }

    public class MainWindow : Window
    {
        private enum Step
        {
            Welcome = 0,
            Agreement = 1,
            Destination = 2,
            Options = 3,
            Installing = 4,
            Finished = 5
        }

        private Step currentStep = Step.Welcome;

        // UI Containers
        private Border rootBorder;
        private Grid mainGrid;
        private Grid headerGrid;
        private ContentControl pageContent;
        private Grid footerGrid;

        // Stepper Dots
        private StackPanel stepperPanel;
        private Border[] stepDots;
        private TextBlock[] stepLabels;

        // Nav Buttons
        private Button btnBack;
        private Button btnNext;
        private Button btnCancel;

        // Pages
        private Grid viewWelcome;
        private Grid viewAgreement;
        private Grid viewDestination;
        private Grid viewOptions;
        private Grid viewInstalling;
        private Grid viewFinished;

        // Controls
        private RadioButton rbAccept;
        private RadioButton rbReject;
        private TextBox txtInstallPath;
        private TextBlock lblFreeSpace;
        private ModernOptionCard optDesktop;
        private ModernOptionCard optStartMenu;
        private ModernOptionCard optBrowser;
        private ModernOptionCard optStartup;
        private ProgressBar prgInstall;
        private TextBlock lblInstallStatus;
        private TextBlock lblInstallFile;
        private TextBlock[] checklistLabels;
        private CheckBox chkLaunchNow;

        // Colors
        private readonly Brush BrushBgDarkest = new SolidColorBrush(Color.FromRgb(7, 10, 15));
        private readonly Brush BrushBgMain = new SolidColorBrush(Color.FromRgb(11, 15, 23));
        private readonly Brush BrushCard = new SolidColorBrush(Color.FromRgb(20, 27, 40));
        private readonly Brush BrushCardBorder = new SolidColorBrush(Color.FromRgb(40, 50, 70));
        private readonly Brush BrushEmerald = new SolidColorBrush(Color.FromRgb(16, 185, 129));
        private readonly Brush BrushEmeraldDark = new SolidColorBrush(Color.FromRgb(5, 150, 105));
        private readonly Brush BrushCyan = new SolidColorBrush(Color.FromRgb(6, 182, 212));
        private readonly Brush BrushTextWhite = new SolidColorBrush(Color.FromRgb(248, 250, 252));
        private readonly Brush BrushTextMuted = new SolidColorBrush(Color.FromRgb(148, 163, 184));
        private readonly Brush BrushTextDim = new SolidColorBrush(Color.FromRgb(100, 116, 139));

        private const string AppName = "FastDownloader Pro";
        private const string AppVersion = "0.1.3";
        private const string AppPublisher = "ZaxMil";
        private const string NativeHostName = "com.fastdownloader.app";
        private const string GitHubUrl = "https://github.com/ZaxMil/fast-downloader";
        private const string KofiUrl = "https://ko-fi.com/zaxmi";

        public MainWindow()
        {
            InitializeWindow();
            BuildUi();
            ShowStep(Step.Welcome);
        }

        private void InitializeWindow()
        {
            this.Title = AppName + " Setup";
            this.Width = 680;
            this.Height = 520;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.WindowStyle = WindowStyle.None;
            this.AllowsTransparency = true;
            this.Background = Brushes.Transparent;
            this.ResizeMode = ResizeMode.NoResize;

            // Allow dragging window from title bar
            this.MouseDown += (s, e) =>
            {
                if (e.ChangedButton == MouseButton.Left)
                    this.DragMove();
            };
        }

        private void BuildUi()
        {
            rootBorder = new Border
            {
                CornerRadius = new CornerRadius(16),
                Background = BrushBgMain,
                BorderBrush = BrushCardBorder,
                BorderThickness = new Thickness(1),
                ClipToBounds = true
            };

            mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) });  // Header
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // Page Content
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(68) });  // Footer

            BuildHeader();
            BuildFooter();
            BuildPages();

            Grid.SetRow(headerGrid, 0);
            Grid.SetRow(pageContent, 1);
            Grid.SetRow(footerGrid, 2);

            mainGrid.Children.Add(headerGrid);
            mainGrid.Children.Add(pageContent);
            mainGrid.Children.Add(footerGrid);

            rootBorder.Child = mainGrid;
            this.Content = rootBorder;
        }

        // ================= HEADER =================
        private void BuildHeader()
        {
            headerGrid = new Grid
            {
                Background = BrushBgDarkest
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(210) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });

            // App branding on left
            StackPanel brand = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(20, 0, 0, 0)
            };

            TextBlock icon = new TextBlock
            {
                Text = "⚡",
                FontSize = 22,
                Foreground = BrushEmerald,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            brand.Children.Add(icon);

            StackPanel brandText = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock title = new TextBlock
            {
                Text = AppName,
                FontWeight = FontWeights.Bold,
                FontSize = 13.5,
                Foreground = BrushTextWhite
            };
            TextBlock ver = new TextBlock
            {
                Text = "Setup Wizard v" + AppVersion,
                FontSize = 10.5,
                Foreground = BrushTextDim
            };
            brandText.Children.Add(title);
            brandText.Children.Add(ver);
            brand.Children.Add(brandText);
            Grid.SetColumn(brand, 0);

            // Stepper pills in center
            stepperPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            string[] stepNames = new string[] { "Welcome", "License", "Folder", "Tasks", "Install", "Done" };
            stepDots = new Border[stepNames.Length];
            stepLabels = new TextBlock[stepNames.Length];

            for (int i = 0; i < stepNames.Length; i++)
            {
                Border dot = new Border
                {
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(9, 4, 9, 4),
                    Margin = new Thickness(2, 0, 2, 0),
                    Background = Brushes.Transparent
                };
                TextBlock lbl = new TextBlock
                {
                    Text = (i + 1) + ". " + stepNames[i],
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = BrushTextDim
                };
                dot.Child = lbl;
                stepDots[i] = dot;
                stepLabels[i] = lbl;
                stepperPanel.Children.Add(dot);
            }
            Grid.SetColumn(stepperPanel, 1);

            // Window control buttons on right (Minimize, Close)
            StackPanel winControls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 10, 14, 0)
            };

            Button btnMin = CreateIconBtn("─", 28, 28);
            btnMin.Click += (s, e) => this.WindowState = WindowState.Minimized;

            Button btnClose = CreateIconBtn("✕", 28, 28);
            btnClose.Click += (s, e) => ConfirmExit();

            winControls.Children.Add(btnMin);
            winControls.Children.Add(btnClose);
            Grid.SetColumn(winControls, 2);

            headerGrid.Children.Add(brand);
            headerGrid.Children.Add(stepperPanel);
            headerGrid.Children.Add(winControls);

            Border divider = new Border
            {
                Height = 1,
                Background = BrushCardBorder,
                VerticalAlignment = VerticalAlignment.Bottom
            };
            Grid.SetColumnSpan(divider, 3);
            headerGrid.Children.Add(divider);
        }

        private Button CreateIconBtn(string glyph, double w, double h)
        {
            Button btn = new Button
            {
                Content = glyph,
                Width = w,
                Height = h,
                Foreground = BrushTextMuted,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                FontSize = 11,
                Cursor = Cursors.Hand,
                Margin = new Thickness(2, 0, 2, 0)
            };
            btn.MouseEnter += (s, e) => { btn.Background = new SolidColorBrush(Color.FromArgb(40, 255, 255, 255)); btn.Foreground = BrushTextWhite; };
            btn.MouseLeave += (s, e) => { btn.Background = Brushes.Transparent; btn.Foreground = BrushTextMuted; };
            return btn;
        }

        // ================= FOOTER =================
        private void BuildFooter()
        {
            footerGrid = new Grid
            {
                Background = BrushBgDarkest
            };

            Border divider = new Border
            {
                Height = 1,
                Background = BrushCardBorder,
                VerticalAlignment = VerticalAlignment.Top
            };
            footerGrid.Children.Add(divider);

            // Left Author branding and quick links
            StackPanel authorSp = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(24, 0, 0, 0)
            };

            TextBlock authorLabel = new TextBlock
            {
                Text = "by ZaxMil",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BrushTextMuted,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 10, 0)
            };

            Button footGh = CreateLinkPill("🐱", "GitHub", GitHubUrl, Color.FromRgb(20, 27, 40), Color.FromRgb(40, 50, 70), Colors.White);
            Button footKofi = CreateLinkPill("☕", "Ko-fi", KofiUrl, Color.FromArgb(40, 255, 94, 91), Color.FromRgb(255, 94, 91), Color.FromRgb(255, 94, 91));
            footGh.Margin = new Thickness(0, 0, 6, 0);

            authorSp.Children.Add(authorLabel);
            authorSp.Children.Add(footGh);
            authorSp.Children.Add(footKofi);
            footerGrid.Children.Add(authorSp);

            // Right navigation buttons
            StackPanel btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 24, 0)
            };

            btnBack = CreateStyledButton("< Back", false, 95);
            btnBack.Click += (s, e) => GoPrev();

            btnNext = CreateStyledButton("Next >", true, 115);
            btnNext.Click += (s, e) => GoNext();

            btnCancel = CreateStyledButton("Cancel", false, 90);
            btnCancel.Click += (s, e) => ConfirmExit();

            btnPanel.Children.Add(btnBack);
            btnPanel.Children.Add(btnNext);
            btnPanel.Children.Add(btnCancel);

            footerGrid.Children.Add(btnPanel);
        }

        private Button CreateLinkPill(string icon, string label, string url, Color bg, Color border, Color fg)
        {
            Button btn = new Button
            {
                Cursor = Cursors.Hand,
                Height = 28,
                Background = new SolidColorBrush(bg),
                BorderBrush = new SolidColorBrush(border),
                BorderThickness = new Thickness(1),
                Foreground = new SolidColorBrush(fg),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold
            };

            StackPanel sp = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(10, 0, 10, 0) };
            TextBlock i = new TextBlock { Text = icon, Margin = new Thickness(0, 0, 5, 0), FontSize = 12, VerticalAlignment = VerticalAlignment.Center };
            TextBlock l = new TextBlock { Text = label, VerticalAlignment = VerticalAlignment.Center };
            sp.Children.Add(i);
            sp.Children.Add(l);

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));
            FrameworkElementFactory cp = new FrameworkElementFactory(typeof(ContentPresenter));
            cp.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            cp.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(cp);
            template.VisualTree = borderFactory;
            btn.Template = template;
            btn.Content = sp;

            btn.Click += (s, e) => OpenUrl(url);
            btn.MouseEnter += (s, e) => { btn.Opacity = 0.85; };
            btn.MouseLeave += (s, e) => { btn.Opacity = 1.0; };

            return btn;
        }

        public static void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch
            {
                try { Process.Start(url); } catch { }
            }
        }

        private Button CreateStyledButton(string text, bool isPrimary, double minWidth)
        {
            Button btn = new Button
            {
                Content = text,
                MinWidth = minWidth,
                Height = 36,
                Margin = new Thickness(6, 0, 0, 0),
                Cursor = Cursors.Hand,
                FontSize = 12,
                FontWeight = isPrimary ? FontWeights.Bold : FontWeights.Normal
            };

            StyleButtonVisual(btn, isPrimary);
            return btn;
        }

        private void StyleButtonVisual(Button btn, bool isPrimary)
        {
            if (isPrimary)
            {
                btn.Background = new LinearGradientBrush(
                    Color.FromRgb(16, 185, 129),
                    Color.FromRgb(5, 150, 105),
                    new Point(0, 0),
                    new Point(1, 1)
                );
                btn.Foreground = Brushes.White;
                btn.BorderThickness = new Thickness(0);
                btn.MouseEnter += (s, e) => { btn.Background = BrushEmeraldDark; };
                btn.MouseLeave += (s, e) =>
                {
                    btn.Background = new LinearGradientBrush(
                        Color.FromRgb(16, 185, 129),
                        Color.FromRgb(5, 150, 105),
                        new Point(0, 0),
                        new Point(1, 1)
                    );
                };
            }
            else
            {
                btn.Background = BrushCard;
                btn.Foreground = BrushTextWhite;
                btn.BorderBrush = BrushCardBorder;
                btn.BorderThickness = new Thickness(1);
                btn.MouseEnter += (s, e) => { btn.Background = new SolidColorBrush(Color.FromArgb(255, 36, 48, 70)); };
                btn.MouseLeave += (s, e) => { btn.Background = BrushCard; };
            }

            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(Button.BackgroundProperty));
            borderFactory.SetValue(Border.BorderBrushProperty, new TemplateBindingExtension(Button.BorderBrushProperty));
            borderFactory.SetValue(Border.BorderThicknessProperty, new TemplateBindingExtension(Button.BorderThicknessProperty));

            FrameworkElementFactory contentFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentFactory.SetValue(ContentPresenter.HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentFactory.SetValue(ContentPresenter.VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentFactory);

            template.VisualTree = borderFactory;
            btn.Template = template;
        }

        // ================= PAGES =================
        private void BuildPages()
        {
            pageContent = new ContentControl();

            BuildWelcomePage();
            BuildAgreementPage();
            BuildDestinationPage();
            BuildOptionsPage();
            BuildInstallingPage();
            BuildFinishedPage();
        }

        // Page 0: Welcome
        private void BuildWelcomePage()
        {
            viewWelcome = new Grid { Margin = new Thickness(36, 24, 36, 24) };
            viewWelcome.RowDefinitions.Add(new RowDefinition { Height = new GridLength(110) });
            viewWelcome.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel hero = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            TextBlock h1 = new TextBlock
            {
                Text = "⚡ Accelerate Your Downloads",
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                Foreground = BrushTextWhite,
                Margin = new Thickness(0, 0, 0, 6)
            };
            TextBlock h2 = new TextBlock
            {
                Text = "FastDownloader Pro installs in seconds with 16-connection parallel chunk streaming.",
                FontSize = 13,
                Foreground = BrushTextMuted
            };
            hero.Children.Add(h1);
            hero.Children.Add(h2);
            Grid.SetRow(hero, 0);

            UniformGrid cards = new UniformGrid { Columns = 2, Margin = new Thickness(0, 10, 0, 0) };
            cards.Children.Add(CreateFeatureCard("🚀 16x Parallel Turbo", "Splits downloads into 16 concurrent chunk streams for maximum gigabit bandwidth saturation."));
            cards.Children.Add(CreateFeatureCard("🌐 Browser Integration", "Native companion extension catches downloads automatically in Microsoft Edge, Chrome & Brave."));
            cards.Children.Add(CreateFeatureCard("🎬 Video & Stream Sniffer", "Captures media streams from YouTube, TikTok, HLS, and social platforms in high quality."));
            cards.Children.Add(CreateFeatureCard("🔒 Zero Telemetry", "100% private, open-source client. No external tracking, ads, or background analytics."));

            Grid.SetRow(cards, 1);

            viewWelcome.Children.Add(hero);
            viewWelcome.Children.Add(cards);
        }

        private Border CreateFeatureCard(string title, string desc)
        {
            Border card = new Border
            {
                Background = BrushCard,
                BorderBrush = BrushCardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(6),
                Padding = new Thickness(16, 14, 16, 14),
                Cursor = Cursors.Hand
            };

            card.MouseEnter += (s, e) =>
            {
                card.BorderBrush = BrushEmerald;
                card.Background = new SolidColorBrush(Color.FromArgb(35, 16, 185, 129));
            };
            card.MouseLeave += (s, e) =>
            {
                card.BorderBrush = BrushCardBorder;
                card.Background = BrushCard;
            };

            StackPanel sp = new StackPanel();
            TextBlock t = new TextBlock
            {
                Text = title,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = BrushEmerald,
                Margin = new Thickness(0, 0, 0, 6)
            };
            TextBlock d = new TextBlock
            {
                Text = desc,
                FontSize = 11,
                Foreground = BrushTextMuted,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16
            };
            sp.Children.Add(t);
            sp.Children.Add(d);
            card.Child = sp;
            return card;
        }

        // Page 1: Agreement
        private void BuildAgreementPage()
        {
            viewAgreement = new Grid { Margin = new Thickness(36, 14, 36, 14) };
            viewAgreement.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            viewAgreement.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            viewAgreement.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            StackPanel head = new StackPanel { Margin = new Thickness(0, 0, 0, 8) };
            TextBlock h1 = new TextBlock
            {
                Text = "License Agreement & Terms of Service",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = BrushTextWhite,
                Margin = new Thickness(0, 0, 0, 2)
            };
            TextBlock h2 = new TextBlock
            {
                Text = "Please review the agreement below carefully before installing FastDownloader Pro:",
                FontSize = 11.5,
                Foreground = BrushTextMuted
            };
            head.Children.Add(h1);
            head.Children.Add(h2);
            Grid.SetRow(head, 0);

            // Rich document viewer inside modern card
            Border docCard = new Border
            {
                Background = BrushCard,
                BorderBrush = BrushCardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 14, 16, 14)
            };

            ScrollViewer sv = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
            };

            StackPanel doc = new StackPanel();

            // Summary Callout Box
            Border alert = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                BorderBrush = BrushEmerald,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 14)
            };
            TextBlock alertText = new TextBlock
            {
                Text = "🛡️ Summary for Users: FastDownloader Pro is 100% free and open-source under the MIT License. It operates entirely locally on your device with zero telemetry and zero external data collection. You are responsible for ensuring that all downloads comply with copyright and platform terms.",
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16
            };
            alert.Child = alertText;
            doc.Children.Add(alert);

            // Detailed Sections
            AddLegalSection(doc, "1. LICENSE GRANT & OPEN SOURCE ATTRIBUTION",
                "FastDownloader Pro is provided under the terms of the MIT License.\n\n" +
                "Copyright (c) 2026 FastDownloader Contributors.\n\n" +
                "Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the 'Software'), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, subject to the condition that the copyright notice and permission notice remain included in all copies or substantial portions of the Software.");

            AddLegalSection(doc, "2. ACCEPTABLE USE & LEGAL COMPLIANCE",
                "FastDownloader Pro is a high-velocity network client and download acceleration utility designed to manage file transfers and media archival.\n\n" +
                "• Permitted Usage: You may use FastDownloader Pro to download content that you own, open-access public datasets, free software distributions, and media for which you possess express copyright permission or licensing.\n\n" +
                "• Prohibited Circumvention: FastDownloader Pro does NOT contain any technology designed to circumvent Digital Rights Management (DRM), encrypted media streams (such as Widevine, PlayReady, or FairPlay), or paywalled subscription services.\n\n" +
                "• User Responsibility: You agree to comply with all applicable local, national, and international laws, regulations, and third-party Terms of Service when using this software.");

            AddLegalSection(doc, "3. PRIVACY & ZERO-TELEMETRY COMMITMENT",
                "We are committed to absolute user privacy. FastDownloader Pro adheres to a strict Zero-Telemetry guarantee:\n\n" +
                "• 100% Local Processing: All communication between the desktop application and the companion browser extension is conducted exclusively through local loopback connections (127.0.0.1) and standard Windows Native Messaging pipes (stdin/stdout).\n\n" +
                "• No Remote Tracking: FastDownloader Pro does NOT log, transmit, sell, or collect browsing history, file URLs, authentication credentials, or user identities.\n\n" +
                "• No Advertising SDKs: There are no third-party analytic trackers, marketing beacons, or advertising SDKs bundled in this software.");

            AddLegalSection(doc, "4. THIRD-PARTY TRADEMARKS & NOTICES",
                "• 'Internet Download Manager' (IDM) is a registered trademark of Tonec Inc. FastDownloader Pro is an independent open-source project and is not affiliated with, endorsed by, or sponsored by Tonec Inc.\n\n" +
                "• Google Chrome, Microsoft Edge, Brave Browser, YouTube, and other trademarks are the property of their respective trademark holders.\n\n" +
                "• Underlying download engines (aria2, yt-dlp, ffmpeg) are distributed under their respective open-source licenses (GPL, Unlicense, LGPL).");

            AddLegalSection(doc, "5. DISCLAIMER OF WARRANTIES & LIMITATION OF LIABILITY",
                "THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE, AND NONINFRINGEMENT.\n\n" +
                "IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES, DATA LOSS, OR OTHER LIABILITY ARISING FROM THE USE OR INABILITY TO USE THIS SOFTWARE.");

            sv.Content = doc;
            docCard.Child = sv;
            Grid.SetRow(docCard, 1);

            // Acceptance Card
            Border acceptCard = new Border
            {
                Background = BrushCard,
                BorderBrush = BrushCardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 8, 0, 0)
            };

            StackPanel radios = new StackPanel();
            rbAccept = new RadioButton
            {
                Content = "I have read, understood, and accept the license agreement and terms of use",
                Foreground = BrushTextWhite,
                FontWeight = FontWeights.Bold,
                FontSize = 11.5,
                Cursor = Cursors.Hand
            };
            rbAccept.Checked += (s, e) => btnNext.IsEnabled = true;

            rbReject = new RadioButton
            {
                Content = "I do not accept the agreement (Setup will not proceed)",
                Foreground = BrushTextDim,
                FontSize = 10.5,
                IsChecked = true,
                Margin = new Thickness(0, 3, 0, 0),
                Cursor = Cursors.Hand
            };
            rbReject.Checked += (s, e) => btnNext.IsEnabled = false;

            radios.Children.Add(rbAccept);
            radios.Children.Add(rbReject);
            acceptCard.Child = radios;
            Grid.SetRow(acceptCard, 2);

            viewAgreement.Children.Add(head);
            viewAgreement.Children.Add(docCard);
            viewAgreement.Children.Add(acceptCard);
        }

        private void AddLegalSection(StackPanel parent, string title, string body)
        {
            TextBlock t = new TextBlock
            {
                Text = title,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = BrushEmerald,
                Margin = new Thickness(0, 8, 0, 4)
            };
            TextBlock b = new TextBlock
            {
                Text = body,
                FontSize = 11,
                Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 16,
                Margin = new Thickness(0, 0, 0, 10)
            };
            parent.Children.Add(t);
            parent.Children.Add(b);
        }

        // Page 2: Destination
        private void BuildDestinationPage()
        {
            viewDestination = new Grid { Margin = new Thickness(36, 24, 36, 24) };
            viewDestination.RowDefinitions.Add(new RowDefinition { Height = new GridLength(55) });
            viewDestination.RowDefinitions.Add(new RowDefinition { Height = new GridLength(70) });
            viewDestination.RowDefinitions.Add(new RowDefinition { Height = new GridLength(100) });

            StackPanel head = new StackPanel();
            TextBlock h1 = new TextBlock
            {
                Text = "Installation Location",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = BrushTextWhite
            };
            TextBlock h2 = new TextBlock
            {
                Text = "Choose where FastDownloader Pro will be installed on your computer:",
                FontSize = 12,
                Foreground = BrushTextMuted
            };
            head.Children.Add(h1);
            head.Children.Add(h2);
            Grid.SetRow(head, 0);

            Grid pathBox = new Grid();
            pathBox.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            pathBox.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(105) });

            string defDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\" + AppName);

            Border txtWrapper = new Border
            {
                Background = BrushCard,
                BorderBrush = BrushCardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Height = 38,
                Padding = new Thickness(12, 0, 12, 0),
                Margin = new Thickness(0, 0, 10, 0)
            };

            txtInstallPath = new TextBox
            {
                Text = defDir,
                Background = Brushes.Transparent,
                Foreground = BrushTextWhite,
                BorderThickness = new Thickness(0),
                FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            };
            txtInstallPath.TextChanged += (s, e) => UpdateDiskInfo();
            txtWrapper.Child = txtInstallPath;
            Grid.SetColumn(txtWrapper, 0);

            Button btnBrowse = CreateStyledButton("Browse...", false, 95);
            btnBrowse.Height = 38;
            btnBrowse.Margin = new Thickness(0);
            btnBrowse.Click += (s, e) =>
            {
                using (var fbd = new System.Windows.Forms.FolderBrowserDialog())
                {
                    fbd.Description = "Select Destination Folder for " + AppName;
                    fbd.SelectedPath = txtInstallPath.Text;
                    if (fbd.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        txtInstallPath.Text = System.IO.Path.Combine(fbd.SelectedPath, AppName);
                    }
                }
            };
            Grid.SetColumn(btnBrowse, 1);

            pathBox.Children.Add(txtWrapper);
            pathBox.Children.Add(btnBrowse);
            Grid.SetRow(pathBox, 1);

            Border spaceCard = new Border
            {
                Background = BrushCard,
                BorderBrush = BrushCardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(16, 12, 16, 12),
                Margin = new Thickness(0, 10, 0, 0)
            };

            StackPanel spaceSp = new StackPanel();
            TextBlock req = new TextBlock
            {
                Text = "📦 Disk Space Required: ~45.0 MB",
                FontSize = 12,
                Foreground = BrushTextWhite,
                Margin = new Thickness(0, 0, 0, 4)
            };
            lblFreeSpace = new TextBlock
            {
                Text = "💾 Space Available: Checking drive...",
                FontSize = 12,
                Foreground = BrushEmerald
            };
            spaceSp.Children.Add(req);
            spaceSp.Children.Add(lblFreeSpace);
            spaceCard.Child = spaceSp;
            Grid.SetRow(spaceCard, 2);

            viewDestination.Children.Add(head);
            viewDestination.Children.Add(pathBox);
            viewDestination.Children.Add(spaceCard);

            UpdateDiskInfo();
        }

        private void UpdateDiskInfo()
        {
            try
            {
                string path = txtInstallPath.Text;
                if (!string.IsNullOrEmpty(path))
                {
                    string root = System.IO.Path.GetPathRoot(System.IO.Path.GetFullPath(path));
                    DriveInfo drive = new DriveInfo(root);
                    double freeGb = (double)drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                    lblFreeSpace.Text = string.Format("💾 Available on Drive ({0}): {1:N1} GB (Ready to install)", root, freeGb);
                }
            }
            catch
            {
                lblFreeSpace.Text = "💾 Available Drive Space: Ready";
            }
        }

        // Page 3: Options
        private void BuildOptionsPage()
        {
            viewOptions = new Grid { Margin = new Thickness(36, 20, 36, 20) };
            viewOptions.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            viewOptions.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel head = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
            TextBlock h1 = new TextBlock
            {
                Text = "Installation Options & Shortcuts",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = BrushTextWhite,
                Margin = new Thickness(0, 0, 0, 2)
            };
            TextBlock h2 = new TextBlock
            {
                Text = "Configure system shortcuts and browser integration (click anywhere on a card to toggle):",
                FontSize = 11.5,
                Foreground = BrushTextMuted
            };
            head.Children.Add(h1);
            head.Children.Add(h2);
            Grid.SetRow(head, 0);

            StackPanel optionsList = new StackPanel();

            optDesktop = new ModernOptionCard("🖥️", "Create Desktop Shortcut", "Places an instant-access turbo launch icon on your desktop", true);
            optStartMenu = new ModernOptionCard("📑", "Create Start Menu Shortcut", "Pins FastDownloader Pro to your Windows Start Menu and Search", true);
            optBrowser = new ModernOptionCard("🌐", "Register Browser Integration", "Enables Chrome, Edge, and Brave to route downloads to FastDownloader", true);
            optStartup = new ModernOptionCard("⚡", "Launch Minimized at Windows Boot", "Starts the turbo engine quietly in system tray when your PC boots", false);

            optionsList.Children.Add(optDesktop);
            optionsList.Children.Add(optStartMenu);
            optionsList.Children.Add(optBrowser);
            optionsList.Children.Add(optStartup);
            Grid.SetRow(optionsList, 1);

            viewOptions.Children.Add(head);
            viewOptions.Children.Add(optionsList);
        }

        // Page 4: Installing
        private void BuildInstallingPage()
        {
            viewInstalling = new Grid { Margin = new Thickness(36, 24, 36, 24) };
            viewInstalling.RowDefinitions.Add(new RowDefinition { Height = new GridLength(50) });
            viewInstalling.RowDefinitions.Add(new RowDefinition { Height = new GridLength(45) });
            viewInstalling.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            StackPanel head = new StackPanel();
            lblInstallStatus = new TextBlock
            {
                Text = "⚡ Installing FastDownloader Pro...",
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = BrushTextWhite
            };
            lblInstallFile = new TextBlock
            {
                Text = "Extracting files to installation directory...",
                FontSize = 11,
                Foreground = BrushTextMuted,
                Margin = new Thickness(0, 4, 0, 0)
            };
            head.Children.Add(lblInstallStatus);
            head.Children.Add(lblInstallFile);
            Grid.SetRow(head, 0);

            // Styled Progress Bar with rounded border and neon gradient
            prgInstall = new ProgressBar
            {
                Height = 12,
                Minimum = 0,
                Maximum = 100,
                Value = 0,
                Foreground = BrushEmerald,
                Background = BrushCard,
                BorderThickness = new Thickness(0)
            };

            Style prgStyle = new Style(typeof(ProgressBar));
            ControlTemplate prgTemplate = new ControlTemplate(typeof(ProgressBar));
            FrameworkElementFactory trackBorder = new FrameworkElementFactory(typeof(Border));
            trackBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
            trackBorder.SetValue(Border.BackgroundProperty, BrushCard);
            trackBorder.SetValue(Border.BorderBrushProperty, BrushCardBorder);
            trackBorder.SetValue(Border.BorderThicknessProperty, new Thickness(1));
            trackBorder.SetValue(Border.ClipToBoundsProperty, true);

            FrameworkElementFactory indicatorBorder = new FrameworkElementFactory(typeof(Border));
            indicatorBorder.Name = "PART_Indicator";
            indicatorBorder.SetValue(Border.CornerRadiusProperty, new CornerRadius(5));
            indicatorBorder.SetValue(Border.HorizontalAlignmentProperty, HorizontalAlignment.Left);
            indicatorBorder.SetValue(Border.BackgroundProperty, new LinearGradientBrush(
                Color.FromRgb(16, 185, 129),
                Color.FromRgb(6, 182, 212),
                new Point(0, 0),
                new Point(1, 0)
            ));
            trackBorder.AppendChild(indicatorBorder);
            prgTemplate.VisualTree = trackBorder;
            prgStyle.Setters.Add(new Setter(ProgressBar.TemplateProperty, prgTemplate));
            prgInstall.Style = prgStyle;

            Grid.SetRow(prgInstall, 1);

            Border checkCard = new Border
            {
                Background = BrushCard,
                BorderBrush = BrushCardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 14, 18, 14),
                Margin = new Thickness(0, 12, 0, 0)
            };

            StackPanel sp = new StackPanel();
            TextBlock cat = new TextBlock
            {
                Text = "COMPONENTS BEING CONFIGURED:",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = BrushEmerald,
                Margin = new Thickness(0, 0, 0, 10)
            };
            sp.Children.Add(cat);

            string[] steps = new string[]
            {
                "FastDownloader Pro Core Binary & Obsidian Dashboard",
                "Turbo 16-Connection Download Engine (aria2c)",
                "Universal Media Stream Sniffer (yt-dlp)",
                "Browser Native Messaging Hosts (Edge, Chrome, Brave)",
                "Desktop and Start Menu Shortcuts"
            };

            checklistLabels = new TextBlock[steps.Length];
            for (int i = 0; i < steps.Length; i++)
            {
                TextBlock l = new TextBlock
                {
                    Text = "⏳  " + steps[i],
                    FontSize = 11.5,
                    Foreground = BrushTextMuted,
                    Margin = new Thickness(0, 3, 0, 3)
                };
                checklistLabels[i] = l;
                sp.Children.Add(l);
            }
            checkCard.Child = sp;
            Grid.SetRow(checkCard, 2);

            viewInstalling.Children.Add(head);
            viewInstalling.Children.Add(prgInstall);
            viewInstalling.Children.Add(checkCard);
        }

        // Page 5: Finished
        private void BuildFinishedPage()
        {
            viewFinished = new Grid { Margin = new Thickness(36, 16, 36, 16) };

            StackPanel sp = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            Border iconCircle = new Border
            {
                Width = 56,
                Height = 56,
                CornerRadius = new CornerRadius(28),
                Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129)),
                BorderBrush = BrushEmerald,
                BorderThickness = new Thickness(2),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 10)
            };
            TextBlock check = new TextBlock
            {
                Text = "✓",
                FontSize = 28,
                FontWeight = FontWeights.Bold,
                Foreground = BrushEmerald,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            iconCircle.Child = check;

            TextBlock title = new TextBlock
            {
                Text = "Installation Completed Successfully!",
                FontSize = 19,
                FontWeight = FontWeights.Bold,
                Foreground = BrushTextWhite,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 6)
            };

            TextBlock desc = new TextBlock
            {
                Text = "FastDownloader Pro is ready to accelerate your downloads at maximum speed.",
                FontSize = 12.5,
                Foreground = BrushTextMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 12)
            };

            chkLaunchNow = new CheckBox
            {
                Content = "Launch FastDownloader Pro now",
                IsChecked = true,
                Foreground = BrushTextWhite,
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 14),
                Cursor = Cursors.Hand
            };

            // Developer attribution & support card
            Border devCard = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(17, 24, 39)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(35, 48, 68)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(18, 10, 18, 10),
                HorizontalAlignment = HorizontalAlignment.Center,
                MaxWidth = 440
            };

            StackPanel cardSp = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
            TextBlock devNote = new TextBlock
            {
                Text = "Developed with ❤️ by ZaxMil • Open source & community supported",
                FontSize = 11.5,
                FontWeight = FontWeights.Medium,
                Foreground = BrushTextMuted,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 8)
            };

            StackPanel btnRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            Button btnStar = CreateLinkPill("⭐", "Star on GitHub", GitHubUrl, Color.FromRgb(20, 27, 40), Color.FromRgb(40, 50, 70), Colors.White);
            Button btnKofi = CreateLinkPill("☕", "Support on Ko-fi", KofiUrl, Color.FromArgb(40, 255, 94, 91), Color.FromRgb(255, 94, 91), Color.FromRgb(255, 94, 91));
            btnStar.Margin = new Thickness(0, 0, 10, 0);

            btnRow.Children.Add(btnStar);
            btnRow.Children.Add(btnKofi);

            cardSp.Children.Add(devNote);
            cardSp.Children.Add(btnRow);
            devCard.Child = cardSp;

            sp.Children.Add(iconCircle);
            sp.Children.Add(title);
            sp.Children.Add(desc);
            sp.Children.Add(chkLaunchNow);
            sp.Children.Add(devCard);

            viewFinished.Children.Add(sp);
        }

        // ================= NAVIGATION =================
        private void ShowStep(Step step)
        {
            currentStep = step;

            for (int i = 0; i < stepDots.Length; i++)
            {
                if (i == (int)step)
                {
                    stepDots[i].Background = new SolidColorBrush(Color.FromArgb(40, 16, 185, 129));
                    stepDots[i].BorderBrush = BrushEmerald;
                    stepDots[i].BorderThickness = new Thickness(1);
                    stepLabels[i].Foreground = BrushEmerald;
                }
                else if (i < (int)step)
                {
                    stepDots[i].Background = Brushes.Transparent;
                    stepDots[i].BorderThickness = new Thickness(0);
                    stepLabels[i].Foreground = BrushTextMuted;
                }
                else
                {
                    stepDots[i].Background = Brushes.Transparent;
                    stepDots[i].BorderThickness = new Thickness(0);
                    stepLabels[i].Foreground = BrushTextDim;
                }
            }

            btnBack.Visibility = (step == Step.Welcome || step == Step.Finished || step == Step.Installing) ? Visibility.Collapsed : Visibility.Visible;
            btnCancel.Visibility = (step == Step.Finished || step == Step.Installing) ? Visibility.Collapsed : Visibility.Visible;

            if (step == Step.Options)
            {
                btnNext.Content = "⚡ Install Now";
            }
            else if (step == Step.Finished)
            {
                btnNext.Content = "Finish";
            }
            else
            {
                btnNext.Content = "Next >";
            }

            switch (step)
            {
                case Step.Welcome:
                    pageContent.Content = viewWelcome;
                    btnNext.IsEnabled = true;
                    break;
                case Step.Agreement:
                    pageContent.Content = viewAgreement;
                    btnNext.IsEnabled = rbAccept.IsChecked == true;
                    break;
                case Step.Destination:
                    pageContent.Content = viewDestination;
                    btnNext.IsEnabled = true;
                    break;
                case Step.Options:
                    pageContent.Content = viewOptions;
                    btnNext.IsEnabled = true;
                    break;
                case Step.Installing:
                    pageContent.Content = viewInstalling;
                    btnNext.Visibility = Visibility.Collapsed;
                    StartInstallation();
                    break;
                case Step.Finished:
                    pageContent.Content = viewFinished;
                    btnNext.Visibility = Visibility.Visible;
                    btnNext.IsEnabled = true;
                    break;
            }
        }

        private void GoNext()
        {
            if (currentStep == Step.Finished)
            {
                if (chkLaunchNow.IsChecked == true)
                {
                    string target = System.IO.Path.Combine(txtInstallPath.Text.Trim(), "FastDownloader.exe");
                    if (File.Exists(target))
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(target)
                        {
                            WorkingDirectory = txtInstallPath.Text.Trim()
                        };
                        Process.Start(psi);
                    }
                }
                this.Close();
                return;
            }

            int next = (int)currentStep + 1;
            if (next <= (int)Step.Finished)
            {
                ShowStep((Step)next);
            }
        }

        private void GoPrev()
        {
            int prev = (int)currentStep - 1;
            if (prev >= 0)
            {
                ShowStep((Step)prev);
            }
        }

        private void ConfirmExit()
        {
            if (currentStep == Step.Installing) return;

            if (MessageBox.Show("Are you sure you want to cancel the installation?", "Exit Setup", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            {
                this.Close();
            }
        }

        // ================= BACKGROUND INSTALLATION =================
        private void StartInstallation()
        {
            string targetDir = txtInstallPath.Text.Trim();
            bool makeDesktop = optDesktop.IsChecked;
            bool makeStart = optStartMenu.IsChecked;
            bool regBrowser = optBrowser.IsChecked;
            bool autoStart = optStartup.IsChecked;

            Thread t = new Thread(() =>
            {
                try
                {
                    UpdateInstallUi(5, "Stopping existing background processes...", "Terminating any running instances");
                    foreach (Process p in Process.GetProcessesByName("FastDownloader")) { try { p.Kill(); p.WaitForExit(1500); } catch { } }
                    foreach (Process p in Process.GetProcessesByName("aria2c")) { try { p.Kill(); p.WaitForExit(1000); } catch { } }

                    if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

                    UpdateInstallUi(15, "Extracting application package...", "Opening bundle...");
                    ExtractPayload(targetDir);

                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        MarkStepDone(0);
                        MarkStepDone(1);
                        MarkStepDone(2);
                    }));

                    UpdateInstallUi(75, "Configuring Browser Integration...", "Updating manifest path...");
                    UpdateNativeManifest(targetDir);

                    if (regBrowser)
                    {
                        RegisterBrowserKeys(targetDir);
                        this.Dispatcher.Invoke(new Action(() => MarkStepDone(3)));
                    }

                    string exePath = System.IO.Path.Combine(targetDir, "FastDownloader.exe");
                    string icoPath = System.IO.Path.Combine(targetDir, "app.ico");

                    if (makeDesktop)
                    {
                        string dPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), AppName + ".lnk");
                        CreateShortcut(dPath, exePath, targetDir, icoPath, AppName);
                    }
                    if (makeStart)
                    {
                        string sPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");
                        CreateShortcut(sPath, exePath, targetDir, icoPath, AppName);
                    }
                    this.Dispatcher.Invoke(new Action(() => MarkStepDone(4)));

                    if (autoStart)
                    {
                        using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                        {
                            if (k != null) k.SetValue("FastDownloader", "\"" + exePath + "\" --minimized");
                        }
                    }

                    RegisterUninstall(targetDir);

                    UpdateInstallUi(100, "Installation Complete!", "Ready to launch.");
                    Thread.Sleep(500);

                    this.Dispatcher.Invoke(new Action(() => ShowStep(Step.Finished)));
                }
                catch (Exception ex)
                {
                    this.Dispatcher.Invoke(new Action(() =>
                    {
                        MessageBox.Show("An error occurred during installation:\n\n" + ex.Message, "Installation Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        btnCancel.Visibility = Visibility.Visible;
                    }));
                }
            });

            t.IsBackground = true;
            t.Start();
        }

        private void UpdateInstallUi(int percent, string status, string file)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                prgInstall.Value = percent;
                lblInstallStatus.Text = status;
                lblInstallFile.Text = file;
            }));
        }

        private void MarkStepDone(int index)
        {
            if (checklistLabels != null && index >= 0 && index < checklistLabels.Length)
            {
                string text = checklistLabels[index].Text.Substring(3);
                checklistLabels[index].Text = "✓  " + text;
                checklistLabels[index].Foreground = BrushEmerald;
            }
        }

        private void ExtractPayload(string targetDir)
        {
            Stream zipStream = null;
            Assembly asm = Assembly.GetExecutingAssembly();
            foreach (string name in asm.GetManifestResourceNames())
            {
                if (name.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase))
                {
                    zipStream = asm.GetManifestResourceStream(name);
                    break;
                }
            }

            if (zipStream == null)
            {
                string localZip = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload.zip");
                if (File.Exists(localZip)) zipStream = File.OpenRead(localZip);
            }

            if (zipStream == null) throw new FileNotFoundException("Setup payload bundle was not found.");

            using (zipStream)
            using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                int total = archive.Entries.Count;
                int count = 0;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    count++;
                    string entryPath = entry.FullName.Replace('/', '\\');
                    string destPath = System.IO.Path.Combine(targetDir, entryPath);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);
                        continue;
                    }

                    string dir = System.IO.Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    int pct = 15 + (int)(((double)count / total) * 55);
                    UpdateInstallUi(pct, "Extracting " + entry.Name, entryPath);

                    entry.ExtractToFile(destPath, true);
                }
            }
        }

        private void UpdateNativeManifest(string targetDir)
        {
            try
            {
                string path = System.IO.Path.Combine(targetDir, "com.fastdownloader.app.json");
                string exe = System.IO.Path.Combine(targetDir, "FastDownloader.exe").Replace("\\", "\\\\");
                string manifest = "{\n" +
                    "  \"name\": \"" + NativeHostName + "\",\n" +
                    "  \"description\": \"FastDownloader Native Messaging Host\",\n" +
                    "  \"path\": \"" + exe + "\",\n" +
                    "  \"type\": \"stdio\",\n" +
                    "  \"allowed_origins\": [\n" +
                    "    \"chrome-extension://knedhffekcklfpbgmghgclgnhpehmjkn/\"\n" +
                    "  ]\n" +
                    "}";
                File.WriteAllText(path, manifest, System.Text.Encoding.UTF8);
            }
            catch { }
        }

        private void RegisterBrowserKeys(string targetDir)
        {
            try
            {
                string path = System.IO.Path.Combine(targetDir, "com.fastdownloader.app.json");
                string[] keys = new string[]
                {
                    @"Software\Google\Chrome\NativeMessagingHosts\" + NativeHostName,
                    @"Software\Microsoft\Edge\NativeMessagingHosts\" + NativeHostName,
                    @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\" + NativeHostName
                };
                foreach (string kPath in keys)
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(kPath))
                    {
                        if (k != null) k.SetValue(null, path);
                    }
                }
            }
            catch { }
        }

        private void RegisterUninstall(string targetDir)
        {
            try
            {
                string cur = Assembly.GetExecutingAssembly().Location;
                string un = System.IO.Path.Combine(targetDir, "uninstall.exe");
                try { if (File.Exists(cur) && !string.Equals(cur, un, StringComparison.OrdinalIgnoreCase)) File.Copy(cur, un, true); } catch { }

                string cmd = "\"" + (File.Exists(un) ? un : cur) + "\" /uninstall";
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\FastDownloader"))
                {
                    if (k != null)
                    {
                        k.SetValue("DisplayName", AppName);
                        k.SetValue("DisplayVersion", "0.1.3");
                        k.SetValue("Publisher", AppPublisher);
                        k.SetValue("DisplayIcon", System.IO.Path.Combine(targetDir, "app.ico") + ",0");
                        k.SetValue("InstallLocation", targetDir);
                        k.SetValue("UninstallString", cmd);
                        k.SetValue("HelpLink", GitHubUrl);
                        k.SetValue("URLInfoAbout", GitHubUrl);
                        k.SetValue("EstimatedSize", 45000, RegistryValueKind.DWord);
                        k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        private void CreateShortcut(string sPath, string tPath, string wDir, string iPath, string desc)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object sc = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { sPath });
                Type st = sc.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, sc, new object[] { tPath });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, sc, new object[] { wDir });
                if (File.Exists(iPath)) st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, sc, new object[] { iPath + ",0" });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, sc, new object[] { desc });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, sc, null);
            }
            catch { }
        }

        private string GetLegalText()
        {
            return
@"FASTDOWNLOADER PRO - END USER LICENSE & TERMS OF USE
Version 0.1.3 | Last Updated: October 2026

1. END USER LICENSE AGREEMENT (MIT LICENSE)
Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the 'Software'), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software.

2. LEGAL DISCLAIMER & ACCEPTABLE USE POLICY
FastDownloader is a general-purpose download acceleration client utility.
- Users are solely responsible for ensuring downloads comply with all applicable
  laws and third-party Terms of Service.
- No DRM Circumvention: Does not bypass Widevine, FairPlay, or encrypted streams.

3. PRIVACY & ZERO TELEMETRY COMMITMENT
- 100% local processing on loopback (127.0.0.1) and native messaging (stdio).
- Zero remote tracking, zero telemetry collection, zero third-party ads.

4. DISCLAIMER OF WARRANTY
THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO WARRANTIES OF MERCHANTABILITY, FITNESS
FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.";
        }
    }
}
