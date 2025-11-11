using Aspire.Hosting;
using Aspire.Hosting.PostgreSQL; // habilita AddPostgres y AddDatabase

var builder = DistributedApplication.CreateBuilder(args);

// 1) Servidor Postgres con volumen de datos (persistencia real)
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();


//en el castor pone que no existe 
// 2) Base de datos específica para tu servicio
var identityDb = postgres.AddDatabase("identitydb");

// 3) Tu servicio .NET que usa esa base de datos
builder.AddProject<Projects.Overflow_Identity>("overflow-identity")
    .WithReference(identityDb); // mejor que referenciar el servidor

builder.Build().Run();
