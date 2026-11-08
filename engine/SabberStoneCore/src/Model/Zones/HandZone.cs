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
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model.Zones
{
	/// <summary>
	/// Zone for all entities which are held 'in hand'.
	/// </summary>
	public class HandZone : PositioningZone<IPlayable>
	{
		public HandZone(Controller controller) : base(Zone.HAND, Controller.MaxHandSize)
		{
			Game = controller.Game;
			Controller = controller;
		}

		private HandZone(Controller c, HandZone zone) : base(c, zone)
		{
		}

		public override void MoveTo(IPlayable entity, int zonePosition = -1)
		{
			if (_count == _entities.Length && !IsFull) Array.Resize(ref _entities, Capacity);
			var from=entity.Zone?.Type ?? Zone.INVALID;
            base.MoveTo(entity, zonePosition);
			Game.Facts.HandEntered(entity,from);
		}

        public int Capacity => Controller[GameTag.MAXHANDSIZE] > 0 ? Controller[GameTag.MAXHANDSIZE] : Controller.MaxHandSize;
		public override bool IsFull => _count >= Capacity;
        public override int FreeSpace => Math.Max(0, Capacity - _count);

        private int _cardStateMutationDepth, _cardStateEntryDepth, _cardStateBefore;
        private bool _drainingCardState;
        private readonly List<int> _heldZoneEvents = new List<int>();
        private bool _holdZoneEvents;
        // Space >= 0 marks an entered splitting parent: its count before entry, and a ZONE event still to fire.
        private readonly List<(int Id, int Generation, int Space)> _cardStateEntries = new List<(int, int, int)>();
        internal bool CardStateMutationActive => _cardStateMutationDepth != 0;
        internal bool CardStateDeferred => CardStateMutationActive || _cardStateEntryDepth != 0;

        private sealed class CardStateScope : IDisposable
        {
            private HandZone hand;
            private readonly bool mutation;
            internal CardStateScope(HandZone owner, bool replacing)
            {
                hand = owner; mutation = replacing;
                if (mutation) { if (hand._cardStateMutationDepth++ == 0) hand._cardStateBefore = hand.Count; }
                else hand._cardStateEntryDepth++;
            }
            public void Dispose()
            {
                if (hand == null) return;
                var owner = hand; hand = null;
                if (mutation)
                {
                    if (--owner._cardStateMutationDepth == 0)
                    {
                        if (owner.Controller.SecretZone.ModernQuests.Count != 0)
                            QuestRules.HandCountChanged(owner.Controller, owner._cardStateBefore, owner.Count);
                        QuestRules.DeliverPendingRewards(owner.Controller);
                    }
                }
                else owner._cardStateEntryDepth--;
                owner.DrainCardState();
                DrawReplacementRules.DeliverBacklog(owner.Controller);
            }
        }
        internal IDisposable BeginCardStateMutation() => new CardStateScope(this, true);
        internal IDisposable DeferCardStateEntry() => new CardStateScope(this, false);
        internal void QueueCardStateEntry(IPlayable card, int space = -1)
        {
            int generation = card[ShatterRules.Residence];
            if (!_cardStateEntries.Any(e => e.Id == card.Id && e.Generation == generation))
                _cardStateEntries.Add((card.Id, generation, space));
        }
        internal void HoldZoneEvents() => _holdZoneEvents = true;
        internal int[] TakeHeldZoneEvents()
        {
            _holdZoneEvents = false;
            var held = _heldZoneEvents.ToArray();
            _heldZoneEvents.Clear();
            return held;
        }
        private void DrainCardState()
        {
            if (CardStateDeferred || _drainingCardState) return;
            _drainingCardState = true;
            try
            {
                do
                {
                    var entries = _cardStateEntries.ToArray(); _cardStateEntries.Clear();
                    foreach (var entry in entries)
                    {
                        if (!Game.IdEntityDic.TryGetValue(entry.Id, out var card)) continue;
                        if (card[ShatterRules.Residence] == entry.Generation) ShatterRules.AfterHandEntry(card, entry.Space);
                        if (entry.Space >= 0) Game.TriggerManager.OnZoneTrigger(card);
                    }
                    if (this.Any(card => card.Card.Shatter != null)) ShatterRules.StabilizeHand(Controller);
                    QuestRules.CombineHeldRewards(Controller);
                } while (_cardStateEntries.Count != 0);
                CardSets.Client.Sets.GennCursedKing.Refresh(Controller);
            }
            finally { _drainingCardState = false; }
        }

		public override void Add(IPlayable entity, int zonePosition = -1)
		{
			int before = _count;
			base.Add(entity, zonePosition);
            PayloadRules.ReplaceCoin(entity);
            LifecycleRules.AdmitRecruit(entity);
            bool restoring = TemporaryRules.IsReturningSavedHand(entity);
            if (!restoring) entity.Power?.HandEntryTask?.Process(Game, Controller, entity, null);
            if (CardStateBoundary.HasPrivateAttachments(entity)) CardStateBoundary.EnrollAttachments(entity);
            if (entity.Card.HandProgress != null) HandProgressRules.OnHandEntry(entity, !restoring);
            if (entity.Card.Shatter != null)
            {
                CostRules.Manager((Playable)entity);
                entity[ShatterRules.Residence]++;
            }
			if (!CardStateMutationActive && Controller.SecretZone.ModernQuests.Count != 0)
				QuestRules.HandCountChanged(Controller, before, _count);

			 if (entity.Power?.Aura is AdaptiveCostEffect e)
				e.Activate((Playable)entity);
			else if (entity.Power?.Aura is HandAdjacencyCostAura adjacency && !(entity.OngoingEffect is HandAdjacencyCostAura))
                adjacency.Activate(entity);
            entity.Power?.Trigger?.Activate(entity, TriggerActivation.HAND);
            if (entity.AppliedEnchantments != null)
                foreach (var actor in entity.AppliedEnchantments.ToArray())
                    if (actor.Card.CopyHandTrigger && actor.ActivatedTrigger == null) actor.Power.Trigger.Activate(actor);
            CostRules.RefreshHandAuras(Game);
            GeneratedDefinitionRules.ActivateHandAuras(entity);
            CostRules.EnterHand(entity);
            if (TemporaryRules.IsTemporary(entity)) TemporaryRules.OnHandEntry(entity);

            // A splitting parent's reactions wait for its halves: they must not change what the split admits.
            if (ShatterRules.WillSplit(entity)) QueueCardStateEntry(entity, Capacity - before);
            else
            {
                if (_holdZoneEvents) _heldZoneEvents.Add(entity.Id);
                else Game.TriggerManager.OnZoneTrigger(entity);
                if (entity.Card.Shatter != null) QueueCardStateEntry(entity);
            }
            DrainCardState();
		}

		public override IPlayable Remove(IPlayable entity)
		{
            LookoutPreviewRules.HandExit(entity);
            PlayLockRules.HandExit(entity);
            PayloadRules.HandExit(entity);
			if (TemporaryRules.IsTemporary(entity)) TemporaryRules.OnHandExit(entity);
            // Calia's recorded Jailbird exception: own Prepare-trigger reductions survive forward play.
            int ownReduction = entity.Card.Id == "JAIL_453" && Game.CurrentEventData?.EventSource == entity
                ? entity.AppliedEnchantments?.Where(e => e.Card.Id == "JAIL_453e").Sum(e => e[GameTag.TAG_SCRIPT_DATA_NUM_1]) ?? 0 : 0;
            ((Playable)entity).ResetCost();
            if (ownReduction > 0) entity.Cost = Math.Max(0, entity.Card.Cost - ownReduction);
            FollowRules.HandExit(entity);
			entity.AppliedEnchantments?.ForEach(p => { if (!FollowRules.IsGrant(p)) p.ActivatedTrigger?.Remove(); });
			int before = _count;
			var removed = base.Remove(entity);
			if (!CardStateMutationActive && Controller.SecretZone.ModernQuests.Count != 0)
				QuestRules.HandCountChanged(Controller, before, _count);
			QuestRules.DeliverPendingRewards(Controller);
            DrawReplacementRules.DeliverBacklog(Controller);
            CostRules.RefreshHandAuras(Game);
            DrainCardState();
			return removed;
		}

        public override IPlayable Remove(int index) => Remove(this[index]);

        internal IPlayable DetachForEscrow(IPlayable card)
        {
            LookoutPreviewRules.HandExit(card);
            // Keep persistent modifiers; hand auras reattach once when escrow returns.
            card.OngoingEffect?.Remove();
            card.AppliedEnchantments?.ForEach(e=>e.ActivatedTrigger?.Remove());
            return base.Remove(card);
        }

		public IPlayable RemoveForTrade(IPlayable entity)
		{
            LookoutPreviewRules.HandExit(entity);
            FollowRules.HandExit(entity, trade: true);
            PlayLockRules.HandExit(entity);
            PayloadRules.HandExit(entity);
            if (TemporaryRules.IsTemporary(entity)) TemporaryRules.OnHandExit(entity);
			int before = _count;
			var removed = base.Remove(entity);
			if (!CardStateMutationActive && Controller.SecretZone.ModernQuests.Count != 0)
				QuestRules.HandCountChanged(Controller, before, _count);
			QuestRules.DeliverPendingRewards(Controller);
            DrawReplacementRules.DeliverBacklog(Controller);
            CostRules.RefreshHandAuras(Game);
            DrainCardState();
			return removed;
		}

        public override void Swap(IPlayable first, IPlayable second)
        {
            base.Swap(first, second);
            CostRules.RefreshHandAuras(Game);
            DrainCardState();
        }

        public override void ChangeEntity(IPlayable oldEntity, IPlayable newEntity)
        {
            base.ChangeEntity(oldEntity, newEntity);
            CostRules.RefreshHandAuras(Game);
        }

        // Swap committed slots without running hand-exit/entry or changing either hand count.
        internal void ExchangeSlots(HandZone other, IPlayable first, IPlayable second)
        {
            if (first.Zone != this || second.Zone != other || Game != other.Game)
                throw new ArgumentException("Exchange requires two current hand entities in one game.");
            int firstPosition = first.ZonePosition, secondPosition = second.ZonePosition;
            _entities[firstPosition] = second; other._entities[secondPosition] = first;
            first.Zone = other; second.Zone = this;
            first.ZonePosition = secondPosition; second.ZonePosition = firstPosition;
        }

		public HandZone Clone(Controller c)
		{
			return new HandZone(c, this);
		}
	}
}
