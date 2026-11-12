# Unity runtime guard

`UnityRuntimeGuard` runs first in `NativeBindingTests`, already built and run by
`python3 tests/run.py --suite native-bindings`. It scans all `plugin/**/*.cs` for unavailable
serializer APIs (including comments) and reports `file:line`. It reads the built
`plugin/bin/Release/net48/HSNativeUIProbe.dll` with `System.Reflection.Metadata`,
without loading Unity, and checks assembly and serializer type references.
Run only this fast guard after building the plugin and test project:

```sh
dotnet run --no-build --project tests/native-bindings/NativeBindingTests.csproj -c Release -- --runtime-guard-only
```

Assembly allowlist:

- File stems from `references/game/*.dll` (the pinned client Managed references).
- Mono BCL: `mscorlib`, `System`, `System.Core`, `System.Xml`.
- Deployed plugin dependencies: `BepInEx`, `0Harmony`, `SabberStoneCore`.

BCL/deployed names were extracted with `PEReader.GetMetadataReader().AssemblyReferences`
from the existing main-branch build at main `addeae84febe2cd0d65ce7f5357ec9b6b29e7401`,
`plugin/bin/Release/net48/HSNativeUIProbe.dll`, SHA-256
`75250efeec81ae3a308e2325b4431814643ccaf28cdeddb9cef7db21e5585c82`.
All other references in that build have corresponding `references/game/*.dll` names.
No broad `System.*` exemption: any new BCL dependency fails until checked against the
actual Unity Mono runtime. Banned serializer assemblies/types fail even if a reference
DLL is added to `references/game`. This checks direct assembly/type dependencies;
it does not establish compatibility of every method or transitive dependency.

`SOLOSTONE_INSTALLER` builds compile part of the launcher, which this guard does not see. `tool mono-guard` in
`installer/SoloStone.Launcher.Tests` covers them.
