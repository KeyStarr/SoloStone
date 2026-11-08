using System;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using System.Collections.Generic;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.Actions
{
	public partial class Generic
	{
		public static IPlayable Copy(in Controller controller, in IEntity creator, in IPlayable source, Zone targetZone, bool deathrattle = false, Action<IPlayable> initialize = null, int zonePosition = -1, bool preserveBoardMinionState = false, bool preserveCapturedState = false, bool deferSummonAfter = false)
		{
            if (preserveBoardMinionState && !(source is Minion && source.Game == controller.Game && source.Zone == source.Controller.BoardZone && targetZone == Zone.PLAY))
                throw new ArgumentException("Board minion state requires a same-game board-to-board minion copy.");
			if (preserveCapturedState && targetZone != Zone.SETASIDE && !PayloadRules.IsStored(source))
                throw new ArgumentException("Captured-state release requires a stored source.");
            // Determine whether enchantments should be also copied.
			// Whenever a card moves forward in that flow (Deck -> Hand, Hand -> Play, Deck -> Play),
			// it retains enchantments. If a card moves backwards in zones
			// (Play -> Hand, Hand -> Deck, Play -> Deck, Play/Hand/Deck -> Graveyard and
			// Graveyard -> Play/Hand/Deck), it loses enchantments.
			// https://playhearthstone.com/en-gb/blog/21965466
			bool copyEnchantments;

			Zone sourceZone = source.Zone?.Type ?? Zone.PLAY;

			if (sourceZone == Zone.GRAVEYARD)
				copyEnchantments = false;
			else if (sourceZone == targetZone)
				copyEnchantments = true;
			else if (targetZone == Zone.SETASIDE)
				copyEnchantments = false;
			else if (targetZone == Zone.PLAY)
				copyEnchantments = true;
			else if (sourceZone == Zone.DECK)
				copyEnchantments = true;
			else if (sourceZone == Zone.HAND && targetZone != Zone.DECK)
				copyEnchantments = true;
			else
				copyEnchantments = false;

			IPlayable copiedEntity;

			if (preserveCapturedState) copyEnchantments = true;

			if (copyEnchantments)
			{
                var tags = preserveBoardMinionState || preserveCapturedState ? new EntityData((EntityData)source.NativeTags) : new EntityData();
                if (preserveBoardMinionState || preserveCapturedState)
                {
                    tags.Remove(GameTag.ENTITY_ID);
                    tags.Remove(GameTag.CONTROLLER);
                    tags.Remove(GameTag.ZONE);
                    tags.Remove(GameTag.ZONE_POSITION);
                    tags.Remove(OutcastRules.PlayedFromEdgeTag);
                    tags[GameTag.EXHAUSTED] = 0;
                    tags[GameTag.ATTACKABLE_BY_RUSH] = 0;
                }
                foreach (GameTag tag in new List<GameTag>(tags.Keys))
                    if (PayloadRules.IsInternalTag(tag)) tags.Remove(tag);
                if (preserveCapturedState && targetZone == Zone.SETASIDE) tags[PayloadRules.Stored] = 1;
                tags[GameTag.DISPLAYED_CREATOR] = creator.Id;
                if (source.Card.Id == "EDR_950")
                {
                    tags.Remove(GameTag.TAG_SCRIPT_DATA_NUM_6);
                    tags.Remove((GameTag)955);
                }

				copiedEntity = Entity.FromCard(in controller, source.Card, tags);
                if (source[HandProgressRules.PrivateAttachmentTarget] != 0)
                    copiedEntity[HandProgressRules.PrivateAttachmentTarget] = copiedEntity.Id;

				int? modifiedCost = CostRules.CopyableScalar((Playable)source);
                CostRules.ClearScalarAuraMemory((Playable)copiedEntity);

				if (modifiedCost.HasValue && !CostRules.HasManaNumbers(source))
					copiedEntity.Cost = modifiedCost.Value;

                // Suppress printed powers first; realized buffs and copied attachments follow below.
                if (!preserveBoardMinionState && source is Minion silencedSource && silencedSource.IsSilenced)
                {
                    ((Minion)copiedEntity).Silence();
                    // Silence must not erase keywords granted afterwards; cached attributes are restored below.
                    foreach (var tag in new[] { GameTag.FROZEN, GameTag.ENRAGED, GameTag.CHARGE, GameTag.RUSH,
                        GameTag.CANNOT_ATTACK_HEROES, GameTag.WINDFURY, GameTag.DIVINE_SHIELD, GameTag.DEATHRATTLE,
                        GameTag.BATTLECRY, GameTag.INSPIRE, GameTag.LIFESTEAL, GameTag.POISONOUS, GameTag.REBORN,
                        GameTag.SPELLPOWER })
                        copiedEntity[tag] = silencedSource.GetNativeGameTag(tag);
                }

				if (copiedEntity is Character c)
					((Character)source).CopyInternalAttributes(in c);

                if (source is Weapon && source.AppliedEnchantments?.Exists(e => e.Card.Id == "END_012e") == true)
                    copiedEntity[GameTag.ATK] = source[GameTag.ATK];
                DarkGifts.CopyKeywords(source, copiedEntity);
				BonusEffects.CopyState(source, copiedEntity);


				if (source.AppliedEnchantments != null)
				{
					foreach (Enchantment e in source.AppliedEnchantments)
					{
                        // External aura actors belong to their destination, not to copied state.
                        if (preserveBoardMinionState && AuraAttachment(e.Creator?.OngoingEffect, e.Card)) continue;
                        if (e.Card.Id == "EDR_469e" || DormantRules.IsHelper(e) || e.Card.Id == "GBL_999e") continue;
                        // Destination adjacency owns presentation; never inherit another slot's actor.
                        if (e.Creator?.OngoingEffect is HandAdjacencyCostAura adjacency && adjacency.OwnsPresentation(e)) continue;
						Enchantment instance = Enchantment.CopyAttachment(in controller, e.Creator, copiedEntity, e);
						if (e.NativeTags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_1))
						{
							instance[GameTag.TAG_SCRIPT_DATA_NUM_1] = e[GameTag.TAG_SCRIPT_DATA_NUM_1];
							if (e.NativeTags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_2))
								instance[GameTag.TAG_SCRIPT_DATA_NUM_2] = e[GameTag.TAG_SCRIPT_DATA_NUM_2];
						}
						instance.CapturedCard = e.CapturedCard;
                        PayloadRules.CopyState(e, instance);
                        HandTimelineRules.CopyFire(e, instance);
                        if (e.Card.CopyHandTrigger)
                        {
                            foreach (var tag in new[] { GameTag.TAG_SCRIPT_DATA_NUM_1, GameTag.TAG_SCRIPT_DATA_NUM_2, PayloadRules.Generation }) instance[tag] = e[tag];
                        }
                        foreach (var tag in e.NativeTags)
                            if (DiscoverRealRules.IsInternalTag(tag.Key)) instance[tag.Key] = tag.Value;

						// Copied stats are already realized; Soulrest still needs its death/zone triggers.
						if ((e.Card.Id == "DINO_417e" || e.Card.Id == "TIME_024e2") && !(preserveCapturedState && targetZone == Zone.SETASIDE)) instance.Power.Trigger.Activate(instance);

						if (e.IsOneTurnActive && e.Card.Id != "JAIL_907e02")
							instance.ExpireAtEndOfTurn();
					}
					
				}

                CostRules.CopyState((Playable)source, (Playable)copiedEntity);
                CostRules.CopyMemory(source, copiedEntity);
                LifecycleRules.CopyState(source, copiedEntity);
                if (source is Spell && source[GameTag.SPELLPOWER] != 0) copiedEntity[GameTag.SPELLPOWER] = source[GameTag.SPELLPOWER];
                if (source[GameTag.CHOOSE_BOTH] != 0) copiedEntity[GameTag.CHOOSE_BOTH] = source[GameTag.CHOOSE_BOTH];
                if (source.Card.Shatter != null) ShatterRules.CopyCosts(source, copiedEntity);

				//var kvps = new KeyValuePair<GameTag, int>[source.NativeTags.Count];
				//source.NativeTags.CopyTo(kvps, 0);

				//for (int i = 0; i < kvps.Length; i++)
				//{
				//	switch (kvps[i].Key)
				//	{
				//		case GameTag.ENTITY_ID:
				//		case GameTag.CONTROLLER:
				//		case GameTag.ZONE:
				//		case GameTag.ZONE_POSITION:
				//		case GameTag.CREATOR:
				//		case GameTag.DISPLAYED_CREATOR:
				//		case GameTag.EXHAUSTED:
				//			continue;
				//		case GameTag.COST:
				//			copiedEntity.AuraEffects.ToBeUpdated = true;
				//			goto default;
				//		default:
				//			tags.Add(kvps[i]);
				//			break;
				//	}
				//}

				List<(int entityId, IEffect effect)> oneTurnEffects = controller.Game.OneTurnEffects;
				for (int i = oneTurnEffects.Count - 1; i >= 0; i--)
				{
					(int id, IEffect effect) = oneTurnEffects[i];
					if (id == source.Id)
						oneTurnEffects.Add((copiedEntity.Id, effect));
				}
			}
			else
			{
				copiedEntity = Entity.FromCard(in controller, source.Card);
                if (source[HandProgressRules.PrivateAttachmentTarget] != 0)
                    copiedEntity[HandProgressRules.PrivateAttachmentTarget] = copiedEntity.Id;
				copiedEntity.NativeTags.Add(GameTag.DISPLAYED_CREATOR, creator.Id);
			}

            if (sourceZone == Zone.PLAY && targetZone == Zone.PLAY) OldGodRules.CopyState(source,copiedEntity);
            ObjectiveRules.CopyComposition(source, copiedEntity);
            if (copyEnchantments) PayloadRules.CopyState(source, copiedEntity, targetZone == Zone.HAND);
            else if (sourceZone == Zone.GRAVEYARD && targetZone == Zone.PLAY)
                PayloadRules.CopyState(source,copiedEntity,resummon: true);
            LookoutPreviewRules.HandExit(copiedEntity);
            if (sourceZone == Zone.PLAY && targetZone == Zone.PLAY) DeathContextRules.CopyShield(source, copiedEntity);

            if (source.Card.Shatter != null) ShatterRules.CopyState(source, copiedEntity);
            if (source.Card.HandProgress != null) HandProgressRules.CopyState(source, copiedEntity);

            // Board copies retain awakened state and an independent remaining timer.
            if (sourceZone == Zone.PLAY && targetZone == Zone.PLAY && source is Minion original && !DormantRules.HasState(original) &&
                (source.Card.Id == "CORE_BT_156" || source.Card.Id == "EDR_469"))
            {
                ((Minion)copiedEntity).CopiedDormantState = original[GameTag.DORMANT];
                copiedEntity[GameTag.TAG_SCRIPT_DATA_NUM_1] = original[GameTag.TAG_SCRIPT_DATA_NUM_1];
            }

			if (copyEnchantments && HeraldRules.IsSoldier(source.Card))
            {
                if (source is Minion soldier && soldier.IsSilenced)
                {
                    ((Minion)copiedEntity).IsSilenced = true;
                    copiedEntity.HasDeathrattle = source.HasDeathrattle;
                }
                copiedEntity[HeraldRules.RealizedFactor] = source[HeraldRules.RealizedFactor];
                copiedEntity[GameTag.TAG_SCRIPT_DATA_NUM_1] = source[GameTag.TAG_SCRIPT_DATA_NUM_1];
            }

            ColossalRules.CopyRefill(source, copiedEntity, copyEnchantments && sourceZone == Zone.PLAY && targetZone == Zone.PLAY);

            if (copyEnchantments && source[ColossalRules.RealizedFactor] > 0)
            {
                copiedEntity[ColossalRules.RealizedFactor] = source[ColossalRules.RealizedFactor];
                copiedEntity[GameTag.TAG_SCRIPT_DATA_NUM_1] = source[GameTag.TAG_SCRIPT_DATA_NUM_1];
            }

            if (copyEnchantments && targetZone == Zone.HAND)
                PrepareRules.CopyState(source, copiedEntity);

			if (sourceZone == Zone.PLAY && targetZone == Zone.PLAY && source is Minion modernSource && copiedEntity is Minion modernCopy && DormantRules.HasState(modernSource))
            {
                if (modernSource.IsSilenced) modernCopy.IsSilenced = true;
                DormantRules.CopyState(modernSource, modernCopy);
            }
            if (targetZone == Zone.HAND && TemporaryRules.IsTemporary(source)) TemporaryRules.CopyState(source, copiedEntity);

            if ((preserveBoardMinionState || preserveCapturedState) && copiedEntity is Minion)
            {
                var minionCopy = (Minion)copiedEntity;
                // Entry recalculates Enrage from copied damage; remove its already-realized stats first.
                if (source.OngoingEffect is EnrageEffect enrage && enrage.Type == AuraType.SELF && ((Minion)source).Damage > 0)
                    foreach (var effect in enrage.EnchantmentCard.Power.Enchant.Effects) effect.RemoveFrom(minionCopy);
                minionCopy.IsSilenced = ((Minion)source).IsSilenced;
                minionCopy.NumAttacksThisTurn = 0;
            }
			if (preserveCapturedState && targetZone == Zone.SETASIDE)
            {
                controller.Game.OneTurnEffects.RemoveAll(effect => effect.entityId == copiedEntity.Id);
                if (copiedEntity.AppliedEnchantments != null)
                    foreach (var actor in copiedEntity.AppliedEnchantments) actor.FreezeForPayload();
            }
            // Frozen payload storage is persistence, not a new provenance event.
            if (preserveCapturedState && targetZone == Zone.SETASIDE)
            {
                int origin = source[PayloadRules.CopySourceController];
                if (origin != 0) copiedEntity[PayloadRules.CopySourceController] = origin;
            }
            else PayloadRules.MarkCopy(source, copiedEntity);
            // Card-specific copy overrides must precede board entry and summon triggers.
			initialize?.Invoke(copiedEntity);

			switch (targetZone)
			{
				case Zone.HAND:
					Generic.AddHandPhase.Invoke(controller, copiedEntity);
					break;
				case Zone.DECK:
					Generic.ShuffleIntoDeck(controller, creator, copiedEntity, revealGenerated:
                        sourceZone == Zone.PLAY || sourceZone == Zone.GRAVEYARD
                        || sourceZone == Zone.HAND && source.Controller == controller
                        || sourceZone == Zone.SETASIDE && source.Controller == controller
                            && controller.Choice?.EntityStack?.Contains(source.Id) == true);
					break;
				case Zone.PLAY:
					int position = zonePosition;
					if (deathrattle)
					{
						position = source is BoardObject deadLocation ? deadLocation.LastBoardPosition : ((Minion) source).LastBoardPosition;
						if (position > controller.BoardZone.SlotCount)
							position = controller.BoardZone.SlotCount;
					}
                    if (copiedEntity is Weapon copiedWeapon && source.AppliedEnchantments?.Exists(e => e.Card.Id == "END_012e") == true)
                        controller.Hero.AddWeapon(copiedWeapon);
                    else if (copiedEntity is BoardObject location)
                    {
                        if (sourceZone == Zone.PLAY && source is BoardObject originalLocation)
                        {
                            location.Location = originalLocation.Location.Clone();
                            location[GameTag.HEALTH] = originalLocation[GameTag.HEALTH];
                            location[GameTag.DAMAGE] = originalLocation[GameTag.DAMAGE];
                            location.IsExhausted = originalLocation.IsExhausted;
                            location[GameTag.EXHAUSTED] = source[GameTag.EXHAUSTED];
                            location[GameTag.LOCATION_ACTION_COOLDOWN] = source[GameTag.LOCATION_ACTION_COOLDOWN];
                        }
                        controller.BoardZone.Add(location, position);
                    }
                    else if (deferSummonAfter) Generic.SummonEntry(controller.Game, (Minion)copiedEntity, position, creator);
                    else Generic.SummonBlock.Invoke(controller.Game, (Minion) copiedEntity, position, creator);
					break;
				case Zone.SETASIDE:
					controller.SetasideZone.Add(copiedEntity);
					break;
			}

			if (!preserveCapturedState && copyEnchantments && !(copiedEntity is Minion sleepingCopy && sleepingCopy.IsDormant) && source.OngoingEffect != null && copiedEntity.OngoingEffect == null)
				source.OngoingEffect.Clone(copiedEntity);

			OldGodRules.FinishCopy(copiedEntity);
            return copiedEntity;
		}
        internal static bool AuraAttachment(IAura aura, Card card)
        {
            if (aura is Aura ordinary) return ordinary.EnchantmentCard == card;
            if (aura is AdjacentAura adjacent) return adjacent.EnchantmentCard == card;
            if (aura is MultiAura multiple)
                foreach (var child in multiple) if (AuraAttachment(child, card)) return true;
            return false;
        }

	}
}
