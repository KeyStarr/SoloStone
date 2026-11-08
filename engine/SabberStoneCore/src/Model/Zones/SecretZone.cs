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
using SabberStoneCore.Enums;
using SabberStoneCore.Exceptions;
using SabberStoneCore.Model.Entities;
using System.Collections.Generic;

namespace SabberStoneCore.Model.Zones
{
	public class SecretZone : LimitedZone<Spell>
	{
		public const int SecretZoneMaxSize = 5;

		/// <summary>
		/// An unique field for Quest.
		/// Gets or sets the quest in this SecretZone.
		/// null means absence.
		/// </summary>
		public Spell Quest { get; set; }

        private List<Spell> _modernQuests;
        private List<Spell> _auras;
        public IReadOnlyList<Spell> Auras => _auras?.Where(s => s.IsAura).ToArray() ?? Array.Empty<Spell>();
        public IReadOnlyList<Spell> ModernQuests => (IReadOnlyList<Spell>)_modernQuests ?? Array.Empty<Spell>();
        public int OccupiedSecretSlots => _count + (_modernQuests?.Count ?? 0)
            + (Quest != null && _modernQuests?.Any(q => q.Card[(GameTag)1192] != 0) == true ? 1 : 0);

		public SecretZone(Controller controller) : base(Zone.SECRET, SecretZoneMaxSize)
		{
			Game = controller.Game;
			Controller = controller;
		}

		private SecretZone(Controller c, SecretZone zone) : base(c, zone)
		{
			Quest = (Spell) zone.Quest?.Clone(c);
            if (zone._auras != null)
            {
                _auras = new List<Spell>(zone._auras.Count);
                foreach (var aura in zone._auras)
                {
                    var copy = (Spell)aura.Clone(c);
                    copy.Zone = this;
                    _auras.Add(copy);
                }
            }
            if (zone._modernQuests != null)
            {
                _modernQuests = new List<Spell>(zone._modernQuests.Count);
                foreach (var quest in zone._modernQuests)
                {
                    var copy = (Spell)quest.Clone(c);
                    copy.Zone = this;
                    _modernQuests.Add(copy);
                }
            }
		}

		public override bool IsFull => OccupiedSecretSlots == SecretZoneMaxSize;
        public override int FreeSpace => SecretZoneMaxSize - OccupiedSecretSlots;

        public override void MoveTo(Spell entity, int zonePosition = -1)
        {
            if (entity.IsAura || Actions.ObjectiveRules.IsObjective(entity))
            {
                if (entity.Controller != Controller || entity.Zone != null && entity.Zone != this)
                    throw new ZoneException("Aura must be detached and owned by this controller.");
                (_auras ??= new List<Spell>()).Add(entity);
                entity.Zone = this;
                entity[GameTag.ZONE] = (int)Type;
                entity.ZonePosition = _auras.Count - 1;
                entity[GameTag.ZONE_POSITION] = _auras.Count;
                entity.OrderOfPlay = Game.NextOop;
                return;
            }
            if ((_modernQuests?.Count ?? 0) != 0 && IsFull)
                throw new ZoneException("The shared secret area is full.");
            base.MoveTo(entity, zonePosition);
            // Generated secrets never visited a positioning zone to publish their cached position.
            if (Game.History) entity[GameTag.ZONE_POSITION] = entity.ZonePosition + 1;
        }

        public override Spell Remove(Spell entity)
        {
            if (_auras != null && _auras.Remove(entity))
            {
                entity.ActivatedTrigger?.Remove();
                entity.OngoingEffect?.Remove();
                entity.Zone = null;
                for (int i = 0; i < _auras.Count; i++)
                {
                    _auras[i].ZonePosition = i;
                    _auras[i][GameTag.ZONE_POSITION] = i + 1;
                }
                return entity;
            }
            if (_modernQuests != null && _modernQuests.Remove(entity))
            {
                entity.ActivatedTrigger?.Remove();
                entity.Zone = null;
                return entity;
            }
            return base.Remove(entity);
        }

		public override void Add(Spell entity, int zonePosition = -1)
		{
            if (entity.IsAura || Actions.ObjectiveRules.IsObjective(entity)) { MoveTo(entity, zonePosition); return; }
			if (entity.Card.IsQuest && QuestDefinition.TryGet(entity.Card.AssetId, out _))
            {
                if (!Actions.QuestRules.CanInstall(Controller, entity.Card))
                    throw new ZoneException("Another quest is active or the secret area is full.");
                (_modernQuests ??= new List<Spell>()).Add(entity);
                RevealQuest(entity);
                entity[GameTag.ZONE] = (int)Type;
                entity.Zone = this;
                if (Game.History) entity[GameTag.ZONE_POSITION] = entity.ZonePosition + 1;
                entity.OrderOfPlay = Game.NextOop;
                return;
            }
            if (entity.Card.IsQuest)
            {
				if (Quest != null || _modernQuests?.Any(q => q.Card[(GameTag)1192] == 0) == true)
					throw new ZoneException($"Another quest is already in play");

				Quest = entity;
				RevealQuest(entity);
				entity[GameTag.ZONE] = (int)Type;
				entity.Zone = this;

				Game.Log(LogLevel.DEBUG, BlockType.PLAY, "Zone", !Game.Logging ? "" : $"Quest {entity} has been added to zone '{Type}'.");

				entity.OrderOfPlay = Game.NextOop;

				return;
			}

			base.Add(entity, zonePosition);
			Game.Log(LogLevel.DEBUG, BlockType.PLAY, "Zone", !Game.Logging ? ""
				: $"Entity '{entity} ({entity.Card.Type})' has been added to zone '{Type}' in position '{entity.ZonePosition}'."); 

			entity.OrderOfPlay = Game.NextOop;
		}

        private void RevealQuest(Spell entity)
        {
            if (!Game.History) return;
            // Native actors need identity with their first public SECRET appearance.
            var reveal = Kettle.PowerHistoryBuilder.ShowEntity(entity);
            reveal.Entity.Tags[GameTag.ZONE] = (int)Type;
            reveal.Entity.Tags[GameTag.ZONE_POSITION] = entity.ZonePosition + 1;
            Game.PowerHistory.Add(reveal);
        }

		public override void ChangeEntity(Spell oldEntity, Spell newEntity)
		{
			throw new NotImplementedException();
		}

		public new Iteration.SecretZoneEnumerator GetEnumerator()
		{
			return new Iteration.SecretZoneEnumerator(this);
		}

		protected override IEnumerator<Spell> CreateEnumerator()
		{
			return new Iteration.SecretZoneEnumerator(this);
		}

		internal List<Spell> ModernQuestList => _modernQuests;
		internal List<Spell> AuraList => _auras;

		public SecretZone Clone(Controller c)
		{
			return new SecretZone(c, this);
		}
	}
}
