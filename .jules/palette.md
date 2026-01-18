## 2024-05-23 - Accessibility for Icon-only Buttons
**Learning:** Avalonia UI requires `AutomationProperties.Name` for accessible names on icon-only buttons (similar to `aria-label` in web). Tooltips are not sufficient for screen readers.
**Action:** Always add `AutomationProperties.Name` matching the `ToolTip.Tip` content for any button that relies solely on an icon for visual communication.
