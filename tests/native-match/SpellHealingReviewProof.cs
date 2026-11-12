using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    private static void SpellHealingReviewProof(string name)
    {
        if (name == "spell") SpellLocalClientTag();
        else if (name == "healing") HealingClientTag();
        else if (name == "draw") SpellAttachmentLifecycle();
        else throw new ArgumentException(name);
    }

    // Pinned Player.TotalSpellpower reads spell-local 291 plus controller 291.
    private static void SpellLocalClientTag()
    {
        var (match, capture) = AnimationMatch(); var p = match.Game.Player1;
        var spell = EngineEntity.FromCard(p, Cards.FromId("CS2_029"), zone: p.HandZone);
        FlushFieldHistory(match); int cursor = capture.Packets.Count;
        Generic.AddEnchantmentBlock(match.Game, Cards.FromId("CATA_458e"), p.Hero, spell);
        FlushFieldHistory(match);
        Check(History(capture, cursor).Any(e => e.HasTagChange && e.TagChange.Entity == spell.Id
            && e.TagChange.Tag == 291 && e.TagChange.Value == 1), "client TotalSpellpower needs spell tag 291=1");
        Check(match.SnapshotVisibleTransportTags(spell).TryGetValue(291, out int local) && local == 1,
            "visible transport snapshot preserves local client alias without duplicate tags");
        Check(SpellDamageRules.Amount(spell, 6) == 7 && p.CurrentSpellPower == 0,
            "spell-local client bonus agrees with damage, never player bonus");
        spell.AppliedEnchantments.Single().Remove(); FlushFieldHistory(match);
        Check(History(capture, cursor).Any(e => e.HasTagChange && e.TagChange.Entity == spell.Id
            && e.TagChange.Tag == 291 && e.TagChange.Value == 0), "removed local bonus clears client tag 291");
        capture.SerializeAll();
        Console.WriteLine("PASS spell-local client tag apply/remove");
    }

    // Pinned Entity.GetHealingBonus reads controller 2333.
    private static void HealingClientTag()
    {
        foreach (var scenario in new[] { (Stacks: 0, Baseline: 0, Retry: 2), (Stacks: 2, Baseline: 4, Retry: 6) })
        {
            var (match, capture) = AnimationMatch(); var p = match.Game.Player1;
            void PlayCleric()
            {
                p.UsedMana = 0;
                var source = EngineEntity.FromCard(p, Cards.FromId("CATA_216"), zone: p.HandZone);
                FlushFieldHistory(match);
                InvokeProcess(match, p.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == source.Id));
                FlushFieldHistory(match);
            }
            for (int i = 0; i < scenario.Stacks; i++) PlayCleric();
            var playerTags = NativeManaTags(capture, p.Id);
            int bonus = playerTags.TryGetValue(2333, out int value) ? value : 0;
            Check(bonus == scenario.Baseline, "client healing tag 2333 accumulates Clerics");
            foreach (var restored in new[] { match.Game.CloneExact(history: true), SabberStoneCore.Model.Game.LoadSnapshot(match.Game.SaveSnapshot()) })
            {
                var reset = SabberStoneCore.Kettle.PowerHistoryBuilder.ResetGame(restored);
                var player = reset.CreateGame.Players.Single(v => v.PowerEntity.Id == p.Id);
                int saved = player.PowerEntity.Tags.TryGetValue((GameTag)2333, out int captured) ? captured : 0;
                Check(saved == scenario.Baseline, "clone/snapshot reset captures accumulated healing bonus");
            }
            // Rewind restores the baseline bonus before replaying one new Battlecry.
            p.UsedMana = 0;
            var retry = EngineEntity.FromCard(p, Cards.FromId("CATA_216"), zone: p.HandZone);
            retry[GameTag.REWIND] = 1;
            FlushFieldHistory(match);
            InvokeProcess(match, p.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == retry.Id));
            FlushFieldHistory(match);
            Check(match.Game.Rewind != null, "Cleric Rewind offered");
            int cursor = capture.Packets.Count;
            match.Game.Process(ChooseTask.Pick(p, match.Game.Rewind.RewindEntityId));
            FlushFieldHistory(match);
            var data = History(capture, cursor);
            var resetPlayer = data.Single(e => e.HasResetGame).ResetGame.CreateGame.Players.Single(v => v.Entity.Id == p.Id);
            Check(resetPlayer.Entity.Tags.Any(t => t.Name == 2333 && t.Value == scenario.Baseline), "native Rewind reset restores baseline healing, including zero");
            Check(NativeManaTags(capture, p.Id).TryGetValue(2333, out bonus) && bonus == scenario.Retry
                && HealingRules.Bonus(match.Game.Player1) == scenario.Retry, "native retry publishes one new grant");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS healing client tag stack/clone/snapshot/Rewind");
    }

    private static void SpellAttachmentLifecycle()
    {
        foreach (bool bot in new[] { false, true })
        {
            var (match, capture) = AnimationMatch(botTurn: bot); var p = match.Game.CurrentPlayer;
            foreach (var old in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(old));
            var spell = EngineEntity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
            Generic.AddEnchantmentBlock(match.Game, Cards.FromId("CATA_458e"), p.Hero, spell);
            var actor = spell.AppliedEnchantments.Single();
            FlushFieldHistory(match); int cursor = capture.Packets.Count;
            Generic.Draw(p, spell); FlushFieldHistory(match);
            var data = History(capture, cursor);
            if (bot)
                Check(!data.Any(e => (SnapshotEntity(e) == spell.Id || SnapshotEntity(e) == actor.Id)
                    && !string.IsNullOrEmpty(SnapshotName(e))), "opponent draw keeps spell and attachment private");
            else
            {
                Check(data.Any(e => SnapshotEntity(e) == actor.Id && SnapshotName(e) == "CATA_458e"
                    && SnapshotTag(e, GameTag.ATTACHED) == spell.Id), "draw reveals named spell attachment");
                Check(data.Any(e => SnapshotEntity(e) == spell.Id && SnapshotTag(e, (GameTag)291) == 1),
                    "draw reveals spell-local client bonus");
                cursor = capture.Packets.Count;
                p.DeckZone.Add(p.HandZone.Remove(spell)); FlushFieldHistory(match);
                Check(History(capture, cursor).Any(e => e.HasHideEntity && e.HideEntity.Entity == actor.Id),
                    "return to deck hides attachment again");
                Generic.Draw(p, spell); FlushFieldHistory(match);
                Check(History(capture, cursor).Any(e => SnapshotEntity(e) == actor.Id && SnapshotName(e) == "CATA_458e"),
                    "redraw reveals attachment again");
            }
            cursor = capture.Packets.Count;
            var play = p.Options().First(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id
                && t.Target?.Id == p.Opponent.Hero.Id);
            InvokeProcess(match, play); FlushFieldHistory(match);
            data = History(capture, cursor);
            Check(p.Opponent.Hero.Damage == 7, "buffed drawn spell deals seven");
            if (bot) Check(data.Any(e => SnapshotEntity(e) == actor.Id && SnapshotName(e) == "CATA_458e"
                && SnapshotTag(e, GameTag.ATTACHED) == spell.Id), "opponent play reveals attached actor");
            cursor = capture.Packets.Count; FlushFieldHistory(match);
            Check(!History(capture, cursor).Any(e => SnapshotEntity(e) == actor.Id && !string.IsNullOrEmpty(SnapshotName(e))),
                "retired attachment never resurfaces after spell resolves");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS spell attachment draw/rehide/redraw/opponent play lifecycle");
    }
}
