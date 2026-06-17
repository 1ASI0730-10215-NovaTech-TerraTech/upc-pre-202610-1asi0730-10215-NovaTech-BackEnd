# TerraTech - Smart Crop Monitoring

**TerraTech** is an intelligent monitoring platform designed for farmers, allowing them to supervise crop lots, monitor humidity levels, and receive recommendations based on real-time field conditions. This repository contains the **backend solution**, built with a Domain-Driven Design (DDD) architecture and following clean-code practices to ensure scalability, maintainability, and business alignment.

---

## 🚀 Technologies Used

| Technology                 | Description |
|----------------------------|-------------|
| **.NET 10**                | Cross-platform runtime and framework |
| **C#**                     | Primary development language |
| **ASP.NET Core Web API**   | RESTful API endpoints |
| **Entity Framework Core**  | ORM for data access |
| **MySQL**                  | Relational database engine |
| **Cortex Mediator**        | CQRS and event bus implementation (mediator pattern) |
| **Swagger / OpenAPI**      | API documentation and testing |
| **BCrypt**                 | Password hashing |
| **JWT**                    | Authentication and authorization |
| **Localization (I18N)**    | Multi-language support (EN/ES) |
| **Global Exception Handling** | Middleware with Problem Details responses |

---

## 🧱 Architecture Overview

The solution is based on **Domain-Driven Design (DDD)** and follows a modular **Bounded Context** approach. Each bounded context is independent and can evolve separately, while sharing a common **Shared Kernel** for base infrastructure and cross-cutting concerns.

The high-level layers per bounded context:

```
BoundedContext/
├── Domain/ # Core business logic (Aggregates, Value Objects, Commands, Queries, Repositories)
├── Application/ # Use cases (Command/Query Handlers, Services, DTOs, Errors)
├── Infrastructure/ # External concerns (EF Core, Persistence, Repositories implementation)
└── Interfaces/ # REST API (Controllers, Resources, Transformers)
```

The **Shared Kernel** provides:

- `IAuditableEntity` – audit timestamps via interceptor.
- `IBaseRepository<T>` – generic CRUD operations.
- `IUnitOfWork` – transactional integrity.
- Common localization resources (`ErrorMessages`, `CommonMessages`).

---

## 📦 Bounded Contexts

| Context | Responsibility | Key Aggregates |
|---------|----------------|----------------|
| **Iam** | Identity and Access Management | `User` |
| **ProfileManagement** | Farmer and cooperative profiles | `Profile` |
| **Monitoring** | Crop fields and IoT devices | `Field`, `Device` |
| **AnalyticsManagement** | Statistical reports from sensors | `Report` |
| **CommercialManagement** | Product catalog and orders | `Product`, `Order` |
| **StockManagement** | Inventory of agricultural inputs | `Inventory` |
| **NotificationManagement** | Alerts and system messages | `Notification` |
| **CommunityManagement** | Social interaction among farmers | `CommunityProfile`, `Comment` |

---

## 🛠️ How to Run the Project

### Prerequisites
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [MySQL Server](https://dev.mysql.com/downloads/mysql/) or a compatible instance
- Optional: [Docker](https://www.docker.com/) for containerized database

### 1. Clone the repository
```bash
git clone https://github.com/1ASI0730-10215-NovaTech-TerraTech/upc-pre-202610-1asi0730-10215-NovaTech-BackEnd.git
```

### 2. Configure the connection string

Edit appsettings.Development.json (or appsettings.json) with your MySQL password:

```bash
{
  "TokenSettings": {
    "Secret": "Place here your secret for token generation"
  },
  "ConnectionStrings": {
    "DefaultConnection": "server=localhost;user=root;password=password;database=TerraTech"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

### 3. Apply database migrations

### 4. Run the API

### 5. Explore the API documentation

Open your browser and navigate to:

```bash
https://localhost:5022/swagger/index.html
```

## Project Structure

```
src/
├── Shared/                  # Shared kernel (base abstractions, common infrastructure)
│   ├── Domain/              # IAuditableEntity, IEvent, Error, etc.
│   ├── Application/         # Result class, Event handlers
│   ├── Infrastructure/      # AppDbContext, interceptors, base repositories, middleware
│   └── Interfaces/          # ProblemDetails factory, localization resources
├── Iam/                     # Identity and Access Management
├── ProfileManagement/       # User profiles
├── Monitoring/              # Fields and IoT devices
├── AnalyticsManagement/     # Statistical reports
├── CommercialManagement/    # Products and orders
├── StockManagement/         # Inventory
├── NotificationManagement/  # Notifications and alerts
├── CommunityManagement/     # Social features (community profiles, comments)
└── TerraTech.API/           # Entry point (Program.cs, appsettings)
```

## Contributors

| Apellidos y Nombres | Código |
|--------------------|---------|
| Aguilar Untiveros, Rodrigo Fabrizio | U202318309 |
| Howard Robles, Guillermo Arturo | U202222275 |
| Perez Encarnación, Breithner Rodolfo | U202418577 |
| Retuerto Rodríguez, Jorge Manuel | U202318612 |