# Future implementations and idea board

## General
- Make a settings page that allows the user to change the theme of the app.
- Make a page that allows the user to create a new box.
- Make a page that allows the user to import a layout.
- Make a page that allows the user to sync the desktop.

## Ideas
- Website for advertising, download and payment handling.
- Tier based subscription system.

## Design Themes
- Acetate
- Glass
- custom box shapes (circular, etc)
- custom animations ()
- custom box colors
- custom box textures

## Add ons
- AI categorization
- Automation
- Default new file type 
- External mini desktop 
    - Raspberry pi to display the desktop boxes
- Widgets
    - Weather
    - Time
    - Calendar
    - Ai chatbot
    - Ai buddy (personal assistant)
    - Music player
    - Automated tasks

### Revealing background effects
Sizing the box reveals a secondary background image.  would be cool to reveal a cook bg over a black or mundane bg.


# TO-DO:
- inspect and improve upon current data saving/updating functionality.
    - (Completed 2026-02-14 - Added atomic file writes, retry logic, backup on corruption, and comprehensive error handling to SettingsService and BoxService)
- Add feature to click and drag desktop (highlighted area) to create a new box the shape of highlighted area.
- Implement box window templates (dashboard picker + template info + selection persistence).
    - (Partially completed 2025-12-14 – Added Dashboard template picker UI, template info panel, and persisted `TemplateKey` placeholder; documented the expander animation approach in `docs/thatSauce/TemplateExpanderAnimation.md`.)
- start building themes, well organized each theme in its own file probably.
    - (Completed 2026-02-14 - Added GlassTheme.axaml and AcetateTheme.axaml with complete color palettes, spacing, shadows, and corner radii)
- Auto start the app when the computer starts up, only boxes windows should start not settings ui.
    - Currently both boxes and settings ui start when the computer starts up.
- Work on adaptive sizing for icons, sizing, spacing and text.
- Enhance settings save and load functionality.
    - (Completed 2025-12-05 – Added sidebar box settings with auto-save for name/description, click-out deselect, and inline delete replacing popup.)
    - (Completed 2026-02-14 - Full rewrite with retry logic, atomic writes, and corruption recovery)
- Add feature to click and drag desktop (highlighted area) to create a new box the shape of highlighted area.
- Focus on standard boxes optimizations and improvements before taskbar boxes.
- Add multi-monitor support.
- Add setting toggle for removing real desktop icons from the desktop when drag the icon into a box.
- Add Pages to the boxes.
- Add hover effects to the boxes.
- Add drag and drop reorganization and cascading sortingwithin boxes.
- Complete snap trigger features.
- remove icons from desktop when dragged into a box. - toggleable setting.


# NOTES:
- Hysteresis is good for solving the jittering issue.