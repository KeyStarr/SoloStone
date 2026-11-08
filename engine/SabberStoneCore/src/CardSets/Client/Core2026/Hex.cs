using SabberStoneCore.Model;
using SabberStoneCore.Enchants;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Core2026
{
    internal static class Hex
    {
        internal const int DbfId = 69551;
        internal static void Configure(Card card)
        {
            // The legacy transform kept native Divine Shield buffs; current Hex must create an unbuffed Frog.
            card.Power = new Power { PowerTask = new TransformTask("hexfrog", EntityType.TARGET, clearNativeKeywords: true) };
        }
    }
}
