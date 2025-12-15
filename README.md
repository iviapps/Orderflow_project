# OrderFlow - Sistema de Gestión de Pedidos

OrderFlow es una plataforma de comercio electrónico construida con una arquitectura de **microservicios en .NET 10** utilizando **.NET Aspire** para orquestación. El sistema permite la gestión completa del ciclo de vida de pedidos, desde el catálogo de productos hasta la entrega final.

## Objetivos del Proyecto

- **Gestión de Identidad**: Autenticación y autorización robusta con JWT y ASP.NET Core Identity
- **Catálogo de Productos**: Administración de productos, categorías e inventario en tiempo real
- **Gestión de Pedidos**: Ciclo completo de pedidos con estados, reserva de stock y trazabilidad
- **Notificaciones**: Sistema de notificaciones por email basado en eventos
- **Escalabilidad**: Arquitectura de microservicios desacoplados y comunicación asíncrona

---

## Arquitectura

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            API Gateway (YARP)                           │
│                    Rate Limiting · JWT Auth · Routing                   │
└─────────────────────────────────────────────────────────────────────────┘
                    │                │                │
         ┌──────────┴──────┐  ┌──────┴──────┐  ┌──────┴──────┐
         │    Identity     │  │   Catalog   │  │   Orders    │
         │    Service      │  │   Service   │  │   Service   │
         └────────┬────────┘  └──────┬──────┘  └──────┬──────┘
                  │                  │                │
         ┌────────┴────────┐  ┌──────┴──────┐  ┌──────┴──────┐
         │  PostgreSQL     │  │ PostgreSQL  │  │ PostgreSQL  │
         │  (identitydb)   │  │ (catalogdb) │  │ (ordersdb)  │
         └─────────────────┘  └─────────────┘  └─────────────┘

                    RabbitMQ (Mensajería Asíncrona)
                              │
                    ┌─────────┴─────────┐
                    │   Notifications   │
                    │   Worker Service  │
                    └───────────────────┘
```

### Stack Tecnológico

| Componente | Tecnología |
|------------|------------|
| Framework | .NET 10 |
| Orquestación | .NET Aspire |
| Base de Datos | PostgreSQL 16 |
| ORM | Entity Framework Core 10 |
| Message Broker | RabbitMQ (MassTransit) |
| Cache | Redis |
| API Gateway | YARP |
| Autenticación | JWT Bearer + ASP.NET Core Identity |
| Validación | FluentValidation |
| Documentación API | Scalar / OpenAPI |
| Observabilidad | OpenTelemetry |
| Email | MailKit |

---

## Microservicios

### 1. Orderflow.Identity

Gestión de usuarios, autenticación y autorización.

**Características:**
- Registro y login de usuarios con JWT
- Gestión de roles (Admin, Customer)
- Administración de usuarios (CRUD)
- Bloqueo/desbloqueo de cuentas
- Cambio de contraseña

**Endpoints principales:**

| Método | Endpoint | Descripción | Auth |
|--------|----------|-------------|------|
| POST | `/api/v1/auth/register` | Registro de usuario | - |
| POST | `/api/v1/auth/login` | Iniciar sesión | - |
| GET | `/api/v1/users/me` | Perfil del usuario | User |
| GET | `/api/v1/admin/users` | Listar usuarios | Admin |
| POST | `/api/v1/admin/roles` | Crear rol | Admin |

---

### 2. Orderflow.Catalog

Gestión del catálogo de productos, categorías e inventario.

**Características:**
- CRUD de productos y categorías
- Control de stock en tiempo real
- Reserva y liberación de inventario
- Filtrado y búsqueda avanzada
- Paginación de resultados

**Modelo de Datos:**

```
Category (1) ─────── (N) Product (1) ─────── (1) Stock
    │                        │                    │
    ├── Id                   ├── Id               ├── Id
    ├── Name                 ├── Name             ├── QuantityAvailable
    └── Description          ├── Price            ├── QuantityReserved
                             ├── IsActive         └── UpdatedAt
                             └── CategoryId
```

**Endpoints principales:**

| Método | Endpoint | Descripción | Auth |
|--------|----------|-------------|------|
| GET | `/api/v1/categories` | Listar categorías | - |
| GET | `/api/v1/products` | Listar productos | - |
| POST | `/api/v1/products` | Crear producto | Admin |
| GET | `/api/v1/products/{id}/stock` | Ver stock | - |
| POST | `/api/v1/products/{id}/stock/reserve` | Reservar stock | System |

---

### 3. Orderflow.Orders

Gestión completa del ciclo de vida de pedidos.

**Características:**
- Creación de pedidos con validación de stock
- Estados del pedido con transiciones controladas
- Cancelación con liberación automática de stock
- Historial de pedidos por usuario
- Panel administrativo de pedidos

**Estados del Pedido:**

```
┌─────────┐     ┌───────────┐     ┌────────────┐     ┌─────────┐     ┌───────────┐
│ Pending │────▶│ Confirmed │────▶│ Processing │────▶│ Shipped │────▶│ Delivered │
└────┬────┘     └─────┬─────┘     └────────────┘     └─────────┘     └───────────┘
     │                │
     ▼                ▼
┌───────────┐   ┌───────────┐
│ Cancelled │   │ Cancelled │
└───────────┘   └───────────┘
```

**Endpoints principales:**

| Método | Endpoint | Descripción | Auth |
|--------|----------|-------------|------|
| POST | `/api/v1/orders` | Crear pedido | User |
| GET | `/api/v1/orders` | Mis pedidos | User |
| POST | `/api/v1/orders/{id}/cancel` | Cancelar pedido | User |
| GET | `/api/v1/admin/orders` | Listar todos | Admin |
| PATCH | `/api/v1/admin/orders/{id}/status` | Cambiar estado | Admin |

---

### 4. Orderflow.Notifications

Worker Service para procesamiento asíncrono de notificaciones.

**Eventos procesados:**
- `UserRegisteredEvent` - Email de bienvenida
- `OrderCreatedEvent` - Confirmación de pedido
- `OrderCancelledEvent` - Notificación de cancelación

---

### 5. Orderflow.API.Gateway

Punto de entrada único para todos los servicios.

**Características:**
- Reverse proxy con YARP
- Service Discovery automático (.NET Aspire)
- Rate limiting por usuario (Redis)
- Validación de JWT centralizada
- Enrutamiento basado en políticas

---

## Estructura del Proyecto

```
ProyectoOrderflow/
├── src/
│   ├── Orderflow.Identity/          # Servicio de autenticación
│   ├── Orderflow.Catalog/           # Servicio de catálogo
│   ├── Orderflow.Orders/            # Servicio de pedidos
│   ├── Orderflow.Notifications/     # Worker de notificaciones
│   ├── Orderflow.API.Gateway/       # API Gateway
│   ├── Orderflow.AppHost/           # Orquestación Aspire
│   ├── Orderflow.ServiceDefaults/   # Configuración compartida
│   └── Overflow.Shared/             # DTOs, eventos, extensiones
├── tests/
│   ├── Orderflow.Api.Identity.Test/
│   └── TestOrderflow.Console/
├── docker-compose.yaml
└── ProyectoOrderflow.sln
```

---

## Requisitos Previos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [Visual Studio 2022](https://visualstudio.microsoft.com/) o [VS Code](https://code.visualstudio.com/)

---

## Instalación y Ejecución

### Opción 1: Con .NET Aspire (Recomendado)

```bash
# Clonar el repositorio
git clone <repository-url>
cd Orderflow_project

# Ejecutar con Aspire
dotnet run --project src/Orderflow.AppHost
```

El dashboard de Aspire estará disponible en `https://localhost:17225`

### Opción 2: Con Docker Compose

```bash
# Iniciar infraestructura
docker-compose up -d

# Ejecutar servicios individualmente
dotnet run --project src/Orderflow.Identity
dotnet run --project src/Orderflow.Catalog
dotnet run --project src/Orderflow.Orders
dotnet run --project src/Orderflow.API.Gateway
```

---

## Configuración

### Variables de Entorno
La configuración se gestiona mediante variables de entorno o `appsettings.json` (no incluido en el repositorio por seguridad).

 

**Variables requeridas:**

 

| Variable | Descripción |

|----------|-------------|

| `Jwt__Secret` | Clave secreta para firmar tokens JWT (mín. 32 caracteres) |

| `Jwt__Issuer` | Emisor del token JWT |

| `Jwt__Audience` | Audiencia válida del token |

| `Jwt__ExpiryInMinutes` | Tiempo de expiración del token |

| `ConnectionStrings__identitydb` | Connection string PostgreSQL para Identity |

| `ConnectionStrings__catalogdb` | Connection string PostgreSQL para Catalog |

| `ConnectionStrings__ordersdb` | Connection string PostgreSQL para Orders |

 

> **Nota**: Con .NET Aspire, las connection strings se configuran automáticamente mediante service discovery.

 

### Configuración Local

 

1. Copia `appsettings.Development.json.example` a `appsettings.Development.json`

2. Configura tus credenciales locales

3. **Nunca** commits archivos con credenciales reales

### Credenciales de Desarrollo

| Usuario | Email | Contraseña | Rol |
|---------|-------|------------|-----|
| Admin | admin@admin.com | Test12345. | Admin |

---

## Documentación de la API

Cada servicio expone su documentación OpenAPI:

- **Identity**: `http://localhost:5001/scalar/v1`
- **Catalog**: `http://localhost:5002/scalar/v1`
- **Orders**: `http://localhost:5003/scalar/v1`
- **Gateway**: `http://localhost:5000/scalar/v1`

---

## Flujos de Negocio Principales

### Crear un Pedido

```
1. Usuario autenticado envía POST /api/v1/orders
2. Orders Service valida productos contra Catalog
3. Se reserva stock para cada item
4. Se crea el pedido en estado "Pending"
5. Se publica OrderCreatedEvent a RabbitMQ
6. Notifications envía email de confirmación
```

### Cancelar un Pedido

```
1. Usuario envía POST /api/v1/orders/{id}/cancel
2. Se valida propiedad y estado del pedido
3. Se libera el stock reservado en Catalog
4. Pedido cambia a estado "Cancelled"
5. Se publica OrderCancelledEvent
6. Notifications envía email de cancelación
```

---

## Comunicación entre Servicios

### Síncrona (HTTP)
- Orders → Catalog: Validación de productos y stock

### Asíncrona (RabbitMQ/MassTransit)
- Identity → Notifications: `UserRegisteredEvent`
- Orders → Notifications: `OrderCreatedEvent`, `OrderCancelledEvent`

---

## Testing

```bash
# Ejecutar tests unitarios
dotnet test

# Ejecutar tests con cobertura
dotnet test --collect:"XPlat Code Coverage"
```

---

## Observabilidad

El proyecto incluye OpenTelemetry para:
- **Traces**: Seguimiento de requests entre servicios
- **Metrics**: Métricas de rendimiento
- **Logs**: Logging estructurado

El dashboard de Aspire proporciona visualización integrada de todos los telemetry data.

---

## Paquetes Principales

```xml
<!-- Aspire -->
<PackageReference Include="Aspire.Hosting.AppHost" Version="13.0.0" />
<PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" Version="9.5.2" />

<!-- ASP.NET Core -->
<PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
<PackageReference Include="Microsoft.AspNetCore.Identity.EntityFrameworkCore" Version="10.0.0" />

<!-- Entity Framework -->
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="10.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.0" />

<!-- Messaging -->
<PackageReference Include="MassTransit.RabbitMQ" Version="8.4.0" />

<!-- Gateway -->
<PackageReference Include="Yarp.ReverseProxy" Version="2.3.0" />
```

---

## Estado del Proyecto

### Backend (Completado)
- [x] Microservicio de Identity
- [x] Microservicio de Catalog
- [x] Microservicio de Orders
- [x] Worker de Notifications
- [x] API Gateway con YARP
- [x] Integración con RabbitMQ
- [x] Rate Limiting con Redis
- [x] OpenTelemetry

### Frontend (En Desarrollo)
- [ ] Aplicación React
- [ ] Integración con API Gateway

---

## Licencia

Este proyecto está bajo la licencia MIT.

---

## Contribuir

1. Fork el repositorio
2. Crea una rama para tu feature (`git checkout -b feature/nueva-funcionalidad`)
3. Commit tus cambios (`git commit -m 'Añadir nueva funcionalidad'`)
4. Push a la rama (`git push origin feature/nueva-funcionalidad`)
5. Abre un Pull Request
