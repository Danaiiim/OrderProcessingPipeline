using System.Collections.Immutable;
using OrderProcessingPipeline.App;

namespace OrderProcessingPipeline.Tests;

public class DispatchTests
{
    private static Order MakeOrder(string id, decimal weight) =>
        Assert.IsType<Success<Order>>(Order.Create(id, CustomerTier.Regular, 5m, 1000m, weight)).Value;

    private static Courier MakeCourier(string id, decimal max) =>
        Assert.IsType<Success<Courier>>(Courier.Create(id, "Test", max)).Value;

    private static readonly Courier Big = MakeCourier("C1", 20m);
    private static readonly Courier Bike = MakeCourier("C2", 1m);
    private static readonly Courier[] Couriers = { Big, Bike };

    [Fact]
    public void CourierFactory_RejectsInvalidValues()
    {
        Assert.IsType<Success<Courier>>(Courier.Create(" C1 ", "Aidos", 20m));
        Assert.Contains("CourierId is missing", Assert.IsType<Failure<Courier>>(Courier.Create("", "Aidos", 20m)).Error);
        Assert.Contains("name is missing", Assert.IsType<Failure<Courier>>(Courier.Create("C1", " ", 20m)).Error);
        Assert.Contains("max weight", Assert.IsType<Failure<Courier>>(Courier.Create("C1", "Aidos", 0m)).Error);
    }

    [Fact]
    public void NormalSequence_WaitingThenOnTheWayThenDelivered()
    {
        DeliveryStatus status = new Waiting(MakeOrder("ORD-1", 3m));

        var onTheWay = Assert.IsType<OnTheWay>(Assert.IsType<Success<DeliveryStatus>>(Dispatch.Assign(status, Big)).Value);
        var delivered = Assert.IsType<Delivered>(Assert.IsType<Success<DeliveryStatus>>(Dispatch.Deliver(onTheWay)).Value);

        Assert.Equal("C1", delivered.Courier.CourierId);
        Assert.IsType<Waiting>(status);
    }

    [Fact]
    public void InvalidSequence_IsRejected()
    {
        var order = MakeOrder("ORD-1", 3m);

        Assert.Contains("Cannot deliver: order ORD-1 is Waiting", Assert.IsType<Failure<DeliveryStatus>>(Dispatch.Deliver(new Waiting(order))).Error);
        Assert.Contains("Cannot assign: order ORD-1 is Delivered", Assert.IsType<Failure<DeliveryStatus>>(Dispatch.Assign(new Delivered(order, Big), Big)).Error);
        Assert.Contains("Cannot cancel: order ORD-1 is Cancelled", Assert.IsType<Failure<DeliveryStatus>>(Dispatch.Cancel(new Cancelled(order, "x"), "again")).Error);
    }

    [Fact]
    public void Assign_TooHeavyForCourier_Fails()
    {
        var result = Dispatch.Assign(new Waiting(MakeOrder("ORD-1", 3m)), Bike);

        Assert.Contains("can carry 1 kg", Assert.IsType<Failure<DeliveryStatus>>(result).Error);
    }

    [Fact]
    public void Cancel_NeedsReasonAndTrimsIt()
    {
        var order = MakeOrder("ORD-1", 3m);

        Assert.Equal("Cancel reason is required", Assert.IsType<Failure<DeliveryStatus>>(Dispatch.Cancel(new Waiting(order), " ")).Error);
        var cancelled = Assert.IsType<Cancelled>(Assert.IsType<Success<DeliveryStatus>>(Dispatch.Cancel(new Waiting(order), " no time ")).Value);
        Assert.Equal("no time", cancelled.Reason);
    }

    [Fact]
    public void Apply_FailedCommand_KeepsBoardUnchanged()
    {
        var board = Dispatch.NewBoard(new[] { MakeOrder("ORD-1", 3m) });

        var unknownOrder = Dispatch.Apply(board, Couriers, new AssignCommand("ORD-9", "C1"));
        var unknownCourier = Dispatch.Apply(board, Couriers, new AssignCommand("ORD-1", "C9"));
        var tooEarly = Dispatch.Apply(board, Couriers, new DeliverCommand("ORD-1"));

        Assert.Contains("not on the dispatch board", Assert.IsType<Failure<ImmutableDictionary<string, DeliveryStatus>>>(unknownOrder).Error);
        Assert.Contains("Courier 'C9' was not found", Assert.IsType<Failure<ImmutableDictionary<string, DeliveryStatus>>>(unknownCourier).Error);
        Assert.IsType<Failure<ImmutableDictionary<string, DeliveryStatus>>>(tooEarly);
        Assert.IsType<Waiting>(board["ORD-1"]);
    }

    [Fact]
    public void Apply_WorkflowAndStatusCsv()
    {
        var board = Dispatch.NewBoard(new[] { MakeOrder("ORD-1", 3m), MakeOrder("ORD-2", 3m) });

        board = Assert.IsType<Success<ImmutableDictionary<string, DeliveryStatus>>>(Dispatch.Apply(board, Couriers, new AssignCommand("ORD-1", "C1"))).Value;
        board = Assert.IsType<Success<ImmutableDictionary<string, DeliveryStatus>>>(Dispatch.Apply(board, Couriers, new DeliverCommand("ORD-1"))).Value;
        board = Assert.IsType<Success<ImmutableDictionary<string, DeliveryStatus>>>(Dispatch.Apply(board, Couriers, new CancelCommand("ORD-2", "closed"))).Value;

        Assert.Equal(
            new[] { "OrderId,Status,CourierId,Reason", "ORD-1,Delivered,C1,", "ORD-2,Cancelled,,closed" },
            Dispatch.StatusCsv(board).ToArray());
    }

    [Fact]
    public void CourierFile_MissingAndMalformed_AreFailures()
    {
        var missing = Path.Combine(Path.GetTempPath(), $"couriers-{Guid.NewGuid()}.csv");
        var bad = Path.Combine(Path.GetTempPath(), $"couriers-{Guid.NewGuid()}.csv");
        File.WriteAllText(bad, $"{CourierParser.Header}\nC1,Aidos,heavy\n");
        try
        {
            Assert.Contains("was not found", Assert.IsType<Failure<ImmutableList<Courier>>>(CourierParser.Load(missing)).Error);
            Assert.Contains("Courier line 2: MaxWeightKg is not a number", Assert.IsType<Failure<ImmutableList<Courier>>>(CourierParser.Load(bad)).Error);
        }
        finally
        {
            File.Delete(bad);
        }
    }

    [Fact]
    public void CourierLookup_PresentAndAbsent()
    {
        Assert.Equal("C2", Assert.IsType<Some<Courier>>(CourierParser.FindById(Couriers, "C2")).Value.CourierId);
        Assert.IsType<None<Courier>>(CourierParser.FindById(Couriers, "C9"));
    }
}
