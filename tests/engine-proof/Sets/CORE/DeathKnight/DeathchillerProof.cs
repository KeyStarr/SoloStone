using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class DeathchillerProof
{
    internal static void Run() => DeathKnightC2Proof.Case("CORE_RLK_083", (g, p, id) => {
        var source = Summon(p, id); var enemy = Summon(p.Opponent, "CS2_182");
        Summon(p, "CS2_142");
        DeathKnightC2Proof.Continue(g, copy => {
            Play(copy, copy.Player1, "CS2_029", copy.Player1.Opponent.Hero);
            Core2026Proof.Check(copy.Player2.Hero.Damage == 8 && ((Minion)copy.IdEntityDic[enemy.Id]).Damage == 1,
                "after spell resolution two distinct enemies; minion trigger ignores spell damage");
        });
        ClearBoard(g, p.Opponent); int before = p.Opponent.Hero.Damage;
        Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == before + 8, "singleton enemy hit once");
        var targets = new[] { Summon(p.Opponent, "CS2_182"), Summon(p.Opponent, "CS2_182"), Summon(p.Opponent, "CS2_182") };
        targets[0].HasStealth = true;
        DeathKnightC2Proof.Continue(g, copy => {
            int heroDamage = copy.Player2.Hero.Damage;
            Play(copy, copy.Player1, "GAME_005");
            var damage = targets.Select(m => ((Minion)copy.IdEntityDic[m.Id]).Damage)
                .Append(copy.Player2.Hero.Damage - heroDamage).ToArray();
            Core2026Proof.Check(damage.Count(n => n == 1) == 2 && damage.Sum() == 2,
                "four candidates produce two distinct one-damage hits, including Stealth candidates");
        });
        ClearBoard(g, p.Opponent);
        source.Silence(); g.DeathProcessingAndAuraUpdate(); before = p.Opponent.Hero.Damage;
        Play(g, p, "CS2_029", p.Opponent.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == before + 7, "silence removes after-cast trigger");
        p.Opponent.Hero.Damage = 0; source = Summon(p, id); End(g);
        Play(g, p.Opponent, "CS2_029", p.Hero);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "opponent spell does not trigger");
        End(g); Play(g, p, "CS2_029", source);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.Opponent.Hero.Damage == 0, "source killed by spell cannot trigger afterward");
    });
}
