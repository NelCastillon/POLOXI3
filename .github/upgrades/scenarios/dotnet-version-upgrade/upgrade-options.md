# Upgrade Options — Ams.sln

Assessment: 6 SDK-style projects on net9.0, a three-level dependency graph, 5 recommended package updates, and binary/source API compatibility findings for net10.0.

## Strategy

### Upgrade Strategy
All projects use modern .NET and the six-project dependency graph is small enough for a coordinated upgrade.

| Value | Description |
|-------|-------------|
| **All-at-Once** (selected) | Upgrade all projects together in one atomic pass and validate the complete solution afterward. |
| Top-Down | Upgrade entry-point applications first and temporarily multi-target shared libraries to preserve incremental buildability. |

## Compatibility

### Unsupported API Handling
The assessment reports 5 binary-incompatible and 165 source-incompatible API usages that require validation or code changes.

| Value | Description |
|-------|-------------|
| **Fix Inline** (selected) | Resolve all API changes within their upgrade tasks, leaving no deferred compatibility stubs. |
| Defer Complex Changes | Apply simple replacements immediately and create compilable stubs plus follow-up subtasks for complex changes. |
