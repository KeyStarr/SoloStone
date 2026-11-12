using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class ZakajzTheCorruptorProof
{
    internal static void Run() => StandardSetsProof.Case("BE_728", (g, p, id) => {
        ClassDeck(p);
        var minion = Add(p, "BE_870"); var spell = Add(p, "BE_802"); var weapon = Add(p, "TLC_833");
        var enemy = Add(p.Opponent, "BE_802");
        var deckMinion = Entity.FromCard(p, Cards.FromId("BE_870")); p.DeckZone.Add(deckMinion);
        var deckSpell = Entity.FromCard(p, Cards.FromId("BE_802")); p.DeckZone.Add(deckSpell);
        var order = p.DeckZone.Select(c => c.Id).ToArray();
        var source = (Minion)Play(g, p, id);
        Visible(minion, 2); Visible(spell, 3); Visible(weapon, 3); Visible(enemy, 2);
        Core2026Proof.Check(deckMinion.Cost == 2 && deckSpell.Cost == 3 && order.SequenceEqual(p.DeckZone.Select(c => c.Id)) && source.Cost == 4, "only current hand/deck minions/spells; no reorder/self discount");
        var future = Add(p, "BE_870"); Visible(future, 3);
        var copy = Generic.Copy(p.Opponent, source, minion, Zone.HAND); Visible(copy, 2);
        var backward = Generic.Copy(p, source, minion, Zone.DECK); Visible(backward, 3);
        Effects.AddCost(1).ApplyTo(minion); Visible(minion, 3); Visible(copy, 2);
        source.Silence(); Kill(g, source); Visible(spell, 3);
        Resume(g, next => {
            var held = next.IdEntityDic[spell.Id]; Effects.AddCost(1).ApplyTo(held); Visible(held, 4);
            var drawn = Generic.Draw(next.Player1, next.IdEntityDic[deckMinion.Id]); Visible(drawn, 2);
        });
        var drawnSpell = Generic.Draw(p, deckSpell); Visible(drawnSpell, 3);
        var zero = Add(p, "BE_870"); Effects.SetCost(0).ApplyTo(zero);
        var zeroSpell = Add(p, "BE_802"); Effects.SetCost(0).ApplyTo(zeroSpell);
        Play(g, p, id); Play(g, p, id); Visible(zero, 0); Visible(zeroSpell, 2);
        var negativeCopy = Generic.Copy(p.Opponent, p.Hero, zero, Zone.HAND);
        Effects.AddCost(2).ApplyTo(negativeCopy); Visible(negativeCopy, 0);
        Effects.AddCost(1).ApplyTo(negativeCopy); Visible(negativeCopy, 1); Visible(zero, 0);
        Effects.AddCost(2).ApplyTo(zero); Visible(zero, 0);
        Effects.AddCost(1).ApplyTo(zero); Visible(zero, 1);
    });
}
