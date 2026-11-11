namespace OrderProcessingPipeline.App;

public static class Policies
{
    public static decimal Standard(Order order)
    {
        if (order.ItemTotal >= 15000m && order.DistanceKm <= 10m)
            return 0m;
        var extraWeight = order.WeightKg > 2m ? (order.WeightKg - 2m) * 50m : 0m;
        return 500m + order.DistanceKm * 100m + extraWeight;
    }

    public static decimal Express(Order order)
    {
        var extraWeight = order.WeightKg > 1m ? (order.WeightKg - 1m) * 100m : 0m;
        return 1200m + order.DistanceKm * 200m + extraWeight;
    }

    public static Result<Func<Order, decimal>> FromName(string? name) =>
        name?.Trim().ToLowerInvariant() switch
        {
            "standard" => new Success<Func<Order, decimal>>(Standard),
            "express" => new Success<Func<Order, decimal>>(Express),
            null or "" => new Failure<Func<Order, decimal>>("Policy name is missing"),
            _ => new Failure<Func<Order, decimal>>($"Unknown policy '{name}'. Use standard or express")
        };
}