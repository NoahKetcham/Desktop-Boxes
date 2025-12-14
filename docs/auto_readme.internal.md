# Project Auto-README (Internal)

## Overview
- Avalonia desktop app that groups files and shortcuts into movable “boxes” on the desktop, with optional taskbar-snapped variants; state persists to JSON under `%APPDATA%/Boxes/`.

## Tech Stack
- .NET + Avalonia UI 11.x; MVVM with CommunityToolkit.Mvvm.
- JSON persistence via `SettingsService`, `BoxService`, and related services.
- Windows-specific integrations for shell shortcuts, taskbar behavior, and startup.

## Architecture & Key Modules
- `Views/DesktopBoxWindow.axaml(.cs)`: Floating box window UI; now uses dynamic resources for icon sizing/spacing and applies settings-driven appearance.
- `ViewModels/DesktopBoxWindowViewModel`: Navigation, drag/drop import, ordering, and state sync for a box window.
- `Views/DashboardPageView.axaml` + `ViewModels/DashboardPageViewModel`: Dashboard “Templates” picker UI (expand/collapse section), template selection commands, and template info navigation.
- `Views/DashboardBoxSettingsView.axaml` + `ViewModels/DashboardBoxSettingsViewModel`: Right sidebar now supports a Template Info mode that temporarily replaces Box Settings.
- `Services/SettingsService`: Reads/writes `settings.json`, caches settings, raises `SettingsChanged`.
- `Services/DesktopIconMetricsService`: Retrieves system desktop icon metrics as defaults.
- `Models/DesktopBox`, `ScannedFile`, `ApplicationSettings`: Core persisted state for boxes, scanned items, and user preferences.

## Data Models & Contracts
- `DesktopBox`: Box identity, geometry, snap/collapse state, item order map, shortcut IDs, and placeholder `TemplateKey` for future window templates.
- `ScannedFile`: Shortcut/file/folder metadata (type, parent/child, archive info).
- `ApplicationSettings`: UI preferences (transparency, header, corner radius, icon size, label visibility, colors).

## APIs, Commands & Workflows
- Settings: `SettingsService.GetAsync/SaveAsync` with `SettingsChanged` event; `SettingsPageViewModel` auto-saves customization tweaks.
- Desktop box UI flow: window initializes defaults from `DesktopIconMetricsService`, then `ApplyAllSettings` pushes user settings (colors, opacity, icon sizing) via resource updates.
- Shortcut interactions: `DesktopBoxWindowViewModel` handles `EnterFolder/NavigateUp`, launch, reorder via `ReorderCurrentItemsAsync`, and drag/drop import through `HandleDropAsync`.
- Dashboard template flow: `ToggleTemplatesForBoxCommand` expands a row; `SelectTemplateForSelectedBoxCommand` persists `DesktopBox.TemplateKey`; `ShowTemplateInfoCommand` swaps the right sidebar into Template Info mode.

## Important Decisions & Rationale
- 2025-12-14: For the Dashboard row expander animation inside a virtualized `ListBox`, the final implementation animates `MaxHeight` + `Opacity` rather than measuring content height at runtime. This avoids virtualization-related layout timing issues and makes the expander reliably clickable.
- 2025-12-10: Icon sizing for desktop boxes now bound to window resources (`IconSize`, `IconItemWidth`, `IconItemHeight`, `IconLabelMaxWidth`, `ShowShortcutLabels`) via `UpdateIconResources`; removed layout-time overrides so user `BoxIconSize`/label settings persist across refreshes while still defaulting to system metrics on first render.
- Existing architecture retains `AppServices` as a simple service locator for UI-first velocity despite tighter coupling.

## Changelog (Most Recent First)
- 2025-12-14 – Added Dashboard template picker UI + template info sidebar mode, persisted `TemplateKey`, and documented expander animation patterns (DashboardPageView.axaml, DashboardPageViewModel.cs, DashboardBoxSettingsView.axaml, DashboardBoxSettingsViewModel.cs, DesktopBox.cs, BoxService.cs, docs/thatSauce/TemplateExpanderAnimation.md).
- 2025-12-10 – Bound desktop icon sizing to settings resources and removed layout overrides (DesktopBoxWindow.axaml, DesktopBoxWindow.axaml.cs).

