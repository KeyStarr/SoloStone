using BepInEx;

// Exit code 42 is what the launcher reads as "the mod itself did not come up", so the update that carried this plugin is undone.
[BepInPlugin("local.hs.native.ui.probe.failing", "SoloStone live test failing plugin", "0.0.0")]
public sealed class FailingPlugin : BaseUnityPlugin
{
    void Awake() { UnityEngine.Application.Quit(42); }
}
