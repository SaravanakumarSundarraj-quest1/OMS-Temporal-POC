using OMS.Worker.Models;

namespace OMS.Worker.Services;

public interface IOrderRepository
{
    void Save(OrderStatusView order);

    OrderStatusView? Get(string orderId);
}