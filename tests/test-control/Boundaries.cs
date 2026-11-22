// Only the board trace's native boundaries are doubled; no client libraries are loaded.
namespace UnityEngine
{
    internal static class Time { internal static int frameCount; internal static float realtimeSinceStartup; }
    internal struct Vector3 { internal float x, y, z; }
    internal class Transform
    {
        internal Transform parent;
        internal Vector3 localPosition;
        internal Vector3 position
        {
            get { var p = parent?.position ?? default; return new Vector3 { x = p.x + localPosition.x, y = p.y + localPosition.y, z = p.z + localPosition.z }; }
        }
        // Translation-only double; production uses Unity's inverse (including rotation and scale).
        internal Vector3 InverseTransformPoint(Vector3 point) => new() { x = point.x - position.x, y = point.y - position.y, z = point.z - position.z };
    }
}
internal enum GAME_TAG { ZONE_POSITION }
internal sealed class Actor
{
    internal UnityEngine.Transform transform = new();
    internal bool Shown = true;
    internal bool IsShown() => Shown;
}
internal sealed class Entity
{
    internal int Id, Position;
    internal string CardId = "TEST_CARD";
    internal int GetEntityId() => Id;
    internal string GetCardId() => CardId;
    internal int GetTag(GAME_TAG _) => Position;
}
internal sealed class Card
{
    internal UnityEngine.Transform transform = new();
    internal bool Shown = true;
    internal bool IsShown() => Shown;
    internal Entity Entity = new();
    internal Actor Actor = new();
    internal Entity GetEntity() => Entity;
    internal Actor GetActor() => Actor;
}
internal sealed class Zone
{
    internal UnityEngine.Transform transform = new();
    internal List<Card> Cards = new();
    internal bool Fail;
    internal List<Card> GetCards() => Fail ? throw new InvalidOperationException("sample failure") : Cards;
}
internal sealed class Player(int id)
{
    internal Zone Zone = new();
    internal int GetPlayerId() => id;
    internal Zone GetBattlefieldZone() => Zone;
}
internal sealed class GameState
{
    internal static GameState Current;
    internal Player Friendly = new(1), Enemy = new(2);
    internal static GameState Get() => Current;
    internal Player GetFriendlySidePlayer() => Friendly;
    internal Player GetOpposingSidePlayer() => Enemy;
}
internal sealed partial class NativeTestControl
{
    internal static void AssertMainThread() { }
}
// Unrelated protocol verbs cannot use these boundaries in this suite.
internal static class TestControlPacks { internal const int MaxCount = int.MaxValue; }
internal static class UiRules
{
    internal const int MaxPressWaitMs = int.MaxValue;
    internal static string CollectionCardId(string _) => throw new NotSupportedException();
    internal static void RequireElementId(string _) => throw new NotSupportedException();
    internal static string DragBlocker(string _, string __) => throw new NotSupportedException();
}
