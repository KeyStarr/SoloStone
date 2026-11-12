# native-bindings

This suite checks that the plugin still fits the Hearthstone client it patches. It reads the client's libraries and the
built plugin as files; it never starts Unity or the game. It has three parts:

- **Binding and metadata checks** (`Program.cs`). Each client method, field and type the plugin hooks or calls must exist
  in `references/game/Assembly-CSharp.dll` with the signature the plugin expects (for example the mulligan coin check,
  the drag and input methods, the pack, deck, reward and crafting hooks).
- **The Unity runtime guard** (`UnityRuntimeGuard.cs`, below).
- **The pack hook IL checks** (`PackHookIlChecks.cs`). They read the intermediate code of the client's pack opening
  methods and check that the pieces the plugin patches are still there in the order it relies on.

Run it with `python3 tests/run.py --suite native-bindings --client "/path/to/Hearthstone"`. It needs your client's
libraries in `references/game/` and `BepInEx.dll` and `0Harmony.dll` in `references/bepinex/` (step 1 of
[BUILDING.md](../../docs/BUILDING.md)). The project builds the plugin first. The checks that need no client are in
`native-bindings-logic`. See [TESTING.md](../../docs/TESTING.md) and the [testing guide](../../docs/TESTING-GUIDE.md).

The list of client calls the plugin makes at startup, `plugin/client-bindings.txt`, is not checked here. See "Client
bindings list" in the guide.

## Unity runtime guard

`UnityRuntimeGuard` runs first in `NativeBindingTests`. It scans all `plugin/**/*.cs` for unavailable serializer APIs
(including comments) and reports `file:line`. It reads the built `plugin/bin/Release/net48/HSNativeUIProbe.dll` (the
default plugin build) with `System.Reflection.Metadata`, without loading Unity, and checks assembly and serializer type
references. Run only this fast guard after building the plugin and the test project:

```sh
dotnet run --no-build --project tests/native-bindings/NativeBindingTests.csproj -c Release -- --runtime-guard-only
```

Assembly allowlist:

- File stems from `references/game/*.dll` (the pinned client Managed references).
- Mono BCL: `mscorlib`, `System`, `System.Core`, `System.Xml`.
- Deployed plugin dependencies: `BepInEx`, `0Harmony`, `SabberStoneCore`.

The BCL and deployed names were read with `PEReader.GetMetadataReader().AssemblyReferences` from an earlier build of
this plugin. All other references in that build have a matching `references/game/*.dll` name. There is no broad
`System.*` exemption: any new BCL dependency fails until it is checked against the actual Unity Mono runtime. Banned
serializer assemblies and types fail even if a reference DLL is added to `references/game`. The guard checks direct
assembly and type dependencies. It does not prove that every method or transitive dependency works.

`SOLOSTONE_INSTALLER` builds compile part of the launcher, which this guard does not see. `tool mono-guard` in
`installer/SoloStone.Launcher.Tests` covers them.
