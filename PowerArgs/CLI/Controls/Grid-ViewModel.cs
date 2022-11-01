namespace PowerArgs.Cli;

public partial class Grid
{
    private CollectionQuery query;
    private int selectedColumnIndex;
    private int visibleRowOffset;
    private IDisposable dataSourceSub;
    private IDisposable boundsSub;

    public Grid()
    {
        InitGridView();
        InitGridViewModel();
    }

    public Grid(CollectionDataSource dataSource) : this() { DataSource = dataSource; }

    public Grid(IEnumerable<object> items) : this(items.ToArray()) { }

    public Grid(params object?[] items) : this()
    {
        var prototype = items.FirstOrDefault();
        if (prototype == null)
            throw new InvalidOperationException("Can't infer columns without at least one item");

        foreach (var prop in prototype.GetType().GetProperties())
            VisibleColumns.Add(new ColumnViewModel(prop.Name.ToConsoleString(DefaultColors.H1Color)));

        DataSource = new MemoryDataSource(items);
    }

    public CollectionDataSource DataSource
    {
        get => Get<CollectionDataSource>()!;
        private set => Set(value);
    }

    public ObservableCollection<ColumnViewModel> VisibleColumns { get; private set; }

    public GridSelectionMode SelectionMode
    {
        get => Get<GridSelectionMode>();
        set => Set(value);
    }

    public ConsoleString RowPrefix
    {
        get => Get<ConsoleString>() ?? ConsoleString.Empty; 
        set => Set(value);
    }

    public int Gutter
    {
        get => Get<int>();
        set => Set(value);
    }

    public string? NoDataMessage
    {
        get => Get<string>();
        set => Set(value);
    }

    public string? EndOfDataMessage
    {
        get => Get<string>();
        set => Set(value);
    }

    public string? NoVisibleColumnsMessage
    {
        get => Get<string>();
        set => Set(value);
    }

    public bool FilteringEnabled
    {
        get => Get<bool>();
        set => Set(value);
    }

    public int NumRowsInView => Height - 2;

    public CollectionDataView DataView
    {
        get => Get<CollectionDataView>()!;
        private set => Set(value);
    }

    public int SelectedIndex
    {
        get => Get<int>();
        set => Set(value);
    }

    public object? SelectedItem
    {
        get => Get<object>();
        private set => Set(value);
    }

    public Func<object?, string, object?> PropertyResolver { get; set; } =
        (item, col) => item?.GetType().GetProperty(col)?.GetValue(item);

    public string? FilterText
    {
        get => query.Filter;
        set => SetFilterText(value);
    }

    public event Action SelectedItemActivated = () => { };

    public void Up()
    {
        if (SelectedIndex > 0)
        {
            SelectedIndex--;
        }

        if (SelectedIndex < visibleRowOffset)
        {
            visibleRowOffset--;
            query.Skip = visibleRowOffset;
            DataView = DataSource.GetDataView(query);
        }

        if (SelectedIndex - visibleRowOffset < DataView.Items.Count)
        {
            SelectedItem = DataView.Items[SelectedIndex - visibleRowOffset];
        }
    }

    public void Down()
    {
        if (DataView.IsLastKnownItem(SelectedItem) == false)
        {
            SelectedIndex++;
        }

        if (SelectedIndex >= visibleRowOffset + NumRowsInView)
        {
            visibleRowOffset++;
            query.Skip = visibleRowOffset;
            DataView = DataSource.GetDataView(query);
        }

        if (SelectedIndex - visibleRowOffset < DataView.Items.Count)
        {
            SelectedItem = DataView.Items[SelectedIndex - visibleRowOffset];
        }
        else if (SelectedIndex > 0)
        {
            SelectedIndex--;
            SelectedItem = DataView.Items[SelectedIndex - visibleRowOffset];
        }
    }

    public void Refresh() { DataView = DataSource.GetDataView(query); }

    public void PageUp()
    {
        if (SelectedIndex > visibleRowOffset)
        {
            SelectedIndex = visibleRowOffset;
        }
        else
        {
            visibleRowOffset -= NumRowsInView - 1;
            if (visibleRowOffset < 0) visibleRowOffset = 0;

            query.Skip = visibleRowOffset;
            DataView = DataSource.GetDataView(query);
        }

        if (SelectedIndex - visibleRowOffset < DataView.Items.Count)
        {
            SelectedItem = DataView.Items[SelectedIndex - visibleRowOffset];
        }
    }

    public void PageDown()
    {
        if (SelectedIndex != visibleRowOffset + DataView.Items.Count - 1)
        {
            SelectedIndex = visibleRowOffset + DataView.Items.Count - 1;
        }
        else
        {
            visibleRowOffset = visibleRowOffset + DataView.Items.Count - 1;
            SelectedIndex = visibleRowOffset;
            query.Skip = visibleRowOffset;
            DataView = DataSource.GetDataView(query);
        }

        if (SelectedIndex - visibleRowOffset < DataView.Items.Count)
        {
            SelectedItem = DataView.Items[SelectedIndex - visibleRowOffset];
        }
        else if (SelectedIndex > 0)
        {
            SelectedIndex--;
            SelectedItem = DataView.Items[SelectedIndex - visibleRowOffset];
        }
    }

    public void Home()
    {
        visibleRowOffset = 0;
        SelectedIndex = 0;
        query.Skip = visibleRowOffset;
        DataView = DataSource.GetDataView(query);

        if (SelectedIndex - visibleRowOffset < DataView.Items.Count)
        {
            SelectedItem = DataView.Items[SelectedIndex - visibleRowOffset];
        }
        else if (SelectedIndex > 0)
        {
            SelectedIndex--;
            SelectedItem = DataView.Items[SelectedIndex - visibleRowOffset];
        }
    }

    public void End()
    {
        if (SelectedIndex == DataSource.GetHighestKnownIndex(query))
        {
            PageDown();
        }
        else
        {
            SelectedIndex = DataSource.GetHighestKnownIndex(query);
            visibleRowOffset = SelectedIndex - NumRowsInView + 1;
            if (visibleRowOffset < 0) visibleRowOffset = 0;
            query.Skip = visibleRowOffset;
            DataView = DataSource.GetDataView(query);

            if (SelectedIndex - visibleRowOffset < DataView.Items.Count)
            {
                SelectedItem = DataView.Items[SelectedIndex - visibleRowOffset];
            }
            else if (SelectedIndex > 0)
            {
                SelectedIndex--;
                SelectedItem = DataView.Items[SelectedIndex - visibleRowOffset];
            }
        }
    }

    public void Left()
    {
        if (selectedColumnIndex > 0)
        {
            selectedColumnIndex--;
        }
    }

    public void Right()
    {
        if (selectedColumnIndex < VisibleColumns.Count - 1)
        {
            selectedColumnIndex++;
        }
    }

    public void Activate()
    {
        if (SelectedItem != null)
        {
            SelectedItemActivated();
        }
    }

    private void InitGridViewModel()
    {
        SelectionMode = GridSelectionMode.Row;
        RowPrefix = ConsoleString.Empty;
        Gutter = 3;
        VisibleColumns = new ObservableCollection<ColumnViewModel>();

        visibleRowOffset = 0;
        SelectedIndex = 0;
        dataSourceSub = SubscribeUnmanaged(nameof(DataSource), DataSourceOrBoundsChangedListener);
        boundsSub = SubscribeUnmanaged(nameof(Bounds), DataSourceOrBoundsChangedListener);

        query = new CollectionQuery();

        NoDataMessage = "No data";
        EndOfDataMessage = "End";
        NoVisibleColumnsMessage = "No visible columns";
    }

    private void SetFilterText(string? value)
    {
        query.Filter = value;
        visibleRowOffset = 0;
        SelectedIndex = 0;
        query.Skip = visibleRowOffset;
        DataView = DataSource.GetDataView(query);
        SelectedItem = DataView.Items.Count > 0 ? DataView.Items[0] : null;
    }

    private void DataSourceOrBoundsChangedListener()
    {
        query.Take = NumRowsInView;
        query.Skip = 0;
        DataView = DataSource.GetDataView(query);
        DataSource.DataChanged += DataSourceDataChangedListener;
        SelectedIndex = 0;
        selectedColumnIndex = 0;
        SelectedItem = DataView.Items.Count > 0 ? DataView.Items[0] : null;
    }

    private void DataSourceDataChangedListener()
    {
        query.Skip = visibleRowOffset;
        DataView = DataSource.GetDataView(query);
        SelectedItem = DataView.Items.Count == 0 ? null : DataView.Items[SelectedIndex - visibleRowOffset];
    }
}

public class ColumnViewModel : ObservableObject
{
    public ColumnViewModel(ConsoleString columnName)
    {
        ColumnName = columnName;
        ColumnDisplayName = columnName;
        OverflowBehavior = new GrowUnboundedOverflowBehavior();
    }

    public ColumnViewModel(string columnName) : this(columnName.ToConsoleString()) { }

    public ConsoleString ColumnName { get; set; }

    public ConsoleString ColumnDisplayName
    {
        get => Get<ConsoleString>() ?? ConsoleString.Empty;
        set => Set(value);
    }

    public double WidthPercentage
    {
        get => Get<double>();
        set => Set(value);
    }

    public ColumnOverflowBehavior OverflowBehavior { get; set; }
}

public enum GridSelectionMode
{
    Row,
    Cell,
    None
}