using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Loader;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class SpiritsOfTheForest
    {
        [StandardCard("EDR_233", 114369)]
        internal static void Configure(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.Power = new Power(); }
        [StandardCard("EDR_233a", 114370)]
        internal static void ConfigureWolves(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                for (int i=0;i<3 && !c.BoardZone.IsFull;i++)
                {
                    // This current-printing snapshot adds Undead/Beast without changing legacy Feral Spirit tokens.
                    var wolf = (Minion)Entity.FromCard(c, ClientOverlay.Snapshot("EX1_tk11"),
                        new EntityData { [GameTag.CREATOR] = s.Id, [GameTag.DISPLAYED_CREATOR] = s.Id,
                            [GameTag.CARDRACE] = 11, [(GameTag)2542] = 1 });
                    Generic.SummonBlock(g,wolf,-1,s);
                }
            }) };
        }
        [StandardCard("EDR_233b", 114371)]
        internal static void ConfigureFalcons(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new SummonTask("EDR_233t2",2) };
        }
        [StandardCard("EDR_233t2", 114645)]
        internal static void ConfigureFalcon(Card card)
        { card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; }
    }
}
