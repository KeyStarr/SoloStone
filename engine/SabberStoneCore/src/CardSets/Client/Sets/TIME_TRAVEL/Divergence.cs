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
    internal static class Divergence
    {
        [StandardCard("TIME_030", 120489)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g,c,s,t,stack) => {
                var candidates = c.HandZone.OfType<Minion>().ToArray();
                if (candidates.Length == 0) return;
                var original = candidates[g.Random.Next(candidates.Length)];
                int cost = original.Cost, attack = Math.Max(0, original.AttackDamage), health = original.BaseHealth;
                int Split(int value) => value / 2 + (value % 2 != 0 ? g.Random.Next(2) : 0);
                int cost1 = Split(cost), attack1 = Split(attack), health1 = Split(health);
                g.OnRandomHappened(true);
                void Realize(IPlayable half, int splitCost, int splitAttack, int splitHealth)
                {
                    half[HandProgressRules.PrivateAttachmentTarget] = half.Id;
                    Generic.AddEnchantmentBlock(g, Cards.FromId("TIME_030e2"), (IPlayable)s, half, splitAttack, Math.Max(1, splitHealth));
                    // The effective-cost delta retains current overlays and lets their normal lifetimes continue.
                    Enchantment.GetInstance(c, (IPlayable)s, half, Cards.FromId("TIME_030e1"), cost - splitCost);
                    var stats = half.AppliedEnchantments?.LastOrDefault(e => e.Card.Id == "TIME_030e2");
                    if (stats != null) { stats[GameTag.TAG_SCRIPT_DATA_NUM_1] = splitAttack; stats[GameTag.TAG_SCRIPT_DATA_NUM_2] = Math.Max(1,splitHealth); }
                    ((Minion)half).Damage = 0;
                    if (g.History) g.PowerHistory.Add(SabberStoneCore.Kettle.PowerHistoryBuilder.TagChange(half.Id,GameTag.COST,half.Cost));
                }
                var snapshot = PayloadRules.Store(s,1,original,(PayloadPolicy)0);
                Realize(original, cost1, attack1, health1);
                Generic.Copy(c, s, snapshot, Zone.HAND, preserveCapturedState:true,
                    initialize: copy => Realize(copy, cost-cost1, attack-attack1, health-health1));
                PayloadRules.Clear(s);
            }) };
        }
        [StandardCard("TIME_030e1", 123012)]
        internal static void CostHalf(Card card) { card.Implemented = true; card.ExpiresOnHandExit = true; card.Power = new Power(); }
        [StandardCard("TIME_030e2", 123013)]
        internal static void StatsHalf(Card card) { card.Implemented = true; card.Power = new Power {
            Enchant = new Enchant(Effects.SetAttackHealth(0)) { UseScriptTag = true } }; }
    }
}
