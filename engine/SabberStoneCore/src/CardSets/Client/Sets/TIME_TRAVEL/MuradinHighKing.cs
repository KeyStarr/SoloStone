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
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MuradinHighKing
    {
        private const int HammerKey=119647;
        [StandardCard("TIME_209",119643)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,k)=> {
                var hammer=c.DeckZone.Concat(c.HandZone).OfType<Weapon>().FirstOrDefault(p=>p.Card.Id=="TIME_209t")
                    ?? (c.Hero.Weapon?.Card.Id=="TIME_209t"?c.Hero.Weapon:null);
                if(hammer==null) return;
                var host=(IPlayable)s;
                PayloadRules.Store(host,HammerKey,hammer);
                Generic.AddEnchantmentBlock(g,Cards.FromId("TIME_209e"),host,host,hammer.AttackDamage,hammer.Durability);
                if(hammer==c.Hero.Weapon) c.Hero.ClearWeapon(); else hammer.Zone.Remove(hammer);
                hammer[PayloadRules.Stored]=1;
                c.SetasideZone.Add(hammer);
            }), DeathrattleTask=new CustomTask((g,c,s,t,k)=> {
                foreach(var captured in PayloadRules.Entries(s,HammerKey))
                Generic.Copy(c,s,captured,Zone.HAND,preserveCapturedState:true);
            }) };
        }
        [StandardCard("TIME_209e",119644)]
        internal static void Holding(Card card)
        {
            card.Implemented=true;
            card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(0),Effects.Health_N(0),new Effect(GameTag.WINDFURY,EffectOperator.SET,1)) { UseScriptTag=true } };
        }
        [StandardCard("TIME_209t",119647)]
        internal static void Hammer(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.Power=new Power { DeathrattleTask=new CustomTask((g,c,s,t,k)=> {
                int bonus=(PayloadRules.Entries(s,HammerKey).FirstOrDefault()?[PayloadRules.Value]??0)+2;
                var next=(Weapon)Entity.FromCard(c,Cards.FromId("TIME_209t"),new EntityData {[GameTag.CREATOR]=s.Id});
                PayloadRules.StoreValue(next,HammerKey,bonus,PayloadPolicy.Copy);
                Effects.Attack_N(bonus).ApplyTo(next);
                Generic.ShuffleIntoDeck(c,s,next);
            }) };
        }
        [StandardCard("TIME_209t2",119653)]
        internal static void Avatar(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int> {{PlayReq.REQ_TARGET_TO_PLAY,0},{PlayReq.REQ_FRIENDLY_TARGET,0}});
            card.Power=new Power { PowerTask=new AddEnchantmentTask("TIME_209t2e",EntityType.TARGET) };
        }
        [StandardCard("TIME_209t2e",121003)]
        internal static void AvatarBuff(Card card)
        {
            card.Implemented=true;
            card.Power=new Power { Enchant=new Enchant(Effects.Attack_N(2)) { IsOneTurnEffect=true },
                Trigger=new Trigger(TriggerType.AFTER_ATTACK) { TriggerSource=TriggerSource.ENCHANTMENT_TARGET,
                    SingleTask=new DamageTask(2,EntityType.ENEMIES) } };
        }
    }
}
