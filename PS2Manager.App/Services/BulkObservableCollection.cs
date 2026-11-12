using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace PS2Manager.App.Services;

/// <summary>ObservableCollection that can replace/remove many items with a single Reset notification.</summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        CheckReentrancy();
        Items.Clear();
        foreach (var i in items) Items.Add(i);
        RaiseReset();
    }

    public void RemoveMany(IEnumerable<T> items)
    {
        var set = items.ToHashSet();
        if (set.Count == 0) return;
        CheckReentrancy();
        for (int i = Items.Count - 1; i >= 0; i--)
            if (set.Contains(Items[i])) Items.RemoveAt(i);
        RaiseReset();
    }

    private void RaiseReset()
    {
        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
