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
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Kettle;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Zones;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public class TransformCopyTask : SimpleTask
	{
		private readonly bool _addToStack;

		public TransformCopyTask(bool addToStack = false)
		{
			_addToStack = addToStack;
		}

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
            return ProcessBoardState(game, controller, source, target, stack);
        }

        internal TaskState ProcessBoardState(in Game game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null, bool preserveBoardState = false)
        {
			var minionTarget = target as Minion;
			if (minionTarget == null)
				return TaskState.STOP;

			var minionSource = source as Minion;
			if (minionSource?.Zone?.Type != Zone.PLAY)
				return TaskState.STOP;


            if (TransformRules.Intercept(minionSource, minionTarget.Card, out var destination))
            {
                if (destination != null) Generic.ChangeEntity(minionSource.Controller, minionSource, destination, true, intercept: false);
                if (_addToStack) stack.Playables = new IPlayable[] { minionSource };
                return TaskState.COMPLETE;
            }
            int attacks = minionSource.NumAttacksThisTurn;

			//if (game.History)
			//	tags.Add(GameTag.PREMIUM, minionTarget[GameTag.PREMIUM]);

			//var copy = (Minion) Entity.FromCard(in controller, minionTarget.Card, tags);

			// Copy tags
            if (DormantRules.HasState(minionSource)) DormantRules.LeavePlay(minionSource);
			{
				int copySource = minionSource[PayloadRules.CopySourceController];
                bool wasFrozen = minionSource.IsFrozen;
                bool wasSilenced = minionSource.IsSilenced;
				EntityData sourceTags = minionSource._data;
				sourceTags.CopyFrom(in minionTarget._data);
                if (copySource != 0) sourceTags[PayloadRules.CopySourceController] = copySource;
                else sourceTags.Remove(PayloadRules.CopySourceController);
                // Raw tag copying bypasses setters; publish changed statuses to the client.
                if (game.History && wasFrozen != minionSource.IsFrozen)
                    game.PowerHistory.Add(PowerHistoryBuilder.TagChange(minionSource.Id, GameTag.FROZEN, minionSource.IsFrozen ? 1 : 0));
                if (game.History && wasSilenced != minionSource.IsSilenced)
                    game.PowerHistory.Add(PowerHistoryBuilder.TagChange(minionSource.Id, GameTag.SILENCED, minionSource.IsSilenced ? 1 : 0));
				if (game.History)
				{
					// Preserve destination readiness so the transform emits any exhaustion change.
					sourceTags[GameTag.EXHAUSTED] = minionSource.IsExhausted ? 1 : 0;
					sourceTags[GameTag.ENTITY_ID] = minionSource.Id;
					sourceTags[GameTag.CONTROLLER] = minionSource.Controller.PlayerId;
					sourceTags[GameTag.ZONE_POSITION] = minionSource.ZonePosition + 1;
				}
			}

			
			minionSource = (Minion) Generic.ChangeEntity(controller, minionSource, minionTarget.Card, true, minionTarget, intercept: false);

			minionTarget.CopyInternalAttributes(minionSource);
            if (preserveBoardState) minionSource.NumAttacksThisTurn = attacks;
            DeathContextRules.CopyShield(minionTarget, minionSource);

			//Trigger trigger = minionTarget.ActivatedTrigger;
			IAura aura = minionTarget.OngoingEffect;

			if (minionSource == game.CurrentEventData?.EventSource)
				game.CurrentEventData.EventSource = minionSource;

			// Copy Enchantments
			if (minionTarget.AppliedEnchantments != null)
				foreach (Enchantment e in minionTarget.AppliedEnchantments)
				{
					if (DormantRules.IsHelper(e) || preserveBoardState && Generic.AuraAttachment(e.Creator?.OngoingEffect, e.Card)) continue;
					Enchantment instance = Enchantment.CopyAttachment(in controller, preserveBoardState ? e.Creator : minionSource, minionSource, e);
					if (preserveBoardState ? e.NativeTags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_1) : e[GameTag.TAG_SCRIPT_DATA_NUM_1] > 0)
					{
						instance[GameTag.TAG_SCRIPT_DATA_NUM_1] = e[GameTag.TAG_SCRIPT_DATA_NUM_1];
						if (preserveBoardState ? e.NativeTags.ContainsKey(GameTag.TAG_SCRIPT_DATA_NUM_2) : e[GameTag.TAG_SCRIPT_DATA_NUM_2] > 0)
							instance[GameTag.TAG_SCRIPT_DATA_NUM_2] = e[GameTag.TAG_SCRIPT_DATA_NUM_2];
					}
					instance.CapturedCard = e.CapturedCard;
					PayloadRules.CopyState(e, instance);
					if (e.Card.Id == "TIME_024e2") instance.Power.Trigger.Activate(instance);
					// Copied stats are already realized; Soulrest still needs its death/zone triggers.
					if (e.Card.Id == "DINO_417e") instance.Power.Trigger.Activate(instance);
					if (e.IsOneTurnActive)
						game.OneTurnEffectEnchantments.Add(instance);
				}
			PayloadRules.CopyState(minionTarget, minionSource);
			OldGodRules.CopyState(minionTarget, minionSource);
			OldGodRules.FinishCopy(minionSource);
            if (preserveBoardState)
            {
                int? cost = CostRules.CopyableScalar(minionTarget);
                CostRules.ClearScalarAuraMemory(minionSource);
                if (cost.HasValue && !CostRules.HasManaNumbers(minionTarget)) minionSource.Cost = cost.Value;
                CostRules.CopyState(minionTarget, minionSource);
                CostRules.CopyMemory(minionTarget, minionSource);
                DarkGifts.CopyKeywords(minionTarget, minionSource);
                BonusEffects.CopyState(minionTarget, minionSource);
                // The new form's aura will realize its own Enrage from copied damage.
                if (minionTarget.OngoingEffect is EnrageEffect enrage && enrage.Type == AuraType.SELF && minionTarget.Damage > 0)
                    foreach (var effect in enrage.EnchantmentCard.Power.Enchant.Effects) effect.RemoveFrom(minionSource);
                game.OneTurnEffects.RemoveAll(effect => effect.entityId == minionSource.Id);
                foreach (var effect in game.OneTurnEffects.ToArray())
                    if (effect.entityId == minionTarget.Id) game.OneTurnEffects.Add((minionSource.Id, effect.effect));
            }


			//foreach (KeyValuePair<GameTag, int> kvp in minionTarget._data)
			//	switch (kvp.Key)
			//	{
			//		case GameTag.ENTITY_ID:
			//		case GameTag.CONTROLLER:
			//		case GameTag.ZONE:
			//		case GameTag.ZONE_POSITION:
			//		case GameTag.CREATOR:
			//		case GameTag.PREMIUM:
			//		case GameTag.EXHAUSTED:
			//		case GameTag.DEATHRATTLE:
			//			continue;
			//		default:
			//			copy._data.Add(kvp.Key, kvp.Value);
			//			break;
			//	}

			if (aura != null && minionSource.OngoingEffect == null)
				aura.Clone(minionSource);

			//List<(int entityId, IEffect effect)> oneTurnEffects = controller.Game.OneTurnEffects;
			//for (int i = oneTurnEffects.Count - 1; i >= 0; i--)
			//{
			//	(int id, IEffect effect) = oneTurnEffects[i];

			//	if (id == target.Id)
			//		oneTurnEffects.Add((.Id, effect));
			//}

			if (minionTarget.HasCharge)
				minionSource.IsExhausted = false;
			else if (minionTarget.IsRush)
			{
				minionSource.AttackableByRush = true;
				game.RushMinions.Add(minionSource.Id);
			}

			if (_addToStack)
				stack.Playables = new IPlayable[] {minionSource};

			return TaskState.COMPLETE;
		}
	}
}
