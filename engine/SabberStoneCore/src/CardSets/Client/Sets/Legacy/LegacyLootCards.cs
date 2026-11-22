using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Sets.Legacy
{
    // Legacy (Wild) cards that Dungeon Run's loot buckets offer and whose legacy definition was left empty (strict admission:
    // a bucket is offered only when every card on its list is implemented).
    internal static class LegacyLootCards
    {
        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Fierce Monkey (Warrior Taunt): a 3/4 Taunt; the keyword is card data.
            register(2894, "LOE_022", card => card.Implemented = true);

            // Obsidian Destroyer (Warrior Taunt): At the end of your turn, summon a 1/1 Scarab with Taunt.
            register(2881, "LOE_009", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.TURN_END) { SingleTask = new SummonTask("LOE_009t", SummonSide.RIGHT) }
                };
                card.Implemented = true;
            });
            register(38325, "LOE_009t", card => card.Implemented = true);

            // Eadric the Pure (Paladin Justice): Battlecry: Change all enemy minions' Attack to 1.
            register(2727, "AT_081", card => {
                card.Power = new Power { InfoCardId = "AT_081e", PowerTask = new AddEnchantmentTask("AT_081e", EntityType.OP_MINIONS) };
                card.Implemented = true;
            });
            register(2728, "AT_081e", card => {
                card.Power = new Power { Enchant = new Enchant(ATK.Effect(EffectOperator.SET, 1)) };
                card.Implemented = true;
            });

            // Mysterious Challenger (Paladin Secrets): Battlecry: Put one of each Secret from your deck into the battlefield
            // (in a random order, until the Secret zone is full; a Secret already active is skipped).
            register(2726, "AT_079", card => {
                card.Power = new Power { PowerTask = new CustomTask((game, controller, source, target, stack) => {
                    var secrets = controller.DeckZone.Where(p => p.Card.IsSecret).ToList();
                    while (secrets.Count > 0 && !controller.SecretZone.IsFull)
                    {
                        game.OnRandomHappened(true);
                        IPlayable pick = secrets[game.Random.Next(secrets.Count)];
                        secrets.RemoveAll(p => p.Card.AssetId == pick.Card.AssetId);
                        if (controller.SecretZone.Any(p => p.Card.AssetId == pick.Card.AssetId)) continue;
                        controller.DeckZone.Remove(pick);
                        pick.Power.Trigger?.Activate(pick);
                        controller.SecretZone.Add((Spell)pick);
                        if (controller == game.CurrentPlayer) pick.IsExhausted = true;
                    }
                }) };
                card.Implemented = true;
            });

            // Vol'jin (Priest Shadow): Battlecry: Swap Health with another minion.
            register(1931, "GVG_014", card => {
                card.Power = new Power { InfoCardId = "GVG_014a", PowerTask = new CustomTask((game, controller, source, target, stack) => {
                    if (!(source is Minion self) || !(target is Minion other) || other == self) return;
                    int mine = self.Health, theirs = other.Health;
                    Card shadowed = Cards.FromId("GVG_014a");
                    foreach (var (minion, health) in new[] { (self, theirs), (other, mine) })
                    {
                        if (game.History) Enchantment.GetInstance(controller, self, minion, in shadowed);
                        Health.Effect(EffectOperator.SET, health).ApplyTo(minion);
                    }
                }) };
                card.Implemented = true;
            });
            register(2169, "GVG_014a", card => card.Implemented = true);

            // Enhance-o Mechano (Shaman Mechanized): Battlecry: Give your other minions Windfury, Taunt, or Divine Shield
            // (each minion one at random).
            register(2075, "GVG_107", card => {
                card.Power = new Power { PowerTask = new CustomTask((game, controller, source, target, stack) => {
                    var keywords = new[] { GameTag.WINDFURY, GameTag.TAUNT, GameTag.DIVINE_SHIELD };
                    foreach (Minion minion in controller.BoardZone.Interactable.Where(m => m != source).ToList())
                    {
                        game.OnRandomHappened(true);
                        new SetGameTagTask(keywords[game.Random.Next(keywords.Length)], 1, EntityType.TARGET).Process(game, controller, source, minion);
                    }
                }) };
                card.Implemented = true;
            });

            // Blingtron 3000 (Hunter Weapons): Battlecry: Equip a random weapon for each player (any collectible weapon).
            register(2087, "GVG_119", card => {
                card.Power = new Power { PowerTask = new CustomTask((game, controller, source, target, stack) => {
                    List<Card> pool = Cards.All.Where(c => c.Type == CardType.WEAPON && c.Collectible && c.Implemented
                            && !c.ExcludeFromLegacyRandomPools).GroupBy(c => c.Name).Select(g => g.OrderBy(c => c.Id, StringComparer.Ordinal).First())
                        .OrderBy(c => c.Id, StringComparer.Ordinal).ToList();
                    if (pool.Count == 0) return;
                    foreach (Controller player in new[] { controller, controller.Opponent })
                    {
                        game.OnRandomHappened(true);
                        var weapon = (Weapon)Entity.FromCard(player, pool[game.Random.Next(pool.Count)]);
                        weapon.Card.Power?.Aura?.Activate(weapon);
                        weapon.Card.Power?.Trigger?.Activate(weapon);
                        player.Hero.AddWeapon(weapon);
                    }
                }) };
                card.Implemented = true;
            });

            // Gelbin Mekkatorque (Shaman Battlecrier): Battlecry: Summon an AWESOME invention (one of Mekka1-4, its entourage).
            register(858, "EX1_112", card => {
                card.Power = new Power { PowerTask = ComplexTask.Create(new RandomEntourageTask(), new SummonTask(SummonSide.RIGHT)) };
                card.Implemented = true;
            });
            // Homing Chicken: At the start of your turn, destroy this minion and draw 3 cards.
            register(227, "Mekka1", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.TURN_START) {
                        SingleTask = ComplexTask.Create(new DestroyTask(EntityType.SOURCE), new DrawTask(3))
                    }
                };
                card.Implemented = true;
            });
            // Repair Bot: At the end of your turn, restore 6 Health to a damaged character.
            register(329, "Mekka2", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.TURN_END) {
                        SingleTask = ComplexTask.Create(
                            new IncludeTask(EntityType.ALL),
                            new FilterStackTask(SelfCondition.IsDamaged),
                            new RandomTask(1, EntityType.STACK),
                            new HealTask(6, EntityType.STACK))
                    }
                };
                card.Implemented = true;
            });
            // Emboldener 3000: At the end of your turn, give a random minion +1/+1.
            register(52, "Mekka3", card => {
                card.Power = new Power {
                    InfoCardId = "Mekka3e",
                    Trigger = new Trigger(TriggerType.TURN_END) {
                        SingleTask = ComplexTask.Create(
                            new RandomTask(1, EntityType.ALLMINIONS),
                            new AddEnchantmentTask("Mekka3e", EntityType.STACK))
                    }
                };
                card.Implemented = true;
            });
            register(346, "Mekka3e", card => {
                card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(1)) };
                card.Implemented = true;
            });
            // Poultryizer: At the start of your turn, transform a random minion into a 1/1 Chicken.
            register(146, "Mekka4", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.TURN_START) {
                        SingleTask = ComplexTask.Create(
                            new RandomTask(1, EntityType.ALLMINIONS),
                            new TransformTask("Mekka4t", EntityType.STACK))
                    }
                };
                card.Implemented = true;
            });
            register(262, "Mekka4t", card => card.Implemented = true);

            // Bouncing Blade (Warrior Everybody Hurts): Deal 1 damage to a random minion. Repeat until a minion dies (at most
            // 80 times, as the original; an Immune minion is never hit).
            register(2018, "GVG_050", card => {
                card.Power = new Power { PowerTask = new CustomTask((game, controller, source, target, stack) => {
                    for (int i = 0; i < 80; i++)
                    {
                        var minions = controller.BoardZone.Interactable.Concat(controller.Opponent.BoardZone.Interactable)
                            .Where(m => !m.ToBeDestroyed && m.Health > 0 && !m.IsImmune).ToList();
                        if (minions.Count == 0) return;
                        game.OnRandomHappened(true);
                        Minion hit = minions[game.Random.Next(minions.Count)];
                        new DamageTask(1, EntityType.TARGET, true).Process(game, controller, source, hit);
                        if (hit.ToBeDestroyed || hit.Health <= 0 || game.State == State.COMPLETE) return;
                    }
                }) };
                card.Implemented = true;
            });

            // Arch-Thief Rafaam (Warrior Legendary): Battlecry: Discover a powerful Artifact.
            register(2964, "LOE_092", card => {
                card.Power = new Power { PowerTask = new DiscoverTask(DiscoverType.ARTIFACT) };
                card.Implemented = true;
            });
            // Lantern of Power (10): Give a minion +10/+10.
            register(19614, "LOEA16_3", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_TARGET_TO_PLAY, 0 }, { PlayReq.REQ_MINION_TARGET, 0 } });
                card.Power = new Power { PowerTask = new AddEnchantmentTask("LOEA16_3e", EntityType.TARGET) };
                card.Implemented = true;
            });
            register(36450, "LOEA16_3e", card => {
                card.Power = new Power { Enchant = new Enchant(Effects.AttackHealth_N(10)) };
                card.Implemented = true;
            });
            // Timepiece of Horror (10): Deal $10 damage randomly split among all enemies.
            register(19615, "LOEA16_4", card => {
                card.Power = new Power { PowerTask = new EnqueueTask(10, ComplexTask.DamageRandomTargets(1, EntityType.ENEMIES, 1), true) };
                card.Implemented = true;
            });
            // Mirror of Doom (10): Fill your board with 3/3 Mummy Zombies.
            register(19616, "LOEA16_5", card => {
                card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
                card.Power = new Power { PowerTask = new SummonTask("LOEA16_5t", 7, SummonSide.SPELL) };
                card.Implemented = true;
            });
            register(36452, "LOEA16_5t", card => card.Implemented = true);

            // Djinni of Zephyrs (Priest Discipline): After you cast a spell on another friendly minion, cast a copy of it on
            // this one (the spell's effect again, aimed at the Djinni, as The Voraxx recasts).
            register(2925, "LOE_053", card => {
                card.Power = new Power {
                    Trigger = new Trigger(TriggerType.AFTER_CAST) {
                        TriggerSource = TriggerSource.FRIENDLY_SPELL_CASTED_ON_OWN_MINIONS,
                        OwnerCondition = new SelfCondition(owner => owner.Game.CurrentEventData?.EventTarget is Minion aimed && aimed != owner),
                        SingleTask = new CustomTask((game, controller, source, target, stack) => {
                            if (!(source is Minion djinni) || djinni.Zone?.Type != Zone.PLAY || djinni.ToBeDestroyed || !(target is Spell spell)) return;
                            using (game.Facts.RepeatActivation(spell)) spell.ActivateTask(PowerActivation.POWER, djinni);
                        })
                    }
                };
                card.Implemented = true;
            });

            // Wilfred Fizzlebang (Warlock Huge Hand): Cards you draw from your Hero Power cost (0).
            register(2621, "AT_027", card => {
                card.Power = new Power {
                    InfoCardId = "AT_027e",
                    Trigger = new Trigger(TriggerType.DRAW) {
                        TriggerSource = TriggerSource.FRIENDLY,
                        OwnerCondition = new SelfCondition(owner =>
                            owner.Game.Facts.ExecutingSource is HeroPower power && power.Controller == owner.Controller),
                        SingleTask = new AddEnchantmentTask("AT_027e", EntityType.TARGET)
                    }
                };
                card.Implemented = true;
            });
            register(2622, "AT_027e", card => {
                card.Power = new Power { Enchant = new Enchant(Effects.SetCost(0)) };
                card.Implemented = true;
            });
        }
    }
}
