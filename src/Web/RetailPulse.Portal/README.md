# RetailPulse Portal PWA

Installable React and TypeScript Progressive Web App for store managers and owners.

## Responsibilities

- Read sales, inventory, sync health, and AI insight read models from the cloud API.
- Submit explicitly authorized manager commands and show pending or confirmed status.
- Support responsive desktop, tablet, Android, and iOS browser experiences.
- Cache small, non-sensitive read models for limited offline viewing.
- Receive operational notifications and deep-link users to the relevant screen.
- Evaluate UI feature flags with safe defaults.

## FEAT-009 MVP Slice

The current manager dashboard reads FEAT-010 sales report contracts when both `VITE_API_BASE_URL` and the explicit local-only `VITE_DEMO_MODE=true` setting are configured. If the API is unavailable, demo mode is disabled, or no API base URL is configured, it falls back to deterministic simulated, non-sensitive report data and labels the state clearly. Production authentication is not implemented yet, so the PWA will not send synthetic identity headers unless demo mode is explicitly enabled.

```bash
npm run dev -- --host 0.0.0.0
VITE_API_BASE_URL=http://localhost:5011 npm run dev -- --host 0.0.0.0
VITE_API_BASE_URL=http://localhost:5011 VITE_DEMO_MODE=true npm run dev -- --host 0.0.0.0
```

The fallback path is for internal development only. Server-side authorization remains authoritative for live API calls.

## Authentication Configuration

For a real Entra ID session, configure these Vite variables at build time:

```bash
VITE_ENTRA_CLIENT_ID=<application-client-id>
VITE_ENTRA_TENANT_ID=<tenant-id>
VITE_ENTRA_API_SCOPE=api://<application-client-id>/access_as_user
VITE_ENTRA_REDIRECT_URI=https://<portal-host>/
```

The browser shows a sign-in gate when Entra configuration is present but no session exists. `VITE_DEMO_MODE=true` is intended only for local synthetic data testing and must not be used for a production build.

The Cloud API must also be configured with matching server-side settings:

```bash
Entra__TenantId=<tenant-id>
Entra__Audience=api://<application-client-id>
```

Provider-backed push registration additionally requires the cloud-only `Push__VapidPublicKey` setting. Keep the corresponding private key in Key Vault or an environment secret; it must never be sent to the browser.

When those settings are present, the API validates issuer, audience, signature, expiry, tenant, store, and role claims before applying its existing authorization policy. Without them, the local header-based test harness remains available but should not be used as a production authentication path.

## Not responsible for

- Checkout or authoritative sale state
- Payment terminal control or card data
- Local POS hardware access
- Unrestricted offline inventory or configuration mutations

## Planned structure

```text
RetailPulse.Portal/
├── public/
│   ├── manifest.webmanifest
│   ├── icons/
│   └── offline.html
├── src/
│   ├── app/              # routes, providers, startup
│   ├── components/       # accessible shared UI
│   ├── features/         # sales, inventory, alerts, insights, settings
│   ├── platform/         # API, auth, flags, cache, notifications, service worker
│   └── styles/           # design tokens and global styles
├── playwright/
└── package.json
```