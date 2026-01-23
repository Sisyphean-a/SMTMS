## 2024-05-23 - Accessibility for Icon-only Buttons
**Learning:** Avalonia UI requires `AutomationProperties.Name` for accessible names on icon-only buttons (similar to `aria-label` in web). Tooltips are not sufficient for screen readers.
**Action:** Always add `AutomationProperties.Name` matching the `ToolTip.Tip` content for any button that relies solely on an icon for visual communication.

## 2024-05-24 - Empty State Visual Hierarchy
**Learning:** Text-only empty states in Detail Panes are easily overlooked. Using large text-based icons (emojis) provides a lightweight "illustration" that anchors the user's attention without requiring asset management.
**Action:** When designing empty states in Avalonia, use a large `TextBlock` (FontSize ~48) with an emoji or symbol to create immediate visual recognition of the state.

## 2026-01-20 - Empty States in Avalonia Lists
**Learning:** Avalonia DataGrids do not have a built-in "Empty Content" template.
**Action:** Wrap the `DataGrid` in a `Panel` and add a sibling `StackPanel` (containing an icon/emoji and helpful text) that is visible when the collection is empty. Use a ViewModel property like `HasItems` to toggle visibility for better performance than binding to `Count`.

## 2026-01-21 - Empty States in Master-Detail Views
**Learning:** In Master-Detail layouts, the Detail pane often appears broken or empty when no item is selected.
**Action:** Use a "No Selection" empty state in the Detail pane with a clear call to action (e.g., "Select an item from the list"). Use `ObjectConverters.IsNull` to toggle visibility against the selected item property.
## 2026-01-20 - Empty States in Avalonia Lists
**Learning:** Avalonia DataGrids do not have a built-in "Empty Content" template.
**Action:** Wrap the `DataGrid` in a `Panel` and add a sibling `StackPanel` (containing an icon/emoji and helpful text) that is visible when the collection is empty. Use a ViewModel property like `HasItems` to toggle visibility for better performance than binding to `Count`.

## 2026-01-21 - Tooltips on Non-Obvious Interactive Elements
**Learning:** Users may not realize that certain text elements (like version numbers in a grid) are clickable links.
**Action:** When making text elements interactive (e.g., acting as hyperlinks), explicitly add `ToolTip.Tip` to explain the action (e.g., "Click to visit Nexus page") to provide immediate visual feedback and clarity.

## 2026-01-21 - Input Field Accessibility
**Learning:** Visual labels (TextBlocks) are insufficient for screen readers. Explicit association via `AutomationProperties.Name` is required on input controls (like `TextBox`).
**Action:** Always add `AutomationProperties.Name` to input fields, matching their visual label. Use `Watermark` to provide additional context where helpful.
