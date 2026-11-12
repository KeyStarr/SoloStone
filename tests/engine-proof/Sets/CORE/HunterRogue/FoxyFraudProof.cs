using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FoxyFraudProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_DMF_511", (g, p, id) => {

        var combo = Add(p, "EX1_613"); var cheap = Add(p, "EX1_124"); var ordinary = Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(combo.Cost == 1 && combo[GameTag.COST] == 1 && cheap.Cost == 0 && ordinary.Cost == 4 && spell.Cost == 4, "visible Combo-only discount, floor zero");
        var enchantment = p.AppliedEnchantments.Single(e => e.Card.Id == "DMF_511e"); Core2026Proof.Check(enchantment.IsOneTurnActive, "exact one-turn controller enchantment");
        var clone = g.CloneExact(history: true); PlayEntity(clone, clone.Player1, clone.IdEntityDic[combo.Id]);
        Core2026Proof.Check(clone.IdEntityDic[cheap.Id].Cost == 2 && combo.Cost == 1 && cheap.Cost == 0, "clone consumption isolated");
        source.Silence(); Kill(g, source); Core2026Proof.Check(combo.Cost == 1, "earned battlecry survives source Silence/death");
        PlayEntity(g, p, ordinary); PlayEntity(g, p, spell, p.Opponent.Hero); Core2026Proof.Check(combo.Cost == 1, "non-Combo minion and spell do not consume");
        var added = Add(p, "EX1_613"); g.AuraUpdate(); Core2026Proof.Check(added.Cost == 1 && added[GameTag.COST] == 1, "later hand card receives visible discount");
        PlayEntity(g, p, cheap, p.Opponent.Hero); Core2026Proof.Check(combo.Cost == 3 && added.Cost == 3 && !p.AppliedEnchantments.Contains(enchantment), "zero-cost Combo consumes discount once");
        ClearBoard(g, p); Play(g, p, id); Play(g, p, id); Core2026Proof.Check(combo.Cost == 0, "two battlecries stack");
        PlayEntity(g, p, combo); Core2026Proof.Check(added.Cost == 3, "next Combo consumes every active discount");
        ClearBoard(g, p); Play(g, p, id); End(g); Core2026Proof.Check(added.Cost == 3 && added[GameTag.COST] == 3, "unused discount expires at turn end");
        // Install the battlecry during the opposing turn to exercise controller-side filtering.
        var offTurn = Summon(p, id); offTurn.Card.Power.PowerTask.Process(g, p, offTurn, null, new TaskStack()); g.AuraUpdate();
        Play(g, p.Opponent, "EX1_131"); Core2026Proof.Check(added.Cost == 1, "opponent Combo cannot consume owner's discount");
        End(g); Core2026Proof.Check(added.Cost == 3, "off-turn discount expires at that turn end");
    });
}
