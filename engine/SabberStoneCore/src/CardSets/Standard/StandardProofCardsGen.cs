using System.Collections.Generic;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.src.Loader;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Standard
{
	public static class StandardProofCardsGen
	{
		public static void AddAll(Dictionary<string, CardDef> cards)
		{
			var aliases = new Dictionary<string, string>
			{
				{ "CORE_EX1_011", "EX1_011" }, { "CORE_CS2_189", "CS2_189" },
				{ "CORE_EX1_096", "EX1_096" }, { "CORE_GVG_085", "GVG_085" },
				{ "CORE_LOOT_413", "LOOT_413" }, { "CORE_EX1_007", "EX1_007" },
				{ "CORE_CS2_179", "CS2_179" }, { "Core_CS2_200", "CS2_200" },
				{ "CORE_CS2_222", "CS2_222" }, { "CORE_CS2_024", "CS2_024" },
				{ "CORE_CS2_023", "CS2_023" }, { "CORE_CS2_029", "CS2_029" },
				{ "CORE_CS2_028", "CS2_028" }, { "CORE_CS2_108", "CS2_108" },
				{ "CORE_EX1_391", "EX1_391" }, { "CORE_AT_064", "AT_064" },
				{ "CORE_EX1_606", "EX1_606" }, { "CORE_EX1_604", "EX1_604" },
				{ "HERO_08bp", "CS2_034" }, { "HERO_01bp", "CS2_102" }
			};
			foreach (KeyValuePair<string, string> alias in aliases)
				cards.Add(alias.Key, cards[alias.Value]);

			cards.Add("CORE_SW_066", new CardDef(
				new Dictionary<PlayReq, int>
				{
					{ PlayReq.REQ_TARGET_IF_AVAILABLE, 0 },
					{ PlayReq.REQ_MINION_TARGET, 0 }
				},
				new Power { PowerTask = new SilenceTask(EntityType.TARGET) }));
			cards["CORE_CS2_032"] = new CardDef(new Power
			{
				PowerTask = new DamageTask(5, EntityType.OP_MINIONS, true)
			});
			cards.Add("TIME_045", new CardDef());
			cards.Add("TLC_101", new CardDef(new Power
			{
				Aura = new EnrageEffect(AuraType.SELF, "TLC_101e")
			}));
			cards.Add("TLC_101e", new CardDef(new Power
			{
				Enchant = Enchants.Enchants.GetAutoEnchantFromText("TLC_101e")
			}));
			ISimpleTask kindredCopy = SabberStoneCore.Actions.KindredRules.Clause(
				new SummonCopyTask(EntityType.SOURCE, side: SummonSide.RIGHT));
			cards.Add("TLC_226", new CardDef(new Power
			{
				PowerTask = kindredCopy,
				DeathrattleTask = ComplexTask.DrawFromDeck(1, SelfCondition.IsSpell)
			}));
			cards.Add("DINO_435", new CardDef(new Power { PowerTask = kindredCopy }));
		}
	}
}
