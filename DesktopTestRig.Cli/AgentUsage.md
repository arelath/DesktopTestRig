# CLI use by agents

`tree` now defaults to a compact semantic interface outline. Use `--view raw`
for the previous full visual-tree response. This is a deliberate default/schema
change; consumers expecting `typeName`, `childIds`, or raw property names should
add `--view raw` or save `config set commands.tree.view raw`.

## Discover commands without attaching

```powershell
DesktopTestRig.Cli.exe --help
DesktopTestRig.Cli.exe find --help
DesktopTestRig.Cli.exe config set --help
DesktopTestRig.Cli.exe schema --command find --pretty
DesktopTestRig.Cli.exe schema
```

Help uses the actual parser model and includes command options, positional
arguments, descriptions, built-in defaults, and examples. `schema` emits a JSON
envelope containing a versioned command catalog, option types/aliases/arity,
allowed values, defaults, examples, and JSON response schemas. Discovery neither
attaches nor loads configuration, so it also works with a broken defaults file.
The catalog describes built-in defaults; `config get` shows configured defaults.
Target selectors and environment overrides still apply at execution time.

## Search coverage and returned matches

```powershell
DesktopTestRig.Cli.exe find --pid 1234 --name ObjectCatalogPanel --limit 10 --scan-limit 20000
```

`--limit` caps returned matches (default 50). `--scan-limit` independently caps
captured nodes (default `commands.tree.limit`, initially 5000). Both must be
positive. Raising `--limit` no longer implicitly raises the capture budget.

Always examine these fields:

| Field | Meaning |
| --- | --- |
| `searchComplete` | The underlying capture was not truncated. |
| `scannedNodeCount` | Nodes evaluated in this capture. |
| `scanLimit` | Requested capture budget, for `find`. |
| `matchCount` | Matches actually returned. |
| `totalMatchCount` | Matches in captured nodes; a lower bound if search is incomplete. |
| `resultsTruncated` | More captured matches exist than were returned. |
| `truncationReason` | Capture truncation reason, if any. |
| `nextStep` | Guidance for repeating or narrowing the search. |

`searchComplete=true` can coexist with `resultsTruncated=true`: capture was
complete but the result list was capped. A zero-match incomplete search does not
prove absence. With `--require-match`, that case returns `search-incomplete`
(exit 11), with the search metadata in `error.details`. A complete zero-match
search continues to return `no-match` (exit 8). A partial search can still prove
that a returned control exists. Each repeated search captures fresh UI state;
there is no stable pagination cursor across changing snapshots.

## Read and expand the semantic outline

```powershell
DesktopTestRig.Cli.exe tree --pid 1234 --limit 20000
DesktopTestRig.Cli.exe tree --pid 1234 --shape nested --max-depth 3
DesktopTestRig.Cli.exe tree --pid 1234 --format text
DesktopTestRig.Cli.exe node --pid 1234 --target <targetId> --subtree
DesktopTestRig.Cli.exe tree --pid 1234 --view raw --root <targetId> --limit 20000
```

Semantic output keeps windows, named anchors, controls, useful labels, and state;
it removes structural wrappers and duplicate child captions using the recording
stack's semantic rules. Children are reparented to their nearest retained
ancestor. Nodes contain `targetId`, `parentId`, `type`, `label`, semantic `depth`,
and compact `properties` such as `name`, `automationId`, `text`, `checked`,
`selected`, `value`, `visible`, and `enabled`. Full target IDs remain usable by
actions and raw expansion. They are temporary runtime handles, not persistent
test selectors. No supported-action guarantees are inferred from control type.

Visibility and enabled state account for raw ancestors, including omitted
wrappers and ancestors outside a requested subtree. False on any ancestor wins;
unknown state remains omitted instead of being assumed true. Hidden controls are
omitted unless `--include-hidden` is supplied. Missing state is not false. Actual
getter failures remain in `propertyDiagnostics`; `--debug` includes missing
property diagnostics. `--props none` suppresses properties and diagnostics.
Explicit `--props` requests can add scalar properties outside the semantic
property set; those extra properties retain their original names.

`capturedNodeCount` counts raw captured nodes. `nodeCount` counts returned
semantic nodes; `omittedNodeCount` counts nodes removed by semantic/visibility
filtering within the requested raw subtree. `truncated` and `truncationReason`
still describe capture/depth/result limits. Wrapper removal itself is not
truncation. `--max-depth` applies to semantic depth, after wrapper removal;
`--limit` still bounds the raw capture. Inspect raw output when a control is
missing from this intentionally lossy outline.

JSON is the default output format. Text semantic output produces one readable
line per retained node. `--shape nested` returns `roots` with nested `children`;
flat output returns `nodes` with parent IDs. `--include-path` gives original raw
paths. Existing `node`, `props`, streams, and action `--after tree` contracts are
unchanged by the standalone `tree` default.

The Startup publish profile remains at
`artifacts/publish/DesktopTestRig.Cli/Startup/win-x64/DesktopTestRig.Cli.exe`.
