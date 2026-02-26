# Task Hierarchy (Internal)

## Current Outstanding Tasks

From docs/To_Add.md TO-DO:
- Add feature to click and drag desktop (highlighted area) to create a new box the shape of highlighted area.
  - Currently both boxes and settings ui start when the computer starts up.
- Add Pages to the boxes.
- Add hover effects to the boxes.
- Add drag and drop reorganization and cascading sorting within boxes.
- Center desktop box title text.
- Remove snap to taskbar feature.
- Add a keyboard shortcut to launch settings ui if closed.
- Improve settings UI scaling and layout.
  - Ensure elements are not cut off when window is resized.
  - Remove ugly highlight bg in dashboard.
  - Use same color/opacity settings in settings as in boxes???
- Add debouncing to the desktop box window collapse animation (same time as animation length).
- Menu button in desktop box window should open the box settings ui and various other options.
  - Migrate close button to menu button.
  - Add a keyboard shortcut to launch settings ui if closed.
  - Menu button should exist in one place for all boxes and not dependent on header bar. If header is hidden the menu button should still be visible.
- Build in feature to store layouts and modes with ability to easily switch between them.
  - game mode, work mode, etc.
- Brainstorm widgets and addons.
- Polish NoteHub UI.

From docs/To_Add.md TO-DO LATER:
- AI Integrations
  - AI organizer
  - AI note optimization

---

## Recommended Priority Order

### High Priority
1. Improve settings UI scaling and layout (elements cut off, highlight bg, color/opacity consistency)
2. Menu button refactor (unified placement, migrate close button, keyboard shortcut)
3. Add keyboard shortcut to launch settings ui if closed
4. Center desktop box title text

### Medium Priority
5. Add debouncing to desktop box window collapse animation
6. Remove snap to taskbar feature
7. Add hover effects to the boxes
8. Polish NoteHub UI
9. Add drag and drop reorganization and cascading sorting within boxes

### Low Priority
10. Add Pages to the boxes
11. Add feature to click and drag desktop to create new box (and fix startup: boxes vs settings ui)
12. Build in feature to store layouts and modes (game mode, work mode, etc.)
13. Brainstorm widgets and addons

### Later
14. AI Integrations (AI organizer, AI note optimization)

---

## Rationale

**High priority** focuses on UX polish and core navigation: the settings UI issues affect usability immediately; the menu button consolidation improves consistency and accessibility (keyboard shortcut). Centering title text is a quick visual fix.

**Medium priority** covers animation polish, removing unwanted behavior (snap to taskbar), and incremental feature improvements (hover effects, NoteHub polish, drag-and-drop). These build on stable core behavior.

**Low priority** items are larger features (Pages, drag-to-create boxes, layout/mode switching) that may require architectural changes. The startup behavior note (boxes vs settings ui) is grouped with the drag-to-create task as it relates to desktop interaction.

**Later** items (AI integrations) are explicitly deferred per To_Add.md.

---

## Recent Updates

- 2025-02-25 – Initial task hierarchy created from docs/To_Add.md TO-DO and TO-DO LATER sections.
