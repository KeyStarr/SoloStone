using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    [GroupProof("spell-healing", ProofKind.Native)]
    private static void SpellHealingNativeProof()
    {
        foreach (bool bot in new[] { false, true })
        foreach (bool deck in new[] { false, true })
        {
            var (match, capture) = AnimationMatch();
            var owner = bot ? match.Game.Player2 : match.Game.Player1;
            var spell = EngineEntity.FromCard(owner, Cards.FromId("CS2_029"),
                zone: deck ? (SabberStoneCore.Model.Zones.IZone)owner.DeckZone : owner.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            Generic.AddEnchantmentBlock(match.Game, Cards.FromId("CATA_458e"), owner.Hero, spell);
            Generic.AddEnchantmentBlock(match.Game, Cards.FromId("CATA_209e"), owner.Hero, spell);
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            Check(spell[GameTag.SPELLPOWER] == 2 && owner.CurrentSpellPower == 0, "native instance Spell Damage stays off player summary");
            var actors = spell.AppliedEnchantments;
            if (bot || deck)
            {
                Check(data.Where(e => actors.Any(a => a.Id == SnapshotEntity(e))).All(e =>
                    string.IsNullOrEmpty(SnapshotName(e)) && SnapshotTag(e, GameTag.ATTACHED) == 0),
                    "hidden spell attachments reveal neither identity nor host");
                Check(!data.Any(e => e.HasTagChange && e.TagChange.Entity == spell.Id
                    && e.TagChange.Tag == (int)GameTag.CURRENT_SPELLPOWER && e.TagChange.Value > 0), "hidden spell bonus does not leak");
            }
            else
            {
                Check(data.Any(e => e.HasTagChange && e.TagChange.Entity == spell.Id
                    && e.TagChange.Tag == (int)GameTag.CURRENT_SPELLPOWER && e.TagChange.Value == 2), "visible host receives retail spell-local CURRENT_SPELLPOWER=2");
                Check(actors.All(a => data.Any(e => SnapshotEntity(e) == a.Id && SnapshotName(e) == a.Card.Id
                    && SnapshotTag(e, GameTag.ATTACHED) == spell.Id)), "visible named enchantments attach to spell");
            }
            Check(!data.Any(e => e.HasTagChange && e.TagChange.Tag >= 31280 && e.TagChange.Tag <= 31282),
                "healing bookkeeping never reaches native packets");
            capture.SerializeAll();
        }
        SpellHealingReviewProof("spell");
        SpellHealingReviewProof("healing");
        SpellHealingReviewProof("draw");
        Console.WriteLine("PASS native spell-local Spell Damage: host tags, named attachments, hand/deck privacy");
    }
}
