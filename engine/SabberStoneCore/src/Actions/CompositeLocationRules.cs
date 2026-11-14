using System;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

namespace SabberStoneCore.Actions
{
    /// <summary>Immutable ordered location recipes; the existing effect frame owns all suspended state.</summary>
    public static class CompositeLocationRules
    {
        public static OfflinePoolFilter Spells => OfflinePoolFilter.Discover(CardType.SPELL);
        public static bool Eligible(Controller c) => c.DeckCards.Select(card => card.Cost).Distinct().Count() >= 10;
        public static string[] Components(int tier) => Enumerable.Range(1, tier == 1 ? 6 : 7)
            .Select(n => "TLC_100t" + tier + n).ToArray();
        private static Card Part(IEntity source, GameTag tag) => source.Card[tag] == 0 ? null : Cards.FromAssetId(source.Card[tag]);
        private static int Kind(Card part) => part == null ? 0 : part.Id[part.Id.Length - 1] - '0';
        private static int Tier(Card body) => body.Id[body.Id.Length - 1] - '0';
        private static bool FriendlyTarget(Controller c, IPlayable target) => target == null || target is Minion m
            && m.Controller == c && m.Zone == c.BoardZone && !m.IsDormant && !m.Untouchable && !m.ToBeDestroyed;

        public static Card Compose(Card body, Card first, Card second)
        {
            if (body.Id != "TLC_100t1" && body.Id != "TLC_100t2" && body.Id != "TLC_100t3")
                throw new ArgumentException("Unknown composite location base.");
            var allowed = Components(Tier(body));
            if (first == null || second == null || first.Id == second.Id || !allowed.Contains(first.Id) || !allowed.Contains(second.Id))
                throw new ArgumentException("Composite location requires two distinct matching components.");
            var result = body.Clone();
            result.Tags[GameTag.MODULAR_ENTITY_PART_1] = first.AssetId;
            result.Tags[GameTag.MODULAR_ENTITY_PART_2] = second.AssetId;
            result.Tags[GameTag.DEATHRATTLE] = Kind(first) == 1 || Kind(second) == 1 ? 1 : 0;
            result.Text = first.Text + " " + second.Text;
            var kinds = new[] { Kind(first), Kind(second) };
            result.Power = new Power {
                LocationActivation = new LocationActivation {
                    Task = body.Power.LocationActivation.Task,
                    Target = kinds.Contains(7) ? LocationTargetKind.Minion : LocationTargetKind.None,
                    TargetCondition = body.Power.LocationActivation.TargetCondition,
                    RequiresFreeSlot = kinds.Contains(5) || kinds.Contains(7)
                },
                DeathrattleTask = body.Power.DeathrattleTask
            };
            return result;
        }

        internal static void ApplyNextSpell(Spell spell)
        {
            var c = spell.Controller;
            foreach (var effect in c.AppliedEnchantments?.Where(e => e.Card.Id == "TLC_100t12e"
                || e.Card.Id == "TLC_100t22e" || e.Card.Id == "TLC_100t32e").ToArray() ?? Array.Empty<Enchantment>())
            {
                effect.Remove();
                Generic.AddEnchantmentBlock(spell.Game, effect.Card, effect, spell);
            }
        }

        public static void ConfigureBase(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power {
                LocationActivation = new LocationActivation {
                    TargetCondition = FriendlyTarget,
                    Task = StateTaskList.Suspendable("navigator/location/" + card.Id + "/v1",
                        new DiscoverThenContinueTask(FirstOffers, DiscoverRealRules.PrepareDiscover),
                        new CustomTask(First),
                        new DiscoverThenContinueTask(SecondOffers, DiscoverRealRules.PrepareDiscover),
                        new CustomTask(Second))
                },
                DeathrattleTask = new CustomTask(Death)
            };
        }
        private static Card[] FirstOffers(Game g, Controller c, IEntity s, TaskStack stack) => Offers(g, c, s, GameTag.MODULAR_ENTITY_PART_1);
        private static Card[] SecondOffers(Game g, Controller c, IEntity s, TaskStack stack) => Offers(g, c, s, GameTag.MODULAR_ENTITY_PART_2);
        private static Card[] Offers(Game g, Controller c, IEntity s, GameTag tag) => Kind(Part(s, tag)) != 3
            ? Array.Empty<Card>() : AssembleRules.Take(g, OfflineCardPools.Candidates(g, c.HeroClass, Spells, 117762, PoolDestination.Other, OfflineCardPools.PrintedClassMask(s.Card)).ToList(), 3);
        private static void First(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack) => Execute(g, c, s, t, stack, Part(s, GameTag.MODULAR_ENTITY_PART_1));
        private static void Second(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack) => Execute(g, c, s, t, stack, Part(s, GameTag.MODULAR_ENTITY_PART_2));
        private static void Execute(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack, Card part)
        {
            int tier = Tier(s.Card), scale = tier == 1 ? 1 : tier == 2 ? 2 : 4;
            switch (Kind(part))
            {
                case 2:
                    Enchantment.GetInstance(c, (IPlayable)s, c, Cards.FromId(part.Id + "e"));
                    break;
                case 3:
                    var chosen = stack.Playables?.LastOrDefault();
                    if (chosen == null) break;
                    CostRules.AddModifier((Playable)chosen, EffectOperator.SUB, tier == 1 ? 1 : tier == 2 ? 4 : 7);
                    Generic.AddHandPhase(c, c.SetasideZone.Remove(chosen));
                    break;
                case 4:
                    foreach (var character in c.BoardZone.GetAll().Cast<IPlayable>().Append(c.Hero).ToArray())
                        Generic.AddEnchantmentBlock(g, Cards.FromId(part.Id + "e2"), (IPlayable)s, character);
                    break;
                case 5:
                    int position = SabberStoneCore.EffectSystem.EffectContext.DeathrattlePosition(c, s);
                    for (int i = 0; i < scale && !c.BoardZone.IsFull; i++)
                        new SummonTask(SummonSide.NUMBER, Cards.FromId("TLC_101t")).Process(g, c, s, null, new TaskStack { Number = position + i + 1 });
                    break;
                case 6: c.Hero.Armor += scale * 3; break;
                case 7:
                    if (t == null || !FriendlyTarget(c, t) || c.BoardZone.IsFull) break;
                    Generic.Copy(c, s, t, Zone.PLAY, preserveBoardMinionState: true,
                        initialize: copy => Generic.AddEnchantmentBlock(copy.Game, Cards.FromId(part.Id + "e"), (IPlayable)s, copy));
                    break;
            }
        }
        private static void Death(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack)
        {
            foreach (var part in new[] { Part(s, GameTag.MODULAR_ENTITY_PART_1), Part(s, GameTag.MODULAR_ENTITY_PART_2) })
                if (Kind(part) == 1)
                {
                    var enemies = c.Opponent.BoardZone.GetAll().Cast<ICharacter>().Append(c.Opponent.Hero).ToArray();
                    using var wave = g.History ? g.PowerHistory.DamageWave(enemies.Length) : null;
                    foreach (var enemy in enemies)
                        Generic.DamageCharFunc((IPlayable)s, enemy, Tier(s.Card) * 2 - 1, false);
                }
        }
    }
}
