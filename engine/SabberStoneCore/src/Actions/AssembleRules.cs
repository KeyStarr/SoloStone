using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Enchants;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    public static class AssembleRules
    {
        // Pinned CardTextBuilder and TextUtils.ParseTextForCraftableSplit consume these dbfIds.
        public const GameTag FirstHalf = (GameTag)4572;
        public const GameTag SecondHalf = (GameTag)4573;
        public static OfflinePoolFilter Pool => OfflinePoolFilter.Random(type: CardType.MINION);

        private static readonly IReadOnlyDictionary<string, (string Token, int Count,
            Action<Game, Controller, IEntity, IPlayable, TaskStack> Run)> SummonPayloads =
            new Dictionary<string, (string, int, Action<Game, Controller, IEntity, IPlayable, TaskStack>)>(StringComparer.Ordinal) {
                ["CORE_EX1_110"] = ("EX1_110t", 1, SummonBaine),
                ["CORE_AV_337"] = ("AV_337t", 2, SummonCubs),
                ["TLC_225"] = ("TLC_249", 1, SummonCinder),
                ["TLC_237"] = ("TLC_237t", 4, SummonHatchlings),
                ["EDR_492"] = ("EDR_492t", 3, SummonDucklings),
                ["JAIL_440"] = ("HERO_11bpt", 2, SummonGhouls),
                ["CATA_476"] = ("CATA_476t", 1, SummonBronzeDragon),
            };

        internal static IEnumerable<(string Key, Action<Game, Controller, IEntity, IPlayable, TaskStack> Run)> SummonRecipes =>
            SummonPayloads.Select(p => ("assemble/summon-payload/" + p.Key + "/v1", p.Value.Run));

        internal static Action<Game, Controller, IEntity, IPlayable, TaskStack> SummonCallback(Card card) =>
            SummonPayloads.TryGetValue(card.Id, out var recipe) ? recipe.Run : null;

        private static void SummonBaine(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack) => ExecuteSummonPayload("CORE_EX1_110", g, c, s);
        private static void SummonCubs(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack) => ExecuteSummonPayload("CORE_AV_337", g, c, s);
        private static void SummonCinder(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack) => ExecuteSummonPayload("TLC_225", g, c, s);
        private static void SummonHatchlings(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack) => ExecuteSummonPayload("TLC_237", g, c, s);
        private static void SummonDucklings(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack) => ExecuteSummonPayload("EDR_492", g, c, s);
        private static void SummonGhouls(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack) => ExecuteSummonPayload("JAIL_440", g, c, s);
        private static void SummonBronzeDragon(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack) => ExecuteSummonPayload("CATA_476", g, c, s);

        private static void ExecuteSummonPayload(string payloadId, Game game, Controller controller, IEntity source)
        {
            var recipe = SummonPayloads[payloadId];
            // Core Cairne carries a pinned 5/5 Baine definition, separate from the legacy token.
            var token = payloadId == "CORE_EX1_110" ? ((SummonTask)Cards.FromId(payloadId).Power.DeathrattleTask).Card : Cards.FromId(recipe.Token);
            int position = EffectContext.DeathrattlePosition(controller, source);
            for (int i = 0; i < recipe.Count && !controller.BoardZone.IsFull; i++)
                new SummonTask(SummonSide.NUMBER, token).Process(game, controller, source, null,
                    new TaskStack { Number = position + i + 1 });
        }

        public static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            string recipe = card.Id;
            int dbfId = card.AssetId;
            var steps = new ISimpleTask[] {
                new CustomTask((g, c, s, t, stack) => {
                    stack.Number = stack.Number1 = 0;
                    if (recipe == "BE_087")
                        for (int i = 0; i < 2; i++) new SummonTask("BE_087t").Process(g, c, s, t, stack);
                }),
                new DiscoverThenContinueTask((g, c, s, stack) => DonorOffers(g, c, s, recipe, dbfId, stack)),
                new DiscoverThenContinueTask(HalfOffers),
                new CustomTask((g, c, s, t, stack) => Finish(g, c, s, stack, recipe))
            };
            var task = card.Type == CardType.SPELL
                ? (ISimpleTask)new DecisionSpellTask("assemble/" + recipe + "/v1", steps)
                : new DecisionBattlecryTask("assemble/" + recipe + "/v1", steps);
            // The decision-play boundary owns suspension; Combo eligibility is checked before offers.
            card.Power = new Power { PowerTask = task };
        }

        public static bool PrimaryMatches(Card card, string recipe, CardClass heroClass)
        {
            if (!AssembleCatalog.Supports(card)) return false;
            if (recipe == "BE_004") return CardSets.Client.Sets.Rajh.Supports(card);
            bool own = card.Class == CardClass.NEUTRAL || CardClassRules.IsAllowedInDeck(card, heroClass);
            if (recipe == "BE_702")
                return card.Class != CardClass.NEUTRAL && !CardClassRules.IsAllowedInDeck(card, heroClass);
            if (recipe == "BE_257") return card.Class != CardClass.NEUTRAL && CardClassRules.IsAllowedInDeck(card, CardClass.MAGE);
            if (!own) return false;
            switch (recipe)
            {
                case "BE_044": return card.Cost == 4;
                case "BE_262": return card.Cost == 1;
                case "BE_045": return card.Cost <= 3;
                case "BE_229": return AssembleCatalog.HasOwnerEnd(card);
                case "BE_553": return card.IsRace(Race.MECHANICAL);
                case "BE_571": return AssembleCatalog.HasDeathrattle(card);
                case "BE_740": return card.Taunt;
                case "BE_851": return card.IsRace(Race.BEAST);
                default: return true;
            }
        }

        public static bool DonorMatches(Card body, Card donor, string recipe, CardClass heroClass)
        {
            if (recipe == "BE_004") return body.Id != donor.Id && CardSets.Client.Sets.Rajh.Supports(donor)
                && (Changes(body, donor, true) || Changes(body, donor, false));
            if (!AssembleCatalog.Supports(donor) || body.Id == donor.Id
                || donor.Class != CardClass.NEUTRAL && !CardClassRules.IsAllowedInDeck(donor, heroClass)) return false;
            bool cost = recipe == "BE_045" ? donor.Cost >= 5 : body.Cost >= 9
                ? donor.Cost >= 8 && donor.Cost <= 10 : donor.Cost >= body.Cost && donor.Cost <= body.Cost + 2;
            return cost && (Changes(body, donor, true) || Changes(body, donor, false));
        }

        private static bool Changes(Card body, Card donor, bool top) =>
            AssembleCatalog.Normalize(AssembleCatalog.Half(body, top)) != AssembleCatalog.Normalize(AssembleCatalog.Half(donor, top));

        public static Card[] Candidates(Game game, Controller controller, string recipe, int sourceDbfId)
        {
            var pool = recipe == "BE_004" ? CardSets.Client.Sets.Rajh.Pool : Pool;
            if (!OfflinePoolDeclarations.Cards.Any(c => c.DbfId == sourceDbfId && c.Pools.Any(p => p.Filter == pool)))
                throw new InvalidOperationException("Assemble requires its declared offline pool.");
            return OfflineCardPools.Candidates(game, controller.HeroClass, pool, sourceDbfId, PoolDestination.Other)
                .Where(AssembleCatalog.Supports).ToArray();
        }

        private static Card[] DonorOffers(Game game, Controller controller, IEntity source, string recipe, int dbfId, TaskStack stack)
        {
            if (recipe == "BE_702" && !controller.IsComboActive) return Array.Empty<Card>();
            var candidates = Candidates(game, controller, recipe, dbfId);
            var primaries = candidates.Where(c => PrimaryMatches(c, recipe, controller.HeroClass)
                && candidates.Any(d => DonorMatches(c, d, recipe, controller.HeroClass))).ToArray();
            if (primaries.Length == 0) return Array.Empty<Card>();
            var body = primaries[game.Random.Next(primaries.Length)];
            var generated = Entity.FromCard(controller, body, new EntityData {
                [GameTag.CREATOR] = source.Id, [GameTag.DISPLAYED_CREATOR] = source.Id,
                [Generic.PrivateDecisionOfferTag] = controller.PlayerId
            }, controller.SetasideZone);
            // Save an entity reference, never a hidden definition's dbfId in the decision frame.
            stack.Number = generated.Id;
            // The core assumption grants the body first; these newer card-specific records grant the finished card.
            if (recipe != "BE_004" && recipe != "BE_851" && recipe != "BE_553" && recipe != "BE_702")
            {
                stack.Number1 = generated.Id;
                Generic.AddHandPhase(controller, controller.SetasideZone.Remove(generated));
            }
            var donors = candidates.Where(d => DonorMatches(body, d, recipe, controller.HeroClass)).ToList();
            return Take(game, donors, 2);
        }

        internal static Card[] Take(Game game, List<Card> candidates, int count)
        {
            var result = new List<Card>();
            while (candidates.Count != 0 && result.Count < count)
            {
                int index = game.Random.Next(candidates.Count);
                result.Add(candidates[index]); candidates.RemoveAt(index);
            }
            return result.ToArray();
        }

        private static Card[] HalfOffers(Game game, Controller controller, IEntity source, TaskStack stack)
        {
            var donor = stack.Playables?.LastOrDefault()?.Card;
            if (stack.Number <= 0 || donor == null) return Array.Empty<Card>();
            var body = game.IdEntityDic[stack.Number].Card;
            var result = new List<Card>();
            if (Changes(body, donor, true)) result.Add(Card.CreateAssembledCard(body, donor, body));
            if (Changes(body, donor, false)) result.Add(Card.CreateAssembledCard(body, body, donor));
            return result.ToArray();
        }

        private static void Finish(Game game, Controller controller, IEntity source, TaskStack stack, string recipe)
        {
            var definition = stack.Playables?.LastOrDefault()?.Card;
            if (recipe == "BE_004") {
                if (definition != null && definition[FirstHalf] != definition[SecondHalf])
                    BasicHeroPowerRules.InstallPassive(controller, source, definition);
                return;
            }
            IPlayable result = null;
            if (definition != null && definition[FirstHalf] != definition[SecondHalf])
            {
                using var handEntry = controller.HandZone.DeferCardStateEntry();
                if (stack.Number1 != 0)
                {
                    result = game.IdEntityDic[stack.Number1];
                    if (result.Card.Id != definition.Id) result = null;
                    else Generic.ChangeEntityBlock(controller, result, definition, false);
                }
                else
                {
                    result = Entity.FromCard(controller, definition, new EntityData {
                        [GameTag.CREATOR] = source.Id, [GameTag.DISPLAYED_CREATOR] = source.Id
                    });
                    Generic.AddHandPhase(controller, result);
                }
                if (result?.Zone == controller.HandZone)
                {
                    if (recipe == "BE_114") CostRules.AddModifier((Playable)result, EffectOperator.SUB, 2);
                    if (recipe == "BE_740") Generic.AddEnchantmentBlock(game, Cards.FromId("BE_740e"), (IPlayable)source, result);
                    if (recipe == "BE_257") Generic.AddEnchantmentBlock(game, Cards.FromId("BE_257e"), (IPlayable)source, result);
                }
            }
            if (recipe == "BE_553") new AddCardTo("BE_553t", EntityType.HAND).Process(game, controller, source, null, stack);
            if (recipe == "BE_571")
                foreach (var held in controller.HandZone.OfType<Minion>().ToArray())
                    Generic.AddEnchantmentBlock(game, Cards.FromId("BE_571e"), (IPlayable)source, held);
        }
    }
}
