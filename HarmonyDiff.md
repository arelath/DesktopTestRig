# Harmony Diff

Comparison date: 2026-05-13

Compared:
- DesktopTestRig: `DesktopTestRig/AppDriverPayload/AppHooks.cs`, `DesktopTestRig/AppDriverPayload/Patching/*`, `.build/Build.cs`, payload command integration.
- DesktopTestRig2: `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppHooks.cs`, `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppDriverPayload.cs`, payload command integration.

## Short Answer

Yes, there are differences.

The actual Harmony patch target set is effectively the same in both projects, but the injection/loading strategy, patch activation model, diagnostics, defensive reflection behavior, and command integration are different.

## Same Patch Targets

Both projects patch the same WPF/dialog surfaces:

| Target | DesktopTestRig | DesktopTestRig2 | Notes |
| --- | --- | --- | --- |
| `MouseDevice.GetButtonState` | Yes | Yes | DesktopTestRig fakes left/right/middle mouse pressed state; DesktopTestRig2 fakes left/right. |
| `ButtonBase.UpdateIsPressed` | Yes | Yes | Toggles button pressed state without real mouse coordinates. |
| `GridViewColumnHeader.IsMouseOutside` | Yes | Yes | Forces mouse-inside behavior. |
| `MenuItem.HandleMouseDown` | Yes | Yes | Clicks menu headers on mouse down. |
| `MenuItem.HandleMouseUp` | Yes | Yes | Clicks menu items on mouse up. |
| `MenuItem.UpdateIsPressed` | Yes | Yes | Keeps menu pressed state aligned with fake mouse state. |
| `RibbonMenuItem.UpdateIsPressed` | Yes | Yes | Same pressed-state workaround for ribbon menu items. |
| `DataGridCheckBoxColumn.IsMouseOver` | Yes | Yes | Forces checkbox-column hit testing to succeed. |
| `Window.ShowDialog` | Yes | Yes | Sets a modal-dialog flag. |
| `Microsoft.Win32.CommonDialog.ShowDialog` | Yes | Yes | Patches public overloads with zero or one parameter. |
| `System.Windows.Forms.CommonDialog.ShowDialog` | Yes | Yes | Patches public overloads with zero or one parameter. |

Source refs:
- DesktopTestRig `AppHooks.cs`: patch declarations around lines 222, 243, 254, 264, 282, 301, 319, 329, 339, and 349.
- DesktopTestRig2 `AppHooks.cs`: patch declarations around lines 64, 86, 103, 114, 136, 162, 186, 200, 210, and 221.

## Dependency Loading And Injection

### DesktopTestRig2

DesktopTestRig2 loads Harmony as a loose dependency inside the injected payload. `AppDriverPayload.LoadDependencies(...)` explicitly calls:

```text
Load("0Harmony.dll")
```

It also installs an `AssemblyResolve` handler that resolves loose dependency DLLs from the payload DLL directory.

Source refs:
- `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppDriverPayload.cs:435`
- `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppDriverPayload.cs:481`
- `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppDriverPayload.cs:482`

### DesktopTestRig

DesktopTestRig does not have an equivalent payload-side `LoadDependencies(...)` or explicit `Load("0Harmony.dll")` call in `AppDriverPayload`.

Instead, the payload project marks Harmony as an internalized payload dependency. `Shared/DesktopTestRig.PayloadRepack.targets` consumes that MSBuild metadata and writes payloads under `artifacts/staging/payloads/<family>/DesktopTestRig.dll`.

Source refs:
- `DesktopTestRig.Payload/DesktopTestRig.Payload.csproj`
- `Shared/DesktopTestRig.PayloadRepack.targets`

Impact:
- DesktopTestRig2 expects `0Harmony.dll` to be present beside the injected payload.
- DesktopTestRig expects the generated/repacked payload assembly to carry Harmony internally.
- This is a real deployment difference, even though both projects reference the same package version.

## Package Version

Both projects use `Lib.Harmony` version `2.3.5`.

Source refs:
- DesktopTestRig `Directory.Packages.props`
- DesktopTestRig2 `D:/dev/research/DesktopTestRig2/Directory.packages.props`

## Patch Activation Model

### DesktopTestRig2

DesktopTestRig2 applies all Harmony patches from the `AppHooks` static constructor:

```text
new Harmony("com.desktoptestrig.apphooks.patch").PatchAll(Assembly.GetExecutingAssembly())
```

`EnsureHooked()` is intentionally empty; calling it triggers the static constructor. DesktopTestRig2 calls `AppHooks.EnsureHooked()` from command processing after building a tree service and only when WPF or WinForms targets are present.

Source refs:
- `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppHooks.cs:23`
- `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppHooks.cs:55`
- `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppHooks.cs:59`
- `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppDriverPayload.cs:163`
- `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppDriverPayload.cs:164`

### DesktopTestRig

DesktopTestRig applies Harmony through a runtime patch coordinator:

```text
AppDriverPayload.Start -> AppHooks.Apply(...) -> RuntimeWpfPatchCoordinator.ApplyCurrentRuntime(...)
```

`AppHooks.EnsureHooked()` does the actual `PatchAll(...)`, guarded by a lock and a `hooksApplied` flag:

```text
new Harmony("com.DesktopTestRig.apphooks.patch").PatchAll(Assembly.GetExecutingAssembly())
```

DesktopTestRig calls `AppHooks.Apply(...)` during payload startup and logs a `WpfPatchResult` summary. It also has a best-effort `TargetActionCommand.TryEnsureAppHooks()` before WPF clicks.

Source refs:
- `DesktopTestRig/AppDriverPayload/AppDriverPayload.cs:32`
- `DesktopTestRig/AppDriverPayload/AppDriverPayload.cs:33`
- `DesktopTestRig/AppDriverPayload/AppHooks.cs:32`
- `DesktopTestRig/AppDriverPayload/AppHooks.cs:40`
- `DesktopTestRig/AppDriverPayload/AppHooks.cs:48`
- `DesktopTestRig/AppDriverPayload/Commands/TargetActionCommand.cs:488`
- `DesktopTestRig/AppDriverPayload/Commands/TargetActionCommand.cs:492`

Impact:
- DesktopTestRig2 is mostly lazy: patches are first applied when command processing sees supported targets.
- DesktopTestRig is mostly startup-driven: it attempts patch coordination during payload startup.
- DesktopTestRig records diagnostics; DesktopTestRig2 does not.

## Optional Patch Diagnostics Are Not One-To-One With Harmony Targets

DesktopTestRig has a `DefaultWpfPatchCatalog` that checks optional private members:

| Diagnostic patch name | Checked member |
| --- | --- |
| `MouseButtonState` | `System.Windows.Input.MouseDevice._mouseButtonState` |
| `ButtonPressedState` | `System.Windows.Controls.Primitives.ButtonBase.SetIsPressed` |
| `WpfDialogOwner` | `System.Windows.Window.ShowDialog` |
| `WinFormsDialogOwner` | `System.Windows.Forms.Form.ShowDialog` |
| `ModernMenuMode` | `System.Windows.Controls.MenuItem.IgnoreNextLeftRelease` on modern .NET only |

Source refs:
- `DesktopTestRig/AppDriverPayload/Patching/DefaultWpfPatchers.cs:42`
- `DesktopTestRig/AppDriverPayload/Patching/DefaultWpfPatchers.cs:44`
- `DesktopTestRig/AppDriverPayload/Patching/DefaultWpfPatchers.cs:50`

Important difference:
- Each optional patch's `Apply` delegate is just `AppHooks.EnsureHooked`.
- `EnsureHooked` runs Harmony `PatchAll(...)`, which applies all Harmony patches in the assembly.
- Therefore the optional patch catalog is a diagnostic/availability gate, not an individual per-Harmony-method patch list.

Consequences:
- DesktopTestRig can report several optional patch names as "applied" even though Harmony was only applied once.
- DesktopTestRig does not individually validate every actual Harmony target. For example, there are no separate optional entries for `GridViewColumnHeader.IsMouseOutside`, `MenuItem.HandleMouseDown`, `MenuItem.HandleMouseUp`, `RibbonMenuItem.UpdateIsPressed`, `DataGridCheckBoxColumn.IsMouseOver`, or `Microsoft.Win32.CommonDialog.ShowDialog`.
- DesktopTestRig2 has no equivalent diagnostics, but its behavior is simpler: the static constructor warms and patches everything in one path.

## Warmup And Reflection Differences

### DesktopTestRig2

DesktopTestRig2 warms the hooked methods in the `AppHooks` static constructor using `ReflectionUtility.InvokeOn(...)` and direct reflection calls. The warmup path is direct and assumes the expected private methods/properties exist.

Source refs:
- `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppHooks.cs:23`
- `D:/dev/research/DesktopTestRig2/DesktopTestRig/AppDriverPayload/AppHooks.cs:55`

### DesktopTestRig

DesktopTestRig moves warmup into `WarmupHookedMembers()` and wraps each warmup call in `TryWarmup(...)`, swallowing non-fatal exceptions. It also has local overload selection helpers (`InvokeBestMatch`, `GetCandidateMethods`, `ParametersMatch`) rather than relying on DesktopTestRig2's `ReflectionUtility`.

Source refs:
- `DesktopTestRig/AppDriverPayload/AppHooks.cs:47`
- `DesktopTestRig/AppDriverPayload/AppHooks.cs:96`
- `DesktopTestRig/AppDriverPayload/AppHooks.cs:121`

Impact:
- DesktopTestRig is more tolerant of runtime/framework drift during warmup.
- DesktopTestRig2 fails faster if expected internals are unavailable.

## Patch Body Differences

Most patch bodies are behaviorally equivalent, but DesktopTestRig made several defensive changes:

| Area | DesktopTestRig2 | DesktopTestRig |
| --- | --- | --- |
| `ButtonBase.UpdateIsPressed` | Directly reads `IsPressed` and invokes `SetIsPressed(true/false)`. | Reads `IsPressed` with fallback `false` and invokes `SetIsPressed(!isPressed)` through local best-match reflection. |
| `MenuItem.HandleMouseDown` | Directly casts `__args[0]` to `MouseButtonEventArgs` and directly reads `Role`. | Checks `__args.Length > 0 && __args[0] is MouseButtonEventArgs`; defaults missing `Role` to `TopLevelItem`. |
| `MenuItem.HandleMouseUp` | Same direct cast/read pattern. | Same defensive arg/type guard and role fallback. |
| `MenuItem.UpdateIsPressed` | Direct property/field access; invokes `ClearValue` when not pressed. | Null-safe property/field lookup; only invokes `ClearValue` if the property key exists. |
| `RibbonMenuItem.UpdateIsPressed` | Explicit if/else set. | Single set using `Mouse.LeftButton == Pressed`. |
| Dialog patches | Same intent. | Same intent, but `Window.ShowDialog` prefix does not take `__instance`. |

Potential behavioral edge:
- In the DesktopTestRig menu down/up patches, if Harmony ever passes unexpected args, DesktopTestRig returns `false` and suppresses the original method without doing the DesktopTestRig2 behavior. DesktopTestRig2 would likely throw in that situation. This is probably only relevant under framework signature drift.

## Shared State Differences

### Mouse state

DesktopTestRig2 exposes mutable fields:

```text
public static bool? IsLeftMousePressed
public static bool? IsRightMousePressed
```

DesktopTestRig exposes private setters and routes changes through `SetButton(...)` / `ResetMouseState()`:

```text
public static bool? IsLeftMousePressed { get; private set; }
public static bool? IsRightMousePressed { get; private set; }
```

Source refs:
- DesktopTestRig2 `AppHooks.cs:275`
- DesktopTestRig `AppHooks.cs:72`
- DesktopTestRig `AppHooks.cs:74`

Impact:
- DesktopTestRig2 command code can set mouse state directly. Its `RaiseEventCommand` does this for mouse routed events.
- DesktopTestRig command code cannot set those fields directly. Its WPF click path uses `SetButton(...)`; its generic known/expression routed-event path does not appear to fake mouse pressed state the same way.

Relevant refs:
- DesktopTestRig2 `RaiseEventCommand.cs:61`
- DesktopTestRig2 `RaiseEventCommand.cs:65`
- DesktopTestRig `TargetActionCommand.cs:452`
- DesktopTestRig `TargetActionCommand.cs:456`
- DesktopTestRig `TargetActionCommand.cs:624`
- DesktopTestRig `TargetActionCommand.cs:642`

### Mouse-over internals

DesktopTestRig2 stores non-nullable `FieldInfo` / `MethodInfo` fields and uses them directly:

```text
AppHooks.MouseOverElement.SetValue(...)
AppHooks.WriteElementOverElement.Invoke(...)
```

DesktopTestRig stores nullable values and uses null propagation:

```text
AppHooks.MouseOverElement?.SetValue(...)
AppHooks.WriteElementOverElement?.Invoke(...)
```

Source refs:
- DesktopTestRig2 `ClickCommand.cs:164`
- DesktopTestRig2 `ClickCommand.cs:166`
- DesktopTestRig `TargetActionCommand.cs:506`
- DesktopTestRig `TargetActionCommand.cs:508`

Impact:
- DesktopTestRig avoids null-reference failures if WPF internals move.
- DesktopTestRig2 fails faster if those internals are not found.

## Modal Dialog Integration

Both projects use the `ShowDialogCalled` flag to return pending/native-dialog behavior when modal dialogs block the UI thread.

Differences:
- DesktopTestRig2 resets `ShowDialogCalled` in the main command processing path after `CheckIfShowDialogCalledOrTimeout(...)`.
- DesktopTestRig resets it in `AppDriverCommandDispatcher.RunUiHandlerWithModalWatchAsync(...)` and uses `WaitForShowDialogAsync(...)`.
- DesktopTestRig's wait method returns `Finished` if no dialog is seen by timeout; DesktopTestRig2's method returns `Pending` when its loop exits by timeout. In normal command flow, command timeout handling may mask some of this, but the method-level behavior differs.

Source refs:
- DesktopTestRig2 `AppDriverPayload.cs:154`
- DesktopTestRig2 `AppDriverPayload.cs:188`
- DesktopTestRig2 `AppDriverPayload.cs:391`
- DesktopTestRig `AppDriverCommandDispatcher.cs:145`
- DesktopTestRig `AppDriverCommandDispatcher.cs:152`
- DesktopTestRig `AppDriverCommandDispatcher.cs:185`
- DesktopTestRig `AppDriverCommandDispatcher.cs:203`

## Visibility And Test Hooks

DesktopTestRig2:
- `AppHooks` is `internal`.
- No equivalent `WpfPatchResult`.
- No `ResetForTests()` on `AppHooks`.

DesktopTestRig:
- `AppHooks` is `public`.
- Exposes `LastResult` and `Apply(...)`.
- Has `ResetForTests()` for patch diagnostics and mutable state.

Source refs:
- DesktopTestRig2 `AppHooks.cs:21`
- DesktopTestRig `AppHooks.cs:16`
- DesktopTestRig `AppHooks.cs:19`
- DesktopTestRig `AppHooks.cs:32`
- DesktopTestRig `AppHooks.cs:88`

## Summary Of Material Differences

1. Same Harmony package version and same declared Harmony patch targets.
2. DesktopTestRig2 loads `0Harmony.dll` explicitly at injection time; DesktopTestRig repacks/internalizes Harmony into generated payload assemblies.
3. DesktopTestRig2 applies patches through the `AppHooks` static constructor; DesktopTestRig applies through `AppHooks.Apply(...)` plus a runtime patch coordinator.
4. DesktopTestRig logs patch diagnostics and catches/skips optional patch failures; DesktopTestRig2 has no equivalent diagnostics.
5. DesktopTestRig's optional patch names are not a one-to-one list of Harmony patch methods; they are availability checks that all call `EnsureHooked()`.
6. DesktopTestRig warmup and patch bodies are more null-safe and framework-drift tolerant.
7. DesktopTestRig2's command integration directly mutates Harmony mouse state for `RaiseEventCommand`; DesktopTestRig's direct mouse-state integration is visible in the click path, not in generic routed-event paths.
8. DesktopTestRig uses nullable mouse-over reflection handles and best-effort behavior; DesktopTestRig2 uses direct handles and fails faster.
9. Modal dialog detection uses the same Harmony flag idea but has different dispatcher/checker structure and slightly different timeout-return semantics.

## Bottom Line

DesktopTestRig is not a byte-for-byte port of DesktopTestRig2's Harmony injection. It preserves the main patch target set, but it has a more defensive, diagnostic, repacked, startup-oriented patching model. The biggest parity questions are not the Harmony patch list itself; they are:

- whether DesktopTestRig's repacked payload deployment is always used in the injection path,
- whether startup-time patching should remain different from DesktopTestRig2's command-time lazy patching,
- whether the optional patch diagnostics should be aligned with actual Harmony patch targets,
- whether DesktopTestRig's routed-event paths should fake `IsLeftMousePressed` / `IsRightMousePressed` the way DesktopTestRig2's `RaiseEventCommand` does.
