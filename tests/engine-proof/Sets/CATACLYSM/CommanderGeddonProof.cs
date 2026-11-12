using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static DiscoverRealProof;

internal static class CommanderGeddonProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_591", Unpinned_PersistentNormalDraw);
    private static void Unpinned_PersistentNormalDraw(Game original, Controller ignored, string id)
    {
        foreach (bool full in new[] { false, true })
        {
            var g = New(original.Seed); var p = g.Player1;
            var source = (Minion)Play(g, p, id); Kill(g, source);
            Play(g, p, id);
            Assert(p.AppliedEnchantments.Count(e => e.Card.Id == "CATA_591e") == 1, "repeated Battlecries refresh rather than stack replacement");
            Deck(p, "CS2_182", "CS2_200", "CS2_231", "CS2_029", "CS2_172");
            var explicitDraw = Generic.Draw(p);
            Assert(p.Choice == null && explicitDraw.Zone == p.HandZone, "explicit draws remain ordinary");
            if (full) PoolPilotProof.FillHand(p);
            End(g); End(g);
            var offers = Offers(g); int bound = Bound(offers[0]); var rejected = offers.Skip(1).Select(Bound).ToArray();
            Assert(offers.Length == 3 && p.DeckZone.Count == 4, "normal draw replaced by one suspended deck Discover");
            int cost = g.IdEntityDic[bound].Cost;
            Saved(g, next => {
                Answer(next, offers[0].Id); var owner = next.Player1; var chosen = next.IdEntityDic[bound];
                Assert((full ? chosen.Zone == owner.GraveyardZone : chosen.Zone == owner.HandZone) && (full || chosen.Cost == Math.Max(0, cost - 3)),
                    "chosen real draw receives discount, or burns normally at full hand");
                Assert(rejected.All(id => next.IdEntityDic[id].Zone == owner.GraveyardZone) && owner.DeckZone.Count == 1,
                    "only rejected bound offers destroyed");
                Deck(owner); int damage = owner.Hero.Damage;
                End(next); End(next);
                Assert(owner.Choice == null && owner.Hero.Damage == damage + 1, "PINNED empty-deck normal draw still fatigues");
            });
        }
    }
}
