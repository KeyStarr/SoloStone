using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    private static void Unpinned_BonusEffectsHistory()
    {
        RegrantedShieldCopyHistory();
        var (match,capture) = AnimationMatch(); var g = match.Game; var p = g.Player1;
        var target = (Minion)EngineEntity.FromCard(p,Cards.FromId("CS2_182"));
        Generic.SummonBlock(g,target,-1,null); g.DeathProcessingAndAuraUpdate();
        FlushFieldHistory(match); int cursor = capture.Packets.Count;
        BonusEffects.GiveRandom(target,p.Hero,8); FlushFieldHistory(match);
        var data = History(capture,cursor).ToArray();
        Check(target.AppliedEnchantments.Count == 8, "eight persistent keyword actors");
        foreach (var actor in target.AppliedEnchantments)
        {
            int show = Array.FindIndex(data,e => e.HasShowEntity && e.ShowEntity.Entity == actor.Id);
            Check(show >= 0 && SnapshotName(data[show]) == actor.Card.Id && SnapshotTag(data[show],GameTag.ATTACHED) == target.Id,
                "export-backed grant actor reveals attached recipient");
            Check(data.Skip(show+1).Any(e => e.HasTagChange && e.TagChange.Entity == target.Id), "grant actor precedes keyword update");
        }
        Check(data.Any(e => e.HasTagChange && e.TagChange.Entity == target.Id && e.TagChange.Tag == 1211 && e.TagChange.Value == 1), "effective Elusive projected");
        cursor=capture.Packets.Count; target.Silence(); FlushFieldHistory(match);
        Check(History(capture,cursor).Any(e => e.HasTagChange && e.TagChange.Entity == target.Id && e.TagChange.Tag == 1211 && e.TagChange.Value == 0), "Silence projects Elusive removal");
        capture.SerializeAll();

        foreach (bool hidden in new[] {false,true})
        {
            var fixture = AnimationMatch(); match=fixture.Item1; capture=fixture.Item2;
            g=match.Game; p=hidden ? g.Player2 : g.Player1;
            var held=(Minion)Generic.DrawCard(p,Cards.FromId("CATA_206"));
            FlushFieldHistory(match); cursor=capture.Packets.Count;
            BonusEffects.ReplacePair(held); FlushFieldHistory(match);
            var changes=History(capture,cursor).Where(e => e.HasTagChange && e.TagChange.Entity == held.Id
                && (e.TagChange.Tag == 4161 || e.TagChange.Tag == 4162)).ToArray();
            Check(hidden ? changes.Length == 0 : changes.Length > 0, "dynamic pair updates visible only to owner");
            if (!hidden) foreach (var change in changes)
                Check(change.TagChange.Value == held[(GameTag)change.TagChange.Tag], "dynamic keyword wire matches current pair");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Unpinned Bonus Effects wire: actors, keywords, Silence, dynamic pair privacy");
    }

    private static void RegrantedShieldCopyHistory()
    {
        var (match, capture) = AnimationMatch(); var g = match.Game; var p = g.Player1;
        var original = (Minion)EngineEntity.FromCard(p, Cards.FromId("EX1_008"), zone: p.BoardZone);
        original.Silence();
        var spell = EngineEntity.FromCard(p, Cards.FromId("EX1_371"), zone: p.HandZone);
        InvokeProcess(match, PlayCardTask.SpellTarget(p, spell, original));
        Check(original.IsSilenced && original.HasDivineShield, "fixture has re-granted Shield");
        FlushFieldHistory(match); int cursor = capture.Packets.Count;
        var copy = (Minion)Generic.Copy(p, p.Hero, original, SabberStoneCore.Enums.Zone.PLAY);
        FlushFieldHistory(match);
        var shown = History(capture, cursor).Last(e => e.HasShowEntity && e.ShowEntity.Entity == copy.Id);
        Check(SnapshotTag(shown, GameTag.SILENCED) == 1 && SnapshotTag(shown, GameTag.DIVINE_SHIELD) == 1,
            "copy reveal retains Silence and re-granted Shield using pinned client keyword tags");
        cursor = capture.Packets.Count;
        copy.TakeDamage(g.Player2.Hero, 1); FlushFieldHistory(match);
        Check(History(capture, cursor).Any(e => e.HasTagChange && e.TagChange.Entity == copy.Id
            && e.TagChange.Tag == (int)GameTag.DIVINE_SHIELD && e.TagChange.Value == 0),
            "copied Shield consumption projects native keyword removal");
        capture.SerializeAll();
    }
}
