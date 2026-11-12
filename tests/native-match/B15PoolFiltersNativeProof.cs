using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using EngineGame = SabberStoneCore.Model.Game;
using EngineEntity = SabberStoneCore.Model.Entities.Entity;

internal static partial class Program
{
    [GroupProof("native-b15-pool-filters", ProofKind.Native)]
    private static void B15PoolFiltersNative()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (string id in new[] { "BE_298", "BE_703", "CATA_567", "CATA_621", "DINO_427", "EDR_888", "FIR_920", "TLC_824", "MEND_100", "JAIL_733", "JAIL_730", "JAIL_891" })
        {
            var game = new EngineGame(new GameConfig { RandomSeed=8, StartPlayer=1, Shuffle=false, SkipMulligan=true,
                History=true, Logging=false, EnableCorpses=true, FormatType=FormatType.FT_STANDARD,
                Player1HeroClass=CardClass.HUNTER, Player2HeroClass=CardClass.HUNTER,
                Player1Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList(),
                Player2Deck=Enumerable.Repeat(Cards.FromId("CS2_182"),30).ToList() });
            game.StartGame();
            if (opponent) Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "opponent turn");
            var p = game.CurrentPlayer;
            foreach (var owner in new[] { p, p.Opponent })
                foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            void Flush() { FlushHistory(match); game.PowerHistory.Last.Clear(); }
            Flush();
            p.BaseMana=10; p.UsedMana=0;
            var source = Generic.DrawCard(p, Cards.FromId(id));
            if (id == "CATA_567") Generic.SummonBlock(game,
                (Minion)EngineEntity.FromCard(p, Cards.FromId("CS2_182")), -1, null);
            if (id == "BE_703") EngineEntity.FromCard(p, Cards.FromId("BE_013"), zone: p.DeckZone);
            ICharacter target = null;
            if (id == "JAIL_891")
            {
                var victim = (Minion)EngineEntity.FromCard(p.Opponent, Cards.FromId("CS2_231"));
                Generic.SummonBlock(game, victim, -1, null); target=victim;
            }
            Check(game.Process(PlayCardTask.Any(p, source, target)), "native pool card play " + id);
            Flush();
            var offers = new System.Collections.Generic.List<int>();
            while (p.Choice != null)
            {
                offers.AddRange(p.Choice.Choices);
                var decision=game.GetPendingDecisions().Single();
                Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[]{p.Choice.Choices[0]}), out var error), "native pool answer " + error);
                Flush();
            }
            if (id == "BE_703" || id == "JAIL_733") { ((Minion)source).Destroy(); game.DeathProcessingAndAuraUpdate(); Flush(); }
            if (id == "JAIL_730") { Check(game.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "Scythe hero attack"); Flush(); }
            if (id == "MEND_100") { Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "Bulb opponent start"); Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "Bulb owner start"); Flush(); }
            if (id == "CATA_567")
            {
                var attachment=p.BoardZone.SelectMany(body => body.AppliedEnchantments ?? new System.Collections.Generic.List<Enchantment>())
                    .Single(e => e.Card.Id == "CATA_567e");
                var original = PayloadRules.Entries(attachment, 1).Single();
                match.SnapshotVisibleTransportTags(attachment).TryGetValue(2, out int ascendedTag);
                Check(attachment.CapturedCard.Id == "CS2_182" && original.Card.Id == "CS2_182"
                    && ascendedTag == original.Id && ascendedTag != original.Card.AssetId,
                    "public Ascended attachment never sends the original's dbfId (client builder 19 reads tag 2 as an entity id): it names the stored original entity");
            }
            if (id == "BE_703") Check(p.HandZone.Any(card => card.Card.Id == "BE_013"), "native tutor draws real God privately");
            if (new[] { "BE_298", "EDR_888", "FIR_920", "TLC_824" }.Contains(id))
                Check(offers.Count > 0, "native Discover actually produced private offers " + id);
            if (new[] { "JAIL_730", "JAIL_733", "JAIL_891" }.Contains(id))
                Check(p.HandZone.Any(card => card.Card.Id == "JAIL_732"), "native feeder actually generated Soul " + id);
            if (id == "CATA_621") Check(p.HandZone.OfType<SabberStoneCore.Model.Entities.Spell>().Any(card => card.IsAura), "native Triumph generated an Aura");
            if (id == "DINO_427") Check(p.HandZone.Any(card => card.Card.Id.StartsWith("DINO_")), "native Merchant generated a Mask");
            if (id == "MEND_100")
            {
                var bulb=p.HandZone.Single(card => card.Card.Id == "MEND_100t");
                if (!opponent) Check(match.SnapshotVisibleTransportTags(bulb)[(int)GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
                    "owner sees retail-shaped Bulb tier counter");
            }
            var hidden = game.IdEntityDic.Values.Where(e => e.Zone == e.Controller.DeckZone || PayloadRules.IsStored(e) && !PayloadRules.SharedWith(e, 1)
                || opponent && (e.Zone == p.HandZone || offers.Contains(e.Id))).Select(e => e.Id).ToHashSet();
            foreach (var e in game.IdEntityDic.Values.Where(e => hidden.Contains(e[GameTag.PARENT_CARD]))) hidden.Add(e.Id);
            foreach (var row in NativeHistory(capture))
            {
                foreach (var entity in new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity }.Where(e => e != null))
                {
                    Check(entity.Tags.All(t => !PayloadRules.IsInternalTag((GameTag)t.Name) && !Generic.IsDecisionInternalTag((GameTag)t.Name)), "pool internal tags filtered");
                    if (hidden.Contains(entity.Entity)) Check(string.IsNullOrEmpty(entity.Name) && HasOnlyHiddenTags(entity.Tags), id + " hidden generated/offered/payload identity stays private");
                }
                if (row.HasTagChange && hidden.Contains(row.TagChange.Entity)) Check(IsPublicHiddenTag(row.TagChange.Tag), id + " hidden tag change");
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS B15 pools native generation, private offers/hand/deck/payload, both seats, retail counters");
    }
}
