using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

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
            // Restrict input to digits/decimals where appropriate
            txtFpsLimit.PreviewTextInput += NumericOnly_PreviewTextInput;
            txtFpsThreshold.PreviewTextInput += NumericOnly_PreviewTextInput;
            txtBackupInterval.PreviewTextInput += NumericOnly_PreviewTextInput;
            txtMaxBackups.PreviewTextInput += NumericOnly_PreviewTextInput;

            txtHudScaleTopLeft.PreviewTextInput += NumericAndPointOnly_PreviewTextInput;
            txtHudScaleBottomLeft.PreviewTextInput += NumericAndPointOnly_PreviewTextInput;
            txtHudScaleBottomRight.PreviewTextInput += NumericAndPointOnly_PreviewTextInput;
            txtHudTopLeftOpacity.PreviewTextInput += NumericAndPointOnly_PreviewTextInput;
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

        private void PopulateResolutions()
        {
            // List of standard resolutions from 640x480 up to 4K
            var resolutions = new[] {
        (640, 480), (800, 600), (1024, 768), (1280, 720),
        (1366, 768), (1600, 900), (1920, 1080), (2560, 1440), (3840, 2160)
    };

            cmbPresentResolution.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = "0x0" });

            foreach (var res in resolutions)
            {
                string resString = $"{res.Item1}x{res.Item2}";

                // Add to Downscaling (all available)
                cmbPresentResolution.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = resString });

                // Add to Render (Only if within PC screen limits)
                if (res.Item1 <= screenWidth && res.Item2 <= screenHeight)
                {
                    cmbRenderResolution.Items.Add(new System.Windows.Controls.ComboBoxItem { Content = resString });
                }
            }

            // Set defaults 
            if (cmbRenderResolution.Items.Count > 0)
                cmbRenderResolution.SelectedIndex = cmbRenderResolution.Items.Count - 1; // Highest native resolution by default

            cmbPresentResolution.SelectedIndex = 0; // 0x0 default
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
            bool isAaOff = false;
            bool isBlurOff = false;

            // Variabilă pentru a urmări dacă citim în interiorul [DisplaySettingWindow]
            bool inDisplaySettingWindow = false;

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                // Detectează schimbarea secțiunilor (ex: [DisplaySettingWindow], [DisplaySettingFullScreen])
                if (trimmedLine.StartsWith("["))
                {
                    inDisplaySettingWindow = (trimmedLine == "[DisplaySettingWindow]");
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
            }

            bool isResCorrect = isWidthCorrect && isHeightCorrect;

            // Adaugă și isResCorrect în condiția de avertizare
            if (!isWindowed || !isAaOff || !isBlurOff || !isResCorrect)
            {
                string probleme = "";
                if (!isWindowed) probleme += "- Window Mode not activated (required for Borderless).\n";
                if (!isResCorrect) probleme += $"- Window resolution setting does not match your display ({screenWidth}x{screenHeight}).\n";
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

            for (int i = 0; i < lines.Length; i++)
            {
                string trimmedLine = lines[i].Trim();

                // Detectează dacă intrăm sau ieșim din secțiunea [DisplaySettingWindow]
                if (trimmedLine.StartsWith("["))
                {
                    inDisplaySettingWindow = (trimmedLine == "[DisplaySettingWindow]");
                }

                // Setările globale care trebuie modificate
                if (trimmedLine.StartsWith("WindowMode")) lines[i] = "WindowMode = 1";
                else if (trimmedLine.StartsWith("Antialiasing")) lines[i] = "Antialiasing = 0";
                else if (trimmedLine.StartsWith("Blur")) lines[i] = "Blur = 0";

                // Modificăm Width și Height DOAR dacă suntem în [DisplaySettingWindow]
                if (inDisplaySettingWindow)
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
                    case "unlockFPS": chkUnlockFPS.IsChecked = value == "1"; break;
                    case "FPSlimit": txtFpsLimit.Text = value; break;
                    case "FPSthreshold": txtFpsThreshold.Text = value; break;

                    case "enableHudMod": chkEnableHudMod.IsChecked = value == "1"; break;
                    case "enableMinimalHud": chkMinimalHud.IsChecked = value == "1"; break;
                    case "hudScaleTopLeft": txtHudScaleTopLeft.Text = value; break;
                    case "hudScaleBottomLeft": txtHudScaleBottomLeft.Text = value; break;
                    case "hudScaleBottomRight": txtHudScaleBottomRight.Text = value; break;
                    case "hudTopLeftOpacity": txtHudTopLeftOpacity.Text = value; break;
                    case "hudBottomLeftOpacity": txtHudBottomLeftOpacity.Text = value; break;
                    case "hudBottomRightOpacity": txtHudBottomRightOpacity.Text = value; break;

                    case "borderlessFullscreen": chkBorderless.IsChecked = value == "1"; break;
                    case "disableCursor": chkDisableCursor.IsChecked = value == "1"; break;
                    case "captureCursor": chkCaptureCursor.IsChecked = value == "1"; break;

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
                }
            }
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
                txtFpsLimit.Text = "30";
                txtFpsThreshold.Text = "20";
                cmbAaType.Text = "SMAA";
                cmbAaQuality.SelectedIndex = 4;
                cmbSsaoType.Text = "VSSAO2";
                cmbSsaoStrength.SelectedIndex = 2; // Medium
                cmbSsaoScale.SelectedIndex = 0; //High quality;     
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
                chkEnableShaderOverride.IsChecked= false;
                cmbPresentResolution.Text = "0x0";
                cmbFilteringOverride.SelectedIndex = 0;
                chkEnableHudMod.IsChecked = false;
                chkCaptureCursor.IsChecked= false;
                cmbOverrideLanguage.SelectedIndex = 0;
                txtScreenshotDir.Text = ".";
                txtD3dAdapterOverride.Text = "-1";
                cmbLogLevel.SelectedIndex = 0;
      
                chkSkipIntro.IsChecked = true;

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
                    cmbDofBlurAmount.SelectedIndex = 0; // 1 or 2
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
                    cmbDofOverrideResolution.Text = "2160";
                    cmbDofBlurAmount.SelectedIndex = 3; // 3 or 4
                }

                TxtPcResolution.Foreground = new SolidColorBrush(Colors.Green);
                ValidateAllControls();
            }
        }

        private void SetComboBoxByValue(System.Windows.Controls.ComboBox cmb, string value)
        {
            foreach (System.Windows.Controls.ComboBoxItem item in cmb.Items)
            {
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
            MessageBox.Show("Settings from DSfix.ini and DSfixKeys.ini have been reloaded.", "Reloaded", MessageBoxButton.OK, MessageBoxImage.Information);
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
                $"unlockFPS={(chkUnlockFPS.IsChecked == true ? "1" : "0")}",
                $"FPSlimit={txtFpsLimit.Text}",
                $"FPSthreshold={txtFpsThreshold.Text}",

                $"enableHudMod={(chkEnableHudMod.IsChecked == true ? "1" : "0")}",
                $"enableMinimalHud={(chkMinimalHud.IsChecked == true ? "1" : "0")}",
                $"hudScaleTopLeft={txtHudScaleTopLeft.Text}",
                $"hudScaleBottomLeft={txtHudScaleBottomLeft.Text}",
                $"hudScaleBottomRight={txtHudScaleBottomRight.Text}",
                $"hudTopLeftOpacity={txtHudTopLeftOpacity.Text}",
                $"hudBottomLeftOpacity={txtHudBottomLeftOpacity.Text}",
                $"hudBottomRightOpacity={txtHudBottomRightOpacity.Text}",

                $"borderlessFullscreen={(chkBorderless.IsChecked == true ? "1" : "0")}",
                $"disableCursor={(chkDisableCursor.IsChecked == true ? "1" : "0")}",
                $"captureCursor={(chkCaptureCursor.IsChecked == true ? "1" : "0")}",

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
                $"togglePaused={txtKeyTogglePaused.Text}|{chkKeyTogglePaused.IsChecked}"
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
                    case "unlockFPS": chkUnlockFPS.IsChecked = value == "1"; break;
                    case "FPSlimit": txtFpsLimit.Text = value; break;
                    case "FPSthreshold": txtFpsThreshold.Text = value; break;

                    case "enableHudMod": chkEnableHudMod.IsChecked = value == "1"; break;
                    case "enableMinimalHud": chkMinimalHud.IsChecked = value == "1"; break;
                    case "hudScaleTopLeft": txtHudScaleTopLeft.Text = value; break;
                    case "hudScaleBottomLeft": txtHudScaleBottomLeft.Text = value; break;
                    case "hudScaleBottomRight": txtHudScaleBottomRight.Text = value; break;
                    case "hudTopLeftOpacity": txtHudTopLeftOpacity.Text = value; break;
                    case "hudBottomLeftOpacity": txtHudBottomLeftOpacity.Text = value; break;
                    case "hudBottomRightOpacity": txtHudBottomRightOpacity.Text = value; break;

                    case "borderlessFullscreen": chkBorderless.IsChecked = value == "1"; break;
                    case "disableCursor": chkDisableCursor.IsChecked = value == "1"; break;
                    case "captureCursor": chkCaptureCursor.IsChecked = value == "1"; break;

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
                            }
                        }
                        break;
                }
            }
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
                    case "unlockFPS": lines[i] = $"unlockFPS {(chkUnlockFPS.IsChecked == true ? "1" : "0")}"; break;
                    case "FPSlimit": lines[i] = $"FPSlimit {txtFpsLimit.Text}"; break;
                    case "FPSthreshold": lines[i] = $"FPSthreshold {txtFpsThreshold.Text}"; break;

                    case "enableHudMod": lines[i] = $"enableHudMod {(chkEnableHudMod.IsChecked == true ? "1" : "0")}"; break;
                    case "enableMinimalHud": lines[i] = $"enableMinimalHud {(chkMinimalHud.IsChecked == true ? "1" : "0")}"; break;
                    case "hudScaleTopLeft": lines[i] = $"hudScaleTopLeft {txtHudScaleTopLeft.Text}"; break;
                    case "hudScaleBottomLeft": lines[i] = $"hudScaleBottomLeft {txtHudScaleBottomLeft.Text}"; break;
                    case "hudScaleBottomRight": lines[i] = $"hudScaleBottomRight {txtHudScaleBottomRight.Text}"; break;
                    case "hudTopLeftOpacity": lines[i] = $"hudTopLeftOpacity {txtHudTopLeftOpacity.Text}"; break;
                    case "hudBottomLeftOpacity": lines[i] = $"hudBottomLeftOpacity {txtHudBottomLeftOpacity.Text}"; break;
                    case "hudBottomRightOpacity": lines[i] = $"hudBottomRightOpacity {txtHudBottomRightOpacity.Text}"; break;

                    case "borderlessFullscreen": lines[i] = $"borderlessFullscreen {(chkBorderless.IsChecked == true ? "1" : "0")}"; break;
                    case "disableCursor": lines[i] = $"disableCursor {(chkDisableCursor.IsChecked == true ? "1" : "0")}"; break;
                    case "captureCursor": lines[i] = $"captureCursor {(chkCaptureCursor.IsChecked == true ? "1" : "0")}"; break;

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
                }
            }
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
                    { "togglePaused", (txtKeyTogglePaused.Text, chkKeyTogglePaused.IsChecked == true) }
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
                if (value > 60)
                {
                    txtFpsLimit.Text = "60";
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
                if (value > 60)
                {
                    txtFpsThreshold.Text = "60";
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
            if (chkMinimalHud == null) return;
            chkMinimalHud.Visibility = Visibility.Visible;
            sep1.Visibility = Visibility.Visible;
            txtblockhud1.Visibility = Visibility.Visible;
            wrappanelhud1.Visibility = Visibility.Visible;
            sep2.Visibility = Visibility.Visible;
            txtblockhud2.Visibility = Visibility.Visible;
            wrappanelhud2.Visibility = Visibility.Visible;
        }

        private void chkEnableHudMod_Unchecked(object sender, RoutedEventArgs e)
        {
            if (chkMinimalHud == null) return;
            chkMinimalHud.Visibility = Visibility.Hidden;
            sep1.Visibility = Visibility.Hidden;
            txtblockhud1.Visibility = Visibility.Hidden;
            wrappanelhud1.Visibility = Visibility.Hidden;
            sep2.Visibility = Visibility.Hidden;
            txtblockhud2.Visibility = Visibility.Hidden;
            wrappanelhud2.Visibility = Visibility.Hidden;
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

            // If the value changed, make it blue (DeepSkyBlue looks great on dark themes). Otherwise, reset it.
            if (currentValue != originalValue)
            {
                ctrl.Foreground = Brushes.BlueViolet;          
            }
            else
            {
                ctrl.Foreground = originalControlColors[ctrl];
            }
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