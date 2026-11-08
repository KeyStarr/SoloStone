using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks;

namespace SabberStoneCore.Model
{
    /// <summary>Per-game pairings live only in offer tags and spell entities.</summary>
    public static class DarkGifts
    {
        public const string PolicyVersion = "dark-gift/1";
        // Attachment-local cursor survives STACK continuation cloning/snapshot-v2 without a live reference.
        private const GameTag PendingSweetDreams = (GameTag)33000;
        /// <summary>Sample own-deck minions weighted by copies, rejecting repeated card IDs with game RNG.
        /// Like DiscoverTask.GetChoices for a deck pool, pools with at most count distinct IDs return
        /// their first entities in deck order without RNG; otherwise rejection draws consume RNG.</summary>
        public static IReadOnlyList<IPlayable> SampleDeckOffers(Controller controller, Func<IPlayable, bool> predicate, int count = 3)
        {
            if (controller == null) throw new ArgumentNullException(nameof(controller));
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));
            var pool = controller.DeckZone.Where(p => p is Minion && predicate(p)).ToArray();
            var distinct = pool.GroupBy(p => p.Card.Id, StringComparer.Ordinal).Select(g => g.First()).ToArray();
            if (count >= distinct.Length) return distinct;
            var result = new List<IPlayable>(count);
            while (result.Count < count)
            {
                var pick = pool[controller.Game.Random.Next(pool.Length)];
                if (result.All(p => p.Card.Id != pick.Card.Id)) result.Add(pick);
            }
            return result;
        }

        // New schema-3 recordings pin this policy; old gift-free goldens remain replayable.
        internal static void EnsureCompatible(Game game)
        {
            if (game.LegacyPoolReplay)
                throw new InvalidOperationException(PolicyVersion + " is unavailable in schema-2 legacy replay mode.");
            if (game.UnversionedDarkGiftReplay)
                throw new InvalidOperationException(PolicyVersion + " requires a recorded Dark Gift policy.");
        }

        public sealed class Gift
        {
            public string SpellId { get; }
            public int SpellDbfId { get; }
            public string EnchantmentId { get; }
            public int EnchantmentDbfId { get; }
            internal Gift(string spell, int dbf, string enchantment, int enchantmentDbf)
            { SpellId = spell; SpellDbfId = dbf; EnchantmentId = enchantment; EnchantmentDbfId = enchantmentDbf; }

            public bool IsEligible(IPlayable entity)
            {
                if (!(entity is Minion m)) return false;
                switch (SpellId)
                {
                    case "EDR_100t": return !m.HasLifeSteal;
                    case "EDR_100t1": return !m.CantBeTargetedBySpells && !m.CantBeTargetedByHeroPowers;
                    case "EDR_100t2": return m.AttackDamage >= 3;
                    case "EDR_100t3": return !m.HasTaunt;
                    case "EDR_100t6": return !m.HasCharge && m.AttackDamage >= 1;
                    case "EDR_100t7": return m[GameTag.BATTLECRY] > 0;
                    case "EDR_100t9": return m[GameTag.REBORN] == 0;
                    case "EDR_100t13": return !m.HasDivineShield && !m.HasWindfury;
                    default: return true;
                }
            }
        }

        public static IReadOnlyList<Gift> Registry { get; } = Array.AsReadOnly(new[] {
            new Gift("EDR_100t", 113796, "EDR_100te", 113803),
            new Gift("EDR_100t1", 113797, "EDR_100t1e", 113804),
            new Gift("EDR_100t2", 113798, "EDR_100t2e", 113805),
            new Gift("EDR_100t3", 113799, "EDR_100t3e", 113806),
            new Gift("EDR_100t5", 113801, "EDR_100t5e", 113808),
            new Gift("EDR_100t6", 113802, "EDR_100t6e", 113809),
            new Gift("EDR_100t7", 113882, "EDR_100t7e", 113883),
            new Gift("EDR_100t8", 113884, "EDR_100t8e1", 115355),
            new Gift("EDR_100t9", 113886, "EDR_100t9e", 113887),
            new Gift("EDR_100t13", 120225, "EDR_100t13e", 120226)
        });

        public static bool Has(IPlayable entity) =>
            entity?.AppliedEnchantments?.Any(e => e[GameTag.IS_NIGHTMARE_BONUS] == 1) == true;

        /// <summary>Attachment order, retaining duplicates.</summary>
        public static IReadOnlyList<Enchantment> GiftsOf(IPlayable entity) =>
            entity?.AppliedEnchantments?.Where(e => e[GameTag.IS_NIGHTMARE_BONUS] == 1)
                .OrderBy(e => e.OrderOfPlay).ThenBy(e => e.Id).ToArray() ?? Array.Empty<Enchantment>();

        // Private native tags retain independent grants even when History omits their attachments.
        private static GameTag KeywordBaseline(GameTag keyword) => (GameTag)(32000 + (int)keyword);

        private static int NativeKeyword(IEntity entity, GameTag keyword)
        {
            // These Boolean getters merge auras; their native values also live in cached fields.
            if (entity is Character character)
            {
                if (keyword == GameTag.TAUNT)
                    return (character._modifiedTaunt ?? character.Card.Taunt) ? 1 : 0;
                if (keyword == GameTag.CANT_BE_TARGETED_BY_SPELLS || keyword == GameTag.CANT_BE_TARGETED_BY_HERO_POWERS)
                    return (character._modifiedCantBeTargetedBySpells ?? character.Card.CantBeTargetedBySpells) ? 1 : 0;
            }
            return entity.NativeTags.TryGetValue(keyword, out int value) ? value : entity.Card[keyword];
        }

        internal static void TrackKeywordGrants(IEntity target, Card enchantment)
        {
            bool gift = enchantment[GameTag.IS_NIGHTMARE_BONUS] == 1;
            if (!gift && (!(target is IPlayable playable) || !Has(playable))) return;
            foreach (var effect in enchantment.Power.Enchant?.Effects ?? Array.Empty<Enchants.IEffect>())
            {
                if (!IsKeyword(effect)) continue;
                var key = KeywordBaseline(effect.Tag);
                if (gift)
                {
                    if (!target.NativeTags.ContainsKey(key))
                        target.NativeTags[key] = NativeKeyword(target, effect.Tag);
                }
                else if (target.NativeTags.TryGetValue(key, out int baseline))
                {
                    switch (effect.Operator)
                    {
                        case Enchants.EffectOperator.SET: baseline = effect.Value; break;
                        case Enchants.EffectOperator.ADD: baseline += effect.Value; break;
                        case Enchants.EffectOperator.SUB: baseline -= effect.Value; break;
                        case Enchants.EffectOperator.MUL: baseline *= effect.Value; break;
                    }
                    target.NativeTags[key] = baseline;
                }
            }
        }

        // Emit these new high tags explicitly; the legacy history filter belongs to other mechanics.
        internal static void SetTag(IPlayable entity, GameTag tag, int value)
        {
            if (entity[tag] == value) return;
            entity[tag] = value;
            if (entity.Game.History)
                entity.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(entity.Id, tag, value));
        }

        internal static int RecipeSource(Game game, int sourceId) =>
            game.IdEntityDic.TryGetValue(sourceId, out var source) && source.Card.Id == "EDR_102t"
                ? source[GameTag.CREATOR] : sourceId;

        internal static bool IsGiftChoice(Choice choice) => choice != null &&
            choice.Controller.Game.IdEntityDic.TryGetValue(choice.SourceId, out var source) && source.Card.Id == "EDR_102t";

        internal static bool Open(Controller controller, IEntity source, IReadOnlyList<IPlayable> offers,
            ChoiceAction action, ISimpleTask afterChoose)
        {
            EnsureCompatible(controller.Game);
            if (offers.Count == 0) return false;
            var offeredIds = new HashSet<int>(offers.Select(p => p.Id));
            for (var pending = controller.Choice; pending != null; pending = pending.NextChoice)
                if (IsGiftChoice(pending) && pending.Choices.Any(offeredIds.Contains))
                    throw new InvalidOperationException("Dark Gift offers overlap a pending gifted choice.");
            var executor = Entity.FromCard(controller, Cards.FromId("EDR_102t"),
                new EntityData { [GameTag.CREATOR] = source.Id }, controller.SetasideZone);
            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var offer in offers)
            {
                offer[Generic.PrivateDecisionOfferTag] = controller.PlayerId;
                var eligible = Registry.Where(gift => !used.Contains(gift.SpellId) && gift.IsEligible(offer)).ToArray();
                if (eligible.Length == 0)
                {
                    SetTag(offer, GameTag.DARK_GIFT_ENTITY, 0);
                    continue;
                }
                var gift = eligible[controller.Game.Random.Next(eligible.Length)];
                used.Add(gift.SpellId);
                var spell = Entity.FromCard(controller, Cards.FromId(gift.SpellId),
                    new EntityData { [GameTag.CREATOR] = executor.Id }, controller.SetasideZone);
                SetTag(offer, GameTag.DARK_GIFT_ENTITY, spell.Id);
            }
            // SourceId is the client executor; its CREATOR resolves AfterChoose's real source.
            // Existing deck IDs are offered directly, without moving or revealing any deck card.
            var choice = new Choice(controller) {
                SourceId = executor.Id, ChoiceType = ChoiceType.GENERAL, ChoiceAction = action,
                Choices = offers.Select(p => p.Id).ToList(), AfterChooseTask = afterChoose
            };
            if (controller.Choice == null) controller.Choice = choice;
            else
            {
                var tail = controller.Choice;
                while (tail.NextChoice != null) tail = tail.NextChoice;
                tail.NextChoice = choice;
            }
            return true;
        }

        internal static void ApplyChosen(Choice choice, IPlayable target)
        {
            if (!IsGiftChoice(choice)) return;
            int link = target[GameTag.DARK_GIFT_ENTITY];
            if (link == 0) return;
            var spell = target.Game.IdEntityDic[link];
            if (spell[GameTag.CREATOR] != choice.SourceId)
                throw new InvalidOperationException("Dark Gift offer does not belong to this executor.");
            Grant(target, spell, chosen: true);
        }

        public static bool Give(IPlayable target, IPlayable source)
        {
            EnsureCompatible(target.Game);
            if (target.Game != source.Game) throw new ArgumentException("Dark Gift source and target must belong to one game.");
            var eligible = Registry.Where(gift => gift.IsEligible(target)).ToArray();
            if (eligible.Length == 0) return false;
            var gift = eligible[target.Game.Random.Next(eligible.Length)];
            var spell = Entity.FromCard(target.Controller, Cards.FromId(gift.SpellId),
                new EntityData { [GameTag.CREATOR] = source.Id }, target.Controller.SetasideZone);
            Grant(target, spell);
            return true;
        }

        private static void Grant(IPlayable target, IPlayable spell, bool chosen = false)
        {
            EnsureCompatible(target.Game);
            var gift = Registry.Single(g => g.SpellId == spell.Card.Id);
            foreach (var attachment in GiftsOf(target)) attachment.NativeTags.Remove(PendingSweetDreams);
            Generic.AddEnchantmentBlock(target.Game, Cards.FromId(gift.EnchantmentId), spell, target);
            if (chosen && gift.SpellId == "EDR_100t8")
                GiftsOf(target).Last().NativeTags[PendingSweetDreams] = 1;
            // Copy/mirror attachment reconstruction never enters this original-grant event path.
            var game = target.Game;
            var previous = game.CurrentEventData;
            game.CurrentEventData = new EventMetaData(spell, target, gift.SpellDbfId);
            try { game.TriggerManager.Events.Raise(new DarkGiftGivenEvent(target, gift.SpellId)); }
            finally { game.CurrentEventData = previous; }
        }

        /// <summary>Gift-only backwards cleanup, with the Wallow Sweet Dreams exception.</summary>
        internal static bool RemoveForBackwardMove(IPlayable entity, Zone from, Zone to, bool preserveGiftsForWallowSweetDreams = false)
        {
            if (!(from == Zone.PLAY && (to == Zone.HAND || to == Zone.DECK) || from == Zone.HAND && to == Zone.DECK))
                return false;
            if (!preserveGiftsForWallowSweetDreams) Remove(entity);
            return true;
        }

        internal static void Remove(IPlayable entity, string except = null)
        {
            foreach (var attachment in GiftsOf(entity).Where(e => e.Card.Id != except))
            {
                var effects = attachment.Card.Power.Enchant?.Effects ?? Array.Empty<Enchants.IEffect>();
                foreach (var effect in effects)
                {
                    if (effect.Operator == Enchants.EffectOperator.SET && IsKeyword(effect))
                        effect.ChangeValue(entity.NativeTags.TryGetValue(KeywordBaseline(effect.Tag), out int baseline)
                            ? baseline : entity.Card[effect.Tag]).ApplyTo(entity);
                    else effect.RemoveFrom(entity);
                }
                if (entity.Game.History)
                    foreach (var effect in effects)
                        entity.Game.PowerHistory.Add(PowerHistoryBuilder.TagChange(entity.Id, effect.Tag, entity[effect.Tag]));
                attachment.Remove();
            }
        }

        internal static void ClearKeywordBaselines(IPlayable entity)
        {
            foreach (var tag in Registry.SelectMany(g => Cards.FromId(g.EnchantmentId).Power.Enchant.Effects)
                .Where(IsKeyword).Select(e => KeywordBaseline(e.Tag)).Distinct())
                entity.NativeTags.Remove(tag);
        }

        private static bool IsKeyword(Enchants.IEffect effect) =>
            effect.Tag != GameTag.ATK && effect.Tag != GameTag.HEALTH && effect.Tag != GameTag.COST;

        // Generic.Copy omits native tags; other copy paths already copy the current tag bag.
        internal static void CopyKeywords(IPlayable source, IPlayable target)
        {
            foreach (var tag in GiftsOf(source).SelectMany(e => e.Card.Power.Enchant.Effects)
                .Where(IsKeyword).Select(e => e.Tag).Distinct())
            {
                target[tag] = NativeKeyword(source, tag);
                var key = KeywordBaseline(tag);
                if (source.NativeTags.TryGetValue(key, out int baseline))
                    target.NativeTags[key] = baseline;
            }
        }

        internal static bool DoublesBattlecry(IPlayable entity) =>
            entity[GameTag.BATTLECRY] > 0 && GiftsOf(entity).Any(e => e.Card.Id == "EDR_100t7e");

        internal static void PreparePlayTriggers(IPlayable entity, bool playedFromHand)
        {
            // Hand removal deactivates attachments before PlayMinion validates its triggers.
            foreach (var e in GiftsOf(entity).Where(e => e.Card.Id == "EDR_100t5e"))
            {
                e.ActivatedTrigger?.Remove();
                if (playedFromHand) e.Card.Power.Trigger.Activate(e);
            }
        }

        internal static void LivingNightmare(Enchantment gift)
        {
            var minion = (Minion)gift.Target;
            var board = minion.Controller.BoardZone;
            if (minion.Zone != board || board.IsFull) return;
            // Lean dg12b: duplicate attachments summon in creation order to the right.
            int earlier = GiftsOf(minion).TakeWhile(e => e != gift).Count(e => e.Card.Id == "EDR_100t5e");
            Generic.Copy(minion.Controller, minion, minion, Zone.PLAY,
                initialize: copy => {
                    Generic.AddEnchantmentBlock(copy.Game, Cards.FromId("EDR_100t5e5"), gift, copy);
                    ((Minion)copy).Damage = 0;
                }, zonePosition: Math.Min(minion.ZonePosition + 1 + earlier, board.SlotCount));
        }

        /// <summary>Consume only the current choice's Sweet Dreams grant after hand entry, including STACK.</summary>
        public static void ResolveSweetDreams(IPlayable entity)
        {
            var pending = GiftsOf(entity).SingleOrDefault(e => e[PendingSweetDreams] == 1);
            if (pending == null) return;
            bool currentGrant = pending.Creator.Id == entity[GameTag.DARK_GIFT_ENTITY];
            // STACK may still await hand entry from SETASIDE or DECK; all other outcomes consume the grant.
            if (currentGrant && (entity.Zone == entity.Controller.SetasideZone || entity.Zone == entity.Controller.DeckZone)) return;
            pending.NativeTags.Remove(PendingSweetDreams);
            if (!currentGrant || entity.Zone != entity.Controller.HandZone || entity.Controller.DeckZone.IsFull) return;
            MoveSweetDreams(entity, entity.Card.Id == "EDR_487");
        }

        private static void MoveSweetDreams(IPlayable entity, bool preserveAllGifts)
        {
            if (entity.Zone == entity.Controller.DeckZone)
            {
                entity.Controller.DeckZone.PutOnTop(entity);
                return;
            }
            if (entity.Zone != entity.Controller.HandZone || entity.Controller.DeckZone.IsFull) return;
            // Lean dg11b: ordinary D6 cleanup removes other gifts, retaining Sweet Dreams itself.
            if (!preserveAllGifts) Remove(entity, "EDR_100t8e1");
            ((Entity)entity).PreserveDarkGiftsOnNextBackwardMove = true;
            entity.Controller.HandZone.Remove(entity);
            // Hand removal resets cost; reconstruct only the retained gifts' cost effects.
            foreach (var effect in GiftsOf(entity).SelectMany(e => e.Card.Power.Enchant.Effects)
                .Where(e => e.Tag == GameTag.COST)) effect.ApplyTo(entity);
            entity.Controller.DeckZone.PutOnTop(entity);
            HideInDeck(entity);
        }

        // A revealed card returning to DECK is hidden again, as Generic.ShuffleIntoDeck does.
        private static void HideInDeck(IPlayable entity)
        {
            if (entity.Game.History) entity.Game.PowerHistory.Add(PowerHistoryBuilder.HideEntity(entity));
        }

        /// <summary>A fresh, nonrecursive grant copy; caller owns Wallow's hand/deck eligibility.</summary>
        public static void Mirror(IPlayable wallow, string giftSpellId, IPlayable source)
        {
            EnsureCompatible(wallow.Game);
            if (wallow.Game != source.Game) throw new ArgumentException("Dark Gift source and target must belong to one game.");
            var gift = Registry.Single(g => g.SpellId == giftSpellId);
            var spell = Entity.FromCard(wallow.Controller, Cards.FromId(giftSpellId),
                new EntityData { [GameTag.CREATOR] = source.Id }, wallow.Controller.SetasideZone);
            Generic.AddEnchantmentBlock(wallow.Game, Cards.FromId(gift.EnchantmentId), spell, wallow);
            if (giftSpellId != "EDR_100t8") return;
            // A self-granted choice moves once after hand entry, never once here and again after draw.
            if (GiftsOf(wallow).Any(e => e[PendingSweetDreams] == 1 && e.Creator.Id == source.Id)) return;
            // Pinned dg7 (32.2): Wallow preserves every gift on this backwards move.
            MoveSweetDreams(wallow, preserveAllGifts: true);
        }

        internal sealed class Rebirth
        {
            internal Enchantment[] Enchantments;
            internal int Attack, Health, Cost;
            internal Dictionary<GameTag, int> Keywords;
        }

        internal static Rebirth CaptureRebirth(Minion minion)
        {
            if (minion[GameTag.REBORN] != 1 || minion[GameTag.HAS_BEEN_REBORN] != 0 ||
                !minion.RestoresRebornState && !GiftsOf(minion).Any(e => e.Card.Id == "EDR_100t9e")) return null;
            // Capture before GraveyardZone removes attachments, including their scripts/creator/expiry.
            var attachments = minion.AppliedEnchantments?.Where(e => e.Card.Id != "EDR_100t9e").ToArray() ?? Array.Empty<Enchantment>();
            var keywords = new[] { GameTag.TAUNT, GameTag.STEALTH, GameTag.CHARGE, GameTag.RUSH,
                GameTag.DIVINE_SHIELD, GameTag.WINDFURY, GameTag.LIFESTEAL, GameTag.POISONOUS, GameTag.SPELLPOWER,
                GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS };
            var tags = keywords.Concat(attachments.SelectMany(e => e.Card.Power.Enchant?.Effects ?? Array.Empty<Enchants.IEffect>())
                .Where(IsKeyword).Select(e => e.Tag)).Distinct().ToDictionary(t => t, t => NativeKeyword(minion, t));
            foreach (var tag in keywords.Select(KeywordBaseline))
                if (minion.NativeTags.TryGetValue(tag, out int baseline)) tags[tag] = baseline;
            return new Rebirth { Enchantments = attachments, Attack = minion._modifiedATK ?? minion.Card.ATK,
                Health = minion._modifiedHealth ?? minion.Card.Health, Cost = ((Playable)minion)._modifiedCost ?? minion.Card.Cost,
                Keywords = tags };
        }

        internal static void RestoreRebirth(Minion minion, Rebirth saved, Minion source)
        {
            foreach (var old in saved.Enchantments)
            {
                Generic.AddEnchantmentBlock(minion.Game, old.Card, old.Creator, minion,
                    old[GameTag.TAG_SCRIPT_DATA_NUM_1], old[GameTag.TAG_SCRIPT_DATA_NUM_2]);
                var copy = minion.AppliedEnchantments.Last();
                foreach (var tag in old.NativeTags)
                    if (tag.Key != GameTag.ENTITY_ID && tag.Key != GameTag.ATTACHED && tag.Key != GameTag.ZONE &&
                        tag.Key != GameTag.CONTROLLER && tag.Key != GameTag.CREATOR) copy[tag.Key] = tag.Value;
                copy.CapturedCard = old.CapturedCard;
                if (old.IsOneTurnActive) copy.ExpireAtEndOfTurn();
            }
            // Restore materialized values once; SET grants still renew consumed keywords such as Shield.
            var setKeywords = saved.Enchantments.SelectMany(e => e.Card.Power.Enchant?.Effects ?? Array.Empty<Enchants.IEffect>())
                .Where(e => IsKeyword(e) && e.Operator == Enchants.EffectOperator.SET).Select(e => e.Tag).ToHashSet();
            foreach (var pair in saved.Keywords)
                if (!setKeywords.Contains(pair.Key) && (pair.Value != 0 || minion.Card[pair.Key] == 0))
                    minion[pair.Key] = pair.Value;
            // Materialized stats also retain stat-only buffs omitted by the engine with History=false.
            minion.AttackDamage = saved.Attack;
            minion.BaseHealth = saved.Health;
            minion.Cost = saved.Cost;
            foreach (var pair in saved.Keywords.Where(p => (int)p.Key >= 32000))
                minion.NativeTags[pair.Key] = pair.Value;
            // History-off temporary buffs may have no attachment; retain their original expiry too.
            var expiry = minion.Game.OneTurnEffects.Where(p => p.entityId == source.Id).ToArray();
            minion.Game.OneTurnEffects.RemoveAll(p => p.entityId == minion.Id);
            foreach (var effect in expiry) minion.Game.OneTurnEffects.Add((minion.Id, effect.effect));
            // Lean dg13b: consume Reborn even if another preserved enchantment also grants it.
            minion[GameTag.REBORN] = 0;
            minion.Damage = 0;
        }
    }
}
