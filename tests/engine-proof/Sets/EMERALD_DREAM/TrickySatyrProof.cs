using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class TrickySatyrProof
{
    internal static void Run() => Unpinned_PayloadLifecycle();
    private static void Unpinned_PayloadLifecycle() => StandardSetsProof.Case("EDR_521", (g, p, id) => {
        var expensive = Add(p.Opponent, "CS2_182"); expensive.Cost = 6;
        var spell = Add(p.Opponent, "CS2_029"); spell.Cost = 1; spell[GameTag.TAG_SCRIPT_DATA_NUM_2] = 17;
        var payload = PayloadRules.StoreCard(spell, 99, Cards.FromId("CS2_120")); payload[PayloadRules.Value] = 4;
        Play(g, p, id);
        var copied = p.HandZone.Single();
        Core2026Proof.Check(copied.Card.Id == spell.Card.Id && copied.Cost == 1 && copied[GameTag.TAG_SCRIPT_DATA_NUM_2] == 17,
            "Unpinned_HandSpell copies numeric Cost and script state");
        var entry = PayloadRules.Entries(copied, 99).Single(); entry[PayloadRules.Value] = 8;
        Core2026Proof.Check(payload[PayloadRules.Value] == 4 && entry.Id != payload.Id, "spell payload deep copy");
        foreach (var card in p.Opponent.HandZone.ToArray()) p.Opponent.SetasideZone.Add(p.Opponent.HandZone.Remove(card));
        int count = p.HandZone.Count; Play(g, p, id); Core2026Proof.Check(p.HandZone.Count == count, "empty enemy hand fizzles");
    });
}
