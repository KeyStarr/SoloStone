using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static DiscoverRealProof;

internal static class HellraiserProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_734", Unpinned_RealDeckDraw);
    private static void Unpinned_RealDeckDraw(Game g, Controller p, string id)
    {
        var deck = Deck(p, "CS2_182", "CS2_182");
        deck[0].Cost = 2; deck[1].Cost = 5;
        foreach (var minion in deck.Cast<Minion>()) { minion.AttackDamage = 9; minion.BaseHealth = 10; minion.HasTaunt = true; }
        var source = (Minion)Play(g, p, id);
        Assert(source.HasTaunt && Offers(g).Length == 1, "pinned Taunt and one distinct deck offer");
        int proxy = Offers(g)[0].Id, bound = Bound(Offers(g)[0]), cost = g.IdEntityDic[bound].Cost;
        Assert(Offers(g)[0].Cost == cost, "real deck offer displays bound entity's modified Cost");
        Assert(Offers(g)[0] is Minion shown && shown.AttackDamage == 9 && shown.BaseHealth == 10 && shown.HasTaunt,
            "real offer displays persistent stats and keywords");
        Saved(g, next => {
            Answer(next, proxy);
            Assert(next.IdEntityDic[bound].Zone == next.Player1.HandZone && next.IdEntityDic[bound].Cost == cost,
                "Unpinned deck entity retains its persistent cost");
            Assert(next.IdEntityDic[bound] is Minion actual && actual.AttackDamage == 9 && actual.BaseHealth == 10 && actual.HasTaunt,
                "chosen original retains deck stats and keywords");
        });
        Deck(p);
        var empty = (Minion)Play(g, p, id);
        Assert(empty.AttackDamage == 6 && empty.Health == 6 && p.Choice == null, "empty deck gains +4/+4 without fatigue or prompt");
        empty.Silence();
        Assert(empty.AttackDamage == 2 && empty.Health == 2 && !empty.HasTaunt, "buff and printed Taunt removable by Silence");
    }
}
