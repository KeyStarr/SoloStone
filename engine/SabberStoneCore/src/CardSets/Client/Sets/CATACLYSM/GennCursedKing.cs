using System;
using System.Linq;
using System.Collections.Generic;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Effects;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
namespace SabberStoneCore.CardSets.Client.Sets
{
    internal static class GennCursedKing
    {
        [StandardCard("CATA_615", 123396)]
        internal static void Configure(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power();
        }
        internal static void Refresh(Controller c)
        {
            List<IPlayable> found = null;
            for (int i = 0; i < c.HandZone.Count; i++)
                if (c.HandZone[i].Card.Id == "CATA_615") (found ??= new List<IPlayable>()).Add(c.HandZone[i]);
            if (found == null) return;
            foreach (var card in found)
            {
                var rest = c.HandZone.Where(p => p != card).Select(p => p.Cost % 2).Distinct().ToArray();
                if (rest.Length > 1) continue;
                // Rebase definition stats, then replay only retained attachment stat layers.
                var attachments = card.AppliedEnchantments?.ToArray() ?? Array.Empty<Enchantment>();
                Generic.ChangeEntityBlock(c, card, Cards.FromId("CATA_615t"), false);
                var minion = (Minion)card;
                minion.AttackDamage = card.Card.ATK; minion.BaseHealth = card.Card.Health;
                foreach (var attachment in attachments)
                    if (attachment.Card.Power?.Enchant is Enchant enchant)
                        for (int i = 0; i < enchant.Effects.Length; i++)
                        {
                            var effect = enchant.Effects[i];
                            if (effect.Tag != GameTag.ATK && effect.Tag != GameTag.HEALTH) continue;
                            if (enchant.UseScriptTag && i < 2) effect = effect.ChangeValue(i == 1 && attachment.ScriptTag2 >= 0 ? attachment.ScriptTag2 : attachment.ScriptTag1);
                            effect.ApplyTo(card);
                        }
            }
        }
        [StandardCard("CATA_615t", 123397)]
        internal static void ConfigureWorgen(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                var power = c.Hero.HeroPower;
                if (power.Card.Id == BasicHeroPowerRules.PowerId(c.BaseClass))
                {
                    string upgraded = power.Card.Id switch {
                        "CS1h_001" => "AT_132_PRIEST", "CS2_017" => "AT_132_DRUID", "CS2_034" => "AT_132_MAGE",
                        "CS2_049" => "AT_132_SHAMAN", "CS2_056" => "AT_132_WARLOCK", "CS2_083b" => "AT_132_ROGUE",
                        "CS2_101" => "AT_132_PALADIN", "CS2_102" => "AT_132_WARRIOR", "DS1h_292" => "AT_132_HUNTER",
                        "HERO_10bp" => "HERO_10bp2", "HERO_11bp" => "HERO_11bp2", _ => null
                    };
                    if (upgraded != null)
                    {
                        new ReplaceHeroPower(Cards.FromId(upgraded)).Process(g, c, s, null);
                        BasicHeroPowerRules.EnrollPowerAuras(c, c.Hero.HeroPower);
                    }
                }
                // shortcut: current-power cost only; later replacement lifetime awaits a Genn trace.
                Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_615e"), (IPlayable)s, c.Hero.HeroPower);
            }) };
        }
        [StandardCard("CATA_615e", 130475)]
        internal static void ConfigureCost(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { Enchant = new Enchant(Effects.SetCost(1)) };
        }
        [StandardCard("HERO_10bp2", 60483)]
        internal static void ConfigureDemonHunter(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.Power = new Power { PowerTask = ComplexTask.Create(new AddEnchantmentTask("HERO_10bpe", EntityType.HERO), new AddEnchantmentTask("HERO_10bpe", EntityType.HERO)) };
        }
        [StandardCard("HERO_11bp2", 79943)]
        internal static void ConfigureDeathKnight(Card card)
        {
            card.Implemented = true; card.ExcludeFromLegacyRandomPools = true;
            card.SetPlayRequirements(new Dictionary<PlayReq, int> { { PlayReq.REQ_NUM_MINION_SLOTS, 1 } });
            card.Power = new Power { PowerTask = new CustomTask((g, c, s, t, stack) => {
                if (c.BoardZone.IsFull) return;
                var ghoul = (Minion)Entity.FromCard(c, Cards.FromId("HERO_11bpt"), new EntityData {
                    [GameTag.ZONE] = (int)Zone.PLAY, [GameTag.DISPLAYED_CREATOR] = s.Id
                });
                ghoul.AttackDamage = 2;
                Generic.SummonBlock(g, ghoul, -1, s);
            }) };
        }
    }
}
