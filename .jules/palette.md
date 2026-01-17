## 2026-01-17 - Accessibility for Icon-Only Buttons
**Learning:** Icon-only buttons (like "🌐") are inaccessible to screen readers if they rely solely on `ToolTip.Tip` or visual content. They require `AutomationProperties.Name` (in Avalonia) to provide context.
**Action:** Always verify icon-only buttons have an accessible name property defined.
