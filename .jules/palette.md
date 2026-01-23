## 2024-05-23 - Accessibility for Icon-only Buttons
**Learning:** Avalonia UI requires `AutomationProperties.Name` for accessible names on icon-only buttons (similar to `aria-label` in web). Tooltips are not sufficient for screen readers.
**Action:** Always add `AutomationProperties.Name` matching the `ToolTip.Tip` content for any button that relies solely on an icon for visual communication.

## 2026-01-20 - Empty States in Avalonia Lists
**Learning:** Avalonia DataGrids do not have a built-in "Empty Content" template.
**Action:** Wrap the `DataGrid` in a `Panel` and add a sibling `StackPanel` (containing an icon/emoji and helpful text) that is visible when the collection is empty. Use a ViewModel property like `HasItems` to toggle visibility for better performance than binding to `Count`.

## 2026-01-21 - Tooltips on Non-Obvious Interactive Elements
**Learning:** Users may not realize that certain text elements (like version numbers in a grid) are clickable links.
**Action:** When making text elements interactive (e.g., acting as hyperlinks), explicitly add `ToolTip.Tip` to explain the action (e.g., "Click to visit Nexus page") to provide immediate visual feedback and clarity.
