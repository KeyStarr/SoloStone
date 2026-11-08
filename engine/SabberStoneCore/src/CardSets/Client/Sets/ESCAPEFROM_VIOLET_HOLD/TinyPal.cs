using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class TinyPal
    {
        private static readonly string[] Forms = { "JAIL_458t1", "JAIL_458t2", "JAIL_458t3", "JAIL_458t4" };
        private static OfflinePoolFilter Earth() => OfflinePoolFilter.Random(type: CardType.MINION, minCost: 3, maxCost: 3);
        private static OfflinePoolFilter Air() => OfflinePoolFilter.Random(type: CardType.MINION, keyword: GameTag.BATTLECRY);
        internal static PoolRequirement[] EarthPool() => new[] { new PoolRequirement("earth-minion", Earth(), new[] { "SHAMAN" }, allowThinExactCost: true, destination: PoolDestination.Summon) };
        internal static PoolRequirement[] AirPool() => new[] { new PoolRequirement("air-battlecry", Air(), new[] { "SHAMAN" }) };

        [StandardCard("JAIL_458", 127711)]
        internal static void Configure(Card card)
        {
            ConfigureForm(card);
            card.Power.PowerTask = new DecisionBattlecryTask("client/JAIL_458/v1",
                new DiscoverThenContinueTask(Forms[0], Forms.Skip(1).ToArray()), Reload());
        }
        [StandardCard("JAIL_458t1", 127713)] internal static void Frost(Card card) => ConfigureForm(card);
        [StandardCard("JAIL_458t2", 127715)] internal static void Fire(Card card) => ConfigureForm(card);
        [StandardCard("JAIL_458t3", 127716)] internal static void Earth(Card card) => ConfigureForm(card);
        [StandardCard("JAIL_458t4", 127717)] internal static void Air(Card card) => ConfigureForm(card);

        private static void ConfigureForm(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                TriggerSource = TriggerSource.HERO,
                SingleTask = AfterAttackRules.Register(card.AssetId,
                    new CustomTask((g, c, s, t, stack) => ResolveAmmo(g, c, s, card, stack)),
                    new DiscoverThenContinueTask((g, c, s, stack) => g.State == State.COMPLETE ||
                        c.Hero.Weapon?.Id != s.Id || c.Hero.Weapon.Durability <= 0 ? Array.Empty<Card>() :
                        Forms.Where(id => id != card.Id).Select(Cards.FromId).ToArray(),
                        (s, offer) => offer[Generic.PrivateDecisionOfferTag] = s.Controller.PlayerId), Reload())
            } };
        }

        private static ISimpleTask Reload() => new CustomTask((g, c, s, t, stack) => {
            var chosen = stack.Playables.LastOrDefault();
            if (chosen == null || s is not Weapon weapon || weapon.ToBeDestroyed || g.State == State.COMPLETE) return;
            // Same entity and equal printed stats preserve damage and all weapon enchantments.
            Generic.ChangeEntityBlock(c, weapon, chosen.Card, false);
        });

        private static void ResolveAmmo(Game g, Controller c, IEntity source, Card form, TaskStack stack)
        {
            if (g.State == State.COMPLETE) return;
            var context = new EffectContext(g, c.Id, source.Id, stack: stack);
            switch (form.Id)
            {
                case "JAIL_458t1":
                    var enemies = c.Opponent.BoardZone.GetAll().Cast<ICharacter>().Concat(new[] { c.Opponent.Hero })
                        .Where(e => !e.IsDead && !(e is Minion m && m.IsDormant) && e.Id != stack.Number1).ToList();
                    for (int i = 0; i < 2 && enemies.Count > 0; i++)
                    {
                        int pick = g.Random.Next(enemies.Count); g.Facts.Freeze(enemies[pick],source); enemies.RemoveAt(pick);
                    }
                    break;
                case "JAIL_458t2":
                    new DamageTask(1, EntityType.ENEMIES).Process(g, c, source, null);
                    break;
                case "JAIL_458t3":
                    var minion = context.PickRandomCard(Earth(), recipeDbfId: form.AssetId);
                    if (minion != null)
                    {
                        int id = context.Summon(minion.Id);
                        if (id != 0) new Enchant(GameTag.TAUNT, EffectOperator.SET, 1).ActivateTo(g.IdEntityDic[id]);
                    }
                    break;
                case "JAIL_458t4":
                    var battlecry = context.PickRandomCard(Air(), recipeDbfId: form.AssetId);
                    if (battlecry != null)
                    {
                        var generated = Generic.DrawCard(c, battlecry);
                        if (generated?.Zone == c.HandZone) new Enchant(Cost.Effect(EffectOperator.SUB, 2)).ActivateTo(generated);
                    }
                    break;
            }
            g.DeathProcessingAndAuraUpdate();
        }
    }
}

namespace SabberStoneCore.CardSets.Client
{
    internal static class B17TinyPalPools
    {
        [OfflinePool("JAIL_458t3", 127716)]
        internal static PoolRequirement[] EarthPool() => Sets.TinyPal.EarthPool();
        [OfflinePool("JAIL_458t4", 127717)]
        internal static PoolRequirement[] AirPool() => Sets.TinyPal.AirPool();
    }
}
