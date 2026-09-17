# ALISflyt — Municipal Grant Application Portal (work in progress)

Short overview
- Purpose: a small municipal web portal (ALISflyt) for creating, editing and submitting grant cases (søknader).
- Status: early implementation of Domain, Application, Infrastructure and a thin Web UI (Razor / MVC) for the GrantCases flow.

Technology stack
- .NET 10 (net10.0)
- C# 12
- ASP.NET Core MVC with Razor views (server rendered)
- EF Core 10 for persistence
- SQL Server / LocalDB used for local development
- xUnit for tests

Project layout
- Alisflyt.Domain — domain entities, enums and business rules (GrantCase entity and status enum)
- Alisflyt.Application — application services, DTOs and requests (IGrantCaseApplicationService, DTOs)
- Alisflyt.Infrastructure — EF Core DbContext, repositories, migrations and development seeding
- Alisflyt.Web — UI project (controllers, Razor views, ViewModels, static assets)
- tests/* — unit and integration test projects (Domain, Application, IntegrationTests, etc.)

Key implemented features
- Domain model for GrantCase with rules for draft/update/submit
- Application service IGrantCaseApplicationService and implementation
- EF Core persistence and initial migration (InitialCreate)
- Thin MVC controller GrantCasesController with Create/Edit/Details/Index/Submit flows
- Razor views and ViewModels for GrantCases (list, create, edit, details)
- Server-side validation for percentage and date ranges; Norwegian UI labels and messages

How to build and run (development)
1. Restore/build/tests:
   dotnet build Alisflyt.slnx --verbosity minimal
   dotnet test Alisflyt.slnx --no-build --verbosity minimal

2. Run web app (development environment requires DefaultConnection in appsettings.Development.json):
   dotnet run --project Alisflyt.Web/Alisflyt.Web.csproj

3. When running in Development the app will apply migrations and optionally seed demo data. To enable seeding set SeedDemoData=true in appsettings.Development.json.

Notes for contributors
- The Web layer is intentionally thin: controllers call IGrantCaseApplicationService and map DTOs to ViewModels.
- Domain rules live in Alisflyt.Domain and must not be duplicated in the Web layer; the Web validates only presentation concerns and mirrors domain expectations.
- Tests: keep existing test projects green before changing domain or persistence.

Where to look
- GrantCases controller: Alisflyt.Web/Controllers/GrantCasesController.cs
- Views: Alisflyt.Web/Views/GrantCases/
- ViewModels: Alisflyt.Web/ViewModels/GrantCases/
- Domain entity: Alisflyt.Domain/Entities/GrantCase.cs
- Application service: Alisflyt.Application/Services/IGrantCaseApplicationService.cs

If you need a focused task (tests for Submit, translation, accessibility), open an issue or request a short implementation.

