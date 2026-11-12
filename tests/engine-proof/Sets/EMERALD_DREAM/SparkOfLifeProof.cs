using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PoolPilotProof;
using static ChooseOneProof;
using static SymbiosisProof;

internal static class SparkOfLifeProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_872", (g, p, id) => {
        KnownMetaProofFixture.UseBlackEmpire(g);
        OptionsSurvive(g, p, id, "EDR_872A", "EDR_872B");
        var pools = OfflinePoolDeclarations.Cards.Single(c => c.CardId == id).Pools;
        Core2026Proof.Check(pools.Count == 2 && pools.All(x => x.Filter.Type == CardType.SPELL && x.Filter.ClassMode == PoolClassMode.Exact), "two exact-class spell pools");
        foreach (int branch in new[] { 1, 2 })
        foreach (bool full in new[] { false, true })
        {
            EmptyHand(p);
            if (full && !p.BoardZone.IsFull) { LocationProofs.Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182"); }
            var twin = g.CloneExact(true);
            PlayOption(g, p, id, branch); PlayOption(twin, twin.Player1, id, branch);
            var filter = pools[branch - 1].Filter;
            Core2026Proof.Check(filter.ExactClass == (branch == 1 ? CardClass.MAGE : CardClass.DRUID), "exact branch class");
            var offers = Offers(g, id, filter, 3);
            Core2026Proof.Check(Vector(g) == Vector(twin) && g.Random.CaptureState().Equals(twin.Random.CaptureState()), "branch deterministic offers/RNG");
            if (full) FillHand(p);
            int slots = p.BoardZone.SlotCount;
            Resume(g, next => {
                var owner = next.Player1; var chosen = next.IdEntityDic[offers[1].Id]; Answer(next, chosen.Id);
                var granted = Granted(chosen, full);
                Core2026Proof.Check(owner.Choice == null && owner.HandZone.Count == (full ? 10 : granted.Length)
                    && owner.BoardZone.SlotCount == slots, "single branch grants one or burns, no summon");
            });
        }
        StandardSetsProof.ParkBoard(p); EmptyHand(p); Both(p);
        PlayOption(g, p, id, -1);
        var mage = Offers(g, id, pools[0].Filter, 3);
        Core2026Proof.Check(p.Choice.NextChoice != null, "Choose Both queues both Discovers in printed order");
        Resume(g, next => Answer(next, mage[0].Id));
        var druid = Offers(g, id, pools[1].Filter, 3);
        Resume(g, next => Answer(next, druid[0].Id));
        var grantedBoth = Granted(mage[0]).Concat(Granted(druid[0])).ToArray();
        Core2026Proof.Check(p.Choice == null && p.HandZone.Select(x => x.Id).OrderBy(x => x)
            .SequenceEqual(grantedBoth.Select(x => x.Id).OrderBy(x => x)), "Choose Both grants exactly both selected spells or their Shatter halves");
    });

    private static IPlayable[] Granted(IPlayable chosen, bool full = false)
    {
        var p = chosen.Controller;
        if (full)
        {
            Core2026Proof.Check(chosen.Zone == p.GraveyardZone, "full hand burns selected spell");
            return Array.Empty<IPlayable>();
        }
        if (chosen.Card.Shatter is { } family && chosen[ShatterRules.AlreadyCombined] == 0)
        {
            var halves = p.HandZone.Where(x => x[GameTag.DISPLAYED_CREATOR] == chosen.Id).ToArray();
            Core2026Proof.Check(chosen[GameTag.ZONE] == (int)Zone.REMOVEDFROMGAME && halves.Length == 2
                && halves.Count(x => x.Card.Id == family.FirstHalfId) == 1
                && halves.Count(x => x.Card.Id == family.SecondHalfId) == 1, "selected Shatter spell grants its exact complementary halves");
            return halves;
        }
        Core2026Proof.Check(chosen.Zone == p.HandZone, "selected spell enters hand");
        return new[] { chosen };
    }
}
