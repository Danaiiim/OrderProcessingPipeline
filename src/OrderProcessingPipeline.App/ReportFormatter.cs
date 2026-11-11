using System.Globalization;
using System.Text;

namespace OrderProcessingPipeline.App;

public static class ReportFormatter
{
    public static string FormatSummary(string policyName, ReportSummary summary)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Policy: {policyName} ===");
        if (summary.Receipts.IsEmpty)
        {
            sb.AppendLine("No orders to deliver.");
            return sb.ToString();
        }
        foreach (var r in summary.Receipts)
            sb.AppendLine($"  {FormatReceipt(r)}");
        sb.AppendLine($"Total sum: {Money(summary.TotalSum)} | Average delivery: {Money(summary.AverageDelivery)}");
        return sb.ToString();
    }

    public static string FormatInput(ParsedInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Valid orders: {input.Orders.Count}");
        if (input.Errors.IsEmpty)
        {
            sb.AppendLine("Rejected: none");
            return sb.ToString();
        }
        sb.AppendLine("Rejected:");
        foreach (var error in input.Errors)
            sb.AppendLine($"  {error}");
        return sb.ToString();
    }

    public static string FormatExpensive(string title, IReadOnlyList<ExpensiveRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine(title);
        if (rows.Count == 0)
        {
            sb.AppendLine("  no orders");
            return sb.ToString();
        }
        foreach (var r in rows)
            sb.AppendLine($"  {r.OrderId} ({r.Tier}): delivery {Money(r.DeliveryFee)} | total {Money(r.GrandTotal)}");
        return sb.ToString();
    }

    public static string FormatZones(IReadOnlyList<ZoneRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Delivery by zone:");
        if (rows.Count == 0)
        {
            sb.AppendLine("  no orders");
            return sb.ToString();
        }
        foreach (var r in rows)
            sb.AppendLine($"  zone {r.Zone}: {r.OrderCount} orders | delivery total {Money(r.TotalDeliveryFee)}");
        return sb.ToString();
    }

    public static IEnumerable<string> ZonesCsv(IEnumerable<ZoneRow> rows) =>
        new[] { "Zone,OrderCount,TotalDeliveryFee" }
            .Concat(rows.Select(r => $"{r.Zone},{r.OrderCount},{r.TotalDeliveryFee.ToString(CultureInfo.InvariantCulture)}"));

    public static string FormatReceipt(OrderReceipt r) =>
        $"{r.OrderId}: items {Money(r.ItemTotal)} | delivery {Money(r.DeliveryFee)} | total {Money(r.GrandTotal)}";

    private static string Money(decimal value) =>
        value.ToString("0.##", CultureInfo.InvariantCulture) + " KZT";
}