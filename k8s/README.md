# OrderFlow - Kubernetes Deployment Guide

Esta guía describe el procedimiento completo para desplegar la plataforma OrderFlow en Kubernetes.

## Arquitectura en Kubernetes

```
                        ┌─────────────────────────────────────────────────────────────┐
                        │                    KUBERNETES CLUSTER                        │
                        │  ┌─────────────────────────────────────────────────────────┐ │
                        │  │                   Namespace: orderflow                   │ │
                        │  │                                                         │ │
    ┌──────────┐        │  │  ┌─────────────┐                                       │ │
    │ Internet │◄───────┼──┼─►│   Ingress   │                                       │ │
    └──────────┘        │  │  │  (nginx)    │                                       │ │
                        │  │  └──────┬──────┘                                       │ │
                        │  │         │                                               │ │
                        │  │         ▼                                               │ │
                        │  │  ┌─────────────┐     ┌─────────────┐                   │ │
                        │  │  │  Frontend   │     │ API Gateway │◄──► Redis         │ │
                        │  │  │  (React)    │     │   (YARP)    │   (rate limit)    │ │
                        │  │  └─────────────┘     └──────┬──────┘                   │ │
                        │  │                             │                           │ │
                        │  │         ┌───────────────────┼───────────────────┐       │ │
                        │  │         ▼                   ▼                   ▼       │ │
                        │  │  ┌───────────┐       ┌───────────┐       ┌───────────┐ │ │
                        │  │  │ Identity  │       │  Catalog  │       │  Orders   │ │ │
                        │  │  │ Service   │       │  Service  │       │  Service  │ │ │
                        │  │  └─────┬─────┘       └─────┬─────┘       └─────┬─────┘ │ │
                        │  │        │                   │                   │       │ │
                        │  │        ▼                   ▼                   ▼       │ │
                        │  │  ┌───────────────────────────────────────────────────┐ │ │
                        │  │  │              PostgreSQL (StatefulSet)              │ │ │
                        │  │  │     identitydb    │    catalogdb    │   ordersdb  │ │ │
                        │  │  └───────────────────────────────────────────────────┘ │ │
                        │  │                                                         │ │
                        │  │  ┌─────────────┐     ┌───────────────────────────────┐ │ │
                        │  │  │Notifications│◄────│        RabbitMQ               │ │ │
                        │  │  │  (Worker)   │     │    (Message Broker)           │ │ │
                        │  │  └─────────────┘     └───────────────────────────────┘ │ │
                        │  │                                                         │ │
                        │  └─────────────────────────────────────────────────────────┘ │
                        └─────────────────────────────────────────────────────────────┘
```

## Prerrequisitos

### 1. Herramientas Necesarias
```bash
# Kubernetes CLI
kubectl version --client

# Docker
docker --version

# (Opcional) Helm para cert-manager
helm version
```

### 2. Cluster de Kubernetes
- **Desarrollo local**: minikube, kind, Docker Desktop
- **Cloud**: AKS (Azure), EKS (AWS), GKE (Google Cloud)

### 3. Ingress Controller (Recomendado)
```bash
# Instalar NGINX Ingress Controller
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.10.0/deploy/static/provider/cloud/deploy.yaml
```

### 4. Metrics Server (para HPA)
```bash
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml
```

## Procedimiento de Despliegue

### Opción A: Despliegue Rápido con Script

```bash
# 1. Hacer el script ejecutable
chmod +x k8s/deploy.sh

# 2. Construir imágenes Docker
./k8s/deploy.sh build

# 3. Subir imágenes al registry
./k8s/deploy.sh push

# 4. Desplegar todo
./k8s/deploy.sh deploy

# 5. Ver estado
./k8s/deploy.sh status
```

### Opción B: Despliegue con Kustomize

```bash
# Desplegar todo con un comando
kubectl apply -k k8s/

# O por partes:
kubectl apply -f k8s/base/namespace.yaml
kubectl apply -f k8s/base/secrets.yaml
kubectl apply -f k8s/base/configmap.yaml
kubectl apply -k k8s/
```

### Opción C: Despliegue Manual Paso a Paso

#### Paso 1: Crear Namespace y Configuración Base
```bash
kubectl apply -f k8s/base/namespace.yaml
kubectl apply -f k8s/base/secrets.yaml    # ¡Editar primero con valores reales!
kubectl apply -f k8s/base/configmap.yaml
kubectl apply -f k8s/base/rbac.yaml
```

#### Paso 2: Desplegar Infraestructura
```bash
# PostgreSQL
kubectl apply -f k8s/infrastructure/postgres.yaml

# Esperar a que PostgreSQL esté listo
kubectl -n orderflow wait --for=condition=Ready pod -l app.kubernetes.io/name=postgres --timeout=300s

# Redis
kubectl apply -f k8s/infrastructure/redis.yaml

# RabbitMQ
kubectl apply -f k8s/infrastructure/rabbitmq.yaml

# Verificar infraestructura
kubectl -n orderflow get pods
```

#### Paso 3: Construir y Subir Imágenes Docker
```bash
# Construir todas las imágenes
docker build -t ghcr.io/iviapps/orderflow-identity:latest -f Orderflow.Identity/Dockerfile .
docker build -t ghcr.io/iviapps/orderflow-catalog:latest -f Orderflow.Catalog/Dockerfile .
docker build -t ghcr.io/iviapps/orderflow-orders:latest -f Orderflow.Orders/Dockerfile .
docker build -t ghcr.io/iviapps/orderflow-notifications:latest -f Orderflow.Notifications/Dockerfile .
docker build -t ghcr.io/iviapps/orderflow-gateway:latest -f Orderflow.Api.Gateway/Dockerfile .
docker build -t ghcr.io/iviapps/orderflow-frontend:latest -f Orderflow.web/Dockerfile Orderflow.web/

# Subir al registry
docker push ghcr.io/iviapps/orderflow-identity:latest
docker push ghcr.io/iviapps/orderflow-catalog:latest
docker push ghcr.io/iviapps/orderflow-orders:latest
docker push ghcr.io/iviapps/orderflow-notifications:latest
docker push ghcr.io/iviapps/orderflow-gateway:latest
docker push ghcr.io/iviapps/orderflow-frontend:latest
```

#### Paso 4: Desplegar Microservicios
```bash
# Servicios base (sin dependencias entre ellos)
kubectl apply -f k8s/services/identity.yaml
kubectl apply -f k8s/services/catalog.yaml

# Servicios con dependencias
kubectl apply -f k8s/services/notifications.yaml
kubectl apply -f k8s/services/orders.yaml

# API Gateway (depende de todos los servicios)
kubectl apply -f k8s/services/gateway.yaml

# Frontend
kubectl apply -f k8s/services/frontend.yaml
```

#### Paso 5: Configurar Ingress y Autoscaling
```bash
kubectl apply -f k8s/ingress/ingress.yaml
kubectl apply -f k8s/base/hpa.yaml
```

## Configuración de Secrets

**IMPORTANTE**: Antes de desplegar, edita `k8s/base/secrets.yaml` con valores reales:

```yaml
stringData:
  jwt-secret: "TU_SECRET_JWT_SEGURO_DE_64_CARACTERES"
  postgres-password: "tu_password_postgres"
  rabbitmq-password: "tu_password_rabbitmq"
  redis-password: "tu_password_redis"
```

### Generar Secrets Seguros
```bash
# Generar JWT Secret
openssl rand -base64 64

# Generar password seguro
openssl rand -base64 32
```

### Usar Sealed Secrets (Producción)
```bash
# Instalar sealed-secrets
helm repo add sealed-secrets https://bitnami-labs.github.io/sealed-secrets
helm install sealed-secrets sealed-secrets/sealed-secrets

# Sellar secrets
kubeseal --format yaml < k8s/base/secrets.yaml > k8s/base/sealed-secrets.yaml
```

## Verificación del Despliegue

```bash
# Ver todos los recursos
kubectl -n orderflow get all

# Ver pods y su estado
kubectl -n orderflow get pods -w

# Ver logs de un servicio
kubectl -n orderflow logs -l app.kubernetes.io/name=orderflow-gateway -f

# Ver eventos
kubectl -n orderflow get events --sort-by='.lastTimestamp'

# Port-forward para pruebas locales
kubectl -n orderflow port-forward svc/orderflow-gateway 8080:80 &
kubectl -n orderflow port-forward svc/orderflow-frontend 3000:80 &
```

## Acceso a la Aplicación

### Con Ingress Configurado
- Frontend: `https://orderflow.yourdomain.com`
- API: `https://api.orderflow.yourdomain.com`

### Sin Ingress (Port-Forward)
```bash
./k8s/deploy.sh port-forward

# Acceder a:
# - Frontend: http://localhost:3000
# - API Gateway: http://localhost:8080
# - RabbitMQ Management: http://localhost:15672
```

## Monitoreo y Observabilidad

### Prometheus + Grafana (Recomendado)
```bash
# Instalar stack de monitoreo
helm repo add prometheus-community https://prometheus-community.github.io/helm-charts
helm install prometheus prometheus-community/kube-prometheus-stack -n monitoring --create-namespace
```

### Ver Métricas
Los servicios exponen métricas en `/metrics` con anotaciones Prometheus.

## Troubleshooting

### Pod no inicia
```bash
kubectl -n orderflow describe pod <pod-name>
kubectl -n orderflow logs <pod-name> --previous
```

### Problemas de conexión a la base de datos
```bash
# Verificar que PostgreSQL está corriendo
kubectl -n orderflow exec -it orderflow-postgres-0 -- psql -U orderflow_admin -d postgres -c "\\l"

# Verificar connectivity desde un pod
kubectl -n orderflow run -it --rm debug --image=busybox -- nc -zv orderflow-postgres 5432
```

### Reiniciar un deployment
```bash
kubectl -n orderflow rollout restart deployment/orderflow-gateway
```

## Limpieza

```bash
# Eliminar todo
kubectl delete namespace orderflow

# O con el script
./k8s/deploy.sh delete
```

## Estructura de Archivos

```
k8s/
├── base/
│   ├── namespace.yaml      # Namespace y ResourceQuota
│   ├── secrets.yaml        # Secrets (editar antes de usar)
│   ├── configmap.yaml      # Configuración no sensible
│   ├── rbac.yaml           # ServiceAccount y NetworkPolicies
│   └── hpa.yaml            # HorizontalPodAutoscaler y PDBs
├── infrastructure/
│   ├── postgres.yaml       # PostgreSQL StatefulSet
│   ├── redis.yaml          # Redis Deployment
│   └── rabbitmq.yaml       # RabbitMQ StatefulSet
├── services/
│   ├── identity.yaml       # Identity Service
│   ├── catalog.yaml        # Catalog Service
│   ├── orders.yaml         # Orders Service
│   ├── notifications.yaml  # Notifications Worker
│   ├── gateway.yaml        # API Gateway
│   └── frontend.yaml       # React Frontend
├── ingress/
│   └── ingress.yaml        # Ingress rules
├── deploy.sh               # Script de despliegue
├── kustomization.yaml      # Kustomize config
└── README.md               # Esta documentación
```

## Comparación: Aspire vs YAML Manual

| Característica | Aspire k8s-output | YAML Manual |
|---------------|-------------------|-------------|
| Automatización | Alta | Baja |
| Control | Limitado | Total |
| Producción-Ready | Preview | Sí |
| Personalización | Limitada | Completa |
| Mantenimiento | Sincronizado con código | Separado |
| Curva de aprendizaje | Baja | Media |

**Recomendación**: Para desarrollo/pruebas usar Aspire, para producción usar YAML manual o Helm.
