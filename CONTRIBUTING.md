# Contributing

## Branches

- Use short, descriptive branches such as `feature/order-template` or `fix/inventory-log`.
- Keep public-release and security-cleanup changes isolated from feature work.

## Before Submitting

Run these commands from the repository root:

```powershell
dotnet restore PRO.sln
dotnet build PRO.sln -c Release
dotnet test PRO.sln -c Release
```

## Security Rules

- Do not commit real customer data, order data, receivables, addresses, phone numbers, database files, logs, backups, exports, or screenshots containing private data.
- Do not commit real API keys, JWT secrets, WeChat secrets, database connection strings, or production appsettings files.
- Use environment variables or local ignored configuration files for secrets.

## Pull Requests

Please include:

- What changed and why.
- Build and test result.
- Any migration or configuration change.
- Any security-relevant impact.
