using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class LightningRodProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_212", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var spell = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(options.Length == 1 && options[0].Target == friendly, "friendly minion target only");
        PlayEntity(g, p, spell, friendly);
        Core2026Proof.Check(friendly.Damage == 2 && enemy.Damage == 4 && p.Opponent.Hero.Damage == 0, "two friendly damage then four enemy minion damage");
        ClearBoard(g, p.Opponent); Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.Damage == 4 && p.Opponent.Hero.Damage == 0, "empty enemy board still damages friendly minion");
        friendly.Damage = 0; Summon(p, "TIME_856");
        enemy = Summon(p.Opponent, "CS2_182"); var other = Summon(p.Opponent, "CS2_182");
        var clone = g.CloneExact(history: true);
        Play(g, p, id, friendly); Play(clone, clone.Player1, id, (Minion)clone.IdEntityDic[friendly.Id]);
        Core2026Proof.Check(friendly.Damage == 4 && new[] { enemy, other }.Count(m => m.Zone == p.Opponent.GraveyardZone) == 1, "spell power applies to both damage values and exactly one random target");
        Core2026Proof.Check(HsSim.Canonical.State(g) == HsSim.Canonical.State(clone), "random target deterministic after clone");
        ClearBoard(g, p); spell = Add(p, id); p.UsedMana = 0;
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "no friendly minion means no legal target");
    });
}
