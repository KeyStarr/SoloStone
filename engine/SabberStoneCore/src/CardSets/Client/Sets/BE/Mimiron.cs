using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Mimiron
    {
        private static readonly string[] Tech = { "DRG_069", "EX1_066", "OG_254", "EX1_048", "CORE_EX1_002", "CORE_EX1_005" };
        private static bool Ready(IPlayable source) => source.Controller.BoardZone.Any(m=>m.Card.IsRace(Race.MECHANICAL));

        [StandardCard("BE_588",132009)]
        internal static void Configure(Card card)
        {
            card.Implemented=true; card.ExcludeFromLegacyRandomPools=true;
            card.PoweredUpWhen(Ready);
            card.Power=new Power { PowerTask=new CustomTask((g,c,s,t,stack)=> {
                if(!Ready((IPlayable)s)) return;
                var candidates=OfflinePoolDeclarations.Cards.Single(d=>d.CardId=="BE_588").Pools.SelectMany(pool=>
                        OfflineCardPools.Candidates(g,c.HeroClass,pool.Filter,s.Card.AssetId))
                    .Where(candidate=>Tech.Contains(candidate.Id)).OrderBy(candidate=>candidate.AssetId).ToArray();
                var enemy=c.Opponent;
                bool Matches(Card candidate)
                {
                    switch(candidate.Id)
                    {
                        case "DRG_069": return enemy.Hero.Armor>0;
                        case "EX1_066": return enemy.Hero.Weapon!=null;
                        case "OG_254": return enemy.SecretZone.Count>0;
                        case "EX1_048": return enemy.BoardZone.Any(m=>!m.IsSilenced &&
                            (m.HasDeathrattle || m.AppliedEnchantments?.Any()==true));
                        case "CORE_EX1_002": return enemy.BoardZone.Any(m=>m.HasTaunt);
                        case "CORE_EX1_005": return enemy.BoardZone.Any(m=>m.AttackDamage>=7);
                        default: return false;
                    }
                }
                var context=new EffectContext(g,c.Id,s.Id,stack:stack);
                var matching=candidates.Where(Matches).ToArray();
                var offers=context.SampleDistinct(matching,Math.Min(3,matching.Length));
                var rest=candidates.Except(offers).ToArray();
                offers=offers.Concat(context.SampleDistinct(rest,Math.Min(3-offers.Length,rest.Length))).ToArray();
                TimeTravelPastPools.Open(g,c,s,offers);
            }) };
        }
    }
}
