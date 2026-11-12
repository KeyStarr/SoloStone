using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;
using static BEPoolProof;
internal static class ForebodingFreezeProof
{
    internal static void Run() => StandardSetsProof.Case("BE_201", (g, p, id) => {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_200");
        var spell = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(options.Any(t => t.Target?.Id == enemy.Id) && options.Any(t => t.Target?.Id == p.Opponent.Hero.Id)
            && options.All(t => t.Target?.Controller == p.Opponent), "only enemy character targets");
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[spell.Id], next.Player2.Hero));
        Outputs(g, p, spell, 1, 5);
        Core2026Proof.Check(p.Opponent.Hero.IsFrozen && Children(p, spell).Single().IsFrozen && !friend.IsFrozen && !enemy.IsFrozen, "freezes target and generated minion only");
        Restricted(g, id, 0, () => {
            var rng = g.Random.CaptureState(); int slots = p.BoardZone.SlotCount;
            Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.IsFrozen && slots == p.BoardZone.SlotCount && rng.Equals(g.Random.CaptureState()), "empty pool still freezes without RNG");
        });
        Restricted(g, id, 1, () => {
            var source = Play(g, p, id, enemy); Outputs(g, p, source, 1, 5);
        });
        Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        long draws = g.Random.DrawCount; spell = Play(g, p, id, enemy);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && Children(p, spell).Length == 0 && g.Random.DrawCount == draws + 1, "full mixed board samples but creates no minion");
    });
}
