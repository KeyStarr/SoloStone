using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Decisions;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class TaelanFordringProof
{
    internal static void Run() => StandardSetsProof.Case("CS3_024", (g, p, id) => {
        ClassDeck(p); var low = Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(low);
        var high = Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(high); Effects.SetCost(9).ApplyTo(high);
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_029")));
        var source = Summon(p, id); Core2026Proof.Check(source.HasTaunt && source.HasDivineShield, "Taunt and Divine Shield");
        Kill(g, source); Core2026Proof.Check(p.HandZone.Single().Id == high.Id, "highest effective deck minion drawn over spell"); Visible(high, 9);
        EmptyHand(p); ClassDeck(p);
        for (int i = 0; i < 3; i++) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        var tiedIds = p.DeckZone.Select(c => c.Id).ToArray();
        // Fresh streams 1, 2, 3 select indices 0, 1, 2 respectively, not always the first tie.
        g.SetRandomSeed(g.Seed);
        int expectedId = tiedIds[new[] { 0, 1, 2 }[(int)g.Seed - 1]];
        source = Summon(p, id); long draws = g.Random.DrawCount;
        Resume(g, other => {
            Kill(other, (Minion)other.IdEntityDic[source.Id]);
            Core2026Proof.Check(other.Random.DrawCount == draws + 1, "tie consumes exactly one game RNG draw");
            Core2026Proof.Check(other.Player1.HandZone.Single().Id == expectedId
                && other.Player1.DeckZone.Select(c => c.Id).SequenceEqual(tiedIds.Where(x => x != expectedId)),
                "known tied entity selected for seed " + g.Seed);
        });
        EmptyHand(p); FillHand(p); source = Summon(p, id); int deck = p.DeckZone.Count;
        Kill(g, source); Core2026Proof.Check(p.DeckZone.Count == deck - 1 && p.HandZone.Count == 10, "highest draw burns at full hand");
        EmptyHand(p); ClassDeck(p); FillHand(p, 5);
        var giant = Entity.FromCard(p, Cards.FromId("EX1_105")); p.DeckZone.Add(giant);
        var fixedCost = Entity.FromCard(p, Cards.FromId("CS2_182")); p.DeckZone.Add(fixedCost); Effects.SetCost(9).ApplyTo(fixedCost);
        Core2026Proof.Check(giant.Cost == 12, "hand-only adaptive cost inactive in deck");
        source = Summon(p, id); Kill(g, source);
        Core2026Proof.Check(giant.Zone == p.HandZone && fixedCost.Zone == p.DeckZone, "deck printed twelve beats modified nine despite held cards"); Visible(giant, 7);
        ClassDeck(p); source = Summon(p, id); int fatigue = p.Hero.Fatigue; Kill(g, source); Core2026Proof.Check(p.Hero.Fatigue == fatigue, "no minion no draw/fatigue");
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        source = Summon(p, id); source.Silence();
        Core2026Proof.Check(source.IsSilenced && !source.HasTaunt && !source.HasDivineShield && !source.HasDeathrattle,
            "Silence removes Taunt, Divine Shield and Deathrattle");
        var deckIds = p.DeckZone.Select(c => c.Id).ToArray(); var handIds = p.HandZone.Select(c => c.Id).ToArray();
        draws = g.Random.DrawCount;
        Resume(g, other => {
            Kill(other, (Minion)other.IdEntityDic[source.Id]);
            Core2026Proof.Check(other.Player1.DeckZone.Select(c => c.Id).SequenceEqual(deckIds)
                && other.Player1.HandZone.Select(c => c.Id).SequenceEqual(handIds) && other.Random.DrawCount == draws,
                "silenced death neither draws nor consumes RNG");
        });
    });
}
