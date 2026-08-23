# RetailPulse Portal PWA

Installable React and TypeScript Progressive Web App for store managers and owners.

## Responsibilities

- Read sales, inventory, sync health, and AI insight read models from the cloud API.
- Submit explicitly authorized manager commands and show pending or confirmed status.
- Support responsive desktop, tablet, Android, and iOS browser experiences.
- Cache small, non-sensitive read models for limited offline viewing.
- Receive operational notifications and deep-link users to the relevant screen.
- Evaluate UI feature flags with safe defaults.

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