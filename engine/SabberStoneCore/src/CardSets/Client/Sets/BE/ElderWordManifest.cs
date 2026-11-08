using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ElderWordManifest
    {
        internal const int DbfId = 130403;
        internal static void Configure(Card card)
        {
            card.Implemented=true;
            card.ExcludeFromLegacyRandomPools=true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,k) => {
                Payload.Process(g,c,s,t,k);
                RepeatRules.GrantPayload(c,(IPlayable)s,"BE_222e");
            }) };
        }
        internal static readonly ISimpleTask Payload = new SummonTask("BE_222t");
        internal static void ConfigurePayload(Card card) { card.Implemented=true; card.Power=new Power { PowerTask=Payload }; }
    }
}
