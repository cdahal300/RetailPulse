# FEAT-005: QA Test Plan

## Test coverage

- Unit tests: policy decisions, tenant/store scope, role matrix, token claim validation, cache expiry, and audit generation.
- Integration tests: Entra/JWKS validation fixture, API middleware, device registration/revocation, event propagation, and database isolation.
- Contract tests: auth error model, role/device events, and protected command contracts.
- End-to-end tests: sign-in, role-aware routes, privileged command, revocation, cross-store denial, and offline expiry.
- PWA or device tests: supported desktop/tablet/Android/iOS authentication, secure session behavior, device registration, and offline messaging.
- Performance and resilience tests: token/JWKS cache behavior, authorization latency, identity-provider outage, revocation propagation, and login throttling.

## Scenario matrix

| Scenario | Expected result | Test location |
|---|---|---|
| Happy path role access | Allowed operation succeeds within store/tenant scope | Unit/Integration/E2E |
| Offline or dependency unavailable | Supported cached session works; privileged cloud action fails closed | Integration/PWA/E2E |
| Timeout or retry | Auth retry does not duplicate command or weaken policy | Integration |
| Duplicate request or event | One registration/action and consistent authorization state | Contract/Integration |
| Unauthorized access | Expired token, wrong role, store, or tenant is denied | Unit/Integration/PWA |
| Invalid input or conflict | Malformed claims/device conflict are rejected and audited | Unit/Integration |
| Revocation | Device/user loses access within the documented window | E2E/Resilience |

## Release evidence

- Test command: focused `dotnet test` identity/authorization unit, integration, and contract filters plus PWA auth Playwright tests.
- Required environment: Entra test tenant or signed-token fixture, isolated stores/tenants, and registered test devices.
- Evidence artifact: role matrix, denial report, revocation timing, audit sample, and device/browser results.
- Known gaps: production conditional-access policies and tenant-specific identity configuration require staging sign-off.
