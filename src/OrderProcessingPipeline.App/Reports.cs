using System.Collections.Immutable;

namespace OrderProcessingPipeline.App;

public sealed record QuotedOrder(Order Order, OrderReceipt Receipt, int Zone);

public sealed record ExpensiveRow(string OrderId, CustomerTier Tier, decimal DeliveryFee, decimal GrandTotal);

public sealed record ZoneRow(int Zone, int OrderCount, decimal TotalDeliveryFee);

public static class Reports
{
    public static Func<QuotedOrder, bool> FeeAtLeast(decimal minFee) =>
        quote => quote.Receipt.DeliveryFee >= minFee;

    public static ImmutableList<ExpensiveRow> ExpensiveDeliveries(IEnumerable<QuotedOrder> quotes, Func<QuotedOrder, bool> isExpensive) =>
        quotes
            .Where(isExpensive)
            .Select(q => new ExpensiveRow(q.Order.OrderId, q.Order.Tier, q.Receipt.DeliveryFee, q.Receipt.GrandTotal))
            .OrderByDescending(r => r.DeliveryFee)
            .ThenBy(r => r.OrderId, StringComparer.Ordinal)
            .ToImmutableList();

    public static ImmutableList<ZoneRow> TotalsByZone(IEnumerable<QuotedOrder> quotes) =>
        quotes
            .GroupBy(q => q.Zone)
            .Select(g => new ZoneRow(g.Key, g.Count(), g.Sum(q => q.Receipt.DeliveryFee)))
            .OrderBy(r => r.Zone)
            .ToImmutableList();
}
