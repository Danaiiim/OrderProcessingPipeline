namespace OrderProcessingPipeline.App;

public static class OrderLookup
{
    public static Maybe<Order> FindById(IEnumerable<Order> orders, string orderId)
    {
        foreach (var order in orders)
            if (order.OrderId == orderId)
                return new Some<Order>(order);

        return new None<Order>();
    }
}
