using System.Collections.Immutable;

namespace OrderProcessingPipeline.App;

public enum CustomerTier
{
    Regular,
    Vip
}

public sealed record Order
{
    public string OrderId { get; }
    public CustomerTier Tier { get; }
    public decimal DistanceKm { get; }
    public decimal ItemTotal { get; }
    public decimal WeightKg { get; }

    private Order(string orderId, CustomerTier tier, decimal distanceKm, decimal itemTotal, decimal weightKg) =>
        (OrderId, Tier, DistanceKm, ItemTotal, WeightKg) = (orderId, tier, distanceKm, itemTotal, weightKg);

    public static Result<Order> Create(string? id, CustomerTier tier, decimal dist, decimal total, decimal weight)
    {
        if (string.IsNullOrWhiteSpace(id))
            return new Failure<Order>("OrderId is missing");
        if (dist < 0m)
            return new Failure<Order>($"DistanceKm cannot be negative, got {dist}");
        if (total < 0m)
            return new Failure<Order>($"ItemTotal cannot be negative, got {total}");
        if (weight <= 0m)
            return new Failure<Order>($"WeightKg must be greater than 0, got {weight}");

        return new Success<Order>(new Order(id.Trim(), tier, dist, total, weight));
    }
}

public abstract record DeliveryEligibility;
public sealed record ServiceableOrder(Order Order) : DeliveryEligibility;
public sealed record UnserviceableOrder(Order Order, string Reason) : DeliveryEligibility;

public sealed record PricedOrder(Order Order, decimal DeliveryFee);

public sealed record OrderReceipt(
    string OrderId,
    decimal ItemTotal,
    decimal DeliveryFee,
    decimal GrandTotal);

public sealed record ReportSummary(
    ImmutableList<OrderReceipt> Receipts,
    decimal TotalSum,
    decimal AverageDelivery)
{
    public static ReportSummary Empty { get; } = new(ImmutableList<OrderReceipt>.Empty, 0m, 0m);
}

public sealed record ParsedInput(ImmutableList<Order> Orders, ImmutableList<string> Errors);

public sealed record DeliverySettings(decimal MaxDistanceKm, decimal MaxWeightKg = 30m);