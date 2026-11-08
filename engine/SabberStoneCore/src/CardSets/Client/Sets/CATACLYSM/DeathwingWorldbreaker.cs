using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class DeathwingWorldbreaker
    {
        internal const int DbfId = 125467;
        private static readonly string[] Options = { "CATA_190t10", "CATA_190t11", "CATA_190t12", "CATA_190t13" };
        internal static OfflinePoolFilter Filter() => OfflinePoolFilter.Random(type: CardType.MINION, race: Race.DRAGON, rarity: Rarity.LEGENDARY);
        [OfflinePool("CATA_190h",DbfId)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("legendary-dragon",Filter(),new[] {
            "DEATHKNIGHT", "ROGUE", "SHAMAN", "WARLOCK", "WARRIOR", "DEMONHUNTER" }, destination: PoolDestination.Deck) };
        private static int Count(IPlayable source) => 1 << HeraldRules.Tier(source.Controller);
        [StandardCard("CATA_190h",DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,Count);
            var steps = new List<ISimpleTask> { new CustomTask((g,c,s,t,k) => { k.Number = 1 << HeraldRules.Tier(c); k.Number1 = 0; }) };
            for (int i=0;i<4;i++)
            {
                steps.Add(new DiscoverThenContinueTask((g,c,s,k) => (k.Number > 0 && g.State != State.COMPLETE ? Options : Array.Empty<string>()).Where((id,index) => (k.Number1 & (1 << index)) == 0).Select(Cards.FromId).ToArray()));
                steps.Add(new CustomTask((g,c,s,t,k) => {
                    var chosen = k.Playables.SingleOrDefault(); if (chosen == null) { k.Number = 0; return; }
                    int index = Array.IndexOf(Options,chosen.Card.Id); if (index < 0) return;
                    k.Number1 |= 1 << index; k.Number--;
                    switch (index)
                    {
                        case 0:
                            new SummonTask("CATA_190t14").Process(g,c,s,t,k); break;
                        case 1:
                            var enemies = c.Opponent.BoardZone.GetAll();
                            if (enemies.Length != 0)
                            {
                                int max = enemies.Max(m => m.Health);
                                var ties = enemies.Where(m => m.Health == max).ToArray();
                                g.OnRandomHappened(true); ties[g.Random.Next(ties.Length)].Destroy();
                            }
                            break;
                        case 2:
                            foreach (var enemy in c.Opponent.BoardZone.GetAll()) enemy.TakeDamage((IPlayable)s,4);
                            break;
                        case 3:
                            var ctx = new EffectContext(g,c.Id,s.Id);
                            for (int j=0;j<5;j++)
                            {
                                var pick = ctx.PickRandomCard(Filter(),recipeDbfId:DbfId); if (pick == null) break;
                                var dragon = Entity.FromCard(c,pick,new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                                CostRules.AddModifier((Playable)dragon,EffectOperator.SET,1);
                                Generic.ShuffleIntoDeck(c,s,dragon);
                            }
                            break;
                    }
                    g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
                }));
            }
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/CATA_190h/v1",steps.ToArray()) };
        }
        [StandardCard("CATA_190p",125466)]
        internal static void HeroPower(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new AddEnchantmentTask("CATA_190pe",EntityType.HERO) };
        }
        [StandardCard("CATA_190pe",125464)]
        internal static void Attack(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(5)) { IsOneTurnEffect = true } };
        }
        [StandardCard("CATA_190t10",127702)]
        [StandardCard("CATA_190t11",127699)]
        [StandardCard("CATA_190t12",127700)]
        [StandardCard("CATA_190t13",127701)]
        [StandardCard("CATA_190t14",127703)]
        internal static void Token(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
    }
}
