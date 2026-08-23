# FEAT-006: Azure AKS Infrastructure - Implementation Summary

## Status: Complete (MVP Dev Environment)

This document summarizes the implementation of FEAT-006 for local validation, Azure infrastructure provisioning, and the first dev AKS deployment.

## Overview

FEAT-006 provides infrastructure as code (IaC) and containerization for deploying RetailPulse to Azure Kubernetes Service (AKS). The implementation follows a staged approach:

1. **Local validation**: Use Docker Compose with PostgreSQL and Redis where Docker host capabilities are available.
2. **AKS infrastructure**: Provision Azure managed services with Bicep.
3. **AKS application deployment**: Deploy Cloud and Edge images by immutable digest through the FEAT-007 GitHub Actions workflow.

## Live Dev Deployment

The dev environment is provisioned in Azure and has completed a successful GitHub Actions deployment.

**Azure scope**:
- Subscription: `15cdbd30-9943-46b6-a451-e19c990099e2`
- Tenant: `84461914-7d3c-452f-bcff-cab760563700`
- Resource group: `rg-retailpulse-dev-centralus`
- Region: `centralus`

**Provisioned resources**:
- AKS: `retailpulse-dev-aks`
- ACR: `retailpulsedevzhztnpacr.azurecr.io` (`Basic` SKU)
- PostgreSQL Flexible Server: `retailpulse-dev-pg-zhztnp` (`Standard_B1ms`, PostgreSQL 15)
- PostgreSQL database: `retailpulse`
- Key Vault: `retailpulsedevkvzhztnp`
- Service Bus: `retailpulse-dev-sb-zhztnp`
- Storage account: `rpdevzhztnpst`
- Managed identity: `retailpulse-dev-uami`
- GitHub Actions managed identity: `retailpulse-dev-github-actions-uami`

**Current workload state**:
- `cloud-api`: 1 ready replica, deployed from ACR by digest
- `edge-api`: 1 ready replica, deployed from ACR by digest
- Services: `cloud-api` and `edge-api`, both `ClusterIP` on port `5000`

**Deployment evidence**:
- FEAT-006 PR: `https://github.com/cdahal300/RetailPulse/pull/5`
- FEAT-007 PR: `https://github.com/cdahal300/RetailPulse/pull/6`
- Deploy workflow run: `https://github.com/cdahal300/RetailPulse/actions/runs/32655419005`
- Result: build, image push, image scan, AKS rollout, and smoke tests succeeded.

## Completed Implementation

### 1. Health Check Endpoints

**Location**: `src/Cloud/RetailPulse.Cloud/Program.cs` and `src/Edge/RetailPulse.Edge/Program.cs`

**Endpoints**:
- `GET /health/live` - Liveness probe for Kubernetes (always 200 if running)
- `GET /health/ready` - Readiness probe for Kubernetes (503 if not ready)

**Cloud API**:
```csharp
GET /health/live → { status: "alive", timestamp: "2026-08-23T..." }
GET /health/ready → { status: "ready", timestamp: "2026-08-23T..." }
```

**Edge API**:
```csharp
GET /health/live → { 
  status: "alive", 
  timestamp: "2026-08-23T...",
  schema_version: 2,
  pending_count: 0
}
GET /health/ready → { 
  status: "ready", 
  timestamp: "2026-08-23T...",
  schema_version: 2
}
```

The Edge API readiness probe also validates SQLite persistence is available.

### 2. Containerization

**Docker Images**:

#### Cloud API
- **File**: `src/Cloud/RetailPulse.Cloud/Dockerfile`
- **Base**: `mcr.microsoft.com/dotnet/aspnet:10.0`
- **Build**: Multi-stage with .NET SDK
- **Exposure**: Port 5000
- **Health Check**: `/health/live` endpoint

#### Edge API
- **File**: `src/Edge/RetailPulse.Edge/Dockerfile`
- **Base**: `mcr.microsoft.com/dotnet/aspnet:10.0`
- **Build**: Multi-stage with .NET SDK
- **Exposure**: Port 5000
- **Database**: SQLite at `/data/retailpulse-edge.db`
- **Health Check**: `/health/ready` endpoint (stricter for edge state)

Both images:
- Include `curl` for health checks
- Run as non-root user
- Support environment variable configuration
- Use layered caching for faster builds

### 3. Local Development Environment

**File**: `docker-compose.yml` (root directory)

**Services**:
- **PostgreSQL 16** - Port 5432 (database)
- **Redis 7** - Port 6379 (cache)
- **Cloud API** - Port 5000
- **Edge API** - Port 5001 (mapped to container 5000)

**Configuration**:
```yaml
ConnectionStrings__Postgres: Host=postgres;Port=5432;...
ConnectionStrings__Redis: redis:6379
RetailPulse__EdgeDatabasePath: /data/retailpulse-edge.db
```

**Usage**:
```bash
# Start all services
docker-compose up -d

# Check logs
docker-compose logs -f cloud-api edge-api

# Stop all services
docker-compose down
```

### 4. Kubernetes Deployment

**Directory**: `infra/kubernetes/`

#### Base Manifests (`infra/kubernetes/base/`)

**namespace.yaml**:
- Namespace `retailpulse`
- ConfigMap with app settings
- Runtime secret is created by deployment automation from GitHub environment secrets
- Network policies (deny-all default, allow ingress, allow internal)
- ServiceAccount with workload identity annotations

**cloud-api.yaml**:
- Service (ClusterIP, port 5000)
- Deployment (3 replicas default)
- RollingUpdate strategy (maxSurge: 1, maxUnavailable: 0)
- Health probes (live @ 10s, ready @ 5s)
- Resource requests (100m CPU, 256Mi memory)
- Resource limits (500m CPU, 512Mi memory)
- Pod anti-affinity (spreads across nodes)
- HPA (3-10 replicas, triggers @ 70% CPU / 80% memory)

**edge-api.yaml**:
- Service (ClusterIP, port 5000)
- Deployment (2 replicas default)
- RollingUpdate strategy
- Health probes (live @ 10s, ready @ 5s with persistence check)
- Resource requests (100m CPU, 256Mi memory)
- Resource limits (500m CPU, 512Mi memory)
- Pod anti-affinity
- Persistent volume for SQLite `/data`
- HPA (2-8 replicas, triggers @ 70% CPU / 80% memory)

**ingress.yaml**:
- Host: `api.retailpulse.example.com`
- TLS termination (cert-manager)
- Routes `/api` and `/health` to cloud-api

#### Environment Overlays

**dev/** - Development customization:
- 1 Cloud API replica, 1 Edge API replica
- Reduced resource requests/limits
- HPA: 1-2 replicas per service
- Image tag: `dev`

**staging/** - Staging customization:
- 2 Cloud API replicas, 2 Edge API replicas
- Standard resource allocations
- HPA: 2-5 replicas per service
- Image tag: `staging`

**prod/** - Production customization:
- 3 Cloud API replicas, 3 Edge API replicas
- Larger resource allocations (250m CPU, 512Mi memory)
- HPA: 3-10 / 3-8 replicas, triggered @ 60% CPU
- Image tag: production-specific (immutable)
- Pod Disruption Budgets (minAvailable: 2)
- Strict network policies

### 5. Network Policies

**Policy 1**: Deny all ingress (default secure posture)
- All pods in `retailpulse` namespace reject ingress by default

**Policy 2**: Allow ingress controller
- Ingress controller (ingress-nginx) can send traffic to cloud-api

**Policy 3**: Allow internal communication
- Cloud API can reach Edge API
- Ingress controller can reach Edge API (for health checks)

### 6. Security Features

- **Non-root containers**: UID 1000
- **Read-only filesystem**: Except `/tmp` and `/data` volumes
- **No privilege escalation**: `allowPrivilegeEscalation: false`
- **Dropped capabilities**: All Linux capabilities removed
- **Network policies**: Least privilege enforcement
- **Workload identity**: OIDC-based Azure authentication
- **Resource limits**: Prevent resource exhaustion

### 7. Observability

**Health Endpoints**:
- Liveness probe detects dead processes
- Readiness probe validates service is ready for traffic
- Kubernetes automatically restarts failed pods

**Prometheus Annotations**:
- All pods include Prometheus scrape annotations
- Metrics exposed on port 5000 at `/metrics`

## Deployment Workflow

### Local Validation (MVP)
```bash
# 1. Build and run locally
docker-compose up -d

# 2. Test health endpoints
curl http://localhost:5000/health/live
curl http://localhost:5001/health/ready

# 3. Test APIs (with Dev Container)
curl http://localhost:5000/api/v1/me
```

### AKS Deployment
```bash
# 1. Run the merged GitHub Actions workflow
# https://github.com/cdahal300/RetailPulse/actions/workflows/deploy-aks.yml

# Use:
# environment: dev
# deployInfrastructure: false
# runSmokeTests: true
# imageTag: empty, which defaults to the commit SHA

# 2. Verify cluster state locally if needed
az aks get-credentials \
  --resource-group rg-retailpulse-dev-centralus \
  --name retailpulse-dev-aks \
  --overwrite-existing

kubectl get deploy,pods,svc -n retailpulse
```

### Manual Image Build (Fallback)
```bash
./scripts/build-and-push-images.sh retailpulsedevzhztnpacr.azurecr.io <tag> --push
```

## Files Created/Modified

**New Files**:
- `src/Cloud/RetailPulse.Cloud/Dockerfile`
- `src/Edge/RetailPulse.Edge/Dockerfile`
- `docker-compose.yml` (root)
- `infra/kubernetes/README.md`
- `infra/kubernetes/base/namespace.yaml`
- `infra/kubernetes/base/cloud-api.yaml`
- `infra/kubernetes/base/edge-api.yaml`
- `infra/kubernetes/base/ingress.yaml`
- `infra/kubernetes/overlays/dev/kustomization.yaml`
- `infra/kubernetes/overlays/staging/kustomization.yaml`
- `infra/kubernetes/overlays/prod/kustomization.yaml`
- `scripts/validate-infrastructure.sh`
- `scripts/build-and-push-images.sh`

**Modified Files**:
- `src/Cloud/RetailPulse.Cloud/Program.cs` - Added health endpoints
- `src/Edge/RetailPulse.Edge/Program.cs` - Added health endpoints with persistence check

## Building and Testing

### Build
```bash
dotnet build RetailPulse.sln
```

### Test Health Endpoints (in Dev Container)
```bash
# Cloud API (port 5000)
curl http://localhost:5000/health/live
curl http://localhost:5000/health/ready

# Edge API (port 5001 when using docker-compose)
curl http://localhost:5001/health/live
curl http://localhost:5001/health/ready
```

## Remaining Work (For Production AKS Deployment)

### Required (Before Production)
1. **Monitoring**: Complete FEAT-008 OpenTelemetry, dashboards, and alerts before production traffic.
2. **Secrets Management**: Move runtime secret source of truth to Azure Key Vault or External Secrets Operator.
3. **Staging/production GitHub environments**: Configure protected environments, approvals, OIDC identities, and secrets.
4. **Load Testing**: Validate autoscaling and performance.
5. **Disaster Recovery**: Test backup, restore, and failover procedures.

### Optional (Post-MVP)
1. **Service Mesh**: Implement Istio for advanced traffic management
2. **GitOps**: Use Flux or ArgoCD for declarative deployments
3. **Policy Enforcement**: Azure Policy for regulatory compliance
4. **Cost Optimization**: Reserved instances, spot instances
5. **Multi-region**: Setup cross-region replication and failover

## References

- [AKS Documentation](https://docs.microsoft.com/azure/aks/)
- [Kubernetes Best Practices](https://kubernetes.io/docs/)
- [Bicep Documentation](https://docs.microsoft.com/azure/azure-resource-manager/bicep/)
- [Workload Identity](https://azure.github.io/azure-workload-identity/)
- [Network Policies](https://kubernetes.io/docs/concepts/services-networking/network-policies/)

## Related Features

- **FEAT-007**: CI/CD and Release Automation (builds on this)
- **FEAT-008**: OpenTelemetry Observability (metrics/tracing)
- **FEAT-005**: Identity and Authorization (uses workload identity)

## Notes

- Current dev container cannot run full Docker Compose because Docker bridge networking requires host capabilities that are not available in the container.
- Azure dev deployment uses `centralus` because PostgreSQL Flexible Server was available there for this subscription; `eastus` did not expose supported PostgreSQL versions.
- All manifests follow Kubernetes best practices and security guidelines
- Health checks are critical for Kubernetes reliability
- Network policies enforce defense-in-depth security
