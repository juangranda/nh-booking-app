# Booking System

A booking system where employees can reserve desks, office spaces, or parking spots from a single application.

## Technology Stack

| Layer        | Technology                      |
| ------------ | ------------------------------- |
| **Backend**  | .NET 8 (API, EF Core, Identity) |
| **Frontend** | Angular 19                      |
| **Database** | SQLite                          |

## Data Structure

```mermaid
erDiagram
  Users ||--o{ UserRoles : has
  Roles ||--o{ UserRoles : grants
  Users ||--o{ UserClaims : has
  Roles ||--o{ RoleClaims : has
  Users ||--o{ UserLogins : has
  Users ||--o{ UserTokens : has
  Users |o--o| Employees : "linked to"
  Office ||--o{ Service : offers
  Service ||--o{ Service_Details : contains
  Service_Details ||--o{ Booking : "booked as"
  Employees ||--o{ Booking : makes

  Users {
    int Id PK
    text UserName
    text Email "indexed"
    int EmailConfirmed
    text PasswordHash
    text PhoneNumber
    int TwoFactorEnabled
    text CreatedAt
  }
  Roles {
    text Id PK
    text Name
  }
  UserRoles {
    int UserId PK, FK
    text RoleId PK, FK
  }
  UserClaims {
    int Id PK
    int UserId FK
    text ClaimType
    text ClaimValue
  }
  RoleClaims {
    int Id PK
    text RoleId FK
    text ClaimType
    text ClaimValue
  }
  UserLogins {
    text LoginProvider PK
    text ProviderKey PK
    text ProviderDisplayName
    int UserId FK
  }
  UserTokens {
    int UserId PK, FK
    text LoginProvider PK
    text Name PK
    text Value
  }
  Employees {
    int ID PK
    text first_name
    text last_name
    text email
    text phone
    text created_at
    int user_id FK, UK
  }
  Office {
    int ID PK
    text name
    text image
  }
  Service {
    int ID PK
    int office_id FK
    text name
    text description
    int quantity
    int max_bookings_week
    int booking_per_time
    text min_max
    text image
    text created_at
  }
  Service_Details {
    int ID PK
    int service_id FK
    text name
    text description
    int capacity
    text icon
    text created_at
  }
  Booking {
    int ID PK
    int service_detail_id FK
    int employee_id FK
    text start_datetime
    text end_datetime
    text created_at
  }
```

## Project Structure

```
booking-repo/
├── backend/    # .NET 8 API (see backend/README.md)
├── frontend/   # Angular app (see frontend/README.md)
└── booking-app-seed-data.json
```

Refer to each project's README for setup instructions, prerequisites, and available commands:

- [Backend README](backend/README.md)
- [Frontend README](frontend/README.md)
