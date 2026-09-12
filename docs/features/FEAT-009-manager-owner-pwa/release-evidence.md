# FEAT-009 Release Evidence

Complete this record for each staging or production promotion. Do not record real customer data, tokens, subscription endpoints, or private keys.

## Environment

| Field | Value |
| --- | --- |
| Release commit | |
| Environment | |
| Test tenant/store | |
| Test identity roles | Manager / Owner |
| API health checks | Pass / Fail |
| VAPID delivery event ID | |
| Rollback version tested | |

## Browser and Device Matrix

| Device | OS | Browser | Install | Offline cached read | Permission flow | Push delivery/deep link | Result | Evidence link |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Desktop | | Chrome | | | | | | |
| iPhone | iOS | Safari | | | | | | |
| Android phone | Android | Chrome | Pass | Pass | Pending | Pending | Partial | Screenshot captured locally; add evidence link |

## Required Checks

- [ ] Sign in as manager and confirm only the assigned store is visible.
- [x] Install the PWA and reopen it after the network is disabled on Android Chrome.
- [x] Confirm cached reads are labeled stale and no new values are invented offline on Android Chrome.
- [ ] Grant notification permission, register the subscription, and trigger a low-stock event.
- [ ] Select the notification and confirm it deep-links to `/#alerts`.
- [ ] Deny notification permission and confirm the unavailable state is actionable.
- [ ] Sign out and confirm cached data and notification state are cleared for the signed-out user.
- [ ] Restore the previous deployment and confirm health, authentication, cached reads, and notification registration recover.

## Automated Baseline

Run from `src/Web/RetailPulse.Portal` before the device checks:

```bash
npm run lint
npm run build
npm run test:e2e -- --project=desktop
npm run test:e2e -- --project=phone
```

Attach screenshots or screen recordings for the iPhone and Android rows, plus the deployment and rollback run links.