using System;
using System.Collections.Generic;
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
				foreach (var minion in c.BoardZone.ToArray())
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

		/// <summary>The minion list for a Cost: Cards.cs regroups the frozen buckets for identity-row cost changes (#141), and this still drops the
		/// frozen quirks (a card whose own Cost or type differs from its bucket), so only minions that cost exactly that now are drawn. Not redundant.</summary>
		internal static List<Card> MinionsCosting(FormatType format, int cost) =>
			Cards.CostMinionCards(format).TryGetValue(cost, out List<Card> pool)
				? pool.Where(card => card.Cost == cost && card.Type == CardType.MINION && card.Collectible).ToList()
				: new List<Card>();

		/// <summary>Summon one random minion of this Cost where the Deathrattle owner stood; nothing when no minion costs that much.</summary>
		public static ISimpleTask SummonRandomMinionCosting(int cost) =>
			ComplexTask.Create(
				new CustomTask((g, c, s, t, stack) =>
				{
					stack.Playables = Array.Empty<IPlayable>();
					var pool = MinionsCosting(g.FormatType, cost);
					if (pool.Count == 0) return;
					var pick = pool[g.Random.Next(pool.Count)];
					g.OnRandomHappened(true);
					stack.Playables = new[] { Entity.FromCard(c, pick) };
				}),
				new SummonTask(SummonSide.DEATHRATTLE));

		/// <summary>Recombobulator: transform the chosen friendly minion into a random minion of the same (current) Cost. An empty cost pool changes nothing.</summary>
		public static ISimpleTask Recombobulator =>
			new CustomTask((g, c, s, t, stack) =>
			{
				if (!(t is Minion target) || target.Zone?.Type != Zone.PLAY) return;
				var pool = MinionsCosting(g.FormatType, target.Cost);
				if (pool.Count == 0) return;
				var pick = pool[g.Random.Next(pool.Count)];
				g.OnRandomHappened(true);
				Generic.ChangeEntityBlock(c, target, pick, true);
			});

		// Fire spells of the 37.0 export whose engine definition predates spell schools (the same identities as Volcanic Thrasher).
		private static readonly HashSet<int> LegacyFireSpells = new HashSet<int> {
			286, 315, 394, 585, 864, 950, 974, 1004, 1087, 1640, 1927, 2284,
			2289, 2323, 2330, 2359, 2432, 2480, 2539, 2541, 2678, 2864, 2874, 2997,
			25271, 25396, 25401, 38413, 38935, 39715, 39747, 39854, 40005, 40066, 40297, 40316,
			40317, 40498, 40500, 40545, 40546, 40938, 41151, 41159, 41690, 41878, 43407, 43411,
			43412, 43414, 45271, 46286, 46425, 46437, 46715, 46953, 47251, 47414, 48393, 48824,
			49333, 50086, 50120, 50122, 51161, 51371, 51530, 52703, 53823, 54429, 55101, 55250,
			55276, 55858, 55859, 55860, 55861, 55935, 56170, 56173, 57316, 57483, 57484, 58970,
			69501, 69502,
		};

		/// <summary>True when the spell cost exactly <paramref name="cost"/> Mana as it was played: the paid amount retained by the play fact, the current Cost without one.</summary>
		public static SelfCondition PlayedSpellCosts(int cost) =>
			new SelfCondition(me =>
			{
				var play = me.Game.HistoryFacts.Query.Plays().LastOrDefault(f => f.EntityId == me.Id);
				return (play != null ? play.PaidCost : me.Cost) == cost;
			});

		/// <summary>Soot Spewer: with another friendly Mech, add a random collectible Fire spell to the hand.</summary>
		public static ISimpleTask SootSpewer =>
			ComplexTask.Create(
				new CustomTask((g, c, s, t, stack) =>
				{
					stack.Playables = Array.Empty<IPlayable>();
					if (!c.BoardZone.Any(m => m != s && m.Card.IsRace(Race.MECHANICAL))) return;
					var pool = Cards.FormatTypeCards(g.FormatType).Where(card => card.Type == CardType.SPELL && card.Collectible && card.Implemented
						&& (card[GameTag.SPELL_SCHOOL] == 2 || LegacyFireSpells.Contains(card.AssetId))).ToArray();
					if (pool.Length == 0) return;
					var pick = pool[g.Random.Next(pool.Length)];
					g.OnRandomHappened(true);
					stack.Playables = new[] { Entity.FromCard(c, pick) };
				}),
				new AddStackTo(EntityType.HAND));
	}
}
