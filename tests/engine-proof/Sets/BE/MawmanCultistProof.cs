using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MawmanCultistProof
{
    internal static void Run() => StandardSetsProof.Case("BE_729", (g, p, id) => {
        var friendlySpell = Add(p, "CS2_029"); var enemySpell = Add(p.Opponent, "CS2_029");
        var minion = Add(p, "CS2_171"); var weapon = Add(p, "CS2_106"); var held = Add(p, id);
        Core2026Proof.Check(friendlySpell.Cost == 4 && enemySpell.Cost == 4, "held source has no aura");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(friendlySpell.Cost == 5 && enemySpell.Cost == 5 && friendlySpell[GameTag.COST] == 5 && enemySpell[GameTag.COST] == 5,
            "both hands expose derived COST plus one");
        Core2026Proof.Check(minion.Cost == 1 && weapon.Cost == 3 && held.Cost == 3, "minions and weapons excluded");
        var newSpell = Add(p, "CS2_008"); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(newSpell.Cost == 1 && newSpell[GameTag.COST] == 1, "newly drawn zero-cost spell gets aura");
        var clone = g.CloneExact(history: true); ((Minion)clone.IdEntityDic[source.Id]).Silence(); clone.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(clone.IdEntityDic[friendlySpell.Id].Cost == 4 && friendlySpell.Cost == 5, "cloned aura removal isolated");
        var other = Summon(p.Opponent, id);
        Core2026Proof.Check(friendlySpell.Cost == 6 && enemySpell.Cost == 6, "opponent source stacks on both hands");
        End(g); Core2026Proof.Check(friendlySpell.Cost == 6 && enemySpell.Cost == 6, "aura remains across turn boundary"); End(g);
        source.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(friendlySpell.Cost == 5 && enemySpell.Cost == 5, "Silence removes only one source aura");
        Kill(g, other);
        Core2026Proof.Check(friendlySpell.Cost == 4 && enemySpell.Cost == 4 && newSpell.Cost == 0, "source leaving play restores both hands");
        source = (Minion)Play(g, p, id);
        int health = p.Opponent.Hero.Health;
        PlayEntity(g, p, friendlySpell, p.Opponent.Hero);
        Core2026Proof.Check(p.UsedMana == 5 && p.Opponent.Hero.Health == health - 6, "spell pays increased cost through ordinary play");
        Kill(g, source);
    });
}
