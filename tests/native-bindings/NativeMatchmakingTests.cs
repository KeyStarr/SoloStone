using System.Reflection;

namespace NativeMatchmakingBoundary;

internal static class MatchmakingTests
{
    private static Type controller;
    private static object Call(string name, params object[] args) => controller.GetMethod(name,
        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, args);
    private static bool Visible => (bool)controller.GetProperty("Visible", BindingFlags.Static | BindingFlags.Public).GetValue(null);
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("matchmaking: " + message);
    }
    internal static void Run()
    {
        controller = typeof(MatchmakingTests).Assembly.GetType("NativeMatchmakingBoundary.NativeMatchmaking");
        Check(controller != null, "native matchmaking controller missing");
        var log = new ManualLogSource();
        Call("Initialize", new Harmony(), log);
        Check(Harmony.Patches.Count == 5, "register timing, readiness and both cancel guards");
        // Regression (staging-1): the offline boot never runs GameMgr.OnLoggedIn, so retail's TOURNAMENT preload
        // listener is missing and nothing loads MatchingPopup3D. The controller must preload it at picker load.
        Check(GameMgr.Get().GetTransitionPopup() == null && GameMgr.Get().LoadCalls == 0, "offline boot starts with no popup");
        Call("Update");
        Check(SceneMgr.Get().PreLoadListeners == 1, "scene preload listener registered once SceneMgr exists");
        Call("Update");
        Check(SceneMgr.Get().PreLoadListeners == 1, "preload listener registered only once");
        SceneMgr.Get().FirePreLoad(SceneMgr.Mode.HUB);
        Check(GameMgr.Get().LoadCalls == 0, "no preload outside the picker");
        SceneMgr.Get().FirePreLoad(SceneMgr.Mode.TOURNAMENT);
        Check(GameMgr.Get().LoadCalls == 1 && GameMgr.Get().GetTransitionPopup() is MatchingPopupDisplay,
            "picker load preloads the native matchmaking prefab before any queue");
        SceneMgr.Get().FirePreLoad(SceneMgr.Mode.TOURNAMENT);
        Check(GameMgr.Get().LoadCalls == 1, "existing preloaded popup is reused");
        Time.realtimeSinceStartup = 10;
        Call("Begin");
        var popup = (MatchingPopupDisplay)GameMgr.Get().GetTransitionPopup();
        Check(GameMgr.Get().LoadCalls == 1, "Begin shows the popup preloaded at picker load");
        var cancel = popup.m_cancelButton;
        Check(Visible && popup.Shows == 1, "show native prefab once, suppress fallback overlay");
        // Retail disabled look: the button object stays drawn, flipped face down onto its charcoal back plate, no input.
        Check(cancel.gameObject.activeSelf && !cancel.IsEnabled() && cancel.FaceDown,
            "Cancel stays active but disabled and face down (retail plate), never hidden");
        // A stale hidden/enabled/face-up button from an earlier use is restored to the retail disabled pose on Begin.
        object[] enable = { popup, true };
        Check(!(bool)Call("AllowCancel", enable) && !(bool)enable[1], "animation cannot re-enable Cancel");
        object[] back = { popup, false };
        Check(!(bool)Call("BlockBack", back) && (bool)back[1], "Escape consumed without native cancel/queue transport");
        Check(!(bool)Call("AllowCancelClick", popup), "queued Cancel release suppressed");
        var unrelated = new MatchingPopupDisplay();
        Check((bool)Call("AllowCancelClick", unrelated) && (bool)Call("AllowHide", unrelated),
            "other popups retain native behavior");
        Time.realtimeSinceStartup = 13.9f;
        Call("Update");
        Check(popup.IsShown() && !(bool)Call("AllowHide", popup), "minimum hold and not-ready guard");
        // Fast load: native Death/reveal starts immediately; don't shorten its 3.5-second animation.
        Time.realtimeSinceStartup = 10.1f;
        Call("GameplayLoaded", popup);
        Time.realtimeSinceStartup = 13.9f;
        Check(!(bool)Call("AllowHide", popup), "native timer cannot close before four seconds total");
        Call("Update");
        Check(popup.IsShown(), "fast load still covered before minimum");
        Time.realtimeSinceStartup = 14;
        Call("Update");
        Check(!popup.IsShown(), "fast load opens match at four seconds");
        popup.gameObject.SetActive(false); // Native fade/deactivation completes.
        Call("Update");
        Check(!Visible && cancel.gameObject.activeSelf, "release ownership, Cancel plate never hidden");

        cancel.gameObject.SetActive(false); cancel.SetEnabled(true); cancel.Flip(true, true);
        Time.realtimeSinceStartup = 20;
        Call("Begin");
        Check(cancel.gameObject.activeSelf && !cancel.IsEnabled() && cancel.FaceDown, "Begin restores the disabled face-down plate");
        Time.realtimeSinceStartup = 29;
        Call("Update");
        Check(popup.IsShown(), "slow scene remains covered past minimum");
        Call("GameplayLoaded", popup);
        Time.realtimeSinceStartup = 32.4f;
        Call("Update");
        Check(popup.IsShown(), "slow load retains full native reveal");
        Time.realtimeSinceStartup = 32.5f;
        Call("Update");
        Check(!popup.IsShown(), "slow load opens after readiness plus native reveal");
        popup.gameObject.SetActive(false);
        Call("Update");

        Time.realtimeSinceStartup = 40;
        Call("Begin");
        Time.realtimeSinceStartup = 85;
        Call("Update");
        Check(!Visible && popup.Destroyed && log.Warnings.Any(line => line.Contains("timed out")),
            "watchdog releases native blocker and logs fallback without cancelling pending match");
        Check(GameMgr.Get().QueueCalls == 0, "presentation never calls network queue or cancellation");
        GameMgr.Get().FailShow = true;
        Call("Begin");
        Check(!Visible && log.Warnings.Any(line => line.Contains("fallback")), "prefab failure keeps old overlay");
        GameMgr.Get().FailShow = false;
        // Popup gone (destroyed by the fallback above) and no scene event since: Begin loads it just in time.
        Check(GameMgr.Get().GetTransitionPopup() == null, "fallback destroyed the popup");
        int loads = GameMgr.Get().LoadCalls;
        Time.realtimeSinceStartup = 100;
        Call("Begin");
        Check(Visible && GameMgr.Get().LoadCalls == loads + 1 && ((MatchingPopupDisplay)GameMgr.Get().GetTransitionPopup()).Shows == 1,
            "missed preload loads the prefab before Show");
        Call("Fallback", "test reset");
        GameMgr.Get().FailLoad = true;
        int shows = GameMgr.Get().ShowCalls;
        Call("Begin");
        Check(!Visible && GameMgr.Get().ShowCalls == shows && log.Warnings.Any(line => line.Contains("preload failed")),
            "prefab that cannot load uses fallback without calling Show");
        GameMgr.Get().FailLoad = false;
        Harmony.FailPatch = true;
        Call("Initialize", new Harmony(), log);
        Call("Begin");
        Check(!Visible, "missing hooks use fallback rather than unsafe native Cancel");
        Harmony.FailPatch = false;
        Console.WriteLine("PASS native matchmaking: hold/readiness/reveal, reuse, cancellation guards, watchdog and load/hook fallback");
    }
}

// Unity and Harmony cannot execute inside this process. Keep only those boundaries fake.
internal sealed class Harmony
{
    internal static readonly List<MethodInfo> Patches = new();
    internal static bool FailPatch;
    internal void Patch(MethodInfo original, HarmonyMethod prefix = null, HarmonyMethod postfix = null)
    {
        if (FailPatch) throw new MissingMethodException("pinned hook unavailable");
        Patches.Add(original);
    }
}
internal sealed class HarmonyMethod { internal HarmonyMethod(Type type, string name) { } }
internal static class AccessTools
{
    internal static MethodInfo Method(Type type, string name, Type[] args = null) => args == null
        ? type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
        : type.GetMethod(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static, null, args, null);
}
internal sealed class ManualLogSource
{
    internal readonly List<string> Warnings = new();
    internal void LogInfo(string value) { }
    internal void LogWarning(string value) => Warnings.Add(value);
}
internal static class Time { internal static float realtimeSinceStartup; }
internal sealed class GameObject
{
    internal bool activeSelf;
    internal Action OnDestroy;
    internal void SetActive(bool active) => activeSelf = active;
}
internal static class UnityEngine
{
    internal static class Object { internal static void Destroy(GameObject value) => value.OnDestroy?.Invoke(); }
}
internal sealed class UIBButton
{
    public readonly GameObject gameObject = new() { activeSelf = true };
    private bool enabled = true;
    public bool FaceDown;
    public void SetEnabled(bool value, bool isInternal = false) => enabled = value;
    public bool IsEnabled() => enabled;
    public void Flip(bool faceUp, bool forceImmediate = false) => FaceDown = !faceUp;
}
internal class TransitionPopup
{
    public readonly UIBButton m_cancelButton = new();
    public readonly GameObject gameObject = new();
    public bool Shown;
    public bool IsShown() => Shown;
    protected virtual bool EnableCancelButtonIfPossible() => true;
}
internal class MatchingPopupDisplay : TransitionPopup
{
    public int Shows;
    public bool Destroyed;
    public MatchingPopupDisplay() { gameObject.OnDestroy = () => { Destroyed = true; Shown = false; }; }
    public void Show() { Shows++; Shown = true; gameObject.SetActive(true); }
    public void Hide() => Shown = false;
    private bool OnNavigateBack() => false;
    protected void OnCancelButtonReleased(UIEvent e) { }
    protected void OnGameplaySceneLoaded() { }
}
internal sealed class UIEvent { }
internal sealed class GameMgr
{
    private const string Prefab = "MatchingPopup3D.prefab:4f4a40d14d907e94da1b81d97c18a44f";
    private static readonly GameMgr instance = new();
    // Offline: GameMgr.OnLoggedIn never ran, so nothing is preloaded.
    private MatchingPopupDisplay popup;
    public bool FailShow, FailLoad;
    public int QueueCalls, ShowCalls, LoadCalls;
    public static GameMgr Get() => instance;
    // Retail OnHandlePopupDestroyed clears the field.
    public TransitionPopup GetTransitionPopup() => popup == null || popup.Destroyed ? null : popup;
    private void LoadTransitionPopup(string prefab)
    {
        LoadCalls++;
        if (prefab != Prefab) throw new Exception("incorrect native popup asset");
        popup = FailLoad ? null : new MatchingPopupDisplay();
    }
    private void ShowTransitionPopup(string prefab, int scenario)
    {
        ShowCalls++;
        if (prefab != Prefab || scenario != 0)
            throw new Exception("incorrect native popup asset/scenario");
        if (FailShow) throw new Exception("prefab unavailable");
        if (GetTransitionPopup() == null) LoadTransitionPopup(prefab);
        popup.Show();
    }
    public void FindGame() => QueueCalls++;
    public void CancelFindGame() => QueueCalls++;
}
internal sealed class SceneMgr
{
    internal enum Mode { HUB, TOURNAMENT, GAMEPLAY }
    internal delegate void ScenePreLoadCallback(Mode prevMode, Mode mode, object userData);
    private static readonly SceneMgr instance = new();
    private readonly List<ScenePreLoadCallback> listeners = new();
    private Mode mode = Mode.HUB;
    internal int PreLoadListeners => listeners.Count;
    public static bool IsInitialized() => true;
    public static SceneMgr Get() => instance;
    public Mode GetMode() => mode;
    public void RegisterScenePreLoadEvent(ScenePreLoadCallback callback) => listeners.Add(callback);
    internal void FirePreLoad(Mode next)
    {
        Mode previous = mode;
        mode = next;
        foreach (var listener in listeners.ToArray()) listener(previous, next, null);
    }
}
