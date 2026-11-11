using System.Collections.Immutable;

namespace OrderProcessingPipeline.App;

public static class Pipeline
{
    public const decimal VipDiscount = 0.8m;

    public static DeliveryEligibility CheckEligibility(Order order, decimal maxDist) =>
        order.DistanceKm <= maxDist
            ? new ServiceableOrder(order)
            : new UnserviceableOrder(order, $"Order {order.OrderId}: distance {order.DistanceKm} km exceeds {maxDist} km");

    public static ImmutableList<Order> SelectServiceable(IEnumerable<Order> orders, decimal maxDistanceKm) =>
        orders
            .Select(o => CheckEligibility(o, maxDistanceKm))
            .SelectMany(e => e switch
            {
                ServiceableOrder s => new[] { s.Order },
                UnserviceableOrder => Enumerable.Empty<Order>(),
                _ => Enumerable.Empty<Order>()
            })
            .ToImmutableList();

    public static PricedOrder PriceServiceable(ServiceableOrder serviceable, Func<Order, decimal> feePolicy)
    {
        var o = serviceable.Order;
        return new PricedOrder(o, ApplyTierDiscount(o.Tier, feePolicy(o)));
    }

    public static Result<PricedOrder> ProcessEligibility(DeliveryEligibility eligibility, Func<Order, decimal> feePolicy) =>
        eligibility switch
        {
            ServiceableOrder s => new Success<PricedOrder>(PriceServiceable(s, feePolicy)),
            UnserviceableOrder u => new Failure<PricedOrder>($"Rejected: {u.Reason}"),
            _ => new Failure<PricedOrder>("Unknown state")
        };

    public static PricedOrder SafeRecalculate(PricedOrder existing) => existing;

    public static ImmutableList<PricedOrder> CalculateDelivery(IEnumerable<Order> orders, Func<Order, decimal> feePolicy) =>
        orders.Select(o => PriceServiceable(new ServiceableOrder(o), feePolicy)).ToImmutableList();

    public static decimal ApplyTierDiscount(CustomerTier tier, decimal fee) =>
        tier == CustomerTier.Vip ? Math.Round(fee * VipDiscount, 2) : fee;

    public static ReportSummary CreateReceipts(IEnumerable<PricedOrder> pricedOrders)
    {
        var receipts = pricedOrders
            .OrderBy(p => p.Order.OrderId, StringComparer.Ordinal)
            .Select(ToReceipt)
            .ToImmutableList();

        return receipts.IsEmpty
            ? ReportSummary.Empty
            : new ReportSummary(receipts, receipts.Sum(r => r.GrandTotal), Math.Round(receipts.Average(r => r.DeliveryFee), 2));
    }

    public static ReportSummary RunWorkflow(IEnumerable<Order> orders, DeliverySettings settings, Func<Order, decimal> feePolicy) =>
        orders
            .Where(o => o.WeightKg <= settings.MaxWeightKg)
            .Pipe(o => SelectServiceable(o, settings.MaxDistanceKm))
            .Pipe(o => CalculateDelivery(o, feePolicy))
            .Pipe(CreateReceipts);

    public static OrderReceipt ToReceipt(PricedOrder priced) =>
        new(priced.Order.OrderId, priced.Order.ItemTotal, priced.DeliveryFee, priced.Order.ItemTotal + priced.DeliveryFee);

    public static Result<Order> FindRequired(IEnumerable<Order> orders, string orderId) =>
        OrderLookup.FindById(orders, orderId).ToResult($"Order '{orderId}' was not found");

    public static Result<Order> CheckWeight(Order order, decimal maxWeightKg) =>
        order.WeightKg <= maxWeightKg
            ? new Success<Order>(order)
            : new Failure<Order>($"Order {order.OrderId}: weight {order.WeightKg} kg exceeds limit {maxWeightKg} kg");

    public static Result<OrderReceipt> QuoteOrder(IEnumerable<Order> orders, string orderId, DeliverySettings settings, Func<Order, decimal> feePolicy) =>
        FindRequired(orders, orderId)
            .Bind(order => Quote(order, settings, feePolicy));

    public static Result<OrderReceipt> Quote(Order order, DeliverySettings settings, Func<Order, decimal> feePolicy) =>
        CheckWeight(order, settings.MaxWeightKg)
            .Map(o => CheckEligibility(o, settings.MaxDistanceKm))
            .Bind(eligibility => ProcessEligibility(eligibility, feePolicy))
            .Map(ToReceipt);

    public static IEnumerable<Result<QuotedOrder>> QuoteAll(IEnumerable<Order> orders, DeliverySettings settings, Func<Order, decimal> feePolicy, Func<decimal, Result<int>> zoneOf)
    {
        foreach (var order in orders)
            yield return Quote(order, settings, feePolicy)
                .Bind(receipt => zoneOf(order.DistanceKm)
                .Map(zone => new QuotedOrder(order, receipt, zone)));
    }
}
