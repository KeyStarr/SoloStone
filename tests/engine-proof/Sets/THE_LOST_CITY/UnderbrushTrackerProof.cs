using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class UnderbrushTrackerProof
{
    internal static void Shuffle(Game g, Controller p, int count = 1)
    {
        new AddCardTo("CS2_231", EntityType.DECK, count).Process(g, p, p.Hero, null);
        g.DeathProcessingAndAuraUpdate();
    }

    internal static void Run() => StandardSetsProof.Case("TLC_520", (g, p, id) => {
        Core2026Proof.Check(Cards.FromId(id).ConsumesHistory, "declared history consumer");
        var held = (Minion)Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(held.Cost == 6 && held.IsRush && held.AttackDamage == 5 && held.Health == 5, "Tracker initial stats/Rush");
        Shuffle(g, p.Opponent, 3);
        Generic.ShuffleIntoDeck(p, p.Opponent.Hero, Entity.FromCard(p, Cards.FromId("CS2_231")));
        Generic.ShuffleIntoDeck(p.Opponent, p.Hero, Entity.FromCard(p.Opponent, Cards.FromId("CS2_231")));
        g.AuraUpdate(); Core2026Proof.Check(held.Cost == 6, "opponent actor or foreign destination does not count");
        var trade = Add(p, "CORE_EX1_002");
        Core2026Proof.Check(g.Process(TradeCardTask.Any(p, trade)), "Trade accepted");
        Core2026Proof.Check(held.Cost == 6, "Trade excluded");
        Shuffle(g, p, 3); Core2026Proof.Check(held.Cost == 5 && held[GameTag.COST] == 5, "three cards one operation and visible COST");
        End(g); End(g);
        var late = Add(p, id); g.AuraUpdate(); Core2026Proof.Check(late.Cost == 5, "lifetime facts before draw persist across turns");
        var clone = g.Clone(); Shuffle(clone, clone.Player1);
        Core2026Proof.Check(((IPlayable)clone.IdEntityDic[held.Id]).Cost == 4 && held.Cost == 5, "clone shuffle diverges without changing original");
        var enemy = Summon(p.Opponent, "CS2_182");
        PlayEntity(g, p, held); AttackOptionsProof.Lifecycle(g, held, enemy, rush: true);
        for (int i = 0; i < 7; i++) Shuffle(g, p);
        Core2026Proof.Check(late.Cost == 0 && late[GameTag.COST] == 0, "discount floors at zero");
    });
}
