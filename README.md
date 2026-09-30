# Web Based CRM for Small Businesses

ASP.NET Core 8 CRM mini-project for BCA Semester V, Parul University.

## Quick start

1. Install .NET 8 SDK and SQL Server LocalDB/SQL Server.
2. Set `ConnectionStrings__CrmDatabase` or edit `appsettings.json`.
3. From `src/SmallBusinessCRM.Api`, run `dotnet restore`, `dotnet ef migrations add InitialIdentityRbac`, `dotnet ef database update`, then `dotnet run`.
4. Browse to the URL shown by ASP.NET Core. Seeded users are Admin, Manager, and Representative accounts.

The SPA-style Bootstrap dashboard is served from `wwwroot`. API documentation is available at `/swagger` in Development.

## Project contents

- `src/SmallBusinessCRM.Api`: Web API, EF Core model, services, background reminder worker, dashboard.
- `database/initial-schema.sql`: standalone SQL Server schema equivalent for SSMS use.
- `docs`: academic report source and generated project report.

The solution uses ASP.NET Core Identity cookies with Admin, Manager, and SalesRepresentative roles. Seeded test passwords are `ChangeMe123!`; change them before any non-local deployment.

## Authorization design

- `POST /account/login` signs a user in with an Identity cookie and redirects Admin to `/admin`, Manager to `/manager`, and SalesRepresentative to `/sales`.
- `ICurrentUserService` exposes claims-derived identity data to the application layer.
- EF Core global filters protect `Lead`, `CrmTask`, and `ActivityLog` rows for sales representatives. Admin and Manager queries bypass those filters.
- `GET /api/leads/audit` is manager/admin-only and uses `IgnoreQueryFilters()` explicitly for audited access.
- The update/delete endpoints evaluate a resource authorization handler. Representatives can edit their assigned leads but cannot delete; managers and admins can perform both.

## Real-time CRM operations

- `/manager` and `/sales` provide the drag-and-drop Kanban pipeline. A card drop calls `PATCH /api/leads/{id}/status` and broadcasts an activity through SignalR.
- Managers and admins assign leads using `PATCH /api/leads/{id}/assignment`; the representative receives a `leadAssigned` toast through `/hubs/crm`.
- `/admin` provides CSV lead import/export, user creation, audit history, and soft-deleted lead recovery.
- Configure the `Smtp` section using environment variables such as `Smtp__Host` and `Smtp__Password`. Email remains disabled until `Smtp__Enabled=true` is set.
- After pulling these changes, create and apply a new migration: `dotnet ef migrations add RealTimePipelineAuditAndMail` then `dotnet ef database update`.
