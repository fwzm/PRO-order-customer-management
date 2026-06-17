# Security Policy

## Reporting a Vulnerability

Please report security issues privately through GitHub Security Advisories when available, or contact the repository owner directly.

Do not open a public issue that contains:

- API keys, JWT secrets, WeChat secrets, database passwords, or connection strings.
- Real customer information, phone numbers, addresses, orders, receivables, or payment records.
- Database dumps, logs, backups, exported spreadsheets, or production configuration files.

## Secret Handling

This repository is intended to contain example configuration only. Production deployments must provide secrets through environment variables or local ignored configuration files.

Required production values include:

- `PRO_ConnectionStrings__DefaultConnection`
- `PRO_ConnectionStrings__PostgreSQL`
- `PRO_Jwt__Key`
- `PRO_WeChat__CorpSecret`
- `PRO_TencentMap__ApiKey`

## Public Repository Checklist

Before publishing changes, run a secret scan and verify that no real customer data, database files, logs, exports, backups, or production settings are included.
