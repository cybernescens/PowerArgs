﻿namespace PowerArgs.Cli;

internal class DataGridCoreOptions
{
    public bool ShowColumnHeaders { get; set; } = true;
    public bool ShowPager { get; set; } = true;
    public bool EnablePagerKeyboardShortcuts { get; set; } = true;
    public List<DataGridColumnDefinition> Columns { get; set; }
    public List<DataGridPresentationRow> Rows { get; set; }
    public PagerState PagerState { get; set; }
    public ConsoleString? LoadingMessage { get; set; }
    public bool IsLoading { get; set; }
}

internal class DataGridPresentationRow
{
    public List<Func<ConsoleControl>> Cells { get; } = new();
}

public class DataGridColumnDefinition : GridColumnDefinition
{
    public ConsoleString Header { get; set; }
}

internal class PagerState
{
    public bool AllowRandomAccess { get; set; }
    public bool CanGoBackwards { get; set; }
    public bool CanGoForwards { get; set; }
    public ConsoleString? CurrentPageLabelValue { get; set; }
}

internal class DataGridPresenter : ProtectedConsolePanel
{
    private bool firstButtonFocused, previousButtonFocused, nextButtonFocused, lastButtonFocused;
    private readonly GridLayout gridLayout;
    private ConsolePanel? loadingPanel;

    private RandomAccessPager? pager;
    private ConsolePanel? pagerContainer;
    private readonly List<ConsoleControl> recomposableControls = new();

    public DataGridPresenter(DataGridCoreOptions options)
    {
        Options = options;

        var columns = options.Columns.Select(c => c as GridColumnDefinition).ToList();

        if (columns.All(c => c.Type != GridValueType.RemainderValue))
        {
            columns.Add(
                new GridColumnDefinition {
                    Type = GridValueType.RemainderValue,
                    Width = 1
                });
        }

        gridLayout = ProtectedPanel
            .Add(new GridLayout(new GridLayoutOptions { Columns = columns, Rows = new List<GridRowDefinition>() }))
            .Fill();

        SubscribeForLifetime(this, nameof(Bounds), Recompose);
    }

    public DataGridCoreOptions Options { get; }

    public Dictionary<int, List<ConsoleControl>> ControlsByRow { get; } = new();

    public Event FirstPageClicked { get; } = new();
    public Event PreviousPageClicked { get; } = new();
    public Event NextPageClicked { get; } = new();
    public Event LastPageClicked { get; } = new();
    public Event BeforeRecompose { get; } = new();
    public Event AfterRecompose { get; } = new();
    public int MaxRowsThatCanBePresented =>
        Options.ShowColumnHeaders ? Math.Max(0, Height - 2) : Math.Max(0, Height - 1);

    public void Recompose()
    {
        if (MaxRowsThatCanBePresented == 0) return;

        BeforeRecompose.Fire();
        SnapshotPagerFocus();
        Decompose();
        ComposeGridLayout();

        if (Options.IsLoading)
        {
            ComposeLoadingUX();
        }
        else
        {
            ComposeDataCells();
            ComposePager();
        }

        AfterRecompose.Fire();
    }

    private void SnapshotPagerFocus()
    {
        firstButtonFocused = pager != null && pager.FirstPageButton.HasFocus;
        previousButtonFocused = pager != null && pager.PreviousPageButton.HasFocus;
        nextButtonFocused = pager != null && pager.NextPageButton.HasFocus;
        lastButtonFocused = pager != null && pager.LastPageButton.HasFocus;
    }

    private void Decompose()
    {
        for (var i = 0; i < recomposableControls.Count; i++)
            gridLayout.Remove(recomposableControls[i]);

        recomposableControls.Clear();
        ControlsByRow.Clear();

        if (loadingPanel != null)
        {
            ProtectedPanel.Controls.Remove(loadingPanel);
            loadingPanel = null;
        }
    }

    private void ComposeGridLayout()
    {
        for (var i = 0; i < Height; i++)
            gridLayout.Options.Rows.Add(new GridRowDefinition { Height = 1, Type = GridValueType.Pixels });

        gridLayout.RefreshLayout();
    }

    private void ComposeLoadingUX()
    {
        loadingPanel = ProtectedPanel.Add(new ConsolePanel { ZIndex = int.MaxValue }).Fill();
        loadingPanel.Add(new Label { Text = Options.LoadingMessage }).CenterBoth();
    }

    private void ComposeDataCells()
    {
        if (Options.ShowColumnHeaders)
        {
            for (var col = 0; col < Options.Columns.Count; col++)
                recomposableControls.Add(gridLayout.Add(new Label { Text = Options.Columns[col].Header }, col, 0));
        }

        var dataRowStartIndex = Options.ShowColumnHeaders ? 1 : 0;
        var currentIndex = 0;
        for (var gridLayoutRow = dataRowStartIndex;
             gridLayoutRow < dataRowStartIndex + MaxRowsThatCanBePresented;
             gridLayoutRow++)
        {
            if (currentIndex >= Options.Rows.Count) break;

            var dataItem = Options.Rows[currentIndex];
            var rowControls = new List<ConsoleControl?>();
            ControlsByRow.Add(currentIndex, rowControls);
            for (var gridLayoutCol = 0; gridLayoutCol < Options.Columns.Count; gridLayoutCol++)
            {
                var cellDisplayControl = gridLayout.Add(
                    dataItem.Cells[gridLayoutCol].Invoke(),
                    gridLayoutCol,
                    gridLayoutRow);

                recomposableControls.Add(cellDisplayControl);
                rowControls.Add(cellDisplayControl);
            }

            currentIndex++;
        }
    }

    private void ComposePager()
    {
        pagerContainer = gridLayout.Add(new ConsolePanel(), 0, Height - 1, gridLayout.Options.Columns.Count);
        recomposableControls.Add(pagerContainer);
        pager = pagerContainer.Add(new RandomAccessPager(Options.EnablePagerKeyboardShortcuts)).CenterHorizontally();
        pager.IsVisible = Options.ShowPager;
        pager.FirstPageButton.Pressed.SubscribeForLifetime(pager, FirstPageClicked.Fire);
        pager.PreviousPageButton.Pressed.SubscribeForLifetime(pager, PreviousPageClicked.Fire);
        pager.NextPageButton.Pressed.SubscribeForLifetime(pager, NextPageClicked.Fire);
        pager.LastPageButton.Pressed.SubscribeForLifetime(pager, LastPageClicked.Fire);
        pager.FirstPageButton.CanFocus = Options.PagerState.CanGoBackwards;
        pager.PreviousPageButton.CanFocus = Options.PagerState.CanGoBackwards;
        pager.NextPageButton.CanFocus = Options.PagerState.CanGoForwards;
        pager.LastPageButton.CanFocus = Options.PagerState.CanGoForwards;
        pager.CurrentPageLabel.Text = Options.PagerState.CurrentPageLabelValue;

        if (Options.PagerState.AllowRandomAccess == false)
        {
            pager.Controls.Remove(pager.LastPageButton);
        }

        if (firstButtonFocused)
        {
            pager.FirstPageButton.TryFocus();
        }
        else if (previousButtonFocused)
        {
            pager.PreviousPageButton.TryFocus();
        }
        else if (nextButtonFocused)
        {
            pager.NextPageButton.TryFocus();
        }
        else if (lastButtonFocused)
        {
            pager.LastPageButton.TryFocus();
        }
    }

    private class RandomAccessPager : StackPanel
    {
        public RandomAccessPager(bool enableShortcuts)
        {
            AutoSize = true;
            Margin = 2;
            Orientation = Orientation.Horizontal;
            FirstPageButton = Add(new Button { Text = "<<".ToConsoleString() });
            PreviousPageButton = Add(new Button { Text = "<".ToConsoleString() });
            CurrentPageLabel = Add(new Label { Text = "Page 1 of 1".ToConsoleString() });
            NextPageButton = Add(new Button { Text = ">".ToConsoleString() });
            LastPageButton = Add(new Button { Text = ">>".ToConsoleString() });

            if (enableShortcuts)
            {
                FirstPageButton.Shortcut = new KeyboardShortcut(ConsoleKey.Home);
                PreviousPageButton.Shortcut = new KeyboardShortcut(ConsoleKey.PageUp);
                NextPageButton.Shortcut = new KeyboardShortcut(ConsoleKey.PageDown);
                LastPageButton.Shortcut = new KeyboardShortcut(ConsoleKey.End);
            }
        }

        public Button FirstPageButton { get; }
        public Button PreviousPageButton { get; }
        public Label CurrentPageLabel { get; }
        public Button NextPageButton { get; }
        public Button LastPageButton { get; }
    }
}