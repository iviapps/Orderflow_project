# 📊 OpenTelemetry - Trazabilidad en Microservicios

## ¿Qué es OpenTelemetry?

OpenTelemetry es un framework de observabilidad que permite **rastrear** (trace), **medir** (metrics) y **registrar** (logs) el comportamiento de tus microservicios. Te ayuda a responder preguntas como:

- ¿Cuánto tiempo tardó una operación de registro?
- ¿Qué servicios llamó mi servicio de Orders?
- ¿Dónde está el cuello de botella en mi sistema?
- ¿Por qué falló esta solicitud?

## 🏗️ Arquitectura de Trazabilidad

```
Usuario registra → Identity Service
                    ├─ DB Query (PostgreSQL)          ✅ Rastreado automáticamente
                    ├─ Publish Event (RabbitMQ)       ✅ Rastreado automáticamente
                    └─ Respuesta al cliente           ✅ Rastreado automáticamente

Usuario crea orden → Orders Service
                    ├─ DB Query (PostgreSQL)          ✅ Rastreado automáticamente
                    ├─ HTTP Call → Catalog Service    ✅ Rastreado automáticamente
                    │   └─ DB Query en Catalog        ✅ Rastreado automáticamente
                    ├─ Publish Event (RabbitMQ)       ✅ Rastreado automáticamente
                    └─ Respuesta al cliente           ✅ Rastreado automáticamente
```

## 🔧 Configuración Actual

### 1. ServiceDefaults (`Orderflow.ServiceDefaults`)

Este proyecto contiene la configuración centralizada de OpenTelemetry que **todos** los servicios heredan automáticamente al llamar `builder.AddServiceDefaults()`.

#### Instrumentación Automática Configurada:

✅ **ASP.NET Core** - Rastrea todas las solicitudes HTTP entrantes
✅ **HttpClient** - Rastrea llamadas HTTP salientes entre servicios
✅ **Entity Framework Core** - Rastrea queries a la base de datos
✅ **Npgsql (PostgreSQL)** - Rastrea operaciones de PostgreSQL
✅ **MassTransit (RabbitMQ)** - Rastrea mensajes publicados/consumidos
✅ **Runtime** - Métricas de .NET (GC, memoria, threads)

#### Propagación de Contexto:

El contexto de traza se propaga **automáticamente** en:
- **HTTP Headers** (W3C Trace Context)
- **RabbitMQ Messages** (MassTransit headers)

### 2. Cómo se aplica en cada servicio

Cada servicio (`Identity`, `Orders`, `Catalog`, etc.) tiene en su `Program.cs`:

```csharp
var builder = WebApplication.CreateBuilder(args);

// ✅ Esta línea activa TODA la instrumentación automática
builder.AddServiceDefaults();

// ... resto de configuración
```

## 📈 Trazabilidad Automática vs Manual

### Trazabilidad Automática (Ya configurada)

**No necesitas hacer nada**, ya está rastreando:

```csharp
// ✅ Automáticamente rastreado
await dbContext.Users.FindAsync(userId);

// ✅ Automáticamente rastreado
await httpClient.GetAsync("https://orderflow-catalog/api/v1/products/1");

// ✅ Automáticamente rastreado
await publishEndpoint.Publish(new UserRegisteredEvent(...));
```

### Trazabilidad Manual (Para operaciones críticas)

Para operaciones específicas donde quieres **agregar contexto adicional**:

```csharp
using Orderflow.ServiceDefaults;

public class MiServicio
{
    public async Task OperacionImportante(string userId)
    {
        // ✅ Crear un span personalizado
        using var activity = OrderflowActivitySource.StartActivity("Operación Importante");

        // ✅ Agregar tags (metadatos) al span
        activity?.SetTag("user.id", userId);
        activity?.SetTag("operation.type", "critical");

        try
        {
            // ✅ Registrar eventos dentro del span
            OrderflowActivitySource.AddEvent("Iniciando validación");

            // Tu código aquí
            await AlgunaOperacion();

            OrderflowActivitySource.AddEvent("Validación completada");

            activity?.SetTag("result", "success");
        }
        catch (Exception ex)
        {
            // ✅ Registrar excepciones
            OrderflowActivitySource.RecordException(ex);
            throw;
        }
    }
}
```

## 🎯 Ejemplos Implementados

### Identity Service - AuthService.cs

Ya implementado en `Orderflow.Identity/Services/Auth/AuthService.cs`:

**Login con trazabilidad:**
```csharp
public async Task<AuthResult<LoginResponse>> LoginAsync(LoginRequest request)
{
    using var activity = OrderflowActivitySource.StartActivity("User Login");
    activity?.SetTag("user.email", request.Email);

    // ... validaciones

    activity?.SetTag("user.id", user.Id);
    activity?.SetTag("user.roles", string.Join(",", roles));
    activity?.SetTag("login.result", "success");

    return AuthResult<LoginResponse>.Success(response);
}
```

**Registro con trazabilidad:**
```csharp
public async Task<AuthResult<RegisterResponse>> RegisterAsync(RegisterRequest request)
{
    using var activity = OrderflowActivitySource.StartActivity("User Registration");
    activity?.SetTag("user.email", request.Email);

    try
    {
        OrderflowActivitySource.AddEvent("Creating user in database");
        var createResult = await _userManager.CreateAsync(user, request.Password);

        activity?.SetTag("user.id", user.Id);
        OrderflowActivitySource.AddEvent("User created successfully");

        OrderflowActivitySource.AddEvent("Publishing UserRegistered event");
        await _publishEndpoint.Publish(userRegisteredEvent);

        activity?.SetTag("registration.result", "success");
        return AuthResult<RegisterResponse>.Success(response);
    }
    catch (Exception ex)
    {
        OrderflowActivitySource.RecordException(ex);
        throw;
    }
}
```

## 🔍 Visualizar las Trazas

### Opción 1: Jaeger (Recomendado)

1. Ejecutar Jaeger con Docker:
```bash
docker run -d --name jaeger \
  -e COLLECTOR_OTLP_ENABLED=true \
  -p 16686:16686 \
  -p 4318:4318 \
  jaegertracing/all-in-one:latest
```

2. Configurar el endpoint en `appsettings.Development.json`:
```json
{
  "OTEL_EXPORTER_OTLP_ENDPOINT": "http://localhost:4318"
}
```

3. Abrir Jaeger UI: http://localhost:16686

### Opción 2: .NET Aspire Dashboard

Si estás usando Aspire:
```bash
dotnet run --project Orderflow.AppHost
```

El dashboard mostrará automáticamente las trazas.

### Opción 3: Azure Application Insights

Descomentar en `ServiceDefaults/Extensions.cs`:
```csharp
if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
{
    builder.Services.AddOpenTelemetry()
       .UseAzureMonitor();
}
```

## 📊 Qué verás en las trazas

### Ejemplo: Usuario se registra y crea una orden

```
┌─ POST /api/v1/auth/register (Identity Service)           [200ms]
│  ├─ User Registration (custom span)                      [195ms]
│  │  ├─ SELECT FROM AspNetUsers (EF Core)                 [5ms]
│  │  ├─ INSERT INTO AspNetUsers (EF Core)                 [15ms]
│  │  ├─ INSERT INTO AspNetUserRoles (EF Core)             [8ms]
│  │  └─ Publish UserRegisteredEvent (MassTransit)         [2ms]
│  └─ Response                                              [5ms]
│
└─ POST /api/v1/orders (Orders Service)                    [350ms]
   ├─ Create Order (custom span)                           [340ms]
   │  ├─ GET /api/v1/products/123 → Catalog Service        [50ms]
   │  │  └─ SELECT FROM Products (EF Core en Catalog)      [45ms]
   │  ├─ POST /api/v1/products/123/stock/reserve           [30ms]
   │  │  └─ UPDATE Products SET Stock (EF Core en Catalog) [25ms]
   │  ├─ INSERT INTO Orders (EF Core)                      [12ms]
   │  └─ Publish OrderCreatedEvent (MassTransit)           [3ms]
   └─ Response                                              [10ms]
```

## 🏷️ Tags Útiles

Usa tags descriptivos para facilitar la búsqueda:

```csharp
// Identificadores
activity?.SetTag("user.id", userId);
activity?.SetTag("order.id", orderId);
activity?.SetTag("product.id", productId);

// Estado de operación
activity?.SetTag("operation.result", "success");
activity?.SetTag("operation.result", "failed");

// Metadatos de negocio
activity?.SetTag("order.total", total);
activity?.SetTag("user.role", role);
activity?.SetTag("payment.method", "credit_card");
```

## 🚀 Mejores Prácticas

1. **Usa `using` para spans manuales**: Asegura que se complete automáticamente
   ```csharp
   using var activity = OrderflowActivitySource.StartActivity("Operation");
   ```

2. **Agrega tags significativos**: Facilita búsqueda y debugging
   ```csharp
   activity?.SetTag("user.id", userId);
   activity?.SetTag("operation.type", "registration");
   ```

3. **Registra eventos importantes**:
   ```csharp
   OrderflowActivitySource.AddEvent("Sending email notification");
   ```

4. **Captura excepciones**:
   ```csharp
   catch (Exception ex)
   {
       OrderflowActivitySource.RecordException(ex);
       throw;
   }
   ```

5. **No rastrear operaciones triviales**: La trazabilidad automática ya cubre la mayoría de casos

6. **Usa nombres descriptivos**: "User Registration" es mejor que "Register"

## 📚 Recursos

- [OpenTelemetry .NET](https://github.com/open-telemetry/opentelemetry-dotnet)
- [W3C Trace Context](https://www.w3.org/TR/trace-context/)
- [Jaeger Documentation](https://www.jaegertracing.io/docs/)
- [.NET Aspire](https://learn.microsoft.com/dotnet/aspire/)

## ❓ FAQ

**P: ¿Necesito configurar OpenTelemetry en cada servicio?**
R: No, todos los servicios heredan la configuración automáticamente al usar `builder.AddServiceDefaults()`.

**P: ¿Cómo se propaga el contexto entre servicios?**
R: Automáticamente vía HTTP headers (W3C Trace Context) y RabbitMQ message headers.

**P: ¿Puedo ver las queries SQL?**
R: Sí, está configurado `SetDbStatementForText = true` en la instrumentación de EF Core.

**P: ¿Afecta el rendimiento?**
R: El overhead es mínimo (<5%) en la mayoría de aplicaciones. Puedes deshabilitarlo en producción si es necesario.

**P: ¿Cómo desactivo tracing en producción?**
R: No configures `OTEL_EXPORTER_OTLP_ENDPOINT` y OpenTelemetry no exportará trazas.
