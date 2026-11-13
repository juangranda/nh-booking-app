# booking-app — Backend (.NET 8, SQLite, JWT)

Backend for booking-app implemented with .NET 8. Project is split into three projects/solutions:

- API: HTTP endpoints and startup (src/booking.api)
- Core: domain and application logic (src/booking.core)
- Infrastructure: persistence, EF Core, Identity (src/booking.infrastructure)

This README explains prerequisites, setup, environment variables and common commands to build, run and publish the backend.

## Prerequisites

- .NET 8 SDK (download from https://dotnet.microsoft.com)
- dotnet-ef tool (for migrations): dotnet tool install --global dotnet-ef
- SQLite (optional: for inspecting DB files). On mac: brew install sqlite
- Git

Confirm:

```bash
dotnet --version   # should be 8.x
dotnet ef --version
sqlite3 --version  # optional
```

## JWT signing key

The JWT signing key is not stored in `appsettings.json`. The API refuses to start without it, and it must be at least 32 bytes.

Development (one-time setup per machine, stored outside the repo via .NET user-secrets):

```bash
cd src/booking.api
dotnet user-secrets set "Jwt:Key" "$(openssl rand -base64 48)"
```

Production / other environments: set the `Jwt__Key` environment variable.

Changing the key invalidates all previously issued tokens, so users must log in again.

## Seed data and the first admin

On startup the API creates the schema (if missing) and the `Admin` and `User` roles with their permissions, in every environment.

Demo data is only created in **Development**: the accounts `admin@demo.com`, `sarah.johnson@company.com`, `michael.chen@company.com` and `emma.rodriguez@company.com` (password `P@ssw0rd!`), two offices, two services and a sample booking. Set `Seed__DemoData=true` to create it in another environment (for example a shared demo server), or `false` to skip it locally.

Outside Development, create the first admin with environment variables:

```bash
export Seed__AdminEmail=ops@company.com
export Seed__AdminPassword='<at least 12 characters>'
```

The account (with the `Admin` role and an employee record) is only created when that email doesn't exist yet, so the variables can be removed after the first start. If a non-Development database still has demo accounts with the demo password, the API logs a warning at startup.

## Run (development)

Run the API from its project folder, so `app.db` is created there:

```bash
cd src/booking.api
dotnet run --launch-profile http
```

The server listens on http://localhost:5170, and Swagger UI is at http://localhost:5170/swagger (Development only).

## Run tests

If there are test projects:

```bash
dotnet test
```
