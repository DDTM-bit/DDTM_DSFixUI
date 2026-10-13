using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace DDTM_DSFixUI
{
    // "DDTM Controller" tab: reads / writes DDTMControllerMappings.ini
    public partial class MainWindow
    {
        private readonly string controllerIniPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DDTMControllerMappings.ini");
        private bool controllerLoaded = false;

        // ---- Button lists (headers + buttons, exactly as in the .ini) ----
        private static readonly (string Header, string[] Buttons)[] ControllerButtonGroups =
        {
            ("PlayStation", new[] { "CROSS", "CIRCLE", "SQUARE", "TRIANGLE", "L1", "R1", "L2", "R2", "L3", "R3", "SHARE", "OPTIONS", "PS", "TOUCHPAD" }),
            ("Xbox",        new[] { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "LS", "RS", "BACK", "START", "GUIDE" }),
            ("Nintendo",    new[] { "SOUTH", "EAST", "WEST", "NORTH", "L", "R", "ZL", "ZR", "MINUS", "PLUS" }),
            ("D-Pad",       new[] { "DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT" }),
        };

        // Every key except BACK (which can appear twice and is handled separately)
        private static readonly string[] MappingKeys =
            { "A", "B", "X", "Y", "LB", "RB", "LT", "RT", "LS", "RS", "START", "DPAD_UP", "DPAD_DOWN", "DPAD_LEFT", "DPAD_RIGHT" };
        private static readonly string[] MacroKeys =
            { "KICK", "LEAP_ATTACK", "AUTO_RUN", "DASH_SPRINT_ONLY", "ROLL_ONLY", "JUMP_ONLY" };
        private static readonly string[] BoolKeys =
            { "AutoRun_CancelOnStop", "SuppressMacroButtons", "MUTE_JUMP_FROM_B" };
        private static readonly string[] SliderKeys =
        {
            "LS_DeadzoneMin", "LS_DeadzoneMax", "RS_DeadzoneMin", "RS_DeadzoneMax",
            "LS_Sensitivity_X", "LS_Sensitivity_Y", "RS_Sensitivity_X", "RS_Sensitivity_Y",
            "LT_Deadzone", "RT_Deadzone", "LT_ActivationThreshold", "RT_ActivationThreshold",
            "Rumble_LeftMotor", "Rumble_RightMotor"
        };
        private static readonly string[] AdvancedKeys = { "RunState_Ms", "JumpGuard_Ms" };
        private static readonly string[] PresetValues = { "NONE", "DS3_JUMP", "MORTAL_SHELL", "ELDEN_RING", "RUN_AND_ATTACK" };

        // Hard-coded default values (the ones in the shipped .ini)
        private static readonly Dictionary<string, string> ControllerDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Preset"] = "NONE",
            ["A"] = "CROSS", ["B"] = "CIRCLE", ["X"] = "SQUARE", ["Y"] = "TRIANGLE",
            ["LB"] = "L1", ["RB"] = "R1", ["LT"] = "L2", ["RT"] = "R2", ["LS"] = "L3", ["RS"] = "R3",
            ["START"] = "OPTIONS",
            ["DPAD_UP"] = "DPAD_UP", ["DPAD_DOWN"] = "DPAD_DOWN", ["DPAD_LEFT"] = "DPAD_LEFT", ["DPAD_RIGHT"] = "DPAD_RIGHT",
            ["KICK"] = "NONE", ["LEAP_ATTACK"] = "NONE", ["AUTO_RUN"] = "NONE",
            ["DASH_SPRINT_ONLY"] = "NONE", ["ROLL_ONLY"] = "NONE", ["JUMP_ONLY"] = "NONE",
            ["AutoRun_CancelOnStop"] = "TRUE", ["SuppressMacroButtons"] = "TRUE", ["MUTE_JUMP_FROM_B"] = "FALSE",
            ["LS_DeadzoneMin"] = "7000", ["LS_DeadzoneMax"] = "22000", ["RS_DeadzoneMin"] = "3000", ["RS_DeadzoneMax"] = "22000",
            ["LS_Sensitivity_X"] = "1.0", ["LS_Sensitivity_Y"] = "1.0", ["RS_Sensitivity_X"] = "1.5", ["RS_Sensitivity_Y"] = "1.5",
            ["LT_Deadzone"] = "1000", ["RT_Deadzone"] = "4000", ["LT_ActivationThreshold"] = "30000", ["RT_ActivationThreshold"] = "30000",
            ["Rumble_LeftMotor"] = "1.0", ["Rumble_RightMotor"] = "1.0",
            ["RunState_Ms"] = "800", ["JumpGuard_Ms"] = "500",
        };
        private static readonly string[] DefaultBack = { "TOUCHPAD", "SHARE" };

        // "# RunState_Ms = 800" : the two advanced keys are shipped commented out
        private static readonly Regex AdvancedCommentRegex =
            new Regex(@"^#\s*(RunState_Ms|JumpGuard_Ms)\s*=\s*(\d+)\s*$", RegexOptions.IgnoreCase);

        // ------------------------------------------------------------------
        //  Helpers
        // ------------------------------------------------------------------
        private T Ctl<T>(string name) where T : class
        {
            return FindName(name) as T ?? throw new InvalidOperationException("Control not found: " + name);
        }

        private static bool IsIntSlider(string key) =>
            key.Contains("Deadzone", StringComparison.Ordinal) || key.Contains("Threshold", StringComparison.Ordinal);

        private void InitControllerTab()
        {
            // keep the (taller) window inside the visible work area on small screens
            Height = Math.Min(Height, SystemParameters.WorkArea.Height);

            var comboNames = new List<string>();
            foreach (string k in MappingKeys) comboNames.Add("cmbMap_" + k);
            comboNames.Add("cmbMap_BACK");
            comboNames.Add("cmbMap_BACK2");
            foreach (string k in MacroKeys)
                for (int i = 1; i <= 3; i++) comboNames.Add($"cmbMacro_{k}_{i}");
            foreach (string n in comboNames) PopulateButtonCombo(Ctl<ComboBox>(n));

            foreach (string p in PresetValues)
                Ctl<RadioButton>("rbPreset_" + p).Checked += PresetChanged;

            foreach (string k in AdvancedKeys)
            {
                var chk = Ctl<CheckBox>("chkAdv_" + k);
                var txt = Ctl<TextBox>("txtAdv_" + k);
                chk.Checked += (s, e) => txt.IsEnabled = true;
                chk.Unchecked += (s, e) => txt.IsEnabled = false;
                txt.IsEnabled = chk.IsChecked == true;
                txt.PreviewTextInput += NumericOnly_PreviewTextInput;
            }
        }

        private void PopulateButtonCombo(ComboBox cmb)
        {
            cmb.Items.Clear();
            cmb.Items.Add(new ComboBoxItem { Content = "NONE" });
            foreach (var group in ControllerButtonGroups)
            {
                cmb.Items.Add(new ComboBoxItem
                {
                    Content = group.Header,
                    IsEnabled = false,           // header, not selectable
                    Focusable = false,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.Black,
                    Background = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)),
                    Padding = new Thickness(6, 3, 6, 3)
                });
                foreach (string b in group.Buttons)
                    cmb.Items.Add(new ComboBoxItem { Content = b, Padding = new Thickness(18, 3, 6, 3) });
            }
            cmb.SelectedIndex = 0;
        }

        private void SelectButton(ComboBox cmb, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) value = "NONE";
            foreach (object o in cmb.Items)
            {
                if (o is ComboBoxItem it && it.IsEnabled && string.Equals(it.Content as string, value.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    cmb.SelectedItem = it;
                    return;
                }
            }
            // a value we do not know (hand-edited file): keep it instead of losing it
            var extra = new ComboBoxItem { Content = value.Trim() };
            cmb.Items.Add(extra);
            cmb.SelectedItem = extra;
        }

        private static string GetButton(ComboBox cmb)
        {
            string v = (cmb.SelectedItem as ComboBoxItem)?.Content as string;
            return string.IsNullOrWhiteSpace(v) ? "NONE" : v;
        }

        private string GetSelectedPreset()
        {
            foreach (string p in PresetValues)
                if (Ctl<RadioButton>("rbPreset_" + p).IsChecked == true) return p;
            return "NONE";
        }

        private void PresetChanged(object sender, RoutedEventArgs e)
        {
            if (FindName("panelMacros") == null) return;
            bool none = GetSelectedPreset() == "NONE";
            Ctl<StackPanel>("panelMacros").IsEnabled = none;
            Ctl<Border>("txtMacroBanner").Visibility = none ? Visibility.Collapsed : Visibility.Visible;
        }

        private static string Pick(Dictionary<string, string> kv, string key) =>
            kv.TryGetValue(key, out string v) ? v : ControllerDefaults[key];

        // ------------------------------------------------------------------
        //  Tab selection: error + empty tab when the file is missing
        // ------------------------------------------------------------------
        private void TabMain_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // ignore selection events bubbling up from combo boxes / the nested tab control
            if (!ReferenceEquals(e.OriginalSource, sender)) return;
            if (tabController == null || gridController == null || tabMain == null) return;
            if (!ReferenceEquals(tabMain.SelectedItem, tabController)) return;

            if (!File.Exists(controllerIniPath))
            {
                controllerLoaded = false;
                gridController.Visibility = Visibility.Collapsed;
                MessageBox.Show("DDTMControllerMappings.ini was not found in the same folder as the program.\n\n" +
                                "Run the game once with the DDTM Controller Fix installed so the file is created, or copy it next to this program.",
                                "DDTM Controller - file missing", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string DDTMControllerdllPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DDTMControllerFix.dll");

            if (!File.Exists(DDTMControllerdllPath))
            {
                controllerLoaded = false;
                gridController.Visibility = Visibility.Collapsed;
                MessageBox.Show("DDTMControllerFix.dll was not found in the same folder as the program.\n\n",
                                "DDTM Controller - file missing", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            
            if (File.Exists(DDTMControllerdllPath) && txtDinput8dllWrapper.Text != "DDTMControllerFix.dll")
            {
                MessageBoxResult result = MessageBox.Show($"DDTMControllerFix.dll is in your folder, but it isn't set up as a dinput8.dll wrapper. The controller settings here only work with that dll. Do you want to set it as a dinput8 wrapper in the Miscellaneous tab?",
                                                          "dinput8 wrapper", MessageBoxButton.YesNo, MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    txtDinput8dllWrapper.Text = "DDTMControllerFix.dll";
                }
            }

            if (!controllerLoaded)
            {
                try
                {
                    LoadControllerIni();
                    controllerLoaded = true;
                }
                catch (Exception ex)
                {
                    gridController.Visibility = Visibility.Collapsed;
                    MessageBox.Show("Could not read DDTMControllerMappings.ini:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
            }
            gridController.Visibility = Visibility.Visible;
        }

        // ------------------------------------------------------------------
        //  READ
        // ------------------------------------------------------------------
        private void LoadControllerIni()
        {
            var kv = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var back = new List<string>();
            var advOff = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string line in File.ReadAllLines(controllerIniPath))
            {
                string t = line.Trim();
                if (t.Length == 0) continue;

                if (t.StartsWith("#"))
                {
                    var m = AdvancedCommentRegex.Match(t);
                    if (m.Success) advOff[m.Groups[1].Value] = m.Groups[2].Value;
                    continue;
                }
                if (t.StartsWith("[")) continue;   // section header

                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                string key = t.Substring(0, eq).Trim();
                string val = t.Substring(eq + 1).Trim();

                if (key.Equals("BACK", StringComparison.OrdinalIgnoreCase)) back.Add(val);
                else if (!kv.ContainsKey(key)) kv[key] = val;
            }

            ApplyControllerValues(kv, back, advOff);
            SnapshotCurrentValues(tabController);   // this is the "saved" state for change colouring
        }

        // Puts values into the controls (used by Load and by Load defaults)
        private void ApplyControllerValues(Dictionary<string, string> kv, List<string> back, Dictionary<string, string> advOff)
        {
            // preset
            string preset = Pick(kv, "Preset");
            bool found = false;
            foreach (string p in PresetValues)
            {
                bool match = string.Equals(p, preset, StringComparison.OrdinalIgnoreCase);
                Ctl<RadioButton>("rbPreset_" + p).IsChecked = match;
                found |= match;
            }
            if (!found) Ctl<RadioButton>("rbPreset_NONE").IsChecked = true;

            // button mapping
            foreach (string k in MappingKeys) SelectButton(Ctl<ComboBox>("cmbMap_" + k), Pick(kv, k));
            if (back.Count == 0) back = new List<string>(DefaultBack);
            SelectButton(Ctl<ComboBox>("cmbMap_BACK"), back[0]);
            SelectButton(Ctl<ComboBox>("cmbMap_BACK2"), back.Count > 1 ? back[1] : "NONE");

            // macros: "R1 + L3" -> three combos
            foreach (string k in MacroKeys)
            {
                string[] parts = Pick(kv, k).Split('+');
                for (int i = 0; i < 3; i++)
                    SelectButton(Ctl<ComboBox>($"cmbMacro_{k}_{i + 1}"), i < parts.Length ? parts[i].Trim() : "NONE");
            }

            // behaviour switches
            foreach (string k in BoolKeys)
                Ctl<CheckBox>("chk_" + k).IsChecked = string.Equals(Pick(kv, k), "TRUE", StringComparison.OrdinalIgnoreCase)
                                                      || Pick(kv, k) == "1";

            // sliders
            foreach (string k in SliderKeys)
            {
                var sld = Ctl<Slider>("sld_" + k);
                if (!double.TryParse(Pick(kv, k), NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
                    v = double.Parse(ControllerDefaults[k], CultureInfo.InvariantCulture);
                sld.Value = Math.Max(sld.Minimum, Math.Min(sld.Maximum, v));
            }

            // advanced (active line = enabled, "# key = n" = disabled but keeps its value)
            foreach (string k in AdvancedKeys)
            {
                bool enabled = kv.ContainsKey(k);
                string val = enabled ? kv[k] : (advOff.TryGetValue(k, out string off) ? off : ControllerDefaults[k]);
                Ctl<CheckBox>("chkAdv_" + k).IsChecked = enabled;
                var txt = Ctl<TextBox>("txtAdv_" + k);
                txt.Text = val;
                txt.IsEnabled = enabled;
            }

            PresetChanged(null, null);
        }

        // ------------------------------------------------------------------
        //  DEFAULTS
        // ------------------------------------------------------------------
        private void BtnLoadControllerDefaults_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Replace ALL settings of the 4 controller tabs with the default values?\nNothing is written to the .ini until you press SAVE.",
                                         "Load default settings", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            var advOff = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["RunState_Ms"] = ControllerDefaults["RunState_Ms"],
                ["JumpGuard_Ms"] = ControllerDefaults["JumpGuard_Ms"]
            };
            var kv = new Dictionary<string, string>(ControllerDefaults, StringComparer.OrdinalIgnoreCase);
            foreach (string k in AdvancedKeys) kv.Remove(k);    // advanced values are shipped disabled
            ApplyControllerValues(kv, new List<string>(DefaultBack), advOff);
            ValidateAllControls();
        }

        private void BtnJapaneseLayout_Click(object sender, RoutedEventArgs e)
        {
            SelectButton(Ctl<ComboBox>("cmbMap_A"), "CIRCLE");
            SelectButton(Ctl<ComboBox>("cmbMap_B"), "CROSS");
            ValidateAllControls();
        }

        private void BtnStandardLayout_Click(object sender, RoutedEventArgs e)
        {
            SelectButton(Ctl<ComboBox>("cmbMap_A"), "CROSS");
            SelectButton(Ctl<ComboBox>("cmbMap_B"), "CIRCLE");
            ValidateAllControls();
        }

        // ------------------------------------------------------------------
        //  WRITE
        // ------------------------------------------------------------------
        private static string FormatSlider(string key, double v) =>
            IsIntSlider(key)
                ? ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture)
                : Math.Round(v, 2).ToString("0.0#", CultureInfo.InvariantCulture);

        private static string KeySection(string key)
        {
            if (key == "Preset") return "";
            if (MappingKeys.Contains(key) || MacroKeys.Contains(key) || key == "BACK"
                || key == "AutoRun_CancelOnStop" || key == "SuppressMacroButtons") return "Bindings";
            if (AdvancedKeys.Contains(key)) return "Advanced";
            return "Settings";
        }

        private static void InsertIntoSection(List<string> lines, string section, string text)
        {
            if (section == "") { lines.Insert(0, text); return; }

            int start = lines.FindIndex(l => l.Trim().Equals("[" + section + "]", StringComparison.OrdinalIgnoreCase));
            if (start < 0) { lines.Add("[" + section + "]"); lines.Add(text); return; }

            int end = lines.FindIndex(start + 1, l => l.TrimStart().StartsWith("["));
            if (end < 0) end = lines.Count;
            // step back over blank / comment lines so the key lands right after the section's last real key
            while (end > start + 1 && (lines[end - 1].Trim().Length == 0 || lines[end - 1].TrimStart().StartsWith("#"))) end--;
            lines.Insert(end, text);
        }

        private void BtnSaveController_Click(object sender, RoutedEventArgs e)
        {
            if (!File.Exists(controllerIniPath))
            {
                MessageBox.Show("DDTMControllerMappings.ini was not found in the same folder as the program.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // ---- collect values from the 4 sub tabs ----
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            values["Preset"] = GetSelectedPreset();
            foreach (string k in MappingKeys) values[k] = GetButton(Ctl<ComboBox>("cmbMap_" + k));

            var backLines = new List<string>();
            string b1 = GetButton(Ctl<ComboBox>("cmbMap_BACK"));
            string b2 = GetButton(Ctl<ComboBox>("cmbMap_BACK2"));
            if (b1 != "NONE") backLines.Add(b1);
            if (b2 != "NONE" && !string.Equals(b1, b2, StringComparison.OrdinalIgnoreCase)) backLines.Add(b2);
            if (backLines.Count == 0) backLines.Add("NONE");

            foreach (string k in MacroKeys)
            {
                var parts = new List<string>();
                for (int i = 1; i <= 3; i++)
                {
                    string b = GetButton(Ctl<ComboBox>($"cmbMacro_{k}_{i}"));
                    if (b != "NONE" && !parts.Contains(b)) parts.Add(b);
                }
                values[k] = parts.Count == 0 ? "NONE" : string.Join(" + ", parts);
            }

            foreach (string k in BoolKeys) values[k] = Ctl<CheckBox>("chk_" + k).IsChecked == true ? "TRUE" : "FALSE";
            foreach (string k in SliderKeys) values[k] = FormatSlider(k, Ctl<Slider>("sld_" + k).Value);

            var advEnabled = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
            foreach (string k in AdvancedKeys)
            {
                string text = Ctl<TextBox>("txtAdv_" + k).Text.Trim();
                if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n < 1)
                {
                    MessageBox.Show($"{k} must be a whole number greater than 0.", "Invalid value", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                values[k] = n.ToString(CultureInfo.InvariantCulture);
                advEnabled[k] = Ctl<CheckBox>("chkAdv_" + k).IsChecked == true;
            }

            // ---- rewrite the file line by line, keeping every comment ----
            string[] original = File.ReadAllLines(controllerIniPath);
            var output = new List<string>();
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool backWritten = false;

            foreach (string line in original)
            {
                string t = line.Trim();

                if (t.StartsWith("#"))
                {
                    var m = AdvancedCommentRegex.Match(t);
                    if (m.Success)
                    {
                        string k = m.Groups[1].Value;
                        string key = AdvancedKeys.First(a => a.Equals(k, StringComparison.OrdinalIgnoreCase));
                        output.Add((advEnabled[key] ? "" : "# ") + key + " = " + values[key]);
                        written.Add(key);
                    }
                    else output.Add(line);
                    continue;
                }

                int eq = t.IndexOf('=');
                if (t.StartsWith("[") || eq <= 0) { output.Add(line); continue; }

                string lineKey = t.Substring(0, eq).Trim();

                if (lineKey.Equals("BACK", StringComparison.OrdinalIgnoreCase))
                {
                    if (!backWritten)
                    {
                        foreach (string b in backLines) output.Add("BACK = " + b);
                        backWritten = true;
                    }
                    continue;   // any further BACK line is replaced by the block above
                }

                if (values.TryGetValue(lineKey, out string newVal))
                {
                    if (AdvancedKeys.Contains(lineKey, StringComparer.OrdinalIgnoreCase))
                    {
                        string key = AdvancedKeys.First(a => a.Equals(lineKey, StringComparison.OrdinalIgnoreCase));
                        output.Add((advEnabled[key] ? "" : "# ") + key + " = " + newVal);
                    }
                    else output.Add(lineKey + " = " + newVal);
                    written.Add(lineKey);
                }
                else output.Add(line);
            }

            // keys that were not in the file at all (older file version): add them to their section
            if (!backWritten)
                for (int i = backLines.Count - 1; i >= 0; i--) InsertIntoSection(output, "Bindings", "BACK = " + backLines[i]);
            foreach (var kvp in values)
            {
                if (written.Contains(kvp.Key)) continue;
                bool isAdv = advEnabled.TryGetValue(kvp.Key, out bool on);
                if (isAdv && !on) continue;     // disabled + absent: nothing to write
                InsertIntoSection(output, KeySection(kvp.Key), kvp.Key + " = " + kvp.Value);
            }

            try
            {
                File.WriteAllLines(controllerIniPath, output);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not write DDTMControllerMappings.ini:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            SnapshotCurrentValues(tabController);
            ShowControllerSaved();
        }

        private async void ShowControllerSaved()
        {
            labelControllerSaved.Visibility = Visibility.Visible;
            await System.Threading.Tasks.Task.Delay(2000);
            labelControllerSaved.Visibility = Visibility.Hidden;
        }
    }
}
