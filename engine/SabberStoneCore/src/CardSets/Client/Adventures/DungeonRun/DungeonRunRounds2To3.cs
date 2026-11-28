using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Adventures.DungeonRun
{
    // Dungeon Run round 2-3 bosses (https://hearthstone.wiki.gg/wiki/Dungeon_Run), with the Rank 2 heroes of the three
    // that come back in rounds 6-7 (a separate hero card each, 60 Health). Names, Health, costs and text come from the
    // facts file; these modules add the behaviour.
    internal static class DungeonRunRounds2To3
    {
        private const string Counterspell = "EX1_287";

        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Candlebeard. Charge! (1): Give a minion Charge. Rank 2, Charrrrrge! (passive): After you play a minion, give it
            // Charge.
            register(46317, "LOOTA_BOSS_12h", DungeonRunCards.Plain);
            register(46165, "LOOTA_BOSS_12p", card => Targeted(card, new AddEnchantmentTask("LOOTA_BOSS_12e", EntityType.TARGET),
                PlayReq.REQ_MINION_TARGET));
            register(46470, "LOOTA_BOSS_12e", card => Enchantment(card, Effects.Charge));
            register(46318, "LOOTA_BOSS_31h", DungeonRunCards.Plain);
            register(46319, "LOOTA_BOSS_31p", card => {
                Actions.PassiveHeroPowerRules.Mark(card);
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.AFTER_PLAY_MINION) {
                        TriggerSource = TriggerSource.FRIENDLY,
                        SingleTask = new AddEnchantmentTask("LOOTA_BOSS_12e", EntityType.TARGET)
                    }
                };
                DungeonRunCards.Plain(card);
            });

            // Elder Brandlemar. Dampen Magic (2): Put a 'Counterspell' Secret into the battlefield. Like any Secret, not while
            // one is already up or the Secret zone is full.
            register(46357, "LOOTA_BOSS_35h", DungeonRunCards.Plain);
            register(46358, "LOOTA_BOSS_35p", card => {
                card.PlayAvailabilityPredicate = (c, _) => !c.SecretZone.IsFull && !c.SecretZone.Any(s => s.Card.IsSameCardAs(Cards.FromId(Counterspell)));
                card.Power = new Power { PowerTask = PutSecret(Counterspell) };
                DungeonRunCards.Plain(card);
            });

            // Frostfur. Freeze (2): Freeze a minion.
            register(46915, "LOOTA_BOSS_09h", DungeonRunCards.Plain);
            register(46159, "LOOTA_BOSS_09p", card => Targeted(card, ComplexTask.Freeze(EntityType.TARGET), PlayReq.REQ_MINION_TARGET));

            // Graves the Cleric. Light's Will (0): Restore 2 Health to all minions.
            register(46310, "LOOTA_BOSS_10h", DungeonRunCards.Plain);
            register(46161, "LOOTA_BOSS_10p", card => {
                card.Power = new Power { PowerTask = new HealTask(2, EntityType.ALLMINIONS) };
                DungeonRunCards.Plain(card);
            });

            // Overseer Mogark. Cruel Words (1): Deal 1 damage to a friendly minion and give it +2 Attack. Rank 2, Searing Lash
            // (1): the same with +5 Attack.
            register(46311, "LOOTA_BOSS_11h", DungeonRunCards.Plain);
            register(46163, "LOOTA_BOSS_11p", card => Whip(card, "LOOTA_BOSS_11e"));
            register(46316, "LOOTA_BOSS_11e", card => Enchantment(card, Effects.Attack_N(2)));
            register(46312, "LOOTA_BOSS_30h", DungeonRunCards.Plain);
            register(46314, "LOOTA_BOSS_30p", card => Whip(card, "LOOTA_BOSS_30e"));
            register(46313, "LOOTA_BOSS_30e", card => Enchantment(card, Effects.Attack_N(5)));

            // Pathmaker Hamm (autocast). Unstable Explosion (1): Deal 1 damage to two random enemies. Rank 2, Unstable
            // Demolition (2): 2 damage to three. Each enemy is hit at most once.
            register(46269, "LOOTA_BOSS_05h", DungeonRunCards.Plain);
            register(46151, "LOOTA_BOSS_05p", card => Explosion(card, 2, 1));
            register(46270, "LOOTA_BOSS_28h", DungeonRunCards.Plain);
            register(46271, "LOOTA_BOSS_28p", card => Explosion(card, 3, 2));

            // Seriona. Fading Light (2): Give a minion -1 Attack.
            register(47261, "LOOTA_BOSS_40h", DungeonRunCards.Plain);
            register(47260, "LOOTA_BOSS_40p", card => Targeted(card, new AddEnchantmentTask("LOOTA_BOSS_40pe", EntityType.TARGET),
                PlayReq.REQ_MINION_TARGET));
            register(47259, "LOOTA_BOSS_40pe", card => Enchantment(card, Effects.Attack_N(-1)));
        }

        private static void Targeted(Card card, ISimpleTask task, params PlayReq[] requirements)
        {
            var reqs = new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 } };
            foreach (var req in requirements) reqs[req] = 0;
            card.SetPlayRequirements(reqs);
            card.Power = new Power { PowerTask = task };
            DungeonRunCards.Plain(card);
        }

        private static void Whip(Card card, string enchantment) => Targeted(card, ComplexTask.Create(
                new DamageTask(1, EntityType.TARGET),
                new AddEnchantmentTask(enchantment, EntityType.TARGET)),
            PlayReq.REQ_MINION_TARGET, PlayReq.REQ_FRIENDLY_TARGET);

        private static void Explosion(Card card, int enemies, int damage)
        {
            card.Power = new Power { PowerTask = ComplexTask.Create(
                new RandomTask(enemies, EntityType.ENEMIES),
                new DamageTask(damage, EntityType.STACK)) };
            DungeonRunCards.Plain(card);
        }

        private static void Enchantment(Card card, params IEffect[] effects)
        {
            card.Power = new Power { Enchant = new Enchant(effects) };
            DungeonRunCards.Plain(card);
        }

        // A Secret created in play, as if cast: live on the opponent's turn (CastSpell's rule for a Secret).
        private static ISimpleTask PutSecret(string id) => new CustomTask((game, controller, source, target, stack) => {
            Card card = Cards.FromId(id);
            if (controller.SecretZone.IsFull || controller.SecretZone.Any(s => s.Card.IsSameCardAs(card))) return;
            var secret = (Spell)Entity.FromCard(controller, card);
            secret.Power.Trigger?.Activate(secret);
            controller.SecretZone.Add(secret);
            if (controller == game.CurrentPlayer) secret.IsExhausted = true;
        });
    }
}
