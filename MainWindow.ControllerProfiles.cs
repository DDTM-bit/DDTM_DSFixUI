using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace DDTM_DSFixUI
{
    // DDTM Controller tab extras:
    //  - offers to keep DSfix.ini swapConfirmIcons / swapXYIcons in line with the button mapping
    //  - Save Profile / Load Profile
    public partial class MainWindow
    {
        private const string ProfileFilter = "DDTM Controller profile (*.ini)|*.ini|All files (*.*)|*.*";

        // ------------------------------------------------------------------
        //  A/B and X/Y in-game button icons
        // ------------------------------------------------------------------
        private static bool IsOneOf(string value, params string[] options) =>
            options.Any(o => o.Equals(value?.Trim(), StringComparison.OrdinalIgnoreCase));

        // Game input A must be on the "east" button and B on the "south" button
        private bool IsConfirmSwapped()
        {
            string a = GetButton(Ctl<ComboBox>("cmbMap_A"));
            string b = GetButton(Ctl<ComboBox>("cmbMap_B"));
            return IsOneOf(b, "CROSS", "A", "SOUTH") && IsOneOf(a, "CIRCLE", "B", "EAST");
        }

        // Game input X must be on the "north" button and Y on the "west" button
        private bool IsXYSwapped()
        {
            string x = GetButton(Ctl<ComboBox>("cmbMap_X"));
            string y = GetButton(Ctl<ComboBox>("cmbMap_Y"));
            return IsOneOf(x, "TRIANGLE", "Y", "NORTH") && IsOneOf(y, "SQUARE", "X", "WEST");
        }

        // Called after the controller .ini was written successfully.
        // Asks the user first; DSfix.ini is only changed when the answer is Yes.
        private void AskSyncPromptIconsWithMapping()
        {
            if (!File.Exists(dsfixPath)) return;

            bool abSwapped = IsConfirmSwapped();
            bool xySwapped = IsXYSwapped();
            string wantAb = abSwapped ? "1" : "0";
            string wantXy = xySwapped ? "1" : "0";

            Dictionary<string, string> current;
            try
            {
                current = ReadDsfixValues("swapConfirmIcons", "swapXYIcons", "DDTMControllerMappings");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not read DSfix.ini:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // DDTMControllerMappings 1: DSfix takes the in-game button icons from this mapping itself (through
            // DDTMControllerFix.dll) and does not use swapConfirmIcons / swapXYIcons, so there is nothing to ask
            if (current.TryGetValue("DDTMControllerMappings", out string ddtmIcons) && ddtmIcons == "1") return;

            // a missing key counts as 0 (icons not swapped)
            string curAb = current.TryGetValue("swapConfirmIcons", out string a) ? a : "0";
            string curXy = current.TryGetValue("swapXYIcons", out string x) ? x : "0";

            var changes = new List<string>();
            if (curAb != wantAb)
                changes.Add(abSwapped ? "- swap the A/B (cross / circle) button icons in game (swapConfirmIcons 1)"
                                      : "- set the A/B (cross / circle) button icons back to normal (swapConfirmIcons 0)");
            if (curXy != wantXy)
                changes.Add(xySwapped ? "- swap the X/Y (square / triangle) button icons in game (swapXYIcons 1)"
                                      : "- set the X/Y (square / triangle) button icons back to normal (swapXYIcons 0)");

            if (changes.Count == 0) return;   // DSfix.ini already matches the mapping: nothing to ask

            var answer = MessageBox.Show(
                "Controller settings saved.\n\n" +
                "To match your button mapping, the in-game button icons can be updated in Miscellaneous tab:\n\n" +
                string.Join("\n", changes) +
                "\n\nDo you want to update DSfix.ini now?",
                "In-game button icons", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (answer == MessageBoxResult.Yes)
                SyncPromptIconsWithMapping();
        }

        // Reads "key value" lines from DSfix.ini (only active, not commented lines)
        private Dictionary<string, string> ReadDsfixValues(params string[] keys)
        {
            var result = new Dictionary<string, string>();
            foreach (string line in File.ReadAllLines(dsfixPath))
            {
                string t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;

                string[] parts = t.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || !keys.Contains(parts[0]) || result.ContainsKey(parts[0])) continue;
                result[parts[0]] = parts.Length > 1 ? parts[1].Trim() : "";
            }
            return result;
        }

        // Writes swapConfirmIcons / swapXYIcons to DSfix.ini (called after the user said Yes)
        private void SyncPromptIconsWithMapping()
        {
            bool abSwapped = IsConfirmSwapped();
            bool xySwapped = IsXYSwapped();

            if (!File.Exists(dsfixPath))
            {
                MessageBox.Show("DSfix.ini was not found, so the in-game button icons (swapConfirmIcons / swapXYIcons) could not be updated.",
                                "DSfix.ini missing", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Dictionary<string, string> previous;
            try
            {
                // only these two keys are written; any other unsaved DSfix changes stay unsaved
                previous = UpdateDsfixValues(new Dictionary<string, string>
                {
                    ["swapConfirmIcons"] = abSwapped ? "1" : "0",
                    ["swapXYIcons"] = xySwapped ? "1" : "0"
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not update DSfix.ini:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // show the new values in the Miscellaneous tab and mark them as saved (no blue-violet)
            chkSwapConfirmIcons.IsChecked = abSwapped;
            chkSwapXYIcons.IsChecked = xySwapped;
            MarkControlSaved(chkSwapConfirmIcons);
            MarkControlSaved(chkSwapXYIcons);

            bool abWasOn = previous.TryGetValue("swapConfirmIcons", out string oldAb) && oldAb == "1";
            bool xyWasOn = previous.TryGetValue("swapXYIcons", out string oldXy) && oldXy == "1";

            var notes = new List<string>();
            if (abSwapped) notes.Add("- A/B (cross / circle) mapping is swapped, so the A/B button icons are now swapped in game too (swapConfirmIcons 1).");
            else if (abWasOn) notes.Add("- A/B mapping is standard again, so the A/B button icons are back to normal in game (swapConfirmIcons 0).");
            if (xySwapped) notes.Add("- X/Y (square / triangle) mapping is swapped, so the X/Y button icons are now swapped in game too (swapXYIcons 1).");
            else if (xyWasOn) notes.Add("- X/Y mapping is standard again, so the X/Y button icons are back to normal in game (swapXYIcons 0).");

            if (notes.Count > 0)
                MessageBox.Show("Controller settings saved.\n\n" + string.Join("\n", notes) + "\n\nDSfix.ini was updated (Miscellaneous tab > Controller Button Prompt Swap).",
                                "In-game button icons", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Writes "key value" lines into DSfix.ini (adds missing keys at the end).
        // Returns the values that were there before.
        private Dictionary<string, string> UpdateDsfixValues(Dictionary<string, string> newValues)
        {
            var previous = new Dictionary<string, string>();
            var lines = new List<string>(File.ReadAllLines(dsfixPath));
            var done = new HashSet<string>();

            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;

                string[] parts = t.Split(new[] { ' ', '\t' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 0 || !newValues.ContainsKey(parts[0])) continue;

                string key = parts[0];
                if (!previous.ContainsKey(key)) previous[key] = parts.Length > 1 ? parts[1].Trim() : "";
                lines[i] = $"{key} {newValues[key]}";
                done.Add(key);
            }
            foreach (var kv in newValues)
                if (!done.Contains(kv.Key)) lines.Add($"{kv.Key} {kv.Value}");

            File.WriteAllLines(dsfixPath, lines);
            return previous;
        }

        // Records a control's current value as the saved value and resets its colour
        private void MarkControlSaved(CheckBox chk)
        {
            originalControlValues[chk] = chk.IsChecked.ToString();
            if (originalControlColors.TryGetValue(chk, out var brush)) chk.Foreground = brush;
        }

        // ------------------------------------------------------------------
        //  Profiles
        // ------------------------------------------------------------------
        private void BtnSaveControllerProfile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save DDTM Controller profile",
                Filter = ProfileFilter,
                DefaultExt = ".ini",
                AddExtension = true,
                OverwritePrompt = true,
                FileName = "DDTM Controller Profile.ini"
            };
            if (dialog.ShowDialog(this) != true) return;

            if (string.Equals(System.IO.Path.GetFullPath(dialog.FileName), System.IO.Path.GetFullPath(controllerIniPath), StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("That is the controller settings file the game uses.\nUse \"SAVE Controller settings\" to write it, or choose another name for the profile.",
                                "Save Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // use the current .ini as template so the profile keeps all the explanations
            string[] template = File.Exists(controllerIniPath) ? File.ReadAllLines(controllerIniPath) : Array.Empty<string>();
            List<string> lines = BuildControllerIniLines(template);
            if (lines == null) return;   // invalid value, user already warned

            lines.InsertRange(0, new[]
            {
                "# DDTM Controller profile",
                $"# Saved by DDTM DSFix UI on {DateTime.Now:yyyy-MM-dd HH:mm}",
                "# Load it with the \"Load Profile\" button in the DDTM Controller tab.",
                ""
            });

            try
            {
                File.WriteAllLines(dialog.FileName, lines);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not save the profile:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            MessageBox.Show("Profile saved:\n" + dialog.FileName, "Save Profile", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnLoadControllerProfile_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Load DDTM Controller profile",
                Filter = ProfileFilter,
                CheckFileExists = true,
                Multiselect = false
            };
            if (dialog.ShowDialog(this) != true) return;

            Dictionary<string, string> kv;
            List<string> back;
            Dictionary<string, string> advOff;
            Dictionary<string, string> kb, mouse;
            bool hasKeyboard;
            try
            {
                ReadControllerFile(dialog.FileName, out kv, out back, out advOff);
                ReadKbmFile(dialog.FileName, out kb, out mouse, out hasKeyboard);   // KEYBOARD & MOUSE sub tab
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not read the profile:\n" + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // a valid profile must contain at least one known controller, keyboard or mouse key
            bool knownController = back.Count > 0 || kv.Keys.Any(k => ControllerDefaults.ContainsKey(k));
            bool knownKbm = hasKeyboard || mouse.Count > 0;
            if (!knownController && !knownKbm)
            {
                MessageBox.Show("This file does not contain any DDTM Controller settings.", "Load Profile", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // a profile with only one part (an older profile has no keyboard / mouse) leaves the other part as it is now
            if (knownController) ApplyControllerValues(kv, back, advOff);
            if (knownKbm) ApplyKbmValues(kb, mouse, hasKeyboard);
            ValidateAllControls();   // changed values are shown in blue-violet

            string note = !knownKbm ? "\n\nThis profile has no keyboard and mouse settings, so the KEYBOARD & MOUSE tab was not changed."
                        : !knownController ? "\n\nThis profile has only keyboard and mouse settings, so the controller tabs were not changed."
                        : "";
            MessageBox.Show("Profile loaded." + note + "\n\nNothing has been written yet: press \"SAVE Controller settings\" to apply it to the game.",
                            "Load Profile", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
