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

The current manager dashboard reads FEAT-010 sales report contracts when `VITE_API_BASE_URL` is configured. If the API is unavailable or no API base URL is configured, it falls back to deterministic simulated, non-sensitive report data and labels the state clearly.

```bash
npm run dev -- --host 0.0.0.0
VITE_API_BASE_URL=http://localhost:5011 npm run dev -- --host 0.0.0.0
```

The fallback path is for internal development only. Server-side authorization remains authoritative for live API calls.

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