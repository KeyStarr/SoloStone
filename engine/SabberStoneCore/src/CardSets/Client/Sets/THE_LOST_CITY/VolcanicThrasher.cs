using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class VolcanicThrasher
    {
        internal const int DbfId = 117533;
        // Legacy CardDefs predates spell schools; FIRE identities from pinned 37.0.0.254477 export.
        private static readonly HashSet<int> LegacyFireSpells = new HashSet<int> {
            286, 315, 394, 585, 864, 950, 974, 1004, 1087, 1640, 1927, 2284,
            2289, 2323, 2330, 2359, 2432, 2480, 2539, 2541, 2678, 2864, 2874, 2997,
            25271, 25396, 25401, 38413, 38935, 39715, 39747, 39854, 40005, 40066, 40297, 40316,
            40317, 40498, 40500, 40545, 40546, 40938, 41151, 41159, 41690, 41878, 43407, 43411,
            43412, 43414, 45271, 46286, 46425, 46437, 46715, 46953, 47251, 47414, 48393, 48824,
            49333, 50086, 50120, 50122, 51161, 51371, 51530, 52703, 53823, 54429, 55101, 55250,
            55276, 55858, 55859, 55860, 55861, 55935, 56170, 56173, 57316, 57483, 57484, 58970,
            69501, 69502,
        };
        [StandardCard("TLC_223", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                bool kindred = KindredRules.Match((IPlayable)s);
                // Native draw impact needs a typed source before the drawn actor's SHOW.
                if (g.History) g.PowerHistory.Add(new PowerHistoryMetaData { Type = MetaDataType.TARGET, Info = new List<int> { s.Id } });
                var drawn = new EffectContext(g, c.Id, s.Id).DrawWhere(p => p.Card.Type == CardType.SPELL
                    && (p.Card[GameTag.SPELL_SCHOOL] == 2 || LegacyFireSpells.Contains(p.Card.AssetId)));
                int repeats = kindred ? KindredRules.BeginMatchedClause((IPlayable)s) : 0;
                for (int i = 0; i < repeats && drawn != null; i++) Generic.AddEnchantmentBlock(g, Cards.FromId("TLC_223e"), (IPlayable)s, drawn);
            }) };
        }
        [StandardCard("TLC_223e", 117607)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(new Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 2)) };
        }
    }
}
