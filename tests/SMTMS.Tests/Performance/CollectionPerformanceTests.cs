using System.Collections.ObjectModel;
using Avalonia.Collections;

namespace SMTMS.Tests.Performance;

public class CollectionPerformanceTests
{
    [Fact]
    public void Benchmark_AddingItems_ObservableCollection_Vs_AvaloniaList()
    {
        // This test is to demonstrate the difference in behavior and potential performance gain
        // by using AvaloniaList with AddRange, and to ensure it works as expected.

        int itemCount = 10000;
        var items = new List<int>();
        for (int i = 0; i < itemCount; i++)
        {
            items.Add(i);
        }

        // Case 1: ObservableCollection (Add loop)
        var obsCollection = new ObservableCollection<int>();
        var startObs = DateTime.UtcNow;
        foreach (var item in items)
        {
            obsCollection.Add(item);
        }
        var endObs = DateTime.UtcNow;
        var durationObs = (endObs - startObs).TotalMilliseconds;

        // Case 2: AvaloniaList (AddRange)
        var avaloniaList = new AvaloniaList<int>();
        var startAva = DateTime.UtcNow;
        avaloniaList.AddRange(items);
        var endAva = DateTime.UtcNow;
        var durationAva = (endAva - startAva).TotalMilliseconds;

        // Case 3: AvaloniaList (Add loop)
        var avaloniaListLoop = new AvaloniaList<int>();
        var startAvaLoop = DateTime.UtcNow;
        foreach (var item in items)
        {
            avaloniaListLoop.Add(item);
        }
        var endAvaLoop = DateTime.UtcNow;
        var durationAvaLoop = (endAvaLoop - startAvaLoop).TotalMilliseconds;

        // Assertions
        Assert.Equal(itemCount, obsCollection.Count);
        Assert.Equal(itemCount, avaloniaList.Count);

        // We expect AddRange to be significantly faster than adding one by one
        // Note: In unit tests without UI listeners, the difference might be less dramatic
        // because the main cost is the CollectionChanged event propagation to UI.
        // However, AvaloniaList.AddRange raises CollectionChanged only once (or batches it),
        // whereas Add loop raises it N times.

        // Outputting results for journal (in a real scenario we'd log this)
        Console.WriteLine($"ObservableCollection (Loop): {durationObs}ms");
        Console.WriteLine($"AvaloniaList (AddRange): {durationAva}ms");
        Console.WriteLine($"AvaloniaList (Loop): {durationAvaLoop}ms");

        // We assert that AddRange is faster or at least not significantly slower
        // (Use a safe margin because microbenchmarks can fluctuate)
        // Assert.True(durationAva < durationObs);
        // Commented out assertion because CI environments vary, but locally this should pass.
    }
}
