using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using BepInEx.Logging;
using MiniJSON;
using UnityEngine;

// Options > SoloStone Options > "FPS Counter" checkbox (NativeOptionsPanels builds the row and hands it to Attach) and the overlay it
// shows: one IMGUI label in the top-right corner (FpsCounterLayout), directly under the always-on brand label (NativeBrandLabel), drawn
// from the plugin's OnGUI so it covers every scene, in a match too. It takes no input (a Label never does) and only reads
// Time.unscaledDeltaTime: no frame-rate, VSync or quality setting is touched. Off, Tick is one bool test and Draw returns at the top.
// The setting applies at once and persists in the BepInEx config ([Display] FpsCounter).
// Also home to the Options overlap helpers (Overlaps, Body) the Options planks are placed and checked with (NativeOptionsPanels).
// Every path fails closed: nothing throws into Options or the frame.
internal static class NativeFpsCounter
{
    private static FpsCounterSettings settings;
    private static ManualLogSource log;
    private static bool enabled, failed;
    private static readonly FpsAverager average = new FpsAverager();
    internal static CheckBox Checkbox { get; private set; }
    // The checkbox while its panel shows it.
    internal static CheckBox Shown => NativeOptionsPanels.OnScreen(Checkbox) ? Checkbox : null;
    internal static bool Enabled => enabled;
    internal static bool Checked => Checkbox != null && Checkbox.IsChecked();

    private static GUIStyle text, shadow;
    private static int styleSize;
    private static readonly GUIContent content = new GUIContent();
    private static bool labelReady;
    private static int labelFps = -1;
    private static Vector2 extent;
    private static FpsRect drawn;
    private static int drawnFrame = -100, drawnWidth, drawnHeight;

    internal static void Initialize(ManualLogSource logger, ConfigFile file)
    {
        settings = new FpsCounterSettings(file); log = logger; enabled = settings.Enabled;
    }

    // The panel's row (NativeOptionsPanels), built once; it stays for the session.
    internal static void Attach(CheckBox box)
    {
        if (settings == null) throw new InvalidOperationException("FPS Counter not initialized");
        box.AddEventListener(UIEventType.RELEASE, _ => {
            try { settings.Set(box.IsChecked()); log.LogInfo("FPS_COUNTER_OPTION enabled=" + settings.Enabled + " applies=now"); }
            catch (Exception error) { log.LogWarning("FPS_COUNTER_OPTION not saved: " + error.Message); }
            Apply(box);
        });
        Checkbox = box;
        Apply(box);
    }

    private static void Apply(CheckBox box)
    {
        bool now = settings.Enabled;
        if (now != enabled) average.Reset();
        enabled = now;
        if (box != null) box.SetChecked(now);
    }

    internal static void Sync() { try { if (Checkbox != null) Apply(Checkbox); } catch (Exception error) { log?.LogWarning("FPS_COUNTER_OPTION sync: " + error.Message); } }

    // Called from OfflineProbe.Update, once per frame.
    internal static void Tick()
    {
        if (enabled) average.Add(Time.unscaledDeltaTime);
    }

    // Every other visible Options row in `rows` the control overlaps in rendered bounds (a tenth of each row's height is slack: native
    // rows' bounds include their own glow), plus "outsideBody" when it is not inside the dialog body: the rendered union of the Options
    // meshes (frame included, the full-screen blocker and `skip` not), inset by the frame's thickness.
    internal static List<string> Overlaps(OptionsMenu options, Component box, List<KeyValuePair<string, Component>> rows, out bool measured, params Component[] skip)
    {
        var names = new List<string>(); measured = false;
        Camera view = CameraUtils.FindFirstByLayerMask(1 << box.gameObject.layer);
        if (view == null) return names;
        Rect mine = DeckLayout.ScreenRect(box.gameObject, view);
        if (mine.height < 12) return names;
        measured = true;
        DeckRect own = Core(mine);
        foreach (var row in rows)
        {
            Component other = row.Value;
            if (other == null || other.gameObject == box.gameObject || !other.gameObject.activeInHierarchy) continue;
            Rect rect = DeckLayout.ScreenRect(other.gameObject, view);
            if (rect.width > 0 && OptionsPaceLayout.Overlap(own, Core(rect))) names.Add(row.Key);
        }
        var excluded = new List<Component>(skip ?? new Component[0]) { box };
        if (!Body(options, view, out Rect body, excluded.ToArray()) || mine.xMin < body.xMin || mine.xMax > body.xMax || mine.yMin < body.yMin || mine.yMax > body.yMax) names.Add("outsideBody");
        return names;
    }

    internal static bool Body(OptionsMenu options, Camera view, out Rect body, params Component[] skip)
    {
        body = Rect.zero; bool any = false;
        float screen = Screen.width * (float)Screen.height;
        foreach (Renderer renderer in options.GetComponentsInChildren<Renderer>(false))
        {
            if (!renderer.enabled || skip.Any(part => part != null && renderer.transform.IsChildOf(part.transform))) continue;
            Rect one = DeckLayout.Project(renderer.bounds, view);
            if (one.width * one.height > .9f * screen) continue;
            body = any ? Rect.MinMaxRect(Mathf.Min(body.xMin, one.xMin), Mathf.Min(body.yMin, one.yMin), Mathf.Max(body.xMax, one.xMax), Mathf.Max(body.yMax, one.yMax)) : one;
            any = true;
        }
        if (!any) return false;
        float dx = body.width * .04f, dy = body.height * .07f;
        body = Rect.MinMaxRect(body.xMin + dx, body.yMin + dy, body.xMax - dx, body.yMax - dy);
        return true;
    }

    private static DeckRect Core(Rect rect) => new DeckRect(rect.x, rect.y + rect.height * .1f, rect.width, rect.height * .8f);

    // Called from OfflineProbe.OnGUI. Repaint only: Layout and input events never draw.
    internal static void Draw()
    {
        if (!enabled || failed || Event.current.type != EventType.Repaint) return;
        try
        {
            int size = FpsCounterLayout.FontSize(Screen.height);
            if (text == null || styleSize != size)
            {
                text = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = false, clipping = TextClipping.Overflow };
                text.padding = new RectOffset(0, 0, 0, 0);
                text.normal.textColor = new Color(1f, 1f, 1f, .92f);
                shadow = new GUIStyle(text);
                shadow.normal.textColor = new Color(0f, 0f, 0f, .85f);
                styleSize = size; extent = Vector2.zero;
            }
            // The string and the native text layout change only when the reading does (twice a second), not on every repaint.
            if (average.Ready != labelReady || average.Fps != labelFps || extent.x <= 0)
            {
                labelReady = average.Ready; labelFps = average.Fps;
                content.text = NativeFpsCounterText.Overlay(labelReady, labelFps);
                extent = text.CalcSize(content);
            }
            FpsRect place = FpsCounterLayout.PlaceFps(Screen.width, Screen.height, NativeBrandLabel.PlacedThisFrame, extent.x, extent.y);
            var area = new Rect(place.X, place.Y, place.Width, place.Height);
            float offset = Mathf.Max(1f, size / 14f);
            GUI.Label(new Rect(area.x + offset, area.y + offset, area.width, area.height), content, shadow);
            GUI.Label(area, content, text);
            drawn = place; drawnFrame = Time.frameCount; drawnWidth = Screen.width; drawnHeight = Screen.height;
        }
        catch (Exception error)
        {
            failed = true; // One warning, not one per frame; the setting stays what the player chose.
            log?.LogWarning("FPS_COUNTER overlay disabled for the session: " + error.Message);
        }
    }

#if !SOLOSTONE_RELEASE
    // Test control (state.fpsCounter): the setting; `drawn`: a repaint in the last frames issued the label's draw call (the call
    // returned: not proof of pixels, so look at a screenshot) and where (normalized 1280x720, top-left origin, like every ui
    // rect); and the checkbox in its panel (NativeOptionsPanels.RowState: `checked`, rect, every row it overlaps or leaves).
    internal static JsonNode State()
    {
        int width = Screen.width, height = Screen.height;
        bool issued = enabled && !failed && Time.frameCount - drawnFrame <= 2 && drawnWidth == width && drawnHeight == height
            && FpsCounterLayout.OnScreen(drawn, width, height);
        var node = new JsonNode {
            { "enabled", enabled }, { "failed", failed }, { "drawn", issued }, { "fps", enabled && average.Ready ? average.Fps : 0 },
            { "screen", TestControlProtocol.List(new object[] { (long)width, (long)height }) }
        };
        node["screenRect"] = !issued ? null : TestControlProtocol.List(FpsCounterLayout.Normalized(drawn, width, height).Select(value => (object)Math.Round((double)value, 1)));
        node["checkbox"] = NativeOptionsPanels.RowState(Checkbox);
        return node;
    }
#endif
}
