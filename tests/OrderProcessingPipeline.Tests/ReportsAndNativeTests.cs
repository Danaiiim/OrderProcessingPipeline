using System.Collections.Immutable;
using OrderProcessingPipeline.App;

namespace OrderProcessingPipeline.Tests;

public class ReportsAndNativeTests
{
    private static readonly DeliverySettings Settings = new(50m, 30m);

    private static Order MakeOrder(string id, CustomerTier tier, decimal dist, decimal total, decimal weight) =>
        Assert.IsType<Success<Order>>(Order.Create(id, tier, dist, total, weight)).Value;

    private static QuotedOrder MakeQuote(string id, decimal fee, int zone) =>
        new(MakeOrder(id, CustomerTier.Regular, 5m, 1000m, 1m), new OrderReceipt(id, 1000m, fee, 1000m + fee), zone);

    private static readonly QuotedOrder[] Quotes =
    {
        MakeQuote("ORD-B", 1240m, 1),
        MakeQuote("ORD-A", 1240m, 1),
        MakeQuote("ORD-C", 5500m, 3),
        MakeQuote("ORD-D", 500m, 1),
        MakeQuote("ORD-E", 2400m, 2)
    };

    private static Result<int> FakeZone(decimal distanceKm) => new Success<int>(1);

    [Fact]
    public void ExpensiveReport_FiltersAndOrdersWithTieBreak()
    {
        var rows = Reports.ExpensiveDeliveries(Quotes, Reports.FeeAtLeast(1000m));

        Assert.Equal(new[] { "ORD-C", "ORD-E", "ORD-A", "ORD-B" }, rows.Select(r => r.OrderId));
        Assert.Equal(6500m, rows[0].GrandTotal);
    }

    [Fact]
    public void ExpensiveReport_EmptyInputAndNoMatches()
    {
        Assert.Empty(Reports.ExpensiveDeliveries(Array.Empty<QuotedOrder>(), Reports.FeeAtLeast(1000m)));
        Assert.Empty(Reports.ExpensiveDeliveries(Quotes, Reports.FeeAtLeast(10000m)));
    }

    [Fact]
    public void ZoneReport_GroupsCountsAndSums()
    {
        var rows = Reports.TotalsByZone(Quotes);

        Assert.Equal(new[] { 1, 2, 3 }, rows.Select(r => r.Zone));
        Assert.Equal(new ZoneRow(1, 3, 2980m), rows[0]);
        Assert.Equal(new ZoneRow(2, 1, 2400m), rows[1]);
        Assert.Equal(new ZoneRow(3, 1, 5500m), rows[2]);
        Assert.Empty(Reports.TotalsByZone(Array.Empty<QuotedOrder>()));
    }

    [Fact]
    public void ConfiguredFilters_GiveDifferentResultsOnSameData()
    {
        var over1000 = Reports.FeeAtLeast(1000m);
        var over3000 = Reports.FeeAtLeast(3000m);

        Assert.Equal(4, Reports.ExpensiveDeliveries(Quotes, over1000).Count);
        Assert.Equal("ORD-C", Assert.Single(Reports.ExpensiveDeliveries(Quotes, over3000)).OrderId);
        Assert.True(over1000(Quotes[0]));
        Assert.False(over3000(Quotes[0]));
    }

    [Fact]
    public void Extensions_SplitSuccessesAndErrors()
    {
        var results = new Result<int>[] { new Success<int>(1), new Failure<int>("bad"), new Success<int>(0) };

        Assert.Equal(new[] { 1, 0 }, results.Successes());
        Assert.Equal(new[] { "bad" }, results.Errors());
        Assert.Empty(Array.Empty<Result<int>>().Successes());
    }

    [Fact]
    public void DeferredQuoteAll_RunsOnlyWhenEnumerated_SnapshotIsReused()
    {
        var calls = 0;
        decimal CountingPolicy(Order o)
        {
            calls++;
            return Policies.Standard(o);
        }
        var orders = new[]
        {
            MakeOrder("ORD-1", CustomerTier.Regular, 5m, 1000m, 1m),
            MakeOrder("ORD-2", CustomerTier.Vip, 20m, 1000m, 1m)
        };

        var lazy = Pipeline.QuoteAll(orders, Settings, CountingPolicy, FakeZone);
        Assert.Equal(0, calls);

        var snapshot = lazy.ToList();
        Assert.Equal(2, calls);

        _ = snapshot.Successes().Count();
        _ = snapshot.Errors().Count();
        Assert.Equal(2, calls);

        _ = lazy.ToList();
        Assert.Equal(4, calls);
    }

    [Fact]
    public void Native_RealCall_ReturnsExpectedZones()
    {
        Assert.Equal(1, Assert.IsType<Success<int>>(DeliveryZones.GetZone(0m)).Value);
        Assert.Equal(1, Assert.IsType<Success<int>>(DeliveryZones.GetZone(10m)).Value);
        Assert.Equal(2, Assert.IsType<Success<int>>(DeliveryZones.GetZone(25m)).Value);
        Assert.Equal(3, Assert.IsType<Success<int>>(DeliveryZones.GetZone(50m)).Value);
    }

    [Fact]
    public void Native_InvalidInputAndLoadFailure_BecomeFailures()
    {
        var calls = 0;
        int Spy(int distanceM)
        {
            calls++;
            return 1;
        }

        var tooFar = DeliveryZones.GetZone(60m, Spy);
        var notLoaded = DeliveryZones.GetZone(5m, _ => throw new DllNotFoundException());

        Assert.Contains("outside 0-50 km", Assert.IsType<Failure<int>>(tooFar).Error);
        Assert.Equal(0, calls);
        Assert.Equal("Native library delivery_zone was not found", Assert.IsType<Failure<int>>(notLoaded).Error);
    }

    [Fact]
    public void Workflow_FileToReportsWithNativeZones()
    {
        var path = Path.Combine(Path.GetTempPath(), $"orders-{Guid.NewGuid()}.csv");
        File.WriteAllText(path, $"{OrderParser.Header}\nORD-1,REGULAR,5,1000,1\nORD-2,VIP,25,6000,2\nORD-3,REGULAR,52,1000,1\n");
        try
        {
            var store = OrderStore.Reload(OrderStore.Empty, path, CsvLoader.Load(path)).Store;
            var quotes = Pipeline.QuoteAll(store.Orders, Settings, Policies.Standard, DeliveryZones.GetZone).ToList();
            var zones = Reports.TotalsByZone(quotes.Successes());

            Assert.Single(quotes.Errors());
            Assert.Equal(new ZoneRow(1, 1, 1000m), zones[0]);
            Assert.Equal(new ZoneRow(2, 1, 2400m), zones[1]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Workflow_NativeFailure_ReportsProblemAndKeepsState()
    {
        var store = new OrderStore(new[] { MakeOrder("ORD-1", CustomerTier.Regular, 5m, 1000m, 1m) }.ToImmutableList(), "test");
        Result<int> Broken(decimal d) => DeliveryZones.GetZone(d, _ => throw new DllNotFoundException());

        var quotes = Pipeline.QuoteAll(store.Orders, Settings, Policies.Standard, Broken).ToList();

        Assert.Empty(Reports.TotalsByZone(quotes.Successes()));
        Assert.Equal("Native library delivery_zone was not found", Assert.Single(quotes.Errors()));
        Assert.Single(store.Orders);
    }

    [Fact]
    public void Export_Success_WritesFileAndReleasesIt()
    {
        var path = Path.Combine(Path.GetTempPath(), $"zones-{Guid.NewGuid()}.csv");
        try
        {
            var result = ReportExporter.Save(path, ReportFormatter.ZonesCsv(new[] { new ZoneRow(1, 2, 1500m) }));

            Assert.IsType<Success<string>>(result);
            Assert.Equal(new[] { "Zone,OrderCount,TotalDeliveryFee", "1,2,1500" }, File.ReadAllLines(path));
            using var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Export_FailureDuringWrite_LeavesNoPartialFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"zones-{Guid.NewGuid()}.csv");
        IEnumerable<string> BrokenLines()
        {
            yield return "Zone,OrderCount,TotalDeliveryFee";
            throw new IOException("disk error");
        }

        var result = ReportExporter.Save(path, BrokenLines());

        Assert.Contains("disk error", Assert.IsType<Failure<string>>(result).Error);
        Assert.False(File.Exists(path));
        Assert.False(File.Exists(path + ".tmp"));
    }
}
