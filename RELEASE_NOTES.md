# EasyToDo - Release Notes

## Version 1.2.0 - [Current Date]

### ?? New Features
- **Critical Item Marking**: Added ?? critical button to mark important items
  - Critical button appears on hover next to each list item
  - Click to toggle critical status - button stays visible when marked as critical
  - Critical items display with bold text and dark red color
  - Critical status is saved and persists between sessions

### ?? Bug Fixes (from v1.1.0)
- Fixed delete functionality for todo list items
- Fixed checkbox functionality for marking items as completed/uncompleted
- Resolved event routing conflicts in ItemsControl template

### ?? Technical Improvements
- Enhanced data model with IsCritical property on NoteItem
- Improved UI layout with additional column for critical button
- Added visual styling for critical items (bold text, red color)
- Proper event handling using PreviewMouseLeftButtonDown pattern

### ?? Features
- Create and manage multiple sticky notes
- Add, edit, and delete todo items within notes
- **Mark items as critical with visual indicators**
- Check/uncheck items as completed with visual feedback
- Drag and drop to reorder items
- Color customization for notes
- Pin notes to stay on top
- Auto-save functionality
- Persistent storage between sessions

---

**Full Changelog**: https://github.com/your-username/your-repo-name/compare/v1.1.0...v1.2.0