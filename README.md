# OrderFlow - Sistema de Gestión de Pedidos E-Commerce

OrderFlow es una plataforma de comercio electrónico moderna construida con **arquitectura de microservicios** usando **.NET 10** para el backend y **React 19** para el frontend, orquestados con **.NET Aspire**.

## Tabla de Contenidos

- [Arquitectura General](#arquitectura-general)
- [Estructura del Proyecto](#estructura-del-proyecto)
- [Microservicios](#microservicios)
- [Patrones de Comunicación](#patrones-de-comunicación)
- [Flujos de Negocio (Workflows)](#flujos-de-negocio-workflows)
- [API Gateway](#api-gateway)
- [Frontend](#frontend)
- [Infraestructura](#infraestructura)
- [Observabilidad](#observabilidad)
- [Instalación y Ejecución](#instalación-y-ejecución)
- [Testing](#testing)
- [Stack Tecnológico](#stack-tecnológico)

---

## Arquitectura General

OrderFlow implementa una arquitectura de **microservicios desacoplados** con el patrón **Database-per-Service**. Cada servicio tiene su propia base de datos PostgreSQL, garantizando aislamiento total de datos.

```
┌─────────────────────────────────────────────────────────────────┐
│                      FRONTEND (React 19)                        │
│                  Aplicación SPA - En desarrollo                 │
└────────────────────────────┬────────────────────────────────────┘
                             │
                             │ HTTP/JSON + JWT Auth
                             ▼
┌─────────────────────────────────────────────────────────────────┐
│               API GATEWAY (YARP) - Puerto 5000                  │
│  ┌─────────────┐ ┌──────────────┐ ┌────────────────────────┐   │
│  │Rate Limiting│ │JWT Validation│ │ Service Discovery      │   │
│  │   (Redis)   │ │ Centralizada │ │ (.NET Aspire)          │   │
│  └─────────────┘ └──────────────┘ └────────────────────────┘   │
└────────┬────────────────────┬────────────────────┬──────────────┘
         │                    │                    │
    ┌────▼────┐         ┌─────▼─────┐        ┌────▼─────┐
    │Identity │         │ Catalog   │        │ Orders   │
    │ :5001   │         │  :5002    │        │  :5003   │
    │         │◄───────►│           │◄──────►│          │
    └────┬────┘  HTTP   └─────┬─────┘  HTTP  └────┬─────┘
         │                    │                    │
    ┌────▼────┐         ┌─────▼─────┐        ┌────▼─────┐
    │identitydb│        │ catalogdb │        │ ordersdb │
    └─────────┘         └───────────┘        └──────────┘
         │                    │                    │
         └────────────────────┼────────────────────┘
                              │
                         PostgreSQL 16
                              │
              ┌───────────────┴───────────────┐
              │                               │
         ┌────▼────┐                    ┌─────▼──────┐
         │RabbitMQ │  ────────────────► │Notifications│
         │ Events  │                    │   Worker   │
         └─────────┘                    └────────────┘
                                              │
                                         ┌────▼────┐
                                         │  SMTP   │
                                         │(MailDev)│
                                         └─────────┘
```

### Principios de Diseño

- **Database-per-Service**: Cada microservicio tiene su propia base de datos PostgreSQL
- **Event-Driven Architecture**: Comunicación asíncrona via RabbitMQ para operaciones no críticas
- **API Gateway Pattern**: Punto de entrada único con autenticación centralizada
- **Service Discovery**: Descubrimiento automático de servicios con .NET Aspire
- **Observability-First**: OpenTelemetry integrado para trazas, métricas y logs

---

## Estructura del Proyecto

```
Orderflow_Project_2025/
│
├── Orderflow.AppHost/               # Orquestación .NET Aspire
│   └── AppHost.cs                   # Configuración de servicios e infraestructura
│
├── Orderflow.ServiceDefaults/       # Configuración compartida de servicios
│   └── Extensions.cs                # OpenTelemetry, Health Checks, etc.
│
├── Orderflow.Shared/                # Código compartido entre servicios
│   ├── DTOs/                        # Data Transfer Objects
│   ├── Events/                      # Eventos de integración (RabbitMQ)
│   │   ├── UserRegisteredEvent.cs
│   │   ├── OrderCreatedEvent.cs
│   │   └── OrderCancelledEvent.cs
│   └── Extensions/                  # Extensiones compartidas
│
├── Orderflow.API.Gateway/           # API Gateway (YARP Reverse Proxy)
│   ├── Program.cs                   # Configuración del gateway
│   └── appsettings.json             # Rutas y clusters YARP
│
├── Orderflow.Identity/              # Microservicio de Autenticación
│   ├── Controllers/                 # AuthController, UsersController
│   ├── Services/                    # AuthService, UserService, TokenService
│   ├── Data/                        # IdentityDbContext, Migrations
│   └── Program.cs
│
├── Orderflow.Catalog/               # Microservicio de Catálogo
│   ├── Controllers/                 # ProductsController, CategoriesController
│   ├── Services/                    # ProductService, CategoryService
│   ├── Data/                        # CatalogDbContext, Entities
│   └── Program.cs
│
├── Orderflow.Orders/                # Microservicio de Pedidos
│   ├── Controllers/                 # OrdersController
│   ├── Services/                    # OrderService
│   ├── Clients/                     # CatalogClient (HTTP client)
│   ├── Data/                        # OrdersDbContext, Entities
│   └── Program.cs
│
├── Orderflow.Notifications/         # Worker Service de Notificaciones
│   ├── Consumers/                   # Consumidores MassTransit
│   │   ├── UserRegisteredConsumer.cs
│   │   ├── OrderCreatedConsumer.cs
│   │   └── OrderCancelledConsumer.cs
│   ├── Services/                    # EmailService
│   └── Program.cs
│
├── Orderflow.Web/                   # Frontend React (En desarrollo)
│
├── Orderflow.Api.Identity.Test/     # Tests unitarios Identity
├── TestOrderflow.Console/           # Tests de integración
│
├── docker-compose.yaml              # Infraestructura Docker
├── Directory.Packages.props         # Gestión centralizada de versiones NuGet
└── ProyectoOrderflow.sln            # Solución .NET
```

---

## Microservicios

### 1. Orderflow.Identity (Puerto 5001)

**Responsabilidad**: Autenticación, autorización y gestión de usuarios.

| Funcionalidad | Descripción |
|---------------|-------------|
| Autenticación JWT | Generación y validación de tokens |
| ASP.NET Core Identity | Gestión de usuarios y roles |
| CRUD de Usuarios | Operaciones administrativas |
| Gestión de Roles | Admin, Customer |
| Bloqueo de Cuentas | Seguridad y control de acceso |

**Endpoints Principales:**
```
POST   /api/v1/auth/register           # Registro público
POST   /api/v1/auth/login              # Login → JWT Token
GET    /api/v1/users/me                # Perfil usuario (auth)
PUT    /api/v1/users/me                # Actualizar perfil (auth)
GET    /api/v1/admin/users             # Listar usuarios (admin)
POST   /api/v1/admin/users/{id}/lock   # Bloquear usuario (admin)
```

**Base de Datos**: `identitydb`

---

### 2. Orderflow.Catalog (Puerto 5002)

**Responsabilidad**: Gestión de productos, categorías e inventario.

| Funcionalidad | Descripción |
|---------------|-------------|
| CRUD Productos | Gestión completa de productos |
| Categorías | Organización del catálogo |
| Control de Stock | Disponibilidad en tiempo real |
| Reserva de Inventario | Para procesamiento de pedidos |

**Modelo de Datos:**
```
Category (1) ──── (N) Product (1) ──── (1) Stock
    │                     │                  │
    ├─ Id                 ├─ Id              ├─ QuantityAvailable
    ├─ Name               ├─ Name            ├─ QuantityReserved
    └─ Description        ├─ Price           └─ UpdatedAt
                          ├─ IsActive
                          └─ CategoryId
```

**Endpoints Principales:**
```
GET    /api/v1/categories                    # Listar categorías
GET    /api/v1/products                      # Listar productos (paginado)
GET    /api/v1/products/{id}                 # Detalle de producto
POST   /api/v1/products/{id}/stock/reserve   # Reservar stock (interno)
POST   /api/v1/products/{id}/stock/release   # Liberar stock (interno)
```

**Base de Datos**: `catalogdb`

---

### 3. Orderflow.Orders (Puerto 5003)

**Responsabilidad**: Gestión del ciclo de vida completo de pedidos.

| Funcionalidad | Descripción |
|---------------|-------------|
| Creación de Pedidos | Con validación de stock |
| Estados de Pedido | Transiciones controladas |
| Cancelación | Liberación automática de stock |
| Historial | Por usuario y administrador |

**Estados del Pedido:**
```
Pending → Confirmed → Processing → Shipped → Delivered
   ↓           ↓
Cancelled  Cancelled
```

**Endpoints Principales:**
```
POST   /api/v1/orders                        # Crear pedido (auth)
GET    /api/v1/orders                        # Mis pedidos (auth)
GET    /api/v1/orders/{id}                   # Detalle pedido (auth)
POST   /api/v1/orders/{id}/cancel            # Cancelar pedido (auth)
GET    /api/v1/admin/orders                  # Todos los pedidos (admin)
PATCH  /api/v1/admin/orders/{id}/status      # Cambiar estado (admin)
```

**Base de Datos**: `ordersdb`

---

### 4. Orderflow.Notifications (Worker Service)

**Responsabilidad**: Procesamiento asíncrono de notificaciones por email.

| Funcionalidad | Descripción |
|---------------|-------------|
| Consumidor RabbitMQ | Escucha eventos con MassTransit |
| Envío de Emails | Via MailKit/SMTP |
| Reintentos Automáticos | Política: 1s, 5s, 15s, 30s |

**Eventos Procesados:**
```
UserRegisteredEvent   → Email de bienvenida
OrderCreatedEvent     → Confirmación de pedido
OrderCancelledEvent   → Notificación de cancelación
```

**No expone endpoints HTTP** (es un worker en background)

---

## Patrones de Comunicación

### Comunicación Síncrona (HTTP/REST)

Utilizada para operaciones que requieren respuesta inmediata y validación en tiempo real.

```
┌──────────────┐          HTTP Request           ┌───────────────┐
│    Orders    │ ───────────────────────────────►│    Catalog    │
│   Service    │◄─────────────────────────────── │    Service    │
└──────────────┘          HTTP Response          └───────────────┘
```

**Casos de Uso:**
- Validar existencia de productos
- Verificar disponibilidad de stock
- Reservar inventario para un pedido
- Liberar stock en cancelación

**Implementación:**
```csharp
// CatalogClient.cs en Orders Service
private readonly HttpClient _http = httpClientFactory.CreateClient("catalog");

// Configuración con Service Discovery
builder.Services.AddHttpClient("catalog", client =>
{
    client.BaseAddress = new Uri("https+http://orderflow-catalog");
});
```

---

### Comunicación Asíncrona (RabbitMQ + MassTransit)

Utilizada para operaciones desacopladas, notificaciones y procesamiento en background.

```
┌──────────────┐     Publish Event      ┌───────────┐     Consume      ┌───────────────┐
│   Identity   │ ──────────────────────►│           │─────────────────►│               │
│   Orders     │                        │ RabbitMQ  │                  │ Notifications │
│   Service    │                        │           │                  │    Worker     │
└──────────────┘                        └───────────┘                  └───────────────┘
```

**Eventos de Integración:**
```csharp
// Orderflow.Shared/Events/
public interface IIntegrationEvent
{
    Guid EventId { get; }
    DateTime Timestamp { get; }
}

public record UserRegisteredEvent(string UserId, string Email, string FirstName);
public record OrderCreatedEvent(int OrderId, string UserId, IEnumerable<OrderItemEvent> Items);
public record OrderCancelledEvent(int OrderId, string UserId, string Reason);
```

**Ventajas:**
- Desacoplamiento total entre servicios
- Procesamiento en background
- Reintentos automáticos con backoff exponencial
- Tolerancia a fallos

---

## Flujos de Negocio (Workflows)

### 1. Registro de Usuario

```
┌─────────┐  POST /api/v1/auth/register  ┌────────────┐
│ Cliente │─────────────────────────────►│API Gateway │
└─────────┘                              └─────┬──────┘
                                               │
                                               ▼
                                        ┌──────────────┐
                                        │   Identity   │
                                        │   Service    │
                                        └──────┬───────┘
                                               │
                    ┌──────────────────────────┼──────────────────────────┐
                    │                          │                          │
                    ▼                          ▼                          ▼
             ┌────────────┐           ┌──────────────┐           ┌──────────────┐
             │  Crear en  │           │   Generar    │           │   Publicar   │
             │  identitydb│           │  JWT Token   │           │UserRegistered│
             └────────────┘           └──────────────┘           │   Event      │
                                                                 └──────┬───────┘
                                                                        │
                                                                        ▼
                                                                 ┌──────────────┐
                                                                 │Notifications │
                                                                 │   Worker     │
                                                                 └──────┬───────┘
                                                                        │
                                                                        ▼
                                                                 ┌──────────────┐
                                                                 │ Email de     │
                                                                 │ Bienvenida   │
                                                                 └──────────────┘
```

---

### 2. Creación de Pedido (Workflow Completo)

```
┌─────────┐  POST /api/v1/orders + JWT   ┌────────────┐
│ Cliente │─────────────────────────────►│API Gateway │
└─────────┘                              └─────┬──────┘
                                               │ Valida JWT
                                               │ Rate Limit
                                               ▼
                                        ┌──────────────┐
                                        │   Orders     │
                                        │   Service    │
                                        └──────┬───────┘
                                               │
           ┌───────────────────────────────────┤
           │                                   │
           ▼                                   ▼
    ┌──────────────┐                  ┌──────────────────┐
    │   Validar    │  HTTP Request   │     Catalog      │
    │   Productos  │────────────────►│     Service      │
    │              │◄────────────────│                  │
    └──────────────┘  Product Data   └─────────┬────────┘
                                               │
                                               ▼
                                      ┌──────────────────┐
                                      │  Reservar Stock  │
                                      │  en catalogdb    │
                                      └─────────┬────────┘
                                                │
                    ┌───────────────────────────┤
                    │                           │
                    ▼                           ▼
             ┌────────────┐            ┌──────────────────┐
             │ Crear en   │            │    Publicar      │
             │  ordersdb  │            │ OrderCreatedEvent│
             │            │            └────────┬─────────┘
             └────────────┘                     │
                                                ▼
                                        ┌───────────────┐
                                        │ Notifications │
                                        │    Worker     │
                                        └───────┬───────┘
                                                │
                                                ▼
                                        ┌───────────────┐
                                        │    Email de   │
                                        │  Confirmación │
                                        └───────────────┘
```

---

### 3. Cancelación de Pedido

```
POST /api/v1/orders/{id}/cancel
              │
              ▼
┌──────────────────────────────────────────────────────────────┐
│                      Orders Service                           │
│                                                               │
│  1. Validar que el usuario es propietario del pedido         │
│  2. Validar que el estado permite cancelación                │
│     (Pending o Confirmed)                                     │
│                                                               │
└─────────────────────────────┬────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────┐
│                     Catalog Service                           │
│                                                               │
│  3. Liberar stock reservado para cada item                   │
│     QuantityReserved -= cantidad                              │
│     QuantityAvailable += cantidad                             │
│                                                               │
└─────────────────────────────┬────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────┐
│                      Orders Service                           │
│                                                               │
│  4. Actualizar estado a "Cancelled"                          │
│  5. Publicar OrderCancelledEvent a RabbitMQ                  │
│                                                               │
└─────────────────────────────┬────────────────────────────────┘
                              │
                              ▼
┌──────────────────────────────────────────────────────────────┐
│                   Notifications Worker                        │
│                                                               │
│  6. Consumir OrderCancelledEvent                             │
│  7. Enviar email de cancelación                              │
│                                                               │
└──────────────────────────────────────────────────────────────┘
```

---

## API Gateway

El **API Gateway** es el punto de entrada único para todas las peticiones del cliente. Implementado con **YARP (Yet Another Reverse Proxy)**.

### Características

| Característica | Implementación |
|----------------|----------------|
| Reverse Proxy | YARP 2.3.0 |
| Rate Limiting | Redis + Sliding Window |
| Autenticación | JWT Bearer centralizada |
| Service Discovery | .NET Aspire automático |
| CORS | Configurado para frontend |

### Políticas de Autorización

| Política | Descripción | Rutas |
|----------|-------------|-------|
| `anonymous` | Sin autenticación | `/api/v1/auth/*`, `/api/v1/products/*`, `/api/v1/categories/*` |
| `authenticated` | JWT válido requerido | `/api/v1/users/*`, `/api/v1/orders/*` |
| `admin` | JWT + Rol Admin | `/api/v1/admin/*` |

### Configuración de Rutas (YARP)

```yaml
# Rutas de Identity
/api/v1/auth/*              → Identity Service (anónimo)
/api/v1/users/*             → Identity Service (autenticado)
/api/v1/admin/users/*       → Identity Service (admin)

# Rutas de Catalog
/api/v1/categories/*        → Catalog Service (anónimo)
/api/v1/products/*          → Catalog Service (anónimo)

# Rutas de Orders
/api/v1/orders/*            → Orders Service (autenticado)
/api/v1/admin/orders/*      → Orders Service (admin)
```

---

## Frontend

> **Estado: En desarrollo**

El frontend es una **Single Page Application (SPA)** que se comunica **exclusivamente** con el API Gateway. Nunca se conecta directamente a los microservicios.

### Conexión con el Backend

```
┌──────────────────────┐
│     Frontend SPA     │
│       React 19       │
└──────────┬───────────┘
           │
           │  HTTP Requests (Axios)
           │  Authorization: Bearer <JWT>
           │
           ▼
┌──────────────────────┐
│     API Gateway      │
│   localhost:5000     │
│                      │
│  • Valida JWT        │
│  • Rate Limiting     │
│  • Enruta request    │
│  • CORS habilitado   │
└──────────────────────┘
```

### Configuración de Conexión

El frontend utiliza variables de entorno para conectarse al gateway:

```bash
# .env
VITE_API_GATEWAY_URL=http://localhost:5000
VITE_PORT=5173
```

### Patrón de Comunicación

```typescript
// Configuración base de Axios
const api = axios.create({
  baseURL: import.meta.env.VITE_API_GATEWAY_URL,
  headers: { 'Content-Type': 'application/json' },
});

// Interceptor para JWT
api.interceptors.request.use((config) => {
  const token = localStorage.getItem('token');
  if (token) {
    config.headers.Authorization = `Bearer ${token}`;
  }
  return config;
});
```

### Stack Tecnológico del Frontend

| Componente | Tecnología | Versión |
|------------|------------|---------|
| Framework | React | 19.2.0 |
| Lenguaje | TypeScript | 5.9.3 |
| Build Tool | Vite (rolldown) | 7.2.5 |
| Routing | React Router | 7.10.1 |
| State/Cache | TanStack Query | 5.90.12 |
| HTTP Client | Axios | 1.13.2 |
| Estilos | Tailwind CSS | 3.4.17 |

---

## Infraestructura

### Docker Compose (Desarrollo Manual)

```yaml
services:
  postgres:
    image: postgres:16
    ports: ["5432:5432"]
    volumes: [pgdata:/var/lib/postgresql/data]
    # Bases de datos: identitydb, catalogdb, ordersdb

  redis:
    image: redis:7
    ports: ["6379:6379"]
    volumes: [redisdata:/data]
```

### .NET Aspire (Recomendado)

Aspire orquesta automáticamente toda la infraestructura:

```csharp
// AppHost.cs
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("Orderflow-postgres-data")
    .WithPgAdmin();

var identityDb = postgres.AddDatabase("identitydb");
var catalogDb = postgres.AddDatabase("catalogdb");
var ordersDb = postgres.AddDatabase("ordersdb");

var redis = builder.AddRedis("cache");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin();

var maildev = builder.AddContainer("maildev", "maildev/maildev")
    .WithHttpEndpoint(port: 1080);  // UI para ver emails
```

### Servicios Registrados en Aspire

```csharp
// Cada servicio con sus dependencias
var identity = builder.AddProject<Projects.Orderflow_Identity>()
    .WithReference(identityDb)
    .WithReference(rabbitmq);

var catalog = builder.AddProject<Projects.Orderflow_Catalog>()
    .WithReference(catalogDb);

var orders = builder.AddProject<Projects.Orderflow_Orders>()
    .WithReference(ordersDb)
    .WithReference(rabbitmq)
    .WithReference(catalog);  // HTTP client

var notifications = builder.AddProject<Projects.Orderflow_Notifications>()
    .WithReference(rabbitmq)
    .WithReference(maildev);

var gateway = builder.AddProject<Projects.Orderflow_API_Gateway>()
    .WithReference(identity)
    .WithReference(catalog)
    .WithReference(orders)
    .WithReference(redis);
```

---

## Observabilidad

El proyecto implementa **OpenTelemetry** para observabilidad completa.

### Componentes

| Tipo | Tecnología | Descripción |
|------|------------|-------------|
| Traces | OpenTelemetry | Seguimiento distribuido de requests |
| Metrics | OpenTelemetry | Contadores, latencias, errores |
| Logs | Serilog | Logging estructurado con correlación |
| Dashboard | .NET Aspire | Visualización integrada |

### Dashboard de Aspire

Disponible en: `https://localhost:17225`

Proporciona:
- Estado en tiempo real de todos los servicios
- Logs agregados con búsqueda
- Traces distribuidos entre servicios
- Métricas de rendimiento
- Health checks

### Health Checks

Cada servicio expone endpoints de salud:
```
GET /health       # Estado general
GET /alive        # Liveness probe
GET /ready        # Readiness probe
```

---

## Instalación y Ejecución

### Requisitos Previos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Node.js 18+](https://nodejs.org/) (para el frontend)

### Opción 1: Con .NET Aspire (Recomendado)

```bash
# Clonar el repositorio
git clone <repository-url>
cd Orderflow_Project_2025

# Ejecutar con Aspire
dotnet run --project Orderflow.AppHost
```

**Dashboard de Aspire**: https://localhost:17225

### Opción 2: Ejecución Manual

```bash
# 1. Iniciar infraestructura
docker-compose up -d

# 2. Ejecutar cada servicio (en terminales separadas)
dotnet run --project Orderflow.Identity
dotnet run --project Orderflow.Catalog
dotnet run --project Orderflow.Orders
dotnet run --project Orderflow.Notifications
dotnet run --project Orderflow.API.Gateway

# 3. Ejecutar frontend
cd Orderflow.Web
npm install
npm run dev
```

### URLs de Acceso

| Servicio | URL |
|----------|-----|
| API Gateway | http://localhost:5000 |
| Frontend | http://localhost:5173 |
| Aspire Dashboard | https://localhost:17225 |
| MailDev (emails) | http://localhost:1080 |
| RabbitMQ Management | http://localhost:15672 |

### Documentación de API (Scalar)

| Servicio | URL |
|----------|-----|
| API Gateway | http://localhost:5000/scalar/v1 |
| Identity | http://localhost:5001/scalar/v1 |
| Catalog | http://localhost:5002/scalar/v1 |
| Orders | http://localhost:5003/scalar/v1 |

---

## Testing

### Tests Unitarios

```bash
# Ejecutar todos los tests
dotnet test

# Con cobertura de código
dotnet test --collect:"XPlat Code Coverage"

# Proyecto específico
dotnet test Orderflow.Api.Identity.Test
```

### Proyectos de Tests

| Proyecto | Cobertura |
|----------|-----------|
| `Orderflow.Api.Identity.Test` | AuthService, UserService, RoleService |
| `TestOrderflow.Console` | Tests de integración |

### Stack de Testing

| Componente | Tecnología |
|------------|------------|
| Framework | NUnit 4.2.2 |
| Mocking | Moq 4.20.72 |
| LINQ Mocking | MockQueryable 10.0.1 |

---

## Stack Tecnológico

### Backend

| Componente | Tecnología | Versión |
|------------|------------|---------|
| Framework | .NET | 10.0 |
| Web Framework | ASP.NET Core | 10.0 |
| ORM | Entity Framework Core | 10.0 |
| Base de Datos | PostgreSQL | 16 |
| Message Broker | RabbitMQ | Latest |
| Messaging | MassTransit | 8.4.0 |
| Cache | Redis | 7 |
| API Gateway | YARP | 2.3.0 |
| Rate Limiting | RedisRateLimiting | 1.2.0 |
| Autenticación | JWT Bearer + Identity | 10.0 |
| Validación | FluentValidation | 12.1.0 |
| Email | MailKit | 4.12.1 |
| API Docs | Scalar / OpenAPI | Latest |
| Observabilidad | OpenTelemetry | 1.14.0 |
| Orquestación | .NET Aspire | 13.0.0 |

### Configuración JWT

```bash
Jwt__Secret=<clave-secreta-minimo-32-caracteres>
Jwt__Issuer=Orderflow.Identity
Jwt__Audience=Orderflow.Api
Jwt__ExpiryInMinutes=60
```

### Credenciales de Desarrollo

| Campo | Valor |
|-------|-------|
| Email | admin@admin.com |
| Password | Test12345. |
| Rol | Admin |

---

## Estado del Proyecto

### Backend - Completado

- [x] Microservicio Identity (Autenticación JWT, Roles, CRUD usuarios)
- [x] Microservicio Catalog (Productos, Categorías, Stock)
- [x] Microservicio Orders (Pedidos, Estados, Cancelación)
- [x] Worker Notifications (RabbitMQ, Emails)
- [x] API Gateway (YARP, Rate Limiting, JWT)
- [x] Integración RabbitMQ/MassTransit
- [x] PostgreSQL con migraciones
- [x] OpenTelemetry
- [x] Tests unitarios

### Frontend - En Desarrollo

El frontend está en desarrollo activo. Se conecta al API Gateway para consumir los servicios del backend.

---

## Licencia

Este proyecto está bajo la licencia MIT.

---

## Contribuir

1. Fork el repositorio
2. Crea una rama (`git checkout -b feature/nueva-funcionalidad`)
3. Commit tus cambios (`git commit -m 'feat: nueva funcionalidad'`)
4. Push a la rama (`git push origin feature/nueva-funcionalidad`)
5. Abre un Pull Request
