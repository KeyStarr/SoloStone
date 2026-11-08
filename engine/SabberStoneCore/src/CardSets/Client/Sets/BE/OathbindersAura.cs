using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class OathbindersAura
    {
        [StandardCard("BE_146", 130673)]
        internal static void Configure(Card card) => AuraSpellRules.Declare(card, holyCast:
            new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                var soldier = (Minion)Entity.FromCard(c, Cards.FromId("BE_142t"),
                    new EntityData { [GameTag.DISPLAYED_CREATOR] = s.Id });
                ATK.Effect(EffectOperator.SET, s[AuraSpellRules.CastCost]).ApplyTo(soldier);
                Health.Effect(EffectOperator.SET, s[AuraSpellRules.CastCost]).ApplyTo(soldier);
                Generic.SummonBlock(g, soldier, -1, s);
                if (soldier.Health <= 0) soldier.ToBeDestroyed = true;
            }));
    }
}
