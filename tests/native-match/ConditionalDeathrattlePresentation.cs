using System;
using System.Collections.Generic;
using System.Linq;
using PegasusGame;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    // Chillmaw (both card ids) dying without a Dragon in its player's hand used to send an empty trigger block that
    // still selected its card effect (index 0), so the client played the frost-breath spell with no damage. A failed
    // condition must send no effect selection; a successful one keeps the effect and both damage tasks. Retail's exact
    // packet for the failed case is not pinned, so "no effect selected" is our chosen presentation, not a copy. Both
    // players are covered, a summon (played or by an effect) must send no Chillmaw block, and the held card stays
    // hidden.
    [GroupProof("conditional-deathrattle", ProofKind.Native)]
    private static void ConditionalDeathrattlePresentation()
    {
        var failures = new List<string>();
        foreach (string id in new[] { "AT_123", "CORE_AT_123" })
        foreach (bool bot in new[] { false, true })
        foreach (bool dragon in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: !bot);
            var game = match.Game;
            var killer = game.CurrentPlayer;
            var owner = killer.Opponent;
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            var hidden = EngineEntity.FromCard(owner, Cards.FromId(dragon ? "NEW1_023" : "CS2_182"), zone: owner.HandZone);
            var source = (Minion)EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.BoardZone);
            var victims = new[] { owner, killer }.Select(c => (Minion)EngineEntity.FromCard(c, Cards.FromId("CS2_200"), zone: c.BoardZone)).ToArray();
            var fireball = EngineEntity.FromCard(killer, Cards.FromId("CS2_029"), zone: killer.HandZone);
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            InvokeProcess(match, killer.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == fireball.Id && t.Target?.Id == source.Id));
            FlushFieldHistory(match);
            string name = $"{id}-death-dragon-{dragon}-owner-{(bot ? "bot" : "human")}";
            var rows = History(capture, cursor);
            DumpDrawHistory(name, capture, cursor);
            Check(source.Zone == owner.GraveyardZone && victims.All(v => v.Damage == (dragon ? 3 : 0)), name + " engine damage");
            var effects = rows.Where(e => e.HasPowerStart && e.PowerStart.Source == source.Id
                && e.PowerStart.Type is HistoryBlock.Type.TRIGGER or HistoryBlock.Type.POWER).ToArray();
            Console.WriteLine(name + " blocks=" + string.Join(";", effects.Select(e => $"{e.PowerStart.Type}/index={e.PowerStart.EffectIndex}/keyword={e.PowerStart.TriggerKeyword}")));
            if (effects.Any(e => e.PowerStart.EffectIndex >= 0 || !string.IsNullOrEmpty(e.PowerStart.EffectCardId)) != dragon)
                failures.Add(name + ": failed condition must not select effect spell");
            if (dragon)
            {
                Check(effects.Any(e => e.PowerStart.Type == HistoryBlock.Type.TRIGGER && e.PowerStart.EffectIndex == 0), name + " trigger slot 0 retained");
                Check(victims.All(v => rows.Any(e => e.HasMetaData && e.MetaData.Type == HistoryMeta.Type.DAMAGE && e.MetaData.Data == 3 && e.MetaData.Info.Contains(v.Id))), name + " damage tasks retained");
            }
            if (bot) Check(!rows.Any(e => SnapshotEntity(e) == hidden.Id && !string.IsNullOrEmpty(SnapshotName(e))), name + " held identity private");
            Check(NativeAdmission.ValidatePackets(capture.HistorySnapshots().Select(p => (IReadOnlyList<PowerHistoryData>)p.List)).Count == 0, name + " native admission");
        }
        foreach (string id in new[] { "AT_123", "CORE_AT_123" })
        foreach (bool bot in new[] { false, true })
        foreach (bool effectSummon in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(bot);
            var owner = match.Game.CurrentPlayer;
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            var source = EngineEntity.FromCard(owner, Cards.FromId(id), zone: owner.HandZone);
            var summoner = effectSummon ? (Minion)EngineEntity.FromCard(owner, Cards.FromId("OG_317"), zone: owner.BoardZone) : null;
            FlushFieldHistory(match);
            int cursor = capture.Packets.Count;
            if (effectSummon)
            {
                summoner.ToBeDestroyed = true;
                match.Game.DeathProcessingAndAuraUpdate();
            }
            else InvokeProcess(match, owner.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
            FlushFieldHistory(match);
            string name = $"{id}-{(effectSummon ? "deathwing-summon" : "play")}-{(bot ? "bot" : "human")}";
            DumpDrawHistory(name, capture, cursor);
            var bodies = owner.BoardZone.Where(m => m.Card.Id == id).ToArray();
            Check(bodies.Length == 1, name + " summoned body");
            var effects = History(capture, cursor).Where(e => e.HasPowerStart && bodies.Any(m => m.Id == e.PowerStart.Source)
                && e.PowerStart.Type is HistoryBlock.Type.TRIGGER or HistoryBlock.Type.POWER).ToArray();
            Console.WriteLine(name + " blocks=" + string.Join(";", effects.Select(e => $"{e.PowerStart.Type}/index={e.PowerStart.EffectIndex}/keyword={e.PowerStart.TriggerKeyword}")));
            Check(effects.Length == 0, name + " no Chillmaw POWER/TRIGGER on summon");
        }
        Check(failures.Count == 0, string.Join(" | ", failures));
        Console.WriteLine("PASS conditional deathrattle: both Chillmaw IDs, both owners, failed/successful condition and hand/effect summons");
    }
}
