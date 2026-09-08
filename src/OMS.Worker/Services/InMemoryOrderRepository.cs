using System.Collections.Concurrent;
using OMS.Worker.Models;

namespace OMS.Worker.Services;

public sealed class InMemoryOrderRepository : IOrderRepository
{
    private readonly ConcurrentDictionary<string, OrderStatusView> orders = new();

    public void Save(OrderStatusView order) => orders[order.OrderId] = order;

    public OrderStatusView? Get(string orderId) =>
        orders.TryGetValue(orderId, out var order) ? order : null;
}
