## 2024-05-23 - [UI Performance Bottleneck]
**Learning:** Adding items to an `ObservableCollection` one-by-one in a loop triggers a UI notification for each item, which can freeze the UI for large lists.
**Action:** Use a temporary list to build the collection and then add in bulk, or use a collection type that supports `AddRange`.
## 2024-05-23 - Redundant FileExists Checks in Hot Paths
**Learning:** In C# `IFileSystem` abstractions (and real file systems), checking `File.Exists` before reading a file is a common "Look Before You Leap" pattern, but it doubles I/O operations if the read operation also handles missing files (or checks existence internally).
**Action:** When processing lists of files (e.g., scanning directories), prefer "Try-Read" patterns or rely on the inner method's validation to avoid O(N) extra syscalls.
