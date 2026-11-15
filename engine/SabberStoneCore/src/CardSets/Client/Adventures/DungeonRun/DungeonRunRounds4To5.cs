using System;
using System.Collections.Generic;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Adventures.DungeonRun
{
    // Dungeon Run round 4-5 bosses (https://hearthstone.wiki.gg/wiki/Dungeon_Run), with the Rank 2 heroes of the three that
    // come back in rounds 6-7. Not yet here: A. F. Kay (her AI plays nothing before turn 6), Mushhuckster Max (crafts a
    // potion), Whompwhisker (Mogor's Champion needs Forgetful).
    internal static class DungeonRunRounds4To5
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Battlecrier Jin'zo. Battle Totem (passive): All Battlecries trigger twice (both players, as written).
            register(46338, "LOOTA_BOSS_17h", DungeonRunCards.Plain);
            register(46174, "LOOTA_BOSS_17p", card => PassiveAura(card, GameTag.EXTRA_BATTLECRIES_BASE));

            // Spiritspeaker Azun. Totem of the Dead (passive): All Deathrattles trigger twice (both players).
            register(46337, "LOOTA_BOSS_16h", DungeonRunCards.Plain);
            register(46172, "LOOTA_BOSS_16p", card => PassiveAura(card, GameTag.EXTRA_DEATHRATTLES_BASE));

            // Blackseed. Evolvomancy (0): Transform a minion into a random one that costs (1) more. Rank 2, Greater Evolution
            // (1): (3) more.
            register(46279, "LOOTA_BOSS_06h", DungeonRunCards.Plain);
            register(46153, "LOOTA_BOSS_06p", card => Targeted(card, new TransformMinionTask(EntityType.TARGET, 1), PlayReq.REQ_MINION_TARGET));
            register(46281, "LOOTA_BOSS_29h", DungeonRunCards.Plain);
            register(46282, "LOOTA_BOSS_29p", card => Targeted(card, new TransformMinionTask(EntityType.TARGET, 3), PlayReq.REQ_MINION_TARGET));

            // Brimstone Warden: starts with four Stone Golems in play (the content file's starting board). Dispel Ward (5):
            // Silence your minions (waking the Golems: no more "Can't attack", no Deathrattle). The engine's Silence keeps a
            // card's own "Can't attack", so the power lifts it itself.
            register(46339, "LOOTA_BOSS_20h", DungeonRunCards.Plain);
            register(46208, "LOOTA_BOSS_20p", card => {
                card.PlayAvailabilityPredicate = (c, _) => !c.BoardZone.IsEmpty;
                Untargeted(card, ComplexTask.Create(new SilenceTask(EntityType.MINIONS), Wake()));
            });
            // Stone Golem: 15/5, Can't attack (facts tags). Deathrattle: Lose a Mana Crystal.
            register(46209, "LOOTA_BOSS_20t", card => {
                card.Power = new Power { DeathrattleTask = LoseManaCrystal() };
                DungeonRunCards.Plain(card);
            });
            // Looming Presence (League of Explorers adventure; in the Warden's deck): Draw 2 cards. Gain 4 Armor.
            register(37345, "LOEA_01", card => {
                card.Power = new Power { PowerTask = ComplexTask.Create(new EnqueueTask(2, new DrawTask()), new ArmorTask(4)) };
                DungeonRunCards.Plain(card);
            });

            // Elder Jari. Mystic Barrier (1): Gain 3 Armor.
            register(47305, "LOOTA_BOSS_42h", DungeonRunCards.Plain);
            register(47304, "LOOTA_BOSS_42p", card => Untargeted(card, new ArmorTask(3)));

            // Fungalmancer Flurgl. Fungal Infection (2): Give your minions +1/+1.
            register(47320, "LOOTA_BOSS_46h", DungeonRunCards.Plain);
            register(47319, "LOOTA_BOSS_46p", card => Untargeted(card, new AddEnchantmentTask("LOOTA_BOSS_46pe", EntityType.MINIONS)));
            register(47317, "LOOTA_BOSS_46pe", card => Enchantment(card, Effects.AttackHealth_N(1)));

            // Gutmook. Digest Magic (passive): Whenever your opponent casts a spell, summon a Tunnel Trogg (before the spell
            // resolves). Rank 2, Metabolized Magic (passive): draw a card instead; it costs (1).
            register(46329, "LOOTA_BOSS_19h", DungeonRunCards.Plain);
            register(46205, "LOOTA_BOSS_19p", card => PassiveTrigger(card, new SummonTask("LOE_018")));
            register(46330, "LOOTA_BOSS_33h", DungeonRunCards.Plain);
            register(46331, "LOOTA_BOSS_33p", card => PassiveTrigger(card, ComplexTask.Create(
                new DrawTask(true),
                new SetGameTagTask(GameTag.COST, 1, EntityType.STACK))));
            // Gluttonous Trogg (Gutmook's deck): Whenever your opponent casts a spell, gain +1/+1.
            register(46918, "LOOTA_109", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.CAST_SPELL) {
                        TriggerSource = TriggerSource.ENEMY,
                        SingleTask = new AddEnchantmentTask("LOOTA_109e", EntityType.SOURCE)
                    }
                };
                DungeonRunCards.Plain(card);
            });
            register(46917, "LOOTA_109e", card => Enchantment(card, Effects.AttackHealth_N(1)));

            // Kraxx. Giant Stomp (2): Deal 1 damage to all enemy minions.
            register(47258, "LOOTA_BOSS_39h", DungeonRunCards.Plain);
            register(47257, "LOOTA_BOSS_39p", card => Untargeted(card, new DamageTask(1, EntityType.OP_MINIONS)));

            // Lyris the Wild Mage. Arcane Infusion (1): Add 'Arcane Missiles' to your hand.
            register(46371, "LOOTA_BOSS_37h", DungeonRunCards.Plain);
            register(46368, "LOOTA_BOSS_37p", card => Untargeted(card, new AddCardTo("EX1_277", EntityType.HAND)));

            // Russell the Bard. Alluring Tune (2): Gain control of an enemy minion with 2 or less Attack.
            register(47365, "LOOTA_BOSS_51h", DungeonRunCards.Plain);
            register(47363, "LOOTA_BOSS_51p", card => Targeted(card, new ControlTask(EntityType.TARGET), PlayReq.REQ_MINION_TARGET,
                PlayReq.REQ_ENEMY_TARGET, PlayReq.REQ_TARGET_MAX_ATTACK, PlayReq.REQ_NUM_MINION_SLOTS));

            // Thaddock the Thief. Tactical Retreat (1): Return a friendly minion to your hand.
            register(46321, "LOOTA_BOSS_13h", DungeonRunCards.Plain);
            register(46167, "LOOTA_BOSS_13p", card => Targeted(card, new ReturnHandTask(EntityType.TARGET),
                PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET));

            // Waxmancer Sturmi. Sculpt Wax (1): Summon a 1/1 copy of a minion (either side's). Rank 2, Harden Sculpture (3):
            // Summon a copy of a minion.
            register(46264, "LOOTA_BOSS_04h", DungeonRunCards.Plain);
            register(46149, "LOOTA_BOSS_04p", card => Copy(card, ComplexTask.Create(
                new SummonCopyTask(EntityType.TARGET, addToStack: true),
                new AddEnchantmentTask("LOOTA_BOSS_04e", EntityType.STACK))));
            register(48839, "LOOTA_BOSS_04e", card => Enchantment(card, Effects.SetAttackHealth(1)));
            register(46266, "LOOTA_BOSS_27h", DungeonRunCards.Plain);
            register(46267, "LOOTA_BOSS_27p", card => Copy(card, new SummonCopyTask(EntityType.TARGET)));
        }

        private static ISimpleTask Wake() => new CustomTask((game, controller, source, target, stack) => {
            foreach (var minion in controller.BoardZone)
                if (minion.Card.CantAttack && minion.CantAttack) minion.CantAttack = false;
        });

        // One fewer Mana Crystal, a spent (empty) one first, as the engine's Felguard does; unlike its ManaCrystalEmptyTask,
        // never a negative spent count when none is spent.
        private static ISimpleTask LoseManaCrystal() => new CustomTask((game, controller, source, target, stack) => {
            if (controller.BaseMana == 0) return;
            Actions.Generic.ChangeManaCrystal(controller, -1, true);
            if (controller.UsedMana > 0) controller.UsedMana--;
        });

        private static void Untargeted(Card card, ISimpleTask task)
        {
            card.Power = new Power { PowerTask = task };
            DungeonRunCards.Plain(card);
        }

        private static void Targeted(Card card, ISimpleTask task, params PlayReq[] requirements)
        {
            var reqs = new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } };
            // Valued requirements: one free board slot; Alluring Tune's 2 or less Attack.
            foreach (var req in requirements)
                reqs[req] = req == PlayReq.REQ_NUM_MINION_SLOTS ? 1 : req == PlayReq.REQ_TARGET_MAX_ATTACK ? 2 : 0;
            card.SetPlayRequirements(reqs);
            card.Power = new Power { PowerTask = task };
            DungeonRunCards.Plain(card);
        }

        private static void Copy(Card card, ISimpleTask task) =>
            Targeted(card, task, PlayReq.REQ_MINION_TARGET, PlayReq.REQ_NUM_MINION_SLOTS);

        private static void PassiveAura(Card card, GameTag tag)
        {
            Actions.PassiveHeroPowerRules.Mark(card);
            card.Power = new Power { Aura = new Aura(AuraType.CONTROLLERS, new Effect(tag, EffectOperator.SET, 1)) };
            DungeonRunCards.Plain(card);
        }

        // Whenever the opponent casts a spell (CAST_SPELL: before it resolves).
        private static void PassiveTrigger(Card card, ISimpleTask task)
        {
            Actions.PassiveHeroPowerRules.Mark(card);
            card.Power = new Power {
                Trigger = new Trigger(TriggerType.CAST_SPELL) { TriggerSource = TriggerSource.ENEMY, SingleTask = task }
            };
            DungeonRunCards.Plain(card);
        }

        private static void Enchantment(Card card, params IEffect[] effects)
        {
            card.Power = new Power { Enchant = new Enchant(effects) };
            DungeonRunCards.Plain(card);
        }
    }
}
