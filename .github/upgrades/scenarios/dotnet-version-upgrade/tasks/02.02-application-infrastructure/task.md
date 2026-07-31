# 02.02-application-infrastructure: Upgrade application and infrastructure libraries

# 02.02-application-infrastructure: Upgrade application and infrastructure libraries

## Objective
Update `src/Ams.Application` and `src/Ams.Infrastructure` to `net10.0` after the Domain foundation. Align the three assessment-recommended Microsoft.Extensions packages in Infrastructure to `10.0.10`, then resolve actual .NET 10 compiler issues in these libraries without changing generated `obj` files.

## Scope
- Replace each library's single target framework with `net10.0`.
- Update Microsoft.Extensions.Configuration.Binder, Microsoft.Extensions.DependencyInjection.Abstractions, and Microsoft.Extensions.Logging.Abstractions in Ams.Infrastructure from `10.0.5` to `10.0.10`.
- Compiler-validate PBKDF2, URI, HTTP content, and JSON usage; change source only for confirmed incompatibilities.
- Restore and build both libraries and their Domain dependency with zero errors and zero warnings.

**Done when**: Ams.Application and Ams.Infrastructure target `net10.0`, the three packages are at `10.0.10`, restore succeeds, and both libraries build with zero errors and zero warnings.

## Research Findings

- Both projects inherit `TargetFramework` from the repository-root `Directory.Build.props` and currently evaluate to `net10.0`; no local TFM edits are required.
- `Ams.Application` references only `Ams.Domain` and has no explicit NuGet packages. Assessment findings in generated regex source should not be edited; `AuthService` PBKDF2 usage will be validated by compilation.
- `Ams.Infrastructure` uses standard per-project package references rather than Central Package Management. Updating the three Microsoft.Extensions references to `10.0.10` caused `NU1510`: .NET 10's `Microsoft.AspNetCore.App` framework reference already supplies these assemblies and recommends removing the redundant package references. They are therefore removed rather than retained as warning-producing explicit references.
- Infrastructure's URI, HTTP content, and JSON findings are behavioral indicators; source changes are only warranted if the .NET 10 compiler or tests identify a concrete incompatibility.
- This is an atomic library/package-alignment subtask: the dependency order is already represented by Domain → Application → Infrastructure, with no unresolved decision gate or further mandatory decomposition.
