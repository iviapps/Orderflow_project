
namespace Orderflow.Shared.Events
{
    public sealed record OrderCreatedEvent(
        int OrderId,
        string UserId,
        IEnumerable<OrderItemEvent> Items) : IIntegrationEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    }

    public sealed record OrderItemEvent(
        int ProductId,
        string ProductName,
        int Quantity);
}