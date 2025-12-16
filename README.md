# OrderFlow - Sistema de Gestión de Pedidos E-Commerce

 

OrderFlow es una plataforma de comercio electrónico moderna construida con **arquitectura de microservicios** usando **.NET 10** para el backend y **React 19** para el frontend, orquestados con **.NET Aspire**.

 

## Tabla de Contenidos

 

- [Arquitectura General](#arquitectura-general)

- [Backend - Microservicios](#backend---microservicios)

  - [Microservicios Disponibles](#microservicios-disponibles)

  - [Stack Tecnológico Backend](#stack-tecnológico-backend)

- [Frontend - Aplicación Web](#frontend---aplicación-web)

  - [Stack Tecnológico Frontend](#stack-tecnológico-frontend)

  - [Estructura del Frontend](#estructura-del-frontend)

- [API Gateway - Punto de Entrada Único](#api-gateway---punto-de-entrada-único)

- [Instalación y Ejecución](#instalación-y-ejecución)

- [Documentación de API](#documentación-de-api)

- [Testing](#testing)

 

---

 

## Arquitectura General

 

OrderFlow utiliza una arquitectura de microservicios desacoplados que se comunican a través de un **API Gateway centralizado**.

 

```

┌─────────────────────────────────────────────────────────────┐

│                    FRONTEND (React 19)                      │

│              http://localhost:VITE_PORT                     │

│   • UI moderna con TypeScript                              │

│   • TanStack Query para estado                             │

│   • Tailwind CSS para estilos                              │

└────────────────────────┬────────────────────────────────────┘

                         │

                         │ HTTP/JSON + JWT Auth

                         ▼

┌─────────────────────────────────────────────────────────────┐

│            API GATEWAY (YARP) - Puerto 5000                 │

│  • Reverse Proxy inteligente                               │

│  • Rate Limiting con Redis                                 │

│  • Validación JWT centralizada                             │

│  • Service Discovery automático                            │

└────────┬──────────────────┬──────────────────┬──────────────┘

         │                  │                  │

    ┌────▼────┐       ┌─────▼─────┐      ┌────▼─────┐

    │Identity │       │ Catalog   │      │ Orders   │

    │Service  │       │ Service   │      │ Service  │

    │:5001    │       │ :5002     │      │ :5003    │

    └────┬────┘       └─────┬─────┘      └────┬─────┘

         │                  │                  │

    ┌────▼──────────────────▼──────────────────▼─────┐

    │            PostgreSQL 16 (3 bases)             │

    │  • identitydb  • catalogdb  • ordersdb        │

    └──────────────────────┬─────────────────────────┘

                           │

                      ┌────▼────┐

                      │RabbitMQ │  ──────▶  ┌──────────────┐

                      │(Events) │           │Notifications │

                      └─────────┘           │Worker Service│

                                            └──────────────┘

```

 

---

 

## Backend - Microservicios

 

El backend está construido con **microservicios independientes** en **.NET 10**, cada uno con su propia base de datos PostgreSQL (patrón Database-per-Service).

 

### Microservicios Disponibles

 

#### 1. **Orderflow.Identity** (Puerto 5001)

 

**Propósito:** Gestión de autenticación, autorización y usuarios.

 

**Funcionalidades:**

- Registro y login de usuarios con JWT

- Gestión de roles (Admin, Customer)

- CRUD completo de usuarios (admin)

- Bloqueo/desbloqueo de cuentas

- Cambio de contraseña

- Gestión de perfiles de usuario

- ASP.NET Core Identity integrado

 

**Endpoints principales:**

```

POST   /api/v1/auth/register         # Registro público

POST   /api/v1/auth/login            # Login público

GET    /api/v1/users/me              # Perfil del usuario (auth)

PUT    /api/v1/users/me              # Actualizar perfil (auth)

POST   /api/v1/users/me/password     # Cambiar contraseña (auth)

 

# Endpoints administrativos

GET    /api/v1/admin/users           # Listar usuarios (admin)

POST   /api/v1/admin/users           # Crear usuario (admin)

PUT    /api/v1/admin/users/{id}      # Actualizar usuario (admin)

DELETE /api/v1/admin/users/{id}      # Eliminar usuario (admin)

POST   /api/v1/admin/users/{id}/lock # Bloquear usuario (admin)

POST   /api/v1/admin/users/{id}/unlock # Desbloquear usuario (admin)

POST   /api/v1/admin/roles           # Crear rol (admin)

```

 

**Base de datos:** `identitydb` (PostgreSQL 16)

 

---

 

#### 2. **Orderflow.Catalog** (Puerto 5002)

 

**Propósito:** Gestión del catálogo de productos, categorías e inventario.

 

**Funcionalidades:**

- CRUD de productos y categorías

- Control de stock en tiempo real

- Reserva y liberación automática de inventario

- Búsqueda y filtrado de productos

- Paginación de resultados

- Endpoints públicos para consultas

 

**Modelo de Datos:**

```

Category (1) ────── (N) Product (1) ────── (1) Stock

  │                       │                     │

  ├─ Id                   ├─ Id                 ├─ QuantityAvailable

  ├─ Name                 ├─ Name               ├─ QuantityReserved

  └─ Description          ├─ Price              └─ UpdatedAt

                          ├─ IsActive

                          └─ CategoryId

```

 

**Endpoints principales:**

```

# Públicos

GET    /api/v1/categories            # Listar categorías

GET    /api/v1/products              # Listar productos (con filtros)

GET    /api/v1/products/{id}         # Detalle de producto

GET    /api/v1/products/{id}/stock   # Ver disponibilidad

 

# Administrativos

POST   /api/v1/products              # Crear producto (admin)

PUT    /api/v1/products/{id}         # Actualizar producto (admin)

DELETE /api/v1/products/{id}         # Eliminar producto (admin)

POST   /api/v1/products/{id}/stock/reserve    # Reservar stock (sistema)

POST   /api/v1/products/{id}/stock/release    # Liberar stock (sistema)

```

 

**Base de datos:** `catalogdb` (PostgreSQL 16)

 

---

 

#### 3. **Orderflow.Orders** (Puerto 5003)

 

**Propósito:** Gestión completa del ciclo de vida de pedidos.

 

**Funcionalidades:**

- Creación de pedidos con validación de stock en tiempo real

- Estados del pedido con transiciones controladas

- Cancelación con liberación automática de stock

- Historial de pedidos por usuario

- Panel administrativo de pedidos

- Comunicación síncrona con Catalog Service (HTTP)

- Comunicación asíncrona con Notifications (RabbitMQ)

 

**Estados del Pedido:**

```

Pending → Confirmed → Processing → Shipped → Delivered

   ↓           ↓

Cancelled  Cancelled

```

 

**Endpoints principales:**

```

# Usuario autenticado

POST   /api/v1/orders                # Crear pedido

GET    /api/v1/orders                # Mis pedidos

GET    /api/v1/orders/{id}           # Detalle de pedido

POST   /api/v1/orders/{id}/cancel    # Cancelar pedido

 

# Administrativos

GET    /api/v1/admin/orders          # Listar todos los pedidos

PATCH  /api/v1/admin/orders/{id}/status  # Cambiar estado de pedido

```

 

**Base de datos:** `ordersdb` (PostgreSQL 16)

 

---

 

#### 4. **Orderflow.Notifications** (Worker Service)

 

**Propósito:** Procesamiento asíncrono de notificaciones por email.

 

**Funcionalidades:**

- Escucha eventos desde RabbitMQ con MassTransit

- Envío de emails transaccionales con MailKit

- Reintentos automáticos en caso de fallo

- Configuración flexible de templates

 

**Eventos procesados:**

```

UserRegisteredEvent   → Email de bienvenida

OrderCreatedEvent     → Confirmación de pedido creado

OrderCancelledEvent   → Notificación de cancelación

```

 

**No tiene endpoints** (es un worker en background)

 

---

 

#### 5. **Orderflow.API.Gateway** (Puerto 5000)

 

**Propósito:** Punto de entrada único para todos los servicios (Reverse Proxy).

 

**Características:**

- Reverse proxy con **YARP** (Yet Another Reverse Proxy)

- Service Discovery automático con .NET Aspire

- Rate limiting por usuario usando Redis

- Validación de JWT centralizada

- Enrutamiento basado en políticas de autorización

- CORS configurado para el frontend

 

**Ver más detalles:** [API Gateway - Punto de Entrada Único](#api-gateway---punto-de-entrada-único)

 

---

 

### Stack Tecnológico Backend

 

| Componente | Tecnología | Versión |

|------------|------------|---------|

| Framework | .NET | 10.0 |

| Web Framework | ASP.NET Core | 10.0 |

| ORM | Entity Framework Core | 10.0 |

| Base de Datos | PostgreSQL | 16 |

| Message Broker | RabbitMQ | Latest |

| Messaging Library | MassTransit | 8.4.0 |

| Cache | Redis | 7 |

| API Gateway | YARP | 2.3.0 |

| Autenticación | JWT Bearer + ASP.NET Core Identity | 10.0 |

| Validación | FluentValidation | Latest |

| Email | MailKit | Latest |

| API Docs | Scalar / OpenAPI | Latest |

| Observabilidad | OpenTelemetry | Latest |

| Orquestación | .NET Aspire | 13.0.0 |

 

---

 

## Frontend - Aplicación Web

 

El frontend es una **Single Page Application (SPA)** construida con **React 19** y **TypeScript**, que se comunica **exclusivamente con el API Gateway**.

 

### Stack Tecnológico Frontend

 

| Componente | Tecnología | Versión |

|------------|------------|---------|

| Framework | React | 19.2.0 |

| Lenguaje | TypeScript | 5.9.3 |

| Build Tool | Vite (rolldown-vite) | 7.2.5 |

| Routing | React Router | 7.10.1 |

| State Management | TanStack React Query | 5.90.12 |

| HTTP Client | Axios | 1.13.2 |

| Estilos | Tailwind CSS | 3.4.17 |

| Linting | ESLint | 9.39.1 |

 

### Estructura del Frontend

 

```

Orderflow.Web/

├── src/

│   ├── main.tsx                    # Punto de entrada

│   ├── App.tsx                     # Componente raíz

│   ├── index.css                   # Estilos globales (Tailwind)

│   │

│   ├── app/                        # Configuración de la app

│   │   ├── router.tsx              # Definición de rutas

│   │   └── ui/

│   │       └── AppLayout.tsx       # Layout compartido (header, footer)

│   │

│   ├── lib/                        # Utilidades compartidas

│   │   ├── api.ts                  # Configuración de Axios (base URL)

│   │   ├── config.ts               # Variables de configuración

│   │   └── storage.ts              # LocalStorage helpers (tokens)

│   │

│   ├── features/                   # Features por módulo

│   │   ├── auth/                   # Autenticación

│   │   │   ├── authApi.ts          # API calls de auth

│   │   │   └── pages/

│   │   │       ├── LoginPage.tsx   # Página de login

│   │   │       └── RegisterPage.tsx # Página de registro

│   │   │

│   │   ├── catalog/                # Catálogo de productos

│   │   │   ├── catalogApi.ts       # React Query hooks

│   │   │   └── pages/

│   │   │       ├── ProductsPage.tsx    # Lista de productos

│   │   │       └── ProductDetailPage.tsx # Detalle de producto

│   │   │

│   │   ├── orders/                 # Gestión de pedidos

│   │   │   ├── ordersApi.ts        # React Query hooks

│   │   │   └── pages/

│   │   │       ├── OrdersPage.tsx       # Mis pedidos

│   │   │       └── CreateOrderPage.tsx  # Crear pedido

│   │   │

│   │   └── admin/                  # Panel administrativo

│   │       └── pages/

│   │           ├── AdminUsersPage.tsx   # Gestión de usuarios

│   │           ├── AdminOrdersPage.tsx  # Gestión de pedidos

│   │           └── AdminProductsPage.tsx # Gestión de productos

│   │

│   └── assets/                     # Imágenes y recursos estáticos

│

├── public/                         # Archivos públicos

├── package.json                    # Dependencias npm

├── vite.config.ts                  # Configuración de Vite

├── tailwind.config.js              # Configuración de Tailwind

└── tsconfig.json                   # Configuración de TypeScript

```

 

### Comunicación Frontend → Backend

 

El frontend **NUNCA** se comunica directamente con los microservicios. Toda la comunicación pasa por el **API Gateway**.

 

```

┌──────────────┐

│   Frontend   │

│  React App   │

└──────┬───────┘

       │

       │ HTTP Requests (Axios)

       │ Authorization: Bearer <JWT_TOKEN>

       │

       ▼

┌──────────────────────┐

│    API Gateway       │

│  localhost:5000      │

│                      │

│  • Valida JWT        │

│  • Rate Limiting     │

│  • Enruta request    │

└──────┬───────────────┘

       │

       ├──▶ Identity Service

       ├──▶ Catalog Service

       └──▶ Orders Service

```

 

### Variables de Entorno del Frontend

 

El frontend utiliza variables de entorno para la configuración:

 

```bash

# .env (desarrollo)

VITE_API_GATEWAY_URL=http://localhost:5000

VITE_PORT=5173

```

 

---

 

## API Gateway - Punto de Entrada Único

 

El **API Gateway** actúa como un **reverse proxy inteligente** que enruta todas las peticiones del frontend a los microservicios correspondientes.

 

### Características del Gateway

 

1. **Reverse Proxy con YARP**

   - Enrutamiento dinámico basado en paths

   - Load balancing automático

   - Service Discovery con .NET Aspire

 

2. **Rate Limiting con Redis**

   - Límite de peticiones por usuario

   - Políticas diferenciadas: `anonymous` y `authenticated`

   - Prevención de abuso de API

 

3. **Autenticación Centralizada**

   - Validación de JWT tokens

   - Políticas de autorización: `anonymous`, `authenticated`, `admin`

   - Headers de autorización propagados a microservicios

 

4. **CORS Configurado**

   - Permite peticiones desde el frontend

   - Headers permitidos: `Authorization`, `Content-Type`

   - Métodos HTTP: GET, POST, PUT, DELETE, PATCH

 

### Rutas del API Gateway

 

```yaml

# Rutas de Identity

/api/v1/auth/*              → Identity Service (anónimo)

/api/v1/users/*             → Identity Service (autenticado)

/api/v1/admin/users/*       → Identity Service (admin)

/api/v1/admin/roles/*       → Identity Service (admin)

 

# Rutas de Catalog

/api/v1/categories/*        → Catalog Service (anónimo)

/api/v1/products/*          → Catalog Service (anónimo)

 

# Rutas de Orders

/api/v1/orders/*            → Orders Service (autenticado)

/api/v1/admin/orders/*      → Orders Service (admin)

```

 

### Ejemplo de Uso desde Frontend

 

```typescript

// lib/api.ts

import axios from 'axios';

 

const api = axios.create({

  baseURL: import.meta.env.VITE_API_GATEWAY_URL, // http://localhost:5000

  headers: {

    'Content-Type': 'application/json',

  },

});

 

// Interceptor para agregar JWT token

api.interceptors.request.use((config) => {

  const token = localStorage.getItem('token');

  if (token) {

    config.headers.Authorization = `Bearer ${token}`;

  }

  return config;

});

 

// features/auth/authApi.ts

export const login = async (email: string, password: string) => {

  const response = await api.post('/api/v1/auth/login', { email, password });

  return response.data;

};

 

// features/catalog/catalogApi.ts

export const getProducts = async () => {

  const response = await api.get('/api/v1/products');

  return response.data;

};

 

// features/orders/ordersApi.ts

export const createOrder = async (orderData: CreateOrderDto) => {

  const response = await api.post('/api/v1/orders', orderData);

  return response.data;

};

```

 

---

 

## Instalación y Ejecución

 

### Requisitos Previos

 

- [.NET 10 SDK](https://dotnet.microsoft.com/download) o superior

- [Docker Desktop](https://www.docker.com/products/docker-desktop)

- [Node.js 18+](https://nodejs.org/) y npm/yarn (para el frontend)

- [Visual Studio 2022](https://visualstudio.microsoft.com/) o [VS Code](https://code.visualstudio.com/)

 

### Opción 1: Con .NET Aspire (Recomendado)

 

**.NET Aspire** orquesta automáticamente todos los servicios, bases de datos y el frontend.

 

```bash

# Clonar el repositorio

git clone <repository-url>

cd Orderflow_project

 

# Ejecutar con Aspire (desde la raíz)

dotnet run --project Orderflow.AppHost

```

 

El **Dashboard de Aspire** estará disponible en: `https://localhost:17225`

 

Desde el dashboard puedes:

- Ver el estado de todos los servicios

- Acceder a logs en tiempo real

- Monitorear métricas y traces (OpenTelemetry)

- Acceder a las interfaces web (MailDev, RabbitMQ Management)

 

### Opción 2: Ejecutar Servicios Manualmente

 

#### 1. Iniciar Infraestructura (Docker)

 

```bash

docker-compose up -d

```

 

Esto inicia:

- PostgreSQL 16 (puerto 5432)

- Redis 7 (puerto 6379)

 

#### 2. Ejecutar Backend (cada servicio en su terminal)

 

```bash

# Terminal 1: Identity Service

dotnet run --project Orderflow.Identity

 

# Terminal 2: Catalog Service

dotnet run --project Orderflow.Catalog

 

# Terminal 3: Orders Service

dotnet run --project Orderflow.Orders

 

# Terminal 4: Notifications Worker

dotnet run --project Orderflow.Notifications

 

# Terminal 5: API Gateway

dotnet run --project Orderflow.API.Gateway

```

 

#### 3. Ejecutar Frontend

 

```bash

cd Orderflow.Web

 

# Instalar dependencias (primera vez)

npm install

 

# Ejecutar en desarrollo

npm run dev

```

 

El frontend estará disponible en: `http://localhost:5173` (o el puerto configurado en `VITE_PORT`)

 

---

 

## Documentación de API

 

Cada servicio expone su documentación OpenAPI interactiva con **Scalar**:

 

| Servicio | URL |

|----------|-----|

| **API Gateway** | http://localhost:5000/scalar/v1 |

| **Identity** | http://localhost:5001/scalar/v1 |

| **Catalog** | http://localhost:5002/scalar/v1 |

| **Orders** | http://localhost:5003/scalar/v1 |

 

### Credenciales de Desarrollo

 

Para probar los endpoints administrativos:

 

| Campo | Valor |

|-------|-------|

| Email | admin@admin.com |

| Password | Test12345. |

| Rol | Admin |

 

---

 

## Testing

 

### Backend - Tests Unitarios

 

Los tests utilizan **NUnit** y **Moq** para mocking.

 

```bash

# Ejecutar todos los tests

dotnet test

 

# Ejecutar tests con cobertura

dotnet test --collect:"XPlat Code Coverage"

 

# Ejecutar tests de un proyecto específico

dotnet test Orderflow.Api.Identity.Test

```

 

**Proyectos de tests:**

- `Orderflow.Api.Identity.Test/` - Tests para Identity Service

  - AuthServiceTests

  - UserServiceTests

  - RoleServiceTests

 

### Cobertura de Código

 

El proyecto incluye tests unitarios exhaustivos para:

- ✅ Servicios de autenticación (AuthService, TokenService)

- ✅ Servicios de usuarios (UserService - CRUD, roles, bloqueos)

- ✅ Servicios de roles (RoleService)

 

---

 

## Flujos de Negocio Principales

 

### 1. Registro y Login de Usuario

 

```

1. Usuario accede a /register en el frontend

2. Frontend envía POST /api/v1/auth/register al Gateway

3. Gateway enruta a Identity Service

4. Identity crea usuario con rol "Customer"

5. Identity publica UserRegisteredEvent a RabbitMQ

6. Notifications envía email de bienvenida

7. Frontend recibe JWT token

8. Token se guarda en localStorage

```

 

### 2. Crear un Pedido (Flow Completo)

 

```

┌─────────┐  POST /api/v1/orders   ┌────────────┐

│Frontend │ ────────────────────▶  │ API Gateway│

└─────────┘   + JWT Token          └──────┬─────┘

                                          │ Valida JWT

                                          │ Rate Limit

                                          ▼

┌──────────────────────────────────────────────────────┐

│                 Orders Service                       │

│                                                      │

│  1. Valida items del pedido                         │

│  2. Llama a Catalog Service (HTTP) para validar     │

│     productos y disponibilidad de stock             │

│                                                      │

└────────┬─────────────────────────────────────────────┘

         │

         ▼

┌──────────────────────────────────────────────────────┐

│              Catalog Service                         │

│                                                      │

│  3. Valida que productos existen y están activos    │

│  4. Reserva stock (QuantityReserved += cantidad)    │

│  5. Actualiza QuantityAvailable -= cantidad         │

│                                                      │

└────────┬─────────────────────────────────────────────┘

         │

         ▼

┌──────────────────────────────────────────────────────┐

│              Orders Service                          │

│                                                      │

│  6. Crea pedido en estado "Pending"                 │

│  7. Guarda OrderItems en base de datos              │

│  8. Publica OrderCreatedEvent a RabbitMQ            │

│  9. Retorna pedido creado (201)                     │

│                                                      │

└────────┬─────────────────────────────────────────────┘

         │

         ▼

┌──────────────────────────────────────────────────────┐

│          Notifications Worker Service                │

│                                                      │

│  10. Escucha OrderCreatedEvent                      │

│  11. Genera email de confirmación                   │

│  12. Envía email via MailKit/SMTP                   │

│                                                      │

└──────────────────────────────────────────────────────┘

```

 

### 3. Cancelar un Pedido

 

```

1. Usuario envía POST /api/v1/orders/{id}/cancel

2. Orders valida que el usuario sea el propietario

3. Orders valida que el estado permita cancelación (Pending/Confirmed)

4. Orders llama a Catalog para liberar el stock reservado

5. Catalog libera stock (QuantityReserved -= cantidad)

6. Orders actualiza estado del pedido a "Cancelled"

7. Orders publica OrderCancelledEvent a RabbitMQ

8. Notifications envía email de cancelación

```

 

---

 

## Comunicación entre Servicios

 

### Comunicación Síncrona (HTTP/REST)

 

**Casos de uso:** Cuando se necesita respuesta inmediata o validación.

 

```

Orders Service ──HTTP──▶ Catalog Service

  • Validar productos

  • Reservar stock

  • Liberar stock

```

 

**Implementación:**

- HttpClient con políticas de retry (Polly)

- Circuit breaker para resiliencia

- Service Discovery automático con Aspire

 

### Comunicación Asíncrona (RabbitMQ/MassTransit)

 

**Casos de uso:** Operaciones no críticas, desacoplamiento, notificaciones.

 

```

Identity/Orders ──RabbitMQ──▶ Notifications Worker

  • UserRegisteredEvent

  • OrderCreatedEvent

  • OrderCancelledEvent

```

 

**Ventajas:**

- Desacoplamiento total entre servicios

- Procesamiento en background

- Reintentos automáticos

- Tolerancia a fallos

 

---

 

## Observabilidad

 

El proyecto incluye **OpenTelemetry** para monitoreo completo:

 

### Traces

- Seguimiento de requests entre servicios

- Identificación de cuellos de botella

- Análisis de latencia end-to-end

 

### Metrics

- Contadores de requests

- Latencia de endpoints

- Tasa de errores

 

### Logs

- Logging estructurado con Serilog

- Contexto de correlación entre servicios

- Niveles configurables por ambiente

 

**Dashboard de Aspire** proporciona visualización integrada de toda la telemetría.

 

---

 

## Configuración

 

### Variables de Entorno (Backend)

 

Las variables se configuran en `appsettings.json` o variables de entorno.

 

```bash

# JWT Configuration

Jwt__Secret=<tu-clave-secreta-minimo-32-caracteres>

Jwt__Issuer=Orderflow.Identity

Jwt__Audience=Orderflow.Api

Jwt__ExpiryInMinutes=60

 

# Connection Strings (manejadas por Aspire en desarrollo)

ConnectionStrings__identitydb=Host=localhost;Database=identitydb;Username=postgres;Password=postgres

ConnectionStrings__catalogdb=Host=localhost;Database=catalogdb;Username=postgres;Password=postgres

ConnectionStrings__ordersdb=Host=localhost;Database=ordersdb;Username=postgres;Password=postgres

 

# RabbitMQ (manejado por Aspire)

ConnectionStrings__messaging=amqp://localhost:5672

 

# Email (MailDev en desarrollo)

Email__SmtpHost=localhost

Email__SmtpPort=1025

Email__FromEmail=noreply@orderflow.com

Email__FromName=OrderFlow

```

 

> **Nota:** Con .NET Aspire, las connection strings se configuran automáticamente mediante service discovery.

 

---

 

## Estado del Proyecto

 

### ✅ Backend (Completado)

- [x] Microservicio de Identity con autenticación JWT

- [x] Microservicio de Catalog con gestión de inventario

- [x] Microservicio de Orders con estados y validaciones

- [x] Worker de Notifications con RabbitMQ

- [x] API Gateway con YARP y rate limiting

- [x] Integración completa con RabbitMQ/MassTransit

- [x] Bases de datos PostgreSQL con migraciones

- [x] OpenTelemetry para observabilidad

- [x] Tests unitarios para servicios críticos

 

### 🚧 Frontend (En Desarrollo)

- [x] Configuración inicial con React 19 + TypeScript

- [x] Integración con API Gateway

- [x] Página de Login

- [x] Página de Productos (Catálogo)

- [ ] Página de Registro

- [ ] Página de Detalle de Producto

- [ ] Página de Mis Pedidos

- [ ] Página de Crear Pedido (Carrito)

- [ ] Panel Administrativo (Usuarios, Pedidos, Productos)

- [ ] Página de Perfil de Usuario

 

---

 

## Estructura del Repositorio

 

```

Orderflow_project/

├── Orderflow.Identity/          # Microservicio de autenticación

├── Orderflow.Catalog/           # Microservicio de catálogo

├── Orderflow.Orders/            # Microservicio de pedidos

├── Orderflow.Notifications/     # Worker de notificaciones

├── Orderflow.API.Gateway/       # API Gateway (YARP)

├── Orderflow.Web/               # Frontend React

├── Orderflow.AppHost/           # Orquestación con Aspire

├── Orderflow.ServiceDefaults/   # Configuración compartida

├── Orderflow.Shared/             # DTOs, eventos y extensiones

├── Orderflow.Api.Identity.Test/ # Tests del servicio Identity

├── TestOrderflow.Console/       # Tests de integración

├── docker-compose.yaml          # Infraestructura (PostgreSQL, Redis)

├── ProyectoOrderflow.sln        # Solución de .NET

└── README.md                    # Este archivo

```

 

---

 

## Licencia

 

Este proyecto está bajo la licencia MIT.

 

---

 

## Contribuir

 

1. Fork el repositorio

2. Crea una rama para tu feature (`git checkout -b feature/nueva-funcionalidad`)

3. Commit tus cambios (`git commit -m 'feat: añadir nueva funcionalidad'`)

4. Push a la rama (`git push origin feature/nueva-funcionalidad`)

5. Abre un Pull Request

 

---

 

## Contacto y Soporte

 

- **Issues:** Reporta problemas en [GitHub Issues](https://github.com/tu-usuario/orderflow/issues)

- **Documentación:** Este README y la documentación OpenAPI de cada servicio

- **Aspire Dashboard:** https://localhost:17225 (durante desarrollo)

 

---

 

**¡Gracias por usar OrderFlow!** 🚀