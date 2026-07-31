# .NET Version Upgrade Plan

## Overview

**Target**: Upgrade Ams.sln from .NET 9 to .NET 10 LTS.
**Scope**: Six SDK-style projects and approximately 143k lines of code, including three class libraries, an ASP.NET Core API, a Blazor web application, and a Worker Service.

### Selected Strategy
**All-At-Once** — All projects upgraded simultaneously in a single operation.
**Rationale**: Six projects, all on .NET 9, with a clear three-level dependency structure and no incompatible NuGet packages.

**Project group**: `src/Ams.Domain`, `src/Ams.Application`, `src/Ams.Infrastructure`, `src/Ams.Api`, `src/Ams.Web`, and `Ams.Worker`.

## Tasks

### 01-toolchain-readiness: Verify .NET 10 toolchain readiness

Verify that the .NET 10 SDK is installed and that repository-level SDK selection is compatible with `net10.0`. Review any `global.json` constraints and establish a clean baseline restore/build signal before changing the six SDK-style projects.

**Done when**: The .NET 10 SDK is available, SDK selection permits .NET 10, and prerequisite issues that would prevent restoring or building the upgraded solution are resolved or explicitly documented.

---

### 02-dotnet-10-upgrade: Upgrade all projects and compatibility dependencies

Upgrade all six projects to `net10.0` in one coordinated pass, keeping the Domain, Application, Infrastructure, API, Blazor Web, and Worker projects on a consistent target framework. Align the five recommended Microsoft package updates, restore dependencies, and address the assessment's 5 binary-incompatible and 165 source-incompatible API findings inline. Pay particular attention to the Web project, where most compatibility findings occur, and verify behavioral changes around URI, HTTP content, JSON, timing, and dependency-injection/configuration APIs without introducing deferred stubs.

Research should begin with the assessment's exact incident locations and package recommendations, then distinguish compiler failures from behavioral warnings. Preserve existing Blazor and Worker Service architecture while applying only changes required for .NET 10 compatibility.

**Done when**: Every project targets `net10.0`, recommended framework-coupled packages are aligned to supported .NET 10 versions, restore succeeds, all compatibility compilation errors are resolved, and the full solution builds with zero errors and zero warnings.

---

### 03-solution-validation: Validate the upgraded solution

Validate the atomic upgrade across the complete solution after the framework, package, and API work is stable. Run all discovered automated tests and a final full solution build, confirm dependency consistency, and check that no security vulnerabilities or unresolved framework compatibility blockers remain.

**Done when**: The full solution restores and builds with zero errors and zero warnings, all discovered tests pass, package dependencies have no known upgrade blockers or reported vulnerabilities, and all six projects remain on `net10.0`.
