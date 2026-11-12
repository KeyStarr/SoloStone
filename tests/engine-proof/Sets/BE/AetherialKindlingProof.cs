using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class AetherialKindlingProof
{
    internal static void Run() => StandardSetsProof.Case("BE_854", (g, p, id) => {
        SourceExcluded(g, id);
        var spellDamage = Summon(p, "CS2_052");
        var target = Summon(p.Opponent, "CS2_200");
        var source = Add(p, id); int health = target.Health;
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[source.Id], (Minion)next.IdEntityDic[target.Id]));
        Core2026Proof.Check(target.Health == health - 4, "spell damage bonus applies before summon");
        Generated(g, source, p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id), 1);
        spellDamage.Silence(); g.DeathProcessingAndAuraUpdate();
        Pool(g, id, 0, () => {
            var random = g.Random.CaptureState(); int count = p.BoardZone.Count; int hp = p.Opponent.Hero.Health;
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Health == hp - 3 && p.BoardZone.Count == count && g.Random.CaptureState().Equals(random), "empty pool still damages");
        });
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        int beforeHealth = p.Opponent.Hero.Health;
        long beforeDraws = g.Random.DrawCount;
        source = Play(g, p, id, p.Opponent.Hero);
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.Opponent.Hero.Health == beforeHealth - 3 &&
            g.Random.DrawCount - beforeDraws == 1, "full board still damages and samples");
        var attacker = p.Opponent.BoardZone.First();
        var spell = Add(p, id);
        Core2026Proof.Check(p.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == p.Hero.Id)
            && p.Options().Any(t => t.Source?.Id == spell.Id && t.Target?.Id == attacker.Id), "damage targets any character");
    });
}
