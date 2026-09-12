# PWA Tests

Browser and device-oriented tests for `src/Web/RetailPulse.Portal/`.

- `responsive/`: phone, tablet, and desktop layouts
- `auth/`: manager and owner roles, expiry, logout, and protected routes
- `offline/`: cached reads, reconnect, stale data, and service-worker lifecycle
- `notifications/`: permissions, deep links, duplicates, and disabled notifications
- `accessibility/`: keyboard, focus, labels, contrast, and touch targets
- `fixtures/`: authenticated sessions, stores, flags, and device profiles

The modern view coverage should verify the priority-first overview, manager/owner navigation, explicit freshness states, advisory insight metadata, phone bottom navigation, stable touch targets, and no horizontal overflow. Keep these tests at the browser boundary; API authorization and command idempotency remain covered by Cloud tests.

Use Playwright for repeatable browser workflows and a small real-device smoke matrix for Safari iOS and Chrome Android.

The automated demo-mode acceptance suite runs from the portal package:

```bash
cd src/Web/RetailPulse.Portal
npx playwright install chromium
npm run test:e2e -- --project=desktop
npm run test:e2e -- --project=phone
```

The desktop project runs in CI. The phone project uses Playwright WebKit locally and should be complemented by a real iOS Safari check for release evidence.