## 2024-05-23 - [UI Performance Bottleneck]
**Learning:** Adding items to an `ObservableCollection` one-by-one in a loop triggers a UI notification for each item, which can freeze the UI for large lists.
**Action:** Use a temporary list to build the collection and then add in bulk, or use a collection type that supports `AddRange`.
