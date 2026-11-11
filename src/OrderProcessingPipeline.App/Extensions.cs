namespace OrderProcessingPipeline.App;

public static class ResultListExtensions
{
    public static IEnumerable<T> Successes<T>(this IEnumerable<Result<T>> results) =>
        results.OfType<Success<T>>().Select(s => s.Value);

    public static IEnumerable<string> Errors<T>(this IEnumerable<Result<T>> results) =>
        results.OfType<Failure<T>>().Select(f => f.Error);
}
