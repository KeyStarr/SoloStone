using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Divination
    {
        // 37.0 export: base/Core Wisp plus the Undead Wisp tokens; stats and name lookalikes do not qualify.
        internal static bool Wisp(ICharacter target) => target.Card.Id is
            "CS2_231" or "CORE_CS2_231" or "OG_195c" or "GIL_553t" or "GILA_BOSS_55t" or "EDR_851t";
        [StandardCard("EDR_804",114012)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new System.Collections.Generic.Dictionary<PlayReq,int> {
                { PlayReq.REQ_TARGET_TO_PLAY,0 }, { PlayReq.REQ_MINION_TARGET,0 }, { PlayReq.REQ_FRIENDLY_TARGET,0 }
            });
            card.AddTargetingPredicate(Wisp);
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (t is not Minion m || m.Controller != c || !Wisp(m) || m.Zone != c.BoardZone) return;
                m.Destroy(); g.DeathProcessingAndAuraUpdate();
                for (int i=0;i<3;i++) Generic.Draw(c);
            }) };
        }
    }
}
