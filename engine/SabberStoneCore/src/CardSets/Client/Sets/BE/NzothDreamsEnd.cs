using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class NzothDreamsEnd
    {
        internal const int DbfId = 128389;
        internal static OfflinePoolFilter Filter() => OfflinePoolFilter.Discover(type:CardType.MINION,minCost:5,keyword:GameTag.DEATHRATTLE);
        [OfflinePool("BE_017",DbfId)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("5-plus-deathrattle",Filter(),new[] { "NEUTRAL" }, destination: PoolDestination.Summon) };
        [StandardCard("BE_017",DbfId)]
        internal static void Configure(Card card) {
            _ = Abilities;
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power { Aura = new AdaptiveCostEffect(s => OldGodRules.DeathrattleSummons(s.Controller)) };
        }
        internal static readonly SuspendableTask Abilities = OldGodRules.Recipe("engine/old-god/nzoth/v1",
            OldGodRules.Branch("engine/old-god/nzoth/call/v1",0,
                new DiscoverThenContinueTask(Filter(),recipeDbfId:DbfId),
                OldGodRules.Power((g,c,s,t,stack) => {
                    var chosen = stack.Playables.LastOrDefault();
                    if (chosen == null) return;
                    c.SetasideZone.Remove(chosen);
                    if (!c.BoardZone.IsFull) Generic.SummonBlock(g,(Minion)chosen,-1,g.IdEntityDic[stack.Number2]);
                    else chosen[GameTag.ZONE] = (int)Zone.REMOVEDFROMGAME;
                })),
            OldGodRules.Branch("engine/old-god/nzoth/army/v1",1,
                OldGodRules.Power((g,c,s,t,stack) => {
                    var dead = g.HistoryFacts.Query.Deaths().Where(f => f.ControllerId == c.Id && f.Definition.ReadDefinition().Deathrattle)
                        .GroupBy(f => f.DbfId).Select(group => group.First().Definition.Materialize()).OrderBy(card => card.AssetId).ToList();
                    var ctx = new EffectContext(g,c.Id,stack.Number2);
                    for (int i = 0; i < 4 && dead.Count > 0 && !c.BoardZone.IsFull; i++) {
                        int pick = g.Random.Next(dead.Count); var card = dead[pick]; dead.RemoveAt(pick); ctx.Summon(card.Id);
                        g.DeathProcessingAndAuraUpdate();
                    }
                })),
            OldGodRules.Branch("engine/old-god/nzoth/domain/v1",2,
                OtherTarget(),OldGodRules.Power((g,c,s,t,stack) => {
                    if (stack.Playables.LastOrDefault() is Minion target)
                        Generic.AddEnchantmentBlock(g,Cards.FromId("BE_017t6e"),g.IdEntityDic[stack.Number2],target);
                },targeted:true)),
            OldGodRules.Branch("engine/old-god/nzoth/mastery/v1",3,
                OtherTarget(true),OldGodRules.Power((g,c,s,t,stack) => {
                    var target = stack.Playables.LastOrDefault();
                    for (int i = 0; i < 2 && target?.Zone == c.BoardZone && !((Minion)target).IsDead; i++) {
                        new ActivateDeathrattleTask(EntityType.TARGET).Process(g,c,g.IdEntityDic[stack.Number2],target);
                        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
                    }
                },targeted:true)));
        private static SelectEntityThenContinueTask OtherTarget(bool deathrattle = false) => new SelectEntityThenContinueTask(
            new SelectorSpec(deathrattle ? SelectorSource.DeathrattleBoard : SelectorSource.Board,SelectorSide.Friendly,cardType:CardType.MINION,excludePlayTarget:true));
        [StandardCard("BE_017t3",128398)]
        internal static void Token0(Card card) => Token(card);
        [StandardCard("BE_017t5",129205)]
        internal static void Token1(Card card) => Token(card);
        [StandardCard("BE_017t6",129210)]
        internal static void Token2(Card card) => Token(card);
        [StandardCard("BE_017t4",129215)]
        internal static void Token3(Card card) => Token(card);
        private static void Token(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
        [StandardCard("BE_017t6e",135608)]
        internal static void Domain(Card card) {
            Token(card);
            card.Power = new Power { Enchant = new Enchant(GameTag.DEATHRATTLE,EffectOperator.SET,1),
                DeathrattleTask = new DamageTask(2,EntityType.OP_MINIONS) };
        }
    }
}
