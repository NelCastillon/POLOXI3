# 02.03-entry-applications: Upgrade API, Blazor, and Worker applications

# 02.03-entry-applications: Upgrade API, Blazor, and Worker applications

## Objective
Complete the coordinated transition by updating Ams.Api, the Blazor Ams.Web application, and Ams.Worker to `net10.0`. Align SignalR Client and Microsoft.Extensions.Hosting with .NET 10, resolve actual compiler and warning findings across the complete solution, and retain the existing BackgroundService-based Worker architecture.

## Scope
- Replace the target framework in Ams.Api, Ams.Web, and Ams.Worker with `net10.0`.
- Update Microsoft.AspNetCore.SignalR.Client in Ams.Web to `10.0.10` and Microsoft.Extensions.Hosting in Ams.Worker to `10.0.10`.
- Resolve .NET 10 source/binary compatibility errors in source Razor/C#; never edit generated `obj` files.
- Account for the pre-existing Ams.Application.Tests fake repository interface mismatch and fix warnings in all touched projects.
- Restore and build the full solution with zero errors and zero warnings; run all affected discovered tests.

**Done when**: All six production projects target `net10.0`, recommended package updates are applied, the complete solution restores and builds with zero errors and zero warnings, and affected tests pass.

## Research Findings

- API, Blazor Web, and Worker projects inherit the centralized `net10.0` target; they do not define local target frameworks.
- Ams.Web directly references `Microsoft.AspNetCore.SignalR.Client` `9.0.0`; the assessment recommends `10.0.10`.
- Ams.Worker directly references `Microsoft.Extensions.Hosting` `10.0.0`; the assessment recommends `10.0.10`. If NuGet reports it as redundant under the Worker SDK, the warning-free framework-provided dependency will take precedence.
- Ams.Api has no assessment-recommended package update. Its existing SignalR Core and Swashbuckle references will be compiler/restore validated.
- The solution service reports no test projects among the six production projects. The repository contains `tests/Ams.Application.Tests`, which is outside `Ams.sln` and has a known pre-existing fake repository interface mismatch; it will be built/tested explicitly after production projects compile.
- Most Web compatibility incidents are in generated Razor files under `obj`; generated files will never be edited. Source changes will be driven by actual .NET 10 diagnostics.
- Worker services already derive from `BackgroundService`; the upgrade will preserve that architecture.
- This subtask is the final entry-application group in the dependency-ordered decomposition and does not require further breakdown.
