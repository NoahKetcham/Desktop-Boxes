# Removing Gray Window Borders (Avalonia + Windows 11)

## Problem

A faint gray outline remained around `CommandCenterWindow` even after setting a transparent background.

In this case, there were two separate border sources:

1. **Avalonia/app border** (`Border` control in the window content)
2. **Windows DWM compositor border** (non-client frame drawn by Windows 11)

Removing only one of them was not enough.

## Final Fix Applied

### 1) Remove OS window decorations in AXAML

In `Boxes.App/Views/Widgets/CommandCenterWindow.axaml`:

- Set `SystemDecorations="None"`  
  (was `BorderOnly`)

This removes standard window chrome/border behavior from Avalonia's side.

### 2) Force content border to be invisible

In `Boxes.App/Views/Widgets/CommandCenterWindow.axaml.cs` inside `ApplyAllSettings(...)`:

- Set:
  - `_outerBorder.BorderBrush = Brushes.Transparent;`
  - `_outerBorder.BorderThickness = new Thickness(0);`

This guarantees the custom shell border is not drawn, regardless of settings.

### 3) Disable Windows 11 DWM border explicitly

In `Boxes.App/Extensions/WindowExtensions.cs`:

- Added `HideDwmBorder(this Window window)` that calls:
  - `DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, DWMWA_COLOR_NONE, ...)`
- `DWMWA_BORDER_COLOR = 34`
- `DWMWA_COLOR_NONE = 0xFFFFFFFE` (passed as `int`)

Then in `Boxes.App/Views/Widgets/CommandCenterWindow.axaml.cs` (`OnOpened`):

- Call `this.HideDwmBorder();`

This removes the compositor-drawn gray outline on Windows 11.

## How To Reproduce This Fix For Any New Window

For any floating/transparent Avalonia window that still shows a gray frame:

1. In that window's `.axaml`:
   - Use `SystemDecorations="None"`
   - Keep transparency settings (`TransparencyLevelHint="Transparent"`, etc.) as needed
2. In code-behind (`.axaml.cs`):
   - Ensure any outer `Border` is transparent or thickness `0`
3. In `OnOpened`:
   - Call `this.HideDwmBorder();`
4. Restart the app fully (not just hot reload)
5. Verify:
   - No faint edge at corners
   - No thin line along sides

## Notes / Caveats

- This DWM border fix is **Windows-only** and safely no-ops on non-Windows platforms.
- If a border still appears after all of the above, check whether resizing behavior or other native window flags are reintroducing non-client frame styling.
