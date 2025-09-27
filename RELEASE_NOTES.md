# EasyToDo - Release Notes

## Version 1.1.0 - [Current Date]

### ?? New Features
- Fixed delete functionality for todo list items
- Improved item deletion with proper event handling

### ?? Bug Fixes
- Fixed X button not working when trying to delete list items
- Resolved event routing conflicts in ItemsControl template
- Improved hover behavior for delete buttons

### ?? Technical Improvements
- Switched from Click to PreviewMouseLeftButtonDown event for delete buttons
- Added proper event handling to prevent event bubbling
- Enhanced DataContext binding for delete functionality

### ?? Features
- Create and manage multiple sticky notes
- Add, edit, and delete todo items within notes
- Check/uncheck items as completed
- Drag and drop to reorder items
- Color customization for notes
- Pin notes to stay on top
- Auto-save functionality
- Persistent storage between sessions

---

**Full Changelog**: https://github.com/your-username/your-repo-name/compare/v1.0.0...v1.1.0