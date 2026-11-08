using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
    public enum DiscoverEntitySource { Deck, EnemyDeckMinions, EnemyHand, EnemyHandGuess, EnemyDeck }
    public enum DiscoverEntityAction { Select, Draw, PutOnTop }
    public enum RejectedOfferAction { Keep, Bottom, Destroy }

    /// <summary>Private display actors bind a saved choice to originals without revealing their identities.</summary>
    public static class DiscoverRealRules
    {
        public const GameTag BoundEntity = (GameTag)32400;
        public const GameTag CorrectAnswer = (GameTag)32401;
        public const GameTag Budget = (GameTag)32402;
        public const GameTag WatchedDefinition = (GameTag)32403;
        public const GameTag WatchedController = (GameTag)32404;
        public const GameTag WatchedTurn = (GameTag)32405;
        public const GameTag RetainedFirst = (GameTag)32406, RetainedSecond = (GameTag)32407,
            Residence = (GameTag)32408, WatchedEntity = (GameTag)32409, Pending = (GameTag)32413;
        // History-only completion pulse, not saved controller state or a selected-card link.
        public const GameTag CompletionEvent = (GameTag)32414;
        public static bool IsInternalTag(GameTag tag) => (int)tag >= 32400 && (int)tag <= 32414;

        public static void RedactSuspiciousOffers(Game game)
        {
            // Runs on every search clone: walk the id slots instead of copying Values (the list itself is not changed).
            var entities = game.IdEntityDic;
            for (int id = 0, n = entities.Capacity; id < n; id++)
            {
                var actor = entities.At(id);
                if (actor != null) CardSets.Client.Sets.Watfin.RedactForFairView(actor);
            }
        }

        public static Kettle.PowerHistoryEntity ProjectOfferForNative(Kettle.PowerHistoryEntity snapshot)
        {
            if (snapshot.Name != "JAIL_EVENT_100t" || !snapshot.Tags.TryGetValue(GameTag.MODULAR_ENTITY_PART_1,out int originalId)) return snapshot;
            var original = Cards.FromAssetId(originalId);
            var tags = new Dictionary<GameTag,int>(snapshot.Tags);
            tags.Remove(GameTag.MODULAR_ENTITY_PART_1);
            tags[GameTag.COLLECTIBLE] = original[GameTag.COLLECTIBLE];
            if (tags.TryGetValue(GameTag.OVERRIDECARDNAME,out int name) && name==originalId) tags.Remove(GameTag.OVERRIDECARDNAME);
            return new Kettle.PowerHistoryEntity { Id=snapshot.Id, Name=original.Id, Tags=tags };
        }

        public static void PrepareDiscover(IEntity source, IPlayable offer)
        {
            offer[Generic.PrivateDecisionOfferTag] = source.Controller.PlayerId;
            if (source.Controller.Choice.HistoryDiscoverId == 0) source.Game.Facts.MarkDiscover(source.Controller.Choice);
        }

        internal static bool IsRefresh(Choice choice, int selected) =>
            choice.Controller.Game.IdEntityDic[selected].Card.Id == "JAIL_319t"
            && choice.Controller.Game.IdEntityDic[choice.SourceId].Card.Id == "JAIL_319";

        internal static void Completed(Choice choice, int selected)
        {
            AutoPlayRules.CaptureDiscover(choice, selected);
            Completed(choice.Controller, choice.Controller.Game.IdEntityDic[selected]);
        }

        internal static void CompletedAll(Controller c, IEntity source, Card[] selected)
        {
            c.Game.Facts.DiscoverAll(c.Id, selected);
            AutoPlayRules.ReserveDiscoverCharge(c, source.Id);
        }

        internal static void Completed(Controller c, IPlayable chosen)
        {
            AutoPlayRules.AdvanceDiscoverQuest(c);
            if (chosen[BoundEntity] != 0) chosen = c.Game.IdEntityDic[chosen[BoundEntity]];
            foreach (var vault in c.BoardZone.Where(m => m.Card.Id == "TLC_483" && !m.IsSilenced && !m.IsDormant).OrderBy(m => m.OrderOfPlay).ToArray())
                vault.Power.PowerTask.Process(c.Game, c, vault, chosen);
        }

        private static bool IsMap(Enchantment e) => e.Card.Id == "TLC_515e" || e.Card.Id == "TLC_464e";
        private static Enchantment[] Maps(Controller c) => c.AppliedEnchantments?.Where(IsMap).ToArray() ?? Array.Empty<Enchantment>();
        private static int ResidenceId(IPlayable p, Zone zone) => checked((int)p.Game.HistoryFacts.Query.Residences(p.Id, zone).LastOrDefault().EntrySequence);
        private static IPlayable[] Retained(IEntity watch) => new[] { watch[RetainedFirst], watch[RetainedSecond] }
            .Where(id => id != 0).Select(id => watch.Game.IdEntityDic[id]).ToArray();

        internal static void RetainMap(IEntity source, Choice choice, int selected, IPlayable chosen)
        {
            if (source.Card.Id != "TLC_515" && source.Card.Id != "TLC_464" || chosen.Zone != chosen.Controller.HandZone) return;
            var c = chosen.Controller;
            var rejected = choice.Choices.Where(id => id != selected).ToArray();
            if (rejected.Length == 0) return;
            var watch = Enchantment.GetInstance(c, (IPlayable)source, c, Cards.FromId(source.Card.Id + "e"));
            watch[WatchedEntity] = chosen.Id; watch[WatchedDefinition] = chosen.Card.AssetId;
            watch[WatchedTurn] = c.Game.Turn; watch[Residence] = ResidenceId(chosen, Zone.HAND);
            watch[RetainedFirst] = rejected[0];
            if (rejected.Length > 1) watch[RetainedSecond] = rejected[1];
            foreach (var offer in Retained(watch)) if (offer[BoundEntity] != 0) {
                var original = c.Game.IdEntityDic[offer[BoundEntity]];
                offer[Residence] = ResidenceId(original, Zone.DECK);
                offer[WatchedDefinition] = original.Card.AssetId;
            }
        }

        internal static void ChangingDefinition(IPlayable entity)
        {
            InfinityRules.ChangingDefinition(entity);
            foreach (var c in new[] { entity.Controller, entity.Controller.Opponent })
                foreach (var watch in Maps(c)) {
                    if (watch[Pending] == 0 && watch[WatchedEntity] == entity.Id) watch[WatchedDefinition] = 0;
                    foreach (var offer in Retained(watch))
                        if (offer[BoundEntity] == entity.Id) offer[WatchedDefinition] = 0;
                }
        }

        internal static void PlayCommitted(IPlayable played)
        {
            var c = played.Controller;
            foreach (var watch in Maps(c))
                if (watch[Pending] == 0 && watch[WatchedEntity] == played.Id && watch[WatchedTurn] == c.Game.Turn
                    && watch[WatchedDefinition] == played.Card.AssetId && watch[Residence] == ResidenceId(played, Zone.HAND))
                    watch[Pending] = 1;
        }

        internal static void PlayCompleted(IPlayable played)
        {
            var c = played.Controller;
            var play = c.Game.HistoryFacts.Query.LatestPlay(played.Id);
            if (play == null) return;
            CardSets.Client.Sets.TimelooperToki.Played(c, played, play);
            foreach (var watch in Maps(c).Where(w => w[WatchedEntity] == played.Id && w[Pending] == 1)) {
                if (watch.Card.Id == "TLC_464e" && c.Game.IdEntityDic[played.Id] is Minion m && m.IsDormant) watch.Remove();
                else watch[Pending] = 2;
            }
            DrainMaps(c);
        }

        internal static void DrainMaps(Controller c)
        {
            if (c.Choice != null || c.Game.Resolution.HasSuspendedEffect || c.Game.State == State.COMPLETE) return;
            var watch = Maps(c).FirstOrDefault(w => w[Pending] == 2);
            if (watch == null) return;
            watch.Remove();
            watch.Power.PowerTask.Process(c.Game, c, watch, null);
        }

        public static Card[] RetainedOffers(Game game, Controller c, IEntity watch, TaskStack stack)
        {
            var offers = Retained(watch).Where(offer => watch.Card.Id != "TLC_515e" ||
                game.IdEntityDic.TryGetValue(offer[BoundEntity], out var original) && original.Zone == c.DeckZone
                && original.Card.AssetId == offer[WatchedDefinition] && ResidenceId(original, Zone.DECK) == offer[Residence]).ToArray();
            // Saved proxies preserve original order. New actors are private presentation only.
            return offers.Select(p => p.Card).ToArray();
        }

        public static void PrepareRetained(IEntity watch, IPlayable offer)
        {
            offer[Generic.PrivateDecisionOfferTag] = watch.Controller.PlayerId;
            var old = Retained(watch).First(p => p.Card.Id == offer.Card.Id);
            offer[BoundEntity] = old[BoundEntity];
            CopyDisplay(offer, old[BoundEntity] == 0 ? old : watch.Game.IdEntityDic[old[BoundEntity]]);
        }

        public static void DeliverRetained(Game game, Controller c, IEntity watch, IPlayable target, TaskStack stack)
        {
            var chosen = stack.Playables.LastOrDefault();
            if (chosen == null) return;
            if (watch.Card.Id == "TLC_515e") {
                var original = game.IdEntityDic[chosen[BoundEntity]];
                if (original.Zone == c.DeckZone) Generic.Draw(c, original);
            } else Generic.AddHandPhase(c, c.SetasideZone.Remove(chosen));
        }

        internal static bool TryReplaceNormalDraw(Controller controller)
        {
            if (controller.DeckZone.IsEmpty) return false;
            var effect = controller.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == "CATA_591e");
            if (effect == null) return false;
            effect.Power.PowerTask.Process(controller.Game, controller, effect, null);
            return true;
        }

        internal static void ControllerEnd(Controller controller)
        {
            foreach (var watch in Maps(controller)) watch.Remove();
            foreach (var owner in new[] { controller, controller.Opponent })
                foreach (var watch in owner.AppliedEnchantments?.Where(e => e[WatchedDefinition] != 0
                    && e[WatchedController] == controller.Id && e[WatchedTurn] <= controller.Game.HistoryFacts.Query.TurnsTaken(controller.Id)).ToArray()
                    ?? Array.Empty<Enchantment>()) watch.Remove();
        }

        internal static IPlayable[] Sample(Controller chooser, DiscoverEntitySource source, int count)
        {
            var cards = (source == DiscoverEntitySource.Deck ? chooser.DeckZone.AsEnumerable() :
                source == DiscoverEntitySource.EnemyDeckMinions ? chooser.Opponent.DeckZone.Where(p => p is Minion) :
                source == DiscoverEntitySource.EnemyDeck ? chooser.Opponent.DeckZone.AsEnumerable() :
                chooser.Opponent.HandZone.AsEnumerable()).ToList();
            bool distinct = source == DiscoverEntitySource.Deck || source == DiscoverEntitySource.EnemyDeckMinions || source == DiscoverEntitySource.EnemyDeck;
            if (!distinct) return Take(cards, count, chooser.Game).ToArray();
            var groups = cards.GroupBy(p => p.Card.Id, StringComparer.Ordinal).Select(g => g.ToList()).ToList();
            return Take(groups, count, chooser.Game).Select(g => g.Count == 1 ? g[0] : g[chooser.Game.Random.Next(g.Count)]).ToArray();
        }

        // Short pools expose every candidate; oversubscribed pools sample uniformly without replacement.
        internal static List<T> Take<T>(List<T> candidates, int count, Game game)
        {
            if (candidates.Count <= count) return candidates;
            var result = new List<T>();
            while (result.Count < count)
            {
                int index = game.Random.Next(candidates.Count);
                result.Add(candidates[index]); candidates.RemoveAt(index);
            }
            return result;
        }

        internal static bool Open(Controller chooser, IEntity source, DiscoverEntitySource origin, int count,
            OfflinePoolFilter? fallback, int? recipeDbfId)
        {
            var game = chooser.Game;
            using var offersScope = game.Facts.ChoiceOffers();
            var candidates = new List<(Card Card, int Bound, bool Correct, IPlayable Display)>();
            var originals = Sample(chooser, origin, origin == DiscoverEntitySource.EnemyHandGuess ? 1 : count);
            foreach (var original in originals)
                candidates.Add((original.Card, origin == DiscoverEntitySource.EnemyHandGuess ? 0 : original.Id,
                    origin == DiscoverEntitySource.EnemyHandGuess, origin == DiscoverEntitySource.EnemyHandGuess ? null : original));
            if (origin == DiscoverEntitySource.EnemyHandGuess)
            {
                var handNames = new HashSet<string>(chooser.Opponent.HandZone.Select(p => p.Card.Name), StringComparer.Ordinal);
                var decoys = chooser.Opponent.DeckZone.Where(p => !handNames.Contains(p.Card.Name))
                    .GroupBy(p => p.Card.Name, StringComparer.Ordinal).Select(g => g.First()).ToList();
                foreach (var decoy in Take(decoys, count - candidates.Count, game))
                    candidates.Add((decoy.Card, 0, false, null));
                if (candidates.Count < count)
                {
                    if (!fallback.HasValue || game.LegacyPoolReplay || !CardSets.Client.OfflinePoolDeclarations.Cards.Any(d =>
                        d.DbfId == (recipeDbfId ?? source.Card.AssetId) && d.Pools.Any(p => p.Filter == fallback.Value)))
                        throw new InvalidOperationException("Guess fallback requires a declared offline pool.");
                    var names = new HashSet<string>(candidates.Select(p => p.Card.Name).Concat(handNames), StringComparer.Ordinal);
                    var fallbackCards = OfflineCardPools.Candidates(game, chooser.HeroClass, fallback.Value, source.Card.AssetId, sourceClassMask: OfflineCardPools.PrintedClassMask(source.Card))
                        .Where(c => !names.Contains(c.Name)).GroupBy(c => c.Name, StringComparer.Ordinal).Select(g => g.First()).ToList();
                    foreach (var card in Take(fallbackCards, count - candidates.Count, game)) candidates.Add((card, 0, false, null));
                }
                // Randomize before allocating IDs: creation order must not identify the answer either.
                if (candidates.Count > 1) candidates.Shuffle(game.Random);
            }
            if (candidates.Count == 0) return false;
            var offers = candidates.Select(p => Proxy(chooser, source, p.Card, p.Bound, p.Correct, p.Display)).ToList();
            Generic.CreateChoice(chooser, source, ChoiceType.GENERAL, ChoiceAction.STACK, offers.Select(p => p.Id).ToList());
            if (origin == DiscoverEntitySource.Deck || origin == DiscoverEntitySource.EnemyDeckMinions || origin == DiscoverEntitySource.EnemyDeck)
                game.Facts.MarkDiscover(chooser.Choice);
            return true;
        }

        private static IPlayable Proxy(Controller chooser, IEntity source, Card card, int bound = 0, bool correct = false, IPlayable display = null)
        {
            var proxy = Entity.FromCard(chooser, card, new EntityData {
                [Generic.PrivateDecisionOfferTag] = chooser.PlayerId, [BoundEntity] = bound,
                [CorrectAnswer] = correct ? 1 : 0,
                [GameTag.CREATOR] = source.Id, [GameTag.DISPLAYED_CREATOR] = source.Id
            }, chooser.SetasideZone);
            CopyDisplay(proxy,display);
            return proxy;
        }

        internal static void CopyDisplay(IPlayable proxy, IPlayable display)
        {
            if (display == null) return;
            var card = proxy.Card;
            proxy.Cost = display.Cost;
            if (proxy is Minion minion && display is Minion original)
            {
                minion.AttackDamage = original.AttackDamage;
                minion.BaseHealth = original.BaseHealth;
            }
            foreach (var tag in new[] { GameTag.TAUNT, GameTag.CHARGE, GameTag.RUSH, GameTag.WINDFURY,
                GameTag.DIVINE_SHIELD, GameTag.POISONOUS, GameTag.LIFESTEAL, GameTag.STEALTH,
                GameTag.DEATHRATTLE, GameTag.SPELLPOWER, GameTag.CANT_BE_TARGETED_BY_SPELLS,
                GameTag.CANT_BE_TARGETED_BY_HERO_POWERS, GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2 })
                if (display[tag] != card[tag]) proxy[tag] = display[tag];
        }

        internal static bool Eligible(Controller chooser, DiscoverEntitySource origin, int selected)
        {
            if (!chooser.Game.IdEntityDic.TryGetValue(selected, out var proxy) || proxy.Controller != chooser
                || proxy.Zone != chooser.SetasideZone) return false;
            if (origin == DiscoverEntitySource.EnemyHandGuess) return true;
            if (!chooser.Game.IdEntityDic.TryGetValue(proxy[BoundEntity], out var real)) return false;
            return origin == DiscoverEntitySource.Deck ? real.Zone == chooser.DeckZone :
                origin == DiscoverEntitySource.EnemyDeck ? real.Zone == chooser.Opponent.DeckZone :
                origin == DiscoverEntitySource.EnemyDeckMinions ? real.Zone == chooser.Opponent.DeckZone && real is Minion :
                real.Zone == chooser.Opponent.HandZone;
        }

        internal static int Resolve(Controller chooser, IEntity source, Choice choice, int selected,
            DiscoverEntitySource origin, DiscoverEntityAction action, RejectedOfferAction rejected, string discount)
        {
            var game = chooser.Game;
            if (origin == DiscoverEntitySource.EnemyHandGuess) return selected;
            var real = game.IdEntityDic[game.IdEntityDic[selected][BoundEntity]];
            var deck = origin == DiscoverEntitySource.EnemyDeckMinions || origin == DiscoverEntitySource.EnemyDeck ? chooser.Opponent.DeckZone : chooser.DeckZone;
            var others = choice.Choices.Where(id => id != selected).Select(id => game.IdEntityDic[game.IdEntityDic[id][BoundEntity]])
                .Where(p => p.Zone == deck).ToList();
            if (action == DiscoverEntityAction.Draw)
            {
                // Apply draw discounts before hand-state processing can retire the chosen entity.
                using var handEntry = chooser.HandZone.DeferCardStateEntry();
                Generic.Draw(chooser, real);
                if (discount != null && real.Zone == chooser.HandZone)
                    Generic.AddEnchantmentBlock(game, Cards.FromId(discount), (IPlayable)source, real);
            }
            else if (action == DiscoverEntityAction.PutOnTop) deck.PutOnTop(real);
            if (rejected == RejectedOfferAction.Bottom)
            {
                if (others.Count > 1) others.Shuffle(game.Random);
                foreach (var other in others) if (other.Zone == deck) { deck.Remove(other); deck.Add(other, 0); }
            }
            else if (rejected == RejectedOfferAction.Destroy)
                foreach (var other in others) if (other.Zone == deck)
                {
                    if (!DrawReplacementRules.DestroyOffboard(other)) other.Controller.GraveyardZone.Add(deck.Remove(other));
                }
            RetainMap(source, choice, selected, real);
            return real.Id;
        }
    }
}
