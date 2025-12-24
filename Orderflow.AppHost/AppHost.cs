
var builder = DistributedApplication.CreateBuilder(args);


var jwtSecret = builder.AddParameter("jwt-secret", secret: true);

// ============================================
// INFRASTRUCTURE
// ============================================

// PostgreSQL - Database for microservices
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("Orderflow-postgres-data")
    .WithPgAdmin()
    .WithHostPort(5432)
    .WithLifetime(ContainerLifetime.Persistent);

// Databases for microservices,
var identityDb = postgres.AddDatabase("identitydb");
var catalogDb = postgres.AddDatabase("catalogdb");
var ordersDb = postgres.AddDatabase("ordersdb");

// Redis - Distributed cache for rate limiting only
var redis = builder.AddRedis("cache")
    .WithDataVolume("Orderflow-redis-data")
    .WithHostPort(6379)
    .WithLifetime(ContainerLifetime.Persistent);

// RabbitMQ - Message broker for reliable event-driven communication
var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithDataVolume("Orderflow-rabbitmq-data")
    .WithManagementPlugin()
    .WithLifetime(ContainerLifetime.Persistent);

// MailDev - Local SMTP server for development (Web UI on 1080, SMTP on 1025)
var maildev = builder.AddContainer("maildev", "maildev/maildev")
    .WithHttpEndpoint(port: 1080, targetPort: 1080, name: "web")
    .WithEndpoint(port: 1025, targetPort: 1025, name: "smtp")
    .WithLifetime(ContainerLifetime.Persistent);

// ============================================
// MICROSERVICES
// ============================================
var identityService = builder.AddProject<Projects.Orderflow_Identity>("orderflow-identity")
    .WithReference(identityDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Jwt__Secret", jwtSecret) // primero
    .WaitFor(identityDb)
    .WaitFor(rabbitmq);


// Catalog Service - Products and Categories
var catalogService = builder.AddProject<Projects.Orderflow_Catalog>("orderflow-catalog")
    .WithReference(catalogDb)
    .WaitFor(catalogDb);
   


//// Notifications Worker - Listens to RabbitMQ events and sends emails
var notificationsService = builder.AddProject<Projects.Orderflow_Notifications>("Orderflow-notifications")
    .WithReference(rabbitmq)
    .WithEnvironment("Email__SmtpHost", maildev.GetEndpoint("smtp").Property(EndpointProperty.Host))
    .WithEnvironment("Email__SmtpPort", maildev.GetEndpoint("smtp").Property(EndpointProperty.Port))
    .WaitFor(rabbitmq);


//// Orders Service - Order management
var ordersService = builder.AddProject<Projects.Orderflow_Orders>("Orderflow-orders")
    .WithReference(ordersDb)
    .WithReference(rabbitmq)
    .WithReference(catalogService)
    .WaitFor(ordersDb)
    .WaitFor(rabbitmq);

//// ============================================
//// API GATEWAY
//// ============================================
// API Gateway acts as the single entry point for all client requests
// It handles authentication, authorization, rate limiting, and routes to microservices
var apiGateway = builder.AddProject<Projects.Orderflow_ApiGateway>("orderflow-apigateway")
    .WithReference(redis) // Redis for rate limiting and caching
    .WithReference(identityService)
    .WithReference(catalogService)
    .WithReference(ordersService)
    .WithEnvironment("Jwt__Secret", jwtSecret)
    .WaitFor(identityService)
    .WaitFor(catalogService)
    .WaitFor(ordersService);



//// ============================================
//// FRONTEND - React App
//// ============================================
// Frontend communicates ONLY with API Gateway (not directly with microservices)
var frontendApp = builder.AddNpmApp("Orderflow-web", "../Orderflow.web", "dev")
    .WithReference(apiGateway) // Frontend talks to Gateway, not to services directly
    .WithEnvironment("VITE_API_GATEWAY_URL", apiGateway.GetEndpoint("https")) // Gateway URL for frontend
    .WithHttpEndpoint(env: "VITE_PORT") // Vite uses VITE_PORT environment variable
    .WaitFor(apiGateway)
    .WithExternalHttpEndpoints() // Make endpoint accessible via Aspire dashboard
    .PublishAsDockerFile();



builder.Build().Run();