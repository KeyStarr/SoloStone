using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class BatMask
    {
        internal const int DbfId = 118406;
        [StandardCard("DINO_402", DbfId)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq,int> {
                { PlayReq.REQ_TARGET_TO_PLAY,0 }, { PlayReq.REQ_MINION_TARGET,0 }, { PlayReq.REQ_FRIENDLY_TARGET,0 }
            });
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                if (!(t is Minion minion) || minion.Zone != c.BoardZone) return;
                Generic.AddEnchantmentBlock(g,Cards.FromId("DINO_402e"),(IPlayable)s,minion);
                int budget = c.BoardZone.FreeSpace;
                for (int i=0; i<budget && !c.BoardZone.IsFull && minion.Zone == c.BoardZone && !minion.IsDead; i++)
                {
                    int position = minion.ZonePosition + 1;
                    while (position < c.BoardZone.SlotCount && c.BoardZone.GetSlot(position)[GameTag.DISPLAYED_CREATOR] == s.Id) position++;
                    Generic.Copy(c,s,minion,Zone.PLAY,zonePosition:position,preserveBoardMinionState:true);
                }
            }) };
        }
        [StandardCard("DINO_402e",118407)]
        internal static void ConfigureBuff(Card card)
        {
            card.Implemented = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetAttack(1),Effects.SetMaxHealth(1)) };
        }
    }
}
