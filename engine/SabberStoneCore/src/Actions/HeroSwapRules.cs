using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Model.Entities;

namespace SabberStoneCore.Actions
{
	/// <summary>What a hero replacement does after the new hero and its hero power are in place. One helper for PlayCard.PlayHeroCore (a hero
	/// card played from hand) and ReplaceHeroTask (Lord Jaraxxus, Majordomo Executus), so the two paths cannot drift.</summary>
	internal static class HeroSwapRules
	{
		/// <summary>The new hero power's own trigger.</summary>
		internal static void ActivateHeroPowerTrigger(Hero hero) => hero.HeroPower.Power?.Trigger?.Activate(hero.HeroPower);

		/// <summary>Moves the controller's hero-targeted auras to the new hero (AdaptiveEffect.RebindHero, HERO auras), then activates the
		/// new hero's own trigger.</summary>
		internal static void RebindAuras(Controller c, Hero oldHero, Hero hero)
		{
			void Rebind(IAura aura)
			{
				if (aura is MultiAura multiple)
					foreach (var child in multiple) Rebind(child);
				if (aura is AdaptiveEffect adaptive) adaptive.RebindHero(oldHero, hero);
				if (aura is Aura fixedAura && fixedAura.Type == AuraType.HERO && fixedAura.Owner.Controller == c)
				{
					fixedAura.DeApply(oldHero);
					fixedAura.EntityAdded(hero);
				}
			}
			foreach (var aura in c.Game.Auras.ToArray()) Rebind(aura);
			hero.Power?.Trigger?.Activate(hero);
		}
	}
}
