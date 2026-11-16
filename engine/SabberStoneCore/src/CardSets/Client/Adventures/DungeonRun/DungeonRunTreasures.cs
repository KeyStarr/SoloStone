using System;
using System.Collections.Generic;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;

namespace SabberStoneCore.CardSets.Client.Adventures.DungeonRun
{
    // Dungeon Run's passive treasures (https://hearthstone.wiki.gg/wiki/Dungeon_Run): a run's loadout, set aside at the start
    // of each fight and resolved as Start of Game after the mulligans (GameConfig.PlayerNLoadout). "These are applied at the
    // start of the match, meaning some buffs that affect cards will not affect cards added to your hand or deck later": the
    // cost treasures enchant the cards present then. The board-wide ones (Captured Flag, Cloak of Invisibility) are the
    // client's player enchantments (an aura, a summon trigger); Battle Totem and Totem of the Dead, which have no enchantment card, keep
    // their aura on the set-aside treasure itself.
    internal static class DungeonRunTreasures
    {
        private const string PlayerEnchantFlag = "LOOTA_828e2", PlayerEnchantCloak = "LOOTA_832e2";

        internal static void Apply(Action<int, string, Action<Card>> register)
        {
            // Battle Totem: Your Battlecries trigger twice. Totem of the Dead: Your Deathrattles trigger twice.
            register(47300, "LOOTA_846", card => OwnAura(card, GameTag.EXTRA_BATTLECRIES_BASE));
            register(47299, "LOOTA_845", card => OwnAura(card, GameTag.EXTRA_DEATHRATTLES_BASE));

            // Captured Flag: Your minions have +1/+1 (the player enchantment's aura, minions in play).
            register(46439, "LOOTA_828", card => Passive(card, s => PlayerEnchant(s, PlayerEnchantFlag)));
            register(47622, PlayerEnchantFlag, card => {
                card.Power = new Power { Aura = new Aura(AuraType.BOARD, "LOOTA_828e") };
                DungeonRunCards.Plain(card);
            });
            register(46438, "LOOTA_828e", card => Enchantment(card, Effects.AttackHealth_N(1)));

            // Cloak of Invisibility: Your minions have permanent Stealth (minions only: its spells were fixed in patch 11.0).
            register(47020, "LOOTA_832", card => Passive(card, s => PlayerEnchant(s, PlayerEnchantCloak)));
            // The engine's Stealth has no aura form, so the player enchantment stealths each friendly minion as it arrives.
            register(47623, PlayerEnchantCloak, card => {
                card.Power = new Power { Trigger = new Trigger(TriggerType.AFTER_SUMMON) {
                    TriggerSource = TriggerSource.MINIONS, SingleTask = new AddEnchantmentTask("LOOTA_832e", EntityType.TARGET) } };
                PermanentStealthRules.Mark(card);
                DungeonRunCards.Plain(card);
            });
            register(47019, "LOOTA_832e", card => Enchantment(card, Effects.StealthEff));

            // Crystal Gem: Start with an extra Mana Crystal (an empty one; the first turn fills it with the turn's own).
            register(46407, "LOOTA_801", card => Passive(card, s => Generic.ChangeManaCrystal(s.Controller, 1, true)));

            // Glyph of Warding: Enemy minions cost (1) more (the opponent's minions in hand and deck at the start).
            register(46925, "LOOTA_831", card => Passive(card, s => EnchantCards(s, s.Controller.Opponent, "LOOTA_831e", p => p is Minion)));
            register(46926, "LOOTA_831e", card => Enchantment(card, Effects.AddCost(1)));

            // Grommash's Armguards: Your weapons cost (1).
            register(46923, "LOOTA_818", card => Passive(card, s => EnchantCards(s, s.Controller, "LOOTA_818e", p => p is Weapon)));
            register(46924, "LOOTA_818e", card => Enchantment(card, Effects.SetCost(1)));

            // Justicar's Ring: Your Hero Power is upgraded and costs (1) (Genn's Worgen shape: the class power upgrades, then
            // the current power costs (1)).
            register(46408, "LOOTA_802", card => Passive(card, JusticarsRing));
            register(47443, "LOOTA_802e", card => Enchantment(card, Effects.SetCost(1)));

            // Khadgar's Scrying Orb: Your spells cost (1) less.
            register(46428, "LOOTA_824", card => Passive(card, s => EnchantCards(s, s.Controller, "LOOTA_824e", p => p is Spell)));
            register(46427, "LOOTA_824e", card => Enchantment(card, Effects.AddCost(-1)));

            // Mysterious Tome: At the start of the game, play 3 random Secrets (distinct ones, of any class).
            register(47021, "LOOTA_833", card => Passive(card, MysteriousTome));

            // Potion of Vitality: Double your starting Health (maximum Health too).
            register(46405, "LOOTA_800", card => Passive(card, s => s.Controller.Hero.BaseHealth *= 2));

            // Robe of the Magi: Spell Damage +3 (the player's own Spell Damage).
            register(46430, "LOOTA_825", card => Passive(card, s => PlayerEnchant(s, "LOOTA_825e")));
            register(46429, "LOOTA_825e", card => Enchantment(card, new Effect(GameTag.SPELLPOWER, EffectOperator.ADD, 3)));

            // Scepter of Summoning: Your minions that cost (5) or more cost (5).
            register(46410, "LOOTA_803", card => Passive(card, s => EnchantCards(s, s.Controller, "LOOTA_803e", p => p is Minion && p.Cost > 5)));
            register(46409, "LOOTA_803e", card => Enchantment(card, Effects.SetCost(5)));

            // Small Backpacks: At the start of the game, draw 2 cards.
            register(46411, "LOOTA_804", card => Passive(card, s => { Generic.Draw(s.Controller); Generic.Draw(s.Controller); }));
        }

        // A passive treasure: no card effect of its own, a Start of Game effect from the loadout.
        private static void Passive(Card card, Action<IPlayable> effect)
        {
            card.Power = new Power();
            StartOfGameRules.Declare(card, effect);
            DungeonRunCards.Plain(card);
        }

        private static void OwnAura(Card card, GameTag tag)
        {
            Passive(card, s => s.Card.Power.Aura.Activate(s));
            card.Power.Aura = new Aura(AuraType.CONTROLLER, new Effect(tag, EffectOperator.SET, 1));
        }

        private static void PlayerEnchant(IPlayable source, string id) =>
            Generic.AddEnchantmentBlock(source.Game, Cards.FromId(id), source, source.Controller);

        private static void EnchantCards(IPlayable source, Controller owner, string id, Func<IPlayable, bool> which)
        {
            Card enchantment = Cards.FromId(id);
            foreach (var target in owner.HandZone.Concat(owner.DeckZone).Where(which).ToList())
                Generic.AddEnchantmentBlock(source.Game, enchantment, source, target);
        }

        // The basic powers the client's data gives no upgrade for (tag 1086) map like Justicar Trueheart's.
        private static readonly Dictionary<string, string> Upgrades = new Dictionary<string, string> {
            { "HERO_01bp", "AT_132_WARRIOR" }, { "HERO_08bp", "AT_132_MAGE" }
        };

        private static void JusticarsRing(IPlayable source)
        {
            var c = source.Controller;
            var power = c.Hero.HeroPower;
            int upgradeDbf = power.Card[(GameTag)1086];
            Card upgraded = upgradeDbf != 0 ? Cards.FromAssetId(upgradeDbf)
                : Upgrades.TryGetValue(power.Card.Id, out string id) ? Cards.FromId(id) : null;
            if (upgraded != null)
            {
                new ReplaceHeroPower(upgraded).Process(c.Game, c, source, null);
                BasicHeroPowerRules.EnrollPowerAuras(c, c.Hero.HeroPower);
            }
            Generic.AddEnchantmentBlock(c.Game, Cards.FromId("LOOTA_802e"), source, c.Hero.HeroPower);
        }

        private static void MysteriousTome(IPlayable source)
        {
            var c = source.Controller; var game = c.Game;
            var pool = Cards.All.Where(card => card.IsSecret && card.Collectible && card.Implemented && !card.ExcludeFromLegacyRandomPools)
                .Select(card => card.Id).Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
            for (int i = 0; i < 3 && pool.Count > 0 && !c.SecretZone.IsFull; i++)
            {
                var candidates = pool.Where(id => c.SecretZone.All(s => s.Card.Id != id)).ToList();
                if (candidates.Count == 0) break;
                string pick = candidates[DungeonRunCards.Pick(game, candidates.Count)];
                var secret = (Spell)Entity.FromCard(c, Cards.FromId(pick));
                secret.Power.Trigger?.Activate(secret);
                c.SecretZone.Add(secret);
            }
        }

        private static void Enchantment(Card card, params IEffect[] effects)
        {
            card.Power = new Power { Enchant = new Enchant(effects) };
            DungeonRunCards.Plain(card);
        }
    }
}
