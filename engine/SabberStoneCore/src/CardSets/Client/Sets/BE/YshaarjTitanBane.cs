using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Costs;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class YshaarjTitanBane
    {
        internal const int DbfId = 131851;
        internal static OfflinePoolFilter Filter() => OfflinePoolFilter.Random(type:CardType.MINION,minCost:8);
        [OfflinePool("BE_784",DbfId)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("8-plus-minion",Filter(),new[] { "NEUTRAL" }, destination: PoolDestination.Summon) };
        private static int Paid(IPlayable s) {
            var q = s.Game.HistoryFacts.Query;
            var plays = q.Plays().Where(f => f.ControllerId == s.Controller.Id && f.CardType == CardType.MINION).Select(f => f.PlayId).ToHashSet();
            return q.Payments().Where(f => f.ControllerId == s.Controller.Id && plays.Contains(f.RelatedPlayId)).Sum(f => f.Mana);
        }
        private static bool Ready(IPlayable s) {
            var quote = CostRules.Quote(s.Controller,s);
            return Paid(s) + (s.Zone == s.Controller.HandZone && quote.Kind == CostPaymentKind.Mana ? quote.Amount : 0) >= 30;
        }
        private static int Remaining(IPlayable s) => Math.Max(0,30-Paid(s));
        [StandardCard("BE_784",DbfId)]
        internal static void Configure(Card card) {
            _ = Abilities;
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.PoweredUpWhen(Ready);
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1,Remaining);
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (Paid((IPlayable)s) >= 30) Generic.AddEnchantmentBlock(g,Cards.FromId("BE_784e"),(IPlayable)s,s);
            }) };
        }
        internal static readonly SuspendableTask Abilities = OldGodRules.Recipe("engine/old-god/yshaarj/v1",
            OldGodRules.Branch("engine/old-god/yshaarj/gaze/v1",0,OldGodRules.Power((g,c,s,t,stack) => MythicalTerror.ForceAll().Process(g,c,s,t,stack))),
            OldGodRules.Branch("engine/old-god/yshaarj/might/v1",1,
                OldGodRules.Power((g,c,s,t,stack) => {
                    var body = (Minion)s;
                    var adjacent = body.GetAdjacentMinions().Where(m => !m.IsDead).ToArray();
                    Generic.AddEnchantmentBlock(g,Cards.FromId("BE_784t2e"),g.IdEntityDic[stack.Number2],body,
                        adjacent.Sum(m => m.AttackDamage),adjacent.Sum(m => m.Health));
                })),
            OldGodRules.Branch("engine/old-god/yshaarj/spawn/v1",2,
                OldGodRules.Power((g,c,s,t,stack) => {
                    var ctx = new EffectContext(g,c.Id,stack.Number2);
                    var pick = ctx.PickRandomCard(Filter(),recipeDbfId:DbfId);
                    if (pick == null) return;
                    ctx.Summon(pick.Id); ctx.AddToHand(pick.Id);
                })),
            OldGodRules.Branch("engine/old-god/yshaarj/domain/v1",3,
                OldGodRules.Power((g,c,s,t,stack) => Generic.AddEnchantmentBlock(g,Cards.FromId("BE_784t4e"),g.IdEntityDic[stack.Number2],c))));
        [StandardCard("BE_784t1",131852)]
        internal static void Token0(Card card) => Token(card);
        [StandardCard("BE_784t3",131853)]
        internal static void Token1(Card card) => Token(card);
        [StandardCard("BE_784t2",131854)]
        internal static void Token2(Card card) => Token(card);
        [StandardCard("BE_784t4",131855)]
        internal static void Token3(Card card) => Token(card);
        private static void Token(Card card) { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
        [StandardCard("BE_784t2e",131856)]
        internal static void Might(Card card) {
            Token(card); card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0),Effects.Health_N(0)) { UseScriptTag = true } };
        }
        [StandardCard("BE_784t4e",132131)]
        internal static void Domain(Card card) {
            Token(card); card.Power = new Power { Aura = new Aura(AuraType.HAND,Effects.ReduceCost(5)),
                Trigger = new Trigger(TriggerType.PLAY_CARD) { TriggerSource = TriggerSource.FRIENDLY,
                    SingleTask = RemoveEnchantmentTask.Task } };
        }
    }
}
