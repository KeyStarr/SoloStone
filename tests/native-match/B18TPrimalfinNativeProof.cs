using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class Program
{
    [GroupProof("native-b18-t-primalfin", ProofKind.Native)]
    private static void B18TPrimalfinNative()
    {
        foreach (bool opponent in new[] { false, true })
        {
            var game = new SabberStoneCore.Model.Game(new GameConfig {
                RandomSeed = 17, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                History = true, Logging = false, FormatType = FormatType.FT_STANDARD,
                Player1HeroClass = CardClass.PRIEST, Player2HeroClass = CardClass.PRIEST,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            game.StartGame();
            foreach (var owner in new[] { game.Player1, game.Player2 })
                foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
            if (opponent) Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "opponent turn setup");
            var p = game.CurrentPlayer; p.BaseMana = 10;
            var capture = new Capture(); var match = new NativeMatch(capture.Send);
            typeof(NativeMatch).GetProperty(nameof(NativeMatch.Game)).SetValue(match, game);
            void Flush() { FlushHistory(match); game.PowerHistory.Last.Clear(); }
            void Play(IPlayable card, ICharacter target = null) {
                p.UsedMana = 0; Check(game.Process(PlayCardTask.Any(p, card, target)), "native play " + card.Card.Id); Flush();
            }
            Flush();
            var primalfin = Generic.DrawCard(p, Cards.FromId("TLC_251")); Play(primalfin);
            var pending = p.AppliedEnchantments.Single(e => e.Card.Id == "TLC_251e");
            Check(NativeHistory(capture).Any(row => row.HasShowEntity && row.ShowEntity.Entity == pending.Id
                && row.ShowEntity.Name == "TLC_251e" && row.ShowEntity.Tags.Any(t => t.Name == (int)GameTag.ATTACHED && t.Value == p.Id)),
                "retail-shaped public player enchantment");
            // A real Holy hand play sets the previous-own-turn window without private setup tags.
            Play(Generic.DrawCard(p, Cards.FromId("TLC_816")));
            Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)) && game.Process(EndTurnTask.Any(game.CurrentPlayer)), "next own turn");
            Flush();
            var first = Generic.DrawCard(p, Cards.FromId("TLC_816")); var second = Generic.DrawCard(p, Cards.FromId("TLC_816")); Flush();
            Check(first.Cost == 0 && second.Cost == 0, "two pure doubled cost previews: opponent=" + opponent + " costs=" + first.Cost + "/" + second.Cost + " match=" + KindredRules.Match(first) + " pending=" + p.AppliedEnchantments.Count(e => e.Card.Id == "TLC_251e") + " hand=" + p.HandZone.Count);
            if (!opponent) Check(match.SnapshotVisibleTransportTags(second)[(int)GameTag.COST] == 0, "friendly effective COST zero");
            Play(first);
            Check(second.Cost == 2 && p.AppliedEnchantments.All(e => e.Card.Id != "TLC_251e"), "consumed modifier restores sibling cost");
            if (!opponent) Check(match.SnapshotVisibleTransportTags(second)[(int)GameTag.COST] == 2, "friendly effective COST restored");
            Check(NativeHistory(capture).Any(row => row.HasTagChange && row.TagChange.Entity == pending.Id
                && row.TagChange.Tag == (int)GameTag.ZONE && row.TagChange.Value == (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME), "consumption retires native actor");
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            Play(Generic.DrawCard(p, Cards.FromId("TLC_440")), p.Opponent.Hero);
            Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)) && game.Process(EndTurnTask.Any(game.CurrentPlayer)), "next Frost window");
            foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
            Play(Generic.DrawCard(p, Cards.FromId("TLC_251")));
            int deck = p.DeckZone.Count, damage = p.Opponent.Hero.Damage;
            Play(Generic.DrawCard(p, Cards.FromId("TLC_440")), p.Opponent.Hero);
            Check(p.HandZone.Count == 3 && p.DeckZone.Count == deck - 3 && p.Opponent.Hero.Damage == damage + 4,
                "active clause draws three private cards with one damage event");
            var hidden = game.IdEntityDic.Values.Where(e => e.Zone == e.Controller.DeckZone || opponent && e.Zone == p.HandZone)
                .Select(e => e.Id).ToHashSet();
            foreach (var row in NativeHistory(capture))
            {
                foreach (var entity in new[] { row.FullEntity, row.ShowEntity, row.ChangeEntity }.Where(e => e != null))
                    if (hidden.Contains(entity.Entity)) Check(string.IsNullOrEmpty(entity.Name) && HasOnlyHiddenTags(entity.Tags), "bonus draws/opponent hand stay anonymous");
                if (row.HasTagChange && hidden.Contains(row.TagChange.Entity)) Check(IsPublicHiddenTag(row.TagChange.Tag), "hidden cards have no cost/identity delta");
            }
            Check(p.Choice == null && !game.Resolution.HasSuspendedEffect, "no pending decision or stall");
            capture.SerializeAll();
        }
        Console.WriteLine("PASS Primalfin native actor, pure costs, consumption and both-seat hidden draw privacy");
    }
}
