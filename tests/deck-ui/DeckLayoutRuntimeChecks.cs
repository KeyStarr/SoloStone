// Production mulligan placement selection, with native objects represented by identities.
static partial class DeckLayoutRuntimeHarness
{
    private sealed class GameObject { internal bool activeInHierarchy = true; }
    private sealed class Actor { internal object Face = new(); internal object GetMeshRenderer() => Face; }
    private sealed class Card { internal GameObject gameObject = new(); internal Actor Actor = new(); internal Actor GetActor() => Actor; }
    private sealed class Widget { internal GameObject gameObject = new(); }
    private sealed class Manager
    {
        internal GameObject Confirm = new(), Banner = new();
        internal Widget mulliganButtonWidget = new();
        internal GameObject GetMulliganButton() => Confirm;
        internal GameObject GetMulliganBanner() => Banner;
    }
    private sealed class Placement
    {
        internal GameObject Root; internal object Face; internal DeckRect Target;
        internal Placement(GameObject root, DeckRect target, object face = null) { Root=root; Target=target; Face=face; }
    }
    private static class Screen { internal const int width=1280, height=800; }
    private static readonly List<Placement> placements = new();
    private static readonly List<Card> cards = new() { new(), new(), new() };
    private static readonly Manager mulligan = new();
    internal static void Run(Action<bool,string> check)
    {
        mulligan.mulliganButtonWidget.gameObject.activeInHierarchy=false;
        Bind();
        check(!placements.Any(p=>ReferenceEquals(p.Root,mulligan.Banner)), "native animated banner is not transformed by Deck layout");
        check(placements.Count==4 && cards.All(c=>placements.Any(p=>ReferenceEquals(p.Root,c.gameObject) && ReferenceEquals(p.Face,c.Actor.Face))),
            "three card face placements and native Confirm remain owned");
        check(placements.Single(p=>ReferenceEquals(p.Root,mulligan.Confirm)).Target.Height>=110, "Deck Confirm receives readable height budget");
        placements.Clear(); mulligan.Confirm.activeInHierarchy=false; mulligan.mulliganButtonWidget.gameObject.activeInHierarchy=true;
        Bind();
        check(placements.Count==4 && placements.Any(p=>ReferenceEquals(p.Root,mulligan.mulliganButtonWidget.gameObject)),
            "alternate native Confirm widget uses same layout without banner ownership");
    }
}
