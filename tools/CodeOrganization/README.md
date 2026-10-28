# Code organization checks

Run from the repository root with the pinned .NET SDK:

```powershell
dotnet run --project tools/CodeOrganization -- check .
```

The read-only Roslyn check inventories every C# source under all five backend layers,
including the empty historical orphan outside the Infrastructure project. It returns
0 for no findings, 1 for findings, and 2 for execution/configuration errors. It writes
`docs/refactoring/CODE_ORGANIZATION_INVENTORY.json`. No NuGet Roslyn dependency is added:
the tool references the compiler assemblies shipped with the selected SDK.

Checks include top-level types and delegates, filenames, namespace/folder correspondence,
enum/interface/exception placement, non-private nested types, compressed parameter lists
and statement blocks, project dependencies, cycles, and forbidden layer imports. Shared
contracts intentionally stay in Domain; Domain has no project dependencies. The existing
`ITransactionManager` repository abstraction is deliberately exempt from the application
manager naming rule. Multiple declarations of a partial type use separate implementation
files; their filename suffix is deliberately allowed.

`generated-sources.json` explicitly lists the 40 existing Database First scaffold files.
These and sources with an explicit auto-generated header are excluded from organizational
and formatting rules, but inventoried. After **reviewed** scaffolding, update this list.
Non-generated persistence sidecars are checked normally. `bin` and `obj` are excluded.

To normalize whitespace and multiline signatures in ordinary backend sources:

```powershell
dotnet run --project tools/CodeOrganization -- format .
```

This command edits ordinary sources. It preserves tokens and comments; it does not split
or relocate types. Run build/tests and inspect the diff afterwards.

The migration's additional preservation audit can be reproduced against its exact baseline:

```powershell
git archive --format=zip --output=docs/refactoring/validation/baseline-source.zip 3cecefc205b1645d6235a43406182c8c96de96c2 Backend/ElectronicRegisterAPI/src
dotnet run --project tools/CodeOrganization -- verify-preservation .
```

The audit compares declaration tokens (including attributes, enum values, constructor
parameters, exception messages and method bodies), plus Program statements. It normalizes
namespace qualifications and accounts for the two extracted public nested records and
the two relocated null observers. It checks the 40 scaffold sources byte-for-byte.
The archive is local validation input, not a dependency of the normal organization check.

These checks detect structural mistakes, not all responsibility errors, authorization flaws,
reflection consumers or runtime DI issues. Builds, reviews and executable tests remain
necessary. External consumers must update CLR namespaces after type moves; JSON property
names and HTTP routes are preserved.
