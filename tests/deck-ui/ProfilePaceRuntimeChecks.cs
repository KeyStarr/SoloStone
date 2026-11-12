// Exercise production row placement after native Show resets menu scale; only native rendering is modeled.
static partial class ProfileMenuRuntimeHarness
{
    private struct Vector3(float x, float y, float z)
    {
        internal float x = x, y = y, z = z;
        internal float sqrMagnitude => x * x + y * y + z * z;
        internal float magnitude => MathF.Sqrt(x * x + y * y + z * z);
        internal static float Dot(Vector3 a, Vector3 b) => a.x * b.x + a.y * b.y + a.z * b.z;
        public static Vector3 operator +(Vector3 a, Vector3 b) => new(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float b) => new(a.x * b, a.y * b, a.z * b);
        public static Vector3 operator /(Vector3 a, float b) => new(a.x / b, a.y / b, a.z / b);
    }
    private sealed class Transform
    {
        internal Transform parent;
        internal GameObject gameObject;
        internal bool Visible = true;
        internal Vector3 localScale = new(1, 1, 1);
        private Vector3 localPosition;
        internal Vector3 position {
            get => parent == null ? localPosition : parent.position + localPosition * parent.lossyScale.x;
            set => localPosition = parent == null ? value : (value - parent.position) / parent.lossyScale.x;
        }
        internal Vector3 lossyScale => localScale * (parent?.lossyScale.x ?? 1);
    }
    private struct Bounds(Vector3 min, Vector3 max)
    {
        internal Vector3 min = min, max = max;
        internal Vector3 size => max - min;
        internal void Encapsulate(Bounds b) {
            min = new Vector3(Math.Min(min.x, b.min.x), Math.Min(min.y, b.min.y), Math.Min(min.z, b.min.z));
            max = new Vector3(Math.Max(max.x, b.max.x), Math.Max(max.y, b.max.y), Math.Max(max.z, b.max.z));
        }
    }
    private sealed class Renderer
    {
        internal bool enabled = true;
        internal Transform transform = new();
        internal Bounds bounds;
    }
    private struct Rect
    {
        internal static Rect MinMaxRect(float x0, float y0, float x1, float y1) => new();
    }
    private static class Screen { internal const int width = 3840, height = 2160; }
    private static class CameraUtils { internal static Camera FindFirstByLayerMask(int mask) => new(); }
    private sealed class Camera { internal Vector3 WorldToScreenPoint(Vector3 point) => point; }
    private sealed class Component
    {
        internal readonly Transform transform = new();
        internal readonly List<Renderer> Renderers = new();
        internal bool MissingGlyphs;
        internal T[] GetComponentsInChildren<T>(bool inactive) => Renderers.Cast<T>().ToArray();
        internal string Text = "Pace — a long collection label";
        internal float FrameWidth = 4, FrameHeight = 1, GlyphWidth = 10;
    }
    private sealed class CheckBox : PegUIElement { }
    private sealed class Mesh { }
    private sealed class GrindTier
    {
        internal string Id = "retail", Name = "Retail";
        internal double HoursPerMetaDeck = 19;
        internal int HoursToHalfCollection = 532;
    }
    private static partial class NativeProfileSelector
    {
        internal static readonly List<string> Logs = new();
        internal static void Log(string value) => Logs.Add(value);
    }
    private static partial class ProfileFormKit
    {
        internal static readonly Type TextType = typeof(Component);
        internal static Component Label(PegUIElement row) => row.Label;
        internal static void UpdateNow(Component text) { }
        internal static void SetText(Component text, string value) => text.Text = value;
        internal static void ScaleRect(Component text, string dimension, float scale)
        {
            if (dimension == "Width") text.FrameWidth *= scale; else text.FrameHeight *= scale;
        }
        internal static bool Span(GameObject value, Vector3 axis, out float min, out float max)
        {
            if (value.Parts.Count > 0)
            {
                min = float.MaxValue; max = float.MinValue;
                foreach (var child in value.Parts)
                    if (Span(child, axis, out float a, out float b)) { min = Math.Min(min, a); max = Math.Max(max, b); }
                return min <= max;
            }
            float center = Vector3.Dot(value.transform.position, axis);
            float half = (axis.x == 1 ? value.Width * value.transform.lossyScale.x : value.Height * value.transform.lossyScale.y) / 2;
            min = center - half; max = center + half;
            return true;
        }
        internal static void SizeOnPlane(GameObject item, Vector3 right, Vector3 up, float width, float height)
        {
            var scale = item.transform.localScale;
            scale.x *= width / (item.Width * item.transform.lossyScale.x);
            scale.y *= height / (item.Height * item.transform.lossyScale.y);
            item.transform.localScale = scale;
        }
        internal static void Center(GameObject value, Vector3 right, Vector3 up, float x, float y)
        {
            Span(value, right, out float x0, out float x1); Span(value, up, out float y0, out float y1);
            value.transform.position += right * (x - (x0 + x1) / 2) + up * (y - (y0 + y1) / 2);
        }
        private static bool ActiveUnder(Transform node, Transform root) => node.Visible;
        internal static Bounds? RectBounds(Component text) => TextBounds(text, text.FrameWidth, text.FrameHeight);
        internal static Bounds? GlyphBounds(Component text) => text.MissingGlyphs ? null : TextBounds(text, text.Text.Contains('\n') ? text.GlyphWidth * .6f : text.GlyphWidth,
            text.Text.Contains('\n') ? 1 : .5f);
        private static Bounds TextBounds(Component text, float width, float height) =>
            new(text.transform.position, text.transform.position + new Vector3(width, height, 0) * text.transform.lossyScale.x);
    }
    private sealed partial class NativeProfileMenu
    {
        private readonly Vector3 right = new(1, 0, 0), up = new(0, 1, 0);
        private PegUIElement paceRow;
        private Component paceName, paceStats, paceGlyph;
        private bool testKit;
        private float statsWidth = 11, rowPitch;
        private void SizeSlice(GameObject wrapper, Mesh mesh, float width, float height) { rowPitch = height; wrapper.Width = width; wrapper.Height = height; }
        private int paceSamples;
        private void CheckPace(Camera camera) => paceSamples++;
        protected override void Layout() { foreach (var hook in beforeLayout) hook(); foreach (var hook in afterLayout) hook(); }
        private void AddPaceRows()
        {
            // Same capture as PaceRow: dimensions stored in wrapper-local units before Show resets menu scale.
            float capturedScale = m_menu.transform.lossyScale.x;
            float localUnit = (capturedScale * 1f) / capturedScale;
            float localWidth = (capturedScale * 7.24f) / capturedScale;
            var content = new GameObject(); content.transform.parent = m_menu.transform;
            paceRow = testKit ? new CheckBox() : rows[1];
            paceRow.transform.parent = content.transform;
            content.Parts.Add(paceRow.gameObject);
            paceRow.gameObject.Width = testKit ? .9f : 4;
            paceRow.gameObject.Height = testKit ? .5f : 1;
            paceName = new Component { Text = "Dad-gamer", GlyphWidth = 2 };
            paceName.transform.localScale = new Vector3(.32f, .32f, .32f);
            paceStats = new Component { Text = "one meta deck ~2.6 h · half collection ~31 h", GlyphWidth = statsWidth };
            paceStats.transform.localScale = new Vector3(.24f, .24f, .24f);
            paceGlyph = testKit ? null : new Component { Text = "☑", GlyphWidth = .5f };
            foreach (var text in new[] { paceName, paceStats, paceGlyph }) if (text != null) {
                text.transform.parent = content.transform; content.Texts.Add(text);
            }
            var control = paceRow;
            AddPaceLayout(content, new Mesh(), localUnit, localWidth, paceName, paceStats, control, paceGlyph, testKit);
            afterLayout.Add(() => CenterPaceRows(new List<GameObject> { content }));
        }
        private void CheckPaceDiagnostics(Action<bool, string> check)
        {
            var text = new Component { MissingGlyphs = true };
            var drawn = new Renderer { bounds = new Bounds(new Vector3(10, 20, 0), new Vector3(40, 25, 0)) };
            text.Renderers.Add(drawn);
            var bounds = ProfileFormKit.RenderedBounds(text).Value;
            check(bounds.min.x == 10 && bounds.max.x == 40 && bounds.min.y == 20 && bounds.max.y == 25,
                "production text measurement uses visible renderer when UberText glyph cache is empty");
            drawn.enabled = false;
            bounds = ProfileFormKit.RenderedBounds(text).Value;
            check(bounds.max.x - bounds.min.x == text.FrameWidth, "missing rendered glyphs fall back to text rect");
            text.MissingGlyphs = false;
            bounds = ProfileFormKit.RenderedBounds(text).Value;
            check(bounds.max.x - bounds.min.x == text.GlyphWidth, "rendered text bounds take priority over layout rect");

            var first = new Component { GlyphWidth = 3 };
            var second = new Component { GlyphWidth = 2 };
            float pitch = ArrangePaceNote(first, second, new Vector3(5, 0, 0), 1, 7);
            var a = ProfileFormKit.RenderedBounds(first).Value; var b = ProfileFormKit.RenderedBounds(second).Value;
            check(Math.Abs((a.min.x + a.max.x) / 2 - 5) < .001f && Math.Abs((b.min.x + b.max.x) / 2 - 5) < .001f,
                "production note centers both differently sized lines on the panel");
            check(b.max.y < a.min.y && a.max.y - b.min.y < pitch, "note lines stay disjoint inside their compact slice");

            var group = new GameObject();
            var wrappers = new[] { new GameObject { Height = .4f }, new GameObject { Height = .5f } };
            var content = wrappers.Select(wrapper => { wrapper.transform.parent = group.transform;
                var body = new GameObject(); body.transform.parent = wrapper.transform; return body; }).ToList();
            StackPaceRows(content, group, new Mesh());
            check(Math.Abs(wrappers[0].transform.position.y - wrappers[1].transform.position.y - .45f) < .001f
                && Math.Abs(group.Height - .9f) < .001f, "production stack adds no retail spacing between reserved row blocks");

            paceSamples = 0; paceCheckFrame = Time.frameCount + 3;
            SamplePaceCheck(); Time.frameCount += 2; SamplePaceCheck();
            check(paceSamples == 0, "pace diagnostic waits for layout and text mesh settling");
            paceCheckFrame = Time.frameCount + 3; // A later native layout postpones the pending sample.
            Time.frameCount++; SamplePaceCheck();
            check(paceSamples == 0, "later layout postpones pace diagnostic");
            Time.frameCount += 2; SamplePaceCheck(); SamplePaceCheck();
            check(paceSamples == 1, "settled pace diagnostic samples once");
        }
        internal void CheckPaceFitting(Action<bool, string> check)
        {
            pacePage = true;
            CheckPaceDiagnostics(check);
            foreach (bool kit in new[] { true, false })
            foreach (float oldScale in new[] { 1f, 40f, .25f })
            foreach (float textWidth in new[] { 3f, 11f })
            {
                statsWidth = textWidth;
                testKit = kit;
                m_menu.transform.localScale = new Vector3(oldScale, oldScale, oldScale);
                Rebuild(false, 2);
                check(Shown && m_menu.transform.lossyScale.x == 1, "pace rebuild resets the previously fitted menu scale");
                var n = ProfileFormKit.GlyphBounds(paceName).Value;
                var s = ProfileFormKit.GlyphBounds(paceStats).Value;
                float blockHeight = n.max.y - s.min.y;
                float textHeight = n.max.y - n.min.y + s.max.y - s.min.y;
                check(rowPitch > blockHeight && rowPitch <= textHeight * 1.6f,
                    "production row pitch reserves drawn name/stats block plus a small gap");
                Bounds c;
                if (kit)
                {
                    ProfileFormKit.Span(paceRow.gameObject, right, out float x0, out float x1);
                    ProfileFormKit.Span(paceRow.gameObject, up, out float y0, out float y1);
                    c = new Bounds(new Vector3(x0, y0, 0), new Vector3(x1, y1, 0));
                }
                else c = ProfileFormKit.GlyphBounds(paceGlyph).Value;
                if (kit) check(Math.Abs((Math.Min(c.min.x, Math.Min(n.min.x, s.min.x))
                    + Math.Max(c.max.x, Math.Max(n.max.x, s.max.x))) / 2) < .001f,
                    "production drawn checkbox/text group is centred for both short and long labels");
                check(c.max.x < n.min.x && c.max.x < s.min.x, "production row places checkbox strictly left of both text lines");
                foreach (var b in new[] { c, n, s })
                    check(b.min.x >= -7.24f / 2 && b.max.x <= 7.24f / 2 && b.min.y >= -rowPitch / 2 && b.max.y <= rowPitch / 2,
                        "production checkbox and both text lines fit reserved row slice");
                if (!kit)
                {
                    ProfileFormKit.Span(paceRow.gameObject, up, out float p0, out float p1);
                    check(p1 - p0 <= rowPitch, "fallback plank art stays inside the reserved two-line height");
                }
                check(s.max.y < n.min.y, "production tier and stats lines never overlap");
                check(paceName.transform.localScale.x > 0 && paceStats.transform.localScale.x > 0 && paceRow.transform.localScale.x > 0,
                    "production row scales stay positive across native scale resets");
                UnityEngine.Object.EndFrame();
            }
        }
    }
}
