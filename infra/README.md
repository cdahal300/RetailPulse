# Infrastructure layout

- `modules/`: reusable infrastructure modules
- `environments/`: development, test, staging, and production composition
- `policies/`: Azure policy and security configuration
- `README.md`: provisioning prerequisites and deployment sequence

Use infrastructure as code for Azure resources. Never commit credentials, connection strings, or provider secrets.
