using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
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
    internal static class Shaladrassil
    {
        private static readonly HashSet<string> Ysera = new HashSet<string>(StringComparer.Ordinal) {
            "EX1_572", "VAN_EX1_572", "CORE_VAN_EX1_572", "CS3_033", "LEG_CS3_033", "DRG_320", "EDR_000", "Story_07_Ysera"
        };
        [StandardCard("EDR_846",115090)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.PoweredUpWhen(CostRules.QualifiedHeldDreams);
            var dreams = new[]{"DREAM_01","DREAM_02","DREAM_03","DREAM_04","DREAM_05"}
                .Select(SabberStoneCore.Loader.ClientOverlay.Snapshot).ToArray();
            foreach(var dream in dreams) dream.UseBehaviourFrom(Cards.FromId(dream.Id));
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                string[] ids = CostRules.QualifiedDreams((IPlayable)s)
                    ? new[]{"EDR_846t3","EDR_846t4","EDR_846t5","EDR_846t2","EDR_846t1"}
                    : new[]{"DREAM_01","DREAM_02","DREAM_03","DREAM_04","DREAM_05"};
                // Delivery order, including overflow, is randomized independently for each cast.
                for(int i=ids.Length-1;i>0;i--){int j=g.Random.Next(i+1);var swap=ids[i];ids[i]=ids[j];ids[j]=swap;}
                g.OnRandomHappened(true);
                foreach(string id in ids) Generic.AddHandPhase(c,Entity.FromCard(c,dreams.FirstOrDefault(d => d.Id == id) ?? Cards.FromId(id),
                    new EntityData { [GameTag.DISPLAYED_CREATOR]=s.Id }));
            }) };
        }
        [StandardCard("DREAM_01",340)]
        [StandardCard("DREAM_03",489)]
        [StandardCard("EDR_846t5",115094)]
        internal static void Body(Card card) { card.Implemented=true;card.Power=new Power(); }
        [StandardCard("EDR_846t3",115092)]
        internal static void Sister(Card card)
        {
            card.Implemented=true;
            card.Power=new Power { Aura=new Aura(AuraType.HERO,"EDR_846t3e") };
        }
        [StandardCard("EDR_846t3e",118553)]
        internal static void SisterAura(Card card){card.Implemented=true;card.Power=new Power {Enchant=new Enchant(Effects.CantBeTargetedBySpellsAndHeroPowers)};}
        [StandardCard("DREAM_02",301)]
        internal static void Awakens(Card card)
        {
            card.Implemented=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                var ids = c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll()).Where(m=>!Ysera.Contains(m.Card.Id))
                    .Cast<ICharacter>().Append(c.Hero).Append(c.Opponent.Hero).Select(m=>m.Id).ToArray();
                new EffectContext(g,c.Id,s.Id,stack:stack).DamageBatch(ids,5,spellDamage:true);
            }) };
        }
        [StandardCard("EDR_846t4",115093)]
        internal static void CorruptedAwakening(Card card)
        {
            card.Implemented=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=>{
                foreach(var m in c.BoardZone.GetAll().Concat(c.Opponent.BoardZone.GetAll()).Where(m=>Ysera.Contains(m.Card.Id)).ToArray())m.Destroy();
                g.DeathProcessingAndAuraUpdate();
                new EffectContext(g,c.Id,s.Id,stack:stack).DamageBatch(c.Opponent.BoardZone.GetAll().Cast<ICharacter>()
                    .Append(c.Opponent.Hero).Where(m=>!m.IsDead).Select(m=>m.Id).ToArray(),5,spellDamage:true);
            }) };
        }
        [StandardCard("DREAM_04",808)]
        internal static void Dream(Card card)
        {
            card.Implemented=true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int>{{PlayReq.REQ_TARGET_TO_PLAY,0},{PlayReq.REQ_MINION_TARGET,0}});
            card.Power=new Power {PowerTask=new ReturnHandTask(EntityType.TARGET)};
        }
        [StandardCard("DREAM_05",217)]
        internal static void Nightmare(Card card)
        {
            card.Implemented=true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int>{{PlayReq.REQ_TARGET_TO_PLAY,0},{PlayReq.REQ_MINION_TARGET,0}});
            card.Power=new Power {PowerTask=new AddEnchantmentTask("DREAM_05e",EntityType.TARGET)};
        }
        [StandardCard("EDR_846t2",115095)]
        internal static void CorruptedDream(Card card)
        {
            card.Implemented=true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int>{{PlayReq.REQ_TARGET_TO_PLAY,0},{PlayReq.REQ_MINION_TARGET,0}});
            card.Power=new Power {PowerTask=new CustomTask((g,c,s,t,stack)=>{
                if(t is Minion m) {var owner=m.Controller; m.Zone.Remove(m); m.Reset(); Generic.ShuffleIntoDeck(owner,s,m);}
            })};
        }
        [StandardCard("EDR_846t1",115096)]
        internal static void CorruptedNightmare(Card card)
        {
            card.Implemented=true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int>{{PlayReq.REQ_TARGET_TO_PLAY,0},{PlayReq.REQ_MINION_TARGET,0}});
            card.Power=new Power {PowerTask=ComplexTask.Create(new AddEnchantmentTask("EDR_846t1e",EntityType.TARGET),
                new AddEnchantmentTask("EDR_846te2",EntityType.TARGET))};
        }
        [StandardCard("EDR_846t1e",115097)]
        internal static void NightmareStats(Card card){card.Implemented=true;card.Power=new Power {Enchant=new Enchant(Effects.AttackHealth_N(5))};}
        [StandardCard("EDR_846te2",115098)]
        internal static void NightmareImmune(Card card){card.Implemented=true;card.Power=new Power {Enchant=new Enchant(Effects.Immune){IsOneTurnEffect=true}};}
    }
}
