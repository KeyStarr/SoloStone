using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

namespace CoreLegacy;

// Tier D: pinned data/carddb/37.0.0.254477/cards.json.gz; current text and printed tags.
internal static class CorePreparationProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EX1_145", (g, p, id) => {
        // Tier A: https://hearthstone.blizzard.com/en-us/news/22990356 (2019-05-20); exact current discount corroborated by pinned export.
        var discount = Cards.FromId("EX1_145o");
        Core2026Proof.Check(discount.AssetId == 1760 && discount.Type == CardType.ENCHANTMENT && discount.Cost == 0
            && discount.ATK == 0 && discount.Health == 0 && discount.Tags.GetValueOrDefault(GameTag.CANT_BE_SILENCED) == 1
            && discount.Tags.GetValueOrDefault(GameTag.TAG_ONE_TURN_EFFECT) == 1
            && discount.Text == "The next spell you cast this turn costs (2) less.", "pinned Preparation dependency metadata");
        var spell = Add(p, "CS2_029"); var second = Add(p, "CS2_029"); var minion = Add(p, "CS2_182");
        var opposing = Add(p.Opponent, "CS2_029");
        Play(g, p, id); Core2026Proof.Check(spell.Cost == 2 && second.Cost == 2 && minion.Cost == 4, "next-spell aura discounts two, excludes minions");
        Core2026Proof.Check(opposing.Cost == 4, "opponent spell excluded while friendly discount is active");
        PlayEntity(g, p, minion); Core2026Proof.Check(spell.Cost == 2, "playing minion does not consume discount");
        var clone = g.CloneExact(history: true); PlayEntity(g, p, spell, p.Opponent.Hero);
        PlayEntity(clone, clone.Player1, (IPlayable)clone.IdEntityDic[spell.Id], clone.Player2.Hero);
        Core2026Proof.Check(second.Cost == 4 && p.Opponent.Hero.Damage == 6 && Canonical.State(g) == Canonical.State(clone), "discount consumed by first spell, cloned aura behaves identically");
        Play(g, p, id); End(g); Core2026Proof.Check(second.Cost == 4 && opposing.Cost == 4, "unused discount expires this turn");
        End(g); Play(g, p, id); var cheap = Add(p, "CS2_008"); Core2026Proof.Check(cheap.Cost == 0, "cost floors at zero");
        PlayEntity(g, p, cheap, p.Opponent.Hero); Core2026Proof.Check(second.Cost == 4, "zero-cost spell consumes discount");
    }, legacyPoolMember: true);
}
