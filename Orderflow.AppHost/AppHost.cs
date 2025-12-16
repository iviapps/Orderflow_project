using EnvDTE;

var builder = DistributedApplication.CreateBuilder(args);

// ============================================
// INFRASTRUCTURE
// ============================================

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("Orderflow-postgres-data")
    .WithPgAdmin()
    .WithLifetime(ContainerLifetime.Persistent);

var identityDb = postgres.AddDatabase("identitydb");
var catalogDb = postgres.AddDatabase("catalogdb");
var ordersDb = postgres.AddDatabase("ordersdb");

var redis = builder.AddRedis("cache")
    .WithDataVolume("Orderflow-redis-data")
    .WithLifetime(ContainerLifetime.Persistent);

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithDataVolume("Orderflow-rabbitmq-data")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

var maildev = builder.AddContainer("maildev", "maildev/maildev")
    .WithHttpEndpoint(port: 1080, targetPort: 1080, name: "web")
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp")
    .WithLifetime(ContainerLifetime.Persistent);

// ============================================
// MICROSERVICES - SIN WithHttpsEndpoint
// ============================================
var identityService = builder.AddProject<Projects.Orderflow_Identity>("orderflow-identity")
    .WithReference(identityDb)
    .WithReference(rabbitmq)
    .WaitFor(identityDb)
    .WaitFor(rabbitmq);

var catalogService = builder.AddProject<Projects.Orderflow_Catalog>("orderflow-catalog")
    .WithReference(catalogDb)
    .WaitFor(catalogDb);

var notificationsService = builder.AddProject<Projects.Orderflow_Notifications>("Orderflow-notifications")
    .WithReference(rabbitmq)
    .WithEnvironment("Email__SmtpHost", maildev.GetEndpoint("smtp").Property(EndpointProperty.Host))
    .WithEnvironment("Email__SmtpPort", maildev.GetEndpoint("smtp").Property(EndpointProperty.Port))
    .WaitFor(rabbitmq);

var ordersService = builder.AddProject<Projects.Orderflow_Orders>("Orderflow-orders")
    .WithReference(ordersDb)
    .WithReference(rabbitmq)
    .WithReference(catalogService)
    .WaitFor(ordersDb)
    .WaitFor(rabbitmq);

// ============================================
// API GATEWAY
// ============================================
var apiGateway = builder.AddProject<Projects.Orderflow_ApiGateway>("orderflow-apigateway")
    .WithReference(redis)
    .WithReference(identityService)
    .WithReference(catalogService)
    .WithReference(ordersService)
    .WaitFor(identityService)
    .WaitFor(catalogService)
    .WaitFor(ordersService);

// ============================================
// FRONTEND - React App
// ============================================
var frontendApp = builder.AddNpmApp("Orderflow-web", "../Orderflow.web", "dev")
    .WithReference(apiGateway)
    .WithEnvironment("VITE_API_GATEWAY_URL", apiGateway.GetEndpoint("https"))
    .WithHttpEndpoint(env: "VITE_PORT")
    .WaitFor(apiGateway)
    .WithExternalHttpEndpoints()
    .PublishAsDockerFile();

builder.Build().Run();