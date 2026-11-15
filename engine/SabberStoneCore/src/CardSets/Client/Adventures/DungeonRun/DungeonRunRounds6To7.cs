using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Adventures.DungeonRun
{
    // Dungeon Run round 6-7 bosses (https://hearthstone.wiki.gg/wiki/Dungeon_Run). Not yet here: George and Karl (two heroes
    // that swap), Tad (no hand; a chest on turn 11), Treasure Vault (a board of Treasure Coffers that add treasures).
    // Chronomancer Inara keeps her portrait during the extra turn (retail shows "Inara the Mage", LOOTA_BOSS_53h2).
    internal static class DungeonRunRounds6To7
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Bristlesnarl. Hunter's Call (3): Reduce the Cost of cards in your hand by (1). The boss set has no enchantment
            // of its own for it; Imperial Favor (BRM_028e, "Costs (1) less.") is the same effect.
            register(46341, "LOOTA_BOSS_23h", DungeonRunCards.Plain);
            register(46342, "LOOTA_BOSS_23p", card => {
                card.PlayAvailabilityPredicate = (c, _) => !c.HandZone.IsEmpty;
                Untargeted(card, new AddEnchantmentTask("BRM_028e", EntityType.HAND));
            });

            // Chronomancer Inara. Stolen Time (10): Destroy 3 of your Mana Crystals. Take an extra turn.
            register(47475, "LOOTA_BOSS_53h", DungeonRunCards.Plain);
            register(47474, "LOOTA_BOSS_53p", card => Untargeted(card, ComplexTask.Create(
                new ManaCrystalEmptyTask(-3),
                new ExtraTurnEffectTask())));

            // Inara's deck: Medivh's Karazhan cards and the heroic Looming Presence.
            // Archmage's Insight: Your spells cost (0) this turn.
            register(39759, "KARA_00_05", card => {
                card.Power = new Power { PowerTask = new AddEnchantmentTask("KARA_00_05e", EntityType.CONTROLLER) };
                DungeonRunCards.Plain(card);
            });
            register(39758, "KARA_00_05e", card => {
                card.Power = new Power {
                    Aura = new Aura(AuraType.HAND, Effects.SetCost(0)) {
                        Condition = SelfCondition.IsSpell,
                        RemoveTrigger = (TriggerType.TURN_END, null)
                    }
                };
                DungeonRunCards.Plain(card);
            });
            // Astral Portal: Summon a random Legendary minion.
            register(39762, "KARA_00_07", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
                card.Power = new Power { PowerTask = ComplexTask.Create(
                    new RandomMinionTask(GameTag.RARITY, (int)Rarity.LEGENDARY),
                    new SummonTask()) };
                DungeonRunCards.Plain(card);
            });
            // Archmage's Apprentice: Whenever you cast a spell, shuffle a copy of it into your deck.
            register(39763, "KARA_00_08", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.CAST_SPELL) {
                        TriggerSource = TriggerSource.FRIENDLY,
                        SingleTask = new CopyTask(EntityType.TARGET, Zone.DECK)
                    }
                };
                DungeonRunCards.Plain(card);
            });
            // Looming Presence (heroic): Draw 3 cards. Gain 6 Armor.
            register(37346, "LOEA_01H", card => {
                card.Power = new Power { PowerTask = ComplexTask.Create(new EnqueueTask(3, new DrawTask()), new ArmorTask(6)) };
                DungeonRunCards.Plain(card);
            });

            // Gnosh the Greatworm. Swallow Whole (2): Destroy the minion with the highest Attack (either side; a random one
            // of a tie). Swallowing a Poisonous minion kills Gnosh.
            register(46243, "LOOTA_BOSS_21h", DungeonRunCards.Plain);
            register(46242, "LOOTA_BOSS_21p", card => {
                card.PlayAvailabilityPredicate = (c, _) => !c.BoardZone.IsEmpty || !c.Opponent.BoardZone.IsEmpty;
                Untargeted(card, SwallowWhole());
            });

            // Ixlid. Sprouting Spore (2): Summon an extremely Deadly Spore (Poisonous, Poisonous to heroes).
            register(46361, "LOOTA_BOSS_36h", DungeonRunCards.Plain);
            register(46363, "LOOTA_BOSS_36p", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
                Untargeted(card, new SummonTask("LOOTA_105"));
            });
            register(46362, "LOOTA_105", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.DEAL_DAMAGE) {
                        TriggerSource = TriggerSource.SELF,
                        Condition = SelfCondition.IsEventTargetIs(CardType.HERO),
                        SingleTask = new DestroyTask(EntityType.EVENT_TARGET)
                    }
                };
                DungeonRunCards.Plain(card);
            });

            // Jeeru. Three Wee Wishes (1): Each player draws 3 cards.
            register(47307, "LOOTA_BOSS_43h", DungeonRunCards.Plain);
            register(47306, "LOOTA_BOSS_43p", card => Untargeted(card, ComplexTask.Create(
                new EnqueueTask(3, new DrawTask()),
                new EnqueueTask(3, new DrawOpTask()))));

            // Lava-Filled Chamber. The Floor is Lava (passive): After a minion is played (either side), deal 2 damage to it.
            register(47322, "LOOTA_BOSS_47h", DungeonRunCards.Plain);
            register(47321, "LOOTA_BOSS_47p", card => PassiveTrigger(card, TriggerType.AFTER_PLAY_MINION, TriggerSource.ALL_MINIONS,
                new DamageTask(2, EntityType.TARGET)));

            // The Mothergloop. Gloop (2): Give minions in your hand +1/+1.
            register(46344, "LOOTA_BOSS_24h", DungeonRunCards.Plain);
            register(46346, "LOOTA_BOSS_24p", card => {
                card.PlayAvailabilityPredicate = (c, _) => c.HandZone.Any(p => p is Minion);
                Untargeted(card, ComplexTask.Create(
                    new IncludeTask(EntityType.HAND),
                    new FilterStackTask(SelfCondition.IsMinion),
                    new AddEnchantmentTask("LOOTA_BOSS_24e", EntityType.STACK)));
            });
            register(46345, "LOOTA_BOSS_24e", card => Enchantment(card, Effects.AttackHealth_N(1)));

            // The Mothergloop's oozes, and the Shroomsayer (also Trapped Room's and Mushhuckster Max's).
            // Growing Ooze: At the start of your turn, gain +1/+1.
            register(42872, "ICCA07_004", card => {
                card.Power = new Power { Trigger = new Trigger(TriggerType.TURN_START) {
                    SingleTask = new AddEnchantmentTask("ICCA07_004e", EntityType.SOURCE) } };
                DungeonRunCards.Plain(card);
            });
            register(43073, "ICCA07_004e", card => Enchantment(card, Effects.AttackHealth_N(1)));
            // Reverberating Ooze: After this minion survives damage, summon a copy of this minion.
            register(46348, "LOOTA_104", card => SurvivesDamage(card, new CopyTask(EntityType.SOURCE, Zone.PLAY)));
            // Shroomsayer: After this minion survives damage, destroy all minions.
            register(46375, "LOOTA_107", card => SurvivesDamage(card, new DestroyTask(EntityType.ALLMINIONS)));

            // Trapped Room. Alarm (passive): Whenever you reveal a Secret, summon a 3/3 Sawblade.
            register(47328, "LOOTA_BOSS_48h", DungeonRunCards.Plain);
            register(47327, "LOOTA_BOSS_48p", card => PassiveTrigger(card, TriggerType.SECRET_REVEALED, TriggerSource.FRIENDLY,
                new SummonTask("LOOTA_BOSS_48t")));
            register(47326, "LOOTA_BOSS_48t", DungeonRunCards.Plain);

            // Voodoomaster Vex. Totem of Chaos (passive): All Battlecries and Deathrattles trigger twice (both players).
            register(47001, "LOOTA_BOSS_38h", DungeonRunCards.Plain);
            register(47000, "LOOTA_BOSS_38p", card => {
                Actions.PassiveHeroPowerRules.Mark(card);
                card.Power = new Power { Aura = new Aura(AuraType.CONTROLLERS,
                    new Effect(GameTag.EXTRA_BATTLECRIES_BASE, EffectOperator.SET, 1),
                    new Effect(GameTag.EXTRA_DEATHRATTLES_BASE, EffectOperator.SET, 1)) };
                DungeonRunCards.Plain(card);
            });
        }

        private static ISimpleTask SwallowWhole() => new CustomTask((game, controller, source, target, stack) => {
            var minions = controller.BoardZone.Concat(controller.Opponent.BoardZone).Where(m => !m.ToBeDestroyed).ToList();
            if (minions.Count == 0) return;
            int top = minions.Max(m => m.AttackDamage);
            var tied = minions.Where(m => m.AttackDamage == top).ToList();
            Minion swallowed = tied.Count == 1 ? tied[0] : tied[game.Random.Next(tied.Count)];
            bool poisonous = swallowed.Poisonous;
            swallowed.Destroy();
            if (poisonous) controller.Hero.Destroy();
        });

        private static void SurvivesDamage(Card card, ISimpleTask task)
        {
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.TAKE_DAMAGE) {
                    TriggerSource = TriggerSource.SELF, Condition = SelfCondition.IsNotDead, SingleTask = task
                }
            };
            DungeonRunCards.Plain(card);
        }

        private static void Untargeted(Card card, ISimpleTask task)
        {
            card.Power = new Power { PowerTask = task };
            DungeonRunCards.Plain(card);
        }

        private static void PassiveTrigger(Card card, TriggerType type, TriggerSource source, ISimpleTask task)
        {
            Actions.PassiveHeroPowerRules.Mark(card);
            card.Power = new Power { Trigger = new Trigger(type) { TriggerSource = source, SingleTask = task } };
            DungeonRunCards.Plain(card);
        }

        private static void Enchantment(Card card, params IEffect[] effects)
        {
            card.Power = new Power { Enchant = new Enchant(effects) };
            DungeonRunCards.Plain(card);
        }
    }
}
