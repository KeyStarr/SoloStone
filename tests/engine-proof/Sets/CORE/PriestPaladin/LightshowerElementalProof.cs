using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LightshowerElementalProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BAR_310", (g, p, id) => {
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.HasTaunt && source.IsRace(Race.ELEMENTAL), "printed Taunt Elemental");
        var friend = Summon(p, "CS2_182"); friend.BaseHealth = 20; friend.Damage = 12;
        var healthy = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 3;
        p.Hero.Damage = 12; p.Opponent.Hero.Damage = 12;
        Kill(g, source);
        Core2026Proof.Check(friend.Damage == 4 && p.Hero.Damage == 4 && healthy.Damage == 0, "death heals all friends by eight, capped at maximum");
        Core2026Proof.Check(enemy.Damage == 3 && p.Opponent.Hero.Damage == 12, "opposing characters unchanged");
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(friend.Damage == 0 && p.Hero.Damage == 0, "healing capped at missing Health");
        source = (Minion)Play(g, p, id); source.Silence(); p.Hero.Damage = 10; Kill(g, source);
        Core2026Proof.Check(p.Hero.Damage == 10, "Silence removes deathrattle");
    });
}
