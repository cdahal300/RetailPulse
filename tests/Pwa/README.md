# PWA Tests

Browser and device-oriented tests for `src/Web/RetailPulse.Portal/`.

- `responsive/`: phone, tablet, and desktop layouts
- `auth/`: manager and owner roles, expiry, logout, and protected routes
- `offline/`: cached reads, reconnect, stale data, and service-worker lifecycle
- `notifications/`: permissions, deep links, duplicates, and disabled notifications
- `accessibility/`: keyboard, focus, labels, contrast, and touch targets
- `fixtures/`: authenticated sessions, stores, flags, and device profiles

Use Playwright for repeatable browser workflows and a small real-device smoke matrix for Safari iOS and Chrome Android.