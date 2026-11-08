using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class AzalinaSoulsever
    {
        [StandardCard("JAIL_430",126055)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                while (g.State == State.RUNNING && c.Hero.Health > 0 && c.Opponent.Hero.Health > 0 && !c.HandZone.IsFull && !c.DeckZone.IsEmpty)
                    Generic.Draw(c);
            }) };
        }
        internal static void PrepareDecks(Game game)
        {
            var players = new[]{game.Player1,game.Player2};
            if (!players.Any(p=>p.DeckZone.Any(e=>e.Card.Id=="JAIL_430"))) return;
            var decks = players.Select(p=>p.DeckZone.ToArray()).ToArray();
            for (int side=0;side<2;side++)
            {
                var owner=players[side]; var source=decks[side].FirstOrDefault(e=>e.Card.Id=="JAIL_430");
                if(source==null) continue;
                owner.Hero.BaseHealth=40;
                var candidates=decks[1-side].ToList();
                int count=Math.Min(20,candidates.Count);
                for(int i=0;i<count;i++)
                {
                    int index=game.Random.Next(candidates.Count);var original=candidates[index];candidates.RemoveAt(index);
                    var copy=Entity.FromCard(owner,original.Card,new EntityData {
                        [PayloadRules.CopySourceController]=original.Controller.PlayerId,
                        [GameTag.CREATOR]=source.Id,[GameTag.DISPLAYED_CREATOR]=source.Id
                    });
                    owner.DeckZone.Add(copy);
                }
            }
        }
    }
}
