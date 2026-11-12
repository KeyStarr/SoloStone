using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    [GroupProof("b17-b13-extensions-native", ProofKind.Native)]
    private static void B17B13ExtensionsNative()
    {
        var capture = new Capture(); var match = StartAtHumanTurn(capture); var p = match.Game.Player1;
        var enemy = p.Opponent;
        foreach (var c in enemy.HandZone.ToArray()) enemy.SetasideZone.Add(enemy.HandZone.Remove(c));
        var ido = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(enemy,Cards.FromId("TLC_241"));
        Generic.SummonBlock(match.Game,ido,-1,null); match.Game.DeathProcessingAndAuraUpdate();
        var linked = enemy.HandZone.Single(c => c.Card.Id == "TLC_241t");
        FlushHistory(match);
        Check(!NativeHistory(capture).Any(e => e.HasFullEntity && e.FullEntity.Entity == linked.Id && !string.IsNullOrEmpty(e.FullEntity.Name)
            || e.HasShowEntity && e.ShowEntity.Entity == linked.Id && !string.IsNullOrEmpty(e.ShowEntity.Name)),"opponent retained spell stays hidden");
        enemy.DeckZone.Add(enemy.HandZone.Remove(linked));
        match.Game.DeathProcessingAndAuraUpdate(); FlushHistory(match);
        ido.Silence(); match.Game.DeathProcessingAndAuraUpdate(); FlushHistory(match);
        Check(!NativeHistory(capture).Any(e => e.HasFullEntity && e.FullEntity.Entity == linked.Id && !string.IsNullOrEmpty(e.FullEntity.Name)
            || e.HasShowEntity && e.ShowEntity.Entity == linked.Id && !string.IsNullOrEmpty(e.ShowEntity.Name)),"shuffle and aura-loss removal do not reveal hidden spell");
        foreach(var entry in NativeHistory(capture))
        {
            var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
            Check(entity == null || entity.Tags.All(t => !RepeatRules.IsInternalTag((GameTag)t.Name)),"retained links filtered from entity snapshots");
            Check(!entry.HasTagChange || !RepeatRules.IsInternalTag((GameTag)entry.TagChange.Tag),"retained link changes filtered");
        }
        CheckPinnedClientIds(capture); capture.SerializeAll();
        Console.WriteLine("PASS b17 extensions native hidden linked spell and private tags");
    }
}
