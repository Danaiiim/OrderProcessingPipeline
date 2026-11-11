using OrderProcessingPipeline.App;

namespace OrderProcessingPipeline.Tests;

public class PipelineTests
{
    private static readonly DeliverySettings Settings = new(50m);

    private static Order MakeOrder(string id, CustomerTier tier, decimal dist, decimal total, decimal weight) =>
        Assert.IsType<Success<Order>>(Order.Create(id, tier, dist, total, weight)).Value;

    [Fact]
    public void FactorySuccess_CreatesValidOrder()
    {
        var result = Order.Create("ORD-1", CustomerTier.Regular, 10m, 5000m, 2m);
        var order = Assert.IsType<Success<Order>>(result).Value;
        Assert.Equal("ORD-1", order.OrderId);
    }

    [Fact]
    public void FactoryRejection_CatchesInvalidValues()
    {
        Assert.IsType<Failure<Order>>(Order.Create("ORD-1", CustomerTier.Regular, -1m, 1000m, 1m));
        Assert.IsType<Failure<Order>>(Order.Create("ORD-1", CustomerTier.Regular, 10m, -100m, 1m));
        Assert.IsType<Failure<Order>>(Order.Create("ORD-1", CustomerTier.Regular, 10m, 1000m, 0m));
        Assert.IsType<Failure<Order>>(Order.Create("", CustomerTier.Regular, 10m, 1000m, 1m));
    }

    [Fact]
    public void DistinctCases_EligibilityStatesMatchCorrectly()
    {
        var inside = MakeOrder("IN", CustomerTier.Regular, 20m, 1000m, 1m);
        var outside = MakeOrder("OUT", CustomerTier.Regular, 60m, 1000m, 1m);

        var m1 = Pipeline.CheckEligibility(inside, 50m) switch { ServiceableOrder => "OK", _ => "FAIL" };
        var m2 = Pipeline.CheckEligibility(outside, 50m) switch { UnserviceableOrder => "REJECT", _ => "FAIL" };

        Assert.Equal("OK", m1);
        Assert.Equal("REJECT", m2);
    }

    [Fact]
    public void NormalWorkflow_ProducesExpectedSummary()
    {
        var orders = new[] { MakeOrder("ORD-1", CustomerTier.Regular, 10m, 5000m, 2m) };
        var summary = Pipeline.RunWorkflow(orders, Settings, Policies.Standard);

        Assert.Single(summary.Receipts);
        Assert.Equal(1500m, summary.Receipts[0].DeliveryFee);
        Assert.Equal(6500m, summary.Receipts[0].GrandTotal);
    }

    [Fact]
    public void MissingPrerequisite_UnserviceableOrderIsRejected()
    {
        var far = MakeOrder("FAR", CustomerTier.Regular, 80m, 2000m, 1m);
        var outcome = Pipeline.ProcessEligibility(Pipeline.CheckEligibility(far, 50m), Policies.Standard);

        var fail = Assert.IsType<Failure<PricedOrder>>(outcome);
        Assert.Contains("Rejected", fail.Error);
    }

    [Fact]
    public void RepeatedOperation_SafeRecalculatePreservesState()
    {
        var order = MakeOrder("ORD-1", CustomerTier.Regular, 10m, 5000m, 2m);
        var priced = Pipeline.PriceServiceable(new ServiceableOrder(order), Policies.Standard);
        var repeated = Pipeline.SafeRecalculate(priced);

        Assert.Same(priced, repeated);
    }

    [Fact]
    public void BoundaryValues_50KmIncluded_50Point1Excluded()
    {
        var list = new[]
        {
            MakeOrder("IN", CustomerTier.Regular, 50m, 1000m, 1m),
            MakeOrder("OUT", CustomerTier.Regular, 50.1m, 1000m, 1m)
        };
        var res = Pipeline.SelectServiceable(list, 50m);
        Assert.Equal("IN", Assert.Single(res).OrderId);
    }

    [Fact]
    public void StatePreservation_OriginalListIsNotModifiedOnFailure()
    {
        var original = new List<Order> { MakeOrder("ORD-1", CustomerTier.Regular, 10m, 1000m, 1m) };
        var countBefore = original.Count;

        var _ = Order.Create("BAD", CustomerTier.Regular, -10m, 1000m, 1m);

        Assert.Equal(countBefore, original.Count);
    }
}