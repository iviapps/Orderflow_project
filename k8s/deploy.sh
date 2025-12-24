#!/bin/bash
# =============================================================================
# OrderFlow Kubernetes Deployment Script
# =============================================================================
# Usage: ./deploy.sh [command] [options]
#
# Commands:
#   deploy      - Deploy all resources to Kubernetes
#   delete      - Delete all OrderFlow resources
#   status      - Show deployment status
#   logs        - Show logs for a service
#   build       - Build and push Docker images
#
# Options:
#   --namespace, -n  Kubernetes namespace (default: orderflow)
#   --env, -e        Environment: dev, staging, prod (default: dev)
#   --dry-run        Show what would be applied without applying
# =============================================================================

set -euo pipefail

# Configuration
NAMESPACE="${NAMESPACE:-orderflow}"
ENVIRONMENT="${ENVIRONMENT:-dev}"
REGISTRY="${REGISTRY:-ghcr.io/iviapps}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

log_info() { echo -e "${BLUE}[INFO]${NC} $1"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $1"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $1"; }
log_error() { echo -e "${RED}[ERROR]${NC} $1"; }

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    if ! command -v kubectl &> /dev/null; then
        log_error "kubectl is not installed"
        exit 1
    fi

    if ! command -v docker &> /dev/null; then
        log_error "docker is not installed"
        exit 1
    fi

    if ! kubectl cluster-info &> /dev/null; then
        log_error "Cannot connect to Kubernetes cluster"
        exit 1
    fi

    log_success "Prerequisites check passed"
}

# Build Docker images
build_images() {
    log_info "Building Docker images..."

    local services=("identity" "catalog" "orders" "notifications" "gateway" "frontend")

    for service in "${services[@]}"; do
        log_info "Building orderflow-$service..."

        case $service in
            identity)
                docker build -t "$REGISTRY/orderflow-identity:latest" \
                    -f "$PROJECT_ROOT/Orderflow.Identity/Dockerfile" \
                    "$PROJECT_ROOT"
                ;;
            catalog)
                docker build -t "$REGISTRY/orderflow-catalog:latest" \
                    -f "$PROJECT_ROOT/Orderflow.Catalog/Dockerfile" \
                    "$PROJECT_ROOT"
                ;;
            orders)
                docker build -t "$REGISTRY/orderflow-orders:latest" \
                    -f "$PROJECT_ROOT/Orderflow.Orders/Dockerfile" \
                    "$PROJECT_ROOT"
                ;;
            notifications)
                docker build -t "$REGISTRY/orderflow-notifications:latest" \
                    -f "$PROJECT_ROOT/Orderflow.Notifications/Dockerfile" \
                    "$PROJECT_ROOT"
                ;;
            gateway)
                docker build -t "$REGISTRY/orderflow-gateway:latest" \
                    -f "$PROJECT_ROOT/Orderflow.Api.Gateway/Dockerfile" \
                    "$PROJECT_ROOT"
                ;;
            frontend)
                docker build -t "$REGISTRY/orderflow-frontend:latest" \
                    -f "$PROJECT_ROOT/Orderflow.web/Dockerfile" \
                    "$PROJECT_ROOT/Orderflow.web"
                ;;
        esac

        log_success "Built orderflow-$service"
    done
}

# Push Docker images
push_images() {
    log_info "Pushing Docker images to registry..."

    local services=("identity" "catalog" "orders" "notifications" "gateway" "frontend")

    for service in "${services[@]}"; do
        log_info "Pushing orderflow-$service..."
        docker push "$REGISTRY/orderflow-$service:latest"
        log_success "Pushed orderflow-$service"
    done
}

# Deploy infrastructure
deploy_infrastructure() {
    log_info "Deploying infrastructure..."

    # Apply in order
    kubectl apply -f "$SCRIPT_DIR/base/namespace.yaml"
    kubectl apply -f "$SCRIPT_DIR/base/secrets.yaml"
    kubectl apply -f "$SCRIPT_DIR/base/configmap.yaml"
    kubectl apply -f "$SCRIPT_DIR/base/rbac.yaml"

    log_info "Waiting for namespace to be ready..."
    kubectl wait --for=condition=Active namespace/$NAMESPACE --timeout=60s

    # Deploy databases and message broker
    kubectl apply -f "$SCRIPT_DIR/infrastructure/postgres.yaml"
    kubectl apply -f "$SCRIPT_DIR/infrastructure/redis.yaml"
    kubectl apply -f "$SCRIPT_DIR/infrastructure/rabbitmq.yaml"

    log_info "Waiting for infrastructure pods to be ready..."
    kubectl -n $NAMESPACE wait --for=condition=Ready pod -l app.kubernetes.io/component=database --timeout=300s || true
    kubectl -n $NAMESPACE wait --for=condition=Ready pod -l app.kubernetes.io/component=cache --timeout=120s || true
    kubectl -n $NAMESPACE wait --for=condition=Ready pod -l app.kubernetes.io/component=messaging --timeout=180s || true

    log_success "Infrastructure deployed"
}

# Deploy microservices
deploy_services() {
    log_info "Deploying microservices..."

    # Deploy services in dependency order
    kubectl apply -f "$SCRIPT_DIR/services/identity.yaml"
    kubectl apply -f "$SCRIPT_DIR/services/catalog.yaml"
    kubectl apply -f "$SCRIPT_DIR/services/notifications.yaml"
    kubectl apply -f "$SCRIPT_DIR/services/orders.yaml"
    kubectl apply -f "$SCRIPT_DIR/services/gateway.yaml"
    kubectl apply -f "$SCRIPT_DIR/services/frontend.yaml"

    log_info "Waiting for services to be ready..."
    kubectl -n $NAMESPACE wait --for=condition=Ready pod -l app.kubernetes.io/part-of=orderflow-platform --timeout=300s || true

    log_success "Microservices deployed"
}

# Deploy ingress and HPA
deploy_extras() {
    log_info "Deploying ingress and autoscaling..."

    kubectl apply -f "$SCRIPT_DIR/ingress/ingress.yaml" || log_warning "Ingress deployment failed (controller may not be installed)"
    kubectl apply -f "$SCRIPT_DIR/base/hpa.yaml" || log_warning "HPA deployment failed (metrics-server may not be installed)"

    log_success "Extras deployed"
}

# Full deployment
deploy_all() {
    check_prerequisites
    deploy_infrastructure
    deploy_services
    deploy_extras
    show_status
}

# Delete all resources
delete_all() {
    log_warning "Deleting all OrderFlow resources..."
    read -p "Are you sure? (y/N) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        kubectl delete namespace $NAMESPACE --ignore-not-found
        log_success "All resources deleted"
    else
        log_info "Deletion cancelled"
    fi
}

# Show status
show_status() {
    log_info "OrderFlow Deployment Status"
    echo "============================================"

    echo ""
    log_info "Pods:"
    kubectl -n $NAMESPACE get pods -o wide 2>/dev/null || log_warning "No pods found"

    echo ""
    log_info "Services:"
    kubectl -n $NAMESPACE get services 2>/dev/null || log_warning "No services found"

    echo ""
    log_info "Deployments:"
    kubectl -n $NAMESPACE get deployments 2>/dev/null || log_warning "No deployments found"

    echo ""
    log_info "StatefulSets:"
    kubectl -n $NAMESPACE get statefulsets 2>/dev/null || log_warning "No statefulsets found"

    echo ""
    log_info "Ingress:"
    kubectl -n $NAMESPACE get ingress 2>/dev/null || log_warning "No ingress found"

    echo ""
    log_info "HPA:"
    kubectl -n $NAMESPACE get hpa 2>/dev/null || log_warning "No HPA found"
}

# Show logs
show_logs() {
    local service="${1:-}"
    if [[ -z "$service" ]]; then
        log_error "Please specify a service name"
        echo "Available services: identity, catalog, orders, notifications, gateway, frontend, postgres, redis, rabbitmq"
        exit 1
    fi

    kubectl -n $NAMESPACE logs -l app.kubernetes.io/name=orderflow-$service -f --tail=100
}

# Port forward for local access
port_forward() {
    log_info "Setting up port forwarding..."
    log_info "API Gateway will be available at http://localhost:8080"
    log_info "Frontend will be available at http://localhost:3000"
    log_info "RabbitMQ Management at http://localhost:15672"
    log_info "Press Ctrl+C to stop"

    kubectl -n $NAMESPACE port-forward svc/orderflow-gateway 8080:80 &
    kubectl -n $NAMESPACE port-forward svc/orderflow-frontend 3000:80 &
    kubectl -n $NAMESPACE port-forward svc/orderflow-rabbitmq 15672:15672 &

    wait
}

# Main
main() {
    local command="${1:-deploy}"
    shift || true

    case "$command" in
        deploy|up)
            deploy_all
            ;;
        build)
            build_images
            ;;
        push)
            push_images
            ;;
        build-push)
            build_images
            push_images
            ;;
        infrastructure|infra)
            check_prerequisites
            deploy_infrastructure
            ;;
        services)
            check_prerequisites
            deploy_services
            ;;
        delete|down)
            delete_all
            ;;
        status|ps)
            show_status
            ;;
        logs)
            show_logs "$@"
            ;;
        port-forward|pf)
            port_forward
            ;;
        help|--help|-h)
            echo "Usage: $0 [command]"
            echo ""
            echo "Commands:"
            echo "  deploy, up       Deploy all resources"
            echo "  build            Build Docker images"
            echo "  push             Push images to registry"
            echo "  build-push       Build and push images"
            echo "  infrastructure   Deploy only infrastructure"
            echo "  services         Deploy only microservices"
            echo "  delete, down     Delete all resources"
            echo "  status, ps       Show deployment status"
            echo "  logs <service>   Show logs for a service"
            echo "  port-forward     Setup port forwarding"
            echo "  help             Show this help"
            ;;
        *)
            log_error "Unknown command: $command"
            echo "Run '$0 help' for usage"
            exit 1
            ;;
    esac
}

main "$@"
