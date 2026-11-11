using System.Collections.Immutable;

namespace OrderProcessingPipeline.App;

public sealed record OrderStore(ImmutableList<Order> Orders, string Source)
{
    public static OrderStore Empty { get; } = new(ImmutableList<Order>.Empty, "nothing loaded");

    public static (OrderStore Store, string Message) Reload(OrderStore current, string path, Result<ParsedInput> loaded) =>
        loaded.Match<ParsedInput, (OrderStore, string)>(
            input => (new OrderStore(input.Orders, path), $"Loaded {input.Orders.Count} orders from '{path}'"),
            error => (current, $"LOAD FAILED: {error}. Kept {current.Orders.Count} orders from '{current.Source}'"));
}
