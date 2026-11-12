using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class ScarletBruiserProof
{
    internal static void Run() => StandardSetsProof.Case("JAIL_328", (g, p, id) => {
        var blocked = (Minion)Play(g, p, id); Kill(g, blocked);
        Core2026Proof.Check(p.HandZone.Count == 0, "neutral in current deck blocks deathrattle");
        ClassDeck(p);
        HandBoundaries(g, p, id, 1, death: true, discount: true);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.HandZone.Count == 0, "empty current deck qualifies but no Battlecry");
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[source.Id]));
        Generated(g, source, p.HandZone, 1, discount: true);
        var result = p.HandZone.Single(); var ordinary = Entity.FromCard(p, result.Card);
        Core2026Proof.Check(ordinary.Cost == ordinary.Card.Cost, "discount never mutates shared Card");
        Resume(g, next => Core2026Proof.Check(next.IdEntityDic[result.Id].Cost == Math.Max(0, result.Card.Cost - 2), "discount survives clone/snapshot"));
        EmptyHand(p); source = Summon(p, id); source.Silence(); Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 0, "Silence removes conditional deathrattle");
        Pool(g, id, 1, () => {
            var apprentice = Summon(p, "EX1_608"); source = Summon(p, id); Kill(g, source);
            var spell = p.HandZone.Single();
            Core2026Proof.Check(spell.Card.Id == "CORE_CS2_093" && spell.Cost == 0 && spell.Card.Cost == 3,
                "generated minus two stacks with spell-cost aura and floors at zero");
            apprentice.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(spell.Cost == 1 && spell.Card.Cost == 3, "removing aura preserves entity-only minus two");
        });
        EmptyHand(p);
        // Check at death, not play: put a neutral into an initially empty deck.
        source = (Minion)Play(g, p, id);
        p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 0, "deck condition evaluated at death");
    });
}
