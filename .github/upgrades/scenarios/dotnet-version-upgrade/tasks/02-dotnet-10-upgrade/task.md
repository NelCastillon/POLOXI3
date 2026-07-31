# 02-dotnet-10-upgrade: Upgrade all projects and compatibility dependencies

Upgrade all six projects to `net10.0` in one coordinated pass, keeping the Domain, Application, Infrastructure, API, Blazor Web, and Worker projects on a consistent target framework. Align the five recommended Microsoft package updates, restore dependencies, and address the assessment's 5 binary-incompatible and 165 source-incompatible API findings inline. Pay particular attention to the Web project, where most compatibility findings occur, and verify behavioral changes around URI, HTTP content, JSON, timing, and dependency-injection/configuration APIs without introducing deferred stubs.

Research should begin with the assessment's exact incident locations and package recommendations, then distinguish compiler failures from behavioral warnings. Preserve existing Blazor and Worker Service architecture while applying only changes required for .NET 10 compatibility.

**Done when**: Every project targets `net10.0`, recommended framework-coupled packages are aligned to supported .NET 10 versions, restore succeeds, all compatibility compilation errors are resolved, and the full solution builds with zero errors and zero warnings.

## Scope Inventory

### Projects Affected
- `src/Ams.Domain/Ams.Domain.csproj` — SDK-style class library; TFM-only change.
- `src/Ams.Application/Ams.Application.csproj` — SDK-style class library; TFM change and assessment review of PBKDF2/source-generator findings.
- `src/Ams.Infrastructure/Ams.Infrastructure.csproj` — SDK-style class library; TFM change, three Microsoft.Extensions package updates, and URI/HTTP/JSON behavioral validation.
- `src/Ams.Api/Ams.Api.csproj` — SDK-style ASP.NET Core API; TFM change and one behavioral compatibility finding.
- `src/Ams.Web/Ams.Web.csproj` — SDK-style Blazor app; TFM change, SignalR client update, and the majority of generated Razor compatibility findings.
- `Ams.Worker/Ams.Worker.csproj` — SDK-style Worker Service; TFM change, Hosting package update, and timing/HTTP/JSON compatibility validation. Existing worker implementations already follow the BackgroundService pattern.

### Packages to Update
| Project | Package | Current | Target |
|---------|---------|---------|--------|
| Ams.Infrastructure | Microsoft.Extensions.Configuration.Binder | 10.0.5 | 10.0.10 |
| Ams.Infrastructure | Microsoft.Extensions.DependencyInjection.Abstractions | 10.0.5 | 10.0.10 |
| Ams.Infrastructure | Microsoft.Extensions.Logging.Abstractions | 10.0.5 | 10.0.10 |
| Ams.Web | Microsoft.AspNetCore.SignalR.Client | 9.0.0 | 10.0.10 |
| Ams.Worker | Microsoft.Extensions.Hosting | 10.0.0 | 10.0.10 |

### Compatibility Findings
- Assessment totals: 5 binary-incompatible, 165 source-incompatible, and 990 behavioral findings.
- Many Web and Application source findings originate in generated `obj` files; source Razor/C# should only be changed when the .NET 10 compiler reports an actual issue.
- Application source includes `Rfc2898DeriveBytes.Pbkdf2`; Worker source includes several `TimeSpan` factory calls. These must be compiler-validated rather than changed preemptively.
- Infrastructure and Worker behavioral findings center on URI, HTTP content, and JSON APIs and require build/test validation.
- No existing `// STUB:` migration markers were found.

### Dependencies and Decomposition
- Dependency graph: Domain → Application → Infrastructure → API/Web/Worker, with Infrastructure also directly referencing Domain.
- Package references are defined directly in the three consuming project files; Central Package Management is not enabled.
- The common .NET upgrade breakdown hint mandates decomposition for three or more projects in a dependency chain. Subtasks therefore follow dependency order while preserving one coordinated parent upgrade and finishing with a full solution compatibility build.
