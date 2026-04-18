# Ams Enterprise - .NET 9 Blazor Server + API + Dapper + Syncfusion

This package contains a multi-project enterprise solution:

- `Ams.Domain` - domain entities and enums
- `Ams.Application` - DTOs, service abstractions, repository abstractions, feature requests
- `Ams.Infrastructure` - Dapper repositories, Azure-ready SQL connection factory, DI
- `Ams.Api` - ASP.NET Core Web API with Swagger and health checks
- `Ams.Web` - Blazor Server front end using Syncfusion components

## Solution goals
This solution is designed as an enterprise-ready starting point for an AMS platform with:
- Leads
- Accounts
- Opportunities
- Agreements
- Invoices
- Commission Plans
- Workflow
- Audit
- Assistant

## Technology
- .NET 9
- Blazor Server
- ASP.NET Core Web API
- Dapper
- Syncfusion Blazor
- Azure-ready SQL connectivity

## Important notes
- This environment did **not** have the .NET SDK installed, so I could not compile-test the solution here.
- The package is structured to be opened in Visual Studio and restored normally.
- Add your Syncfusion license key in `src/Ams.Web/appsettings.json` and/or startup as needed.
- Update API and SQL connection strings before running.

## Run order in Visual Studio
1. Open `Ams.sln`
2. Restore NuGet packages
3. Set multiple startup projects:
   - `Ams.Api`
   - `Ams.Web`
4. Update:
   - `src/Ams.Api/appsettings.Development.json`
   - `src/Ams.Web/appsettings.json`
5. Run database deployment scripts
6. Press F5

## Default local URLs
- API: `https://localhost:7051`
- Web: `https://localhost:7061`

## End-to-end sample included
- Create Lead page in Blazor Server
- Leads API POST endpoint
- Dapper insert repository
- Search/list pages for all major modules

## Next expansion you may want
- authentication / authorization
- MediatR / validation
- transaction / unit of work wrapper
- full command handlers for all modules
- richer Syncfusion layouts and dialogs
- dashboards backed by real KPI queries
