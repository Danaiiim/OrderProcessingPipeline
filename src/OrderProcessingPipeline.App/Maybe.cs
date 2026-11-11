namespace OrderProcessingPipeline.App;

public abstract class Maybe<T>
{
    private protected Maybe() { }
}

public sealed class Some<T> : Maybe<T>
{
    public Some(T value) =>
        Value = value ?? throw new ArgumentNullException(nameof(value), "Some must carry a value");

    public T Value { get; }
}

public sealed class None<T> : Maybe<T> { }

public static class MaybeExtensions
{
    public static Result<T> ToResult<T>(this Maybe<T> source, string errorIfMissing) =>
        source switch
        {
            Some<T> s => new Success<T>(s.Value),
            None<T> => new Failure<T>(errorIfMissing),
            _ => new Failure<T>("Unknown maybe state")
        };
}
