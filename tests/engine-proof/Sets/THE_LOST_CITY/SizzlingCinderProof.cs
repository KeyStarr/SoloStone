using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class SizzlingCinderProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_249", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && p.Hero.Damage == 0, "no enemy minions sends both pings to enemy hero");
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "EX1_506a"); var other = Summon(p.Opponent, "CS2_182");
        Summon(p, "CS2_222"); int before = p.Opponent.Hero.Damage;
        source = (Minion)Play(g, p, id); Kill(g, source);
        Core2026Proof.Check(p.Opponent.Hero.Damage - before + enemy.Damage + other.Damage == 2 && friend.Damage == 0 && p.Hero.Damage == 0, "two random one-damage pings across enemies; no spell damage bonus or friendly damage");
        before = p.Opponent.Hero.Damage; int total = enemy.Damage + other.Damage;
        source = (Minion)Play(g, p, id); Play(g, p, "BE_228", source); Kill(g, source);
        Core2026Proof.Check(p.Opponent.Hero.Damage == before && enemy.Damage + other.Damage == total, "silence removes deathrattle");
    });
}
