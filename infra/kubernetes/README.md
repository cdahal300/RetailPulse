# RetailPulse Kubernetes Deployment

This directory contains Kubernetes manifests for deploying RetailPulse to AKS.

## Directory Structure

```
infra/kubernetes/
├── base/                  # Base manifests shared across all environments
│   ├── namespace.yaml     # Namespace, RBAC, and network policies
│   ├── cloud-api.yaml     # Cloud API deployment and service
│   ├── edge-api.yaml      # Edge API deployment and service
│   └── ingress.yaml       # Ingress for external traffic
├── overlays/
│   ├── dev/               # Development environment customization
│   ├── staging/           # Staging environment customization
│   └── prod/              # Production environment customization
└── README.md              # This file
```

## Prerequisites

1. AKS cluster with:
   - Workload identity enabled
   - Network policies enabled (Azure CNI)
   - Ingress controller installed

2. Azure Container Registry with published images:
   - `retailpulse-cloud:latest`
   - `retailpulse-edge:latest`

3. PostgreSQL and Redis instances (managed or self-hosted):
   - Update connection strings in `base/namespace.yaml`

## Deployment

### Using kubectl

```bash
# Development
kubectl apply -k infra/kubernetes/overlays/dev

# Staging
kubectl apply -k infra/kubernetes/overlays/staging

# Production
kubectl apply -k infra/kubernetes/overlays/prod
```

### Verifying Deployment

```bash
# Check namespace
kubectl get ns -l app=retailpulse

# Check deployments
kubectl get deployments -n retailpulse

# Check pods
kubectl get pods -n retailpulse

# Check services
kubectl get svc -n retailpulse

# Check ingress
kubectl get ingress -n retailpulse

# Check health
kubectl get events -n retailpulse
kubectl logs -n retailpulse -l app=cloud-api
kubectl logs -n retailpulse -l app=edge-api
```

## Health Checks

Both APIs expose health check endpoints for Kubernetes probes:

- **Liveness probe**: `GET /health/live`
  - Returns 200 if the service is running
  - Used by Kubernetes to detect and restart unhealthy pods

- **Readiness probe**: `GET /health/ready`
  - Returns 200 if the service is ready to accept traffic
  - Edge API also checks SQLite persistence availability
  - Used by Kubernetes to route traffic only to ready pods

## Network Policies

The deployment includes three network policies:

1. **Deny all ingress** (default secure posture)
2. **Allow ingress controller traffic** to Cloud API
3. **Allow internal traffic** from Cloud API to Edge API

## Workload Identity

Applications use Azure Workload Identity (OIDC) for authentication:

1. Create a user-assigned managed identity in Azure
2. Update the service account annotation with the client ID
3. Grant the identity appropriate Azure RBAC roles (Key Vault, Storage, etc.)

## Configuration

### Secrets (from Azure Key Vault)

- Database connection strings
- Redis connection strings
- API keys and credentials

Update `base/namespace.yaml` with:
- Actual database host and credentials
- Redis endpoint
- Any other sensitive configuration

### ConfigMaps

Application settings are in `base/namespace.yaml`:

```yaml
data:
  ASPNETCORE_ENVIRONMENT: Production
  ASPNETCORE_URLS: "http://+:5000"
```

## Rolling Updates

Deployments use `RollingUpdate` strategy:

- `maxSurge: 1` - Allow one extra pod during update
- `maxUnavailable: 0` - Never take down pods (ensures availability)

Traffic is only routed to Ready pods via readiness probes.

## Scaling

Horizontal Pod Autoscalers are configured for both APIs:

**Cloud API:**
- Minimum: 3 replicas
- Maximum: 10 replicas
- Triggers: CPU 70%, Memory 80%

**Edge API:**
- Minimum: 2 replicas
- Maximum: 8 replicas
- Triggers: CPU 70%, Memory 80%

## Observability

Pods include Prometheus annotations for scraping metrics:

```yaml
prometheus.io/scrape: "true"
prometheus.io/port: "5000"
prometheus.io/path: "/metrics"
```

Configure Prometheus ServiceMonitor to collect metrics from `retailpulse` namespace.

## Security

- Non-root containers (UID 1000)
- Read-only root filesystem
- No privilege escalation
- Dropped all Linux capabilities
- Network policies enforcing least privilege
- Pod anti-affinity to spread across nodes

## Troubleshooting

### Pod not starting

```bash
kubectl describe pod -n retailpulse <pod-name>
kubectl logs -n retailpulse <pod-name> --previous
```

### Readiness probe failing

Check the service is healthy:

```bash
kubectl port-forward -n retailpulse svc/cloud-api 5000:5000
curl http://localhost:5000/health/ready
```

### Network connectivity

Verify network policies:

```bash
kubectl get networkpolicies -n retailpulse
kubectl describe networkpolicy -n retailpulse retailpulse-allow-ingress-controller
```

## References

- [AKS Documentation](https://docs.microsoft.com/azure/aks/)
- [Kubernetes Deployments](https://kubernetes.io/docs/concepts/workloads/controllers/deployment/)
- [Network Policies](https://kubernetes.io/docs/concepts/services-networking/network-policies/)
- [Workload Identity](https://azure.github.io/azure-workload-identity/docs/)
