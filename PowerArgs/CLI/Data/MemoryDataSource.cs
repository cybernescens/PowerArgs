using System.Reflection;

namespace PowerArgs.Cli;

public class MemoryDataSource : CollectionDataSource
{
    public MemoryDataSource(params object?[] items) { Items = items; }

    public MemoryDataSource(IEnumerable<object?> items) { Items = items; }

    public IEnumerable<object?> Items { get; }

    public override int GetHighestKnownIndex(CollectionQuery query)
    {
        var results = Items;

        if (query.Filter != null)
            results = results.Where(item => MatchesFilter(item, query.Filter));

        return results.Count() - 1;
    }

    public void Invalidate() { FireDataChanged(); }

    public override void ClearCachedData() { FireDataChanged(); }

    public override CollectionDataView GetDataView(CollectionQuery query)
    {
        var results = Items;

        if (query.Filter != null)
            results = results.Where(item => MatchesFilter(item, query.Filter));

        results = results.Skip(query.Skip).Take(query.Take);

        foreach (var orderBy in query.SortOrder)
        {
            object? ItemValue(object? item) => item?.GetType().GetProperty(orderBy.Value)?.GetValue(item);

            if (results is IOrderedEnumerable<object?> ordered)
            {
                Func<IOrderedEnumerable<object?>, IOrderedEnumerable<object?>> thenBy =
                    orderBy.Descending
                        ? x => x.ThenByDescending(ItemValue)
                        : x => x.ThenBy(ItemValue);

                results = thenBy(ordered);
            }
            else
            {
                Func<IEnumerable<object?>, IOrderedEnumerable<object?>> order =
                    orderBy.Descending
                        ? x => x.OrderByDescending(ItemValue)
                        : x => x.OrderBy(ItemValue);

                results = order(results);
            }
        }

        return new CollectionDataView(
            results.ToList(),
            true,
            query.Skip,
            query.Take);
    }

    private bool MatchesFilter(object? item, string? filter)
    {
        if (string.IsNullOrEmpty(filter)) 
            return true;

        var filterables = (item?.GetType().GetProperties() ?? Array.Empty<PropertyInfo>())
            .Where(prop => prop.HasAttr<FilterableAttribute>())
            .ToArray();

        if (!filterables.Any())
        {
            return (item?.ToString() ?? string.Empty).IndexOf(filter, StringComparison.InvariantCultureIgnoreCase) >= 0;
        }

        foreach (var filterable in filterables)
        {
            var propValue = filterable.GetValue(item);
         
            if (propValue == null)
                continue;
            
            if ((propValue.ToString() ?? string.Empty).IndexOf(filter, StringComparison.InvariantCultureIgnoreCase) >= 0)
                return true;
        }

        return false;
    }
}