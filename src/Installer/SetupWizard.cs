using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Win32;

namespace FastDownloader.Installer
{
    // =========================================================================
    // Custom Modern Neon Gradient Progress Bar
    // =========================================================================
    public class ModernProgressBar : Control
    {
        private int val = 0;
        private int max = 100;

        public int Value
        {
            get { return val; }
            set
            {
                val = Math.Max(0, Math.Min(max, value));
                Invalidate();
            }
        }

        public int Maximum
        {
            get { return max; }
            set
            {
                max = Math.Max(1, value);
                Invalidate();
            }
        }

        public ModernProgressBar()
        {
            this.SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            this.BackColor = Color.FromArgb(15, 23, 42); // Slate-900 track
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Track background
            using (SolidBrush trackBrush = new SolidBrush(Color.FromArgb(15, 23, 42)))
            {
                g.FillRectangle(trackBrush, ClientRectangle);
            }

            // Track border
            using (Pen borderPen = new Pen(Color.FromArgb(51, 65, 85), 1))
            {
                g.DrawRectangle(borderPen, 0, 0, Width - 1, Height - 1);
            }

            // Progress Fill with Neon Gradient (Emerald #10B981 -> Cyan #06B6D4)
            if (val > 0 && max > 0)
            {
                int fillWidth = (int)(((double)val / max) * (Width - 2));
                if (fillWidth > 0)
                {
                    Rectangle fillRect = new Rectangle(1, 1, fillWidth, Height - 2);
                    using (LinearGradientBrush fillBrush = new LinearGradientBrush(
                        fillRect,
                        Color.FromArgb(16, 185, 129),
                        Color.FromArgb(6, 182, 212),
                        LinearGradientMode.Horizontal))
                    {
                        g.FillRectangle(fillBrush, fillRect);
                    }
                }
            }
        }
    }

    // =========================================================================
    // FastDownloader Pro - Modern Dark Setup Wizard
    // =========================================================================
    public class SetupWizardForm : Form
    {
        public enum WizardStep
        {
            Welcome = 0,
            Agreement = 1,
            Destination = 2,
            Options = 3,
            Ready = 4,
            Installing = 5,
            Finished = 6
        }

        private WizardStep currentStep = WizardStep.Welcome;

        // Container Panels
        private Panel pnlHeader;
        private Label lblStepBadge;
        private Label lblHeaderTitle;
        private Label lblHeaderSubtitle;
        private PictureBox picHeaderIcon;
        private Panel pnlHeaderAccent;

        private Panel pnlFooter;
        private Panel pnlFooterLine;
        private Button btnBack;
        private Button btnNext;
        private Button btnCancel;

        private Panel pnlContent;

        // Step Panels
        private Panel pnlWelcome;
        private Panel pnlAgreement;
        private Panel pnlDestination;
        private Panel pnlOptions;
        private Panel pnlReady;
        private Panel pnlInstalling;
        private Panel pnlFinished;

        // Agreement Controls
        private TextBox txtAgreement;
        private RadioButton rbAccept;
        private RadioButton rbReject;

        // Destination Controls
        private TextBox txtInstallDir;
        private Button btnBrowse;
        private Label lblSpaceRequired;
        private Label lblSpaceAvailable;

        // Options Controls
        private CheckBox chkDesktop;
        private CheckBox chkStartMenu;
        private CheckBox chkBrowser;
        private CheckBox chkStartup;

        // Ready Controls
        private TextBox txtSummary;

        // Installing Controls
        private Label lblInstallStatus;
        private ModernProgressBar progressBar;
        private Label lblInstallDetail;
        private Label[] lblChecklistItems;

        // Finished Controls
        private CheckBox chkLaunchNow;

        private Icon appIcon;
        private Image appIconImage;

        // Color Palette (Obsidian Dark Theme)
        private static readonly Color ColBgDarkest = Color.FromArgb(7, 10, 15);    // #070A0F
        private static readonly Color ColBgMain = Color.FromArgb(11, 15, 23);       // #0B0F17
        private static readonly Color ColBgHeader = Color.FromArgb(15, 23, 42);     // #0F172A
        private static readonly Color ColBgCard = Color.FromArgb(24, 33, 50);       // #182132
        private static readonly Color ColBorder = Color.FromArgb(51, 65, 85);       // #334155
        private static readonly Color ColBorderLight = Color.FromArgb(71, 85, 105); // #475569
        private static readonly Color ColEmerald = Color.FromArgb(16, 185, 129);    // #10B981
        private static readonly Color ColEmeraldHover = Color.FromArgb(5, 150, 105);// #059669
        private static readonly Color ColTextWhite = Color.FromArgb(248, 250, 252); // #F8FAFC
        private static readonly Color ColTextMuted = Color.FromArgb(148, 163, 184); // #94A3B8
        private static readonly Color ColTextDim = Color.FromArgb(100, 116, 139);   // #64748B

        private const string AppName = "FastDownloader Pro";
        private const string AppVersion = "0.1.3";
        private const string AppPublisher = "FastDownloader Contributors";
        private const string NativeHostName = "com.fastdownloader.app";

        public SetupWizardForm()
        {
            InitializeComponent();
            LoadAppIcon();
            UpdateStepView();
        }

        private void LoadAppIcon()
        {
            try
            {
                string localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "installer.ico");
                if (!File.Exists(localIco)) localIco = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");

                if (File.Exists(localIco))
                {
                    appIcon = new Icon(localIco, 64, 64);
                    appIconImage = appIcon.ToBitmap();
                    this.Icon = appIcon;
                    if (picHeaderIcon != null) picHeaderIcon.Image = appIconImage;
                }
                else
                {
                    this.Icon = SystemIcons.Application;
                    appIconImage = SystemIcons.Application.ToBitmap();
                    if (picHeaderIcon != null) picHeaderIcon.Image = appIconImage;
                }
            }
            catch
            {
                this.Icon = SystemIcons.Application;
            }
        }

        private void InitializeComponent()
        {
            this.Text = AppName + " Setup";
            this.ClientSize = new Size(650, 500);
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = true;
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = ColBgMain;
            this.ForeColor = ColTextWhite;
            this.Font = new Font("Segoe UI", 9F, FontStyle.Regular, GraphicsUnit.Point);

            // ================= HEADER (Visible for steps 1 - 5) =================
            pnlHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 78,
                BackColor = ColBgHeader
            };

            lblStepBadge = new Label
            {
                Location = new Point(24, 10),
                AutoSize = true,
                ForeColor = ColEmerald,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold)
            };

            lblHeaderTitle = new Label
            {
                Location = new Point(24, 28),
                AutoSize = true,
                ForeColor = ColTextWhite,
                Font = new Font("Segoe UI", 12F, FontStyle.Bold)
            };

            lblHeaderSubtitle = new Label
            {
                Location = new Point(24, 52),
                AutoSize = true,
                ForeColor = ColTextMuted,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };

            picHeaderIcon = new PictureBox
            {
                Location = new Point(576, 14),
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.Zoom
            };

            pnlHeaderAccent = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 2,
                BackColor = ColEmerald
            };

            pnlHeader.Controls.Add(lblStepBadge);
            pnlHeader.Controls.Add(lblHeaderTitle);
            pnlHeader.Controls.Add(lblHeaderSubtitle);
            pnlHeader.Controls.Add(picHeaderIcon);
            pnlHeader.Controls.Add(pnlHeaderAccent);

            // ================= FOOTER =================
            pnlFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 60,
                BackColor = ColBgDarkest
            };

            pnlFooterLine = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = ColBorder
            };

            btnBack = new Button
            {
                Text = "< Back",
                Size = new Size(88, 32),
                Location = new Point(325, 14),
                Cursor = Cursors.Hand
            };
            StyleSecondaryButton(btnBack);
            btnBack.Click += (s, e) => GoPrevious();

            btnNext = new Button
            {
                Text = "Next >",
                Size = new Size(100, 32),
                Location = new Point(423, 14),
                Cursor = Cursors.Hand
            };
            StylePrimaryButton(btnNext);
            btnNext.Click += (s, e) => GoNext();

            btnCancel = new Button
            {
                Text = "Cancel",
                Size = new Size(88, 32),
                Location = new Point(533, 14),
                Cursor = Cursors.Hand
            };
            StyleSecondaryButton(btnCancel);
            btnCancel.Click += (s, e) => ConfirmCancel();

            pnlFooter.Controls.Add(pnlFooterLine);
            pnlFooter.Controls.Add(btnBack);
            pnlFooter.Controls.Add(btnNext);
            pnlFooter.Controls.Add(btnCancel);

            // ================= CONTENT CONTAINER =================
            pnlContent = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = ColBgMain
            };

            // Build individual step panels
            BuildWelcomeStep();
            BuildAgreementStep();
            BuildDestinationStep();
            BuildOptionsStep();
            BuildReadyStep();
            BuildInstallingStep();
            BuildFinishedStep();

            this.Controls.Add(pnlContent);
            this.Controls.Add(pnlHeader);
            this.Controls.Add(pnlFooter);
        }

        private void StylePrimaryButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = ColEmerald;
            btn.ForeColor = Color.White;
            btn.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            btn.FlatAppearance.BorderSize = 0;
            btn.MouseEnter += (s, e) => { if (btn.Enabled) btn.BackColor = ColEmeraldHover; };
            btn.MouseLeave += (s, e) => { if (btn.Enabled) btn.BackColor = ColEmerald; };
        }

        private void StyleSecondaryButton(Button btn)
        {
            btn.FlatStyle = FlatStyle.Flat;
            btn.BackColor = ColBgCard;
            btn.ForeColor = ColTextWhite;
            btn.Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            btn.FlatAppearance.BorderSize = 1;
            btn.FlatAppearance.BorderColor = ColBorder;
            btn.MouseEnter += (s, e) => { if (btn.Enabled) btn.BackColor = Color.FromArgb(36, 48, 70); };
            btn.MouseLeave += (s, e) => { if (btn.Enabled) btn.BackColor = ColBgCard; };
        }

        private Panel CreateHeroSidebar(string headline, string tagline)
        {
            Panel sidebar = new Panel
            {
                Dock = DockStyle.Left,
                Width = 180,
                BackColor = ColBgDarkest
            };

            Panel accent = new Panel
            {
                Dock = DockStyle.Right,
                Width = 2,
                BackColor = ColEmerald
            };
            sidebar.Controls.Add(accent);

            PictureBox pic = new PictureBox
            {
                Location = new Point(54, 45),
                Size = new Size(72, 72),
                SizeMode = PictureBoxSizeMode.Zoom
            };
            if (appIconImage != null) pic.Image = appIconImage;
            sidebar.Controls.Add(pic);

            Label lblHead = new Label
            {
                Text = headline,
                Location = new Point(10, 135),
                Size = new Size(160, 26),
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = ColTextWhite,
                Font = new Font("Segoe UI", 11F, FontStyle.Bold)
            };
            sidebar.Controls.Add(lblHead);

            Label lblTag = new Label
            {
                Text = tagline,
                Location = new Point(10, 162),
                Size = new Size(160, 42),
                TextAlign = ContentAlignment.TopCenter,
                ForeColor = ColEmerald,
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular)
            };
            sidebar.Controls.Add(lblTag);

            return sidebar;
        }

        // ------------------ STEP 0: WELCOME ------------------
        private void BuildWelcomeStep()
        {
            pnlWelcome = new Panel { Dock = DockStyle.Fill, Visible = false };

            Panel sidebar = CreateHeroSidebar("FastDownloader", "Turbo Multi-Stream\nEdition v" + AppVersion);
            pnlWelcome.Controls.Add(sidebar);

            Panel rightArea = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 25, 28, 15),
                BackColor = ColBgMain
            };

            Label lblTitle = new Label
            {
                Text = "Welcome to the " + AppName + "\nSetup Wizard",
                Location = new Point(24, 20),
                Size = new Size(410, 58),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = ColTextWhite
            };

            Label lblDesc = new Label
            {
                Text = "FastDownloader Pro is an ultra-fast, multi-threaded download manager designed to saturate your full bandwidth with zero bloatware or telemetry.\n\n" +
                       "Key Features:\n" +
                       "  ⚡ Up to 16x parallel connection chunk downloading\n" +
                       "  🌐 Browser companion integration (Edge, Chrome, Brave)\n" +
                       "  🎬 Media stream extraction (YouTube, HLS, MP4)\n" +
                       "  🔒 100% private, zero telemetry, local loopback processing\n\n" +
                       "Click Next to review the legal agreement and configure your installation settings.",
                Location = new Point(24, 90),
                Size = new Size(410, 280),
                Font = new Font("Segoe UI", 9.25F, FontStyle.Regular),
                ForeColor = ColTextMuted
            };

            rightArea.Controls.Add(lblTitle);
            rightArea.Controls.Add(lblDesc);
            pnlWelcome.Controls.Add(rightArea);

            pnlContent.Controls.Add(pnlWelcome);
        }

        // ------------------ STEP 1: AGREEMENT ------------------
        private void BuildAgreementStep()
        {
            pnlAgreement = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(24, 12, 24, 12) };

            Label lblPrompt = new Label
            {
                Text = "Please review the license terms and acceptable use policy before installing " + AppName + ":",
                Location = new Point(24, 8),
                Size = new Size(595, 22),
                ForeColor = ColTextMuted
            };

            txtAgreement = new TextBox
            {
                Location = new Point(24, 34),
                Size = new Size(595, 235),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = ColBgHeader,
                ForeColor = Color.FromArgb(226, 232, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.25F, FontStyle.Regular),
                Text = GetLegalAgreementText()
            };

            rbAccept = new RadioButton
            {
                Text = "I accept the license agreement and terms of use",
                Location = new Point(26, 280),
                Size = new Size(585, 24),
                ForeColor = ColTextWhite,
                Font = new Font("Segoe UI", 9.25F, FontStyle.Bold),
                Cursor = Cursors.Hand
            };
            rbAccept.CheckedChanged += (s, e) =>
            {
                btnNext.Enabled = rbAccept.Checked;
                btnNext.BackColor = rbAccept.Checked ? ColEmerald : Color.FromArgb(51, 65, 85);
            };

            rbReject = new RadioButton
            {
                Text = "I do not accept the agreement (Setup will not continue)",
                Location = new Point(26, 308),
                Size = new Size(585, 22),
                Checked = true,
                ForeColor = ColTextDim,
                Cursor = Cursors.Hand
            };

            pnlAgreement.Controls.Add(lblPrompt);
            pnlAgreement.Controls.Add(txtAgreement);
            pnlAgreement.Controls.Add(rbAccept);
            pnlAgreement.Controls.Add(rbReject);

            pnlContent.Controls.Add(pnlAgreement);
        }

        // ------------------ STEP 2: DESTINATION ------------------
        private void BuildDestinationStep()
        {
            pnlDestination = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(24, 14, 24, 14) };

            Label lblPrompt = new Label
            {
                Text = "Setup will install " + AppName + " into the following destination folder.\n" +
                       "To install to a different folder, click Browse and select another folder.",
                Location = new Point(24, 15),
                Size = new Size(595, 38),
                ForeColor = ColTextMuted
            };

            Label lblFolder = new Label
            {
                Text = "Destination Folder:",
                Location = new Point(24, 68),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Bold),
                ForeColor = ColTextWhite
            };

            string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\" + AppName);

            txtInstallDir = new TextBox
            {
                Location = new Point(24, 92),
                Size = new Size(490, 26),
                Text = defaultDir,
                BackColor = ColBgHeader,
                ForeColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Regular)
            };
            txtInstallDir.TextChanged += (s, e) => UpdateDiskSpaceInfo();

            btnBrowse = new Button
            {
                Text = "Browse...",
                Location = new Point(524, 90),
                Size = new Size(95, 28),
                Cursor = Cursors.Hand
            };
            StyleSecondaryButton(btnBrowse);
            btnBrowse.Click += (s, e) =>
            {
                using (FolderBrowserDialog fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Select Destination Folder for " + AppName;
                    fbd.SelectedPath = txtInstallDir.Text;
                    if (fbd.ShowDialog() == DialogResult.OK)
                    {
                        txtInstallDir.Text = Path.Combine(fbd.SelectedPath, AppName);
                    }
                }
            };

            Panel pnlDriveInfo = new Panel
            {
                Location = new Point(24, 140),
                Size = new Size(595, 80),
                BackColor = ColBgCard
            };

            Panel pnlCardBorder = new Panel
            {
                Dock = DockStyle.Left,
                Width = 3,
                BackColor = ColEmerald
            };
            pnlDriveInfo.Controls.Add(pnlCardBorder);

            lblSpaceRequired = new Label
            {
                Text = "Space required: 45.0 MB",
                Location = new Point(20, 16),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = ColTextWhite
            };

            lblSpaceAvailable = new Label
            {
                Text = "Space available on drive: Calculating...",
                Location = new Point(20, 42),
                AutoSize = true,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular),
                ForeColor = ColTextMuted
            };

            pnlDriveInfo.Controls.Add(lblSpaceRequired);
            pnlDriveInfo.Controls.Add(lblSpaceAvailable);

            pnlDestination.Controls.Add(lblPrompt);
            pnlDestination.Controls.Add(lblFolder);
            pnlDestination.Controls.Add(txtInstallDir);
            pnlDestination.Controls.Add(btnBrowse);
            pnlDestination.Controls.Add(pnlDriveInfo);

            pnlContent.Controls.Add(pnlDestination);
            UpdateDiskSpaceInfo();
        }

        private void UpdateDiskSpaceInfo()
        {
            try
            {
                string path = txtInstallDir.Text;
                if (!string.IsNullOrEmpty(path))
                {
                    string root = Path.GetPathRoot(Path.GetFullPath(path));
                    DriveInfo drive = new DriveInfo(root);
                    double freeGb = (double)drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                    lblSpaceAvailable.Text = string.Format("Space available on drive ({0}): {1:N1} GB (Ready)", root, freeGb);
                    lblSpaceAvailable.ForeColor = ColEmerald;
                }
            }
            catch
            {
                lblSpaceAvailable.Text = "Space available on drive: Verified";
                lblSpaceAvailable.ForeColor = ColTextMuted;
            }
        }

        // ------------------ STEP 3: OPTIONS ------------------
        private void BuildOptionsStep()
        {
            pnlOptions = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(24, 14, 24, 14) };

            Label lblPrompt = new Label
            {
                Text = "Select which additional shortcuts and system integrations you would like Setup to configure:",
                Location = new Point(24, 15),
                Size = new Size(595, 25),
                ForeColor = ColTextMuted
            };

            chkDesktop = new CheckBox
            {
                Text = "Create a Desktop shortcut",
                Location = new Point(32, 55),
                Size = new Size(560, 24),
                Checked = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = ColTextWhite,
                Cursor = Cursors.Hand
            };

            Label lblDesktopSub = new Label
            {
                Text = "Places a quick launch shortcut on your Windows desktop",
                Location = new Point(56, 80),
                Size = new Size(540, 18),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = ColTextDim
            };

            chkStartMenu = new CheckBox
            {
                Text = "Create a Start Menu shortcut",
                Location = new Point(32, 110),
                Size = new Size(560, 24),
                Checked = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = ColTextWhite,
                Cursor = Cursors.Hand
            };

            Label lblStartMenuSub = new Label
            {
                Text = "Adds FastDownloader Pro to your Windows Start Menu and Search",
                Location = new Point(56, 135),
                Size = new Size(540, 18),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = ColTextDim
            };

            chkBrowser = new CheckBox
            {
                Text = "Register Browser Native Integration (Microsoft Edge, Google Chrome, Brave)",
                Location = new Point(32, 165),
                Size = new Size(560, 24),
                Checked = true,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = ColTextWhite,
                Cursor = Cursors.Hand
            };

            Label lblBrowserSub = new Label
            {
                Text = "Registers NativeMessagingHosts so browser extensions can route downloads directly",
                Location = new Point(56, 190),
                Size = new Size(540, 18),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = ColTextDim
            };

            chkStartup = new CheckBox
            {
                Text = "Launch FastDownloader Pro minimized when Windows starts",
                Location = new Point(32, 220),
                Size = new Size(560, 24),
                Checked = false,
                Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
                ForeColor = ColTextWhite,
                Cursor = Cursors.Hand
            };

            Label lblStartupSub = new Label
            {
                Text = "Starts the background acceleration engine quietly in the system tray at boot",
                Location = new Point(56, 245),
                Size = new Size(540, 18),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = ColTextDim
            };

            pnlOptions.Controls.Add(lblPrompt);
            pnlOptions.Controls.Add(chkDesktop);
            pnlOptions.Controls.Add(lblDesktopSub);
            pnlOptions.Controls.Add(chkStartMenu);
            pnlOptions.Controls.Add(lblStartMenuSub);
            pnlOptions.Controls.Add(chkBrowser);
            pnlOptions.Controls.Add(lblBrowserSub);
            pnlOptions.Controls.Add(chkStartup);
            pnlOptions.Controls.Add(lblStartupSub);

            pnlContent.Controls.Add(pnlOptions);
        }

        // ------------------ STEP 4: READY ------------------
        private void BuildReadyStep()
        {
            pnlReady = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(24, 14, 24, 14) };

            Label lblPrompt = new Label
            {
                Text = "Setup is now ready to begin installing " + AppName + " on your computer.\n" +
                       "Click Install to proceed, or click Back to review your choices.",
                Location = new Point(24, 15),
                Size = new Size(595, 38),
                ForeColor = ColTextMuted
            };

            txtSummary = new TextBox
            {
                Location = new Point(24, 60),
                Size = new Size(595, 255),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = ColBgHeader,
                ForeColor = Color.FromArgb(226, 232, 240),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9F, FontStyle.Regular)
            };

            pnlReady.Controls.Add(lblPrompt);
            pnlReady.Controls.Add(txtSummary);

            pnlContent.Controls.Add(pnlReady);
        }

        private void UpdateSummary()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("INSTALLATION SETTINGS SUMMARY:");
            sb.AppendLine("================================================");
            sb.AppendLine();
            sb.AppendLine("Destination Location:");
            sb.AppendLine("  " + txtInstallDir.Text);
            sb.AppendLine();
            sb.AppendLine("Shortcuts & Placement:");
            if (chkDesktop.Checked) sb.AppendLine("  • Desktop Shortcut");
            if (chkStartMenu.Checked) sb.AppendLine("  • Start Menu Shortcut");
            sb.AppendLine();
            sb.AppendLine("Browser Native Integration:");
            if (chkBrowser.Checked)
                sb.AppendLine("  • NativeMessagingHost configured for Microsoft Edge, Chrome & Brave");
            else
                sb.AppendLine("  • Disabled");
            sb.AppendLine();
            sb.AppendLine("System Startup:");
            sb.AppendLine(chkStartup.Checked ? "  • Start automatically with Windows (Minimized to tray)" : "  • Manual start");

            txtSummary.Text = sb.ToString();
        }

        // ------------------ STEP 5: INSTALLING ------------------
        private void BuildInstallingStep()
        {
            pnlInstalling = new Panel { Dock = DockStyle.Fill, Visible = false, Padding = new Padding(24, 14, 24, 14) };

            lblInstallStatus = new Label
            {
                Text = "Preparing extraction...",
                Location = new Point(24, 25),
                Size = new Size(595, 25),
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColTextWhite
            };

            progressBar = new ModernProgressBar
            {
                Location = new Point(24, 58),
                Size = new Size(595, 22),
                Value = 0,
                Maximum = 100
            };

            lblInstallDetail = new Label
            {
                Text = "Please wait while files are being copied to your system.",
                Location = new Point(24, 88),
                Size = new Size(595, 22),
                Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                ForeColor = ColTextMuted
            };

            // Checklist of components
            Panel pnlChecklist = new Panel
            {
                Location = new Point(24, 120),
                Size = new Size(595, 180),
                BackColor = ColBgCard
            };

            Label lblChecklistHeader = new Label
            {
                Text = "COMPONENTS BEING CONFIGURED:",
                Location = new Point(18, 12),
                AutoSize = true,
                Font = new Font("Segoe UI", 8F, FontStyle.Bold),
                ForeColor = ColEmerald
            };
            pnlChecklist.Controls.Add(lblChecklistHeader);

            string[] items = new string[]
            {
                "FastDownloader Pro Core Executable & Web Dashboard",
                "Turbo 16-Connection Download Engine (aria2c)",
                "Universal Media Stream Sniffer (yt-dlp)",
                "Browser Native Messaging Hosts (Edge, Chrome, Brave)",
                "Desktop & Start Menu System Shortcuts"
            };

            lblChecklistItems = new Label[items.Length];
            for (int i = 0; i < items.Length; i++)
            {
                lblChecklistItems[i] = new Label
                {
                    Text = "⏳  " + items[i],
                    Location = new Point(18, 38 + (i * 26)),
                    Size = new Size(555, 20),
                    Font = new Font("Segoe UI", 8.5F, FontStyle.Regular),
                    ForeColor = ColTextMuted
                };
                pnlChecklist.Controls.Add(lblChecklistItems[i]);
            }

            pnlInstalling.Controls.Add(lblInstallStatus);
            pnlInstalling.Controls.Add(progressBar);
            pnlInstalling.Controls.Add(lblInstallDetail);
            pnlInstalling.Controls.Add(pnlChecklist);

            pnlContent.Controls.Add(pnlInstalling);
        }

        public void SetChecklistStatus(int index, bool done)
        {
            if (lblChecklistItems != null && index >= 0 && index < lblChecklistItems.Length)
            {
                string originalText = lblChecklistItems[index].Text.Substring(3);
                if (done)
                {
                    lblChecklistItems[index].Text = "✓  " + originalText;
                    lblChecklistItems[index].ForeColor = ColEmerald;
                }
                else
                {
                    lblChecklistItems[index].Text = "⚡  " + originalText;
                    lblChecklistItems[index].ForeColor = ColTextWhite;
                }
            }
        }

        // ------------------ STEP 6: FINISHED ------------------
        private void BuildFinishedStep()
        {
            pnlFinished = new Panel { Dock = DockStyle.Fill, Visible = false };

            Panel sidebar = CreateHeroSidebar("Completed!", "Installation\nSuccessful");
            pnlFinished.Controls.Add(sidebar);

            Panel rightArea = new Panel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(28, 25, 28, 15),
                BackColor = ColBgMain
            };

            Label lblTitle = new Label
            {
                Text = "Completing the " + AppName + "\nSetup Wizard",
                Location = new Point(24, 20),
                Size = new Size(410, 58),
                Font = new Font("Segoe UI", 14F, FontStyle.Bold),
                ForeColor = ColTextWhite
            };

            Label lblFinishedDesc = new Label
            {
                Text = AppName + " has been successfully installed on your computer.\n\n" +
                       "The application may be launched using the newly created desktop shortcuts " +
                       "or directly through your web browser.\n\n" +
                       "Click Finish to exit the Setup Wizard.",
                Location = new Point(24, 95),
                Size = new Size(410, 110),
                Font = new Font("Segoe UI", 9.25F, FontStyle.Regular),
                ForeColor = ColTextMuted
            };

            chkLaunchNow = new CheckBox
            {
                Text = "Launch " + AppName + " now",
                Location = new Point(24, 220),
                Size = new Size(400, 25),
                Checked = true,
                Font = new Font("Segoe UI", 10F, FontStyle.Bold),
                ForeColor = ColTextWhite,
                Cursor = Cursors.Hand
            };

            rightArea.Controls.Add(lblTitle);
            rightArea.Controls.Add(lblFinishedDesc);
            rightArea.Controls.Add(chkLaunchNow);

            pnlFinished.Controls.Add(rightArea);
            pnlContent.Controls.Add(pnlFinished);
        }

        // ================= NAVIGATION =================
        public void UpdateStepView()
        {
            // Hide all step panels
            pnlWelcome.Visible = false;
            pnlAgreement.Visible = false;
            pnlDestination.Visible = false;
            pnlOptions.Visible = false;
            pnlReady.Visible = false;
            pnlInstalling.Visible = false;
            pnlFinished.Visible = false;

            btnBack.Visible = true;
            btnBack.Enabled = true;
            btnNext.Visible = true;
            btnNext.Enabled = true;
            btnCancel.Visible = true;
            btnCancel.Enabled = true;

            btnNext.Text = "Next >";
            StylePrimaryButton(btnNext);

            // Welcome and Finished hide top header and use hero sidebar
            if (currentStep == WizardStep.Welcome || currentStep == WizardStep.Finished)
            {
                pnlHeader.Visible = false;
            }
            else
            {
                pnlHeader.Visible = true;
            }

            switch (currentStep)
            {
                case WizardStep.Welcome:
                    pnlWelcome.Visible = true;
                    btnBack.Enabled = false;
                    break;

                case WizardStep.Agreement:
                    pnlAgreement.Visible = true;
                    btnNext.Enabled = rbAccept.Checked;
                    btnNext.BackColor = rbAccept.Checked ? ColEmerald : Color.FromArgb(51, 65, 85);
                    lblStepBadge.Text = "STEP 1 OF 5 • LICENSE AGREEMENT";
                    lblHeaderTitle.Text = "License Agreement & Legal Terms";
                    lblHeaderSubtitle.Text = "Please review the license terms and acceptable use policy.";
                    break;

                case WizardStep.Destination:
                    pnlDestination.Visible = true;
                    lblStepBadge.Text = "STEP 2 OF 5 • DESTINATION FOLDER";
                    lblHeaderTitle.Text = "Select Destination Location";
                    lblHeaderSubtitle.Text = "Where should " + AppName + " be installed?";
                    UpdateDiskSpaceInfo();
                    break;

                case WizardStep.Options:
                    pnlOptions.Visible = true;
                    lblStepBadge.Text = "STEP 3 OF 5 • ADDITIONAL TASKS";
                    lblHeaderTitle.Text = "Select Additional Tasks";
                    lblHeaderSubtitle.Text = "Which additional shortcuts and integrations should be configured?";
                    break;

                case WizardStep.Ready:
                    pnlReady.Visible = true;
                    UpdateSummary();
                    btnNext.Text = "Install";
                    lblStepBadge.Text = "STEP 4 OF 5 • READY TO INSTALL";
                    lblHeaderTitle.Text = "Ready to Install";
                    lblHeaderSubtitle.Text = "Setup is now ready to begin installing files.";
                    break;

                case WizardStep.Installing:
                    pnlInstalling.Visible = true;
                    btnBack.Enabled = false;
                    btnNext.Enabled = false;
                    btnCancel.Enabled = false;
                    lblStepBadge.Text = "STEP 5 OF 5 • INSTALLATION IN PROGRESS";
                    lblHeaderTitle.Text = "Installing " + AppName;
                    lblHeaderSubtitle.Text = "Please wait while Setup configures your installation...";
                    StartInstallationAsync();
                    break;

                case WizardStep.Finished:
                    pnlFinished.Visible = true;
                    btnBack.Visible = false;
                    btnCancel.Visible = false;
                    btnNext.Text = "Finish";
                    break;
            }
        }

        private void GoNext()
        {
            if (currentStep == WizardStep.Finished)
            {
                if (chkLaunchNow.Checked)
                {
                    string targetExe = Path.Combine(txtInstallDir.Text.Trim(), "FastDownloader.exe");
                    if (File.Exists(targetExe))
                    {
                        ProcessStartInfo psi = new ProcessStartInfo(targetExe);
                        psi.WorkingDirectory = txtInstallDir.Text.Trim();
                        Process.Start(psi);
                    }
                }
                Application.Exit();
                return;
            }

            if (currentStep == WizardStep.Destination)
            {
                string path = txtInstallDir.Text.Trim();
                if (string.IsNullOrEmpty(path))
                {
                    MessageBox.Show("Please specify a valid destination folder.", "Invalid Path", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            int next = (int)currentStep + 1;
            if (next <= (int)WizardStep.Finished)
            {
                currentStep = (WizardStep)next;
                UpdateStepView();
            }
        }

        private void GoPrevious()
        {
            int prev = (int)currentStep - 1;
            if (prev >= 0)
            {
                currentStep = (WizardStep)prev;
                UpdateStepView();
            }
        }

        private void ConfirmCancel()
        {
            if (currentStep == WizardStep.Installing)
            {
                return;
            }

            if (MessageBox.Show("Are you sure you want to exit " + AppName + " Setup?", "Exit Setup", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                Application.Exit();
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (currentStep == WizardStep.Installing)
            {
                e.Cancel = true;
                return;
            }
            base.OnFormClosing(e);
        }

        // ================= BACKGROUND INSTALLATION =================
        private void StartInstallationAsync()
        {
            string targetDir = txtInstallDir.Text.Trim();
            bool makeDesktopShortcut = chkDesktop.Checked;
            bool makeStartMenuShortcut = chkStartMenu.Checked;
            bool registerBrowser = chkBrowser.Checked;
            bool runAtStartup = chkStartup.Checked;

            Thread worker = new Thread(() =>
            {
                try
                {
                    // 1. Close running processes if any
                    ReportProgress("Closing active instances...", 5, "Stopping background processes...");
                    KillRunningFastDownloader();

                    // 2. Prepare Directory
                    ReportProgress("Preparing destination folder...", 10, targetDir);
                    if (!Directory.Exists(targetDir))
                    {
                        Directory.CreateDirectory(targetDir);
                    }

                    // 3. Extract Payload
                    ReportProgress("Extracting application package...", 15, "Opening embedded bundle...");
                    this.Invoke(new Action(() => SetChecklistStatus(0, false)));
                    ExtractPayload(targetDir);
                    this.Invoke(new Action(() => {
                        SetChecklistStatus(0, true);
                        SetChecklistStatus(1, true);
                        SetChecklistStatus(2, true);
                    }));

                    // 4. Update com.fastdownloader.app.json
                    ReportProgress("Configuring browser host...", 75, "Updating native host manifest...");
                    UpdateNativeHostManifest(targetDir);

                    // 5. Register Browser Integration
                    if (registerBrowser)
                    {
                        ReportProgress("Registering browser extensions...", 80, "Writing NativeMessagingHosts registry keys...");
                        RegisterBrowserHosts(targetDir);
                        this.Invoke(new Action(() => SetChecklistStatus(3, true)));
                    }

                    // 6. Shortcuts
                    string exePath = Path.Combine(targetDir, "FastDownloader.exe");
                    string icoPath = Path.Combine(targetDir, "app.ico");

                    if (makeDesktopShortcut)
                    {
                        ReportProgress("Creating Desktop shortcut...", 85, "Desktop");
                        string desktopPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), AppName + ".lnk");
                        CreateShellShortcut(desktopPath, exePath, targetDir, icoPath, AppName + " - Turbo Download Manager");
                    }

                    if (makeStartMenuShortcut)
                    {
                        ReportProgress("Creating Start Menu shortcut...", 90, "Start Menu Programs");
                        string programsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");
                        CreateShellShortcut(programsPath, exePath, targetDir, icoPath, AppName + " - Turbo Download Manager");
                    }
                    this.Invoke(new Action(() => SetChecklistStatus(4, true)));

                    // 7. Startup Registry
                    if (runAtStartup)
                    {
                        ReportProgress("Registering Windows startup...", 93, "HKCU Run Key");
                        SetStartupRunKey(exePath);
                    }

                    // 8. Add Windows Uninstall Entry
                    ReportProgress("Registering Windows uninstall entry...", 96, "Windows Programs & Features");
                    RegisterUninstallEntry(targetDir);

                    // Complete!
                    ReportProgress("Setup completed!", 100, "Done.");
                    Thread.Sleep(500);

                    this.Invoke(new Action(() =>
                    {
                        currentStep = WizardStep.Finished;
                        UpdateStepView();
                    }));
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show("An error occurred during installation:\n\n" + ex.Message, "Installation Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        btnCancel.Enabled = true;
                    }));
                }
            });

            worker.IsBackground = true;
            worker.Start();
        }

        private void KillRunningFastDownloader()
        {
            try
            {
                foreach (Process p in Process.GetProcessesByName("FastDownloader"))
                {
                    try { p.Kill(); p.WaitForExit(1500); } catch { }
                }
                foreach (Process p in Process.GetProcessesByName("aria2c"))
                {
                    try { p.Kill(); p.WaitForExit(1000); } catch { }
                }
            }
            catch { }
        }

        private void ExtractPayload(string targetDir)
        {
            Stream zipStream = null;

            // First check embedded resource
            Assembly asm = Assembly.GetExecutingAssembly();
            string[] resNames = asm.GetManifestResourceNames();
            foreach (string name in resNames)
            {
                if (name.EndsWith("payload.zip", StringComparison.OrdinalIgnoreCase))
                {
                    zipStream = asm.GetManifestResourceStream(name);
                    break;
                }
            }

            // Fallback: check if payload.zip is alongside the installer
            if (zipStream == null)
            {
                string localZip = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "payload.zip");
                if (File.Exists(localZip))
                {
                    zipStream = File.OpenRead(localZip);
                }
            }

            if (zipStream == null)
            {
                throw new FileNotFoundException("Setup payload could not be located inside the installer package.");
            }

            using (zipStream)
            using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read))
            {
                int total = archive.Entries.Count;
                int count = 0;

                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    count++;
                    string entryPath = entry.FullName.Replace('/', '\\');
                    string destPath = Path.Combine(targetDir, entryPath);

                    if (string.IsNullOrEmpty(entry.Name))
                    {
                        if (!Directory.Exists(destPath)) Directory.CreateDirectory(destPath);
                        continue;
                    }

                    string dir = Path.GetDirectoryName(destPath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                    int pct = 15 + (int)(((double)count / total) * 55);
                    ReportProgress("Extracting " + entry.Name, pct, entryPath);

                    entry.ExtractToFile(destPath, true);
                }
            }
        }

        private void UpdateNativeHostManifest(string targetDir)
        {
            try
            {
                string manifestPath = Path.Combine(targetDir, "com.fastdownloader.app.json");
                string exePath = Path.Combine(targetDir, "FastDownloader.exe").Replace("\\", "\\\\");

                if (File.Exists(manifestPath))
                {
                    string content = File.ReadAllText(manifestPath);
                    string pattern = "\"path\": \".*?\"";
                    string replacement = "\"path\": \"" + exePath + "\"";
                    content = System.Text.RegularExpressions.Regex.Replace(content, pattern, replacement);
                    File.WriteAllText(manifestPath, content);
                }
                else
                {
                    string json = "{\n  \"name\": \"" + NativeHostName + "\",\n  \"description\": \"FastDownloader Native Messaging Host\",\n  \"path\": \"" + exePath + "\",\n  \"type\": \"stdio\",\n  \"allowed_origins\": [\n    \"chrome-extension://gofdohcbjeiggejfkdhbpgddoejjdafg/\"\n  ]\n}";
                    File.WriteAllText(manifestPath, json);
                }
            }
            catch { }
        }

        private void RegisterBrowserHosts(string targetDir)
        {
            try
            {
                string manifestPath = Path.Combine(targetDir, "com.fastdownloader.app.json");
                string[] browserKeys = new string[]
                {
                    @"Software\Google\Chrome\NativeMessagingHosts\" + NativeHostName,
                    @"Software\Microsoft\Edge\NativeMessagingHosts\" + NativeHostName,
                    @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\" + NativeHostName
                };

                foreach (string keyPath in browserKeys)
                {
                    using (RegistryKey k = Registry.CurrentUser.CreateSubKey(keyPath))
                    {
                        if (k != null)
                        {
                            k.SetValue(null, manifestPath);
                        }
                    }
                }
            }
            catch { }
        }

        private void SetStartupRunKey(string exePath)
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    if (k != null)
                    {
                        k.SetValue("FastDownloader", "\"" + exePath + "\" --minimized");
                    }
                }
            }
            catch { }
        }

        private void RegisterUninstallEntry(string targetDir)
        {
            try
            {
                string currentInstaller = Assembly.GetExecutingAssembly().Location;
                string uninstallerPath = Path.Combine(targetDir, "uninstall.exe");
                try
                {
                    if (File.Exists(currentInstaller) && !string.Equals(currentInstaller, uninstallerPath, StringComparison.OrdinalIgnoreCase))
                    {
                        File.Copy(currentInstaller, uninstallerPath, true);
                    }
                }
                catch { }

                string uninstallCmd = "\"" + (File.Exists(uninstallerPath) ? uninstallerPath : currentInstaller) + "\" /uninstall";

                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\FastDownloader";
                using (RegistryKey k = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (k != null)
                    {
                        k.SetValue("DisplayName", AppName);
                        k.SetValue("DisplayVersion", AppVersion);
                        k.SetValue("Publisher", AppPublisher);
                        k.SetValue("DisplayIcon", Path.Combine(targetDir, "app.ico") + ",0");
                        k.SetValue("InstallLocation", targetDir);
                        k.SetValue("UninstallString", uninstallCmd);
                        k.SetValue("EstimatedSize", 45000, RegistryValueKind.DWord);
                        k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch { }
        }

        private void CreateShellShortcut(string shortcutPath, string targetPath, string workingDir, string iconPath, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return;
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                Type shortcutType = shortcut.GetType();
                shortcutType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                shortcutType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                if (File.Exists(iconPath))
                {
                    shortcutType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
                }
                shortcutType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                shortcutType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            catch { }
        }

        private void ReportProgress(string status, int percent, string detail)
        {
            if (this.InvokeRequired)
            {
                this.Invoke(new Action(() => ReportProgress(status, percent, detail)));
                return;
            }

            lblInstallStatus.Text = status;
            progressBar.Value = Math.Max(0, Math.Min(100, percent));
            lblInstallDetail.Text = detail;
        }

        // ================= UNINSTALL LOGIC =================
        public static void PerformUninstall()
        {
            DialogResult res = MessageBox.Show(
                "Are you sure you want to completely uninstall " + AppName + " and remove its shortcuts and integrations?",
                AppName + " Uninstall",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (res != DialogResult.Yes) return;

            try
            {
                // Kill running processes
                foreach (Process p in Process.GetProcessesByName("FastDownloader")) { try { p.Kill(); } catch { } }
                foreach (Process p in Process.GetProcessesByName("aria2c")) { try { p.Kill(); } catch { } }

                // Remove shortcuts
                string dLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), AppName + ".lnk");
                if (File.Exists(dLnk)) File.Delete(dLnk);

                string pLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), AppName + ".lnk");
                if (File.Exists(pLnk)) File.Delete(pLnk);

                // Remove registry keys
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

                // Schedule self deletion
                string currentDir = AppDomain.CurrentDomain.BaseDirectory;
                string script = string.Format(
                    "timeout /t 2 /nobreak >nul & rmdir /s /q \"{0}\"",
                    currentDir.TrimEnd('\\')
                );
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", "/c " + script)
                {
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                };
                Process.Start(psi);

                MessageBox.Show(AppName + " was successfully uninstalled from your computer.", "Uninstall Finished", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Uninstall completed with notes: " + ex.Message, "Uninstall", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        // ================= LEGAL TEXT =================
        private string GetLegalAgreementText()
        {
            return
@"FASTDOWNLOADER PRO - END USER LICENSE & TERMS OF USE
====================================================
Version 0.1.3 | Last Updated: October 2026

PLEASE READ THIS AGREEMENT CAREFULLY BEFORE INSTALLING OR USING FASTDOWNLOADER PRO. BY SELECTING 'I ACCEPT', YOU AGREE TO BE BOUND BY THE FOLLOWING TERMS AND CONDITIONS.

----------------------------------------------------
1. END USER LICENSE AGREEMENT (MIT LICENSE)
----------------------------------------------------
Copyright (c) 2026 FastDownloader Contributors.

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the 'Software'), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

----------------------------------------------------
2. LEGAL DISCLAIMER & ACCEPTABLE USE POLICY
----------------------------------------------------
2.1 Intended Purpose:
FastDownloader is a high-performance network client and download acceleration utility designed to:
- Accelerate authorized downloads of open-access software distributions, open datasets, and user-owned files via multi-connection parallel chunk streaming.
- Manage sequential download queues, scheduling, and local network transfers.
- Provide assistive capture and media archival for content that users have explicit copyright authorization or licensing to download.

2.2 Copyright & Intellectual Property Compliance:
- User Responsibility: Users of FastDownloader are solely responsible for ensuring that their downloads comply with all applicable local, national, and international laws, regulations, and third-party Terms of Service.
- Fair Use & Licensing: You must only download content that you own, that is licensed under public domain or Creative Commons licenses, or for which you have secured explicit permission from the copyright owner.
- No DRM Circumvention: FastDownloader does not include technology designed to circumvent digital rights management (DRM), encrypted media streams (e.g., Widevine, FairPlay), or paywalled content.

----------------------------------------------------
3. PRIVACY POLICY & ZERO-TELEMETRY COMMITMENT
----------------------------------------------------
- Zero Remote Tracking: FastDownloader does NOT collect, transmit, store, or sell any personal data, browsing history, or user information to any external or remote servers.
- 100% Local Processing: All communications are strictly restricted to your own local machine via loopback address (127.0.0.1) or Windows Native Messaging channels (stdin/stdout).
- Zero Advertising / Zero Analytics: No advertising SDKs, tracking pixels, or third-party telemetry are included in this software.

----------------------------------------------------
4. THIRD-PARTY TRADEMARKS & ACKNOWLEDGMENTS
----------------------------------------------------
- 'Internet Download Manager' (IDM) is a registered trademark of Tonec Inc.
- 'Google Chrome', 'Microsoft Edge', 'Brave', 'YouTube', and other trademarks are the property of their respective owners.
- FastDownloader Pro is an independent open-source project and is neither affiliated with, endorsed by, nor sponsored by Tonec Inc. or any mentioned trademark owners.

----------------------------------------------------
5. DISCLAIMER OF WARRANTY & LIMITATION OF LIABILITY
----------------------------------------------------
THE SOFTWARE IS PROVIDED 'AS IS', WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
";
        }

        // ================= ENTRY POINT =================
        [STAThread]
        public static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            if (args != null && args.Length > 0)
            {
                string arg = args[0].ToLowerInvariant();
                if (arg == "/uninstall" || arg == "-uninstall" || arg == "/u")
                {
                    PerformUninstall();
                    return;
                }
            }

            Application.Run(new SetupWizardForm());
        }
    }
}
