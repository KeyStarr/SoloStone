using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class TindralSageswiftProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_958", (g, p, id) => {
        var enemy = Summon(p.Opponent, "CS2_182"); var friendly = Summon(p, "CS2_182");
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(enemy.Damage == 0, "damage waits for death"); Kill(g, source);
        Core2026Proof.Check(enemy.Damage == 1 && p.Opponent.Hero.Damage == 1 && friendly.Damage == 0 && p.Hero.Damage == 0, "own-turn Deathrattle deals one to all enemies");
        source = Summon(p, id); End(g); Kill(g, source);
        Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && p.Opponent.Hero.Damage == 5 && friendly.Damage == 0, "opponent-turn Deathrattle deals four");
        source = Summon(p, id); source.Silence(); Kill(g, source); Core2026Proof.Check(p.Opponent.Hero.Damage == 5, "silence removes Deathrattle");
        source = Summon(p, id); Kill(g, source); Core2026Proof.Check(p.Opponent.Hero.Damage == 9, "empty enemy board still damages hero");
    });
}
