using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class MugZee
    {
        [StandardCard("JAIL_800", 126416)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power();
            StartOfGameRules.Declare(card, source => {
                var c = source.Controller;
                bool mug = c.DeckCards.Count(x => x.Type == CardType.MINION) == 1;
                bool zee = c.DeckCards.All(x => x.Type != CardType.SPELL);
                if (mug) new ReplaceHeroPower(Cards.FromId("JAIL_800hp1")).Process(c.Game,c,source,null);
                if (zee)
                {
                    if (mug) BasicHeroPowerRules.InstallSecondary(c,source,"JAIL_800hp2");
                    else new ReplaceHeroPower(Cards.FromId("JAIL_800hp2")).Process(c.Game,c,source,null);
                }
                Refresh(c);
            });
        }
        [StandardCard("JAIL_800e", 126417)]
        internal static void Discount(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power { Aura = new Aura(AuraType.HAND, Effects.ReduceCost(2)) {
                Restless = true, Condition = new SelfCondition(p => p is Minion
                    && p.Controller.HeroPowers.Any(h => h.Card.Id == "JAIL_800hp1")
                    && p.Game.HistoryFacts.Query.TurnsTaken(p.Controller.Id) >= 3
                    && p.Controller.NumMinionsPlayedThisTurn == 0)
            } };
        }
        [StandardCard("JAIL_800hp1", 127468)]
        internal static void Mug(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power();
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, MugRemaining);
        }
        [StandardCard("JAIL_800hp2", 127476)]
        internal static void Zee(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true; card.ConsumesHistory = true;
            card.Power = new Power();
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, ZeeProgress);
        }
        internal static void Refresh(Controller c)
        {
            if (c.Hero == null) return;
            var mug = c.HeroPowers.FirstOrDefault(p => p.Card.Id == "JAIL_800hp1");
            if (mug != null) mug[(GameTag)4414] = MugRemaining(mug) > 0 ? 1 : 0; // LOCK_VISUAL
            var effect = c.AppliedEnchantments?.FirstOrDefault(e => e.Card.Id == "JAIL_800e");
            if (mug != null && effect == null)
            {
                effect = Enchantment.GetInstance(c,mug,c,Cards.FromId("JAIL_800e"));
                effect.Power.Aura.Activate(effect);
            }
            effect?.OngoingEffect?.Update();
            if (mug == null) effect?.Remove();
            foreach (var power in c.HeroPowers)
                if (power.Card.Id == "JAIL_800hp1" || power.Card.Id == "JAIL_800hp2")
                    power.Card.UpdateHistoryDisplays(power);
        }
        private static int PlayCount(Controller c) => c.Game.HistoryFacts.Query.Plays()
            .Count(f => f.ControllerId == c.Id && f.CardType == CardType.MINION);
        private static int MugRemaining(IPlayable p) => System.Math.Max(0,3-p.Game.HistoryFacts.Query.TurnsTaken(p.Controller.Id));
        // Published SDN1 is PROGRESS toward the fifth play, not minions remaining. JAIL_800hp2 carries ENTITY_TAG_THRESHOLD
        // (2459=2 selects SDN1, 2460=5): the pinned client's EntityTagThresholdCardTextBuilder prints "(5 - SDN1 left!)",
        // or "(Ready!)" once SDN1 >= 5. Feeding "remaining" inverted the hint (7 plays showed 2 left, the 8th showed 3).
        // 0..4 plays since the last doubling; the 5th play is doubled and wraps to 0. "Ready!" never shows (unpinned).
        private static int ZeeProgress(IPlayable p) => PlayCount(p.Controller)%5;
        internal static bool DoubleBattlecry(IPlayable source)
        {
            var c = source.Controller;
            if (!(source is Minion) || !c.HeroPowers.Any(p => p.Card.Id == "JAIL_800hp2")) return false;
            var plays = c.Game.HistoryFacts.Query.Plays().Where(f => f.ControllerId == c.Id && f.CardType == CardType.MINION).ToArray();
            return plays.Length > 0 && plays.Length%5 == 0 && plays[plays.Length-1].EntityId == source.Id;
        }
    }
}
