# FEAT-009: Modern PWA View Plan

## Product direction

RetailPulse should feel like a calm operations cockpit for a busy store manager, not a generic analytics dashboard. The first viewport answers three questions quickly:

1. Is the data current and trustworthy?
2. What needs attention now?
3. What action is safe and available to me?

The UI remains advisory and operational. It never becomes a checkout surface, payment-terminal controller, or authorization boundary.

## Information architecture

### Manager workspace

- **Overview:** store context, freshness state, sales pulse, priority alerts, sync readiness, and the next safe action.
- **Inventory:** top movers, low-stock worklist, product detail, and authorized inventory adjustments.
- **Alerts:** low-stock and sync-failure alerts, notification preferences, and deep links.
- **Sync:** pending, retry, conflict, dead-letter, and last-success details with recovery guidance.

### Owner workspace

- **Overview:** cross-store performance and exceptions.
- **Reports:** sales periods, data quality, and source freshness.
- **Insights:** advisory summaries with supporting facts, source references, validation state, prompt version, and model deployment.
- **Store settings:** owner-only configuration with optimistic version handling.

The first implementation may keep these as view states within the existing React app. The URL/hash and component boundaries should be designed so they can become route-level views without changing API ownership.

## Visual direction

- Use a warm off-white workspace, deep green navigation, and a restrained amber accent for attention states.
- Use expressive display typography for page titles and compact readable text for operational data.
- Prefer full-width workspace bands and unframed sections; reserve bordered panels for repeated data groups, dialogs, and tools.
- Make freshness, cached, offline, partial, reviewable, and unavailable states visually distinct and textually explicit.
- Use icon buttons only for familiar actions such as refresh, export, and navigation; provide accessible labels and tooltips.
- Use short page-load and staggered list reveals only where they help scanning. Avoid decorative motion, gradients used as content, and dashboard ornament.

## Responsive behavior

- Desktop: persistent rail, two-column overview, priority worklist beside the sales pulse.
- Tablet: compact rail or top navigation, one primary content column with a persistent status header.
- Phone: bottom navigation for Overview, Inventory, Alerts, and More; cards become full-width list sections; commands use a focused sheet or dialog.
- Maintain 44px minimum touch targets, no horizontal scrolling, stable chart dimensions, and readable text at every supported viewport.

## Implementation slices

1. Extract `App.tsx` into shell, navigation, status, overview, inventory, alerts, sync, insights, and settings components without changing API calls.
2. Add an explicit view state and role-aware navigation model. Server authorization remains authoritative.
3. Replace the current dense dashboard ordering with a priority-first overview and reusable status components.
4. Add focused inventory and alert worklists while preserving offline command queue semantics.
5. Add owner report/insight views and retain advisory metadata in the visible detail surface.
6. Add responsive, accessibility, offline, and role-navigation Playwright coverage before rollout.

## Architecture constraints

- Keep API clients and cache ownership in the PWA layer; cloud services own authorization, commands, events, and read-model semantics.
- Cache only scoped, non-sensitive read data. Never persist tokens, payment data, model credentials, or raw customer data.
- Keep manager commands idempotent and visibly pending until the server confirms them.
- Keep AI insights advisory. No insight action may mutate inventory, pricing, payment, or checkout automatically.
- Treat stale, partial, reviewable, and unavailable data as first-class states rather than hiding them behind loading or empty screens.

## Success measures

- A manager can identify the store state and highest-priority issue from the first viewport.
- A manager can reach inventory, alerts, and sync details in one interaction from the overview.
- An owner can distinguish an advisory insight from verified operational facts.
- Offline and cached views remain useful without implying freshness.
- Desktop, tablet, and phone flows pass accessibility, overflow, and touch-target checks.
