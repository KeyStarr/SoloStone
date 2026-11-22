using System;
using System.Linq;
using SabberStoneCore.Conditions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Adventures
{
    // Nefarian Rises! (co-op Tavern Brawl, TB_CoOpv3_* / TB_Coopv3_*; https://hearthstone.wiki.gg/wiki/Nefarian_Rises!). The two
    // players are a team (GameConfig.Coop, Actions.CoopRules) against Nefarian, a boss minion that switches sides every turn
    // (NefarianRises/NefarianBoss.cs). This file holds the brawl's player cards; none is collectible and all stay out of the
    // legacy random pools.
    public static class NefarianRisesCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            NefarianRises.NefarianBoss.Apply(register);

            // Main Tank: Battlecry: Give all other minions +2/+2, except the Boss (both sides: the other side is the teammate's).
            Action<Card> mainTank = card => {
                card.Power = new Power {
                    PowerTask = new CustomTask((game, controller, source, target, stack) => {
                        var ctx = new EffectContext(game, controller.Id, source.Id);
                        foreach (var minion in game.Player1.BoardZone.Interactable.Concat(game.Player2.BoardZone.Interactable).ToList())
                            if (minion != source && !NefarianRises.NefarianBoss.IsBoss(minion)) ctx.GiveBuff(minion.Id, Unity);
                    })
                };
                Plain(card);
            };
            register(40520, "TB_Coopv3_104", mainTank);
            register(41653, "TB_Coopv3_104_NewClasses", mainTank);
            register(40519, Unity, card => Enchantment(card, Effects.AttackHealth_N(2)));

            // Raid Healer: Whenever your hero is healed, also heal your teammate for that much.
            register(40544, "TB_Coopv3_105", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.HEAL) {
                        TriggerSource = TriggerSource.HERO,
                        // The echo it gives the teammate is healing from this minion: never answered again (no ping-pong
                        // between two Raid Healers after a Bamboozle).
                        Condition = new SelfCondition(p => p.Game.CurrentEventData?.EventNumber > 0
                            && p.Game.CurrentEventData.EventSource?.Card.Id != "TB_Coopv3_105"),
                        SingleTask = new CustomTask((game, controller, source, target, stack) => {
                            int amount = game.CurrentEventData?.EventNumber ?? 0;
                            if (amount > 0 && controller.Opponent.Hero != null)
                                new EffectContext(game, controller.Id, source.Id).Heal(controller.Opponent.Hero.Id, amount);
                        })
                    }
                };
                Plain(card);
            });

            // Dragonscale Warrior: Whenever any player targets this minion with a spell, that player draws a card.
            register(40377, "TB_Coopv3_100", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.TARGET) {
                        SingleTask = new CustomTask((game, controller, source, target, stack) => {
                            if (!(target is Spell spell) || game.CurrentEventData?.EventTarget != source) return;
                            new DrawTask().Process(game, spell.Controller, source, null);
                        })
                    }
                };
                Plain(card);
            });

            // Freewheeling Skulker: At the end of your turn, switch sides. On the teammate's side it is a Team Player (Immune while
            // attacking), so it can keep hitting Nefarian.
            register(40380, "TB_Coopv3_101", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.TURN_END) {
                        SingleTask = new CustomTask((game, controller, source, target, stack) => {
                            if (!(source is Minion skulker) || skulker.Zone?.Type != Zone.PLAY || skulker.IsDead) return;
                            new ControlTask(EntityType.SOURCE, opposite: true).Process(game, controller, source, null);
                            if (skulker.Zone?.Type == Zone.PLAY && skulker.AppliedEnchantments?.Any(e => e.Card.Id == TeamPlayer) != true)
                                new EffectContext(game, skulker.Controller.Id, source.Id).GiveBuff(skulker.Id, TeamPlayer);
                        })
                    }
                };
                Plain(card);
            });
            register(40524, TeamPlayer, card => Enchantment(card, new Effect(GameTag.IMMUNE_WHILE_ATTACKING, EffectOperator.SET, 1)));

            // Intrepid Dragonstalker: Whenever ANY player plays a card, gain +1/+1 (Questing Adventurer's Level Up!).
            register(40387, "TB_Coopv3_103", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.AFTER_PLAY_CARD) {
                        TriggerSource = TriggerSource.ALL,
                        EitherTurn = true,
                        Condition = new SelfCondition(p => p.Game.CurrentEventData?.EventSource != null),
                        SingleTask = new AddEnchantmentTask("EX1_044e", EntityType.SOURCE)
                    }
                };
                Plain(card);
            });
        }

        internal const string Unity = "TB_CoOpv3_104e", TeamPlayer = "TB_CoOpv3_101e";

        internal static void Plain(Card card)
        {
            card.Implemented = true;
            card.ExcludeFromLegacyRandomPools = true;
        }

        internal static void Enchantment(Card card, params IEffect[] effects)
        {
            card.Power = new Power { Enchant = new Enchant(effects) };
            Plain(card);
        }
    }
}
