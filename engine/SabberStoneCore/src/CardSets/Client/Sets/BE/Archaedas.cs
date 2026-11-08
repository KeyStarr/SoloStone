using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class Archaedas
    {
        internal static OfflinePoolFilter SpellPool() => OfflinePoolFilter.Discover(type: CardType.SPELL);

        [StandardCard("BE_055", 129245)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new DecisionBattlecryTask("client/BE_055/v1",
                new DiscoverThenContinueTask(SpellPool(), recipeDbfId: 129245),
                new CustomTask((g, c, s, t, stack) => {
                    var selected = stack.Playables.SingleOrDefault();
                    if (selected == null) return;
                    var spell = selected.Card; int cost = selected.Cost;
                    Generic.RemoveFromZone(c, selected);
                    var weapon = (Weapon)Generic.ChangeEntityBlock(c, selected, Cards.FromId("BE_055t"), true);
                    CostRules.AddModifier(weapon, EffectOperator.SET, cost);
                    PayloadRules.StoreCard(weapon, PayloadRules.WeaponSpell, spell,
                        PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowOwner);
                    // The client builds "cast '{0}'" once, when the weapon is shown in hand, from its tag 2 read as an
                    // ENTITY id, and does not rebuild on later tag-2 deltas: set the display before the hand phase.
                    weapon.Card.UpdateHistoryDisplays(weapon);
                    Generic.AddHandPhase(c, weapon);
                    stack.Playables.Clear();
                })) };
        }

        [StandardCard("BE_055t", 132338)]
        internal static void ConfigureWeapon(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.HistoryDisplay(GameTag.TAG_SCRIPT_DATA_NUM_1, PayloadRules.WeaponSpellReference);
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                SingleTask = new CustomTask((g, c, s, t, stack) => PayloadRules.CastWeaponSpell((IPlayable)s))
            } };
        }
    }
}

namespace SabberStoneCore.CardSets.Client
{
    internal static class B18ArchaedasPools
    {
        [OfflinePool("BE_055", 129245)]
        internal static PoolRequirement[] Declare() => new[] {
            new PoolRequirement("archaedas-spell", Sets.Archaedas.SpellPool(), new[] { "WARRIOR" })
        };
    }
}
