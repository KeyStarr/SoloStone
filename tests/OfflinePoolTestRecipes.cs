using System;
using EngineCard = SabberStoneCore.Model.Card;
using EngineGame = SabberStoneCore.Model.Game;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

// Only linked by proof projects. Never registered in StandardSets or EffectModules.
internal static class OfflinePoolTestRecipes
{
    internal static readonly string[] PilotIds = "CATA_484 CATA_723 BE_854 JAIL_125 BE_220 EDR_530 TIME_040 JAIL_328 EDR_848".Split(' ');
    // Small-pool fixtures use the three five-cost Past members, independent of later admissions.
    internal static readonly OfflinePoolFilter Past = OfflinePoolDeclarations.PastFiveCostMinion();
    internal static readonly OfflinePoolFilter Empty = OfflinePoolFilter.Discover(minCost: 999);
    internal static readonly DecisionBattlecryTask Dynamic = new("proof/offline-pool/dynamic/v1",
        new Cost(5), new DiscoverThenContinueTask(Past, PoolCostBinding.StackNumber), new Collect(),
        new Cost(1), new DiscoverThenContinueTask(OfflinePoolFilter.Random(type: CardType.SPELL), PoolCostBinding.StackNumber),
        new Collect());
    internal static readonly DecisionBattlecryTask NoOffers = new("proof/offline-pool/empty/v1",
        new Cost(999), new DiscoverThenContinueTask(Past, PoolCostBinding.StackNumber),
        new Collect(), new ArmorTask(7));
    internal static readonly DecisionBattlecryTask Small = new("proof/offline-pool/small/v1",
        new DiscoverThenContinueTask(Past), new Collect());

    // Synthetic recipe declarations stay in test fixtures; restore all real declarations after each scope.
    internal static void WithDeclarations(Action proof)
    {
        var slots = (IList<OfflinePoolDeclaration>)typeof(ReadOnlyCollection<OfflinePoolDeclaration>)
            .GetProperty("Items", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(OfflinePoolDeclarations.Cards);
        var original = slots.ToArray();
        var filters = new[] { Past, Empty, OfflinePoolFilter.Random(scope: PoolScope.Past, race: Race.PIRATE),
            OfflinePoolFilter.Discover(), OfflinePoolFilter.Random() };
        var pools = filters.Select((f, i) => new PoolRequirement("proof-" + i, f, new[] { "NEUTRAL" })).Concat(new[] {
            new PoolRequirement("dynamic-past", Past, new[] { "NEUTRAL" }, PoolCostBinding.StackNumber, 5),
            new PoolRequirement("dynamic-spell", OfflinePoolFilter.Random(type: CardType.SPELL), new[] { "NEUTRAL" }, PoolCostBinding.StackNumber, 1)
        }).ToArray();
        try
        {
            int i = 0;
            foreach (string id in new[] { "HERO_08", "HERO_01", "EX1_319", "CS2_231", "EX1_028", "GVG_070" })
                slots[i++] = new OfflinePoolDeclaration(id, Cards.FromId(id).AssetId, pools);
            proof();
        }
        finally { for (int i = 0; i < original.Length; i++) slots[i] = original[i]; }
    }

    internal static void WithSource(ISimpleTask recipe, Action<EngineCard> proof)
    {
        var card = Cards.FromId("EX1_319");
        var original = card.Power;
        try { card.Power = new Power { PowerTask = recipe }; proof(card); }
        finally { card.Power = original; }
    }

    internal static EngineGame NewGame(bool opponent = false, bool legacy = false, CardClass hero = CardClass.MAGE)
    {
        var game = new EngineGame(new GameConfig { RandomSeed = 71, StartPlayer = opponent ? 2 : 1,
            SkipMulligan = true, History = true, Logging = false, LegacyPoolReplay = legacy,
            Player1HeroClass = hero, Player2HeroClass = CardClass.WARRIOR, EnableCorpses = hero == CardClass.DEATHKNIGHT,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); game.CurrentPlayer.BaseMana = 10;
        return game;
    }

    private sealed class Cost(int value) : SimpleTask
    {
        public override TaskState Process(in EngineGame game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        { stack.Number = value; return TaskState.COMPLETE; }
    }
    private sealed class Collect : SimpleTask
    {
        public override TaskState Process(in EngineGame game, in Controller controller, in IEntity source,
            in IPlayable target, in TaskStack stack = null)
        {
            var chosen = stack.Playables.SingleOrDefault();
            if (chosen == null) return TaskState.COMPLETE;
            Generic.RemoveFromZone(controller, chosen);
            Generic.AddHandPhase(controller, chosen);
            stack.Playables.Clear();
            controller.Hero.Armor++;
            return TaskState.COMPLETE;
        }
    }
}
