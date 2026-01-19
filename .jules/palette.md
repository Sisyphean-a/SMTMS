## 2024-05-23 - Accessibility for Icon-only Buttons
**Learning:** Avalonia UI requires `AutomationProperties.Name` for accessible names on icon-only buttons (similar to `aria-label` in web). Tooltips are not sufficient for screen readers.
**Action:** Always add `AutomationProperties.Name` matching the `ToolTip.Tip` content for any button that relies solely on an icon for visual communication.

## 2024-05-24 - Empty State Visual Hierarchy
**Learning:** Text-only empty states in Detail Panes are easily overlooked. Using large text-based icons (emojis) provides a lightweight "illustration" that anchors the user's attention without requiring asset management.
**Action:** When designing empty states in Avalonia, use a large `TextBlock` (FontSize ~48) with an emoji or symbol to create immediate visual recognition of the state.
