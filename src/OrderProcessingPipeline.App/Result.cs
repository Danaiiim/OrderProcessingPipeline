namespace OrderProcessingPipeline.App;

public abstract class Result<T>
{
    private protected Result() { }
}

public sealed class Success<T> : Result<T>
{
    public Success(T value) =>
        Value = value ?? throw new ArgumentNullException(nameof(value), "Success must carry a value");

    public T Value { get; }
}

public sealed class Failure<T> : Result<T>
{
    public Failure(string error) =>
        Error = string.IsNullOrWhiteSpace(error)
            ? throw new ArgumentException("Failure must have an error message", nameof(error))
            : error;

    public string Error { get; }
}

public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> source, Func<TIn, TOut> function) =>
        source switch
        {
            Success<TIn> s => new Success<TOut>(function(s.Value)),
            Failure<TIn> f => new Failure<TOut>(f.Error),
            _ => new Failure<TOut>("Unknown result state")
        };

    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> source, Func<TIn, Result<TOut>> function) =>
        source switch
        {
            Success<TIn> s => function(s.Value),
            Failure<TIn> f => new Failure<TOut>(f.Error),
            _ => new Failure<TOut>("Unknown result state")
        };

    public static TOut Match<TIn, TOut>(this Result<TIn> source, Func<TIn, TOut> onSuccess, Func<string, TOut> onFailure) =>
        source switch
        {
            Success<TIn> s => onSuccess(s.Value),
            Failure<TIn> f => onFailure(f.Error),
            _ => onFailure("Unknown result state")
        };

    public static TOut Pipe<TIn, TOut>(this TIn value, Func<TIn, TOut> function) => function(value);
}
