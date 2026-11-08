using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.Actions
{
    public static class DredgeRules
    {
        public static DiscoverThenContinueTask Choose() => new DiscoverThenContinueTask(Offers, Prepare);
        private static Card[] Offers(Game g, Controller c, IEntity s, TaskStack k) => c.DeckZone.Take(3).Select(p => p.Card).ToArray();
        private static void Prepare(IEntity source, IPlayable offer)
        {
            var c = source.Controller;
            int index = c.Choice.Choices.IndexOf(offer.Id);
            offer[Generic.PrivateDecisionOfferTag] = c.PlayerId;
            offer[DiscoverRealRules.BoundEntity] = c.DeckZone[index].Id;
            DiscoverRealRules.CopyDisplay(offer, c.DeckZone[index]);
        }
        public static ISimpleTask PutOnTop() => new CustomTask((g,c,s,t,k) => {
            var offer = k.Playables.LastOrDefault();
            if (offer != null && g.IdEntityDic.TryGetValue(offer[DiscoverRealRules.BoundEntity], out var real) && real.Zone == c.DeckZone)
                c.DeckZone.PutOnTop(real);
        });
    }
}
