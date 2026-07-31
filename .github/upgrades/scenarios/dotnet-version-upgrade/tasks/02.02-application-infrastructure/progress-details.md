## Files Modified
- `src/Ams.Infrastructure/Ams.Infrastructure.csproj`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/02.02-application-infrastructure/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/02.02-application-infrastructure/progress-details.md`

## Build Result
- Errors: 0
- Warnings: 0
- Projects built: `Ams.Application`, `Ams.Infrastructure`, and referenced `Ams.Domain`
- Evaluated frameworks: `Ams.Application=net10.0`, `Ams.Infrastructure=net10.0`
- Explicit Infrastructure restore: successful with zero warnings

## Test Result
- Tests run: 0
- No test project is included in `Ams.sln`; solution-level test discovery and execution remains part of the entry-application/final validation work.

## Changes Summary
- Confirmed both libraries inherit and evaluate the centralized `net10.0` target.
- Initially aligned the three assessed Microsoft.Extensions references to `10.0.10`.
- NuGet then reported `NU1510` for all three because `Microsoft.AspNetCore.App` on .NET 10 provides them automatically. Removed the redundant explicit references to achieve the required warning-free restore/build while retaining the supported .NET 10 framework versions.
- PBKDF2, URI, HTTP content, and JSON source compiled without compatibility changes; generated `obj` files were not modified.

## Done-When Verification
- Ams.Application and Ams.Infrastructure target `net10.0`: verified through evaluated MSBuild properties.
- Framework-coupled Microsoft.Extensions dependencies aligned: verified through the .NET 10 shared framework; redundant explicit references removed per NU1510.
- Restore succeeds: verified with zero warnings.
- Both libraries build with zero errors and zero warnings: verified.

## Issues Encountered
- Explicit `10.0.10` Microsoft.Extensions references generated NU1510 pruning warnings. Resolved by removing the redundant references and using the existing `Microsoft.AspNetCore.App` framework reference.
