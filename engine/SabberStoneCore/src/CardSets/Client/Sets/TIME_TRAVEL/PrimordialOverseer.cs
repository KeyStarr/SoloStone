using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using System.Collections.Generic;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class PrimordialOverseer
    {
        internal const int DbfId = 120437;
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.PoweredUpWhen(CastNatureWhileHolding);
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.CAST_SPELL) {
                    TriggerActivation = TriggerActivation.HAND,
                    TriggerSource = TriggerSource.FRIENDLY,
                    Condition = new SelfCondition(me => IsNatureSpell(me.Card)),
                    SingleTask = new SetGameTagTask(GameTag.TAG_SCRIPT_DATA_NUM_1, 1, EntityType.SOURCE)
                },
                PowerTask = new CustomTask((g, c, s, t, stack) => {
                    if (!CastNatureWhileHolding((IPlayable)s)) return;
                    foreach (var effect in Effects.AttackHealth_N(1)) effect.ApplyTo((IPlayable)s);
                    new EffectContext(g, c.Id, s.Id, stack: stack).Draw();
                })
            };
        }

        // Legacy CardDefs predates spell schools; these Nature identities come from pinned 37.0.0.254477.
        private static readonly HashSet<int> LegacyNatureSpells = new HashSet<int> {
            51, 95, 137, 149, 151, 213, 233, 238, 239, 253, 254, 299,
            301, 325, 381, 451, 459, 493, 505, 629, 766, 767, 773, 808,
            836, 1124, 1802, 1809, 1832, 1925, 1976, 1995, 2001, 2006, 2009, 2041,
            2176, 2177, 2387, 2612, 2620, 2659, 2789, 2791, 2792, 2793, 25395, 25398,
            38262, 38266, 38289, 38329, 38652, 38654, 38655, 38724, 38934, 38938, 38940, 39712,
            39714, 40402, 40455, 40523, 40694, 40991, 41081, 41094, 41206, 41207, 41212, 41407,
            41521, 41834, 41879, 42011, 42168, 42648, 42656, 42665, 43282, 43288, 43289, 43290,
            43331, 43332, 43333, 43361, 43362, 43363, 45828, 45904, 46067, 46303, 47063, 47152,
            47222, 47553, 47887, 48051, 48161, 48524, 48607, 48798, 48819, 48820, 48821, 48837,
            48860, 48862, 49052, 49231, 49246, 49275, 49995, 49997, 50042, 50997, 51140, 51187,
            51373, 51769, 51773, 51775, 51779, 51781, 52148, 52421, 52819, 52821, 53576, 53578,
            53579, 53978, 53979, 54001, 54128, 54160, 54400, 54405, 54833, 54922, 54965, 54971,
            55035, 55036, 55039, 55040, 55041, 55052, 55778, 55779, 55780, 55784, 55789, 55790,
            55791, 57587, 57993, 57994,
        };
        internal static bool IsNatureSpell(Card card) => card.Type == CardType.SPELL
            && (card[GameTag.SPELL_SCHOOL] == 4 || LegacyNatureSpells.Contains(card.AssetId));
        private static bool CastNatureWhileHolding(IPlayable source) => source[GameTag.TAG_SCRIPT_DATA_NUM_1] != 0;
    }
}
