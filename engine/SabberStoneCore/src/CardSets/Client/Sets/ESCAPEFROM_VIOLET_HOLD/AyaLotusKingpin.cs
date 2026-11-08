using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AyaLotusKingpin
    {
        internal static readonly string[] Ingredients = { "CFM_621t2", "CFM_621t3", "CFM_621t4", "CFM_621t5",
            "CFM_621t6", "CFM_621t8", "CFM_621t9", "CFM_621t10", "CFM_621t37" };
        internal static OfflinePoolFilter Demons => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DEMON);
        [OfflinePool("JAIL_504", 125989)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("kabal-potion-demon", Demons, new[] { "ROGUE" }) };

        [StandardCard("JAIL_504", 125989)]
        internal static void Configure(Card card)
        {
            Mark(card);
            card.Entourage = new[] { "JAIL_504t", "JAIL_504t2", "JAIL_504t3", "JAIL_504t3p" };
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/JAIL_504/v1",
                new DiscoverThenContinueTask("JAIL_504t", "JAIL_504t2", "JAIL_504t3"),
                new CustomTask((g,c,s,t,stack) => {
                    var chosen = stack.Playables.Last().Card;
                    c[PayloadRules.CoinReplacement] = chosen.AssetId;
                    foreach (var held in c.HandZone.ToArray()) PayloadRules.ReplaceCoin(held);
                    for (int i = 0; i < 3; i++) Generic.DrawCard(c, chosen);
                })) };
        }

        internal static Controller FirstPlayer(Game game, Controller ordinary)
        {
            bool first = game.Player1.DeckCards.Any(c => c.Id == "JAIL_504");
            bool second = game.Player2.DeckCards.Any(c => c.Id == "JAIL_504");
            return first == second ? ordinary : first ? game.Player2 : game.Player1;
        }
        [StandardCard("JAIL_504t", 127152)]
        internal static void Jade(Card card)
        {
            Mark(card); card.Power = new Power { PowerTask = ComplexTask.Create(new TempManaTask(1), ComplexTask.SummonJadeGolem(SummonSide.DEFAULT)) };
        }
        [StandardCard("JAIL_504t2", 127153)]
        internal static void Grimy(Card card)
        {
            Mark(card); card.Power = new Power { PowerTask = ComplexTask.Create(new TempManaTask(1),
                new CustomTask((g,c,s,t,stack) => {
                    var enemies = c.Opponent.BoardZone.Where(m => !m.Untouchable && !m.IsDead).ToArray();
                    if (enemies.Length != 0) new DamageTask(2, EntityType.TARGET, true).Process(g,c,s,enemies[g.Random.Next(enemies.Length)]);
                })) };
        }
        [StandardCard("JAIL_504t3", 127154)]
        internal static void Kabal(Card card)
        {
            Mark(card); card.Entourage = new[] { "JAIL_504t3p" };
            card.Power = new Power { PowerTask = ComplexTask.Create(new TempManaTask(1), new CustomTask((g,c,s,t,stack) => {
                var parts = AssembleRules.Take(g, Ingredients.Select(Cards.FromId).ToList(), 2);
                Generic.DrawCard(c, GeneratedDefinitionRules.Potion(parts[0], parts[1]));
            })) };
        }
        [StandardCard("JAIL_504t3p", 131327)]
        internal static void Potion(Card card) { Mark(card); card.Entourage = Ingredients; }

        [StandardCard("CFM_621t2", 41590)]
        [StandardCard("CFM_621t3", 41589)]
        [StandardCard("CFM_621t4", 41588)]
        [StandardCard("CFM_621t5", 41587)]
        [StandardCard("CFM_621t6", 41586)]
        [StandardCard("CFM_621t8", 41584)]
        [StandardCard("CFM_621t9", 41583)]
        [StandardCard("CFM_621t10", 41582)]
        [StandardCard("CFM_621t37", 42068)]
        internal static void Ingredient(Card card)
        {
            Mark(card);
            switch (card.Id)
            {
                case "CFM_621t9":
                    card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                        var demon = OfflineCardPools.Pick(g, Demons, c.HeroClass, 125989);
                        if (demon != null) Generic.DrawCard(c, demon);
                    }) }; break;
                case "CFM_621t5":
                    card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                        var enemies = c.Opponent.BoardZone.Where(m => !m.Untouchable && !m.IsDead).ToArray();
                        if (enemies.Length != 0) g.Facts.Freeze(enemies[g.Random.Next(enemies.Length)],s);
                    }) }; break;
            }
        }
        [StandardCard("CFM_621_m4", 42071)]
        [StandardCard("CFM_621e", 42058)]
        internal static void Dependency(Card card) => Mark(card);
        private static void Mark(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
    }
}
