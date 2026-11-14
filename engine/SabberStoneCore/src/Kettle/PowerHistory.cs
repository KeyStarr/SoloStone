#region copyright
// SabberStone, Hearthstone Simulator in C# .NET Core
// Copyright (C) 2017-2019 SabberStone Team, darkfriend77 & rnilva
//
// SabberStone is free software: you can redistribute it and/or modify
// it under the terms of the GNU Affero General Public License as
// published by the Free Software Foundation, either version 3 of the
// License.
// SabberStone is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU Affero General Public License for more details.
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Presentation;

namespace SabberStoneCore.Kettle
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
{
	public class PowerHistoryBuilder
	{
        // Older decision tags belong to frozen engine histories; native transport filters their full range.
        internal static Dictionary<GameTag, int> SnapshotTags(IDictionary<GameTag, int> tags) =>
            tags.Where(pair => !Actions.TurnQueueRules.IsInternalTag(pair.Key) && !Actions.RepeatRules.IsInternalTag(pair.Key) && pair.Key != Actions.OutcastRules.PlayedFromEdgeTag && !Actions.LeylineRules.IsInternalTag(pair.Key) && !Actions.ShatterRules.IsInternalTag(pair.Key) && !Actions.HandProgressRules.IsInternalTag(pair.Key)).ToDictionary(pair => pair.Key, pair => pair.Value);

		public static PowerHistoryCreateGame CreateGame(Game game, Controller[] players)
		{
			return new PowerHistoryCreateGame()
			{
				Game = new PowerEntity
				{
					Id = game.Id,
					Tags = SnapshotTags(game._data)
				},
				Players = new[]
						{
					new PowerPlayer
					{
						PlayerId = players[0].PlayerId,
						AccountId = 12718623,
						CardBack = 0,
						PowerEntity = new PowerEntity
						{
							Id = players[0].Id,
							Tags = SnapshotTags(players[0]._data)
						}
					},
					new PowerPlayer
					{
						PlayerId = players[1].PlayerId,
						AccountId = 18463223,
						CardBack = 0,
						PowerEntity = new PowerEntity
						{
							Id = players[1].Id,
							Tags = SnapshotTags(players[1]._data)
						}
					},
						}
			};
		}

		public static PowerHistoryTagChange TagChange(int id, GameTag tag, int value)
		{
			return new PowerHistoryTagChange()
			{
				EntityId = id,
				Tag = tag,
				Value = value
			};
		}

        public static IEnumerable<KeyValuePair<GameTag, int>> ControllerAuraState(Controller controller)
        {
            foreach (var tag in new[] { GameTag.TIMEOUT, GameTag.SPELLPOWER_DOUBLE, GameTag.SPELL_HEALING_DOUBLE,
                GameTag.HERO_POWER_DOUBLE, GameTag.HEALING_DOES_DAMAGE, GameTag.CHOOSE_BOTH, GameTag.SPELLS_COST_HEALTH,
                GameTag.EXTRA_BATTLECRIES_BASE, GameTag.EXTRA_END_TURN_EFFECT, GameTag.HERO_POWER_DISABLED,
                GameTag.ALL_HEALING_DOUBLE, GameTag.EXTRA_MINION_BATTLECRIES_BASE, GameTag.SPELLPOWER })
                yield return new KeyValuePair<GameTag, int>(tag, controller[tag]);
        }

        public static PowerHistoryResetGame ResetGame(Game game)
        {
            var create = CreateGame(game, new[] { game.Player1, game.Player2 });
            foreach (var player in create.Players)
            {
                var controller = game.ControllerById(player.PowerEntity.Id);
                foreach (var tag in ControllerAuraState(controller)) player.PowerEntity.Tags[tag.Key] = tag.Value;
                player.PowerEntity.Tags[(GameTag)291] = controller.CurrentSpellPower;
                // Pinned Entity.GetHealingBonus reads the accumulated player bonus, at this immutable reset boundary.
                int healing = Actions.HealingRules.Bonus(controller);
                if (healing != 0) player.PowerEntity.Tags[(GameTag)2333] = healing;
            }
            return new PowerHistoryResetGame { CreateGame = create, SourceIsRewind = true };
        }

		public static PowerHistoryFullEntity FullEntity(IPlayable playable, bool reset = false)
		{
            // Enchantments implement IPlayable directly; their wire state is the attachment tag map.
            if (playable is Enchantment enchantment)
            {
                var attachmentTags = SnapshotTags(playable.NativeTags);
                // Ordinary attachments keep these references outside NativeTags; reset must freeze them too.
                attachmentTags[GameTag.CARDTYPE] = (int)CardType.ENCHANTMENT;
                if (!attachmentTags.ContainsKey(GameTag.ATTACHED)) attachmentTags[GameTag.ATTACHED] = enchantment.Target?.Id ?? 0;
                if (!attachmentTags.ContainsKey(GameTag.CREATOR)) attachmentTags[GameTag.CREATOR] = enchantment.Creator?.Id ?? 0;
                return new PowerHistoryFullEntity { Entity = new PowerHistoryEntity {
                    Id = playable.Id, Name = playable.Card.Id, Tags = attachmentTags
                } };
            }
			var tags = SnapshotTags(((Entity)playable)._data);
			gameTagsEntities.ForEach(p => tags[p] = playable[p]);
            AddAuraDerivedTags(playable, tags);
            // Offer privacy is history provenance, not an extra gameplay tag.
            if (playable.Game.Facts.CreatingChoiceOffers && tags[GameTag.ZONE] == (int)Zone.SETASIDE)
                tags[Actions.Generic.PrivateDecisionOfferTag] = playable.Controller.PlayerId;
            AddModernSnapshotTags(playable, tags);
            AddLeylineTags(playable, tags);
            if (playable.Card.Shatter != null)
                foreach (var tag in new[] { (GameTag)4239, (GameTag)4240, GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2, (GameTag)401 }) tags[tag] = playable[tag];
            if (playable.Card.HandProgress != null) tags[GameTag.TAG_SCRIPT_DATA_NUM_1] = playable[GameTag.TAG_SCRIPT_DATA_NUM_1];
            int alternate = (int)Actions.CostRules.PaymentKind(playable);
            if (alternate != 0) tags[GameTag.CARD_ALTERNATE_COST] = alternate;
            else tags.Remove(GameTag.CARD_ALTERNATE_COST);
			if (playable is Character c)
			{
				tags[GameTag.ATK] = c.AttackDamage;
				tags[GameTag.HEALTH] = c.Health;
				tags[GameTag.DAMAGE] = c.Damage;
			}

            if (reset)
            {
                // Match ordinary snapshots: definition metadata (e.g. Smoldering IMMOLATING) is not live actor state.
                // Printed client fields use NativeMatch's normal projection; instance zeros still undo gameplay changes.
                foreach (var tag in playable.NativeTags.Keys)
                    if (tag != Actions.OutcastRules.PlayedFromEdgeTag) tags[tag] = playable[tag];
                // A scoped printing can change real state (e.g. text counters); freeze only its differences from the registry.
                Card registered = null;
                try { registered = Cards.FromId(playable.Card.Id); }
                catch (KeyNotFoundException) { }
                if (registered != null && !ReferenceEquals(registered, playable.Card))
                    foreach (var tag in playable.Card.Tags.Keys.Concat(registered.Tags.Keys).Distinct())
                        if (playable.Card[tag] != registered[tag] && tag != Actions.OutcastRules.PlayedFromEdgeTag)
                            tags[tag] = playable[tag];
                tags[GameTag.COST] = playable.Cost;
                if (playable.Card[Actions.HeraldRules.Keyword] > 0 || Actions.HeraldRules.IsSoldier(playable.Card))
                    tags[Actions.HeraldRules.Profile] = (int)Actions.HeraldRules.Class(playable.Controller, playable.Card);
                if (playable is Character character)
                {
                    tags[GameTag.ATK] = character.AttackDamage;
                    tags[GameTag.HEALTH] = character.BaseHealth;
                    tags[GameTag.DAMAGE] = character.Damage;
                    // Armor counts only gained Armor; a hero card that isn't its controller's hero shows its printed Armor (as NativeMatch.VisibleStats).
                    tags[GameTag.ARMOR] = character is Hero armorHero && !ReferenceEquals(armorHero, armorHero.Controller?.Hero)
                        ? armorHero[GameTag.ARMOR] : character.Armor;
                }
                if (playable is Minion minion) tags[GameTag.SPELLPOWER] = minion.SpellPower;
                if (playable is Weapon weapon)
                {
                    tags[GameTag.ATK] = weapon.AttackDamage;
                    tags[GameTag.DURABILITY] = weapon.Durability + weapon.Damage;
                }
                if (playable is BoardObject location)
                {
                    tags[GameTag.EXHAUSTED] = location.IsExhausted ? 1 : 0;
                    tags[GameTag.LOCATION_ACTION_COOLDOWN] = location[GameTag.LOCATION_ACTION_COOLDOWN];
                }
            }

			return new PowerHistoryFullEntity
			{
				Entity = new PowerHistoryEntity
				{
					Id = playable.Id,
					Name = playable.Card.Id,
					Tags = tags
				}
			};
		}

        private static void AddLeylineTags(IPlayable playable, Dictionary<GameTag, int> tags)
        {
            if (!Actions.LeylineRules.IsLeyline(playable.Card)) return;
            tags[GameTag.TAG_SCRIPT_DATA_NUM_1] = Actions.LeylineRules.Magnitude(playable);
            tags[GameTag.TAG_SCRIPT_DATA_NUM_2] = Actions.LeylineRules.Times(playable);
        }

        private static void AddModernSnapshotTags(IPlayable playable, Dictionary<GameTag, int> tags)
        {
            Actions.GeneratedDefinitionRules.CaptureTags(playable, tags);
            if (playable[GameTag.REWIND] != 0 || playable[GameTag.USED_REWIND] != 0)
            {
                tags[GameTag.REWIND] = playable[GameTag.REWIND];
                tags[GameTag.USED_REWIND] = playable[GameTag.USED_REWIND];
            }
            if (playable.Card[(GameTag)1192] != 0) tags[(GameTag)1192] = playable.Card[(GameTag)1192];
            if (!(playable is Spell) || !QuestDefinition.TryGet(playable.Card.AssetId, out var definition)) return;
            tags[GameTag.QUEST] = playable[GameTag.QUEST];
            tags[GameTag.QUEST_PROGRESS] = playable[GameTag.QUEST_PROGRESS];
            tags[GameTag.QUEST_PROGRESS_TOTAL] = playable[GameTag.QUEST_PROGRESS_TOTAL];
            tags[GameTag.QUEST_REWARD_DATABASE_ID] = playable[GameTag.QUEST_REWARD_DATABASE_ID];
            if (definition.Repeatable) tags[GameTag.REPEATABLE] = playable[GameTag.REPEATABLE];
        }

        // Freeze effective-vs-replay-fallback differences, including auras and printed-Elusive targeting.
        // ATK/HEALTH/CHARGE/LIFESTEAL/TAUNT/STEALTH/WINDFURY are already frozen by gameTagsEntities.
        // ELUSIVE is derived by transport from frozen targeting; CARD_COSTS_HEALTH uses the existing
        // CARD_ALTERNATE_COST projection. CANT_ATTACK has no aura indexer contribution; SPELLPOWER,
        // other printed keywords and effect/enchanted values use raw tags or definition fallback.
        // COST is already frozen; CANT_PLAY/CANT_ATTACK also derive from scoped lock/sleep attachments.
        // Dormant/Untouchable and weapon-inherited hero keywords add no further indexer contributions.
        private static readonly GameTag[] auraDerivedTags = {
            GameTag.CANT_BE_TARGETED_BY_SPELLS, GameTag.CANT_BE_TARGETED_BY_HERO_POWERS,
            GameTag.ECHO, GameTag.RUSH, GameTag.IMMUNE, GameTag.HEROPOWER_DAMAGE, GameTag.CANNOT_ATTACK_HEROES,
            GameTag.CANT_PLAY, GameTag.CANT_ATTACK
        };

        private static void AddAuraDerivedTags(IPlayable playable, Dictionary<GameTag, int> tags)
        {
            foreach (GameTag tag in auraDerivedTags)
            {
                // AuraEffects.Rush storage exists only on minions; other types retain raw/printed values.
                if (tag == GameTag.RUSH && !(playable is Minion)) continue;
                int effective = playable is Character character
                    && (tag == GameTag.CANT_BE_TARGETED_BY_SPELLS || tag == GameTag.CANT_BE_TARGETED_BY_HERO_POWERS)
                    ? (character.CantBeTargetedBySpellsNoCache ? 1 : 0) : playable[tag];
                int fallback = tags.TryGetValue(tag, out int value) ? value : playable.Card[tag];
                // Freeze only differences so ordinary snapshots keep their existing tag shape.
                if (effective != fallback) tags[tag] = effective;
            }
            if (!(playable is Minion)) return;
            Card registered;
            try { registered = Cards.FromId(playable.Card.Id); }
            catch (KeyNotFoundException) { return; }
            if (ReferenceEquals(playable.Card, registered)) return;
            // Scoped printings must survive reset without falling back to legacy races.
            foreach (var tag in Card.AdditionalRaceTags.Keys.Concat(new[] { GameTag.CARDRACE }))
                if (playable.Card[tag] != registered[tag]) tags[tag] = playable[tag];
        }

		private static List<GameTag> gameTagsEntities = new List<GameTag>()
		{
			GameTag.PREMIUM,
			GameTag.ATK,
			GameTag.HEALTH,
			GameTag.DURABILITY,
			GameTag.DAMAGE,
			GameTag.COST,
			GameTag.ZONE,
			GameTag.ZONE_POSITION,
			GameTag.CONTROLLER,
			GameTag.ENTITY_ID,
			GameTag.SILENCED,
			GameTag.WINDFURY,
			GameTag.TAUNT,
			GameTag.STEALTH,
			GameTag.DIVINE_SHIELD,
			GameTag.POISONOUS,
			GameTag.LIFESTEAL,
			GameTag.CHARGE,
			GameTag.FACTION,
			GameTag.CARDTYPE,
			GameTag.RARITY,
			GameTag.BATTLECRY,
			GameTag.DEATHRATTLE,
			GameTag.FROZEN,
			GameTag.NUM_ATTACKS_THIS_TURN,
			GameTag.FORCED_PLAY,
			GameTag.TO_BE_DESTROYED,
			GameTag.START_WITH_1_HEALTH,
			GameTag.CUSTOM_KEYWORD_EFFECT,
			GameTag.EXTRA_ATTACKS_THIS_TURN
		};

		public static PowerHistoryShowEntity ShowEntity(IPlayable playable)
		{
			var tags = SnapshotTags(((Entity)playable)._data);
			gameTagsEntities.ForEach(p => tags[p] = playable[p]);
            AddAuraDerivedTags(playable, tags);
            AddModernSnapshotTags(playable, tags);
            AddLeylineTags(playable, tags);
            if (playable.Card.Shatter != null)
                foreach (var tag in new[] { (GameTag)4239, (GameTag)4240, GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2, (GameTag)401 }) tags[tag] = playable[tag];
            if (playable.Card.HandProgress != null) tags[GameTag.TAG_SCRIPT_DATA_NUM_1] = playable[GameTag.TAG_SCRIPT_DATA_NUM_1];
            int alternate = (int)Actions.CostRules.PaymentKind(playable);
            if (alternate != 0) tags[GameTag.CARD_ALTERNATE_COST] = alternate;
            else tags.Remove(GameTag.CARD_ALTERNATE_COST);
			//tags[GameTag.TAG_LAST_KNOWN_COST_IN_HAND] = playable[GameTag.COST];

			return new PowerHistoryShowEntity
			{
				Entity = new PowerHistoryEntity
				{
					Id = playable.Id,
					Name = playable.Card.Id,
					Tags = tags
				}
			};
		}

		public static IPowerHistoryEntry HideEntity(IPlayable playable)
		{
			return new PowerHistoryHideEntity
			{
				EntityID = playable.Id,
				Zone = playable.Zone.Type
			};
		}

		public static PowerHistoryBlockStart BlockStart(BlockType blockType, int source, string effectCardId, int index, int target,
            int? effectIndex = null, int? triggerKeyword = null)
		{
			return new PowerHistoryBlockStart
			{
				BlockType = blockType,
				Source = source,
				EffectCardId = effectCardId,
				Index = index,
				Target = target,
                EffectIndex = effectIndex ?? (blockType == BlockType.POWER || blockType == BlockType.TRIGGER ? 0 : (int?)null),
                TriggerKeyword = triggerKeyword
			};
		}

		public static PowerHistoryBlockEnd BlockEnd()
		{
			return new PowerHistoryBlockEnd();
		}
	}

	//message PowerHistory
	//{
	//    enum PacketID
	//    {
	//        ID_acd5 = 19;
	//    }
	//    repeated PowerHistoryData list = 1;
	//}
	public class PowerHistory
	{
		private readonly bool _enabled = true;

        private sealed class BlockFrame
        {
            internal PowerHistoryBlockStart Start;
            internal readonly List<int> Targets = new List<int>();
            internal readonly HashSet<int> SelectedTargets = new HashSet<int>();
            internal readonly List<int> BurnedCards = new List<int>();
            internal bool HasEffects;
            internal bool Appendages;
        }
        private readonly Stack<BlockFrame> _blocks = new Stack<BlockFrame>();
        private readonly Stack<PowerHistoryBlockStart> _damageGroups = new Stack<PowerHistoryBlockStart>();
        internal PowerHistoryBlockStart CurrentBlock => _blocks.Count == 0 ? null : _blocks.Peek().Start;
        // CAST_RANDOM_SPELLS is published once around a caster's open effect plays (no extra runtime state).
        internal bool HasEffectPlayCaster(int caster) => _blocks.Any(b => b.Start is PowerHistoryEffectPlayStart play && play.Caster == caster);
        internal bool HasPowerAncestor(int source) => _blocks.Any(b => b.Start.BlockType == BlockType.POWER && b.Start.Source == source);
        internal bool HasEffectPlay(int source) => _blocks.Any(b => b.Start is PowerHistoryEffectPlayStart && b.Start.Source == source);
        internal bool HasPlayAncestor(int source) => _blocks.Any(b => b.Start.BlockType == BlockType.PLAY && b.Start.Source == source);

        internal void TargetHeader(IEntity source, IPlayable target)
        {
            if (target != null && (source is Spell || source is HeroPower || source is BoardObject || HasPlayAncestor(source.Id)))
                TargetRange(new[] { target.Id });
        }

        // TRIGGER headers describe the event, not effect targets. Random/explicit task selections authorize
        // resolved hit ranges; fixed-set IncludeTask/AoE does not (retail O:2445 vs F:7704).
        internal void SelectTargets(IEnumerable<IPlayable> targets)
        {
            if (!_enabled || !(CurrentBlock?.BlockType == BlockType.POWER || CurrentBlock?.BlockType == BlockType.TRIGGER)) return;
            var selected = _blocks.Peek().SelectedTargets;
            selected.Clear();
            if (targets != null) selected.UnionWith(targets.OfType<ICharacter>().Select(p => p.Id));
        }

        // Custom recipes resolve targets without passing through RandomTask/IncludeTask.
        internal void SelectResolvedTarget(ICharacter target)
        {
            if (!_enabled) return;
            SelectTargets(new[] { target });
        }

        internal void TargetRange(IEnumerable<IPlayable> targets, EntityType type = EntityType.INVALID)
        {
            if (!_enabled) return;
            var characters = targets.OfType<ICharacter>().ToArray();
            if (type != EntityType.STACK && type != EntityType.INVALID)
                SelectTargets(type == EntityType.TARGET || type == EntityType.EVENT_TARGET || type == EntityType.EVENT_SOURCE ? characters : null);
            if (CurrentBlock?.BlockType == BlockType.TRIGGER)
            {
                TargetRange(characters.Where(p => _blocks.Peek().SelectedTargets.Contains(p.Id)).Select(p => p.Id), resolved: true);
            }
            else TargetRange(characters.Select(p => p.Id));
        }

        internal void TargetRange(IEnumerable<int> targets, bool resolved = false)
        {
            if (!_enabled || !(CurrentBlock?.BlockType == BlockType.POWER || resolved && CurrentBlock?.BlockType == BlockType.TRIGGER)) return;
            var ids = targets.ToList();
            if (ids.Count == 0) return;
            var frame = _blocks.Peek();
            if (!frame.HasEffects && ids.All(id => frame.Targets.Contains(id))) return;
            Add(new PowerHistoryMetaData { Type = MetaDataType.TARGET, Data = frame.Start.Source, Info = ids });
        }

        internal void HitTarget(int victim)
        {
            if ((CurrentBlock?.BlockType == BlockType.POWER || CurrentBlock?.BlockType == BlockType.TRIGGER && _blocks.Peek().SelectedTargets.Contains(victim))
                && !_blocks.Peek().Targets.Contains(victim))
                TargetRange(new[] { victim }, resolved: true);
        }

        [NonSerialized] private int _damageDepth;
        [NonSerialized] private (int Depth, List<IPowerHistoryEntry> Entries)? _damageWave;
        [NonSerialized] private List<IPowerHistoryEntry> _poisonCapture;

        [NonSerialized] private int _damageReactionDepth;

        internal IDisposable DamageReaction()
        {
            if (!_damageWave.HasValue) return null;
            _damageReactionDepth++;
            return new EffectSourceScope(() => _damageReactionDepth--);
        }

        private void FlushPoisonous()
        {
            if (!_damageWave.HasValue || _damageWave.Value.Entries.Count == 0) return;
            var entries = _damageWave.Value.Entries.ToArray();
            _damageWave.Value.Entries.Clear();
            foreach (var entry in entries) Add(entry);
        }

        internal IDisposable DamageWave(int targets)
        {
            if (!_enabled || targets < 2) return null;
            FlushPoisonous();
            var previous = _damageWave;
            // A wave opened inside a reaction batches its own hits; the outer reaction depth returns on close.
            int outerReactions = _damageReactionDepth;
            _damageReactionDepth = 0;
            var entries = new List<IPowerHistoryEntry>();
            _damageWave = (_damageDepth, entries);
            return new EffectSourceScope(() => {
                _damageWave = previous;
                _damageReactionDepth = outerReactions;
                // hslog-tests/25770_inferrable_player.power.log:1542-1673: all hits, then poison, then collected Lifesteal.
                foreach (var entry in entries) Add(entry);
            });
        }

        internal IDisposable DamageHit()
        {
            if (!_damageWave.HasValue) return null;
            _damageDepth++;
            return new EffectSourceScope(() => _damageDepth--);
        }

        internal IDisposable PoisonousSource(IPlayable source, ICharacter victim)
        {
            // Nested damage callbacks/activations own their poison, even when they use the same source and block.
            bool defer = _damageWave.HasValue && _damageWave.Value.Depth + 1 == _damageDepth;
            if (defer) _poisonCapture = _damageWave.Value.Entries;
            // Native notes 5.2-5.3: keyword 363 selects actor spell 76; -1 suppresses the card's own trigger effect.
            Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "", -1, 0,
                effectIndex: -1, triggerKeyword: (int)GameTag.POISONOUS));
            Add(new PowerHistoryMetaData { Type = MetaDataType.TARGET, Data = 0, Info = new List<int> { victim.Id } });
            Add(new PowerHistoryMetaData { Type = MetaDataType.POISONOUS, Data = 0, Info = new List<int> { victim.Id } });
            return defer ? new EffectSourceScope(() => {
                Add(PowerHistoryBuilder.BlockEnd());
                _poisonCapture = null;
            }) : new EffectSourceScope(this);
        }

        // Inline effects and Discover continuations can resolve without their own queued block.
        internal IDisposable EffectSource(IPlayable source, ICharacter victim)
        {
            var block = CurrentBlock;
            if (block != null && (block.BlockType == BlockType.ATTACK || block.BlockType == BlockType.FATIGUE)) return null;
            if (block != null && (block.BlockType == BlockType.POWER || block.BlockType == BlockType.TRIGGER))
            {
                if (block.Source == source.Id) return null;
                if (source is Enchantment enchantment && (enchantment.Target?.Id == block.Source || enchantment.Creator?.Id == block.Source)) return null;
                if (source is Weapon weapon && weapon.Controller.Hero.Weapon == weapon && weapon.Controller.Hero.Id == block.Source) return null;
                if (source.Game.IdEntityDic.TryGetValue(block.Source, out var owner) && owner is Enchantment effect
                    && (effect.Target?.Id == source.Id || effect.Creator?.Id == source.Id)) return null;
            }
            // Attribute the hit without replaying the contributor card's trigger effect.
            bool selected = _blocks.Count > 0 && _blocks.Peek().SelectedTargets.Contains(victim.Id);
            Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "", -1, victim.Id, effectIndex: -1));
            if (selected) SelectTargets(new[] { victim });
            return new EffectSourceScope(this);
        }

        // Retail 186852 (hsreplay-test-data 186852_hearthstone_access_eoe.power.log L2326-2495, Gigafin): appendages
        // enter inside TRIGGER(body, SubOption -1, EffectIndex 0) nested in the body's PLAY block. The client drops
        // card zone changes made directly in a locally predicted PLAY list (ZoneMgr.CheckAndIgnoreServerChangeList).
        internal IDisposable ColossalAppendages(IPlayable body)
        {
            if (!_enabled || _blocks.Count > 0 && _blocks.Peek().Appendages && _blocks.Peek().Start.Source == body.Id) return null;
            Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, body.Id, "", -1, 0));
            _blocks.Peek().Appendages = true;
            return new EffectSourceScope(this);
        }

        private sealed class EffectSourceScope : IDisposable
        {
            private readonly PowerHistory _history;
            [NonSerialized] private readonly Action _dispose;
            internal EffectSourceScope(PowerHistory history) { _history = history; }
            internal EffectSourceScope(Action dispose) { _dispose = dispose; }
            public void Dispose()
            {
                if (_dispose != null) _dispose();
                else _history.Add(PowerHistoryBuilder.BlockEnd());
            }
        }

        internal IDisposable HandBuff(IEntity target) => _enabled && target.Zone?.Type == Zone.HAND
            ? new HandBuffScope(this, target) : null;

        public const string GeneratedShufflePrefab = "ReuseFX_Generic_SpawnToDeck_NoFX_CardFromScript_Super:cc0ba21e3e5306140870277e116b2c4b";

        internal IDisposable GeneratedShuffleSource(IEntity source)
        {
            if (!_enabled || CurrentBlock != null && CurrentBlock.Source == source.Id
                && (CurrentBlock.BlockType == BlockType.POWER || CurrentBlock.BlockType == BlockType.TRIGGER)) return null;
            Add(PowerHistoryBuilder.BlockStart(BlockType.TRIGGER, source.Id, "", -1, 0, effectIndex: -1));
            return new EffectSourceScope(this);
        }

        internal bool GeneratedShuffle(IPlayable card, bool reveal)
        {
            int creation = Last.FindLastIndex(e => e is PowerHistoryFullEntity full && full.Entity.Id == card.Id
                && full.Entity.Tags.TryGetValue(GameTag.ZONE, out int zone) && (zone == (int)Zone.INVALID || zone == (int)Zone.DECK));
            if (creation < 0 || CurrentBlock == null || Last.Skip(creation + 1).Any(e =>
                e is PowerHistoryBlockStart || e is PowerHistoryBlockEnd || e is PowerHistoryResetGame
                || e is PowerHistoryShowEntity show && show.Entity.Id == card.Id
                || e is PowerHistoryHideEntity hide && hide.EntityID == card.Id)) return false;

            var full = (PowerHistoryFullEntity)Last[creation];
            int fullIndex = Full.IndexOf(full);
            if (fullIndex < 0) return false;
            // Retail F:2086-2131: SpawnToDeckSpell targets DECK FULLs, then loads their SHOWs.
            full.Entity.Name = "";
            full.Entity.Tags = new Dictionary<GameTag, int> {
                [GameTag.ZONE] = (int)Zone.DECK, [GameTag.CONTROLLER] = card.Controller.PlayerId, [GameTag.ENTITY_ID] = card.Id
            };
            var presentation = new List<IPowerHistoryEntry> {
                new PowerHistorySubSpellStart { Source = card.Controller.Hero.Id, SpellPrefabGuid = GeneratedShufflePrefab },
                full
            };
            // Random stacks and hidden deck copies must not gain identity through presentation.
            if (reveal) presentation.Add(PowerHistoryBuilder.ShowEntity(card));
            presentation.AddRange(new IPowerHistoryEntry[] {
                PowerHistoryBuilder.HideEntity(card),
                new PowerHistoryMetaData { Type = MetaDataType.TARGET, Data = 0, Info = new List<int> { card.Id } },
                new PowerHistorySubSpellEnd()
            });
            int open = JoinableShuffleEnd(card, creation);
            if (open >= 0)
            {
                // Retail SpawnToDeckSpell.AddPowerTargets flies every DECK FULL of one sub-spell together;
                // separate scopes queue separate task lists and animate one card after another.
                int fullOpen = fullIndex - (creation - open);
                Full.RemoveAt(fullOpen); Last.RemoveAt(open);
                fullIndex--; creation--;
                presentation.RemoveAt(0);
            }
            // Insert at each creation, so preallocated batches cannot produce crossing scopes.
            Full.RemoveAt(fullIndex); Full.InsertRange(fullIndex, presentation);
            Last.RemoveAt(creation); Last.InsertRange(creation, presentation);
            return true;
        }

        // Last index of an unflushed generated spawn scope into the same deck that this creation can join:
        // same block, and in between only the scope cards' own deck-side tag changes and enchantments attached to
        // them (e.g. Academic Espionage's cost buff). Any other row, such as a listener's effect, keeps its own scope.
        private int JoinableShuffleEnd(IPlayable card, int creation)
        {
            var game = card.Game;
            int end = creation - 1;
            while (end >= 0 && (Last[end] is PowerHistoryTagChange || Last[end] is PowerHistoryFullEntity
                || Last[end] is PowerHistoryShowEntity)) end--;
            if (end < 0 || !(Last[end] is PowerHistorySubSpellEnd)) return -1;
            var cards = new HashSet<int>();
            int start = end - 1;
            for (; start >= 0 && !(Last[start] is PowerHistorySubSpellStart); start--)
            {
                var entry = Last[start];
                if (entry is PowerHistoryFullEntity full)
                {
                    if (full.Entity.Tags.TryGetValue(GameTag.ZONE, out int zone) && zone == (int)Zone.DECK) cards.Add(full.Entity.Id);
                }
                else if (!(entry is PowerHistoryShowEntity || entry is PowerHistoryHideEntity || entry is PowerHistoryTagChange
                    || entry is PowerHistoryMetaData meta && meta.Type == MetaDataType.TARGET)) return -1;
            }
            if (start < 0 || !(Last[start] is PowerHistorySubSpellStart scope) || scope.SpellPrefabGuid != GeneratedShufflePrefab
                || scope.Source != card.Controller.Hero.Id || scope.Targets.Count != 0 || scope.RequirePublicOrigin) return -1;
            var buffs = new HashSet<int>();
            bool Buff(int id) => game.IdEntityDic.TryGetValue(id, out var entity) && entity is Enchantment enchantment
                && enchantment.Target != null && cards.Contains(enchantment.Target.Id);
            for (int i = end + 1; i < creation; i++)
            {
                switch (Last[i])
                {
                    case PowerHistoryFullEntity full when Buff(full.Entity.Id): buffs.Add(full.Entity.Id); break;
                    case PowerHistoryShowEntity show when buffs.Contains(show.Entity.Id): break;
                    case PowerHistoryTagChange change when buffs.Contains(change.EntityId): break;
                    case PowerHistoryTagChange change when cards.Contains(change.EntityId) && change.Tag != GameTag.CONTROLLER
                        && (change.Tag != GameTag.ZONE || change.Value == (int)Zone.DECK): break;
                    default: return -1;
                }
            }
            // Both histories must hold the same unflushed run, or a merge would desynchronize them.
            int fullEnd = Full.LastIndexOf(Last[end]);
            if (fullEnd < 0 || fullEnd + creation - end >= Full.Count) return -1;
            for (int i = end; i <= creation; i++)
                if (!ReferenceEquals(Full[fullEnd + i - end], Last[i])) return -1;
            return end;
        }

        /// <summary>The enchantment whose granted Deathrattle is resolving, for presentation recipes only.</summary>
        internal Enchantment GrantedDeathrattle { get; set; }

        internal IDisposable Visual(Game game, VisualPhase phase, IPlayable source = null, IEntity target = null)
        {
            if (!_enabled || CurrentBlock == null) return null;
            if (phase == VisualPhase.Damage && target != null && source?.Id == CurrentBlock.Source && _damageGroups.Contains(CurrentBlock)) return null;
            if (source == null) game.IdEntityDic.TryGetValue(CurrentBlock.Source, out source);
            if (source == null) return null;
            IPlayable identity = source;
            bool requirePublicOrigin = CurrentBlock.BlockType == BlockType.TRIGGER
                && CurrentBlock.TriggerKeyword == (int)GameTag.DEATHRATTLE
                || source.Card.Id == "END_024" || source.Card.Id == "FIR_902"
                || source.Card.Id == "JAIL_881t" || source.Card.Id == "JAIL_443t" || source.Card.Id == "TIME_025t" || source.Card.Id == "JAIL_319";
            if (source.Id != CurrentBlock.Source)
            {
                if (phase != VisualPhase.Damage || source.Card.Id != "EDR_482" || target?.Id != source.Controller.Hero.Id
                    || !game.IdEntityDic.TryGetValue(CurrentBlock.Source, out var blockSource) || !(blockSource is Enchantment timer)
                    || timer.Card.Id != "EDR_482e" || timer.Creator?.Id != source.Id || timer.Target?.Id != source.Controller.Id)
                    return null;
                identity = timer; requirePublicOrigin = true;
            }
            // Past Gnomeregan's granted Deathrattle resolves as the dying minion's own TRIGGER block (no effect card id),
            // so the enchantment, not the minion, names the recipe.
            if (phase == VisualPhase.Damage && CurrentBlock.BlockType == BlockType.TRIGGER && CurrentBlock.TriggerKeyword == (int)GameTag.DEATHRATTLE
                && GrantedDeathrattle != null && GrantedDeathrattle.Card.Id == "TIME_044t1e" && GrantedDeathrattle.Target?.Id == CurrentBlock.Source
                && source.Id == CurrentBlock.Source)
                identity = GrantedDeathrattle;
            var recipe = SubSpellRecipes.Find(CurrentBlock, identity.Card.Id, phase);
            if (recipe == null || (recipe.Group ? target != null
                : recipe.Targets == VisualTargets.Resolved && target == null)) return null;
            int visualSource = recipe.Source == VisualSource.Inherit ? 0
                : recipe.Source == VisualSource.Hero ? source.Controller.Hero.Id : source.Id;
            return new VisualScope(this, game, recipe, phase, visualSource, target, requirePublicOrigin);
        }

        private sealed class VisualScope : IDisposable
        {
            private readonly PowerHistory _history;
            private readonly Game _game;
            private readonly SubSpellRecipe _recipe;
            private readonly VisualPhase _phase;
            private readonly PowerHistoryBlockStart _block;
            private readonly int _full, _last, _source, _target, _attack, _health, _rush;
            private readonly bool _requirePublicOrigin;
            private bool _disposed;
            internal VisualScope(PowerHistory history, Game game, SubSpellRecipe recipe, VisualPhase phase, int source, IEntity target, bool requirePublicOrigin)
            {
                _history = history; _game = game; _recipe = recipe; _phase = phase; _source = source; _target = target?.Id ?? 0;
                _requirePublicOrigin = requirePublicOrigin;
                if (phase == VisualPhase.BoardBuff && target != null)
                { _attack = target[GameTag.ATK]; _health = target[GameTag.HEALTH]; _rush = target[GameTag.RUSH]; }
                _block = history.CurrentBlock; _full = history.Full.Count; _last = history.Last.Count;
                if (phase == VisualPhase.DamageGroup) history._damageGroups.Push(_block);
                if (phase == VisualPhase.Summon || phase == VisualPhase.Create)
                {
                    int creation = history.Full.FindLastIndex(e => e is PowerHistoryFullEntity full && full.Entity.Id == _target);
                    if (creation >= 0 && !history.Full.Skip(creation).Any(e => e is PowerHistoryBlockStart
                        || e is PowerHistoryBlockEnd || e is PowerHistorySubSpellEnd || e is PowerHistoryResetGame))
                    {
                        int lastCreation = history.Last.IndexOf(history.Full[creation]);
                        if (lastCreation >= 0) { _full = creation; _last = lastCreation; }
                    }
                }
            }
            public void Dispose()
            {
                if (_disposed) return;
                _disposed = true;
                if (_phase == VisualPhase.DamageGroup) _history._damageGroups.Pop();
                if (_history.CurrentBlock != _block || _last < 0 || _history.Last.Count < _last
                    || _history.Full.Skip(_full).Any(e => e is PowerHistoryResetGame)) return;
                if (_recipe.Group && (_phase == VisualPhase.Damage || _phase == VisualPhase.DamageGroup))
                {
                    var entries = _history.Full.Skip(_full).ToArray();
                    if (!_history.Last.Skip(_last).SequenceEqual(entries)) return;
                    int groupDepth = 0;
                    bool hit = false;
                    foreach (var entry in entries)
                    {
                        if (entry is PowerHistoryBlockStart) groupDepth++;
                        else if (entry is PowerHistoryBlockEnd) groupDepth--;
                        else if (groupDepth == 0 && entry is PowerHistoryMetaData meta
                            && meta.Type == MetaDataType.DAMAGE && meta.Data > 0) hit = true;
                    }
                    if (!hit) return;
                    // A pulse before the hits avoids replaying the full-board effect after nested reactions.
                    var pulse = new IPowerHistoryEntry[] {
                        new PowerHistorySubSpellStart { Source = _source, SpellPrefabGuid = _recipe.Prefab },
                        new PowerHistorySubSpellEnd()
                    };
                    _history.Full.InsertRange(_full, pulse); _history.Last.InsertRange(_last, pulse);
                    return;
                }
                if (_recipe.Group)
                {
                    // Invocation-local only: never annotate across blocks, rewinds or a flushed packet.
                    var entries = _history.Full.Skip(_full).ToArray();
                    if (!_history.Last.Skip(_last).SequenceEqual(entries) || entries.Any(e =>
                        e is PowerHistoryBlockStart || e is PowerHistoryBlockEnd)) return;
                    var recipients = entries.OfType<PowerHistoryFullEntity>().Where(e =>
                        _game.IdEntityDic.TryGetValue(e.Entity.Id, out var entity) && entity is Enchantment enchantment
                        && enchantment.Creator.Id == _source && enchantment.Target is Minion
                        && enchantment.Target.Zone?.Type == Zone.PLAY).ToArray();
                    if (recipients.Length == 0) return;
                    var targets = recipients.Select(e => ((Enchantment)_game.IdEntityDic[e.Entity.Id]).Target.Id).ToList();
                    int full = _history.Full.IndexOf(recipients[0]), last = _history.Last.IndexOf(recipients[0]);
                    var group = new IPowerHistoryEntry[] {
                        new PowerHistoryMetaData { Type = MetaDataType.TARGET, Data = 0, Info = targets },
                        new PowerHistorySubSpellStart { Source = _source, SpellPrefabGuid = _recipe.Prefab, Targets = targets }
                    };
                    _history.Full.InsertRange(full, group); _history.Last.InsertRange(last, group);
                    _history.Add(new PowerHistorySubSpellEnd());
                    return;
                }
                bool applicable = false;
                int depth = 0;
                foreach (var entry in _history.Full.Skip(_full))
                {
                    if (entry is PowerHistoryBlockStart) { depth++; continue; }
                    if (entry is PowerHistoryBlockEnd) { depth--; continue; }
                    if (depth == 0 && Applies(entry)) applicable = true;
                }
                if (!applicable) return;
                var start = new PowerHistorySubSpellStart { Source = _source, SpellPrefabGuid = _recipe.Prefab, RequirePublicOrigin = _requirePublicOrigin };
                if (_recipe.Targets == VisualTargets.Resolved) start.Targets.Add(_target);
                // Retail wire (END_010b, data/rules-evidence/choose-one): META_DATA TARGET precedes SUB_SPELL_START, so a
                // hit's lazily published TARGET stays first in its block and the scope opens after it.
                int at = _full, atLast = _last;
                while (_target != 0 && at < _history.Full.Count && atLast < _history.Last.Count
                    && _history.Full[at] is PowerHistoryMetaData meta && meta.Type == MetaDataType.TARGET && meta.Info.Contains(_target)
                    && ReferenceEquals(_history.Last[atLast], meta)) { at++; atLast++; }
                _history.Full.Insert(at, start); _history.Last.Insert(atLast, start);
                if (_recipe.Pulse)
                {
                    var end = new PowerHistorySubSpellEnd();
                    _history.Full.Insert(at + 1, end); _history.Last.Insert(atLast + 1, end);
                }
                else _history.Add(new PowerHistorySubSpellEnd());
            }
            private bool Applies(IPowerHistoryEntry entry) =>
                    entry is PowerHistoryMetaData meta && meta.Info.Contains(_target)
                        && (_phase == VisualPhase.Damage && meta.Type == MetaDataType.DAMAGE
                            || _phase == VisualPhase.Heal && meta.Type == MetaDataType.HEALING)
                    || entry is PowerHistoryTagChange tag && tag.EntityId == _target && _phase == VisualPhase.BoardBuff
                        // Enchantments also publish unchanged stats; only actual changes deserve an impact.
                        && (tag.Tag == GameTag.ATK && tag.Value != _attack
                            || tag.Tag == GameTag.HEALTH && tag.Value != _health
                            || tag.Tag == GameTag.RUSH && tag.Value != _rush)
                    || entry is PowerHistoryTagChange armor && armor.EntityId == _target && _phase == VisualPhase.Armor
                        && armor.Tag == GameTag.ARMOR
                    || entry is PowerHistoryShowEntity show && show.Entity.Id == _target && _phase == VisualPhase.Summon
                        && show.Entity.Tags.TryGetValue(GameTag.ZONE, out int zone) && zone == (int)Zone.PLAY
                    || entry is PowerHistoryTagChange move && move.EntityId == _target && move.Tag == GameTag.ZONE
                        && ((_phase == VisualPhase.Draw || _phase == VisualPhase.Create) && move.Value == (int)Zone.HAND
                            || _phase == VisualPhase.Summon && move.Value == (int)Zone.PLAY);
        }

        private sealed class HandBuffScope : IDisposable
        {
            private readonly PowerHistory _history;
            private readonly IEntity _target;
            private readonly int _full, _last, _attack, _health, _source;
            internal HandBuffScope(PowerHistory history, IEntity target)
            {
                _history = history; _target = target;
                _full = history.Full.Count; _last = history.Last.Count;
                _attack = target[GameTag.ATK]; _health = target[GameTag.HEALTH];
                _source = target.Controller.Hero.Id;
            }
            public void Dispose()
            {
                if (_target[GameTag.ATK] <= _attack && _target[GameTag.HEALTH] <= _health) return;
                // Retail O:3282-3307 wraps attachment + stats; annotations never change engine state.
                var start = new PowerHistorySubSpellStart { Source = _source, Targets = new List<int> { _target.Id },
                    SpellPrefabGuid = "ReuseFX_Generic_HandBuff_ScaleUp_OpponentAE_Gold_Super:2fb243a4d1ef3324fa09d018ce3f4418" };
                _history.Full.Insert(_full, start);
                _history.Last.Insert(_last, start);
                _history.Add(new PowerHistorySubSpellEnd());
            }
        }

        // Public observations belong to this history branch, even when clone packet output is disabled.
        // Snapshots are frozen on insertion; forks copy the map, never mutate a shared observation.
        [NonSerialized] private Dictionary<int, PowerHistoryEntity> _publicHandReveals = new Dictionary<int, PowerHistoryEntity>();
        private Dictionary<int, PowerHistoryEntity> PublicHandReveals
        {
            get {
                if (_publicHandReveals != null) return _publicHandReveals;
                _publicHandReveals = new Dictionary<int, PowerHistoryEntity>();
                // Legacy snapshots retain packets only; current snapshots carry observations separately.
                if (Full.Count <= 65536) {
                    var snapshots = new Dictionary<int, PowerHistoryEntity>();
                    foreach (var entry in Full) {
                        var snapshot = (entry as PowerHistoryShowEntity)?.Entity ?? (entry as PowerHistoryFullEntity)?.Entity
                            ?? (entry as PowerHistoryChangeEntity)?.Entity;
                        if (snapshot != null) snapshots[snapshot.Id] = new PowerHistoryEntity { Id = snapshot.Id,
                            Name = snapshot.Name, Tags = new Dictionary<GameTag, int>(snapshot.Tags) };
                        if (entry is PowerHistoryTagChange change && snapshots.TryGetValue(change.EntityId, out var actor)) {
                            actor.Tags[change.Tag] = change.Value;
                            if (change.Tag == GameTag.REVEALED && change.Value != 0) RememberRevealedHand(actor);
                        }
                        ObservePublicHand(entry);
                    }
                }
                return _publicHandReveals;
            }
        }
        // Kept outside the positional field schema so legacy snapshots still load.
        internal Dictionary<int, PowerHistoryEntity> SnapshotHandReveals() =>
            new Dictionary<int, PowerHistoryEntity>(PublicHandReveals);
        internal void RestoreHandReveals(Dictionary<int, PowerHistoryEntity> reveals) => _publicHandReveals = reveals;
        private void ObservePublicHand(IPowerHistoryEntry entry)
        {
            if (entry is PowerHistoryTagChange move && move.Tag == GameTag.ZONE && move.Value != (int)Zone.HAND)
                PublicHandReveals.Remove(move.EntityId);
            var snapshot = (entry as PowerHistoryShowEntity)?.Entity ?? (entry as PowerHistoryFullEntity)?.Entity;
            if (snapshot != null) RememberRevealedHand(snapshot);
        }
        public bool TryGetPublicHandReveal(int entity, int controller, out string cardId, out int cost)
        {
            cardId = null; cost = 0;
            if (!PublicHandReveals.TryGetValue(entity, out var snapshot)
                || snapshot.Tags[GameTag.CONTROLLER] != controller) return false;
            cardId = snapshot.Name; cost = snapshot.Tags[GameTag.COST]; return true;
        }
        internal void RememberRevealedHand(IPlayable card)
        {
            if (card[GameTag.ZONE] == (int)Zone.HAND && card[GameTag.REVEALED] != 0)
                RememberRevealedHand(PowerHistoryBuilder.ShowEntity(card).Entity);
        }
        private void RememberRevealedHand(PowerHistoryEntity snapshot)
        {
            if (!snapshot.Tags.TryGetValue(GameTag.ZONE, out int zone) || zone != (int)Zone.HAND
                || !snapshot.Tags.TryGetValue(GameTag.REVEALED, out int flag) || flag == 0
                || !snapshot.Tags.TryGetValue(GameTag.CONTROLLER, out int controller)
                || string.IsNullOrEmpty(snapshot.Name)) return;
            PublicHandReveals[snapshot.Id] = new PowerHistoryEntity { Id = snapshot.Id, Name = snapshot.Name,
                Tags = new Dictionary<GameTag, int> { [GameTag.CONTROLLER] = controller,
                    [GameTag.COST] = snapshot.Tags.TryGetValue(GameTag.COST, out int cost) ? cost : Cards.FromId(snapshot.Name).Cost } };
        }

		public PowerHistory() { }

        internal PowerHistory(bool enabled, PowerHistory source)
        {
            _enabled = enabled;
            if (source != null) _publicHandReveals = new Dictionary<int, PowerHistoryEntity>(source.PublicHandReveals);
            if (!enabled || source == null) return;
            // Exact clones start a fresh log but retain the suspended activation's history context.
            foreach (var frame in source._blocks.Reverse())
            {
                var start = frame.Start;
                var copy = new BlockFrame { HasEffects = frame.HasEffects, Appendages = frame.Appendages, Start = new PowerHistoryBlockStart {
                    BlockType = start.BlockType, Index = start.Index, EffectIndex = start.EffectIndex,
                    TriggerKeyword = start.TriggerKeyword, Source = start.Source, Target = start.Target, EffectCardId = start.EffectCardId
                } };
                copy.Targets.AddRange(frame.Targets);
                copy.SelectedTargets.UnionWith(frame.SelectedTargets);
                copy.BurnedCards.AddRange(frame.BurnedCards);
                _blocks.Push(copy);
            }
        }

		public List<IPowerHistoryEntry> Full { get; } = new List<IPowerHistoryEntry>();
		public List<IPowerHistoryEntry> Last { get; } = new List<IPowerHistoryEntry>();

        // Diagnostics only (match event log): RNG draws consumed between consecutive entries of the real game. The side table is
        // NonSerialized, never copied to clones and read by no gameplay code, so state, RNG and fingerprints are untouched.
        [NonSerialized] private Func<long> _drawCounter;
        [NonSerialized] private long _lastDrawCount;
        [NonSerialized] private System.Runtime.CompilerServices.ConditionalWeakTable<IPowerHistoryEntry, object> _drawsBefore;
        internal void TrackRandomDraws(Func<long> counter)
        {
            _drawCounter = counter; _drawsBefore = new System.Runtime.CompilerServices.ConditionalWeakTable<IPowerHistoryEntry, object>();
            try { _lastDrawCount = counter(); } catch (Exception) { _drawCounter = null; }
        }
        /// <summary>RNG draws consumed since the previous entry was added (0: none, unknown or not the tracked live game).</summary>
        public int RandomDrawsBefore(IPowerHistoryEntry entry) =>
            _drawsBefore != null && entry != null && _drawsBefore.TryGetValue(entry, out object draws) ? (int)draws : 0;
        // AutoPlayRules states what it chose from just before the play opens; EffectPlayRules.Open consumes it for that card only.
        [NonSerialized] private int _noteCard, _noteLegal = -1, _noteMode;
        [NonSerialized] private bool _noteModeRandom;
        [NonSerialized] private string _noteModeCard;
        // legalTargets: candidates the automatic play chose from; -1 unknown; -2 the card's own rule fixed the target.
        internal void NoteAutoChoice(int card, int legalTargets, int mode, string modeCardId, bool modeRandom)
        {
            if (!_enabled) return;
            _noteCard = card; _noteLegal = legalTargets; _noteMode = mode; _noteModeCard = modeCardId; _noteModeRandom = modeRandom;
        }
        // Kept in a side table, never on the entry: entries are compared field by field (canonical history, snapshots), and a
        // diagnostic must not make a live game's history differ from a reloaded one. No new engine TYPE either: the snapshot codec's
        // type table (and so every encoded card definition inside a replay fingerprint) shifts when one is added.
        [NonSerialized] private System.Runtime.CompilerServices.ConditionalWeakTable<IPowerHistoryEntry, object> _autoChoices;
        internal void ApplyAutoChoice(PowerHistoryEffectPlayStart start)
        {
            if (!_enabled || _noteCard == 0 || _noteCard != start.Source) return;
            if (_autoChoices == null) _autoChoices = new System.Runtime.CompilerServices.ConditionalWeakTable<IPowerHistoryEntry, object>();
            _autoChoices.Add(start, (_noteLegal, _noteMode, _noteModeCard, _noteModeRandom));
            _noteCard = 0;
        }
        /// <summary>What an automatic play chose from (null: not recorded, e.g. a reloaded or cloned history). LegalTargets: candidates it chose
        /// among (-1 unknown, -2 fixed by the card's own rule); Mode: the Choose One mode it picked (0 none) and that option's card id.</summary>
        public (int LegalTargets, int Mode, string ModeCardId, bool ModeRandom)? AutoChoiceOf(IPowerHistoryEntry entry) =>
            _autoChoices != null && entry != null && _autoChoices.TryGetValue(entry, out var note) ? ((int, int, string, bool))note : ((int, int, string, bool)?)null;
        internal void ClearAutoChoice() { _noteCard = 0; }
        private void StampRandomDraws(IPowerHistoryEntry entry)
        {
            try
            {
                long now = _drawCounter();
                long delta = now - _lastDrawCount;
                _lastDrawCount = now;
                if (delta > 0) _drawsBefore.Add(entry, (int)Math.Min(delta, int.MaxValue));
            }
            catch (Exception) { }
        }

        internal void BurnedCard(int entity)
        {
            if (!_enabled) return;
            if (_blocks.Count == 0)
                Add(new PowerHistoryMetaData { Type = MetaDataType.BURNED_CARD, Data = 0, Info = new List<int> { entity } });
            else _blocks.Peek().BurnedCards.Add(entity);
        }

        public void FlushBurnedCards()
        {
            // hslog-tests/34104_subspell.power.log:30226,33325: one range inside its enclosing block.
            // Ancestor ranges wait for their own end; appending here would attribute them to the child.
            if (_blocks.Count == 0 || _blocks.Peek().BurnedCards.Count == 0) return;
            var ids = _blocks.Peek().BurnedCards.ToList();
            _blocks.Peek().BurnedCards.Clear();
            Add(new PowerHistoryMetaData { Type = MetaDataType.BURNED_CARD, Data = 0, Info = ids });
        }

		public void Add(IPowerHistoryEntry entry)
		{
            if (_poisonCapture != null) { _poisonCapture.Add(entry); return; }
            if (_damageWave.HasValue && (_damageReactionDepth > 0 || _damageDepth <= _damageWave.Value.Depth)) FlushPoisonous();
            ObservePublicHand(entry);
			if (!_enabled) return;
            if (entry is PowerHistoryBlockEnd) FlushBurnedCards();
            if (_drawCounter != null) StampRandomDraws(entry);
            if (entry is PowerHistoryBlockStart start)
            {
                if (_blocks.Count > 0) _blocks.Peek().HasEffects = true;
                _blocks.Push(new BlockFrame { Start = start });
            }
            else if (entry is PowerHistoryBlockEnd)
            {
                if (_blocks.Count > 0) _blocks.Pop();
            }
            else if (_blocks.Count > 0)
            {
                var frame = _blocks.Peek();
                if (entry is PowerHistoryMetaData meta)
                {
                    if (meta.Type == MetaDataType.TARGET)
                    {
                        frame.Targets.Clear();
                        frame.Targets.AddRange(meta.Info);
                        frame.HasEffects = false;
                    }
                    else if (meta.Type == MetaDataType.DAMAGE || meta.Type == MetaDataType.HEALING)
                    {
                        frame.Targets.RemoveAll(id => meta.Info.Contains(id));
                        frame.HasEffects = true;
                    }
                }
                else if (entry is PowerHistoryTagChange tag && frame.Targets.Contains(tag.EntityId)
                    && (tag.Tag == GameTag.DAMAGE || tag.Tag == GameTag.ARMOR || tag.Tag == GameTag.HEALTH
                        || tag.Tag == GameTag.ATK || tag.Tag == GameTag.DIVINE_SHIELD || tag.Tag == GameTag.ZONE
                        || tag.Tag == GameTag.TAUNT || tag.Tag == GameTag.SILENCED || tag.Tag == GameTag.FROZEN
                        || tag.Tag == GameTag.STEALTH || tag.Tag == GameTag.WINDFURY || tag.Tag == GameTag.CHARGE
                        || tag.Tag == GameTag.LIFESTEAL || tag.Tag == GameTag.POISONOUS || tag.Tag == GameTag.RUSH || tag.Tag == GameTag.COST))
                    frame.HasEffects = true;
            }
			Full.Add(entry);
			Last.Add(entry);
		}

        // The enclosing block's effect flag before a child opens.
        internal bool CurrentBlockHasEffects => _blocks.Count > 0 && _blocks.Peek().HasEffects;

        // Withdraws the innermost block when nothing was recorded since its start, as if it had never opened.
        // Callers decide when an empty activation is not an activation at all (an unfired Secret).
        internal bool DiscardEmptyBlock(PowerHistoryBlockStart start, bool parentHadEffects)
        {
            if (!_enabled || start == null || _blocks.Count == 0 || !ReferenceEquals(_blocks.Peek().Start, start)
                || Full.Count == 0 || !ReferenceEquals(Full[Full.Count - 1], start)
                || Last.Count == 0 || !ReferenceEquals(Last[Last.Count - 1], start)) return false;
            _blocks.Pop();
            if (_blocks.Count > 0) _blocks.Peek().HasEffects = parentHadEffects;
            Full.RemoveAt(Full.Count - 1);
            Last.RemoveAt(Last.Count - 1);
            return true;
        }

		public string Print(bool fullFlag = true)
		{
			var str = new StringBuilder();
			if (fullFlag)
				Full.ForEach(p => str.Append(p.Print()));
			else
				Last.ForEach(p => str.Append(p.Print()));
			return str.ToString();
		}
	}

	//message PowerHistoryData
	//{
	//    optional PowerHistoryEntity full_entity = 1;
	//    optional PowerHistoryEntity show_entity = 2;
	//    optional PowerHistoryHide hide_entity = 3;
	//    optional PowerHistoryTagChange tag_change = 4;
	//    optional PowerHistoryCreateGame create_game = 5;
	//    optional PowerHistoryStart power_start = 6;
	//    optional PowerHistoryEnd power_end = 7;
	//    optional PowerHistoryMetaData meta_data = 8;
	//    optional PowerHistoryEntity change_entity = 9;
	//}
	public interface IPowerHistoryEntry
	{
		PowerType PowerType { get; }
		string Print();
	}

    public class PowerHistoryResetGame : IPowerHistoryEntry
    {
        public PowerType PowerType => PowerType.RESET_GAME;
        public PowerHistoryCreateGame CreateGame { get; set; }
        public bool SourceIsRewind { get; set; } = true;
        public string Print() => $"{PowerType} SourceIsRewind={SourceIsRewind}\n{CreateGame.Print()}";
    }

	//message PowerHistoryCreateGame
	//{
	//    required Entity game_entity = 1;
	//    repeated Player players = 2;
	//}
	public class PowerHistoryCreateGame : IPowerHistoryEntry
	{
		public PowerType PowerType => PowerType.CREATE_GAME;
		public PowerEntity Game { get; set; }
		public PowerPlayer[] Players { get; set; }
		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"{PowerType}");
			str.AppendLine($" - Game Entity = [{Game.Print()}]");
			foreach (PowerPlayer player in Players)
			{
				str.AppendLine($" - Player Entity = [{player.Print()}]");
			}
			return str.ToString();
		}
	}

	//message PowerHistoryStart
	//{
	//    required HistoryBlock.Type type = 1;
	//    required int32 index = 2;
	//    required int32 source = 3;
	//    required int32 target = 4;
	//    optional string effect_card_id = 5;
	//}
	public class PowerHistoryBlockStart : IPowerHistoryEntry
	{
		public PowerType PowerType => PowerType.BLOCK_START;
		public BlockType BlockType { get; set; }
		public int Index { get; set; } = -1;
		public int? EffectIndex { get; set; }
		public int? TriggerKeyword { get; set; }
		public int Source { get; set; }
		public int Target { get; set; }
		public string EffectCardId { get; set; } = "";

		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"{PowerType} {BlockType} Entity=[{Source}] EffectCardId={EffectCardId} EffectIndex={EffectIndex ?? Index} Target={Target}"
                + (TriggerKeyword.HasValue ? $" TriggerKeyword={(GameTag)TriggerKeyword.Value}" : ""));
			return str.ToString();
		}
	}

	// History-only presentation of an automatic play (EffectPlayRules): a PLAY block that is never a card play.
	// Every PLAY consumer that counts plays, paid costs or hand reveals must skip it.
	public sealed class PowerHistoryEffectPlayStart : PowerHistoryBlockStart
	{
		public bool ForceShowBigCard { get; set; }
		// The enclosing caster marked CAST_RANDOM_SPELLS for this play (0: none). Not part of the published entry.
		internal int Caster { get; set; }
	}

	//message PowerHistoryEnd
	//{
	//}
	public class PowerHistoryBlockEnd : IPowerHistoryEntry
	{
		public PowerType PowerType => PowerType.BLOCK_END;

		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"{PowerType}");
			return str.ToString();
		}
	}

	public class PowerHistoryFullEntity : IPowerHistoryEntry
	{
		public PowerType PowerType => PowerType.FULL_ENTITY;
		public PowerHistoryEntity Entity { get; set; }

		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"{PowerType} - Creating Entity = [{Entity.Print()}]");
			return str.ToString();
		}
	}

	public class PowerHistoryShowEntity : IPowerHistoryEntry
	{
		public PowerType PowerType => PowerType.SHOW_ENTITY;
		public PowerHistoryEntity Entity { get; set; }
		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"{PowerType} - Updating Entity = [{Entity.Print()}]");
			return str.ToString();
		}
	}

	//message PowerHistoryHide
	//{
	//    required int32 entity = 1;
	//    required int32 zone = 2;
	//}
	public class PowerHistoryHideEntity : IPowerHistoryEntry
	{
		public PowerType PowerType => PowerType.HIDE_ENTITY;
		public int EntityID { get; set; }
		public Zone Zone { get; set; }

		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"{PowerType} Entity = [{EntityID}] tag={Zone}");
			return str.ToString();
		}
	}

	//message PowerHistoryTagChange
	//{
	//    required int32 entity = 1;
	//    required int32 tag = 2;
	//    required int32 value = 3;
	//}
	public class PowerHistoryTagChange : IPowerHistoryEntry
	{
		public PowerType PowerType => PowerType.TAG_CHANGE;
		public int EntityId { get; set; }
		public GameTag Tag { get; set; }
		public int Value { get; set; }

		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine(Model.Tag.TypedTags.ContainsKey(Tag)
				? $"{PowerType} Entity = [{EntityId}] Tag={Tag} Value={Enum.GetName(Model.Tag.TypedTags[Tag], Value)}"
				: $"{PowerType} Entity = [{EntityId}] Tag={Tag} Value={Value}");
			return str.ToString();
		}

		public override string ToString()
		{
			return $"[{EntityId}][{Tag}][{Value}]";
		}
	}

	//message PowerHistoryMetaData
	//{
	//    repeated int32 info = 2;
	//    HistoryMeta.Type type = 3;
	//    int32 data = 4;
	//}
    // Native projects this frozen public burn onto a disposable actor, never the private retained identity.
    public sealed class PowerHistoryRetainedOverdraw : IPowerHistoryEntry
    {
        public PowerType PowerType => PowerType.META_DATA;
        public PowerHistoryEntity Entity { get; set; }
        public int Player { get; set; }
        public string Print() => $"RETAINED_OVERDRAW Player={Player} Entity=[{Entity.Print()}]";
    }

    public class PowerHistorySubSpellStart : IPowerHistoryEntry
    {
        public PowerType PowerType => PowerType.SUB_SPELL_START;
        public string SpellPrefabGuid { get; set; }
        public int Source { get; set; }
        public bool RequirePublicOrigin { get; set; }
        public List<int> Targets { get; set; } = new List<int>();
        public string Print() => $"{PowerType} SpellPrefabGUID={SpellPrefabGuid} Source={Source} TargetCount={Targets.Count} Targets={string.Join(",", Targets)}";
    }

    public class PowerHistorySubSpellEnd : IPowerHistoryEntry
    {
        public PowerType PowerType => PowerType.SUB_SPELL_END;
        public string Print() => PowerType.ToString();
    }

    public class PowerHistoryShuffleDeck : IPowerHistoryEntry
    {
        public PowerType PowerType => PowerType.SHUFFLE_DECK;
        public int PlayerId { get; set; }
        public string Print() => $"{PowerType} PlayerId={PlayerId}";
    }

	public class PowerHistoryMetaData : IPowerHistoryEntry
	{
		public PowerType PowerType => PowerType.META_DATA;
		public MetaDataType Type { get; set; }
		public int Data { get; set; }
		public List<int> Info { get; set; }

		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"{PowerType} Type={Type} Data={Data} Info={string.Join(", ", Info)}");
			return str.ToString();
		}
	}

	public class PowerHistoryChangeEntity : IPowerHistoryEntry
	{
		public PowerType PowerType => PowerType.CHANGE_ENTITY;
		public string CardId { get; set; }
		public PowerHistoryEntity Entity { get; set; }

		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"{PowerType} Type={CardId} - Change Entity = [{Entity.Print()}]");
			return str.ToString();
		}
	}

	//message PowerHistoryEntity
	//{
	//    required int32 entity = 1;
	//    required string name = 2;
	//    repeated Tag tags = 3;
	//}
	//message Tag
	//{
	//    required int32 name = 1;
	//    required int32 value = 2;
	//}
	public class PowerHistoryEntity
	{
		public int Id { get; set; }
		public string Name { get; set; }
		public IDictionary<GameTag, int> Tags { get; set; } = new Dictionary<GameTag, int>();
		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"Id={Id}, Name={Name}, Tags=[");
			foreach (KeyValuePair<GameTag, int> pair in Tags)
			{
				str.AppendLine(Tag.TypedTags.ContainsKey(pair.Key)
					? $"      [{pair.Key},{Enum.GetName(Tag.TypedTags[pair.Key], pair.Value)}]"
					: $"      [{pair.Key},{pair.Value}]");
			}
			str.Append("]");
			return str.ToString();
		}
	}

	//message Player
	//{
	//    required int32 id = 1;
	//    required pegasus.pegasusshared.BnetId game_account_id = 2;
	//    required int32 card_back = 3;
	//    required Entity entity = 4;
	//}
	public class PowerPlayer
	{
		public int PlayerId { get; set; }
		public int AccountId { get; set; }
		public int CardBack { get; set; }
		public PowerEntity PowerEntity { get; set; }
		public string Print()
		{
			var str = new StringBuilder();
			str.Append($"PlayerId={PlayerId}, AccountId={AccountId}, CardBack={CardBack}, Entity=[{PowerEntity.Print()}]");
			return str.ToString();
		}
	}

	//message Entity
	//{
	//    required int32 id = 1;
	//    repeated Tag tags = 2;
	//}
	public class PowerEntity
	{
		public int Id { get; set; }
		public Dictionary<GameTag, int> Tags { get; set; } = new Dictionary<GameTag, int>();
		public string Print()
		{
			var str = new StringBuilder();
			str.AppendLine($"Id={Id} Tags=[");
			foreach (KeyValuePair<GameTag, int> pair in Tags)
			{
				str.AppendLine($"      [{pair.Key},{pair.Value}]");
			}
			str.Append("]");
			return str.ToString();
		}
	}
}
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
