# FEAT-009: Manager and Owner PWA

## Status

Implementation complete; staging release evidence remains pending. The MVP slices include an Entra-authenticated manager dashboard with live sales, tenant/store-scoped sync-health data, authorized inventory commands that can queue safely while offline, browser notification lifecycle handling, and scoped cache cleanup.

## Verified Dev Evidence

- Android Chrome installation and offline cached-dashboard launch passed.
- Dev API report reads passed through the PWA with live API and event-backed freshness metadata.
- Service-worker app-shell caching and online recovery were verified.
- Pending: real Entra sign-in, iPhone Safari, VAPID push delivery/deep-link, and PostgreSQL integration evidence.

## Outcome

As a manager or owner, I want secure mobile access to store performance and operational actions without installing a native app.

## Scope

- Responsive React/TypeScript PWA for sales, inventory, sync health, alerts, settings, and AI insights.
- Authentication, role-aware routes, push notifications, install manifest, and service worker.
- Small non-sensitive read-only offline cache.
- Authorized manager commands with pending and confirmed status.

## MVP Dashboard Slice

- Store selector for the current tenant pilot stores.
- Sales KPI cards, hourly sales, top products, and operational readiness notes.
- Data freshness/source/status indicators so simulated or cached data is never presented as fresh production data.
- Basic install metadata and offline fallback page.
- Production-only service worker registration to avoid stale assets during local Vite development.
- Configurable Entra/MSAL browser session with a sign-in gate; synthetic manager identity is limited to explicit local demo mode.

## Acceptance criteria

- Works on supported desktop, tablet, Android, and iOS browsers.
- No horizontal scrolling or clipped controls at supported viewport sizes.
- Offline launch shows clearly labeled stale read data and does not invent fresh values.
- PWA cannot perform checkout or control payment terminals.
- Browser storage contains no sensitive payment data.

## Dependencies and QA

Depends on FEAT-005, FEAT-008, and read models from FEAT-010. Use Playwright plus real iOS and Android smoke checks for authentication, responsive UI, offline cache, notifications, accessibility, and service-worker updates.

## Definition of Ready

- [Acceptance criteria](acceptance-criteria.md)
- [API and event contracts](api-and-events.md)
- [QA test plan](qa-test-plan.md)
- [Rollout and operations](rollout.md)
