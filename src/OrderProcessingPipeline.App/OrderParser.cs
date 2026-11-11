using System.Collections.Immutable;
using System.Globalization;

namespace OrderProcessingPipeline.App;

public static class OrderParser
{
    public const string Header = "OrderId,CustomerTier,DistanceKm,ItemTotal,WeightKg";

    public static Result<ParsedInput> Parse(IReadOnlyList<string>? lines)
    {
        if (lines is null) return new Failure<ParsedInput>("Input is missing");
        if (lines.Count == 0) return new Failure<ParsedInput>("Input has no header line");
        if (lines[0].Trim() != Header) return new Failure<ParsedInput>($"Wrong header. Expected: {Header}");

        var results = lines.Skip(1).Select((line, i) => ParseLine(line, i + 2)).ToList();
        return new Success<ParsedInput>(new ParsedInput(
            results.OfType<Success<Order>>().Select(s => s.Value).ToImmutableList(),
            results.OfType<Failure<Order>>().Select(f => f.Error).ToImmutableList()));
    }

    public static Result<Order> ParseLine(string? line, int lineNumber) =>
        SplitFields(line)
            .Bind(f => ParseId(f[0])
                .Bind(id => ParseTier(f[1])
                .Bind(tier => ParseDecimal(f[2], "DistanceKm")
                .Bind(dist => ParseDecimal(f[3], "ItemTotal")
                .Bind(total => ParseDecimal(f[4], "WeightKg")
                .Bind(weight => Order.Create(id, tier, dist, total, weight)))))))
            .Match<Order, Result<Order>>(
                order => new Success<Order>(order),
                error => new Failure<Order>($"Line {lineNumber}: {error}"));

    private static Result<string[]> SplitFields(string? line) =>
        string.IsNullOrWhiteSpace(line)
            ? new Failure<string[]>("Line is empty")
            : line.Split(',').Select(f => f.Trim()).ToArray() is { Length: 5 } fields
                ? new Success<string[]>(fields)
                : new Failure<string[]>($"Expected 5 fields, got {line?.Split(',').Length}");

    private static Result<string> ParseId(string text) =>
        text.Length > 0 ? new Success<string>(text) : new Failure<string>("OrderId is missing");

    private static Result<CustomerTier> ParseTier(string text) =>
        text.ToUpperInvariant() switch
        {
            "REGULAR" => new Success<CustomerTier>(CustomerTier.Regular),
            "VIP" => new Success<CustomerTier>(CustomerTier.Vip),
            "" => new Failure<CustomerTier>("CustomerTier is missing"),
            _ => new Failure<CustomerTier>($"CustomerTier must be REGULAR or VIP, got '{text}'")
        };

    private static Result<decimal> ParseDecimal(string text, string field) =>
        text.Length == 0
            ? new Failure<decimal>($"{field} is missing")
            : decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var v)
                ? new Success<decimal>(v)
                : new Failure<decimal>($"{field} is not a number: '{text}'");
}