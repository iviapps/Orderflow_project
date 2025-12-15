using MassTransit;

using Orderflow.Notifications.Consumers;
using Orderflow.Notifications.Services;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// DI: tu interfaz, tu implementación
builder.Services.AddScoped<IEmailService, EmailService>();

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<UserRegisteredConsumer>();
    x.AddConsumer<OrderCreatedConsumer>();
    x.AddConsumer<OrderCancelledConsumer>();

    x.UsingRabbitMq((context, cfg) =>
    {
        var configuration = context.GetRequiredService<IConfiguration>();
        var connectionString = configuration.GetConnectionString("messaging");

        if (!string.IsNullOrEmpty(connectionString))
            cfg.Host(new Uri(connectionString));

        cfg.UseMessageRetry(r => r.Intervals(
            TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30)));

        cfg.ConfigureEndpoints(context);
    });
});

var host = builder.Build();
host.Run();
