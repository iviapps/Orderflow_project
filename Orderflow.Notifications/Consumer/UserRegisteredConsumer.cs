using MassTransit;
using NETCore.MailKit.Core;
using Orderflow.Shared.Events;


namespace OrderFlow.Notifications.Consumers;

public class UserRegisteredConsumer(
    IEmailService emailService,
    ILogger<UserRegisteredConsumer> logger) : IConsumer<UserRegisteredEvent>
{
    public async Task Consume(ConsumeContext<UserRegisteredEvent> context)
    {
        var @event = context.Message;

        logger.LogInformation(
            "Processing UserRegisteredEvent: EventId={EventId}, UserId={UserId}, Email={Email}",
            @event.EventId, @event.UserId, @event.Email);

        await emailService.SendWelcomeEmailAsync(
            @event.Email,
            @event.FirstName,
            context.CancellationToken);

        logger.LogInformation(
            "Successfully processed UserRegisteredEvent: EventId={EventId}",
            @event.EventId);
    }
}
