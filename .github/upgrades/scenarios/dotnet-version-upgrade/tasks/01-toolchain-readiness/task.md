# 01-toolchain-readiness: Verify .NET 10 toolchain readiness

Verify that the .NET 10 SDK is installed and that repository-level SDK selection is compatible with `net10.0`. Review any `global.json` constraints and establish a clean baseline restore/build signal before changing the six SDK-style projects.

**Done when**: The .NET 10 SDK is available, SDK selection permits .NET 10, and prerequisite issues that would prevent restoring or building the upgraded solution are resolved or explicitly documented.

## Research Findings

- .NET SDK versions `10.0.301` and `10.0.302` are installed; repository SDK resolution currently selects `10.0.302`.
- `validate_dotnet_sdk_installation` confirmed a compatible SDK for `net10.0`.
- No repository `global.json`, `Directory.Build.props`, or `Directory.Packages.props` file constrains SDK selection or centralizes the target framework.
- The solution contains SDK-style modern .NET projects, so the baseline and upgraded solution should use the IDE/.NET SDK build path.
- This task is a single toolchain-readiness concern and does not require decomposition or source changes.
- The baseline solution build reaches compilation under SDK `10.0.302`. The normal IDE build is blocked because the running `Ams.Worker` process (PID 31680) locks its apphost executable; building with `UseAppHost=false` bypasses that environmental lock.
- The apphost-free baseline then reports four pre-existing `CS0535` errors in `tests/Ams.Application.Tests/SubmissionProposalWorkflowTests.cs`: `FakeSubmissionRepository` does not implement four current `ISubmissionRepository` proposal-delivery members. This is documented baseline debt rather than a .NET 10 SDK prerequisite failure and will be accounted for during upgrade validation.
