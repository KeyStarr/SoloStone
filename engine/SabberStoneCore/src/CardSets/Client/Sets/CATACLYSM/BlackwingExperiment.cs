using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BlackwingExperiment
    {
        [StandardCard("CATA_464", 122759)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Tags[DeathContextRules.CaptureDeath] = 1;
            card.Power = new Power { DeathrattleTask = new CustomTask((g, c, s, t, stack) => {
                var spell = Entity.FromCard(c, Cards.FromId("CATA_464t"), new EntityData {
                    [GameTag.TAG_SCRIPT_DATA_NUM_1] = DeathContextRules.AttackAtDeath((IPlayable)s),
                    [GameTag.DISPLAYED_CREATOR] = s.Id });
                Generic.AddHandPhase(c, spell);
            }) };
        }
        [StandardCard("CATA_464t", 122754)]
        internal static void Breath(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (t is ICharacter victim) Generic.DamageCharFunc((IPlayable)s, victim, s[GameTag.TAG_SCRIPT_DATA_NUM_1], true);
            }) };
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq, int> {
                { PlayReq.REQ_TARGET_TO_PLAY, 0 }});
        }
    }
}
