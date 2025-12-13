# Changelog

All notable changes to Desktop Boxes will be documented in this file.

---

## [Unreleased] - December 2024

### Frontend UX Changes

#### Added
- **Navigation icons**: Overview (📊), Dashboard (📦), and Settings (⚙️) now have visual icons
- **Empty states**: DesktopBoxWindow shows helpful empty state with drag-drop hint
- **Breadcrumb navigation**: Box windows show current folder path in header when navigating
- **Overview page stats**: Real-time data for Active Boxes, Shortcuts, and Scanned Files counts
- **Recent activity feed**: Dynamic status updates on Overview page
- **Quick tips sections**: Added to sidebar views for user guidance
- **Tooltips**: Added to all interactive elements for accessibility
- **ARIA labels**: Added `AutomationProperties.Name` to buttons and inputs

#### Changed
- **Dialog styling**: All dialogs now use consistent `.btn-primary`, `.btn-subtle`, `.btn-danger` classes
- **Dialog layouts**: Improved spacing, added field labels, better visual hierarchy
- **MainWindow navigation**: Added hover transitions, selected state accent border
- **DesktopBoxWindow**: Improved shortcut item hover effects, better header button styling
- **OverviewPageView**: Replaced hardcoded placeholders with real service bindings
- **AdvertisingView**: Added quick actions and keyboard tips
- **DashboardBoxSettingsView**: Better empty state, consistent card styling

#### Fixed
- Button styling inconsistency across dialogs
- Missing visual feedback on navigation item selection
- Hardcoded placeholder data in Overview page

### Documentation
- Added `docs/ARCHITECTURE_OVERVIEW.md` with system documentation
- Added `docs/UX_REFACTOR_SUMMARY.md` with refactor details
- Added `docs/CHANGELOG.md` (this file)

---

## How to Read This Changelog

- **Added**: New features
- **Changed**: Changes in existing functionality
- **Deprecated**: Soon-to-be removed features
- **Removed**: Removed features
- **Fixed**: Bug fixes
- **Security**: Vulnerability fixes

---

*Format based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/)*
