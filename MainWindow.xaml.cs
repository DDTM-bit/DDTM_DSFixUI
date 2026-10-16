using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Globalization;

namespace DDTM_DSFixUI
{
    public partial class MainWindow : Window
    {
        private string dsfixPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DSfix.ini");
        private string dsfixKeysPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DSfixKeys.ini");
        private string darkSoulsIniPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"NBGI\DarkSouls\DarkSouls.ini");

        private double screenWidth = SystemParameters.PrimaryScreenWidth;
        private double screenHeight = SystemParameters.PrimaryScreenHeight;
        private Dictionary<System.Windows.Controls.Control, string> originalControlValues = new Dictionary<System.Windows.Controls.Control, string>();
        private Dictionary<System.Windows.Controls.Control, Brush> originalControlColors = new Dictionary<System.Windows.Controls.Control, Brush>();

        public MainWindow()
        {
            // Fix for WPF bug where menus open to the left
            var menuDropAlignmentField = typeof(SystemParameters).GetField("_menuDropAlignment", BindingFlags.NonPublic | BindingFlags.Static);
            if (menuDropAlignmentField != null)
            {
                // 1. Force WPF to initialize the property first
                _ = SystemParameters.MenuDropAlignment;

                // 2. Overwrite the cached value to force right-opening menus
                menuDropAlignmentField.SetValue(null, false);

                // 3. Re-apply the fix automatically if Windows triggers a system parameter update
                SystemParameters.StaticPropertyChanged += (sender, e) =>
                {
                    if (e.PropertyName == "MenuDropAlignment")
                    {
                        menuDropAlignmentField.SetValue(null, false);
                    }
                };
            }

            InitializeComponent();
            InitControllerTab();   // DDTM Controller tab (MainWindow.Controller.cs)
            // Restrict input to digits/decimals where appropriate
            txtFpsLimit.PreviewTextInput += NumericOnly_PreviewTextInput;
            txtFpsThreshold.PreviewTextInput += NumericOnly_PreviewTextInput;
            txtBackupInterval.PreviewTextInput += NumericOnly_PreviewTextInput;
            txtMaxBackups.PreviewTextInput += NumericOnly_PreviewTextInput;

            txtHudTopLeftOpacity.PreviewTextInput += NumericAndPointOnly_PreviewTextInput;
            txtFullscreenHz.PreviewTextInput += NumericOnly_PreviewTextInput;
            System.Windows.DataObject.AddPastingHandler(txtFullscreenHz, NumericOnly_Pasting);
            txtHudBottomLeftOpacity.PreviewTextInput += NumericAndPointOnly_PreviewTextInput;
            txtHudBottomRightOpacity.PreviewTextInput += NumericAndPointOnly_PreviewTextInput;
            txtBackupInterval.PreviewTextInput += NumericOnly_PreviewTextInput;
            txtMaxBackups.PreviewTextInput += NumericOnly_PreviewTextInput;

            TxtPcResolution.Text = $"Display Resolution: {screenWidth}x{screenHeight}";

            PopulateResolutions();
            SetupChangeTracking(this);
            CheckDarkSoulsIni();
            LoadDSFixIni();
        }

        // ---- Resolutions, grouped by aspect ratio ----
        private static readonly (int W, int H)[] StandardResolutions =
        {
            (640, 480), (800, 600), (1024, 768), (1280, 1024),
            (1280, 720), (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160),
            (1280, 800), (1440, 900), (1680, 1050), (1920, 1200), (2560, 1600), (3840, 2400),   // 1280x800 is the Steam Deck
            (2560, 1080), (3440, 1440), (3840, 1600),
            (3840, 1080), (5120, 1440)
        };

        // Header text + the exact ratio of each group, in the order they are shown
        private static readonly (string Header, double Ratio)[] AspectGroups =
        {
            ("4:3", 4.0 / 3.0),
            ("5:4", 5.0 / 4.0),
            ("16:9", 16.0 / 9.0),
            ("16:10", 16.0 / 10.0),
            ("21:9 Ultrawide", 21.0 / 9.0),
            ("32:9 Super ultrawide", 32.0 / 9.0)
        };
        private const string OtherAspectGroup = "Other";

        // 1366x768 or 3440x1440 are not exact ratios, so allow a small tolerance
        private static string AspectGroupOf(int w, int h)
        {
            double r = (double)w / h;
            foreach (var g in AspectGroups)
                if (Math.Abs(r - g.Ratio) / g.Ratio < 0.035) return g.Header;
            return OtherAspectGroup;
        }

        private static int AspectGroupOrder(string header)
        {
            int i = Array.FindIndex(AspectGroups, g => g.Header == header);
            return i >= 0 ? i : AspectGroups.Length;   // "Other" goes last
        }

        private void PopulateResolutions()
        {
            // Keep the native resolution selectable even if it isn't in the standard list
            int nativeW = (int)screenWidth, nativeH = (int)screenHeight;
            var list = StandardResolutions.ToList();
            if (!list.Contains((nativeW, nativeH))) list.Add((nativeW, nativeH));

            FillResolutionCombo(cmbRenderResolution, list);
            FillResolutionCombo(cmbPresentResolution, list);

            SetComboBoxByValue(cmbRenderResolution, $"{nativeW}x{nativeH}");   // default = display resolution
            SetComboBoxByValue(cmbPresentResolution, "0x0");                    // default = no downscaling
        }

        // Rebuilds a resolution combo: a non-selectable header per aspect ratio, resolutions sorted small to large
        private void FillResolutionCombo(System.Windows.Controls.ComboBox cmb, IEnumerable<(int W, int H)> resolutions)
        {
            cmb.Items.Clear();

            if (ReferenceEquals(cmb, cmbPresentResolution))
            {
                cmb.Items.Add(MakeResolutionHeader("No downscaling"));
                cmb.Items.Add(MakeResolutionItem("0x0"));
            }

            var groups = resolutions
                .Where(r => r.W > 0 && r.H > 0)
                .Distinct()
                .GroupBy(r => AspectGroupOf(r.W, r.H))
                .OrderBy(g => AspectGroupOrder(g.Key));

            foreach (var group in groups)
            {
                cmb.Items.Add(MakeResolutionHeader(group.Key));
                foreach (var r in group.OrderBy(r => r.W * r.H).ThenBy(r => r.W))
                    cmb.Items.Add(MakeResolutionItem($"{r.W}x{r.H}"));
            }
        }

        private static System.Windows.Controls.ComboBoxItem MakeResolutionHeader(string text)
        {
            return new System.Windows.Controls.ComboBoxItem
            {
                Content = text,
                IsEnabled = false,      // header, not selectable
                Focusable = false,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.Black,
                Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)),
                Padding = new Thickness(6, 3, 6, 3)
            };
        }

        private static System.Windows.Controls.ComboBoxItem MakeResolutionItem(string text)
        {
            return new System.Windows.Controls.ComboBoxItem { Content = text, Padding = new Thickness(18, 3, 6, 3) };
        }

        // A resolution read from an .ini that is not in the list is added to its aspect-ratio group,
        // so it can be selected (instead of silently keeping another value)
        private void EnsureResolutionInList(System.Windows.Controls.ComboBox cmb, string w, string h)
        {
            if (!int.TryParse(w, out int iw) || !int.TryParse(h, out int ih) || iw <= 0 || ih <= 0) return;

            var list = new List<(int W, int H)>();
            foreach (var o in cmb.Items)
            {
                if (o is System.Windows.Controls.ComboBoxItem it && it.IsEnabled && it.Content is string s)
                {
                    string[] parts = s.Split('x');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int a) && int.TryParse(parts[1], out int b) && a > 0 && b > 0)
                        list.Add((a, b));
                }
            }
            if (list.Contains((iw, ih))) return;

            string selected = (cmb.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content as string;
            list.Add((iw, ih));
            FillResolutionCombo(cmb, list);
            if (selected != null) SetComboBoxByValue(cmb, selected);
        }

        private void NumericOnly_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Rejects anything that is not a digit (0-9)
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9]+$");
        }
        private void NumericAndPointOnly_PreviewTextInput(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            // Rejects anything that is not a digit (0-9) or a decimal point
            e.Handled = !System.Text.RegularExpressions.Regex.IsMatch(e.Text, "^[0-9.]+$");
        }

        private void CheckDarkSoulsIni()
        {
            if (!File.Exists(darkSoulsIniPath))
            {
                MessageBox.Show("The in-game initialization file was not generated. It might be because the game has not been opened yet.\n\nIt is recommended to select the following in-game options:\n- Fullscreen: OFF\n- AntiAliasing: OFF\n- MotionBlur: OFF",
                                "Missing DarkSouls.ini", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string[] lines = File.ReadAllLines(darkSoulsIniPath);
            bool isWindowed = false;
            bool isWidthCorrect = false;
            bool isHeightCorrect = false;
            bool isWidthFullscreenCorrect = false;
            bool isHeightFullscreenCorrect = false;
            bool isAaOff = false;
            bool isBlurOff = false;

            // Variabilă pentru a urmări dacă citim în interiorul [DisplaySettingWindow]
            bool inDisplaySettingWindow = false;
            // Variabilă pentru a urmări dacă citim în interiorul [DisplaySettingFullScreen]
            bool inDisplaySettingFullscreenWindow = false;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                // Detectează schimbarea secțiunilor (ex: [DisplaySettingWindow], [DisplaySettingFullScreen])
                if (trimmedLine.StartsWith("["))
                {
                    inDisplaySettingWindow = (trimmedLine == "[DisplaySettingWindow]");
                }
                if (trimmedLine.StartsWith("["))
                {
                    inDisplaySettingFullscreenWindow = (trimmedLine == "[DisplaySettingFullScreen]");
                }

                if (trimmedLine == "WindowMode = 1") isWindowed = true;
                if (trimmedLine == "Antialiasing = 0") isAaOff = true;
                if (trimmedLine == "Blur = 0") isBlurOff = true;

                // Verifică lățimea și înălțimea DOAR dacă suntem în secțiunea [DisplaySettingWindow]
                if (inDisplaySettingWindow)
                {
                    if (trimmedLine == $"Width = {screenWidth}") isWidthCorrect = true;
                    if (trimmedLine == $"Height = {screenHeight}") isHeightCorrect = true;
                }
                // Verifică lățimea și înălțimea DOAR dacă suntem în secțiunea [DisplaySettingFullScreen]
                if (inDisplaySettingFullscreenWindow)
                {
                    if (trimmedLine == $"Width = {screenWidth}") isWidthFullscreenCorrect = true;
                    if (trimmedLine == $"Height = {screenHeight}") isHeightFullscreenCorrect = true;
                }
            }

            bool isResCorrect = isWidthCorrect && isHeightCorrect && isWidthFullscreenCorrect && isHeightFullscreenCorrect;

            // Adaugă și isResCorrect în condiția de avertizare
            if (!isWindowed || !isAaOff || !isBlurOff || !isResCorrect)
            {
                string probleme = "";
                if (!isWindowed) probleme += "- Window Mode not activated (required for Borderless).\n";
                if (!isResCorrect) probleme += $"- Game resolution setting does not match your display ({screenWidth}x{screenHeight}).\n";
                if (!isAaOff) probleme += "- In-game Anti-Aliasing is ON (conflict with DSFix).\n";
                if (!isBlurOff) probleme += "- In-game Motion Blur is ON.\n";

                MessageBoxResult result = MessageBox.Show($"In-game graphics settings are not optimal for DSFix:\n{probleme}\nWould you like to automatically adjust them for optimal performance?",
                                                          "Settings not compatible", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (result == MessageBoxResult.Yes)
                {
                    FixDarkSoulsIni(lines);
                }
            }
        }

        private void FixDarkSoulsIni(string[] lines)
        {
            bool inDisplaySettingWindow = false;
            bool inDisplaySettingFullscreenWindow = false;

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmedLine = lines[i].Trim();


                if (trimmedLine.StartsWith("["))
                {
                    inDisplaySettingWindow = (trimmedLine == "[DisplaySettingWindow]");
                }
                if (trimmedLine.StartsWith("["))
                {
                    inDisplaySettingFullscreenWindow = (trimmedLine == "[DisplaySettingFullScreen]");
                }

                // Setările globale care trebuie modificate
                if (trimmedLine.StartsWith("WindowMode")) lines[i] = "WindowMode = 1";
                else if (trimmedLine.StartsWith("Antialiasing")) lines[i] = "Antialiasing = 0";
                else if (trimmedLine.StartsWith("Blur")) lines[i] = "Blur = 0";


                if (inDisplaySettingWindow)
                {
                    if (trimmedLine.StartsWith("Width")) lines[i] = $"Width = {screenWidth}";
                    else if (trimmedLine.StartsWith("Height")) lines[i] = $"Height = {screenHeight}";
                }
                if (inDisplaySettingFullscreenWindow)
                {
                    if (trimmedLine.StartsWith("Width")) lines[i] = $"Width = {screenWidth}";
                    else if (trimmedLine.StartsWith("Height")) lines[i] = $"Height = {screenHeight}";
                }
            }

            File.WriteAllLines(darkSoulsIniPath, lines);
        }

        private void LoadDSFixIni()
        {
            if (!File.Exists(dsfixPath))
            {
                MessageBox.Show("DSfix.ini was not found in the same folder as the program.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
                return;
            }
            if (!File.Exists(dsfixKeysPath))
            {
                MessageBox.Show("DSfixKeys.ini was not found in the same folder as the program.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();

            }

            // 1. Load DSFix.ini
            string[] dsfixLines = File.ReadAllLines(dsfixPath);

            string rW = "1920", rH = "1080";
            string pW = "0", pH = "0";
            foreach (string line in dsfixLines)
            {
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                string[] parts = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                switch (key)
                {
                    case "renderWidth": rW = value; break;
                    case "renderHeight": rH = value; break;
                    case "presentWidth": pW = value; break;
                    case "presentHeight": pH = value; break;
                    case "aaQuality": SetComboBoxByValue(cmbAaQuality, value); break;
                    case "aaType": SetComboBoxByValue(cmbAaType, value); break;
                    case "ssaoStrength": SetComboBoxByValue(cmbSsaoStrength, value); break;
                    case "ssaoScale": SetComboBoxByValue(cmbSsaoScale, value); break;
                    case "ssaoType": SetComboBoxByValue(cmbSsaoType, value); break;
                    case "dofOverrideResolution": SetComboBoxByValue(cmbDofOverrideResolution, value); break;
                    case "disableDofScaling": chkDisableDofScaling.IsChecked = value == "1"; break;
                    case "dofBlurAmount": SetComboBoxByValue(cmbDofBlurAmount, value); break;
                    case "filteringOverride": SetComboBoxByValue(cmbFilteringOverride, value); break;
                    case "sharpenStrength": SetSliderByValue(sldSharpenStrength, value); break;
                    case "unlockFPS": chkUnlockFPS.IsChecked = value == "1"; break;
                    case "FPSlimit": txtFpsLimit.Text = value; break;
                    case "FPSthreshold": txtFpsThreshold.Text = value; break;

                    case "enableHudMod": chkEnableHudMod.IsChecked = value == "1"; break;
                    case "hudScaleTopLeft": SetSliderByValue(sldHudScaleTopLeft, value); break;
                    case "hudScaleBottomLeft": SetSliderByValue(sldHudScaleBottomLeft, value); break;
                    case "hudScaleBottomRight": SetSliderByValue(sldHudScaleBottomRight, value); break;
                    case "hudScaleBottomCenter": SetSliderByValue(sldHudScaleBottomCenter, value); break;
                    case "menuScale": SetSliderByValue(sldMenuScale, value); break;
                    case "listWindowScale": SetSliderByValue(sldListWindowScale, value); break;
                    case "promptScale": SetSliderByValue(sldPromptScale, value); break;
                    case "subtitleScale": SetSliderByValue(sldSubtitleScale, value); break;
                    case "hudTopLeftOpacity": txtHudTopLeftOpacity.Text = value; break;
                    case "hudBottomLeftOpacity": txtHudBottomLeftOpacity.Text = value; break;
                    case "hudBottomRightOpacity": txtHudBottomRightOpacity.Text = value; break;

                    case "borderlessFullscreen": chkBorderless.IsChecked = value == "1"; break;
                    case "disableCursor": chkDisableCursor.IsChecked = value == "1"; break;
                    case "captureCursor": chkCaptureCursor.IsChecked = value == "1"; break;
                    case "enableVsync": SetVsyncValue(value); break;
                    case "fullscreenHz": txtFullscreenHz.Text = value; break;

                    case "enableBackups": chkEnableBackups.IsChecked = value == "1"; break;
                    case "backupInterval": txtBackupInterval.Text = value; break;
                    case "maxBackups": txtMaxBackups.Text = value; break;
                    case "customSaveFolder": txtCustomSaveFolder.Text = value; break;

                    case "enableTextureDumping": chkEnableTextureDumping.IsChecked = value == "1"; break;
                    case "enableTextureOverride": chkTextureOverride.IsChecked = value == "1"; break;
                    case "enableTexturePrefetch": chkEnableTexturePrefetch.IsChecked = value == "1"; break;
                    case "enableShaderDumping": chkEnableShaderDumping.IsChecked = value == "1"; break;
                    case "enableShaderOverride": chkEnableShaderOverride.IsChecked = value == "1"; break;

                    case "skipIntro": chkSkipIntro.IsChecked = value == "1"; break;
                    case "screenshotDir": txtScreenshotDir.Text = value; break;
                    case "overrideLanguage": SetComboBoxByValue(cmbOverrideLanguage, value); break;
                    case "dinput8dllWrapper": txtDinput8dllWrapper.Text = value; break;
                    case "d3dAdapterOverride": txtD3dAdapterOverride.Text = value; break;
                    case "logLevel": SetComboBoxByValue(cmbLogLevel, value); break;
                    case "swapConfirmIcons": chkSwapConfirmIcons.IsChecked = value == "1"; break;
                    case "swapXYIcons": chkSwapXYIcons.IsChecked = value == "1"; break;
                    case "controllerIcons": if (value.Length > 0) SetComboBoxByValue(cmbControllerIcons, value.ToLowerInvariant()); break;
                }
            }
            EnsureResolutionInList(cmbRenderResolution, rW, rH);
            EnsureResolutionInList(cmbPresentResolution, pW, pH);
            SetComboBoxByValue(cmbRenderResolution, $"{rW}x{rH}");
            SetComboBoxByValue(cmbPresentResolution, $"{pW}x{pH}");

            // 2. Load DSfixKeys.ini
            string[] keysLines = File.ReadAllLines(dsfixKeysPath);
            foreach (string line in keysLines)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;

                bool isEnabled = !line.StartsWith("#");
                string cleanLine = line.TrimStart('#').Trim();

                string[] parts = cleanLine.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                string action = parts[0].Trim();
                string key = parts[1].Trim();

                switch (action)
                {
                    case "toggleCursorCapture": txtKeyToggleCursorCapture.Text = key; chkKeyToggleCursorCapture.IsChecked = isEnabled; break;
                    case "toggleCursorVisibility": txtKeyToggleCursorVisibility.Text = key; chkKeyToggleCursorVisibility.IsChecked = isEnabled; break;
                    case "toggleBorderlessFullscreen": txtKeyToggleBorderless.Text = key; chkKeyToggleBorderless.IsChecked = isEnabled; break;
                    case "takeHudlessScreenshot": txtKeyTakeHudlessScreenshot.Text = key; chkKeyTakeHudlessScreenshot.IsChecked = isEnabled; break;
                    case "toggleHUD": txtKeyToggleHUD.Text = key; chkKeyToggleHUD.IsChecked = isEnabled; break;
                    case "toggleHUDChanges": txtKeyToggleHUDChanges.Text = key; chkKeyToggleHUDChanges.IsChecked = isEnabled; break;
                    case "toggleSMAA": txtKeyToggleSMAA.Text = key; chkKeyToggleSMAA.IsChecked = isEnabled; break;
                    case "toggleVSSAO": txtKeyToggleVSSAO.Text = key; chkKeyToggleVSSAO.IsChecked = isEnabled; break;
                    case "toggleDofGauss": txtKeyToggleDofGauss.Text = key; chkKeyToggleDofGauss.IsChecked = isEnabled; break;
                    case "toggle30FPSLimit": txtKeyToggle30FPSLimit.Text = key; chkKeyToggle30FPSLimit.IsChecked = isEnabled; break;
                    case "reloadSSAOEffect": txtKeyReloadSSAO.Text = key; chkKeyReloadSSAO.IsChecked = isEnabled; break;
                    case "togglePaused": txtKeyTogglePaused.Text = key; chkKeyTogglePaused.IsChecked = isEnabled; break;
                    case "toggleSharpen": txtKeyToggleSharpen.Text = key; chkKeyToggleSharpen.IsChecked = isEnabled; break;
                }
            }
            SnapshotCurrentValues(this);

        }

        private void BtnApplyOptimal_Click(object sender, RoutedEventArgs e)
        {

            MessageBoxResult result = MessageBox.Show($"Change all settings to tested, optimal settings? Select \"Yes\" if you are not familiar with the saved settings.",
                                                          "Change Settings", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {

                chkUnlockFPS.IsChecked = true;
                txtFpsLimit.Text = "60";
                txtFpsThreshold.Text = "28";
                cmbAaType.Text = "SMAA";
                cmbAaQuality.SelectedIndex = 4; //Ultra
                cmbSsaoType.Text = "SCAO";
                cmbSsaoStrength.SelectedIndex = 2; // Medium
                cmbSsaoScale.SelectedIndex = 0; //High quality;
                sldSharpenStrength.Value = 0;
                chkBorderless.IsChecked = true;
                chkDisableCursor.IsChecked = true;
                chkEnableBackups.IsChecked = true;
                txtBackupInterval.Text = "1400";
                txtMaxBackups.Text = "10";
                txtCustomSaveFolder.Text = "%USERPROFILE%\\Documents\\NBGI\\DarkSouls";
                chkDisableDofScaling.IsChecked = false;
                chkTextureOverride.IsChecked = true;
                chkEnableTextureDumping.IsChecked = false;
                chkEnableTexturePrefetch.IsChecked = false;
                chkEnableShaderDumping.IsChecked = false;
                chkEnableShaderOverride.IsChecked = false;
                cmbPresentResolution.Text = "0x0";
                cmbFilteringOverride.SelectedIndex = 0;
                chkEnableHudMod.IsChecked = false;
                chkCaptureCursor.IsChecked = false;
                cmbOverrideLanguage.SelectedIndex = 0;
                txtScreenshotDir.Text = ".";
                txtD3dAdapterOverride.Text = "-1";
                cmbLogLevel.SelectedIndex = 0;

                //check dinput8 wrapper controller
                string DDTMControllerdllPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DDTMControllerFix.dll");
                if (File.Exists(DDTMControllerdllPath))
                {
                    txtDinput8dllWrapper.Text = "DDTMControllerFix.dll";
                }

                chkSkipIntro.IsChecked = true;

                // DarkSouls.ini: borderless needs the in-game windowed mode
                EnsureDarkSoulsWindowMode();

                if (screenWidth == 1024) // 1024x768
                {
                    cmbRenderResolution.Text = "1024x768";
                    cmbDofOverrideResolution.Text = "540";
                    cmbDofBlurAmount.SelectedIndex = 0; // 0 or 1
                }
                else if (screenWidth == 1280) // 1280x720
                {
                    cmbRenderResolution.Text = "1280x720";
                    cmbDofOverrideResolution.Text = "540";
                    cmbDofBlurAmount.SelectedIndex = 0; // 0 or 1
                }
                else if (screenWidth == 1366) // 1366x768
                {
                    cmbRenderResolution.Text = "1366x768";
                    cmbDofOverrideResolution.Text = "540";
                    cmbDofBlurAmount.SelectedIndex = 0; // 0 or 1
                }
                else if (screenWidth == 1600) // 1600x900
                {
                    cmbRenderResolution.Text = "1600x900";
                    cmbDofOverrideResolution.Text = "540";
                    cmbDofBlurAmount.SelectedIndex = 0;
                }
                else if (screenWidth == 1920) // 1920x1080
                {
                    cmbRenderResolution.Text = "1920x1080";
                    cmbDofOverrideResolution.Text = "810";
                    cmbDofBlurAmount.SelectedIndex = 1; // 1 or 2
                }
                else if (screenWidth == 2560) // 2560x1440
                {
                    cmbRenderResolution.Text = "2560x1440";
                    cmbDofOverrideResolution.Text = "1080";
                    cmbDofBlurAmount.SelectedIndex = 1; // 1 or 2
                }
                else if (screenWidth >= 3840) // 3840x2160
                {
                    cmbRenderResolution.Text = "3840x2160";
                    cmbDofOverrideResolution.Text = "1080";
                    cmbDofBlurAmount.SelectedIndex = 1; // 3 or 4
                }
                else // any other resolution: 16:10, Steam Deck, ultrawide, etc.
                {
                    int w = (int)screenWidth;
                    int h = (int)screenHeight;

                    // Render resolution = desktop resolution
                    cmbRenderResolution.Text = $"{w}x{h}";

                    // DoF override: smallest allowed value that is > half the height and < the height
                    int[] allowedDof = { 0, 540, 810, 1080, 2160 };
                    int dof = 0;
                    foreach (int candidate in allowedDof)
                    {
                        if (candidate > h / 2.0 && candidate < h)
                        {
                            dof = candidate;
                            break; // list is ascending, so this is the closest to half
                        }
                    }
                    cmbDofOverrideResolution.Text = dof.ToString();

                    // Blur amount follows the DoF value
                    cmbDofBlurAmount.SelectedIndex = dof switch
                    {
                        810 => 1,
                        1080 => 1,
                        2160 => 2,
                        _ => 0   // 0 or 540
                    };
                }

                TxtPcResolution.Foreground = new SolidColorBrush(Colors.Green);
                ValidateAllControls();
            }
        }

        // enableVsync: "-1" = game's choice, "1" = On, "0" = Off (combo order: game's choice, On, Off)
        private void SetVsyncValue(string value)
        {
            switch (value.Trim())
            {
                case "1": cmbEnableVsync.SelectedIndex = 1; break;
                case "0": cmbEnableVsync.SelectedIndex = 2; break;
                default: cmbEnableVsync.SelectedIndex = 0; break;   // -1 or anything unknown
            }
        }

        private string GetVsyncValue()
        {
            switch (cmbEnableVsync.SelectedIndex)
            {
                case 1: return "1";
                case 2: return "0";
                default: return "-1";
            }
        }

        // fullscreenHz: whole number, empty = 0 (game's choice)
        private string GetFullscreenHzValue()
        {
            return int.TryParse(txtFullscreenHz.Text.Trim(), out int hz) && hz > 0 ? hz.ToString() : "0";
        }

        private void NumericOnly_Pasting(object sender, System.Windows.DataObjectPastingEventArgs e)
        {
            string text = e.DataObject.GetData(typeof(string)) as string;
            if (text == null || !System.Text.RegularExpressions.Regex.IsMatch(text, "^[0-9]+$")) e.CancelCommand();
        }

        // Sets WindowMode = 1 in DarkSouls.ini when it is 0 (keeps the file's encoding)
        private void EnsureDarkSoulsWindowMode()
        {
            if (!File.Exists(darkSoulsIniPath)) return;
            try
            {
                System.Text.Encoding encoding;
                string[] lines;
                using (var reader = new StreamReader(darkSoulsIniPath, System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true))
                {
                    var list = new List<string>();
                    string line;
                    while ((line = reader.ReadLine()) != null) list.Add(line);
                    lines = list.ToArray();
                    encoding = reader.CurrentEncoding;
                }

                bool changed = false;
                for (int i = 0; i < lines.Length; i++)
                {
                    if (System.Text.RegularExpressions.Regex.IsMatch(lines[i].Trim(), @"^WindowMode\s*=\s*0$"))
                    {
                        lines[i] = "WindowMode = 1";
                        changed = true;
                    }
                }

                if (changed)
                {
                    File.WriteAllLines(darkSoulsIniPath, lines, encoding);
                    MessageBox.Show("DarkSouls.ini: in-game Window Mode was OFF and has been set to ON (WindowMode = 1), as needed for borderless fullscreen.",
                                    "DarkSouls.ini updated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not update DarkSouls.ini:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetComboBoxByValue(System.Windows.Controls.ComboBox cmb, string value)
        {
            foreach (System.Windows.Controls.ComboBoxItem item in cmb.Items)
            {
                if (!item.IsEnabled) continue;   // group headers (resolution lists) are not selectable
                if (item.Content.ToString().StartsWith(value) || item.Content.ToString() == value)
                {
                    cmb.SelectedItem = item;
                    break;
                }
            }
        }

        private void MenuLoadDSFix_Click(object sender, RoutedEventArgs e)
        {
            LoadDSFixIni();

            // Also reload the DDTM Controller tab from DDTMControllerMappings.ini
            string reloaded = "DSfix.ini and DSfixKeys.ini";
            if (File.Exists(controllerIniPath))
            {
                try
                {
                    LoadControllerIni();        // fills the 4 controller tabs and marks them as saved
                    controllerLoaded = true;
                    reloaded = "DSfix.ini, DSfixKeys.ini and DDTMControllerMappings.ini";
                }
                catch (Exception ex)
                {
                    controllerLoaded = false;   // the tab will try again when it is opened
                    MessageBox.Show("Could not read DDTMControllerMappings.ini:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            else
            {
                controllerLoaded = false;       // the tab shows its "file missing" message when opened
            }

            MessageBox.Show($"Settings from {reloaded} have been reloaded.", "Reloaded", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuSaveLocal_Click(object sender, RoutedEventArgs e)
        {
            // Collect all configs (Both standard and keys)
            string[] renderResL = cmbRenderResolution.Text.Split('x');
            string[] presentResL = cmbPresentResolution.Text.Split('x');

            string[] localConfig = {
                $"renderWidth={renderResL[0]}",
                $"renderHeight={renderResL[1]}",
                $"presentWidth={presentResL[0]}",
                $"presentHeight={presentResL[1]}",
                $"aaQuality={GetComboBoxValue(cmbAaQuality)}",
                $"aaType={GetComboBoxValue(cmbAaType)}",
                $"ssaoStrength={GetComboBoxValue(cmbSsaoStrength)}",
                $"ssaoScale={GetComboBoxValue(cmbSsaoScale)}",
                $"ssaoType={GetComboBoxValue(cmbSsaoType)}",
                $"dofOverrideResolution={GetComboBoxValue(cmbDofOverrideResolution)}",
                $"disableDofScaling={(chkDisableDofScaling.IsChecked == true ? "1" : "0")}",
                $"dofBlurAmount={GetComboBoxValue(cmbDofBlurAmount)}",
                $"filteringOverride={GetComboBoxValue(cmbFilteringOverride)}",
                $"sharpenStrength={GetSliderValue(sldSharpenStrength)}",
                $"unlockFPS={(chkUnlockFPS.IsChecked == true ? "1" : "0")}",
                $"FPSlimit={txtFpsLimit.Text}",
                $"FPSthreshold={txtFpsThreshold.Text}",

                $"enableHudMod={(chkEnableHudMod.IsChecked == true ? "1" : "0")}",
                $"hudScaleTopLeft={GetSliderValue(sldHudScaleTopLeft)}",
                $"hudScaleBottomLeft={GetSliderValue(sldHudScaleBottomLeft)}",
                $"hudScaleBottomRight={GetSliderValue(sldHudScaleBottomRight)}",
                $"hudScaleBottomCenter={GetSliderValue(sldHudScaleBottomCenter)}",
                $"menuScale={GetSliderValue(sldMenuScale)}",
                $"listWindowScale={GetSliderValue(sldListWindowScale)}",
                $"promptScale={GetSliderValue(sldPromptScale)}",
                $"subtitleScale={GetSliderValue(sldSubtitleScale)}",
                $"hudTopLeftOpacity={txtHudTopLeftOpacity.Text}",
                $"hudBottomLeftOpacity={txtHudBottomLeftOpacity.Text}",
                $"hudBottomRightOpacity={txtHudBottomRightOpacity.Text}",

                $"borderlessFullscreen={(chkBorderless.IsChecked == true ? "1" : "0")}",
                $"disableCursor={(chkDisableCursor.IsChecked == true ? "1" : "0")}",
                $"captureCursor={(chkCaptureCursor.IsChecked == true ? "1" : "0")}",
                $"enableVsync={GetVsyncValue()}",
                $"fullscreenHz={GetFullscreenHzValue()}",

                $"enableBackups={(chkEnableBackups.IsChecked == true ? "1" : "0")}",
                $"backupInterval={txtBackupInterval.Text}",
                $"maxBackups={txtMaxBackups.Text}",
                $"customSaveFolder={txtCustomSaveFolder.Text}",

                $"enableTextureDumping={(chkEnableTextureDumping.IsChecked == true ? "1" : "0")}",
                $"enableTextureOverride={(chkTextureOverride.IsChecked == true ? "1" : "0")}",
                $"enableTexturePrefetch={(chkEnableTexturePrefetch.IsChecked == true ? "1" : "0")}",
                $"enableShaderDumping={(chkEnableShaderDumping.IsChecked == true ? "1" : "0")}",
                $"enableShaderOverride={(chkEnableShaderOverride.IsChecked == true ? "1" : "0")}",

                $"skipIntro={(chkSkipIntro.IsChecked == true ? "1" : "0")}",
                $"screenshotDir={txtScreenshotDir.Text}",
                $"overrideLanguage={GetComboBoxValue(cmbOverrideLanguage)}",
                $"dinput8dllWrapper={txtDinput8dllWrapper.Text}",
                $"d3dAdapterOverride={txtD3dAdapterOverride.Text}",
                $"logLevel={GetComboBoxValue(cmbLogLevel)}",
                $"swapConfirmIcons={(chkSwapConfirmIcons.IsChecked == true ? "1" : "0")}",
                $"swapXYIcons={(chkSwapXYIcons.IsChecked == true ? "1" : "0")}",
                $"controllerIcons={GetComboBoxValue(cmbControllerIcons)}",

                // Keys mappings
                $"toggleCursorCapture={txtKeyToggleCursorCapture.Text}|{chkKeyToggleCursorCapture.IsChecked}",
                $"toggleCursorVisibility={txtKeyToggleCursorVisibility.Text}|{chkKeyToggleCursorVisibility.IsChecked}",
                $"toggleBorderlessFullscreen={txtKeyToggleBorderless.Text}|{chkKeyToggleBorderless.IsChecked}",
                $"takeHudlessScreenshot={txtKeyTakeHudlessScreenshot.Text}|{chkKeyTakeHudlessScreenshot.IsChecked}",
                $"toggleHUD={txtKeyToggleHUD.Text}|{chkKeyToggleHUD.IsChecked}",
                $"toggleHUDChanges={txtKeyToggleHUDChanges.Text}|{chkKeyToggleHUDChanges.IsChecked}",
                $"toggleSMAA={txtKeyToggleSMAA.Text}|{chkKeyToggleSMAA.IsChecked}",
                $"toggleVSSAO={txtKeyToggleVSSAO.Text}|{chkKeyToggleVSSAO.IsChecked}",
                $"toggleDofGauss={txtKeyToggleDofGauss.Text}|{chkKeyToggleDofGauss.IsChecked}",
                $"toggle30FPSLimit={txtKeyToggle30FPSLimit.Text}|{chkKeyToggle30FPSLimit.IsChecked}",
                $"reloadSSAOEffect={txtKeyReloadSSAO.Text}|{chkKeyReloadSSAO.IsChecked}",
                $"togglePaused={txtKeyTogglePaused.Text}|{chkKeyTogglePaused.IsChecked}",
                $"toggleSharpen={txtKeyToggleSharpen.Text}|{chkKeyToggleSharpen.IsChecked}"
            };

            string localConfigPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DDTM_DSFixUI_LocalConfig.txt");

            File.WriteAllLines(localConfigPath, localConfig);
            MessageBox.Show("Local configuration saved.", "Local Save", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void MenuLoadLocal_Click(object sender, RoutedEventArgs e)
        {
            string localConfigPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DDTM_DSFixUI_LocalConfig.txt");

            if (!File.Exists(localConfigPath))
            {
                MessageBox.Show("No local configuration saved.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string[] lines = File.ReadAllLines(localConfigPath);

            string rW = "1920", rH = "1080";
            string pW = "0", pH = "0";

            foreach (string line in lines)
            {
                string[] parts = line.Split(new[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                string key = parts[0].Trim();
                string value = parts[1].Trim();

                switch (key)
                {
                    case "renderWidth": rW = value; break;
                    case "renderHeight": rH = value; break;
                    case "presentWidth": pW = value; break;
                    case "presentHeight": pH = value; break;
                    case "aaQuality": SetComboBoxByValue(cmbAaQuality, value); break;
                    case "aaType": SetComboBoxByValue(cmbAaType, value); break;
                    case "ssaoStrength": SetComboBoxByValue(cmbSsaoStrength, value); break;
                    case "ssaoScale": SetComboBoxByValue(cmbSsaoScale, value); break;
                    case "ssaoType": SetComboBoxByValue(cmbSsaoType, value); break;
                    case "dofOverrideResolution": SetComboBoxByValue(cmbDofOverrideResolution, value); break;
                    case "disableDofScaling": chkDisableDofScaling.IsChecked = value == "1"; break;
                    case "dofBlurAmount": SetComboBoxByValue(cmbDofBlurAmount, value); break;
                    case "filteringOverride": SetComboBoxByValue(cmbFilteringOverride, value); break;
                    case "sharpenStrength": SetSliderByValue(sldSharpenStrength, value); break;
                    case "unlockFPS": chkUnlockFPS.IsChecked = value == "1"; break;
                    case "FPSlimit": txtFpsLimit.Text = value; break;
                    case "FPSthreshold": txtFpsThreshold.Text = value; break;

                    case "enableHudMod": chkEnableHudMod.IsChecked = value == "1"; break;
                    case "hudScaleTopLeft": SetSliderByValue(sldHudScaleTopLeft, value); break;
                    case "hudScaleBottomLeft": SetSliderByValue(sldHudScaleBottomLeft, value); break;
                    case "hudScaleBottomRight": SetSliderByValue(sldHudScaleBottomRight, value); break;
                    case "hudScaleBottomCenter": SetSliderByValue(sldHudScaleBottomCenter, value); break;
                    case "menuScale": SetSliderByValue(sldMenuScale, value); break;
                    case "listWindowScale": SetSliderByValue(sldListWindowScale, value); break;
                    case "promptScale": SetSliderByValue(sldPromptScale, value); break;
                    case "subtitleScale": SetSliderByValue(sldSubtitleScale, value); break;
                    case "hudTopLeftOpacity": txtHudTopLeftOpacity.Text = value; break;
                    case "hudBottomLeftOpacity": txtHudBottomLeftOpacity.Text = value; break;
                    case "hudBottomRightOpacity": txtHudBottomRightOpacity.Text = value; break;

                    case "borderlessFullscreen": chkBorderless.IsChecked = value == "1"; break;
                    case "disableCursor": chkDisableCursor.IsChecked = value == "1"; break;
                    case "captureCursor": chkCaptureCursor.IsChecked = value == "1"; break;
                    case "enableVsync": SetVsyncValue(value); break;
                    case "fullscreenHz": txtFullscreenHz.Text = value; break;

                    case "enableBackups": chkEnableBackups.IsChecked = value == "1"; break;
                    case "backupInterval": txtBackupInterval.Text = value; break;
                    case "maxBackups": txtMaxBackups.Text = value; break;
                    case "customSaveFolder": txtCustomSaveFolder.Text = value; break;

                    case "enableTextureDumping": chkEnableTextureDumping.IsChecked = value == "1"; break;
                    case "enableTextureOverride": chkTextureOverride.IsChecked = value == "1"; break;
                    case "enableTexturePrefetch": chkEnableTexturePrefetch.IsChecked = value == "1"; break;
                    case "enableShaderDumping": chkEnableShaderDumping.IsChecked = value == "1"; break;
                    case "enableShaderOverride": chkEnableShaderOverride.IsChecked = value == "1"; break;

                    case "skipIntro": chkSkipIntro.IsChecked = value == "1"; break;
                    case "screenshotDir": txtScreenshotDir.Text = value; break;
                    case "overrideLanguage": SetComboBoxByValue(cmbOverrideLanguage, value); break;
                    case "dinput8dllWrapper": txtDinput8dllWrapper.Text = value; break;
                    case "d3dAdapterOverride": txtD3dAdapterOverride.Text = value; break;
                    case "logLevel": SetComboBoxByValue(cmbLogLevel, value); break;
                    case "swapConfirmIcons": chkSwapConfirmIcons.IsChecked = value == "1"; break;
                    case "swapXYIcons": chkSwapXYIcons.IsChecked = value == "1"; break;
                    case "controllerIcons": if (value.Length > 0) SetComboBoxByValue(cmbControllerIcons, value.ToLowerInvariant()); break;

                    default:
                        // Handle Keys formatted as "Action=Key|IsChecked"
                        if (value.Contains("|"))
                        {
                            string[] keyParts = value.Split('|');
                            string vkKey = keyParts[0];
                            bool isChecked = keyParts[1] == "True";

                            switch (key)
                            {
                                case "toggleCursorCapture": txtKeyToggleCursorCapture.Text = vkKey; chkKeyToggleCursorCapture.IsChecked = isChecked; break;
                                case "toggleCursorVisibility": txtKeyToggleCursorVisibility.Text = vkKey; chkKeyToggleCursorVisibility.IsChecked = isChecked; break;
                                case "toggleBorderlessFullscreen": txtKeyToggleBorderless.Text = vkKey; chkKeyToggleBorderless.IsChecked = isChecked; break;
                                case "takeHudlessScreenshot": txtKeyTakeHudlessScreenshot.Text = vkKey; chkKeyTakeHudlessScreenshot.IsChecked = isChecked; break;
                                case "toggleHUD": txtKeyToggleHUD.Text = vkKey; chkKeyToggleHUD.IsChecked = isChecked; break;
                                case "toggleHUDChanges": txtKeyToggleHUDChanges.Text = vkKey; chkKeyToggleHUDChanges.IsChecked = isChecked; break;
                                case "toggleSMAA": txtKeyToggleSMAA.Text = vkKey; chkKeyToggleSMAA.IsChecked = isChecked; break;
                                case "toggleVSSAO": txtKeyToggleVSSAO.Text = vkKey; chkKeyToggleVSSAO.IsChecked = isChecked; break;
                                case "toggleDofGauss": txtKeyToggleDofGauss.Text = vkKey; chkKeyToggleDofGauss.IsChecked = isChecked; break;
                                case "toggle30FPSLimit": txtKeyToggle30FPSLimit.Text = vkKey; chkKeyToggle30FPSLimit.IsChecked = isChecked; break;
                                case "reloadSSAOEffect": txtKeyReloadSSAO.Text = vkKey; chkKeyReloadSSAO.IsChecked = isChecked; break;
                                case "togglePaused": txtKeyTogglePaused.Text = vkKey; chkKeyTogglePaused.IsChecked = isChecked; break;
                                case "toggleSharpen": txtKeyToggleSharpen.Text = vkKey; chkKeyToggleSharpen.IsChecked = isChecked; break;
                            }
                        }
                        break;
                }
            }
            EnsureResolutionInList(cmbRenderResolution, rW, rH);
            EnsureResolutionInList(cmbPresentResolution, pW, pH);
            SetComboBoxByValue(cmbRenderResolution, $"{rW}x{rH}");
            SetComboBoxByValue(cmbPresentResolution, $"{pW}x{pH}");
            MessageBox.Show("Local configuration loaded.", "Local Load", MessageBoxButton.OK, MessageBoxImage.Information);
            ValidateAllControls();

        }
        private async void BtnSaveDSFix_Click(object sender, RoutedEventArgs e)
        {
            // --- 1. SAVE DSFIX.INI ---
            if (!File.Exists(dsfixPath))
            {
                MessageBox.Show("The original DSfix.ini file was not found to be overwritten.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string[] renderRes = cmbRenderResolution.Text.Split('x');
            string[] presentRes = cmbPresentResolution.Text.Split('x');

            string[] lines = File.ReadAllLines(dsfixPath);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                string[] parts = line.Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 2) continue;

                string key = parts[0].Trim();

                // Overwrite values maintaining formatting and comments
                switch (key)
                {
                    case "renderWidth": lines[i] = $"renderWidth {renderRes[0]}"; break;
                    case "renderHeight": lines[i] = $"renderHeight {renderRes[1]}"; break;
                    case "presentWidth": lines[i] = $"presentWidth {presentRes[0]}"; break;
                    case "presentHeight": lines[i] = $"presentHeight {presentRes[1]}"; break;
                    case "aaQuality": lines[i] = $"aaQuality {GetComboBoxValue(cmbAaQuality)}"; break;
                    case "aaType": lines[i] = $"aaType {GetComboBoxValue(cmbAaType)}"; break;
                    case "ssaoStrength": lines[i] = $"ssaoStrength {GetComboBoxValue(cmbSsaoStrength)}"; break;
                    case "ssaoScale": lines[i] = $"ssaoScale {GetComboBoxValue(cmbSsaoScale)}"; break;
                    case "ssaoType": lines[i] = $"ssaoType {GetComboBoxValue(cmbSsaoType)}"; break;
                    case "dofOverrideResolution": lines[i] = $"dofOverrideResolution {GetComboBoxValue(cmbDofOverrideResolution)}"; break;
                    case "disableDofScaling": lines[i] = $"disableDofScaling {(chkDisableDofScaling.IsChecked == true ? "1" : "0")}"; break;
                    case "dofBlurAmount": lines[i] = $"dofBlurAmount {GetComboBoxValue(cmbDofBlurAmount)}"; break;
                    case "filteringOverride": lines[i] = $"filteringOverride {GetComboBoxValue(cmbFilteringOverride)}"; break;
                    case "sharpenStrength": lines[i] = $"sharpenStrength {GetSliderValue(sldSharpenStrength)}"; break;
                    case "unlockFPS": lines[i] = $"unlockFPS {(chkUnlockFPS.IsChecked == true ? "1" : "0")}"; break;
                    case "FPSlimit": lines[i] = $"FPSlimit {txtFpsLimit.Text}"; break;
                    case "FPSthreshold": lines[i] = $"FPSthreshold {txtFpsThreshold.Text}"; break;

                    case "enableHudMod": lines[i] = $"enableHudMod {(chkEnableHudMod.IsChecked == true ? "1" : "0")}"; break;
                    case "hudScaleTopLeft": lines[i] = $"hudScaleTopLeft {GetSliderValue(sldHudScaleTopLeft)}"; break;
                    case "hudScaleBottomLeft": lines[i] = $"hudScaleBottomLeft {GetSliderValue(sldHudScaleBottomLeft)}"; break;
                    case "hudScaleBottomRight": lines[i] = $"hudScaleBottomRight {GetSliderValue(sldHudScaleBottomRight)}"; break;
                    case "hudScaleBottomCenter": lines[i] = $"hudScaleBottomCenter {GetSliderValue(sldHudScaleBottomCenter)}"; break;
                    case "menuScale": lines[i] = $"menuScale {GetSliderValue(sldMenuScale)}"; break;
                    case "listWindowScale": lines[i] = $"listWindowScale {GetSliderValue(sldListWindowScale)}"; break;
                    case "promptScale": lines[i] = $"promptScale {GetSliderValue(sldPromptScale)}"; break;
                    case "subtitleScale": lines[i] = $"subtitleScale {GetSliderValue(sldSubtitleScale)}"; break;
                    case "hudTopLeftOpacity": lines[i] = $"hudTopLeftOpacity {txtHudTopLeftOpacity.Text}"; break;
                    case "hudBottomLeftOpacity": lines[i] = $"hudBottomLeftOpacity {txtHudBottomLeftOpacity.Text}"; break;
                    case "hudBottomRightOpacity": lines[i] = $"hudBottomRightOpacity {txtHudBottomRightOpacity.Text}"; break;

                    case "borderlessFullscreen": lines[i] = $"borderlessFullscreen {(chkBorderless.IsChecked == true ? "1" : "0")}"; break;
                    case "disableCursor": lines[i] = $"disableCursor {(chkDisableCursor.IsChecked == true ? "1" : "0")}"; break;
                    case "captureCursor": lines[i] = $"captureCursor {(chkCaptureCursor.IsChecked == true ? "1" : "0")}"; break;
                    case "enableVsync": lines[i] = $"enableVsync {GetVsyncValue()}"; break;
                    case "fullscreenHz": lines[i] = $"fullscreenHz {GetFullscreenHzValue()}"; break;

                    case "enableBackups": lines[i] = $"enableBackups {(chkEnableBackups.IsChecked == true ? "1" : "0")}"; break;
                    case "backupInterval": lines[i] = $"backupInterval {txtBackupInterval.Text}"; break;
                    case "maxBackups": lines[i] = $"maxBackups {txtMaxBackups.Text}"; break;
                    case "customSaveFolder": lines[i] = $"customSaveFolder {txtCustomSaveFolder.Text}"; break;

                    case "enableTextureDumping": lines[i] = $"enableTextureDumping {(chkEnableTextureDumping.IsChecked == true ? "1" : "0")}"; break;
                    case "enableTextureOverride": lines[i] = $"enableTextureOverride {(chkTextureOverride.IsChecked == true ? "1" : "0")}"; break;
                    case "enableTexturePrefetch": lines[i] = $"enableTexturePrefetch {(chkEnableTexturePrefetch.IsChecked == true ? "1" : "0")}"; break;
                    case "enableShaderDumping": lines[i] = $"enableShaderDumping {(chkEnableShaderDumping.IsChecked == true ? "1" : "0")}"; break;
                    case "enableShaderOverride": lines[i] = $"enableShaderOverride {(chkEnableShaderOverride.IsChecked == true ? "1" : "0")}"; break;

                    case "skipIntro": lines[i] = $"skipIntro {(chkSkipIntro.IsChecked == true ? "1" : "0")}"; break;
                    case "screenshotDir": lines[i] = $"screenshotDir {txtScreenshotDir.Text}"; break;
                    case "overrideLanguage": lines[i] = $"overrideLanguage {GetComboBoxValue(cmbOverrideLanguage)}"; break;
                    case "dinput8dllWrapper": lines[i] = $"dinput8dllWrapper {txtDinput8dllWrapper.Text}"; break;
                    case "d3dAdapterOverride": lines[i] = $"d3dAdapterOverride {txtD3dAdapterOverride.Text}"; break;
                    case "logLevel": lines[i] = $"logLevel {GetComboBoxValue(cmbLogLevel)}"; break;
                    case "swapConfirmIcons": lines[i] = $"swapConfirmIcons {(chkSwapConfirmIcons.IsChecked == true ? "1" : "0")}"; break;
                    case "swapXYIcons": lines[i] = $"swapXYIcons {(chkSwapXYIcons.IsChecked == true ? "1" : "0")}"; break;
                    case "controllerIcons": if (cmbControllerIcons.SelectedItem != null) lines[i] = $"controllerIcons {GetComboBoxValue(cmbControllerIcons)}"; break;   // never appended: older DSfix.ini files do not have it
                }
            }
            // Newer settings: append them if the .ini does not contain them yet
            var newerSliders = new (string Key, System.Windows.Controls.Slider Slider)[]
            {
                ("hudScaleBottomCenter", sldHudScaleBottomCenter),
                ("menuScale", sldMenuScale),
                ("listWindowScale", sldListWindowScale),
                ("promptScale", sldPromptScale),
                ("subtitleScale", sldSubtitleScale)
            };
            var withNew = new List<string>(lines);
            foreach (var (key, slider) in newerSliders)
            {
                // exact key match on an active (not commented) line
                bool exists = withNew.Any(l => l.TrimStart().Split(new[] { ' ', '\t' }, 2)[0] == key);
                if (!exists) withNew.Add($"{key} {GetSliderValue(slider)}");
            }
            var newerChecks = new (string Key, System.Windows.Controls.CheckBox Check)[]
            {
                ("swapConfirmIcons", chkSwapConfirmIcons),
                ("swapXYIcons", chkSwapXYIcons)
            };
            foreach (var (key, check) in newerChecks)
            {
                bool exists = withNew.Any(l => l.TrimStart().Split(new[] { ' ', '\t' }, 2)[0] == key);
                if (!exists) withNew.Add($"{key} {(check.IsChecked == true ? "1" : "0")}");
            }
            var newerValues = new (string Key, string Value)[]
            {
                ("sharpenStrength", GetSliderValue(sldSharpenStrength)),
                ("enableVsync", GetVsyncValue()),
                ("fullscreenHz", GetFullscreenHzValue())
            };
            foreach (var (key, val) in newerValues)
            {
                bool exists = withNew.Any(l => l.TrimStart().Split(new[] { ' ', '\t' }, 2)[0] == key);
                if (!exists) withNew.Add($"{key} {val}");
            }
            lines = withNew.ToArray();
            File.WriteAllLines(dsfixPath, lines);

            // --- 2. SAVE DSFIXKEYS.INI ---
            if (File.Exists(dsfixKeysPath))
            {
                var keysConfig = new Dictionary<string, (string vkKey, bool isEnabled)>
                {
                    { "toggleCursorCapture", (txtKeyToggleCursorCapture.Text, chkKeyToggleCursorCapture.IsChecked == true) },
                    { "toggleCursorVisibility", (txtKeyToggleCursorVisibility.Text, chkKeyToggleCursorVisibility.IsChecked == true) },
                    { "toggleBorderlessFullscreen", (txtKeyToggleBorderless.Text, chkKeyToggleBorderless.IsChecked == true) },
                    { "takeHudlessScreenshot", (txtKeyTakeHudlessScreenshot.Text, chkKeyTakeHudlessScreenshot.IsChecked == true) },
                    { "toggleHUD", (txtKeyToggleHUD.Text, chkKeyToggleHUD.IsChecked == true) },
                    { "toggleHUDChanges", (txtKeyToggleHUDChanges.Text, chkKeyToggleHUDChanges.IsChecked == true) },
                    { "toggleSMAA", (txtKeyToggleSMAA.Text, chkKeyToggleSMAA.IsChecked == true) },
                    { "toggleVSSAO", (txtKeyToggleVSSAO.Text, chkKeyToggleVSSAO.IsChecked == true) },
                    { "toggleDofGauss", (txtKeyToggleDofGauss.Text, chkKeyToggleDofGauss.IsChecked == true) },
                    { "toggle30FPSLimit", (txtKeyToggle30FPSLimit.Text, chkKeyToggle30FPSLimit.IsChecked == true) },
                    { "reloadSSAOEffect", (txtKeyReloadSSAO.Text, chkKeyReloadSSAO.IsChecked == true) },
                    { "togglePaused", (txtKeyTogglePaused.Text, chkKeyTogglePaused.IsChecked == true) },
                    { "toggleSharpen", (txtKeyToggleSharpen.Text, chkKeyToggleSharpen.IsChecked == true) }
                };

                string[] keysLines = File.ReadAllLines(dsfixKeysPath);
                for (int i = 0; i < keysLines.Length; i++)
                {
                    string line = keysLines[i];
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    string cleanLine = line.TrimStart('#').Trim();
                    string[] parts = cleanLine.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);

                    if (parts.Length > 0)
                    {
                        string action = parts[0].Trim();
                        if (keysConfig.ContainsKey(action))
                        {
                            var settings = keysConfig[action];
                            string prefix = settings.isEnabled ? "" : "#";
                            keysLines[i] = $"{prefix}{action} {settings.vkKey}";
                            keysConfig.Remove(action); // Track that it was processed
                        }
                    }
                }

                // If any keys were completely missing from the original file, append them at the end
                List<string> finalKeysLines = new List<string>(keysLines);
                foreach (var kvp in keysConfig)
                {
                    string prefix = kvp.Value.isEnabled ? "" : "#";
                    finalKeysLines.Add($"{prefix}{kvp.Key} {kvp.Value.vkKey}");
                }

                File.WriteAllLines(dsfixKeysPath, finalKeysLines);
            }
            SnapshotCurrentValues(this);

            labeldsfixsalvat.Visibility = Visibility.Visible;
            await System.Threading.Tasks.Task.Delay(2000);
            labeldsfixsalvat.Visibility = Visibility.Hidden;
        }

        private void SetSliderByValue(System.Windows.Controls.Slider sld, string value)
        {
            // The .ini files always use '.' as decimal separator, regardless of the Windows locale
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            {
                sld.Value = Math.Max(sld.Minimum, Math.Min(sld.Maximum, v));
            }
        }

        private string GetSliderValue(System.Windows.Controls.Slider sld)
        {
            return sld.Value.ToString("0.00", CultureInfo.InvariantCulture);
        }

        private string GetComboBoxValue(System.Windows.Controls.ComboBox cmb)
        {
            if (cmb.SelectedItem == null) return "";
            string content = ((System.Windows.Controls.ComboBoxItem)cmb.SelectedItem).Content.ToString();

            if (content.Contains("-"))
            {
                return content.Split('-')[0].Trim();
            }
            return content.Trim();
        }

        private void BtnLaunchGame_Click(object sender, RoutedEventArgs e)
        {
            string gameExePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DARKSOULS.exe");

            if (File.Exists(gameExePath))
                System.Diagnostics.Process.Start(gameExePath);
            else
                MessageBox.Show("DARKSOULS.exe was not found in the current folder.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void chkUnlockFPS_Unchecked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("If you do not unlock the framerate (unlockFPS), the game will not use the optimized DSFix code for a smooth framerate.", "FPS Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void TxtFpsLimit_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtFpsLimit.Text)) return;

            if (int.TryParse(txtFpsLimit.Text, out int value))
            {
                if (value > 120)
                {
                    txtFpsLimit.Text = "120";
                    txtFpsLimit.CaretIndex = txtFpsLimit.Text.Length;
                }
                else if (value < 1)
                {
                    txtFpsLimit.Text = "1";
                    txtFpsLimit.CaretIndex = txtFpsLimit.Text.Length;
                }
            }
        }

        private void txtFpsThreshold_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (string.IsNullOrEmpty(txtFpsThreshold.Text)) return;

            if (int.TryParse(txtFpsThreshold.Text, out int value))
            {
                if (value > 120)
                {
                    txtFpsThreshold.Text = "120";
                    txtFpsThreshold.CaretIndex = txtFpsThreshold.Text.Length;
                }
                else if (value < 1)
                {
                    txtFpsThreshold.Text = "1";
                    txtFpsThreshold.CaretIndex = txtFpsThreshold.Text.Length;
                }
            }
        }

        private void chkEnableHudMod_Checked(object sender, RoutedEventArgs e)
        {
            if (wrappanelhud3 == null) return;
            sep1.Visibility = Visibility.Visible;
            txtblockhud1.Visibility = Visibility.Visible;
            wrappanelhud1.Visibility = Visibility.Visible;
            sep2.Visibility = Visibility.Visible;
            txtblockhud2.Visibility = Visibility.Visible;
            wrappanelhud2.Visibility = Visibility.Visible;
            sep3.Visibility = Visibility.Visible;
            txtblockhud3.Visibility = Visibility.Visible;
            wrappanelhud3.Visibility = Visibility.Visible;
        }

        private void chkEnableHudMod_Unchecked(object sender, RoutedEventArgs e)
        {
            if (wrappanelhud3 == null) return;
            sep1.Visibility = Visibility.Hidden;
            txtblockhud1.Visibility = Visibility.Hidden;
            wrappanelhud1.Visibility = Visibility.Hidden;
            sep2.Visibility = Visibility.Hidden;
            txtblockhud2.Visibility = Visibility.Hidden;
            wrappanelhud2.Visibility = Visibility.Hidden;
            sep3.Visibility = Visibility.Hidden;
            txtblockhud3.Visibility = Visibility.Hidden;
            wrappanelhud3.Visibility = Visibility.Hidden;
        }

        private void chkEnableBackups_Checked(object sender, RoutedEventArgs e)
        {
            if (WrapPanelsave1 == null) return;
            WrapPanelsave1.Visibility = Visibility.Visible;
            labelsavefolder.Visibility = Visibility.Visible;
            txtCustomSaveFolder.Visibility = Visibility.Visible;
            wrappanelsave3.Visibility = Visibility.Visible;
        }

        private void chkEnableBackups_Unchecked(object sender, RoutedEventArgs e)
        {
            if (WrapPanelsave1 == null) return;
            WrapPanelsave1.Visibility = Visibility.Hidden;
            labelsavefolder.Visibility = Visibility.Hidden;
            txtCustomSaveFolder.Visibility = Visibility.Hidden;
            wrappanelsave3.Visibility = Visibility.Hidden;
        }

        private void BtnLoadKeysDefault_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show($"Replace the current assignments?",
                                                          "Default Settings", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result == MessageBoxResult.Yes)
            {
                txtKeyToggleCursorCapture.Text = "VK_F6";
                txtKeyToggleCursorVisibility.Text = "VK_F7";
                txtKeyToggleBorderless.Text = "VK_F8";
                txtKeyTakeHudlessScreenshot.Text = "VK_NEXT";
                txtKeyToggleHUD.Text = "VK_RCONTROL";
                txtKeyToggleHUDChanges.Text = "VK_RSHIFT";
                txtKeyToggleSMAA.Text = "VK_NumPad1";
                txtKeyToggleVSSAO.Text = "VK_NUMPAD2";
                txtKeyToggleDofGauss.Text = "VK_NUMPAD3";
                txtKeyToggle30FPSLimit.Text = "VK_BACK";
                txtKeyReloadSSAO.Text = "VK_NUMPAD5";
                txtKeyTogglePaused.Text = "VK_F9";
                txtKeyToggleSharpen.Text = "VK_F4";
                ValidateAllControls();
            }
        }

        private void chkDisableDofScaling_Checked(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Disabling DoF scaling will result in a sharper image and worse performance, and deviates from the originally intended look.", "Disable DoF Scaling", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BrowseCustomSaveFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select your custom save backup folder",
                Multiselect = false
            };

            if (dialog.ShowDialog() == true)
            {
                txtCustomSaveFolder.Text = dialog.FolderName; // Atenție, se folosește FolderName, nu FileName
                ValidateAllControls();
            }
        }

        private void BrowseScreenshotFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFolderDialog
            {
                Title = "Select your custom Screenshot path",
                Multiselect = false
            };


            if (dialog.ShowDialog() == true)
            {
                txtScreenshotDir.Text = dialog.FolderName; // Atenție, se folosește FolderName, nu FileName
                ValidateAllControls();
            }
        }

        private void BrowseDinput8Wrapper_Click(object sender, RoutedEventArgs e)
        {
            string appDir = AppDomain.CurrentDomain.BaseDirectory;

            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Select the dinput8.dll wrapper to chain (e.g. DDTMControllerFix.dll)",
                Filter = "DLL files (*.dll)|*.dll",
                InitialDirectory = appDir,
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;

            string fileName = System.IO.Path.GetFileName(dialog.FileName);

            // DSfix itself is dinput8.dll, so it cannot be chained to itself
            if (fileName.Equals("dinput8.dll", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("dinput8.dll is DSfix itself and cannot be used as the wrapper.\nSelect the other dll you want to chain (e.g. DDTMControllerFix.dll).",
                                "dinput8.dll Wrapper", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string dllFolder = System.IO.Path.GetFullPath(System.IO.Path.GetDirectoryName(dialog.FileName)).TrimEnd('\\');
            string gameFolder = System.IO.Path.GetFullPath(appDir).TrimEnd('\\');

            if (string.Equals(dllFolder, gameFolder, StringComparison.OrdinalIgnoreCase))
            {
                // in the game folder: store just the file name, as DSfix expects
                txtDinput8dllWrapper.Text = fileName;
            }
            else
            {
                string target = System.IO.Path.Combine(appDir, fileName);
                string replaceNote = File.Exists(target) ? "\n(The file with the same name in the game folder will be replaced.)" : "";

                var answer = MessageBox.Show(
                    $"{fileName} is not in the game folder.\n\n" +
                    "Yes = copy it into the game folder and use it (recommended)" + replaceNote + "\n" +
                    "No = use it from where it is (full path)\n" +
                    "Cancel = do nothing",
                    "dinput8.dll Wrapper", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);

                if (answer == MessageBoxResult.Cancel) return;

                if (answer == MessageBoxResult.Yes)
                {
                    try
                    {
                        File.Copy(dialog.FileName, target, overwrite: true);
                        txtDinput8dllWrapper.Text = fileName;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Could not copy the dll:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    txtDinput8dllWrapper.Text = dialog.FileName;
                }
            }

            ValidateAllControls();   // blue-violet if it differs from DSfix.ini
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void SetupChangeTracking(DependencyObject parent)
        {
            foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(parent))
            {
                if (child is DependencyObject depChild)
                {
                    if (depChild is System.Windows.Controls.TextBox txt)
                    {
                        txt.LostFocus += Control_ValueChanged;
                        originalControlColors[txt] = txt.Foreground; // Save original XAML color
                    }
                    else if (depChild is System.Windows.Controls.ComboBox cmb)
                    {
                        cmb.LostFocus += Control_ValueChanged;
                        cmb.DropDownClosed += Control_ValueChanged;
                        originalControlColors[cmb] = cmb.Foreground;
                    }
                    else if (depChild is System.Windows.Controls.CheckBox chk)
                    {
                        chk.Click += Control_ValueChanged;
                        originalControlColors[chk] = chk.Foreground;
                    }
                    else if (depChild is System.Windows.Controls.Slider sld)
                    {
                        sld.ValueChanged += Control_ValueChanged;
                        originalControlColors[sld] = sld.Foreground;
                    }

                    SetupChangeTracking(depChild); // Recursively search inside tabs/panels
                }
            }
        }

        private void SnapshotCurrentValues(DependencyObject parent)
        {
            foreach (var child in System.Windows.LogicalTreeHelper.GetChildren(parent))
            {
                if (child is DependencyObject depChild)
                {
                    // the DDTM Controller tab keeps its own saved state (see MainWindow.Controller.cs)
                    if (ReferenceEquals(depChild, tabController)) continue;

                    if (depChild is System.Windows.Controls.TextBox txt)
                    {
                        originalControlValues[txt] = txt.Text;
                        txt.Foreground = originalControlColors[txt]; // Reset to original color
                    }
                    else if (depChild is System.Windows.Controls.ComboBox cmb)
                    {
                        originalControlValues[cmb] = cmb.Text;
                        cmb.Foreground = originalControlColors[cmb];
                    }
                    else if (depChild is System.Windows.Controls.CheckBox chk)
                    {
                        originalControlValues[chk] = chk.IsChecked.ToString();
                        chk.Foreground = originalControlColors[chk];
                    }
                    else if (depChild is System.Windows.Controls.Slider sld)
                    {
                        originalControlValues[sld] = GetSliderValue(sld);
                        ApplySliderBrush(sld, originalControlColors[sld]);
                    }
                    SnapshotCurrentValues(depChild);
                }
            }
        }

        private void Control_ValueChanged(object sender, EventArgs e)
        {
            ValidateControlColor(sender as System.Windows.Controls.Control);
        }

        private void ValidateControlColor(System.Windows.Controls.Control ctrl)
        {
            if (ctrl == null || !originalControlValues.ContainsKey(ctrl)) return;

            string originalValue = originalControlValues[ctrl];
            string currentValue = "";

            if (ctrl is System.Windows.Controls.TextBox txt) currentValue = txt.Text;
            else if (ctrl is System.Windows.Controls.ComboBox cmb) currentValue = cmb.Text;
            else if (ctrl is System.Windows.Controls.CheckBox chk) currentValue = chk.IsChecked.ToString();
            else if (ctrl is System.Windows.Controls.Slider sld) currentValue = GetSliderValue(sld);

            // If the value changed, make it blue (DeepSkyBlue looks great on dark themes). Otherwise, reset it.
            Brush brush = currentValue != originalValue ? Brushes.BlueViolet : originalControlColors[ctrl];
            if (ctrl is System.Windows.Controls.Slider slider)
                ApplySliderBrush(slider, brush);
            else
                ctrl.Foreground = brush;
        }

        // Colors the slider and its value label (linked through the slider's Tag)
        private void ApplySliderBrush(System.Windows.Controls.Slider sld, Brush brush)
        {
            sld.Foreground = brush;
            if (sld.Tag is System.Windows.Controls.TextBlock valueLabel)
                valueLabel.Foreground = brush;
        }

        private void ValidateAllControls()
        {
            foreach (var ctrl in originalControlValues.Keys)
            {
                ValidateControlColor(ctrl);
            }
        }
    }
}