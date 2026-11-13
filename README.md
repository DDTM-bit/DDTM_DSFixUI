# DDTM DSfix UI — Settings app for DSfix 3.0

A settings app for **[DSfix 3.0](https://github.com/DDTM-bit/DSfix-3.0---Dark-Souls-PTDE)** and **[DDTM Controller Fix](https://github.com/DDTM-bit/DDTMControllerFixPDTE)** on Dark Souls: Prepare to Die Edition.

Instead of scrolling through hundreds of lines in `.ini` files, you get tabs, checkboxes and drop-down lists. Every option is explained, changed values are highlighted, and nothing is written until you press Save.

---

## Compatibility

| | |
|---|---|
| **Game** | Dark Souls: Prepare to Die Edition (Steam). *Not* for Dark Souls Remastered. |
| **DSfix** | Made for **DSfix 3.0**. Many options don't exist in the original DSfix 2.4, so don't use it with 2.4. |
| **Controller tabs** | Need **DDTM Controller Fix** (they edit its `DDTMControllerMappings.ini`). Without it, the DSfix tabs still work. |
| **OS** | Windows 10 / 11, 64-bit. |
| **Requires** | Nothing else. The .exe is self-contained, so you don't need to install .NET. |

---

## What it edits

The app only reads and writes these settings files. It never touches the game itself or your save file.

| File | Where | Edited by |
|---|---|---|
| `DSfix.ini` | game `DATA` folder | DSfix tabs |
| `DSfixKeys.ini` | game `DATA` folder | *Keys - Toggles* tab |
| `DDTMControllerMappings.ini` | game `DATA` folder | *DDTM Controls* and *Keyboard & Mouse* tabs |
| `DarkSouls.ini` | `%LOCALAPPDATA%\NBGI\DarkSouls\` | Only if you answer **Yes** to the startup check (see below) |

---

## Installation

1. Download `DDTM_DSFixUI SETTINGS.exe` from the [latest release](https://github.com/DDTM-bit/DDTM_DSFixUI/releases/latest) (current version: [v2.7.0](https://github.com/DDTM-bit/DDTM_DSFixUI/releases/tag/v2.7.0)).
2. Put it in the game's **`DATA`** folder, next to `DARKSOULS.exe` and `DSfix.ini`:
   `...\Steam\steamapps\common\Dark Souls Prepare to Die Edition\DATA`
3. Run it.

**"Windows protected your PC"?** The app isn't signed with a paid certificate, so Windows SmartScreen doesn't recognize it. Click **More info → Run anyway**. The full source code is in this repository, so you can read it or build it yourself.

---

## How to use it

1. **Start the app.** It reads your current settings from the `.ini` files.
2. **Startup check.** If the game's own settings clash with DSfix (in-game anti-aliasing or motion blur on, resolution not matching your monitor, wrong fullscreen / borderless combination, refresh rate not set), the app lists the problems and offers to fix them in `DarkSouls.ini`. Nothing changes unless you click **Yes**.
3. **Change what you want.** Values you change are shown in a different colour, so you can see what you touched.
4. **Save.** Press **SAVE DSfix** for the DSfix tabs, or **SAVE Controls settings** for the controller and keyboard tabs.
5. **Play.** **LAUNCH GAME** starts `DARKSOULS.exe` from the same folder.

Not sure what to pick? Press **Apply Default Optimal Settings** for a tested, safe setup, then adjust from there.

### Tabs

| Tab | What's inside |
|---|---|
| **Graphics** | Rendering & present resolution, FPS unlock & limit, anti-aliasing (SMAA / FXAA), sharpening, camera distance, ambient occlusion (VSSAO, HBAO, SCAO, VSSAO2), depth of field, texture filtering |
| **HUD & UI** | Size and opacity of each HUD element (health / stamina, items, souls, boss bar), menu, bonfire list, prompts and subtitle scaling |
| **QoL Features** | Weapon durability bars, bonfire world map, no auto-equip of consumables, Estus quick select, use or offer several items at once, change covenant at bonfires |
| **Window & Mouse** | Cursor at startup and cursor capture, VSync, refresh rate, G-Sync / FreeSync |
| **Backups** | Save backups (interval, how many to keep, custom save folder), Quick Save / Quick Load keys |
| **Modding & Override** | Texture override, dumping and prefetch, shader dumping and override |
| **Miscellaneous** | Skip intro logos, game language, screenshot folder, `dinput8dllWrapper` (with a file picker), D3D adapter, log level, in-game button icons (auto / Xbox / PlayStation / Switch) |
| **Keys - Toggles** | Hotkeys for HUD, SMAA, SSAO, DoF, 30 FPS limit, cursor, borderless, screenshots and more |
| **DDTM Controls** | Button mapping, Japanese PS3 layout, presets (DS3_JUMP, ELDEN_RING, MORTAL_SHELL, RUN_AND_ATTACK), macros, stick & trigger deadzones, sensitivity, rumble |
| **Keyboard & Mouse** | Every keyboard / mouse action of DDTM Controller Fix, two-key combos, mouse and bow sensitivity |

### Extra buttons

* **RELOAD settings from .ini files** — throw away unsaved changes and read the files again.
* **SAVE / LOAD settings to a local file** — keep your DSfix setup in a separate file, for example before trying something new.
* **Save Profile / Load Profile** — the same for controller and keyboard settings. Profiles are small `.ini` files you can share with friends. Loading a profile doesn't write anything until you press **SAVE Controls settings**.
* **Load default settings / Load Keys Default Assignment** — reset a tab to its defaults.
* **dinput8.dll Wrapper → Browse** — pick `DDTMControllerFix.dll`. If it's outside the game folder, the app offers to copy it in.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| The app doesn't start | Right-click the .exe → *Properties* → tick **Unblock** if it's there, then try again. Check that your antivirus didn't quarantine it. |
| "DSfix.ini was not found" | The app must be in the same folder as `DSfix.ini` (the game's `DATA` folder). |
| "DDTMControllerMappings.ini was not found" | Install DDTM Controller Fix and start the game once; it creates the file. |
| "Missing DarkSouls.ini" | Start the game once so it creates its own settings file. |
| Changes have no effect | Press the **Save** button of that tab before starting the game. |

---

## Building from source

Requirements: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) on Windows.

```
dotnet publish "DDTM_DSFixUI SETTINGS.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

It's a WPF app with no extra packages. The command above builds the same single, self-contained .exe as the release.

---

## Credits

* **[DSfix](https://github.com/PeterTh/dsfix)** by Durante (Peter Thoman): some of the setting descriptions are taken from DSfix.ini.
* **[.NET](https://github.com/dotnet/runtime)** and **[WPF](https://github.com/dotnet/wpf)** by the .NET Foundation and contributors (MIT): built into the self-contained .exe. License text in `THIRD_PARTY_LICENSES.txt`.
* Made for [DSfix 3.0](https://github.com/DDTM-bit/DSfix-3.0---Dark-Souls-PTDE) and [DDTM Controller Fix](https://github.com/DDTM-bit/DDTMControllerFixPDTE).

### AI disclosure

All of the code and the app icon were generated by AI. DDTM decided what the app should do, directed the AI through prompts and tested the functionality.

Dark Souls is a trademark of FromSoftware / Bandai Namco. This project is not affiliated with or endorsed by them.

## License

Copyright (C) 2026 DDTM

This program is free software: you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation, either version 3 of the License, or (at your option) any later version.

This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the [LICENSE](LICENSE) file for details.
