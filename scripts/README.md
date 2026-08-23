# Developer scripts

Place repeatable local commands here for setup, database migrations, test data, Mermaid validation, and CI parity. Scripts should be safe to rerun and should fail with actionable messages.

## Analytics traffic

Use `generate-analytics-traffic.sh` to exercise the simulated FEAT-010 sales report endpoint against a local or deployed Cloud API base URL.

```bash
./scripts/generate-analytics-traffic.sh http://localhost:5000 20
```

The script sends manager-authorized report requests for `tenant-1` across `store-1` and `store-2`. Override `RETAILPULSE_TENANT_ID`, `RETAILPULSE_STORE_IDS`, or `RETAILPULSE_SUBJECT_ID` to test other scopes.
