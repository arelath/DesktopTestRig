# Performance Budgets

These budgets are guardrails for local development and CI. Measure regressions on a stable machine and compare medians across repeated runs rather than treating a single sample as definitive.

| Operation | Budget |
| --- | ---: |
| Payload handshake after injection | 5 seconds |
| Typical command round trip | 250 ms |
| Visual tree snapshot with 1,000 nodes | 1 second |
| Selector wait polling interval | 100 ms or greater |
| Stable screenshot wait | 5 seconds |
| Graceful payload shutdown | 5 seconds |

Streaming producers must use bounded queues and report dropped frames rather than allowing unbounded memory growth. Tree capture should honor node and depth limits. Screenshot streams should use the slowest interval that still satisfies the scenario.

Performance changes should be tested separately from correctness changes when possible. Record the target framework, process architecture, node count, image dimensions, and whether the session is local or remote.

## Standalone CLI startup and attachment

PID selection and validation of a cached process name inspect only the selected process. Window discovery still enumerates top-level windows; name and title searches without a valid cache still enumerate processes. Custom snapshot providers can override `GetSnapshot(int)` for a fast lookup; the default implementation preserves enumeration-based providers.

Automatic attachment probes an existing pipe for up to 50 ms before injection. The hello response retains the remaining attachment budget, so a slow handshake does not cause reinjection. Injection and subsequent readiness attempts share one monotonic deadline. `--no-inject` retains the normal connection timeout cap. Custom connectors retain their original timeout unless they implement the separate connection-probe overload.

Help skips service initialization, and non-automation commands lazily avoid creating automation services. For faster standalone calls, publish the ReadyToRun profile:

```powershell
dotnet publish DesktopTestRig.Cli/DesktopTestRig.Cli.csproj -c Release -p:PublishProfile=Startup
```

The executable is `artifacts/publish/DesktopTestRig.Cli/Startup/win-x64/DesktopTestRig.Cli.exe`. This profile defaults to Windows x64 and remains framework-dependent; override `-r` for another supported runtime identifier. The ordinary portable publish remains available. Both paths retain staged payload and injector resources; prepare these through the normal build pipeline before publishing from a clean checkout.

Measure total process time, including runtime startup, using:

```powershell
./Tools/Measure-CliLatency.ps1 -Executable ./artifacts/publish/DesktopTestRig.Cli/Startup/win-x64/DesktopTestRig.Cli.exe
```

Use `-CommandArguments @('ping', '--pid', '1234', '--no-inject')` for a listener that is already running. The script validates exit codes and reports first-run, median, and p95 times. Cold attachment requires a fresh target for each sample; do not mix it with warm reconnection results.

Local Windows x64/.NET 8 measurements on 2026-09-07, using the saved pre-change Release executable and a local HelloWorld WPF target:

| Operation | Baseline median | Updated Release | Updated ReadyToRun |
| --- | ---: | ---: | ---: |
| `version` (25 launches) | 101 ms | 100 ms | 94 ms |
| First `ping`, including injection (5 fresh targets) | 1268 ms | 719 ms | 679 ms |
| Subsequent `ping`, new CLI process (5 targets) | 294 ms | 184 ms | 145 ms |

Target startup is excluded from attach timings; CLI startup, resolution, injection when needed, handshake, and ping are included. These are local observations, not CI timing assertions. Most of the improvement comes from process resolution and the initial pipe probe; ReadyToRun provides an additional, smaller gain.

