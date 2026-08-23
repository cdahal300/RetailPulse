# Source layout

Planned deployable hosts and modules:

- `Edge/`: local store runtime, SQLite, device gateway, outbox, and sync agent
- `Cloud/`: ASP.NET Core API and background workers
- `Web/`: manager and owner dashboard PWA
- `Web/RetailPulse.Portal/`: installable React and TypeScript PWA for manager and owner workflows
- `Web/RetailPulse.Portal/src/features/`: feature areas such as sales overview, inventory, alerts, and AI insights
- `Web/RetailPulse.Portal/src/platform/`: authentication, API client, feature flags, service worker, notifications, and offline read cache
- `Web/RetailPulse.Portal/src/components/`: shared accessible UI components
- `Web/RetailPulse.Portal/public/`: PWA manifest and static assets
- `BuildingBlocks/`: shared primitives only, not a dumping ground for business logic
- `Tools/`: local development and MCP utilities
