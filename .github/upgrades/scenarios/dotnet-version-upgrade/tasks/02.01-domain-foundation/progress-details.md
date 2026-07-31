## Files Modified
- `Directory.Build.props`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/02.01-domain-foundation/task.md`
- `.github/upgrades/scenarios/dotnet-version-upgrade/tasks/02.01-domain-foundation/progress-details.md`

## Build Result
- Errors: 0
- Warnings: 0
- Project built: `src/Ams.Domain/Ams.Domain.csproj`
- Evaluated target framework: `net10.0`
- Output verified: `src/Ams.Domain/bin/Debug/net10.0/Ams.Domain.dll`

## Test Result
- Tests run: 0
- No test project is included in `Ams.sln`; this subtask changes only the centralized TFM and Domain has no source compatibility findings.

## Changes Summary
- Replaced the centralized target framework in `Directory.Build.props` from `net9.0` to `net10.0`.
- Because the repository centralizes this property, all six solution projects now evaluate to .NET 10 as required by the All-at-Once strategy.
- Confirmed the leaf Domain project restores and builds cleanly on .NET 10.

## Done-When Verification
- Ams.Domain targets `net10.0`: verified by evaluated MSBuild property.
- Restore succeeds: verified through successful IDE build.
- Domain builds with zero errors and zero warnings: verified.

## Issues Encountered
- None.
