// Production rebuild/input/event configuration is extracted unchanged; native boundaries model deferred destruction and event gates.
static partial class ProfileMenuRuntimeHarness
{
    private sealed class GameObject
    {
        internal bool Destroyed, Active = true;
        internal int layer = 5;
        internal DeckRect Rect = new DeckRect(573, 170, 134, 32);
        internal readonly Transform transform = new();
        internal GameObject() { transform.gameObject = this; }
        internal GameObject gameObject => this;
        internal float Width = 4, Height = 1;
        internal readonly List<Component> Texts = new();
        internal readonly List<GameObject> Parts = new();
        internal Component[] GetComponentsInChildren(Type type, bool inactive) => Texts.ToArray();
        internal void SetActive(bool active) { if (Destroyed) throw new InvalidOperationException("Destroyed native button"); Active = active; }
    }
    private enum GameLayer { }
    private enum Anchor { TOP_RIGHT_XZ, TOP_LEFT_XZ }
    private static class TransformUtil
    {
        internal static void ConstrainToScreen(GameObject value, int layer)
        {
            if (value == null || value.layer != layer) throw new InvalidOperationException("Tooltip must exist on its owner layer before screen constraint");
            var rect = value.Rect;
            value.Rect = new DeckRect(Math.Clamp(rect.X, 0, 1280 - rect.Width), Math.Clamp(rect.Y, 0, 720 - rect.Height), rect.Width, rect.Height);
        }
    }
    private static class Time { internal static int frameCount; internal static float realtimeSinceStartup; }
    private static partial class NativeProfileSelector
    {
        internal static readonly List<string> TracePhases = new();
        internal static void TraceTooltip(string phase, PegUIElement row, TooltipZone zone) => TracePhases.Add(phase);
    }
    private static class ProfileCatalog { internal const string BacklogTooltip = "Not done yet! In the backlog"; }
    private sealed class TooltipZone
    {
        internal GameObject Clone;
        internal string Headline, Body;
        internal void ShowBoxTooltip(string headline, string body)
        {
            // Pinned TooltipPanel.Awake uses layer 14; this named overload does not apply a stored override.
            Clone ??= new GameObject { layer = 14, Rect = new DeckRect(600, 175, 81, 25) }; Headline = headline; Body = body;
        }
        internal GameObject GetTooltipObject() => Clone;
        internal void AnchorTooltipTo(GameObject target, Anchor targetAnchor, Anchor tooltipAnchor)
        {
            if (Clone == null || targetAnchor != Anchor.TOP_RIGHT_XZ || tooltipAnchor != Anchor.TOP_LEFT_XZ)
                throw new InvalidOperationException("Unexpected native tooltip anchoring contract");
            Clone.Rect = new DeckRect(target.Rect.X + target.Rect.Width, target.Rect.Y, Clone.Rect.Width, Clone.Rect.Height);
        }
        internal GameLayer LayerOverride { set { if (Clone != null) Clone.layer = (int)value; } }
        internal void HideTooltip() => Clone = null;
    }
    private enum UIEventType { PRESS, TAP, RELEASE, ROLLOVER, ROLLOUT }
    private class PegUIElement
    {
        private bool enabled = true;
        private readonly HashSet<UIEventType> disabledEvents = new();
        private readonly Dictionary<UIEventType, Action<object>> listeners = new();
        private readonly GameObject instance = new();
        internal readonly TooltipZone Tooltip = new();
        internal GameObject gameObject => !instance.Destroyed ? instance : throw new InvalidOperationException("Destroyed native button");
        internal Transform transform => gameObject.transform;
        internal readonly Component Label = new();
        internal void AddEventListener(UIEventType type, Action<object> action) => listeners[type] = action;
        internal void SetEnabled(bool value) => enabled = value;
        internal void SetEnabled(UIEventType type, bool value) { if (value) disabledEvents.Remove(type); else disabledEvents.Add(type); }
        internal bool IsEnabled() => enabled;
        internal bool IsEnabled(UIEventType type) => !disabledEvents.Contains(type);
        internal bool Trigger(UIEventType type)
        {
            if (!enabled || !IsEnabled(type)) return false;
            if (listeners.TryGetValue(type, out var listener)) listener(null);
            return true;
        }
    }
    private sealed class UIBButton : PegUIElement { }
    private static class UnityEngine
    {
        internal static class Object
        {
            private static readonly List<GameObject> pending = new();
            internal static void Destroy(Mesh value) { }
            internal static void Destroy(GameObject value) => pending.Add(value);
            internal static bool RetiredInactive => pending.Count > 0 && pending.All(value => !value.Active);
            internal static void EndFrame() { foreach (var value in pending) value.Destroyed = true; pending.Clear(); }
        }
    }
    private static class AccessTools
    {
        internal static System.Reflection.FieldInfo Field(Type type, string name) =>
            type.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        internal static System.Reflection.PropertyInfo Property(Type type, string name) =>
            type.GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
    }
    private sealed class TextInput { internal string Text = "Typed name"; internal string GetInputText() => Text; }
    private static class UniversalInputManager { internal static readonly TextInput Input = new(); internal static TextInput Get() => Input; }
    private static partial class ProfileFormKit { internal static bool Ready => false; }
    private class ButtonListMenu
    {
        // Pinned native CreateMenuButton retains each button; Show later dereferences every retained button.
        private readonly List<UIBButton> m_allButtons = new();
        private bool shown;
        protected readonly GameObject m_menu = new();
        internal int CachedCount => m_allButtons.Count;
        protected UIBButton CreateMenuButton() { var button = new UIBButton(); button.transform.parent = m_menu.transform; button.Label.transform.parent = button.transform; m_allButtons.Add(button); return button; }
        protected bool IsShown() => shown;
        protected void Hide() => shown = false;
        protected void Show()
        {
            m_menu.transform.localScale = new Vector3(1, 1, 1); // Pinned native Show resets scale before LayoutMenu.
            foreach (var button in m_allButtons) button.gameObject.SetActive(false);
            foreach (var button in m_allButtons) button.gameObject.SetActive(true); // Native LayoutMenuButtons shows current rows.
            shown = true;
            Layout();
        }
        protected virtual void Layout() { }
    }
    private sealed partial class NativeProfileMenu
    {
        private readonly List<UIBButton> rows = new();
        private readonly List<object> slices = new();
        private readonly List<GameObject> owned = new();
        private readonly List<Action> beforeLayout = new();
        private readonly Dictionary<PegUIElement, string> controls = new();
        private readonly HashSet<PegUIElement> selectedControls = new();
        private bool wizard, pacePage, startup = false, loading = false, ownsNativeDialog = true, editing;
        private int nextCount, commits;
        private string committed;
        private object nameText, nameColor;
        private readonly List<Action> afterLayout = new();
        private readonly List<GameObject> unsized = new();
        private readonly List<KeyValuePair<GameObject, GameObject>> fieldRows = new();
        private GameObject fieldsContent;
        private Action fieldsLayout;
        private readonly List<object> paceBounds = new();
        private readonly List<Mesh> paceMeshes = new();
        private bool layoutLogged;
        private int paceCheckFrame;
        private string lastPaceCheck;
        private UIBButton unitPlank;
        private GameObject cheatList;
        private int cheatRows;
        private float cheatPitch;
        private float unitWorld, unitScale, kitScale;
        private int tooltipTraceCount, tooltipTraceFrame;
        private PegUIElement tooltipTraceRow;
        private TooltipZone tooltipTraceZone;
        private readonly ClickLockout lockout = new();
        private string heading;
        private void Header(string value) => heading = value;
        private void Axes() { }
        private void FinishEdit(string value) { if (!editing) return; editing = false; commits++; committed = value; }
        private static TooltipZone ConfigureTooltip(PegUIElement button) => button.Tooltip;
        private static void FitTooltip(TooltipZone zone, int layer) { }
        // Production RaiseTooltip lifts the shown tooltip in front of the dialog; the stub records that it ran on the shown, constrained tip.
        internal readonly List<int> Raised = new();
        private void RaiseTooltip(TooltipZone zone, int layer) { if (zone.GetTooltipObject() != null) Raised.Add(layer); }
        private void BuildSlots(bool kit) => AddRows();
        private void BuildWizard(bool kit) => AddRows();
        private void BuildPace(bool kit) { AddRows(); AddPaceRows(); }
        private void AddRows()
        {
            for (int index = 0; index < nextCount; index++) {
                var button = CreateMenuButton(); rows.Add(button); owned.Add(button.gameObject); slices.Add(button.gameObject);
                controls[button] = "row"; selectedControls.Add(button);
            }
        }
        internal void Rebuild(bool editing, int count) { wizard = editing; nextCount = count; Build(); }
        internal void Open() => Show();
        internal void Close() => Hide();
        internal bool Shown => IsShown();
        internal bool Consistent => rows.Count == CachedCount && owned.Count == rows.Count && slices.Count == rows.Count
            && controls.Count == rows.Count && selectedControls.Count == rows.Count && heading == (wizard ? "Create save" : "Choose save");
        internal void CheckEditing(Action<bool, string> check)
        {
            editing = true; Rebuild(true, 4);
            check(!editing && commits == 1 && committed == UniversalInputManager.Input.Text,
                "rebuilding during a name edit commits the typed text first");
        }
        internal void CheckLockout(Action<bool, string> check)
        {
            Time.realtimeSinceStartup = 100; Rebuild(false, 2);
            check(lockout.Blocks(100) && lockout.Blocks(100 + ClickLockout.Seconds * .9f),
                "a production rebuild locks out the second click of a double-click");
            check(!lockout.Blocks(100 + ClickLockout.Seconds + .01f), "the click lockout expires");
        }
        internal void CheckHover(Action<bool, string> check)
        {
            var meta = rows[1]; selectedControls.Remove(meta); ConfigureMeta(meta);
            check(selectedControls.Contains(meta), "meta value reports its selected state");
            foreach (int layer in new[] { 5, 27 }) {
                meta.gameObject.layer = layer;
                check(meta.Trigger(UIEventType.ROLLOVER) && meta.Tooltip.Clone != null
                    && meta.Tooltip.Clone.layer == layer && meta.Tooltip.Headline == "Meta"
                    && meta.Tooltip.Body == ProfileCatalog.BacklogTooltip,
                    "production hover renders each new tooltip on its current native menu layer");
                var tipRect = meta.Tooltip.Clone.Rect;
                check(tipRect.X >= meta.gameObject.Rect.X + meta.gameObject.Rect.Width && tipRect.Y == meta.gameObject.Rect.Y
                    && tipRect.X + tipRect.Width <= 1280 && tipRect.Y + tipRect.Height <= 720,
                    "production hover anchors tooltip beside the row without overlap or viewport clipping");
                check(Raised.Count > 0 && Raised[^1] == layer, "production hover lifts the shown tooltip in front of the dialog");
                int traceCount = NativeProfileSelector.TracePhases.Count;
                SampleTooltipTrace();
                check(NativeProfileSelector.TracePhases.Count == traceCount, "tooltip diagnostic waits for the next frame");
                Time.frameCount++; SampleTooltipTrace(); SampleTooltipTrace();
                check(NativeProfileSelector.TracePhases.Count == traceCount + 1, "tooltip diagnostic samples only once on the next frame");
                check(meta.Trigger(UIEventType.ROLLOUT) && meta.Tooltip.Clone == null,
                    "production rollout destroys tooltip before next hover");
            }
            for (int hover = 0; hover < 3; hover++) {
                meta.Trigger(UIEventType.ROLLOVER); Time.frameCount++; SampleTooltipTrace();
                check(meta.Tooltip.Clone != null, "tooltip still shows after diagnostic budget is exhausted");
                meta.Trigger(UIEventType.ROLLOUT);
            }
            check(NativeProfileSelector.TracePhases.SequenceEqual(Enumerable.Range(0, 4).SelectMany(_ => new[] { "enter", "shown", "next-frame", "out" })),
                "production tooltip diagnostics are capped at four complete hover samples per popup");
            check(!meta.Trigger(UIEventType.PRESS) && !meta.Trigger(UIEventType.TAP) && !meta.Trigger(UIEventType.RELEASE),
                "production meta configuration blocks every activation event");
        }
    }
    internal static void Run(Action<bool, string> check)
    {
        var menu = new NativeProfileMenu();
        menu.Rebuild(false, 2); menu.Open();
        foreach (var state in new (bool Wizard, int Rows)[] { (true, 10), (true, 10), (true, 10), (false, 7), (false, 3), (true, 10) }) {
            menu.Rebuild(state.Wizard, state.Rows);
            check(menu.Shown && menu.Consistent, "production rebuild replaces native cache and every row registry");
            check(UnityEngine.Object.RetiredInactive, "retired production rows leave layout before deferred destruction");
            UnityEngine.Object.EndFrame();
            menu.Close(); menu.Open();
            check(menu.Shown && menu.Consistent, "production rebuild survives deferred destruction and native reopen");
        }
        menu.Close(); menu.Rebuild(false, 1);
        check(UnityEngine.Object.RetiredInactive, "hidden rebuild deactivates retired rows before reopen");
        UnityEngine.Object.EndFrame();
        check(!menu.Shown && menu.Consistent, "hidden production rebuild does not open menu");
        menu.Open();
        check(menu.Shown && menu.Consistent, "hidden rebuild can open after retired native rows are destroyed");
        menu.Rebuild(false, 3); menu.CheckHover(check); UnityEngine.Object.EndFrame();
        menu.CheckEditing(check); UnityEngine.Object.EndFrame();
        menu.CheckLockout(check); UnityEngine.Object.EndFrame();
        menu.CheckPaceFitting(check); UnityEngine.Object.EndFrame();
    }
}
