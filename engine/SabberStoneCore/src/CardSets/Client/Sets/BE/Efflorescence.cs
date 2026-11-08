using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Efflorescence
    {
        internal const int DbfId = 131177;
        internal static void Configure(Card card)
        {
            card.Implemented=true;
            card.ExcludeFromLegacyRandomPools=true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,k) => {
                var drawn = new EffectContext(g,c.Id,s.Id).DrawWhere(x => x.Card.Type == CardType.SPELL && x.Card.Id != s.Card.Id);
                if (drawn != null && drawn.Zone == c.HandZone && drawn.Card[GameTag.SPELL_SCHOOL] == 4)
                    RepeatRules.Mark(drawn,(IPlayable)s,"BE_801e");
            }) };
        }
    }
}
