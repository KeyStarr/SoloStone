using System.Reflection;
#pragma warning disable CS0649, CS0414 // Native DTO/reflection fields intentionally mirror client boundaries.

namespace BepInEx.Logging { public class ManualLogSource { public void LogInfo(object value) { } public void LogWarning(object value) { } } }
namespace HarmonyLib
{
    public static class AccessTools
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance;
        public static Type TypeByName(string name) => typeof(AccessTools).Assembly.GetTypes().SingleOrDefault(t => t.Name == name);
        public static FieldInfo Field(Type type, string name) => type.GetField(name, Flags);
        public static PropertyInfo Property(Type type, string name) => type.GetProperty(name, Flags);
        public static MethodInfo Method(Type type, string name) => type.GetMethod(name, Flags);
    }
}
namespace UnityEngine
{
    // Unity semantics: a destroyed object compares equal to null (the step detects scene-change destruction this way).
    public class Object
    {
        public static void Destroy(GameObject target) { target.Destroyed = true; }
        internal virtual bool Gone => false;
        static bool Null(Object value) => ReferenceEquals(value, null) || value.Gone;
        public static bool operator ==(Object a, Object b) => Null(a) || Null(b) ? Null(a) && Null(b) : ReferenceEquals(a, b);
        public static bool operator !=(Object a, Object b) => !(a == b);
        public override bool Equals(object other) => other is Object value && this == value;
        public override int GetHashCode() => 0;
    }
    public class Component : Object
    {
        public GameObject gameObject;
        internal override bool Gone => !ReferenceEquals(gameObject, null) && gameObject.Gone;
        public Transform transform => gameObject.transform;
        public T[] GetComponentsInChildren<T>(bool inactive = false) where T : Component => gameObject.GetComponentsInChildren<T>(inactive);
        public Component[] GetComponentsInChildren(Type type, bool inactive = false) => gameObject.All().SelectMany(g => g.Components).Where(type.IsInstanceOfType).ToArray();
        public Component GetComponentInParent(Type type)
        {
            for (var node = transform; node != null; node = node.parent)
            {
                var found = node.gameObject.Components.FirstOrDefault(type.IsInstanceOfType);
                if (found != null) return found;
            }
            return null;
        }
    }
    public class MonoBehaviour : Component { public bool isActiveAndEnabled => gameObject.activeSelf; }
    public class GameObject : Object
    {
        public string name; public int layer = 19; public bool activeSelf = true, Destroyed;
        public bool activeInHierarchy => activeSelf && (transform.parent == null || transform.parent.gameObject.activeInHierarchy);
        public readonly List<Component> Components = new(); public readonly List<GameObject> Children = new();
        public Transform transform;
        // Destroying a GameObject destroys its children too.
        internal override bool Gone => Destroyed || (!ReferenceEquals(transform.parent, null) && transform.parent.gameObject.Gone);
        public GameObject(string name) { this.name = name; transform = new Transform { gameObject = this }; }
        public void SetActive(bool value) => activeSelf = value;
        public T AddComponent<T>() where T : Component, new() { var result = new T { gameObject = this }; Components.Add(result); return result; }
        public GameObject Child(string name) { var result = new GameObject(name); result.transform.parent = transform; Children.Add(result); return result; }
        public IEnumerable<GameObject> All() => new[] { this }.Concat(Children.SelectMany(g => g.All()));
        public T[] GetComponentsInChildren<T>(bool inactive = false) where T : Component => All().SelectMany(g => g.Components).OfType<T>().ToArray();
    }
    // Translation-only transforms: enough to check centring arithmetic in the display's local space.
    public class Transform : Component
    {
        public Transform parent; public Vector3 localPosition;
        public string name => gameObject.name;
        public Vector3 position => parent == null ? localPosition : parent.position + localPosition;
        public Transform Find(string name) => gameObject.Children.FirstOrDefault(g => g.name == name)?.transform;
        public bool IsChildOf(Transform root) { for (var node = this; node != null; node = node.parent) if (node == root) return true; return false; }
        public Vector3 InverseTransformPoint(Vector3 point) => point - position;
        public Vector3 InverseTransformVector(Vector3 vector) => vector;
        public Vector3 TransformVector(Vector3 vector) => vector;
    }
    public class Renderer : Component
    {
        public bool forceRenderingOff, enabled = true; public Vector3 Size = new(1, 1, 1);
        public Bounds bounds => new(transform.position, Size);
    }
    public struct Bounds
    {
        public Vector3 center, size;
        public Bounds(Vector3 center, Vector3 size) { this.center = center; this.size = size; }
        public void Encapsulate(Bounds other)
        {
            float min = Math.Min(center.x - size.x / 2, other.center.x - other.size.x / 2), max = Math.Max(center.x + size.x / 2, other.center.x + other.size.x / 2);
            center = new Vector3((min + max) / 2, center.y, center.z); size = new Vector3(max - min, size.y, size.z);
        }
    }
    public class Camera : Component { }
    public static class Time { public static float realtimeSinceStartup; }
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 zero => new();
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static bool operator ==(Vector3 a, Vector3 b) => Math.Abs(a.x - b.x) < 1e-5f && Math.Abs(a.y - b.y) < 1e-5f && Math.Abs(a.z - b.z) < 1e-5f;
        public static bool operator !=(Vector3 a, Vector3 b) => !(a == b);
        public override bool Equals(object other) => other is Vector3 v && this == v;
        public override int GetHashCode() => 0;
    }
    public struct Quaternion { public static Quaternion identity => new(); }
    public struct Matrix4x4 { public static Matrix4x4 TRS(Vector3 v, Quaternion q, Vector3 scale) => new(); }
    public struct Color { public Color(float r, float g, float b) { } }
    public struct Rect { public Rect(float x, float y, float w, float h) { } }
    public class GUIContent { public GUIContent(string text) { } public static GUIContent none => new(""); }
    public class GUIStyleState { public Color textColor; }
    public class GUIStyle { public GUIStyle(GUIStyle other) { } public GUIStyle() { } public int fontSize; public bool wordWrap; public GUIStyleState normal = new(); public float CalcHeight(GUIContent c, int width) => 10; }
    public class GUISkin { public GUIStyle label = new(); }
    public static class GUI { public static Matrix4x4 matrix; public static GUISkin skin = new(); public static void Box(Rect r, GUIContent c) { } public static void Label(Rect r, string t, GUIStyle s) { } }
    public static class Screen { public static int width = 1280, height = 720; }
}
namespace PegasusShared { public enum FormatType { FT_WILD = 1, FT_STANDARD = 2 } public enum TAG_RARITY { EPIC } public enum TAG_PREMIUM { NORMAL } }
namespace Hearthstone.UI
{
    public class WidgetInstance : UnityEngine.MonoBehaviour
    {
        public static WidgetInstance Last; public RankedRewardDisplay Display;
        public static WidgetInstance Create(string prefab)
        {
            Last = new UnityEngine.GameObject(prefab).AddComponent<WidgetInstance>();
            Last.Display = Last.gameObject.Child("RankedRewardDisplay").AddComponent<RankedRewardDisplay>();
            Last.Display.m_debugClickCatcher = Last.Display.gameObject.Child("ClickCatcher").AddComponent<PegUIElement>();
            return Last;
        }
        public void RegisterReadyListener(Action<object> callback) => callback(null);
        public T GetComponentInChildren<T>() where T : UnityEngine.Component => GetComponentsInChildren<T>(true).Single();
    }
}
public class UberText : UnityEngine.Component { public string Text { get; set; } }
public class RankedMedal : UnityEngine.Component { }
public class ParticleSystemRenderer : UnityEngine.Renderer { }
public class ScreenEffectsHandle { public void StopEffect() { } }
public class EndGameScreen { public PegUIElement m_hitbox; public static EndGameScreen Get() => null; }
public enum UIEventType { RELEASE }
public class UIEvent { }
public class PegUIElement : UnityEngine.Component
{
    Action<UIEvent> released;
    public void AddEventListener(UIEventType type, Action<UIEvent> callback) => released += callback;
    public void Release() { if (PegUI.Get().Registered(CameraUtils.PopupCamera)) released?.Invoke(new UIEvent()); }
}
public class PegUI
{
    public static PegUI Instance = new(); readonly System.Collections.IList m_UICams = new List<UnityEngine.Camera>();
    public static PegUI Get() => Instance;
    public void AddInputCamera(UnityEngine.Camera camera) => m_UICams.Add(camera);
    public void RemoveInputCamera(UnityEngine.Camera camera) => m_UICams.Remove(camera);
    public bool Registered(UnityEngine.Camera camera) => m_UICams.Contains(camera);
}
public static class CameraUtils { public static UnityEngine.Camera PopupCamera = new UnityEngine.GameObject("PopupCamera").AddComponent<UnityEngine.Camera>(); public static UnityEngine.Camera FindFirstByLayerMask(int mask) => PopupCamera; }
public class TranslatedMedalInfo { public bool IsValid() => true; }
public class RankMgr { public static RankMgr Get() => new(); public RankMgr GetLocalPlayerMedalInfo() => this; public TranslatedMedalInfo GetCurrentMedal(PegasusShared.FormatType format) => new(); }
public class RewardData { }
public class BoosterPackRewardData : RewardData { public BoosterPackRewardData(int id, int count) { } }
public class CardBackRewardData : RewardData { public CardBackRewardData(int id) { } }
public class RandomCardRewardData : RewardData { public RandomCardRewardData(PegasusShared.TAG_RARITY r, PegasusShared.TAG_PREMIUM p, int count) { } }
public class RankedRewardDisplay : UnityEngine.MonoBehaviour
{
    public bool m_isAnimating, m_isHidePending;
    public PegUIElement m_debugClickCatcher; int m_numAnimationsRemaining = 1; ScreenEffectsHandle m_screenEffectsHandle = new();
    Action closed;
    public void Initialize(TranslatedMedalInfo medal, List<List<RewardData>> groups, Action callback) => closed = callback;
    public void Show() { } // Native Show is asynchronous; tests deliver OnShown separately.
    void ActivateDebugEquivalentsOfEndGameScreen() => m_debugClickCatcher.AddEventListener(UIEventType.RELEASE, _ => closed());
}
public class DialogBase { public void Hide() { } }
public class AlertPopup
{
    public enum Response { CANCEL } public enum ResponseDisplay { CANCEL }
    public static AlertPopup.PopupInfo Shown;
    public class PopupInfo
    {
        public string m_id, m_headerText, m_text, m_cancelText; public bool m_showAlertIcon, m_keyboardEscIsCancel;
        public ResponseDisplay m_responseDisplay; public Action<Response, object> m_responseCallback;
    }
}
// Null by default (no dialog service); tests install one to see the REWARDS SAVED fallback.
public class DialogManager
{
    public static DialogManager Current; public static DialogManager Get() => Current;
    public bool ShowUniquePopup(AlertPopup.PopupInfo info, Func<DialogBase, object, bool> process)
    { if (!process(new DialogBase(), null)) return false; AlertPopup.Shown = info; return true; }
}
internal interface ILocalTierRewardStep { bool AwaitingPlayer { get; } void Present(LocalResultsReceipt receipt, Action done); void Retire(); }
internal class LocalTierReward { }
internal class LocalResultsReceipt { public string MatchId; public IReadOnlyList<LocalTierReward> TierRewards; }
internal enum LocalTierRewardTileKind { Pack, CardBack, RandomCard }
internal class LocalTierRewardTile { public LocalTierRewardTileKind Kind; public int Id, Count; public string Rarity; }
internal class LocalTierRewardPlan
{
    public static LocalTierRewardPlan Next;
    public string Header = "Bronze 5 reached", Title = "Bronze 5", Subtitle; public List<string> Captions = new();
    public List<LocalTierRewardTile> Tiles = new() { new() { Kind = LocalTierRewardTileKind.Pack, Id = 713, Count = 2 } };
    public static IReadOnlyList<LocalTierRewardPlan> Build(IReadOnlyList<LocalTierReward> r, Func<string, string> recipe, Func<int, bool> back, Func<string, string> opponent) => new[] { Next ?? new LocalTierRewardPlan() };
}
internal class LocalProfile { public object Pending; public bool PackRevealPending; }
internal class LocalTierRewardCatchUp
{
    public static bool LastIdle;
    public void Update(LocalProfile p, bool idle, Action<LocalResultsReceipt, Action> present, Action<Exception> error) { LastIdle = idle; }
}
internal static class NativeUIProbe { public static LocalProfile RewardProfile; public static bool RewardContextReady, GameplayRewardContextReady; }
internal static class NativeResultsUI { public static ILocalTierRewardStep TierRewardStep; public static bool CanStart, BlocksEscape; public static void PresentTierRewardsOnly(LocalResultsReceipt r, Action done) { } }
internal static class NativeRewardUI { public static bool Active; }
internal static class NativeAchievementToast { public static bool Active; }
internal static class ClientBridge { public static bool OwnsLocalPostMatch; }
internal class LocalRecipe { public string Id, Name; }
internal class LocalShop { public static LocalShop Default = new(); public List<LocalRecipe> Recipes = new(); }
internal class LocalDeck { public string Id, Opponent; }
internal static class LocalDecks { public static LocalDeck[] All = Array.Empty<LocalDeck>(), TestLoaners = All, TestOpponents = All; }
internal class CardBackDb { public object GetRecord(int id) => new(); }
internal static class GameDbf { public static CardBackDb CardBack = new(); }
internal static class LocalRewardPresentation { public static string TierRewardText(IReadOnlyList<LocalTierReward> r) => "Rewards"; }

// NPC World is not linked here: no world installed, so challenger ids fall back to deck names (legacy behaviour).
internal static class NpcWorldRuntime { internal static NpcWorldDouble World => null; }
internal sealed class NpcWorldDouble { internal NpcDouble Find(string id) => null; }
internal sealed class NpcDouble { internal string DisplayName = null; }
internal static class NativeWelcomeUI { internal static bool ScrollActive => false; }
