using SabberStoneCore.Model.Zones;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static CostSliceProof;
using static PoolPilotProof;

internal static class MeadowstriderProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_978", (g, p, id) => {

        var order = p.DeckZone.Select(x => x.Id).ToArray();
        var dead = Summon(p, id); dead.AttackDamage = 20; dead.BaseHealth = 20;
        Effects.SetCost(0).ApplyTo(dead); Kill(g, dead);
        var bottom = p.DeckZone[0];
        Core2026Proof.Check(bottom.Card.Id == id && bottom.Id != dead.Id && bottom.Cost == 1
            && ((Minion)bottom).AttackDamage == 6 && ((Minion)bottom).Health == 11 && ((Minion)bottom).HasTaunt,
            "fresh 6/11 Taunt at bottom with SET one");
        Core2026Proof.Check(p.DeckZone.Skip(1).Select(x => x.Id).SequenceEqual(order), "existing deck order preserved");
        foreach (var restored in new[] { g.CloneExact(true), Game.LoadSnapshot(g.SaveSnapshot()) }) {
            var drawn = Generic.Draw(restored.Player1, restored.IdEntityDic[bottom.Id]); Visible(drawn, 1);
            Core2026Proof.Check(bottom.Zone == p.DeckZone, "restored deck cost independent");
        }
        var top = Generic.Draw(p); Core2026Proof.Check(top.Id == order.Last(), "ordinary draw takes top, not new bottom");
        var hand = Generic.Draw(p, bottom); Visible(hand, 1);
        var quiet = Summon(p, id); quiet.Silence(); int count = p.DeckZone.Count; Kill(g, quiet);
        Core2026Proof.Check(p.DeckZone.Count == count, "silenced Deathrattle absent");
        while (!p.DeckZone.IsFull) p.DeckZone.Add(Entity.FromCard(p, Cards.FromId("CS2_182")));
        Kill(g, Summon(p, id)); Core2026Proof.Check(p.DeckZone.Count == DeckZone.DeckMaximumCapcity, "full deck cannot overflow");
    });
}
