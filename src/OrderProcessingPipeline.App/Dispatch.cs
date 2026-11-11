using System.Collections.Immutable;

namespace OrderProcessingPipeline.App;

public abstract record DeliveryStatus(Order Order);
public sealed record Waiting(Order Order) : DeliveryStatus(Order);
public sealed record OnTheWay(Order Order, Courier Courier) : DeliveryStatus(Order);
public sealed record Delivered(Order Order, Courier Courier) : DeliveryStatus(Order);
public sealed record Cancelled(Order Order, string Reason) : DeliveryStatus(Order);

public abstract record DispatchCommand(string OrderId);
public sealed record AssignCommand(string OrderId, string CourierId) : DispatchCommand(OrderId);
public sealed record DeliverCommand(string OrderId) : DispatchCommand(OrderId);
public sealed record CancelCommand(string OrderId, string Reason) : DispatchCommand(OrderId);

public static class Dispatch
{
    public static string StateName(DeliveryStatus status) =>
        status switch
        {
            Waiting => "Waiting",
            OnTheWay => "OnTheWay",
            Delivered => "Delivered",
            Cancelled => "Cancelled",
            _ => throw new ArgumentException("Unknown status", nameof(status))
        };

    public static Result<DeliveryStatus> Assign(DeliveryStatus status, Courier courier) =>
        status switch
        {
            Waiting w when w.Order.WeightKg > courier.MaxWeightKg =>
                new Failure<DeliveryStatus>($"Courier {courier.CourierId} can carry {courier.MaxWeightKg} kg, order {w.Order.OrderId} is {w.Order.WeightKg} kg"),
            Waiting w => new Success<DeliveryStatus>(new OnTheWay(w.Order, courier)),
            _ => new Failure<DeliveryStatus>($"Cannot assign: order {status.Order.OrderId} is {StateName(status)}")
        };

    public static Result<DeliveryStatus> Deliver(DeliveryStatus status) =>
        status switch
        {
            OnTheWay o => new Success<DeliveryStatus>(new Delivered(o.Order, o.Courier)),
            _ => new Failure<DeliveryStatus>($"Cannot deliver: order {status.Order.OrderId} is {StateName(status)}")
        };

    public static Result<DeliveryStatus> Cancel(DeliveryStatus status, string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return new Failure<DeliveryStatus>("Cancel reason is required");

        return status switch
        {
            Waiting or OnTheWay => new Success<DeliveryStatus>(new Cancelled(status.Order, reason.Trim())),
            _ => new Failure<DeliveryStatus>($"Cannot cancel: order {status.Order.OrderId} is {StateName(status)}")
        };
    }

    public static ImmutableDictionary<string, DeliveryStatus> NewBoard(IEnumerable<Order> orders) =>
        orders.ToImmutableDictionary(o => o.OrderId, o => (DeliveryStatus)new Waiting(o));

    public static Result<ImmutableDictionary<string, DeliveryStatus>> Apply(
        ImmutableDictionary<string, DeliveryStatus> board,
        IEnumerable<Courier> couriers,
        DispatchCommand command) =>
        FindStatus(board, command.OrderId)
            .Bind(status => Run(status, couriers, command))
            .Map(next => board.SetItem(command.OrderId, next));

    private static Result<DeliveryStatus> FindStatus(ImmutableDictionary<string, DeliveryStatus> board, string orderId) =>
        board.TryGetValue(orderId, out var status)
            ? new Success<DeliveryStatus>(status)
            : new Failure<DeliveryStatus>($"Order '{orderId}' is not on the dispatch board");

    private static Result<DeliveryStatus> Run(DeliveryStatus status, IEnumerable<Courier> couriers, DispatchCommand command) =>
        command switch
        {
            AssignCommand a => CourierParser.FindById(couriers, a.CourierId)
                .ToResult($"Courier '{a.CourierId}' was not found")
                .Bind(courier => Assign(status, courier)),
            DeliverCommand => Deliver(status),
            CancelCommand c => Cancel(status, c.Reason),
            _ => new Failure<DeliveryStatus>("Unknown command")
        };

    public static IEnumerable<string> StatusCsv(ImmutableDictionary<string, DeliveryStatus> board) =>
        new[] { "OrderId,Status,CourierId,Reason" }
            .Concat(board.Values
                .OrderBy(s => s.Order.OrderId, StringComparer.Ordinal)
                .Select(s => s switch
                {
                    OnTheWay o => $"{o.Order.OrderId},OnTheWay,{o.Courier.CourierId},",
                    Delivered d => $"{d.Order.OrderId},Delivered,{d.Courier.CourierId},",
                    Cancelled c => $"{c.Order.OrderId},Cancelled,,{c.Reason}",
                    _ => $"{s.Order.OrderId},Waiting,,"
                }));
}
