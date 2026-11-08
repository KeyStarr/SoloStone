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
using SabberStoneCore.Exceptions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Model.Zones
{
	public class BoardZone : PositioningZone<Minion>, IZone
	{
		private int _untouchableCount;
		private bool _hasUntouchables;

		public List<AdjacentAura> AdjacentAuras = new List<AdjacentAura>();

		public BoardZone(Controller controller) : base(Zone.PLAY, Game.MAX_MINIONS_ON_BOARD)
		{
			Game = controller.Game;
			Controller = controller;
		}

		public int CountExceptUntouchables
        {
            get
            {
                int active = 0;
                for (int i = 0; i < _count; i++) if (!_entities[i].Untouchable) active++;
                return active;
            }
        }
        public bool HasUntouchables => CountExceptUntouchables != _count;
        public override Minion Random
        {
            get
            {
                Minion[] active = GetAll();
                return active.Length == 0 ? null : active[Game.Random.Next(active.Length)];
            }
        }

		[NonSerialized] private bool _addingMinion;
		private List<IPlayable> _slots;
        public int MinionCount => Count;
        public int SlotCount => _slots?.Count ?? Count;
        public int LocationCount => SlotCount - Count;
        public bool HasLocations => _slots != null;
        public IReadOnlyList<IPlayable> Slots => _slots != null ? _slots.AsReadOnly() : (IReadOnlyList<IPlayable>)this.ToArray();
        public IPlayable GetSlot(int physicalIndex) => _slots != null ? _slots[physicalIndex] : this[physicalIndex];
        public BoardObject[] GetLocations() => _slots == null ? Array.Empty<BoardObject>() : _slots.OfType<BoardObject>().ToArray();
        public override bool IsFull => SlotCount == Game.MAX_MINIONS_ON_BOARD;
        public override int FreeSpace => Game.MAX_MINIONS_ON_BOARD - SlotCount;

        public override string Hash(params GameTag[] ignore)
        {
            if (!HasLocations) return base.Hash(ignore);
            var text = new StringBuilder($"[Z:{Type} ");
            foreach (IPlayable entity in _slots) text.Append(entity.Hash(ignore));
            text.Append("[As:");
            foreach (Aura aura in Auras.OrderBy(a => a.Owner.Id)) text.Append(aura);
            return text.Append("]]").ToString();
        }

        public override string FullPrint()
        {
            if (!HasLocations) return base.FullPrint();
            var text = new StringBuilder($"{this}|");
            foreach (IPlayable entity in _slots)
            {
                string stats = entity is Minion m ? $"[{m.AttackDamage}/{m.Health}]" : $"[{((BoardObject)entity).Durability}]";
                text.Append($"[P{entity.ZonePosition}]{stats}[C{entity.Cost}]{entity}|");
            }
            return text.ToString();
        }

        private void RebuildMinions()
        {
            Array.Clear(_entities, 0, _entities.Length);
            _count = 0;
            foreach (IPlayable entity in _slots)
                if (entity is Minion minion) _entities[_count++] = minion;
        }

        private void InsertPhysical(IPlayable entity, int position)
        {
            if (entity == null || entity.Controller != Controller || IsFull || position > SlotCount)
                throw new ZoneException("Invalid board insertion");
            if (_slots == null) _slots = this.Cast<IPlayable>().ToList();
            int pos = position < 0 ? _slots.Count : position;
            _slots.Insert(pos, entity);
            RebuildMinions();
            entity.Zone = this;
            if (Game.History) entity[GameTag.ZONE] = (int)Type;
            for (int i = pos; i < _slots.Count; i++) _slots[i].ZonePosition = i;
            foreach (var aura in AdjacentAuras) aura.BoardChanged = true;
        }

        private void RemovePhysical(IPlayable entity)
        {
            if (entity.Zone != this || !_slots.Contains(entity)) throw new ZoneException("Invalid board removal");
            int pos = _slots.IndexOf(entity);
            _slots.RemoveAt(pos);
            RebuildMinions();
            for (int i = pos; i < _slots.Count; i++) _slots[i].ZonePosition = i;
            entity.Zone = null;
            entity.ActivatedTrigger?.Remove();
            foreach (var aura in AdjacentAuras) aura.BoardChanged = true;
            if (_slots.Count == _count) _slots = null;
        }

        public void Add(BoardObject entity, int zonePosition = -1)
        {
            InsertPhysical(entity, zonePosition);
            entity.OrderOfPlay = Game.NextOop;
            if (Controller.SecretZone.ModernQuests.Count != 0)
                Actions.QuestRules.FriendlyBoardChanged(Controller);
        }

        public BoardObject Remove(BoardObject entity)
        {
            Actions.DeathContextRules.Removing(entity);
            RemovePhysical(entity);
            entity.OngoingEffect?.Remove();
            return entity;
        }

        public override void MoveTo(Minion entity, int zonePosition = -1)
        {
			if (!HasLocations) base.MoveTo(entity, zonePosition);
			else InsertPhysical(entity, zonePosition);
			if (entity.Card.Dormant != null || Actions.DormantRules.HasPending(entity) || Actions.DormantRules.HasState(entity))
                Actions.DormantRules.Initialize(entity);
			Game.Facts.BoardEntered(entity, _addingMinion);
        }

        public override void ChangeEntity(Minion oldEntity, Minion newEntity)
        {
            if (!HasLocations) { base.ChangeEntity(oldEntity, newEntity); return; }
            _slots[oldEntity.ZonePosition] = newEntity;
            newEntity.ZonePosition = oldEntity.ZonePosition;
            newEntity.Zone = this;
            RebuildMinions();
        }

        public override void Swap(Minion oldEntity, Minion newEntity)
        {
            if (!HasLocations) { base.Swap(oldEntity, newEntity); return; }
            if (oldEntity.Zone != this || newEntity.Zone != this) throw new ZoneException("Swap zone mismatch");
            int a = oldEntity.ZonePosition, b = newEntity.ZonePosition;
            _slots[a] = newEntity;
            _slots[b] = oldEntity;
            newEntity.ZonePosition = a;
            oldEntity.ZonePosition = b;
            RebuildMinions();
            foreach (var aura in AdjacentAuras) aura.BoardChanged = true;
        }

        public override Minion Remove(int index)
        {
			if (Actions.DormantRules.HasState(this[index])) return Remove(this[index]);
            if (!HasLocations)
			{
				var removed = base.Remove(index);
				Game.Facts.BoardLeft(removed);
				return removed;
			}
            return Remove(this[index]);
        }

        int IZone.Count => Count;
        List<IPlayable> IZone.GetAll => HasLocations ? new List<IPlayable>(_slots) : this.Cast<IPlayable>().ToList();
        void IZone.Add(IPlayable entity, int position)
        {
            if (entity is BoardObject location) Add(location, position);
            else Add((Minion)entity, position);
        }
        void IZone.MoveTo(IPlayable entity, int position)
        {
            if (entity is BoardObject) InsertPhysical(entity, position);
            else MoveTo((Minion)entity, position);
        }
        IPlayable IZone.Remove(IPlayable entity) => entity is BoardObject location ? (IPlayable)Remove(location) : Remove((Minion)entity);

		public override void Add(Minion entity, int zonePosition = -1)
			=> Add(entity, zonePosition, applyEntrySetters: false);

		internal void Add(Minion entity, int zonePosition, bool applyEntrySetters, bool preserveAge = false)
		{
            if (!preserveAge) entity[GameTag.NUM_TURNS_IN_PLAY] = 0;
            Actions.EitherSideRules.SyncController(entity);
			Actions.HeraldRules.ConfigureSummon(entity, Actions.HeraldRules.Tier(entity.Controller));
            // Summon setters supersede intrinsic scaling before any insertion callbacks.
            if (applyEntrySetters) Actions.StatGainRules.BeforeEntry(entity);
            Actions.LifecycleRules.AdmitRecruit(entity);
			if (!HasLocations)
			{
				_addingMinion = true;
				try { base.Add(entity, zonePosition); }
				finally { _addingMinion = false; }
			}
            else
            {
                InsertPhysical(entity, zonePosition);
                if (entity.Card.Dormant != null || Actions.DormantRules.HasPending(entity) || Actions.DormantRules.HasState(entity)) Actions.DormantRules.Initialize(entity);
				Game.Facts.BoardEntered(entity);
                for (int i = Auras.Count - 1; i >= 0; i--) Auras[i].EntityAdded(entity);
            }
            entity.BoardEntryTurn = Game.Turn;
            if (!Actions.DormantRules.HasState(entity))
            {
                entity.StartDormant();
                entity.EnsureDormantCondition();
            }

			if (entity.Controller == Game.CurrentPlayer && !(entity.IsDormant && Actions.DormantRules.HasState(entity)))
			{
				if (!entity.HasCharge)
				{
					if (entity.IsRush)
					{
						entity.AttackableByRush = true;
						Game.RushMinions.Add(entity.Id);
					}
					else
						entity.IsExhausted = true;
				}
			}

			entity.OrderOfPlay = Game.NextOop;
            if (Actions.QuestRules.IsRift(entity)) entity.IsExhausted = false;

			ActivateAura(entity);

			for (int i = AdjacentAuras.Count - 1; i >= 0; i--)
				AdjacentAuras[i].BoardChanged = true;

			if (Controller.SecretZone.ModernQuests.Count != 0)
				Actions.QuestRules.FriendlyBoardChanged(Controller, settle: false);

			Game.TriggerManager.OnZoneTrigger(entity);

			if (entity.Card.Untouchable)
			{
				++_untouchableCount;
				_hasUntouchables = true;
			}
            if (Controller.SecretZone.ModernQuests.Count != 0)
                Actions.QuestRules.SettleBoardChanges(Controller);
		}

		internal static void InitialiseMinion(Minion entity)
		{
            entity[GameTag.NUM_TURNS_IN_PLAY] = 0;
			entity.BoardEntryTurn = entity.Game.Turn;
            if (entity.Game.CurrentPlayer != entity.Controller) return;
			if (entity.HasCharge) return;
			if (entity.IsRush)
			{
				entity.AttackableByRush = true;
				entity.Game.RushMinions.Add(entity.Id);
			}
			else
				entity.IsExhausted = true;
		}

		public override Minion Remove(Minion entity)
		{
			Actions.DeathContextRules.Removing(entity);
            Game.Facts.BoardLeft(entity);
            if (Actions.DormantRules.HasState(entity))
            {
                if (!Actions.DormantRules.IsControlTransfer(entity)) Actions.DormantRules.LeavePlay(entity);
            }
            else entity.LeaveDormant();
			RemoveAura(entity);
			for (int i = 0; i < AdjacentAuras.Count; i++)
				AdjacentAuras[i].BoardChanged = true;
			if (entity.Card.Untouchable && --_untouchableCount == 0)
				_hasUntouchables = false;
			if (!HasLocations) return base.Remove(entity);
            RemovePhysical(entity);
            for (int i = Auras.Count - 1; i >= 0; i--) Auras[i].EntityRemoved(entity);
            return entity;
		}

		//internal override void ChangeEntity(Minion oldEntity, Minion newEntity)
		//{
		//	if (oldEntity.Untouchable)
		//		if (!newEntity.Untouchable && --_untouchableCount == 0)
		//			_hasUntouchables = false;

		//	base.ChangeEntity(oldEntity, newEntity);
		//}

		/// <summary>
		/// Replaces an entity in this zone to new entity and returns the old entity.
		/// The position of the new entity is the same position the old entity had.
		/// </summary>
		/// <param name="oldEntity">The entity to be replaced.</param>
		/// <param name="newEntity">The new entity.</param>
		/// <returns></returns>
		public void Replace(Minion oldEntity, Minion newEntity)
		{
			int pos = oldEntity.ZonePosition;
            if (Actions.DormantRules.HasState(oldEntity)) Actions.DormantRules.LeavePlay(oldEntity);

			// Remove old Entity
			RemoveAura(oldEntity);
			for (int i = 0; i < Auras.Count; i++)
				Auras[i].EntityRemoved(oldEntity);
			oldEntity.ActivatedTrigger?.Remove();
			if (oldEntity.Card.Untouchable && --_untouchableCount == 0)
				_hasUntouchables = false;
			oldEntity.ZonePosition = 0;
			oldEntity.Controller.SetasideZone.Add(oldEntity);

			// Add new Entity
            newEntity.BoardEntryTurn = Game.Turn;
            newEntity[GameTag.NUM_TURNS_IN_PLAY] = oldEntity[GameTag.NUM_TURNS_IN_PLAY];
			newEntity.OrderOfPlay = Game.NextOop;
			if (!HasLocations) _entities[pos] = newEntity;
            else { _slots[pos] = newEntity; RebuildMinions(); }
			if (newEntity.Game.History)
				newEntity[GameTag.ZONE] = (int)Type;
			newEntity.Zone = this;
			newEntity.ZonePosition = pos;
			Game.Facts.TransformReplacement(oldEntity,newEntity);
            if (newEntity.Card.Dormant != null || Actions.DormantRules.HasPending(newEntity) || Actions.DormantRules.HasState(newEntity)) Actions.DormantRules.Initialize(newEntity);
			ActivateAura(newEntity);
			if (newEntity.Card.Untouchable)
			{
				++_untouchableCount;
				_hasUntouchables = true;
			}
			Auras.ForEach(a => a.EntityAdded(newEntity));
			AdjacentAuras.ForEach(a => a.BoardChanged = true);
			if (!newEntity.HasCharge)
			{
				if (newEntity.IsRush)
				{
					newEntity.AttackableByRush = true;
					Game.RushMinions.Add(newEntity.Id);
				}
				else
					newEntity.IsExhausted = true;
			}
			if (Controller.SecretZone.ModernQuests.Count != 0)
				Actions.QuestRules.FriendlyBoardChanged(Controller);
		}

		/// <summary>
		/// Activates a <see cref="Minion"/>'s <see cref="Triggers.Trigger"/> and <see cref="Aura"/> and
		/// applies it's Spell Power increment.
		/// </summary>
		/// <param name="entity"></param>
		public static void ActivateAura(Minion entity)
		{
            if (entity.IsSilenced || entity.IsDormant) return;
			entity.Power?.Trigger?.Activate(entity);
			entity.Power?.Aura?.Activate(entity);
		}

		private static void RemoveAura(Minion entity)
		{
			entity.OngoingEffect?.Remove();
		}

		/// <summary>
		/// Gets all board minions except untouchables(dormant).
		/// </summary>
		/// <returns></returns>
		public override Minion[] GetAll()
		{
			return HasUntouchables ? GetAll(null) : base.GetAll();
		}

		/// <summary>
		/// Gets all board minions satisfying the given predicate except untouchables(dormant).
		/// </summary>
		/// <param name="predicate"></param>
		/// <returns></returns>
		public override Minion[] GetAll(Func<Minion, bool> predicate)
		{
            Func<Minion, bool> condition = predicate;
            predicate = p => !p.Untouchable && (condition == null || condition(p));
			return base.GetAll(predicate);
		}

		internal override void CopyTo(Array destination, int index)
		{
			if (HasUntouchables)
			{
				Array.Copy(GetAll(null), 0, destination, index, CountExceptUntouchables);
				return;
			}

			base.CopyTo(destination, index);
		}

		internal void DecrementUntouchablesCount()
		{
			if (!_hasUntouchables) return;
			if (--_untouchableCount == 0)
				_hasUntouchables = false;
		}

		public void Stamp(BoardZone zone)
        {
            if (HasLocations)
            {
                zone._hasUntouchables = _hasUntouchables;
                zone._untouchableCount = _untouchableCount;
                zone._slots = new List<IPlayable>(_slots.Count);
                foreach (IPlayable entity in _slots)
                {
                    IPlayable copy = entity.Clone(zone.Controller);
                    copy.Zone = zone;
                    zone._slots.Add(copy);
                }
                zone.RebuildMinions();
                return;
            }
            zone._slots = null;
			zone._hasUntouchables = _hasUntouchables;
			zone._untouchableCount = _untouchableCount;
			zone._count = _count;

			Minion[] entities = _entities;
			Minion[] src = zone._entities;
			for (int i = 0; i < _count; ++i)
			{
				Minion copy = (Minion)entities[i].Clone(zone.Controller);
				copy.Zone = zone;
				src[i] = copy;
			}
		}
	}
}
