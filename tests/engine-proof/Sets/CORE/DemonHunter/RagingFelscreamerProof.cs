using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RagingFelscreamerProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_BT_416", (g, p, id) => {
        var demon = Add(p, "ICC_218"); var cheap = Add(p, "EX1_319"); var ordinary = Add(p, "CS2_182"); var spell = Add(p, "CS2_029");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(demon.Cost == 1 && demon[GameTag.COST] == 1 && cheap.Cost == 0 && ordinary.Cost == 4 && spell.Cost == 4, "visible Demon-only discount floors at zero");
        var clone = g.Clone(); PlayEntity(clone, clone.Player1, clone.IdEntityDic[cheap.Id]);
        Core2026Proof.Check(clone.IdEntityDic[demon.Id].Cost == 3 && demon.Cost == 1, "clone discount consumption isolated");
        source.Silence(); Kill(g, source); Core2026Proof.Check(demon.Cost == 1, "earned battlecry survives Silence and death");
        PlayEntity(g, p, ordinary); PlayEntity(g, p, spell, p.Opponent.Hero);
        Summon(p, "EX1_319"); Core2026Proof.Check(demon.Cost == 1, "non-Demons, spells and summoned Demons do not consume");
        End(g); Play(g, p.Opponent, "EX1_319"); Core2026Proof.Check(demon.Cost == 1, "opponent Demon does not consume");
        End(g); Core2026Proof.Check(demon.Cost == 1, "unused discount persists across turns");
        var later = Add(p, "ICC_218"); g.AuraUpdate(); Core2026Proof.Check(later.Cost == 1 && later[GameTag.COST] == 1, "later draw receives visible discount");
        PlayEntity(g, p, cheap); Core2026Proof.Check(demon.Cost == 3 && later.Cost == 3, "zero-cost Demon consumes once");
        ClearBoard(g, p); Play(g, p, id); Play(g, p, id);
        Core2026Proof.Check(demon.Cost == 0 && demon[GameTag.COST] == 0, "two battlecries stack");
        PlayEntity(g, p, demon); Core2026Proof.Check(later.Cost == 3, "next Demon consumes both discounts");
    });
}
