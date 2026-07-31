# 02.01-domain-foundation: Upgrade the domain foundation to .NET 10

# 02.01-domain-foundation: Upgrade the domain foundation to .NET 10

## Objective
Update `src/Ams.Domain/Ams.Domain.csproj` from `net9.0` to `net10.0` as the leaf of the project-reference graph. Preserve the SDK-style single-target configuration and verify the project compiles cleanly on .NET 10 before dependent libraries are moved.

## Scope
- Replace the existing target framework in `src/Ams.Domain/Ams.Domain.csproj`.
- Restore and build the Domain project with zero errors and zero warnings.
- Do not introduce multi-targeting or unrelated source changes.

**Done when**: Ams.Domain targets `net10.0`, restore succeeds, and the Domain project builds with zero errors and zero warnings.

## Research Findings

- `Ams.Domain.csproj` is a one-line SDK-style project and does not define `TargetFramework` locally.
- The evaluated `net9.0` target comes from the repository-root `Directory.Build.props` file.
- `Directory.Build.props` centralizes `TargetFramework` for the full solution, so changing it to `net10.0` performs the intended All-at-Once framework transition for every project; later subtasks will align packages and resolve dependent-project compilation issues.
- Ams.Domain has no project or package dependencies and the assessment reports no Domain API compatibility findings.
- No further decomposition is needed: this subtask contains one centralized property replacement and a focused Domain restore/build validation.
