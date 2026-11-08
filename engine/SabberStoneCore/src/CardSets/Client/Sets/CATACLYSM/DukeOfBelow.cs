using System;
using System.Collections.Generic;
using System.Linq;
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
    internal static class DukeOfBelow
    {
        [StandardCard("CATA_493",122713)]
        internal static void Configure(Card card)
        {
            card.Implemented=true;card.ExcludeFromLegacyRandomPools=true;card.ConsumesHistory=true;
            card.Power=new Power { Aura=new DiscardScalingAura() };
        }
        [StandardCard("CATA_493e",130993)]
        internal static void Buff(Card card) { card.Implemented=true;card.Power=new Power(); }

        // The exported actor retains the realized amount; no game state lives in the recipe.
        internal sealed class DiscardScalingAura : IAura
        {
            public IPlayable Owner { get; private set; }
            public void Activate(IPlayable owner)
            {
                var aura=new DiscardScalingAura { Owner=owner };
                owner.OngoingEffect=aura;owner.Game.Auras.Add(aura);
            }
            public void Clone(IPlayable owner)=>Activate(owner);
            private Enchantment Actor()=>Owner.AppliedEnchantments?.FirstOrDefault(e=>e.Card.Id=="CATA_493e");
            private void Apply(int amount)
            {
                var body=(Minion)Owner;var actor=Actor();int previous=actor?[GameTag.TAG_SCRIPT_DATA_NUM_1]??0;
                if(previous==amount) return;
                if(actor==null) actor=Enchantment.GetInstance(body.Controller,body,body,Cards.FromId("CATA_493e"));
                int health=body.Health;
                ATK.Effect(EffectOperator.ADD,amount-previous).ApplyTo(body);
                Health.Effect(EffectOperator.ADD,amount-previous).ApplyTo(body);
                if(amount<previous) body.Damage=Math.Max(0,body.BaseHealth-health);
                actor[GameTag.TAG_SCRIPT_DATA_NUM_1]=amount;
            }
            public void Update()
            {
                if(Owner.OngoingEffect!=this) { Owner.Game.Auras.Remove(this);return; }
                Apply(2*Owner.Game.HistoryFacts.Query.Discards().Count(f=>f.ControllerId==Owner.Controller.Id));
            }
            public void Remove()
            {
                Apply(0);Actor()?.Remove();Owner.OngoingEffect=null;
            }
        }
    }
}
