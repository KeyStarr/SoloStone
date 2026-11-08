using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class ElderWordFlay
    {
        internal const int DbfId = 130404;
        internal static void Configure(Card card)
        {
            card.Implemented=true;
            card.ExcludeFromLegacyRandomPools=true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,k) => {
                Payload.Process(g,c,s,t,k);
                RepeatRules.GrantPayload(c,(IPlayable)s,"BE_223e");
            }) };
        }
        internal static readonly ISimpleTask Payload = new CustomTask((g,c,s,t,k) => {
            var targets=c.Opponent.BoardZone.GetAll().Where(m=>m.Health>0 && !m.ToBeDestroyed).Cast<ICharacter>().Append(c.Opponent.Hero).ToArray();
            var target=targets[g.Random.Next(targets.Length)]; g.OnRandomHappened(true);
            Generic.DamageCharFunc((IPlayable)s,target,3,true);
        });
        internal static void ConfigurePayload(Card card) { card.Implemented=true; card.Power=new Power { PowerTask=Payload }; }
    }
}
