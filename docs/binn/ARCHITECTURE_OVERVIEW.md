# Desktop Boxes - Architecture Overview

## High-Level Description

**Desktop Boxes** is an Avalonia UI desktop application for Windows that helps users organize their desktop by grouping files and shortcuts into customizable "boxes" - floating windows that sit on the desktop and provide quick access to curated collections of files.

## Technology Stack

| Layer | Technology |
|-------|------------|
| **UI Framework** | Avalonia UI 11.x (cross-platform .NET UI) |
| **Architecture** | MVVM (Model-View-ViewModel) |
| **State Management** | CommunityToolkit.Mvvm |
| **Persistence** | JSON files in `%APPDATA%/Boxes/` |
| **Platform** | Windows (with Windows-specific integrations) |

## Project Structure

```
Boxes.App/
├── App.axaml(.cs)          # Application entry, theme configuration
├── Program.cs              # Entry point, command-line handling
├── ViewLocator.cs          # Convention-based view resolution
│
├── Models/                 # Data models (POCO)
│   ├── DesktopBox.cs       # Box configuration (position, shortcuts, etc.)
│   ├── ScannedFile.cs      # Discovered desktop file metadata
│   └── DesktopBuild.cs     # Saved desktop configuration snapshot
│
├── Services/               # Business logic layer
│   ├── AppServices.cs      # Service locator / DI container
│   ├── BoxService.cs       # Box CRUD operations
│   ├── BoxWindowManager.cs # Window lifecycle management
│   ├── ScannedFileService.cs     # Desktop file scanning
│   ├── DesktopCleanupService.cs  # Desktop archiving
│   ├── SettingsService.cs        # App configuration
│   ├── DialogService.cs          # Modal dialog management
│   ├── WindowStateService.cs     # Window position persistence
│   └── ...
│
├── ViewModels/             # MVVM ViewModels
│   ├── MainWindowViewModel.cs
│   ├── DashboardPageViewModel.cs
│   ├── DesktopBoxWindowViewModel.cs
│   └── ...
│
├── Views/                  # XAML views
│   ├── MainWindow.axaml    # Main application window
│   ├── DashboardPageView.axaml
│   ├── DesktopBoxWindow.axaml    # Floating box window
│   ├── TaskbarBoxWindow.axaml    # Taskbar-snapped variant
│   └── Dialogs/            # Modal dialogs
│
├── Styles/                 # Design system
│   ├── Tokens.axaml        # Color, spacing, shadow tokens
│   ├── Typography.axaml    # Text styles
│   ├── Buttons.axaml       # Button variants
│   ├── Cards.axaml         # Card components
│   ├── Badges.axaml        # Pill badges
│   └── ScrollBar.axaml     # Custom scrollbar
│
├── Converters/             # XAML value converters
└── Extensions/             # Extension methods
```

## Core Concepts

### 1. DesktopBox
A "box" represents a floating container for shortcuts. Each box has:
- **Identity**: Unique GUID, name, description
- **Geometry**: Width, height, position (X, Y)
- **Content**: List of shortcut IDs pointing to `ScannedFile` entries
- **State**: Taskbar-snapped mode, collapsed state, expanded height

### 2. ScannedFile
Represents a file discovered on the desktop:
- **ItemType**: File, Folder, or Shortcut
- **Hierarchy**: Parent/child relationships for folder nesting
- **Archival**: Tracks whether file was moved during "Clean Desktop"

### 3. Window Modes
Boxes can appear in two modes:
- **Normal (DesktopBoxWindow)**: Free-floating, resizable window
- **Taskbar-snapped (TaskbarBoxWindow)**: Pinned to taskbar edge, collapsible

## Data Flow

```
┌─────────────────────────────────────────────────────────────────────────┐
│                              USER ACTIONS                                │
│  Create Box │ Scan Desktop │ Drag & Drop │ Clean/Restore │ Settings    │
└──────────────┬──────────────┬─────────────┬───────────────┬─────────────┘
               │              │             │               │
               ▼              ▼             ▼               ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                            VIEW MODELS                                   │
│  DashboardPageVM │ DesktopBoxWindowVM │ SettingsPageVM │ DialogVMs     │
└──────────────────────────────────────────────────────────────────────────┘
               │              │             │               │
               ▼              ▼             ▼               ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                             SERVICES                                     │
│  BoxService │ ScannedFileService │ BoxWindowManager │ SettingsService   │
└──────────────────────────────────────────────────────────────────────────┘
               │              │             │               │
               ▼              ▼             ▼               ▼
┌─────────────────────────────────────────────────────────────────────────┐
│                           PERSISTENCE                                    │
│  boxes.json │ scanned_files.json │ settings.json │ shortcuts.json       │
│                        %APPDATA%/Boxes/                                  │
└─────────────────────────────────────────────────────────────────────────┘
```

## Key User Flows

### 1. First-Time Setup
1. User launches app → `MainWindow` opens
2. Dashboard shows empty state
3. User clicks "Scan Desktop" → `ScannedFileService.ScanAndSaveAsync()`
4. User clicks "New Box" → `NewBoxWindow` dialog
5. Box window appears on desktop via `BoxWindowManager.ShowAsync()`

### 2. Box Interaction
1. User double-clicks item in box → Launch via shell
2. User drags file into box → `ImportPathsAsync()` + `AddOrUpdateAsync()`
3. User resizes/moves box → State saved on close
4. User snaps to taskbar → `SnapToTaskbarAsync()` creates `TaskbarBoxWindow`

### 3. Desktop Cleanup
1. User clicks "Clean Desktop" → `DesktopCleanupService.CleanAsync()`
2. Files moved to `%APPDATA%/Boxes/archive/`
3. Boxes still show shortcuts (marked as archived)
4. User clicks "Restore Desktop" → Files returned

## Design System

The app uses a dark theme with these core tokens:

| Token | Value | Usage |
|-------|-------|-------|
| `AccentBrush` | `#3A8DFF` | Primary actions, selection |
| `SurfaceBrush` | `#121316` | Base background |
| `SurfaceElevatedBrush` | `#1A1C20` | Cards, elevated elements |
| `BorderBrush` | `#2A2D33` | Borders, dividers |
| `RadiusS` | `8px` | Buttons, inputs |
| `RadiusL` | `18px` | Cards, modals |

### Button Variants
- `.btn-primary`: Accent background, white text (primary actions)
- `.btn-subtle`: Surface background, border (secondary actions)
- `.btn-danger`: Red outline → red fill on hover (destructive actions)

## Windows Integration

The app integrates with Windows via:
- **AppUserModelID**: Custom IDs for taskbar grouping
- **Shell shortcuts**: Create `.lnk` files via COM
- **Context menu**: Desktop right-click integration
- **Startup**: Optional run-at-login via Startup folder
- **Global mouse hook**: Detect clicks outside burst windows

## Known Architectural Decisions

1. **Static service locator (`AppServices`)**: Trade-off for simplicity over full DI
2. **JSON persistence**: Human-readable, easy debugging, no database dependency
3. **Single-threaded UI with async**: All UI updates on dispatcher thread
4. **No external dependencies for shortcuts**: Uses native Windows COM interop

## Future Considerations

- Cross-platform support (macOS, Linux) would require abstracting Windows-specific code
- SQLite could improve performance for large shortcut collections
- Plugin architecture for custom box types

---
*Last updated: December 2024*
