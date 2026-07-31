# .NET Version Upgrade

## Preferences
- **Flow Mode**: Automatic
- **Target Framework**: net10.0

## Source Control
- **Source Branch**: Develop
- **Working Branch**: upgrade-dotnet-10
- **Commit Strategy**: Single Commit at End
- **Branch Sync**: Auto (Merge)

## Upgrade Options
**Source**: .github/upgrades/scenarios/dotnet-version-upgrade/upgrade-options.md

### Strategy
- Upgrade Strategy: All-at-Once

### Compatibility
- Unsupported API Handling: Fix Inline

## Strategy
**Selected**: All-at-Once
**Rationale**: All six projects are SDK-style and target modern .NET, with a compact three-level dependency graph suited to one coordinated target-framework update.

### Execution Constraints
- Upgrade all projects together as one atomic framework transition.
- Keep project references on a consistent target framework throughout the upgrade.
- Resolve package and API compatibility issues inline rather than creating deferred stubs.
- Validate the complete solution after all project updates are applied.
- Run affected tests and finish with a warning-free full solution build.
