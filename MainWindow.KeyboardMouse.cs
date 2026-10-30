#nullable enable
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DDTM_DSFixUI
{
    // "DDTM Controller" tab > "KEYBOARD & MOUSE" sub tab: the [Keyboard] and [Mouse] sections of
    // DDTMControllerMappings.ini. Kept apart from the controller sections: those have actions with the same
    // names (KICK, LEAP_ATTACK, DASH_SPRINT_ONLY), so every read / write here only looks inside [Keyboard] / [Mouse].
    //
    // How a key is assigned: click a box (it turns orange), then press a key, click a mouse button on the box or
    // turn the mouse wheel over the box. Clicking anywhere else cancels. The x button clears the action (NONE).
    // The names written are exactly the ones DDTMControllerFix.dll reads.
    public partial class MainWindow
    {
        // ------------------------------------------------------------------
        //  Actions: ini name, label, short help, combination of two allowed, default, group panel
        // ------------------------------------------------------------------
        private sealed class KbmAction
        {
            public string Key = "", Label = "", Help = "", Default = "NONE", Panel = "";
            public bool Combo;
            public TextBox Box1 = null!, Box2 = null!;
        }

        private static KbmAction KbmA(string panel, string key, string label, string help, bool combo, string def) =>
            new KbmAction { Panel = panel, Key = key, Label = label, Help = help, Combo = combo, Default = def };

        private readonly KbmAction[] KbmActions =
        {
            KbmA("panelKbm_Movement", "MOVE_FORWARD", "Move forward", "Like the left stick pushed fully forward.", false, "W"),
            KbmA("panelKbm_Movement", "MOVE_BACKWARD", "Move backward", "Like the left stick pushed fully backward.", false, "S"),
            KbmA("panelKbm_Movement", "MOVE_LEFT", "Move left", "Like the left stick pushed fully left.", false, "A"),
            KbmA("panelKbm_Movement", "MOVE_RIGHT", "Move right", "Like the left stick pushed fully right.", false, "D"),
            KbmA("panelKbm_Movement", "WALK", "Walk", "Hold together with a movement key to walk instead of run.", false, "LALT"),
            KbmA("panelKbm_Movement", "DASH_ROLL_BACKSTEP", "Dash / roll / backstep", "Tap to roll (backstep when standing), hold to sprint. It never jumps. In menus it goes back (B on a controller).", false, "SPACE"),
            KbmA("panelKbm_Movement", "JUMP", "Jump", "Jumps while moving, like the controller's JUMP_ONLY macro.", false, "F"),
            KbmA("panelKbm_Movement", "DASH_SPRINT_ONLY", "Sprint (toggle)", "Press once to sprint while moving. It stops when you stop moving, attack or open a menu. It never rolls or jumps.", false, "LSHIFT"),

            KbmA("panelKbm_Combat", "LIGHT_ATTACK", "Right weapon attack", "Light attack with the right hand (RB on a controller).", true, "MOUSE_LEFT"),
            KbmA("panelKbm_Combat", "HEAVY_ATTACK", "Right weapon heavy attack", "Heavy attack with the right hand (RT on a controller).", true, "MOUSE_RIGHT"),
            KbmA("panelKbm_Combat", "BLOCK", "Left weapon / block", "Block or left-hand attack (LB on a controller).", true, "LCTRL"),
            KbmA("panelKbm_Combat", "PARRY", "Parry / shield bash", "Left-hand heavy action (LT on a controller).", true, "Q"),
            KbmA("panelKbm_Combat", "KICK", "Kick", "Instant kick, no stick flick needed.", true, "NONE"),
            KbmA("panelKbm_Combat", "LEAP_ATTACK", "Leaping attack", "Instant jumping heavy attack, no stick flick needed.", true, "NONE"),

            KbmA("panelKbm_Equipment", "SWITCH_LEFT_WEAPON", "Switch left weapon", "D-pad left on a controller.", true, "1"),
            KbmA("panelKbm_Equipment", "SWITCH_RIGHT_WEAPON", "Switch right weapon", "D-pad right on a controller.", true, "2"),
            KbmA("panelKbm_Equipment", "SWITCH_ITEM", "Switch item", "D-pad down on a controller.", true, "3"),
            KbmA("panelKbm_Equipment", "SWITCH_MAGIC", "Switch magic", "D-pad up on a controller.", true, "4"),

            KbmA("panelKbm_Actions", "INTERACT", "Interact / confirm", "Talk, open, pick up, confirm in menus (A on a controller).", false, "E"),
            KbmA("panelKbm_Actions", "USE_ITEM", "Use item", "Uses the selected item (X on a controller).", false, "R"),
            KbmA("panelKbm_Actions", "TWO_HAND", "Two-hand", "Toggles two-handing the weapon (Y on a controller).", false, "T"),
            KbmA("panelKbm_Actions", "MENU", "Menu", "Opens the menu (START on a controller).", false, "TAB"),
            KbmA("panelKbm_Actions", "GESTURES", "Gestures", "Opens the gesture menu (BACK on a controller).", false, "G"),

            KbmA("panelKbm_Target", "LOCK_ON", "Lock on", "Locks on to a target, or resets the camera (right stick click).", false, "MOUSE_MIDDLE"),
            KbmA("panelKbm_Target", "NEXT_TARGET", "Next target", "While locked on: switch to the next target. A fast mouse flick does the same.", false, "MOUSE_WHEEL_DOWN"),
            KbmA("panelKbm_Target", "PREVIOUS_TARGET", "Previous target", "While locked on: switch to the previous target.", false, "MOUSE_WHEEL_UP"),

            KbmA("panelKbm_Menus", "MENU_LEFT", "Menu tab left", "Previous tab (LB). Only in the quick menu and the menus opened from it; there the key does nothing else.", false, "COMMA"),
            KbmA("panelKbm_Menus", "MENU_RIGHT", "Menu tab right", "Next tab (RB). Only in the quick menu and the menus opened from it; there the key does nothing else.", false, "PERIOD"),
        };

        private static readonly string[] KbmSliderKeys = { "Sensitivity_X", "Sensitivity_Y", "Bow_Sensitivity_X", "Bow_Sensitivity_Y" };
        private static readonly string[] KbmBoolKeys = { "Invert_X", "Invert_Y" };
        private static readonly Dictionary<string, string> KbmMouseDefaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sensitivity_X"] = "1.0", ["Sensitivity_Y"] = "1.0", ["Bow_Sensitivity_X"] = "0.5", ["Bow_Sensitivity_Y"] = "0.5",
            ["Invert_X"] = "FALSE", ["Invert_Y"] = "FALSE",
        };

        // ------------------------------------------------------------------
        //  Key names: exactly the list the dll understands (see the help text in the .ini)
        // ------------------------------------------------------------------
        private static readonly Dictionary<Key, string> KbmKeyNames = BuildKbmKeyNames();

        private static Dictionary<Key, string> BuildKbmKeyNames()
        {
            var d = new Dictionary<Key, string>();
            for (Key k = Key.A; k <= Key.Z; k++) d[k] = k.ToString();
            for (int i = 0; i <= 9; i++) { d[Key.D0 + i] = i.ToString(CultureInfo.InvariantCulture); d[Key.NumPad0 + i] = "NUMPAD" + i; }
            for (int i = 0; i < 24; i++) d[Key.F1 + i] = "F" + (i + 1);
            d[Key.Multiply] = "NUMPAD_MULTIPLY"; d[Key.Add] = "NUMPAD_ADD"; d[Key.Subtract] = "NUMPAD_SUBTRACT";
            d[Key.Divide] = "NUMPAD_DIVIDE"; d[Key.Decimal] = "NUMPAD_DECIMAL";
            d[Key.Space] = "SPACE"; d[Key.Tab] = "TAB"; d[Key.Enter] = "ENTER"; d[Key.Escape] = "ESC"; d[Key.Back] = "BACKSPACE";
            d[Key.CapsLock] = "CAPSLOCK";
            d[Key.LeftShift] = "LSHIFT"; d[Key.RightShift] = "RSHIFT"; d[Key.LeftCtrl] = "LCTRL"; d[Key.RightCtrl] = "RCTRL";
            d[Key.LeftAlt] = "LALT"; d[Key.RightAlt] = "RALT";
            d[Key.Up] = "UP"; d[Key.Down] = "DOWN"; d[Key.Left] = "LEFT"; d[Key.Right] = "RIGHT";
            d[Key.Insert] = "INSERT"; d[Key.Delete] = "DELETE"; d[Key.Home] = "HOME"; d[Key.End] = "END";
            d[Key.PageUp] = "PAGEUP"; d[Key.PageDown] = "PAGEDOWN";
            d[Key.OemMinus] = "MINUS"; d[Key.OemPlus] = "EQUALS"; d[Key.OemOpenBrackets] = "LBRACKET"; d[Key.OemCloseBrackets] = "RBRACKET";
            d[Key.OemPipe] = "BACKSLASH"; d[Key.OemSemicolon] = "SEMICOLON"; d[Key.OemQuotes] = "APOSTROPHE"; d[Key.OemTilde] = "GRAVE";
            d[Key.OemComma] = "COMMA"; d[Key.OemPeriod] = "PERIOD"; d[Key.OemQuestion] = "SLASH";
            d[Key.NumLock] = "NUMLOCK"; d[Key.Scroll] = "SCROLLLOCK"; d[Key.Pause] = "PAUSE";
            return d;
        }

        // ------------------------------------------------------------------
        //  Building the rows (called from InitControllerTab, before the change tracking is set up)
        // ------------------------------------------------------------------
        private static readonly Brush KbmWaitBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xB3, 0x47));
        private static readonly Brush KbmBoxBrush = Brushes.White;
        private const string KbmWaitText = "press a key...";

        private void InitKbmTab()
        {
            foreach (KbmAction act in KbmActions)
            {
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2), ToolTip = act.Help + "\nIn DDTMControllerMappings.ini: " + act.Key };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(230) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(130) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(34) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var name = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                name.Children.Add(new TextBlock { Text = act.Label, Foreground = new SolidColorBrush(Color.FromRgb(0xD3, 0xD3, 0xD3)), FontWeight = FontWeights.SemiBold, FontSize = 13 });
                name.Children.Add(new TextBlock { Text = act.Key, Foreground = new SolidColorBrush(Color.FromRgb(0x80, 0x80, 0x80)), FontSize = 10 });
                row.Children.Add(name);

                act.Box1 = MakeKeyBox();
                Grid.SetColumn(act.Box1, 1);
                row.Children.Add(act.Box1);

                act.Box2 = MakeKeyBox();
                Grid.SetColumn(act.Box2, 3);
                if (act.Combo)
                {
                    var plus = new TextBlock { Text = "+", Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x8C, 0x00)), FontWeight = FontWeights.Bold, FontSize = 16, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                    Grid.SetColumn(plus, 2);
                    row.Children.Add(plus);
                    row.Children.Add(act.Box2);
                }

                var clear = new Button { Content = "✕", Padding = new Thickness(0), Margin = new Thickness(6, 2, 2, 2), Width = 24, Height = 24, ToolTip = "Clear: no key for this action (NONE)." };
                KbmAction captured = act;
                clear.Click += (s, e) =>
                {
                    CancelKeyCapture();
                    captured.Box1.Text = "";
                    captured.Box2.Text = "";
                    ValidateControlColor(captured.Box1);
                    ValidateControlColor(captured.Box2);
                };
                Grid.SetColumn(clear, 4);
                row.Children.Add(clear);

                var help = new TextBlock { Text = act.Help, Foreground = new SolidColorBrush(Color.FromRgb(0xA8, 0xA8, 0xA8)), FontSize = 11, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(8, 0, 0, 0) };
                Grid.SetColumn(help, 5);
                row.Children.Add(help);

                Ctl<StackPanel>(act.Panel).Children.Add(row);
            }

            // a click anywhere outside the waiting box cancels the wait
            PreviewMouseDown += (s, e) =>
            {
                if (_kbmCapture != null && !IsInside(e.OriginalSource as DependencyObject, _kbmCapture)) CancelKeyCapture();
            };
        }

        private static bool IsInside(DependencyObject? d, DependencyObject parent)
        {
            while (d != null)
            {
                if (ReferenceEquals(d, parent)) return true;
                d = d is Visual || d is System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(d) : LogicalTreeHelper.GetParent(d);
            }
            return false;
        }

        private TextBox MakeKeyBox()
        {
            var tb = new TextBox
            {
                Height = 26,
                Margin = new Thickness(0, 1, 0, 1),
                IsReadOnly = true,
                IsReadOnlyCaretVisible = false,
                Cursor = Cursors.Hand,
                FontWeight = FontWeights.Bold,
                FontSize = 13,
                HorizontalContentAlignment = HorizontalAlignment.Center,
                VerticalContentAlignment = VerticalAlignment.Center,
                Background = KbmBoxBrush,
                ContextMenu = null,
            };
            tb.PreviewMouseDown += KeyBox_PreviewMouseDown;
            tb.PreviewMouseWheel += KeyBox_PreviewMouseWheel;
            tb.PreviewKeyDown += KeyBox_PreviewKeyDown;
            tb.LostKeyboardFocus += (s, e) => { if (ReferenceEquals(_kbmCapture, s)) CancelKeyCapture(); };
            tb.ContextMenuOpening += (s, e) => e.Handled = true;
            return tb;
        }

        // ------------------------------------------------------------------
        //  Key capture
        // ------------------------------------------------------------------
        private TextBox? _kbmCapture;
        private string _kbmCaptureOld = "";

        private void StartKeyCapture(TextBox tb)
        {
            CancelKeyCapture();
            _kbmCapture = tb;
            _kbmCaptureOld = tb.Text;
            tb.Text = KbmWaitText;
            tb.Background = KbmWaitBrush;
            tb.Focus();
            Keyboard.Focus(tb);
        }

        private void CancelKeyCapture()
        {
            TextBox? tb = _kbmCapture;
            if (tb == null) return;
            _kbmCapture = null;
            tb.Text = _kbmCaptureOld;
            tb.Background = KbmBoxBrush;
            ValidateControlColor(tb);
        }

        private void FinishKeyCapture(string name)
        {
            TextBox? tb = _kbmCapture;
            if (tb == null) return;
            _kbmCapture = null;
            tb.Text = name;
            tb.Background = KbmBoxBrush;
            ValidateControlColor(tb);   // blue-violet when it differs from the saved .ini
        }

        private void KeyBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            var tb = (TextBox)sender;
            e.Handled = true;
            if (!ReferenceEquals(_kbmCapture, tb)) { StartKeyCapture(tb); return; }   // this click only starts waiting

            string? name = e.ChangedButton switch
            {
                MouseButton.Left => "MOUSE_LEFT",
                MouseButton.Right => "MOUSE_RIGHT",
                MouseButton.Middle => "MOUSE_MIDDLE",
                MouseButton.XButton1 => "MOUSE_4",
                MouseButton.XButton2 => "MOUSE_5",
                _ => null
            };
            if (name != null) FinishKeyCapture(name);
        }

        private void KeyBox_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            e.Handled = true;
            if (!ReferenceEquals(_kbmCapture, sender))
            {
                // not waiting: scroll the page (the box itself would swallow the wheel)
                var sv = Ctl<ScrollViewer>("svKbm");
                sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
                return;
            }
            FinishKeyCapture(e.Delta > 0 ? "MOUSE_WHEEL_UP" : "MOUSE_WHEEL_DOWN");
        }

        private void KeyBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!ReferenceEquals(_kbmCapture, sender)) return;   // not waiting: Tab etc. work as usual
            e.Handled = true;                                    // while waiting, every key is taken (also Tab, Alt, Esc)
            Key k = e.Key == Key.System ? e.SystemKey : e.Key == Key.ImeProcessed ? e.ImeProcessedKey : e.Key;
            if (KbmKeyNames.TryGetValue(k, out string? name)) FinishKeyCapture(name);
            else ((TextBox)sender).Text = "key not supported";   // keeps waiting for another key
        }

        // ------------------------------------------------------------------
        //  Read
        // ------------------------------------------------------------------
        private static bool IsKbmSection(string sectionLine) =>
            sectionLine.Equals("[Keyboard]", StringComparison.OrdinalIgnoreCase) || sectionLine.Equals("[Mouse]", StringComparison.OrdinalIgnoreCase);

        // Reads the [Keyboard] and [Mouse] sections. hasKeyboard = the file has a [Keyboard] section (without one,
        // the dll uses its defaults; with one, a missing action has no key, exactly like the dll).
        // An action written on several lines (two different keys): the first line is shown here, the other lines
        // are kept in the file when saving.
        private static void ReadKbmFile(string path, out Dictionary<string, string> kb, out Dictionary<string, string> mouse, out bool hasKeyboard)
        {
            kb = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            mouse = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            hasKeyboard = false;
            string section = "";
            foreach (string line in File.ReadAllLines(path))
            {
                string t = line.Trim();
                if (t.Length == 0 || t.StartsWith("#")) continue;
                if (t.StartsWith("["))
                {
                    section = t;
                    if (t.Equals("[Keyboard]", StringComparison.OrdinalIgnoreCase)) hasKeyboard = true;
                    continue;
                }
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                string key = t.Substring(0, eq).Trim();
                string val = t.Substring(eq + 1).Trim();
                if (section.Equals("[Keyboard]", StringComparison.OrdinalIgnoreCase)) { if (!kb.ContainsKey(key)) kb[key] = val; }
                else if (section.Equals("[Mouse]", StringComparison.OrdinalIgnoreCase)) { if (!mouse.ContainsKey(key)) mouse[key] = val; }
            }
        }

        // Puts values into the sub tab (used by Load, Load defaults and Load Profile)
        private void ApplyKbmValues(Dictionary<string, string> kb, Dictionary<string, string> mouse, bool hasKeyboard)
        {
            CancelKeyCapture();
            foreach (KbmAction act in KbmActions)
            {
                string v = kb.TryGetValue(act.Key, out string? val) ? val : (hasKeyboard ? "NONE" : act.Default);
                string[] parts = v.Split('+').Select(p => p.Trim()).Where(p => p.Length > 0 && !p.Equals("NONE", StringComparison.OrdinalIgnoreCase)).ToArray();
                act.Box1.Text = parts.Length > 0 ? parts[0].ToUpperInvariant() : "";
                act.Box2.Text = act.Combo && parts.Length > 1 ? parts[1].ToUpperInvariant() : "";
            }

            foreach (string k in KbmSliderKeys)
            {
                var sld = Ctl<Slider>("sldKbm_" + k);
                string s = mouse.TryGetValue(k, out string? mv) ? mv : KbmMouseDefaults[k];
                if (!double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) || d <= 0)
                    d = double.Parse(KbmMouseDefaults[k], CultureInfo.InvariantCulture);
                sld.Value = Math.Max(sld.Minimum, Math.Min(sld.Maximum, d));
            }
            foreach (string k in KbmBoolKeys)
            {
                string s = mouse.TryGetValue(k, out string? mv) ? mv : KbmMouseDefaults[k];
                Ctl<CheckBox>("chkKbm_" + k).IsChecked = s.Equals("TRUE", StringComparison.OrdinalIgnoreCase) || s == "1"
                                                         || s.Equals("YES", StringComparison.OrdinalIgnoreCase) || s.Equals("ON", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void ApplyKbmDefaults() =>
            ApplyKbmValues(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), false);

        // ------------------------------------------------------------------
        //  Write: updates the [Keyboard] / [Mouse] lines in 'lines' (the controller lines are already done)
        // ------------------------------------------------------------------
        private string KbmValue(KbmAction act)
        {
            // a name typed by hand in the .ini that the dll does not know is kept as it is (the dll ignores it)
            string a = act.Box1.Text.Trim(), b = act.Combo ? act.Box2.Text.Trim() : "";
            if (a.Length == 0) { a = b; b = ""; }
            if (b.Equals(a, StringComparison.OrdinalIgnoreCase)) b = "";
            if (a.Length == 0) return "NONE";
            return b.Length == 0 ? a : a + " + " + b;
        }

        private void ApplyKbmLines(List<string> lines)
        {
            CancelKeyCapture();
            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (KbmAction act in KbmActions) values[act.Key] = KbmValue(act);
            var mouseValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string k in KbmSliderKeys)
                mouseValues[k] = Math.Round(Ctl<Slider>("sldKbm_" + k).Value, 2).ToString("0.0#", CultureInfo.InvariantCulture);
            foreach (string k in KbmBoolKeys)
                mouseValues[k] = Ctl<CheckBox>("chkKbm_" + k).IsChecked == true ? "TRUE" : "FALSE";

            // No [Keyboard] section yet (file from an older dll): add the same section the dll writes, with its help text.
            if (!lines.Any(l => l.Trim().Equals("[Keyboard]", StringComparison.OrdinalIgnoreCase)))
                lines.AddRange(KbmDefaultSection.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'));
            if (!lines.Any(l => l.Trim().Equals("[Mouse]", StringComparison.OrdinalIgnoreCase)))
                lines.Add("[Mouse]");

            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string section = "";
            for (int i = 0; i < lines.Count; i++)
            {
                string t = lines[i].Trim();
                if (t.StartsWith("[")) { section = t; continue; }
                if (!IsKbmSection(section) || t.Length == 0 || t.StartsWith("#")) continue;
                int eq = t.IndexOf('=');
                if (eq <= 0) continue;
                string key = t.Substring(0, eq).Trim();
                bool kbSection = section.Equals("[Keyboard]", StringComparison.OrdinalIgnoreCase);
                var dict = kbSection ? values : mouseValues;
                if (!dict.TryGetValue(key, out string? v)) continue;           // unknown line: kept as it is
                string tag = (kbSection ? "K:" : "M:") + key;
                if (written.Contains(tag)) continue;                           // a second key for the same action: kept
                string realKey = kbSection ? KbmActions.First(a => a.Key.Equals(key, StringComparison.OrdinalIgnoreCase)).Key
                                           : KbmSliderKeys.Concat(KbmBoolKeys).First(m => m.Equals(key, StringComparison.OrdinalIgnoreCase));
                lines[i] = realKey + " = " + v;
                written.Add(tag);
            }

            foreach (KbmAction act in KbmActions)
                if (!written.Contains("K:" + act.Key)) InsertIntoSection(lines, "Keyboard", act.Key + " = " + values[act.Key]);
            foreach (string k in KbmSliderKeys.Concat(KbmBoolKeys))
                if (!written.Contains("M:" + k)) InsertIntoSection(lines, "Mouse", k + " = " + mouseValues[k]);
        }

        // The section DDTMControllerFix.dll adds to the .ini (same text), used when the file has none yet.
        private const string KbmDefaultSection =
@"

# ===========================================================================
#  5. KEYBOARD AND MOUSE
# ===========================================================================
# Keyboard and mouse always work, together with the controller.
# This section is completely separate from the controller sections above:
# presets, macros and controller settings never change it, and it never
# changes them.
#
# The game's own keyboard and mouse settings are ignored, only these are used.
# The camera is moved with the mouse only, never with keys.
#
# Format:  ACTION = KEY
#     or:  ACTION = KEY + KEY        (a combination of two, where allowed)
# NONE leaves an action without a key. You can write the same action on two
# lines to give it two different keys.
# When a combination is held (for example LSHIFT + MOUSE_LEFT), only the
# combination's action happens, not the action of MOUSE_LEFT alone.
#
# Keys you can use after the '=':
#   Letters      A to Z
#   Numbers      0 to 9 (the row above the letters)
#   F keys       F1 to F24
#   Numpad       NUMPAD0 to NUMPAD9, NUMPAD_ADD, NUMPAD_SUBTRACT,
#                NUMPAD_MULTIPLY, NUMPAD_DIVIDE, NUMPAD_DECIMAL
#   Modifiers    LSHIFT, RSHIFT, LCTRL, RCTRL, LALT, RALT
#   Others       SPACE, TAB, ENTER, ESC, BACKSPACE, CAPSLOCK,
#                UP, DOWN, LEFT, RIGHT, INSERT, DELETE, HOME, END,
#                PAGEUP, PAGEDOWN, MINUS, EQUALS, LBRACKET, RBRACKET,
#                BACKSLASH, SEMICOLON, APOSTROPHE, GRAVE, COMMA, PERIOD,
#                SLASH, NUMLOCK, SCROLLLOCK, PAUSE
#   Mouse        MOUSE_LEFT, MOUSE_RIGHT, MOUSE_MIDDLE, MOUSE_4, MOUSE_5,
#                MOUSE_WHEEL_UP, MOUSE_WHEEL_DOWN
#
# In menus: only INTERACT, DASH_ROLL_BACKSTEP, USE_ITEM, TWO_HAND, MENU,
# GESTURES, MENU_LEFT and MENU_RIGHT work, and the mouse works as in the
# normal game. These keys always work in menus, whatever is set here:
#   ENTER = confirm, BACKSPACE = back, ARROW KEYS = move through the items,
#   ESC = close the menu (like START).
# With DSfix: if you turn the HUD off in the game options, also set
# enableHudMod 0 in DSfix.ini, or mouse clicks land in the wrong place.
# With a bow or crossbow two-handed, and while aiming: MOUSE_LEFT shoots and
# MOUSE_RIGHT changes the arrow type, whatever is written here. While aiming,
# the mouse wheel zooms.
[Keyboard]

# One key only:
MOVE_FORWARD = W
MOVE_BACKWARD = S
MOVE_LEFT = A
MOVE_RIGHT = D
# Hold together with a movement key to walk.
WALK = LALT
# Roll, backstep, hold to sprint. It never jumps.
DASH_ROLL_BACKSTEP = SPACE
# Jumps while moving.
JUMP = F
# Press once to sprint while moving. It stops when you stop moving, attack
# or open a menu. It never rolls or jumps.
DASH_SPRINT_ONLY = LSHIFT

# One key or a combination of two:
SWITCH_LEFT_WEAPON = 1
SWITCH_RIGHT_WEAPON = 2
SWITCH_ITEM = 3
SWITCH_MAGIC = 4
LIGHT_ATTACK = MOUSE_LEFT
HEAVY_ATTACK = MOUSE_RIGHT
BLOCK = LCTRL
PARRY = Q
# Instant kick and instant jumping heavy attack (no flick needed),
# for example KICK = X  or  KICK = LALT + MOUSE_LEFT
KICK = NONE
LEAP_ATTACK = NONE

# One key only:
INTERACT = E
USE_ITEM = R
TWO_HAND = T
MENU = TAB
GESTURES = G
LOCK_ON = MOUSE_MIDDLE
NEXT_TARGET = MOUSE_WHEEL_DOWN
PREVIOUS_TARGET = MOUSE_WHEEL_UP

# Only in the quick menu and the menus opened from it (inventory,
# equipment, status...): change the tab, like LB / RB on the controller.
# There these keys do nothing else; in the game they keep their normal job.
MENU_LEFT = Q
MENU_RIGHT = E

[Mouse]
# 1.0 = normal. 2.0 = twice as fast. 0.5 = half speed.
Sensitivity_X = 1.0
Sensitivity_Y = 1.0
# Used while aiming with a bow or crossbow.
Bow_Sensitivity_X = 1.0
Bow_Sensitivity_Y = 1.0
# TRUE turns the direction around.
Invert_X = FALSE
Invert_Y = FALSE
";
    }
}
