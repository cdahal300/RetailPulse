# Security Policy

## Scope

RetailPulse is a multi-tenant retail SaaS platform. Tenant/store isolation, payment-data exclusion, identity, secrets, and operational access are security boundaries.

## Reporting

Do not report vulnerabilities in public issues. Use the repository's private security advisory process or contact the project security owner. Do not include payment card data or production customer data in a report.

## Development rules

- Never store PAN, CVV, PIN, magnetic-stripe data, or raw card data.
- Keep payment processing and acquiring external to RetailPulse.
- Never commit secrets, provider credentials, Azure tokens, or kubeconfig files.
- Validate tenant and store scope server-side on every request, record, event, cache key, analytics query, export, flag, and AI job.
- Run dependency and secret scanning in CI.
- Treat cross-tenant data access as a release-blocking defect.
