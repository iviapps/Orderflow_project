using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

// 1) Servidor Postgres con volumen de datos (persistencia real)
var postgres = builder.AddPostgres("postgres")
    .WithLifetime(ContainerLifetime.Persistent);


//en el castor pone que no existe 
// 2) Base de datos específica para tu servicio
var identityDb = postgres.AddDatabase("identitydb");

// --- -  - - - - - - - - - - - -- 
//Si quieres que DBeaver apunte al Postgres de Aspire, puedes fijar el puerto publicado de Aspire: 

//var postgres = builder.AddPostgres("postgres")
//    .WithLifetime(ContainerLifetime.Persistent)
//    .WithEndpoint(port: 5432, targetPort: 5432); // host:5432 -> contenedor:5432

// 3) Tu servicio .NET que usa esa base de datos
builder.AddProject<Projects.Overflow_Identity>("overflow-identity")
    .WaitFor(identityDb)
    .WithReference(identityDb); // mejor que referenciar el servidor

builder.Build().Run();
