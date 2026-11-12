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
internal static class ConstellarsCallingProof
{
    internal static void Run() => StandardSetsProof.Case("BE_252", (g, p, id) => {
        var target = Summon(p, "CS2_200"); var enemy = Summon(p.Opponent, "CS2_182");
        var spell = Add(p, id);
        var options = p.Options().Where(t => t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target is Minion && t.Target.Controller == p), "friendly minion required; enemies and heroes excluded");
        target.Cost = 1;
        Resume(g, next => PlayEntity(next, next.Player1, next.IdEntityDic[spell.Id], (Minion)next.IdEntityDic[target.Id]));
        Outputs(g, p, spell, 1, 1);
        Core2026Proof.Check(target.AttackDamage == target.Card.ATK && target.Health == target.Card.Health, "chosen minion untouched");
        target.Cost = 99;
        long draws = g.Random.DrawCount; int slots = p.BoardZone.SlotCount;
        Play(g, p, id, target);
        Core2026Proof.Check(g.Random.DrawCount == draws && p.BoardZone.SlotCount == slots, "unavailable dynamic cost creates nothing without RNG");
        target.Cost = 2;
        Restricted(g, id, 1, () => { var source = Play(g, p, id, target); Outputs(g, p, source, 1, 2); }, 2);
        Location(p); while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        draws = g.Random.DrawCount; spell = Play(g, p, id, target);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && Children(p, spell).Length == 0 && g.Random.DrawCount == draws + 1, "full mixed board does not overflow");
    });
}
