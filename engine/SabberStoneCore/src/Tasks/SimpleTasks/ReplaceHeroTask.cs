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
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Tasks.SimpleTasks
{
	public class ReplaceHeroTask : SimpleTask
	{
		private readonly Card _heroCard;
		private readonly Card _powerCard;
		private readonly Card _weaponCard;
		private readonly bool _removeSource = true;

		public ReplaceHeroTask(Card cardHero, Card cardWeapon, Card cardPower)
		{
			_heroCard = cardHero;
			_weaponCard = cardWeapon;
			_powerCard = cardPower;
		}

		public ReplaceHeroTask(string cardIdHero, string cardIdPower, bool removeSource = true)
		{
			_heroCard = Cards.FromId(cardIdHero);
			_powerCard = Cards.FromId(cardIdPower);
			_removeSource = removeSource;
		}

		public ReplaceHeroTask(string cardIdHero, string cardIdWeapon, string cardIdPower)
		{
			_heroCard = Cards.FromId(cardIdHero);
			_weaponCard = Cards.FromId(cardIdWeapon);
			_powerCard = Cards.FromId(cardIdPower);
		}

		public override TaskState Process(in Game game, in Controller controller, in IEntity source,
			in IPlayable target,
			in TaskStack stack = null)
		{
			var playable = source as IPlayable;
			if (playable == null || controller == null) return TaskState.STOP;

			// The source is the played Jaraxxus, which leaves play. A Deathrattle source (Majordomo Executus) stays where it is: dead in the
			// graveyard, or alive when its Deathrattle is triggered.
			if (_removeSource)
				playable.Controller.SetasideZone.Add(playable.Zone.Remove(playable));
			// Same inheritance as PlayCard.PlayHeroCore: a Neutral hero (Ragnaros) keeps the player's class for class-bound rules (Imbue).
			var inherit = _heroCard.Class == CardClass.NEUTRAL || _heroCard.Class == CardClass.INVALID
				? new EntityData { [GameTag.CLASS] = (int)controller.HeroClass } : null; // EntityData, as Controller.AddHero: a plain dictionary is copied and the ids would never reach it
			var oldHero = controller.Hero;
			controller.AddHeroAndPower(_heroCard, _powerCard, inherit);
			// The same steps as PlayCard.PlayHeroCore after the swap: the new hero power's trigger, the aura rebind, the hero's own trigger.
			HeroSwapRules.ActivateHeroPowerTrigger(controller.Hero);
			HeroSwapRules.RebindAuras(controller, oldHero, controller.Hero);
			if (_weaponCard != null)
				controller.Hero.AddWeapon(Entity.FromCard(in controller, in _weaponCard) as Weapon);
			return TaskState.COMPLETE;
		}
	}
}
