namespace PowerArgs;

public static class ExceptionsEx
{
    public static IEnumerable<Exception> Clean(this Exception? ex) =>
        ex switch {
            AggregateException ae => Clean(ae.InnerExceptions),
            null                  => Array.Empty<Exception>(),
            _                     => new[] { ex }
        };

    public static IEnumerable<Exception> Clean(this IEnumerable<Exception> inner) =>
        inner.SelectMany(
            e => e switch {
                AggregateException ae => Clean(ae.InnerExceptions),
                null                  => Array.Empty<Exception>(),
                _                     => new[] { e }
            });
}