using System.Collections.Immutable;
using OrderProcessingPipeline.App;

namespace OrderProcessingPipeline.Tests;

public class ErrorHandlingTests
{
    private static readonly DeliverySettings Settings = new(50m, 30m);

    private static Order MakeOrder(string id, CustomerTier tier, decimal dist, decimal total, decimal weight) =>
        Assert.IsType<Success<Order>>(Order.Create(id, tier, dist, total, weight)).Value;

    private static readonly Order[] Orders =
    {
        MakeOrder("ORD-002", CustomerTier.Vip, 10m, 8000m, 3m),
        MakeOrder("ORD-004", CustomerTier.Regular, 52m, 3000m, 2m),
        MakeOrder("ORD-010", CustomerTier.Regular, 0m, 0m, 1m),
        MakeOrder("ORD-011", CustomerTier.Regular, 8m, 7000m, 40m)
    };

    [Fact]
    public void CompleteSuccess_QuoteChainReturnsCorrectReceipt()
    {
        var result = Pipeline.QuoteOrder(Orders, "ORD-002", Settings, Policies.Standard);

        var receipt = Assert.IsType<Success<OrderReceipt>>(result).Value;
        Assert.Equal("ORD-002", receipt.OrderId);
        Assert.Equal(1240m, receipt.DeliveryFee);
        Assert.Equal(9240m, receipt.GrandTotal);
    }

    [Fact]
    public void DomainFailure_OrderNotFound()
    {
        var result = Pipeline.QuoteOrder(Orders, "ORD-999", Settings, Policies.Standard);

        var failure = Assert.IsType<Failure<OrderReceipt>>(result);
        Assert.Equal("Order 'ORD-999' was not found", failure.Error);
    }

    [Fact]
    public void DomainFailure_WeightOverLimit()
    {
        var result = Pipeline.QuoteOrder(Orders, "ORD-011", Settings, Policies.Standard);

        var failure = Assert.IsType<Failure<OrderReceipt>>(result);
        Assert.Contains("weight 40 kg exceeds limit 30 kg", failure.Error);
    }

    [Fact]
    public void DomainFailure_DistanceRejectedByEligibility()
    {
        var result = Pipeline.QuoteOrder(Orders, "ORD-004", Settings, Policies.Standard);

        var failure = Assert.IsType<Failure<OrderReceipt>>(result);
        Assert.Contains("Rejected", failure.Error);
        Assert.Contains("ORD-004", failure.Error);
    }

    [Fact]
    public void Lookup_PresentAndAbsent_ZeroValuesAreNotAbsence()
    {
        var present = OrderLookup.FindById(Orders, "ORD-010");
        var absent = OrderLookup.FindById(Orders, "ORD-999");

        var found = Assert.IsType<Some<Order>>(present).Value;
        Assert.Equal(0m, found.DistanceKm);
        Assert.Equal(0m, found.ItemTotal);
        Assert.IsType<None<Order>>(absent);
    }

    [Fact]
    public void MapSuccess_TransformsValue()
    {
        Result<int> start = new Success<int>(5);

        var result = start.Map(x => x * 2);

        Assert.Equal(10, Assert.IsType<Success<int>>(result).Value);
    }

    [Fact]
    public void BindSuccess_KeepsResultOfNextOperation()
    {
        var order = Orders[3];

        var passed = Pipeline.FindRequired(Orders, "ORD-002").Bind(o => Pipeline.CheckWeight(o, 30m));
        var rejected = new Success<Order>(order).Bind(o => Pipeline.CheckWeight(o, 30m));

        Assert.Equal("ORD-002", Assert.IsType<Success<Order>>(passed).Value.OrderId);
        Assert.Contains("exceeds limit", Assert.IsType<Failure<Order>>(rejected).Error);
    }

    [Fact]
    public void FailurePropagation_MapAndBindDoNotCallFunctions()
    {
        var calls = 0;
        Result<int> failed = new Failure<int>("boom");

        var mapped = failed.Map(x => { calls++; return x * 2; });
        var bound = failed.Bind(x => { calls++; return (Result<int>)new Success<int>(x); });

        Assert.Equal(0, calls);
        Assert.Equal("boom", Assert.IsType<Failure<int>>(mapped).Error);
        Assert.Equal("boom", Assert.IsType<Failure<int>>(bound).Error);
    }

    [Fact]
    public void FailurePropagation_MissingOrderSkipsPricing()
    {
        var calls = 0;
        decimal CountingPolicy(Order o)
        {
            calls++;
            return Policies.Standard(o);
        }

        Pipeline.QuoteOrder(Orders, "ORD-999", Settings, CountingPolicy);
        Assert.Equal(0, calls);

        Pipeline.QuoteOrder(Orders, "ORD-002", Settings, CountingPolicy);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void MissingFile_ReportsProblemAndKeepsState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid()}.csv");
        var before = new OrderStore(Orders.ToImmutableList(), "test");

        var loaded = CsvLoader.Load(path);
        var (after, message) = OrderStore.Reload(before, path, loaded);

        Assert.Contains("was not found", Assert.IsType<Failure<ParsedInput>>(loaded).Error);
        Assert.Same(before, after);
        Assert.Contains("LOAD FAILED", message);
    }

    [Fact]
    public void MalformedFile_FailsWithoutPartialUpdate()
    {
        var path = Path.Combine(Path.GetTempPath(), $"malformed-{Guid.NewGuid()}.csv");
        File.WriteAllText(path, "Order;Tier\nORD-1;VIP\n");
        try
        {
            var before = new OrderStore(Orders.ToImmutableList(), "test");

            var loaded = CsvLoader.Load(path);
            var (after, _) = OrderStore.Reload(before, path, loaded);

            Assert.Contains("malformed content", Assert.IsType<Failure<ParsedInput>>(loaded).Error);
            Assert.Same(before, after);
            Assert.Equal(4, after.Orders.Count);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ValidFile_LoadsOrdersAndReplacesState()
    {
        var path = Path.Combine(Path.GetTempPath(), $"valid-{Guid.NewGuid()}.csv");
        File.WriteAllText(path, $"{OrderParser.Header}\nORD-1,VIP,5,1000,1\nORD-2,REGULAR,abc,1000,1\n");
        try
        {
            var loaded = CsvLoader.Load(path);
            var (after, _) = OrderStore.Reload(OrderStore.Empty, path, loaded);

            var input = Assert.IsType<Success<ParsedInput>>(loaded).Value;
            Assert.Single(input.Orders);
            Assert.Single(input.Errors);
            Assert.Equal("ORD-1", Assert.Single(after.Orders).OrderId);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
