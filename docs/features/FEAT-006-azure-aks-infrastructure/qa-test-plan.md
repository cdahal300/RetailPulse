# FEAT-006: QA Test Plan

## Test coverage

- Unit tests: IaC module defaults, naming/tagging, policy rules, network rules, identity bindings, and alert thresholds.
- Integration tests: deploy disposable environments, private endpoint connectivity, Key Vault access, managed identity, Service Bus, PostgreSQL, Blob, and App Configuration.
- Contract tests: health/readiness probes, ingress/WAF behavior, and infrastructure outputs consumed by deployment pipelines.
- End-to-end tests: provision, deploy a minimal workload, exercise health checks, rotate a secret, back up, restore, and tear down.
- PWA or device tests: none for infrastructure itself; verify supported client traffic through ingress in staging.
- Performance and resilience tests: node/pod disruption, zone failure simulation, autoscaling, throttling, backup restore time, ingress load, and cost ceilings.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path environment | Versioned IaC provisions tagged, private, healthy resources | IaC/Integration |
| Offline or dependency unavailable | Edge remains independent; cloud maintenance does not corrupt local state | E2E/Resilience |
| Timeout or retry | Apply/retry is safe and does not duplicate resources | IaC/Integration |
| Duplicate request or event | Repeated apply/change ID is idempotent | IaC/Integration |
| Unauthorized access | RBAC, network, and identity deny out-of-scope access | Security/Integration |
| Invalid input or conflict | Policy/plan fails before apply with clear remediation | IaC |
| Node/zone/data failure | Recovery meets documented RTO/RPO and preserves data | Resilience |

## Release evidence

- Test command: infrastructure formatter/validate/plan, policy scanner, image scanner, and deployment pipeline validation commands defined by FEAT-007.
- Required environment: isolated Azure subscription/resource group or approved ephemeral environment with cost limits.
- Evidence artifact: plan, policy report, access matrix, connectivity report, restore timings, and Azure Activity Log sample.
- Known gaps: regional disaster recovery and provider quota limits require scheduled production-like rehearsal.
