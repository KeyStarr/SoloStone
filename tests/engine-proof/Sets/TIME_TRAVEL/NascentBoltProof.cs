using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class NascentBoltProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_216", (g, p, id) => {
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        TargetRules(g, p, id, enemy, friendly, false);
        var spell = p.HandZone.Single(); int deck = p.DeckZone.Count;
        enemy.HasDivineShield = true; PlayEntity(g, p, spell, enemy);
        Core2026Proof.Check(!enemy.HasDivineShield && enemy.Damage == 0 && p.DeckZone.Count == deck - 2, "shield survives and draws two");
        deck = p.DeckZone.Count; Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.Zone == p.Opponent.GraveyardZone && p.DeckZone.Count == deck, "exact lethal draws nothing");
        var durable = Summon(p, "TIME_856"); deck = p.DeckZone.Count;
        Play(g, p, id, durable);
        Core2026Proof.Check(durable.Damage == 7 && durable.Zone == p.GraveyardZone && p.DeckZone.Count == deck, "spell power makes seven damage lethal");
        durable = Summon(p, "CS2_186"); deck = p.DeckZone.Count;
        Play(g, p, id, durable);
        Core2026Proof.Check(durable.Damage == 5 && durable.Health == 2 && p.DeckZone.Count == deck - 2, "friendly survivor draws two");
    });
}
