using Orderflow.Shared.Events;

namespace Orderflow.Shared.Events;
public sealed record OrderCancelledEvent(
    int OrderId,
    string UserId,
    IEnumerable<OrderItemEvent> Items) : IIntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
