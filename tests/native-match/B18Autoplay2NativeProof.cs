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
using EngineEntity = SabberStoneCore.Model.Entities.Entity;
internal static partial class Program
{
    [GroupProof("native-b18-t-autoplay2", ProofKind.Native)]
    private static void B18Autoplay2Native()
    {
        foreach (bool opponent in new[] { false, true })
        foreach (int tier in new[] { 1, 2, 3 })
        foreach (bool reverse in new[] { false, true })
        {
            var game = new SabberStoneCore.Model.Game(new GameConfig {
                RandomSeed = 17, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
                Player1HeroClass = CardClass.MAGE, Player2HeroClass = CardClass.MAGE,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            if (opponent) Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "opponent seat");
            var p = game.CurrentPlayer; p.BaseMana = 10;
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            void Flush() { FlushHistory(match); game.PowerHistory.Last.Clear(); }
            Flush();
            string body = "TLC_100t" + tier;
            var first = Cards.FromId(body + (reverse ? "6" : "3"));
            var second = Cards.FromId(body + (reverse ? "3" : "6"));
            Check(!LocalNativeText.TryGetOverride(true, body, text => text, out _), "client builder owns composed location text");
            var card = CompositeLocationRules.Compose(Cards.FromId(body), first, second);
            var location = (BoardObject)EngineEntity.FromCard(p, card, zone: p.HandZone);
            Flush();
            var initial = NativeHistory(capture).Where(row => row.HasFullEntity && row.FullEntity.Entity == location.Id
                || row.HasShowEntity && row.ShowEntity.Entity == location.Id).ToArray();
            Check(initial.Length > 0, "crafted location has initial snapshot");
            if (opponent)
                Check(NativeHistory(capture).SelectMany(row => new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity })
                    .Where(e => e != null && e.Entity == location.Id).All(e => string.IsNullOrEmpty(e.Name)
                        && e.Tags.All(t => t.Name != (int)GameTag.MODULAR_ENTITY_PART_1 && t.Name != (int)GameTag.MODULAR_ENTITY_PART_2)), "opponent cannot inspect hidden recipe");
            else
                Check(SnapshotTag(initial[0], GameTag.MODULAR_ENTITY_PART_1) == first.AssetId
                    && SnapshotTag(initial[0], GameTag.MODULAR_ENTITY_PART_2) == second.AssetId, "first hand snapshot freezes ordered parts");
            Check(game.Process(PlayCardTask.Any(p, location)), "play composed location"); Flush();
            var revealed = NativeHistory(capture).First(row => row.HasShowEntity && row.ShowEntity.Entity == location.Id
                && !string.IsNullOrEmpty(row.ShowEntity.Name));
            Check(SnapshotTag(revealed, GameTag.MODULAR_ENTITY_PART_1) == first.AssetId
                && SnapshotTag(revealed, GameTag.MODULAR_ENTITY_PART_2) == second.AssetId, "first play SHOW freezes ordered parts");
            var visible = match.SnapshotVisibleTransportTags(location);
            Check(visible[(int)GameTag.MODULAR_ENTITY_PART_1] == first.AssetId && visible[(int)GameTag.MODULAR_ENTITY_PART_2] == second.AssetId, "retail modular text receives ordered dbfIds");
            var stone = (Weapon)EngineEntity.FromCard(p, Cards.FromId("TLC_460t"), zone: p.SetasideZone);
            AutoPlayRules.Play(stone, p.Hero);
            Check(game.Process(new ActivateLocationTask(p, location)), "Runic opens native choice");
            var offers = p.Choice.Choices.ToArray(); Flush();
            var decision = game.GetPendingDecisions().Single();
            Check(game.TryAnswer(new DecisionAnswer(decision.Id, p.Id, new[] { offers[0] }), out var error), "native answer " + error);
            Flush();
            Check(p.Choice == null && !game.Resolution.HasSuspendedEffect && location.Durability == 1 && p.Hero.Armor >= (tier == 1 ? 3 : tier == 2 ? 6 : 12), "composite plus Stone decisions cannot stall");
            var freshStone = (Weapon)EngineEntity.FromCard(p, Cards.FromId("TLC_460t"), zone: p.SetasideZone);
            AutoPlayRules.Play(freshStone, p.Hero);
            var source = EngineEntity.FromCard(p, Cards.FromId("CS2_023"), zone: p.SetasideZone);
            var secretOffers = new[] { "CS2_182", "EX1_287" }.Select(id => EngineEntity.FromCard(p, Cards.FromId(id), zone: p.SetasideZone)).ToArray();
            p.Choice = new Choice(p) { SourceId = source.Id, ChoiceType = ChoiceType.GENERAL, ChoiceAction = ChoiceAction.HAND,
                Choices = secretOffers.Select(e => e.Id).ToList() };
            foreach (var offer in secretOffers) DiscoverRealRules.PrepareDiscover(source, offer);
            Flush();
            decision = game.GetPendingDecisions().Single();
            Check(game.TryAnswer(new DecisionAnswer(decision.Id, p.Id, new[] { secretOffers[0].Id }), out error), "secret autoplay answer " + error);
            Flush();
            Check(p.SecretZone.Any(e => e.Card.Id == "EX1_287"), "unchosen Secret is installed");
            var installedSecrets = p.SecretZone.Where(e => e.Card.IsSecret).Select(e => e.Id).ToHashSet();
            if (opponent)
                foreach (int secret in installedSecrets) AssertHiddenSecretPlay(capture, 0, secret);
            offers = offers.Concat(secretOffers.Select(e => e.Id)).ToArray();
            var hidden = game.IdEntityDic.Values.Where(e => e.Zone == e.Controller.DeckZone || PayloadRules.IsStored(e)
                || opponent && (e.Zone == p.HandZone || e.Zone == p.SecretZone || offers.Contains(e.Id) && e.Zone == p.SetasideZone)).Select(e => e.Id).ToHashSet();
            foreach (var row in NativeHistory(capture))
            {
                foreach (var entity in new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity }.Where(e => e != null))
                {
                    Check(entity.Tags.All(t => !AutoPlayRules.IsInternalTag((GameTag)t.Name)), "internal autoplay tags filtered");
                    if (hidden.Contains(entity.Entity)) Check(string.IsNullOrEmpty(entity.Name) && HasOnlyHiddenTags(entity.Tags), "negative privacy: hidden originals/copies/offers");
                }
                if (row.HasTagChange)
                {
                    Check(!AutoPlayRules.IsInternalTag((GameTag)row.TagChange.Tag), "internal incremental tags filtered");
                    // The played Secret's type is public; stored payloads and unchosen offer originals stay private.
                    bool secretPresentation = installedSecrets.Contains(row.TagChange.Entity)
                        && (row.TagChange.Tag == (int)GameTag.SECRET && row.TagChange.Value == 1
                            || row.TagChange.Tag == (int)GameTag.CARDTYPE && row.TagChange.Value == (int)CardType.SPELL);
                    if (hidden.Contains(row.TagChange.Entity)) Check(IsPublicHiddenTag(row.TagChange.Tag) || secretPresentation,
                        $"negative privacy: no hidden payload delta entity={row.TagChange.Entity} tag={(GameTag)row.TagChange.Tag}");
                }
            }
            capture.SerializeAll();
        }
        Console.WriteLine("PASS b18 autoplay native composite text, nested completion and both-seat privacy");
    }
}
