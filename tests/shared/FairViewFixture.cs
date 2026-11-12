using EngineUnknown = SabberStoneCore.Model.Entities.Unknown;
using EngineGame = SabberStoneCore.Model.Game;
using System;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static QuestNativeProof;
using SabberStoneCore.Actions;

// Fair search over own-deck offers (Dark Gift deck Discover, Tracking) and effects that read hidden cards.
// Engine-only helpers of the fair-view proofs. FairViewHiddenOfferProof is partial: native-match has Run, native-match-logic has
// CanonicalPrivacyAssertions, and both link this file.
internal static partial class FairViewHiddenOfferProof
{
    private static EngineGame SearchGame(FairBotState state) => (EngineGame)typeof(FairBotState)
        .GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(state);
    // Private slot IDs are canonicalized: validate residences and the reachable graph, not old IDs alone.
    internal static void AssertHiddenActors(EngineGame source, EngineGame view, int hiddenPlayer, params int[] privateIds)
    {
        var live = hiddenPlayer == 1 ? source.Player1 : source.Player2;
        var hidden = hiddenPlayer == 1 ? view.Player1 : view.Player2;
        Check(hidden.HandZone.Count == live.HandZone.Count && hidden.DeckZone.Count == live.DeckZone.Count,
            "private live hand/deck slots cannot disappear");
        bool Anonymous(IPlayable actor) => actor is EngineUnknown && string.IsNullOrEmpty(actor.Card.Id)
            && actor.ChooseOnePlayables == null && actor.AppliedEnchantments?.Any() != true
            && actor.NativeTags.Keys.All(tag => tag == GameTag.ENTITY_ID || tag == GameTag.CONTROLLER
                || tag == GameTag.ZONE || tag == GameTag.ZONE_POSITION);
        Check(hidden.HandZone.All(Anonymous) && hidden.DeckZone.All(Anonymous),
            "canonical private hand/deck actors must stay Unknown without descendants, buffs or links");
        foreach (int id in privateIds)
        {
            var original = source.IdEntityDic[id];
            if (original.Zone == live.HandZone)
                Check(Anonymous(hidden.HandZone[original.ZonePosition]), "canonical held actor is hidden");
            else if (original.Zone == live.DeckZone)
                Check(hidden.DeckZone.All(Anonymous), "canonical deck actors are hidden");
            else
            {
                Check(original.Zone?.Type == SabberStoneCore.Enums.Zone.SETASIDE
                    || original[GameTag.ZONE] == (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME
                    || original is Enchantment, "only retired/private offboard actors may be absent");
                Check(!view.IdEntityDic.TryGetValue(id, out var retired) || retired is EngineUnknown,
                    "retired private actor must be absent or Unknown");
            }
        }
        var privateGraph = new System.Collections.Generic.HashSet<int>(privateIds
            .Concat(live.HandZone.Select(c => c.Id)).Concat(live.DeckZone.Select(c => c.Id)));
        int previous;
        do
        {
            previous = privateGraph.Count;
            foreach (var actor in source.IdEntityDic.Values)
            {
                if (privateGraph.Contains(actor.Id))
                    foreach (var branch in actor.ChooseOnePlayables ?? Array.Empty<IPlayable>())
                        if (branch != null) privateGraph.Add(branch.Id);
                if (actor is Enchantment attachment && privateGraph.Contains(attachment.Target.Id)
                    || privateGraph.Contains(actor[GameTag.PARENT_CARD])) privateGraph.Add(actor.Id);
            }
        } while (previous != privateGraph.Count);
        var seen = new System.Collections.Generic.HashSet<IEntity>();
        void Visit(IEntity actor)
        {
            if (actor == null || !seen.Add(actor)) return;
            if (actor is IPlayable playable)
            {
                Check(view.IdEntityDic.TryGetValue(playable.Id, out var registered) && ReferenceEquals(playable, registered),
                    "reachable actor bypasses canonical registry");
                if (privateGraph.Contains(playable.Id))
                    Check(playable is EngineUnknown || playable is Enchantment schedule
                        && schedule.Card.Id == "CATA_EVENT_001e" && schedule.Target == hidden
                        && schedule[GameTag.ATTACHED] == hidden.Id,
                        "private descendant/reference retains its definition or target");
                if (registered is EngineUnknown)
                    Check(string.IsNullOrEmpty(playable.Card.Id), "reference reveals hidden definition");
                foreach (var branch in playable.ChooseOnePlayables ?? Array.Empty<IPlayable>()) Visit(branch);
            }
            foreach (var attachment in actor.AppliedEnchantments ?? Enumerable.Empty<Enchantment>()) Visit(attachment);
            if (actor is Enchantment enchantment) { Visit(enchantment.Target); Visit(enchantment.Creator); }
        }
        foreach (var actor in view.IdEntityDic.Values) Visit(actor);
        foreach (var player in new[] { view.Player1, view.Player2 })
        {
            Visit(player);
            foreach (var actor in player.HandZone.Concat(player.DeckZone).Concat(player.SetasideZone)) Visit(actor);
        }
    }
    private static EngineGame Fixture()
    {
        var minions = new[] { "CS2_182", "CS2_120", "CS2_200", "CS2_119", "CS2_172" };
        var deck = Enumerable.Range(0, 30).Select(i => Cards.FromId(minions[i % minions.Length])).ToList();
        var game = new EngineGame(new GameConfig { RandomSeed = 23, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = true, Logging = false, FormatType = FormatType.FT_WILD,
            Player1Deck = deck, Player2Deck = deck.ToList() });
        game.StartGame();
        foreach (var p in new[] { game.Player1, game.Player2 })
        {
            foreach (var c in p.HandZone.ToArray()) { p.HandZone.Remove(c); p.SetasideZone.Add(c); }
            p.BaseMana = 10;
        }
        return game;
    }
}
