using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GodfatherKazakus
    {
        private static readonly string[] Components = Enumerable.Range(1,9).Select(i => "CAP_405t"+i).ToArray();
        private static OfflinePoolFilter Minions() => OfflinePoolFilter.Random(type:CardType.MINION,minCost:3,maxCost:3);
        [OfflinePool("CAP_405", 127067)]
        internal static PoolRequirement[] Pools() => new[] { new PoolRequirement("trial-three-cost-minions",Minions(),new[] { "WARLOCK" }, destination: PoolDestination.Summon) };
        [StandardCard("CAP_405",127067)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new DecisionBattlecryTask("client/CAP_405/v1",
                new DiscoverThenContinueTask(FirstOffers),
                new CustomTask((g,c,s,t,stack) => stack.Number1=stack.Playables.Last().Card.AssetId),
                new DiscoverThenContinueTask(SecondOffers),
                new CustomTask((g,c,s,t,stack) => stack.Number2=stack.Playables.Last().Card.AssetId),
                new DiscoverThenContinueTask("CAP_405tb1b","CAP_405tb2b","CAP_405tb3b"),
                new CustomTask(Generate)) };
        }
        private static Card[] FirstOffers(Game g,Controller c,IEntity s,TaskStack stack) => Offers(g,0);
        private static Card[] SecondOffers(Game g,Controller c,IEntity s,TaskStack stack) => Offers(g,stack.Number1);
        private static Card[] Offers(Game g,int excluded)
        {
            var candidates=Components.Select(Cards.FromId).Where(c=>c.AssetId!=excluded).ToList();
            var offers=new Card[3];
            for(int i=0;i<3;i++) { int pick=g.Random.Next(candidates.Count);offers[i]=candidates[pick];candidates.RemoveAt(pick); }
            return offers;
        }
        private static void Generate(Game g,Controller c,IEntity s,IPlayable t,TaskStack stack)
        {
            string id=stack.Playables.Last().Card.Id;
            var trial=Entity.FromCard(c,Cards.FromId(id.Substring(0,id.Length-1)),new EntityData {
                [GameTag.ZONE]=(int)Zone.HAND,[GameTag.CREATOR]=s.Id,[GameTag.DISPLAYED_CREATOR]=s.Id,
                [GameTag.MODULAR_ENTITY_PART_1]=stack.Number1,[GameTag.MODULAR_ENTITY_PART_2]=stack.Number2
            });
            Generic.AddHandPhase(c,trial);
        }
        private static readonly ISimpleTask Payload = new CustomTask((g,c,s,t,stack) => {
            foreach(var tag in new[] { GameTag.MODULAR_ENTITY_PART_1,GameTag.MODULAR_ENTITY_PART_2 })
            {
                if(g.State==State.COMPLETE) break;
                var component=Cards.FromAssetId(s[tag]);
                if(!Components.Contains(component.Id)) throw new InvalidOperationException("Trial has no valid saved component.");
                component.Power.PowerTask.Process(g,c,s,null,new TaskStack());
                g.DeathProcessingAndAuraUpdate();
            }
        });
        [StandardCard("CAP_405tb1",132829)]
        internal static void Rushed(Card card) { card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.Power=new Power { PowerTask=Payload }; }
        [StandardCard("CAP_405tb2",132830)]
        internal static void Grueling(Card card) => ObjectiveRules.Declare(card,1,Payload);
        [StandardCard("CAP_405tb3",132831)]
        internal static void Unending(Card card) => ObjectiveRules.Declare(card,4,Payload);
        [StandardCard("CAP_405tb1b",132873)]
        [StandardCard("CAP_405tb2b",132874)]
        [StandardCard("CAP_405tb3b",132875)]
        internal static void Length(Card card) { card.Implemented=true;card.ExcludeFromLegacyRandomPools=true; }
        [StandardCard("CAP_405t1",132837)]
        internal static void Destruction(Card card) => ConfigurePart(card,new CustomTask(ForceAll));
        [StandardCard("CAP_405t2",132849)]
        internal static void Conspiracy(Card card) => ConfigurePart(card,new CustomTask((g,c,s,t,stack) => {
            var candidates=c.Opponent.BoardZone.Where(m=>!m.Untouchable && !m.IsDead).ToArray();
            if(candidates.Length>0) new ControlTask(EntityType.TARGET).Process(g,c,s,candidates[g.Random.Next(candidates.Length)]);
        }));
        [StandardCard("CAP_405t3",132843)]
        internal static void Smuggling(Card card) => ConfigurePart(card,new CustomTask((g,c,s,t,stack) => {
            for(int i=0;i<2 && !c.Opponent.HandZone.IsEmpty;i++) {
                var stolen=c.Opponent.HandZone[g.Random.Next(c.Opponent.HandZone.Count)];
                using var historyTransfer=g.Facts.CardTransfer(stolen,c.Opponent.Id,Zone.HAND,s);
                using var transfer=TemporaryRules.PreserveHandTransfer(stolen);
                if(stolen.OngoingEffect is SabberStoneCore.Auras.AdaptiveCostEffect cost) cost.Remove();
                c.Opponent.HandZone.RemoveForTrade(stolen);
                stolen.Controller=c;stolen[GameTag.CONTROLLER]=c.PlayerId;
                Generic.AddHandPhase(c,stolen);
            }
        }));
        [StandardCard("CAP_405t4",132838)]
        internal static void Contraband(Card card) => ConfigurePart(card,new CustomTask((g,c,s,t,stack) => {
            for(int i=0;i<3 && g.State!=State.COMPLETE;i++) Generic.Draw(c);
        }));
        [StandardCard("CAP_405t5",132844)]
        internal static void Shiv(Card card) => ConfigurePart(card,new CustomTask((g,c,s,t,stack) => {
            foreach(var m in c.BoardZone.GetAll().Cast<IPlayable>().Concat(c.HandZone).OfType<Minion>().ToArray())
                Generic.AddEnchantmentBlock(g,Cards.FromId("CAP_405t5e"),(IPlayable)s,m);
        }));
        [StandardCard("CAP_405t6",132850)]
        internal static void Contract(Card card) => ConfigurePart(card,new CustomTask((g,c,s,t,stack) => {
            for(int i=0;i<3;i++) {
                var picked=OfflineCardPools.Pick(g,Minions(),c.HeroClass,127067, PoolDestination.Summon);
                if(picked!=null) new SummonTask(picked.Id).Process(g,c,s,t,stack);
            }
        }));
        [StandardCard("CAP_405t7",132851)]
        internal static void Perjury(Card card) => ConfigurePart(card,new CustomTask((g,c,s,t,stack) => {
            foreach(var m in c.HandZone.OfType<Minion>().ToArray()) Generic.AddEnchantmentBlock(g,Cards.FromId("CAP_405t7e"),(IPlayable)s,m);
        }));
        [StandardCard("CAP_405t8",132856)]
        internal static void Swill(Card card) => ConfigurePart(card,new HealTask(12,EntityType.HERO));
        [StandardCard("CAP_405t9",132848)]
        internal static void Tonic(Card card) => ConfigurePart(card,new SummonTask("CORE_LOOT_368"));
        [StandardCard("CAP_405t5e",132846)]
        internal static void Buff(Card card) { card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(3),Effects.Health_N(3)) }; }
        [StandardCard("CAP_405t7e",132855)]
        internal static void Discount(Card card) { card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.Power=new Power { Enchant=new Enchant(Effects.ReduceCost(2)) }; }
        private static void ConfigurePart(Card card,ISimpleTask task) { card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.Power=new Power { PowerTask=task }; }
        private static void ForceAll(Game g,Controller c,IEntity s,IPlayable t,TaskStack stack)
        {
            var queue=g.Player1.BoardZone.Concat(g.Player2.BoardZone).Where(m=>!m.Untouchable && !m.IsDead)
                .Select(m=>(Body:m,Order:m.OrderOfPlay)).ToArray();
            for(int i=queue.Length-1;i>0;i--) { int j=g.Random.Next(i+1);var tmp=queue[i];queue[i]=queue[j];queue[j]=tmp; }
            var previous=g.CurrentEventData;
            try { foreach(var original in queue) {
                var attacker=original.Body;
                if(attacker.Zone!=attacker.Controller.BoardZone || attacker.OrderOfPlay!=original.Order || attacker.Untouchable || attacker.IsDead) continue;
                var defenders=g.Player1.BoardZone.Concat(g.Player2.BoardZone).Where(m=>m.Id!=attacker.Id && !m.Untouchable && !m.IsDead).ToArray();
                if(defenders.Length==0) continue;
                Generic.AttackBlock(attacker.Controller,attacker,defenders[g.Random.Next(defenders.Length)],true,true);
            } } finally { g.CurrentEventData=previous; }
        }
    }
}
