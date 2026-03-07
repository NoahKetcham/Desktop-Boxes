# Future implementations and idea board

## General
- Make a settings page that allows the user to change the theme of the app.
- Make a page that allows the user to create a new box.
- Make a page that allows the user to import a layout.
- Make a page that allows the user to sync the desktop.

## Ideas
- Website for advertising, download and payment handling.
- Tier based subscription system.
- Control panel widget
    - general controls
    - mode switcher
    - settings
    - box switcher
    - notehub access

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
- Add feature to click and drag desktop (highlighted area) to create a new box the shape of highlighted area.
    - Currently both boxes and settings ui start when the computer starts up.
- Add Pages to the boxes.
- Add hover effects to the boxes.
- Add drag and drop reorganization and cascading sortingwithin boxes.
- Center destop box title text
- remove snap to taskbar feature.
- add a keyboard shortcut to launch settings ui if closed.
- improve settings UI scaling and layout.
    - ensure elements are not cut off when window is resized.
    - remove ugly highlight bg in dashboard.
    - use same color/opacity settings in settings as in boxes???
- Add debouncing to the desktop box window collapse animation (same time as animation length).
- Menu button in desktop box window should open the box settings ui and various other options.
    - migrate close button to menu button.
    - add a keyboard shortcut to launch settings ui if closed.
    menu button should exist in one place for all boxes and not dependant on header bar.  If header is hidden the menu button should still be visible.
- Build in feature to store layouts and modes with ability to easily switch between them.
    - game mode, work mode, etc.
- brainstorm widgets and addons.
- Polish NoteHub UI.




# TO-DO LATER:
- AI Integrations
    - AI organizer
    - AI note optimization

# NOTES:
- Hysteresis is good for solving the jittering issue.