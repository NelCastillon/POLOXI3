# 03-solution-validation: Validate the upgraded solution

Validate the atomic upgrade across the complete solution after the framework, package, and API work is stable. Run all discovered automated tests and a final full solution build, confirm dependency consistency, and check that no security vulnerabilities or unresolved framework compatibility blockers remain.

**Done when**: The full solution restores and builds with zero errors and zero warnings, all discovered tests pass, package dependencies have no known upgrade blockers or reported vulnerabilities, and all six projects remain on `net10.0`.

## Research Findings

- All production projects and `tests/Ams.Application.Tests` inherit the repository-root `net10.0` target from `Directory.Build.props`.
- The previous coordinated build succeeded after package and test-interface fixes; final validation must independently repeat a non-incremental full solution build, direct test execution, target-framework verification, and vulnerability scan.
- Visual Studio Test Explorer may still cache the old `net9.0` test container, so the authoritative test run will use `dotnet test` against the test project and its `net10.0` output.
- A running Ams.Worker process can lock its generated apphost executable. Final compilation uses `UseAppHost=false`, which validates all managed outputs without stopping the user's running Worker process.
- This is a validation-only task with no internal decision points and requires no decomposition.
