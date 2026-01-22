## 2024-05-23 - Accessibility for Icon-only Buttons
**Learning:** Avalonia UI requires `AutomationProperties.Name` for accessible names on icon-only buttons (similar to `aria-label` in web). Tooltips are not sufficient for screen readers.
**Action:** Always add `AutomationProperties.Name` matching the `ToolTip.Tip` content for any button that relies solely on an icon for visual communication.

## 2026-01-20 - Empty States in Avalonia Lists
**Learning:** Avalonia DataGrids do not have a built-in "Empty Content" template.
**Action:** Wrap the `DataGrid` in a `Panel` and add a sibling `StackPanel` (containing an icon/emoji and helpful text) that is visible when the collection is empty. Use a ViewModel property like `HasItems` to toggle visibility for better performance than binding to `Count`.

## 2026-01-21 - Empty States in Master-Detail Views
**Learning:** In Master-Detail layouts, the Detail pane often appears broken or empty when no item is selected.
**Action:** Use a "No Selection" empty state in the Detail pane with a clear call to action (e.g., "Select an item from the list"). Use `ObjectConverters.IsNull` to toggle visibility against the selected item property.
