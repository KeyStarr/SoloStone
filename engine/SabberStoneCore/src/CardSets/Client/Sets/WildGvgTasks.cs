using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
	/// <summary>Goblins vs Gnomes effects (Wild wave Y0b, lane m2e) that the stock tasks cannot express. Kept outside the Tasks namespaces: the snapshot schema numbers every type there (SnapshotSchema), so a new type would renumber historical definition bytes.</summary>
	internal static class WildGvgTasks
	{
		/// <summary>Enhance-o Mechano: every other friendly minion gets one of Windfury, Taunt or Divine Shield, each rolled on its own.</summary>
		public static ISimpleTask EnhanceOMechano =>
			new CustomTask((g, c, s, t, stack) =>
			{
				foreach (var minion in c.BoardZone.Interactable.ToArray())
				{
					if (minion == s || minion.Zone?.Type != Zone.PLAY) continue;
					switch (g.Random.Next(3))
					{
						case 0: minion.HasWindfury = true; break;
						case 1: minion.HasTaunt = true; break;
						default: minion.HasDivineShield = true; break;
					}
					g.OnRandomHappened(true);
				}
			});

		/// <summary>Transform a friendly minion using its current Cost and the match pool.</summary>
		public static ISimpleTask Recombobulator =>
			new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion target) || target.Zone?.Type != Zone.PLAY) return;
				var pool = Cards.RandomPool(g, PoolDestination.Transform, s);
				if (!pool.TryMinionsOfCost(target.Cost, out var bucket)) return;
				var eligible = pool.WithoutSource(bucket, s);
				if (eligible.Count == 0) return;
				Generic.ChangeEntityBlock(c, target, eligible[g.Random.Next(eligible.Count)], true);
				g.OnRandomHappened(true);
			});

		/// <summary>True when the spell cost exactly <paramref name="cost"/> Mana as it was played: the paid amount retained by the play fact, the current Cost without one.</summary>
		public static SelfCondition PlayedSpellCosts(int cost) =>
			new SelfCondition(me =>
			{
				var play = me.Game.HistoryFacts.Query.Plays().LastOrDefault(f => f.EntityId == me.Id);
				return (play != null ? play.PaidCost : me.Cost) == cost;
			});
	}
}
