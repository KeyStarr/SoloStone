using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SizzlingSwarm
    {
        internal const int DbfId = 117532;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is ICharacter target)) return;
                int armor = target is Hero hero ? hero.Armor : 0;
                int damage = Generic.DamageCharFunc((IPlayable)s, target, 3, true);
                if (target is Hero damagedHero) damage += armor - damagedHero.Armor;
                g.DeathProcessingAndAuraUpdate();
                var ctx = new EffectContext(g, c.Id, s.Id, stack: stack);
                for (int i = 0; i < damage && !c.BoardZone.IsFull; i++) ctx.Summon("TLC_249");
            }) };
        }
    }
}
