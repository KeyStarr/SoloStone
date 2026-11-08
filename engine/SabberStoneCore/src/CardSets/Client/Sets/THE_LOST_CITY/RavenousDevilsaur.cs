using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class RavenousDevilsaur
    {
        internal const int DbfId = 117610;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_IF_AVAILABLE, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (!(t is Minion target)) { KindredRules.BeginMatchedClause((IPlayable)s); return; }
                int attack = target.AttackDamage, health = target.Health;
                new DestroyTask(EntityType.TARGET).Process(g, c, s, target, stack);
                int repeats = KindredRules.BeginMatchedClause((IPlayable)s);
                for (int i = 0; i < repeats; i++)
                    Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_829te2"), (IPlayable)s, s, attack, health, 0);
            }) };
        }

        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.Attack_N(0), Effects.Health_N(0)) { UseScriptTag = true } };
        }
    }
}
