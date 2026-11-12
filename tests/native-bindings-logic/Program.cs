// Plugin logic checks that need no Hearthstone client: the hook classes extracted from plugin sources (extract_*.py), the
// home/strings/status helpers, and the plugin source-text pins. The metadata checks against client libraries stay in native-bindings.
bool craftingOnly = args.Contains("--crafting-only");
bool gameplayRankOnly = args.Contains("--gameplay-rank-only");
foreach (string arg in args)
    if (arg != "--crafting-only" && arg != "--gameplay-rank-only") throw new ArgumentException("unknown argument: " + arg);
if (craftingOnly) { NativeCrafting.RunBatchTests(); NativeCrafting.RunHookTests(); return; }
if (gameplayRankOnly) { NativeGameplayRankBoundary.GameplayRankBindingTests.Run(); return; }
string root = RepoRoot.Find("plugin/NativeFormatLock.cs") ?? throw new InvalidOperationException("repository root not found (plugin/NativeFormatLock.cs)");
NativeGameplayRankBoundary.GameplayRankBindingTests.Run();
NativeMatchmakingBoundary.MatchmakingTests.Run();
NativeCrafting.RunBatchTests();
NativeCrafting.RunHookTests();
NativeStringsTests.Run();
NativeHomeTests.Run();
StatusRoutineTests.Run();
PluginSourcePins.Run(root);
Console.WriteLine("PASS native-bindings-logic");
