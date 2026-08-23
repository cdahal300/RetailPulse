# Development and QA Workflow

## Vertical slice order

1. Update local `main` and create a fresh `feat/<feature-id>-<short-name>` branch.
2. Write the feature specification under `docs/features/<feature-id>-<short-name>/`.
3. Review scope, non-goals, architecture, security, data, offline, payment, mobile, and rollout impact.
4. Define the domain rule and acceptance criteria.
5. Implement local edge persistence and the outbox transaction where applicable.
6. Define the cloud command and event contract.
7. Implement sync retry and idempotency behavior.
8. Implement UI or device integration.
9. Add unit, integration, contract, and end-to-end tests.
10. Add telemetry, dashboard, and runbook updates.

## Prompt usage

- Use `Implement RetailPulse feature` for a complete feature slice.
- Use `RetailPulse QA review` before merging or after a risky integration change.
- Keep prompts and agents in `.github/` so they are versioned with the GitHub repository.

## MCP usage

MCP configuration belongs in `.vscode/mcp.json` for local developer tooling. Keep secrets out of the repository. Use MCP servers for repository context, Azure inspection, test execution, and observability queries; keep business decisions and credentials in reviewed code and environment configuration.

The checked-in `.vscode/mcp.json.example` is a template only. Add real server entries locally or through a secure team setup once the tool choices are finalized.

For AKS delivery, the CI/CD workflow should build immutable images, scan them, push them to Azure Container Registry, deploy with the selected infrastructure-as-code and Kubernetes packaging approach, and verify health before promoting a release. Cluster credentials and kubeconfig files must never be committed.

## QA gates

- Pull request: format, build, unit tests, static analysis, secret scan
- Integration stage: PostgreSQL, SQLite, messaging, and provider-adapter tests
- Pilot stage: offline checkout, sync recovery, duplicate delivery, device failure, refund, and authorization scenarios
- Release: migration review, rollback plan, alert verification, and payment-provider certification status

## Mobile release boundary

Treat the manager and owner PWA in `src/Web/RetailPulse.Portal/` as a cloud client, not as a second transaction engine. Test responsive layouts, authentication, push notifications, cached reads, and authorization on supported desktop, tablet, Android, and iOS browsers. Keep checkout and hardware workflows in the local POS application.

### PWA acceptance coverage

The PWA is ready for pilot only when these scenarios are covered by automated tests where practical and a device/browser smoke matrix:

| Area | Required coverage |
|---|---|
| Responsive UI | Phone portrait, phone landscape, tablet, and desktop; no clipped controls or horizontal scrolling |
| Authentication | Sign-in, token expiry, sign-out, unauthorized route, and manager versus owner permissions |
| Data behavior | Loading, empty, stale, error, retry, and read-only offline cache states |
| Commands | Authorized manager change, duplicate submission, timeout, pending state, and confirmed result |
| Notifications | Permission denied, permission granted, duplicate notification, deep link, and disabled notifications |
| PWA lifecycle | Install prompt, reload, service-worker update, offline launch, and recovery after reconnect |
| Security | No sensitive data in browser storage, secure headers, tenant/store isolation, and safe logout |
| Accessibility | Keyboard navigation, focus order, labels, contrast, touch target size, and screen-reader basics |

Minimum smoke targets are the current Chrome and Edge desktop browsers plus Safari iOS and Chrome Android on supported pilot devices. Use Playwright for repeatable browser workflows and keep at least one real iOS and Android device check because browser installation and push behavior differ by platform.

## Feature flag release gate

- New functionality must be disabled by default until its automated tests pass.
- Pull requests must document each new flag, owner, default, targeting dimensions, and expiry date.
- Production deployment and feature activation are separate approvals.
- Enable changes gradually by environment, store, terminal, role, or percentage.
- Monitor technical and business signals before expanding a rollout.
- Disable the flag first for rollback; revert the deployment only when the code version itself is unsafe.
- Remove temporary flags and their dead branches after the rollout is complete.
