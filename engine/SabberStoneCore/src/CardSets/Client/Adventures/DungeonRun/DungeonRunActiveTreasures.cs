using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
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
    // Dungeon Run's active treasures (https://hearthstone.wiki.gg/wiki/Dungeon_Run): cards a run adds to its deck after rounds
    // 3 and 7. Names, costs, stats and keywords come from the facts file; these modules add the behaviour.
    internal static class DungeonRunActiveTreasures
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Bag of Coins (0): Fill your hand with Coins.
            register(47037, "LOOTA_836", card => Spell(card, new CustomTask((game, controller, source, target, stack) => {
                while (!controller.HandZone.IsFull)
                    Generic.AddHandPhase(controller, Entity.FromCard(controller, Cards.FromId("GAME_005")));
            })));

            // Bag of Stuffing (1): Draw cards until your hand is full (an empty deck stops it).
            register(46426, "LOOTA_823", card => Spell(card, new CustomTask((game, controller, source, target, stack) => {
                while (!controller.HandZone.IsFull && !controller.DeckZone.IsEmpty)
                    Generic.Draw(controller);
            })));

            // Boots of Haste (1): Your minions cost (0) this turn.
            register(46416, "LOOTA_812", card => Spell(card, new AddEnchantmentTask("LOOTA_812e", EntityType.CONTROLLER)));
            register(46415, "LOOTA_812e", card => {
                card.Power = new Power {
                    Aura = new Aura(AuraType.HAND, Effects.SetCost(0)) {
                        Condition = SelfCondition.IsMinion, RemoveTrigger = (TriggerType.TURN_END, null)
                    }
                };
                DungeonRunCards.Plain(card);
            });

            // THE CANDLE (1): Deal 4 damage to all enemy minions. Shuffle this into your deck.
            register(47251, "LOOTA_843", card => Spell(card, ComplexTask.Create(
                new DamageTask(4, EntityType.OP_MINIONS, true),
                new CopyTask(EntityType.SOURCE, Zone.DECK))));

            // Embers of Ragnaros (3): Shoot three fireballs at random enemies that deal 8 damage each.
            register(46437, "LOOTA_827", card => Spell(card, new EnqueueTask(3, ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 8, true))));

            // Orb of Destruction (3): Destroy 2 of your opponent's Mana Crystals and they discard 2 cards (random ones).
            register(46414, "LOOTA_811", card => Spell(card, new CustomTask((game, controller, source, target, stack) => {
                var opponent = controller.Opponent;
                for (int i = 0; i < 2; i++) LoseManaCrystal(opponent);
                for (int i = 0; i < 2 && !opponent.HandZone.IsEmpty; i++)
                    Generic.DiscardBlock(opponent, opponent.HandZone[DungeonRunCards.Pick(game, opponent.HandZone.Count)]);
            })));

            // Wand of Disintegration (3): Silence and destroy all enemy minions.
            register(46413, "LOOTA_806", card => Spell(card, ComplexTask.Create(
                new SilenceTask(EntityType.OP_MINIONS),
                new DestroyTask(EntityType.OP_MINIONS))));

            // Wax Rager (3, 5/1): Deathrattle: Resummon this minion.
            register(47041, "LOOTA_840", card => {
                card.Power = new Power { DeathrattleTask = new SummonTask("LOOTA_840", SummonSide.DEATHRATTLE) };
                DungeonRunCards.Plain(card);
            });

            // Dr. Boom's Boombox (4): Summon 7 'Boom Bots'.
            register(47039, "LOOTA_838", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
                Spell(card, new EnqueueTask(7, new SummonTask("GVG_110t")));
            });

            // Vorpal Dagger (2, 1/4 weapon): Poisonous, Mega-Windfury (facts tag for Poisonous; Mega-Windfury set here).
            register(46424, "LOOTA_821", card => {
                card.Power = new Power();
                card.Tags[GameTag.MEGA_WINDFURY] = 1;
                DungeonRunCards.Plain(card);
            });

            // Greedy Pickaxe (2, 3/2 weapon): After your hero attacks, gain an empty Mana Crystal.
            register(47036, "LOOTA_835", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                        TriggerSource = TriggerSource.HERO, SingleTask = new ManaCrystalEmptyTask(1)
                    }
                };
                DungeonRunCards.Plain(card);
            });

            // Portable Ice Wall (1, 3/15): Taunt, Can't attack, Freeze any character damaged by this minion (facts keywords).
            register(46436, "LOOTA_826", DungeonRunCards.Plain);

            // Party Portal (1, 0/10): Whenever you cast a spell, summon a random minion of the same Cost (Summoning Stone's
            // shape).
            register(46420, "LOOTA_816", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.CAST_SPELL) {
                        TriggerSource = TriggerSource.FRIENDLY,
                        SingleTask = ComplexTask.Create(
                            new GetGameTagTask(GameTag.TAG_LAST_KNOWN_COST_IN_HAND, EntityType.TARGET),
                            new RandomMinionNumberTask(GameTag.COST),
                            new SummonTask())
                    }
                };
                DungeonRunCards.Plain(card);
            });

            // Horn of Cenarius (2): Recruit 3 minions.
            register(47038, "LOOTA_837", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
                Spell(card, new RecruitTask(3));
            });

            // Archmage Staff (0/3 weapon): At the start of your turn, add a random Mage spell to your hand.
            register(46422, "LOOTA_819", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.TURN_START) {
                        SingleTask = ComplexTask.Create(new RandomCardTask(CardType.SPELL, CardClass.MAGE), new AddStackTo(EntityType.HAND))
                    }
                };
                DungeonRunCards.Plain(card);
            });

            // Gloves of Mugging (1): Steal 3 cards from your opponent's hand (random ones, buffs kept).
            register(47022, "LOOTA_834", card => Spell(card, new CustomTask((game, controller, source, target, stack) => {
                var opponent = controller.Opponent;
                for (int i = 0; i < 3 && !opponent.HandZone.IsEmpty; i++)
                {
                    IPlayable stolen = opponent.HandZone[DungeonRunCards.Pick(game, opponent.HandZone.Count)];
                    opponent.HandZone.Remove(stolen);
                    stolen.Controller = controller;
                    stolen[GameTag.CONTROLLER] = controller.PlayerId;
                    Generic.AddHandPhase(controller, stolen);
                }
            })));

            // Quel'Delar: the Blade (1, a 3/3 weapon) and the Hilt (1: Give a minion +3/+3) are offered apart; "If the player
            // has both ... in their deck at the start of a game, they combine into this card which gets added to the player's
            // hand immediately" (wiki, Quel'Delar).
            register(47045, "LOOTA_842a", card => {
                card.Power = new Power();
                StartOfGameRules.Declare(card, ForgeQuelDelar, owner => owner.DeckZone.Any(p => p.Card.Id == Hilt));
                DungeonRunCards.Plain(card);
            });
            register(47046, Hilt, card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
                Spell(card, new AddEnchantmentTask("LOOTA_842be", EntityType.TARGET));
            });
            register(47047, "LOOTA_842be", card => {
                card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(3)) };
                DungeonRunCards.Plain(card);
            });
            // Quel'Delar (1, a 6-Attack weapon): After your hero attacks, deal 6 damage to all enemies.
            register(47044, "LOOTA_842", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.AFTER_ATTACK) {
                        TriggerSource = TriggerSource.HERO, SingleTask = new DamageTask(6, EntityType.ENEMIES)
                    }
                };
                DungeonRunCards.Plain(card);
            });

            // Mask of Mimicry (1): Choose a minion. Minions in your hand become copies of it.
            register(47893, "LOOTA_847", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
                Spell(card, new CustomTask((game, controller, source, target, stack) => {
                    if (target == null) return;
                    foreach (var minion in controller.HandZone.Where(p => p is Minion).ToList())
                        Generic.ChangeEntityBlock(controller, minion, target.Card, true);
                }));
            });

            // Scroll of Confusion (0): Each minion randomly chooses a side of the battlefield (a full side keeps its minions).
            register(47040, "LOOTA_839", card => Spell(card, new CustomTask((game, controller, source, target, stack) => {
                var minions = controller.BoardZone.Concat(controller.Opponent.BoardZone).Where(m => !m.ToBeDestroyed && !m.Untouchable).ToList();
                foreach (var minion in minions)
                {
                    Controller side = DungeonRunCards.Pick(game, 2) == 0 ? controller : controller.Opponent;
                    if (minion.Controller == side || side.BoardZone.IsFull || minion.Zone?.Type != Zone.PLAY) continue;
                    new ControlTask(EntityType.TARGET).Process(game, side, source, minion);
                }
            })));

            // Aleatoric Cube (2): Shuffle both decks together and give half to each player. Your cards cost (2) less (the
            // cards in hand and deck after the deal).
            register(46423, "LOOTA_820", card => Spell(card, new CustomTask((game, controller, source, target, stack) => {
                var opponent = controller.Opponent;
                var pool = controller.DeckZone.Concat(opponent.DeckZone).ToList();
                foreach (var p in pool) p.Zone.Remove(p);
                for (int i = pool.Count - 1; i > 0; i--)
                {
                    int j = DungeonRunCards.Pick(game, i + 1);
                    (pool[i], pool[j]) = (pool[j], pool[i]);
                }
                for (int i = 0; i < pool.Count; i++)
                {
                    Controller owner = i < (pool.Count + 1) / 2 ? controller : opponent;
                    var p = pool[i];
                    if (p.Controller != owner) { p.Controller = owner; p[GameTag.CONTROLLER] = owner.PlayerId; }
                    owner.DeckZone.Add(p);
                }
                Card discount = Cards.FromId("LOOTA_820e");
                foreach (var p in controller.HandZone.Concat(controller.DeckZone).ToList())
                    Generic.AddEnchantmentBlock(game, discount, source as IPlayable, p);
            })));
            register(46928, "LOOTA_820e", card => {
                card.Power = new Power { Enchant = new Enchant(Effects.AddCost(-2)) };
                DungeonRunCards.Plain(card);
            });

            // Rod of Roasting (10): Cast 'Pyroblast' randomly until a hero dies (each at a random legal target).
            register(46425, "LOOTA_822", card => Spell(card, new CustomTask((game, controller, source, target, stack) => {
                for (int i = 0; i < 60 && !controller.Hero.IsDead && !controller.Opponent.Hero.IsDead; i++)
                {
                    var pyroblast = (Spell)Entity.FromCard(controller, Cards.FromId("EX1_279"));
                    ICharacter randomTarget = pyroblast.GetRandomValidTarget();
                    if (randomTarget == null) break;
                    game.OnRandomHappened(true);
                    game.TaskQueue.StartEvent();
                    Generic.CastSpell.Invoke(controller, game, pyroblast, randomTarget, 0);
                    game.DeathProcessingAndAuraUpdate();
                    game.TaskQueue.EndEvent();
                }
            })));

            // Portable Forge (1): Discover a Legendary weapon (of any class) and equip it.
            register(47042, "LOOTA_841", card => Spell(card, new CustomTask((game, controller, source, target, stack) => {
                var pool = Cards.All.Where(c => c.Type == CardType.WEAPON && c.Collectible && c.Rarity == Rarity.LEGENDARY && c.Implemented
                        && !c.ExcludeFromLegacyRandomPools).GroupBy(c => c.Name).Select(g => g.OrderBy(c => c.Id, StringComparer.Ordinal).First())
                    .OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
                var offer = new List<Card>();
                while (offer.Count < 3 && pool.Count > 0)
                {
                    int i = DungeonRunCards.Pick(game, pool.Count);
                    offer.Add(pool[i]); pool.RemoveAt(i);
                }
                if (offer.Count == 0) return;
                Generic.CreateChoiceCards.Invoke(controller, source, null, ChoiceType.GENERAL, ChoiceAction.STACK, offer.ToArray(),
                    ComplexTask.Create(
                        new CustomTask((g, c, s, t, chosen) => { foreach (var w in chosen.Playables) w.Zone?.Remove(w); }),
                        new WeaponTask()));
            })));

            // Magic Mirror (1): Choose a minion and summon a copy of it. Add it to your Dungeon Deck.
            register(46417, "LOOTA_813", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                    { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
                Spell(card, ComplexTask.Create(new SummonCopyTask(EntityType.TARGET), AddTargetToDungeonDeck()));
            });

            // Amulet of Domination (2): Take control of an enemy minion. Add it to your Dungeon Deck.
            register(46412, "LOOTA_805", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                    { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_ENEMY_TARGET, 0 },
                    { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
                Spell(card, ComplexTask.Create(new ControlTask(EntityType.TARGET), AddTargetToDungeonDeck()));
            });

            // Run-scaled treasures: the bosses defeated before this fight (RunProgressRules), on the card at the start.
            // Loyal Sidekick (1, 2/2): Taunt. Has +1/+1 for each Boss you've defeated this run.
            register(46921, "LOOTA_829", card => {
                card.Power = new Power();
                RunProgressRules.Mark(card, (sidekick, bosses) => {
                    sidekick[GameTag.ATK] = sidekick.Card.ATK + bosses;
                    sidekick[GameTag.HEALTH] = sidekick.Card.Health + bosses;
                });
                DungeonRunCards.Plain(card);
            });
            // Primordial Wand (4): Adapt a friendly minion; repeat for each boss defeated this run ("Adapt a friendly minion
            // {0} times": at least once).
            register(46421, "LOOTA_817", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> {
                    { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 }, { PlayReq.REQ_FRIENDLY_TARGET, 0 } });
                RunProgressRules.Mark(card);
                Spell(card, ComplexTask.Create(
                    new FuncNumberTask(p => Math.Max(1, RunProgressRules.Count(p))),
                    new EnqueueNumberTask(new AdaptTask(EntityType.TARGET))));
            });
            // Shifting Hourglass (7): Take an extra turn. Costs (1) less for each Boss you've defeated this run.
            register(46922, "LOOTA_830", card => {
                RunProgressRules.Mark(card);
                card.Power = new Power {
                    Aura = new AdaptiveCostEffect(p => RunProgressRules.Count(p)),
                    PowerTask = new ExtraTurnEffectTask()
                };
                DungeonRunCards.Plain(card);
            });

            // Wish (10): Fill your board with Legendary minions. Fully heal your hero.
            register(46418, "LOOTA_814", card => Spell(card, ComplexTask.Create(
                new EnqueueTask(7, ComplexTask.Create(new RandomMinionTask(GameTag.RARITY, (int)Rarity.LEGENDARY), new SummonTask())),
                new HealFullTask(EntityType.HERO))));
        }

        private const string Hilt = "LOOTA_842b";

        private static ISimpleTask AddTargetToDungeonDeck() => new CustomTask((game, controller, source, target, stack) => {
            if (target != null) RunDeckRules.Add(controller, target.Card);
        });

        // Blade and Hilt leave the game (wherever the opening deal put them) and Quel'Delar is added to the hand.
        private static void ForgeQuelDelar(IPlayable blade)
        {
            var c = blade.Controller;
            var hilt = c.DeckZone.Concat(c.HandZone).FirstOrDefault(p => p.Card.Id == Hilt);
            if (hilt == null || blade.Zone == null) return;
            foreach (var part in new[] { blade, hilt })
            {
                part.Zone.Remove(part);
                c.SetasideZone.Add(part);
            }
            Generic.AddHandPhase(c, Entity.FromCard(c, Cards.FromId("LOOTA_842")));
        }

        // A Mana Crystal lost, a spent one first.
        private static void LoseManaCrystal(Controller c)
        {
            if (c.BaseMana == 0) return;
            Generic.ChangeManaCrystal(c, -1, true);
            if (c.UsedMana > 0) c.UsedMana--;
        }

        private static void Spell(Card card, ISimpleTask task)
        {
            card.Power = new Power { PowerTask = task };
            DungeonRunCards.Plain(card);
        }
    }
}
