// Stand-ins for the few UnityEngine types that plugin/LocalCollectionUI.cs names. That file mixes the collection and deck
// draft logic with IMGUI drawing; the tests run the logic and never the drawing, so every draw call here does nothing.
// Written from what the plugin calls, nothing more.
namespace UnityEngine
{
    internal struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
    }

    internal struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a = 1f) { this.r = r; this.g = g; this.b = b; this.a = a; }
    }

    internal struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float width, float height) { this.x = x; this.y = y; this.width = width; this.height = height; }
        public bool Contains(Vector2 point) => point.x >= x && point.x < x + width && point.y >= y && point.y < y + height;
    }

    internal class Texture2D { public static readonly Texture2D whiteTexture = new Texture2D(); }

    internal class GUIStyleState { public Color textColor; }

    internal class GUIStyle
    {
        public int fontSize;
        public bool wordWrap;
        public GUIStyleState normal = new GUIStyleState();
        public GUIStyle() { }
        public GUIStyle(GUIStyle other) { fontSize = other.fontSize; wordWrap = other.wordWrap; }
    }

    internal class GUISkin { public GUIStyle textField = new GUIStyle(); }

    internal class Event
    {
        public static Event current => null;   // no IMGUI event is ever being processed
        public Vector2 mousePosition;
    }

    internal static class Screen { public static int width, height; }

    internal static class Application { public static void Quit() { } }

    internal static class GUI
    {
        public static GUISkin skin = new GUISkin();
        public static bool enabled = true;
        public static Color color;
        public static void Label(Rect rect, string text, GUIStyle style) { }
        public static bool Button(Rect rect, string text, GUIStyle style) => false;
        public static string TextField(Rect rect, string text) => text;
        public static void DrawTexture(Rect rect, Texture2D texture) { }
    }
}
