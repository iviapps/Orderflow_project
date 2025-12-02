namespace Orderflow.Shared.Events
{
    public interface IIntegrationEvent
    {
        Guid EventId { get; }
        DateTime Timestamp { get; }
    }
}