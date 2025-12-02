using MassTransit;
using Orderflow.Notifications.Consumers;
using Orderflow.Notifications.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Register services
builder.Services.AddScoped<IEmailService, EmailService>();

// Configure MassTransit with RabbitMQ
builder.Services.AddMassTransit(x =>
{
//    // Register consumers - BY THIS DAY IS NOT IMPLEMENTED. 
//    x.AddConsumer<UserRegisteredConsumer>();
//    x.AddConsumer<OrderCreatedConsumer>();
//    x.AddConsumer<OrderCancelledConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        // Use Aspire service discovery for RabbitMQ connection
        var configuration = context.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("messaging");

        if (!string.IsNullOrEmpty(connectionString))
        {
            cfg.Host(new Uri(connectionString));
        }

        // Configure retry policy
        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30)));

        // Configure endpoints for all consumers
        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
