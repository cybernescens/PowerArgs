namespace PowerArgs.Cli;

public abstract class CollectionDataSource
{
    public event Action DataChanged = () => { };
    public abstract CollectionDataView GetDataView(CollectionQuery query);
    public abstract int GetHighestKnownIndex(CollectionQuery query);

    public void FireDataChanged()
    {
        DataChanged();
    }

    public abstract void ClearCachedData();
}