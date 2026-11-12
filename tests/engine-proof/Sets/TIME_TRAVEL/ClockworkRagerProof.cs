using SabberStoneCore.Model.Entities;
using SabberStoneCore.Enums;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ClockworkRagerProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_048", (g, p, id) => {
        HistoryCardProofChecks.HiddenDisplay(g, id);
        var held = Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "first-turn hand text includes current turn");
        var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(body.AttackDamage == 5 && body.Health == 2 && body.Card.ConsumesHistory, "current first turn counts; printed 5/1 gains one Health");
        Core2026Proof.Check(body.AppliedEnchantments.Single().Card.Id == "TIME_048e", "exact Health enchantment");
        body.Silence(); g.DeathProcessingAndAuraUpdate(); Core2026Proof.Check(body.Health == 1, "silence removes gained Health");
        End(g);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "opponent turn does not advance hand text");
        var enemy = (Minion)Play(g, p.Opponent, id); Core2026Proof.Check(enemy.Health == 2, "second player counts own turns, not global turn");
        End(g);
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "held Rager text updates at owner start");
        body = (Minion)Play(g, p, id); Core2026Proof.Check(body.Health == 3, "turns before creation retained; opponent turn excluded");
        var clone = g.Clone(); clone.Player1.NumTurnsLeft = 2; End(clone);
        var cloned = (Minion)Play(clone, clone.Player1, id);
        Core2026Proof.Check(clone.IdEntityDic[held.Id][GameTag.TAG_SCRIPT_DATA_NUM_1] == 3
            && held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "extra-turn display updates in clone only");
        Core2026Proof.Check(cloned.Health == 4 && g.HistoryFacts.Query.TurnsTaken(p.Id) == 2, "extra turn in clone counts independently, original isolated");
        p.NumTurnsLeft = 2; End(g);
        body = (Minion)Play(g, p, id); Core2026Proof.Check(body.Health == 4, "consecutive extra own turn counts without parity inference");
    });
}
