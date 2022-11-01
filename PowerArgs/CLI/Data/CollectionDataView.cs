namespace PowerArgs.Cli;

public class CollectionDataView
{
    public CollectionDataView(List<object?> items, bool isCompletelyLoaded, int rowOffset, int pageLength)
    {
        Items = items.AsReadOnly();
        IsViewComplete = isCompletelyLoaded;
        IsViewEndOfData = rowOffset + pageLength >= items.Count - 1;
        RowOffset = rowOffset;
    }

    public bool IsViewComplete { get; }
    public bool IsViewEndOfData { get; }
    public int RowOffset { get; }
    public IReadOnlyList<object?> Items { get; }

    public bool IsLastKnownItem(object? item)
    {
        if (Items.Count < 1) 
            return item == null;

        if (ReferenceEquals(item, Items[^1]) == false)
            return false;

        return IsViewComplete == false || IsViewEndOfData;
    }
}