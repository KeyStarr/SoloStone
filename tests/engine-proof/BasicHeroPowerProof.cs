using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client.Sets;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class BasicHeroPowerProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "basic power: " + message);
    internal static void Run()
    {
        using var stream = new GZipStream(File.OpenRead("data/carddb/37.0.0.254477/cards.json.gz"), CompressionMode.Decompress);
        using var doc = JsonDocument.Parse(stream);
        var data = doc.RootElement.GetProperty("cards").EnumerateArray().ToDictionary(c => c.GetProperty("cardId").GetString());
        for (int i = 1; i <= 11; i++)
        {
            string id = "BE_720t" + i;
            var definition = Cards.FromId(id); var row = data[id];
            Check(definition.Implemented && definition.AssetId == row.GetProperty("dbfId").GetInt32()
                && definition.Text == row.GetProperty("text").GetString() && definition.Cost == 1
                && definition.ATK == 2 && definition.Health == 1 && definition.IsRace(Race.BEAST), id + " pinned token");
            foreach (bool combo in new[] { false, true })
            {
                var g = ImbueProof.Make(CardClass.ROGUE); var p = g.Player1;
                var installed = p.Hero.HeroPower;
                installed.IsExhausted = true; p.Hero.Damage = 5;
                int deck = p.DeckZone.Count;
                p.IsComboActive = combo;
                var larva = Add(p, id);
                var target = definition.Class == CardClass.MAGE ? p.Opponent.Hero
                    : definition.Class == CardClass.PRIEST ? p.Hero : null;
                int uses = p.NumTimesHeroPowerUsedThisGame;
                var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
                foreach (var copy in new[] { g, clone, loaded })
                {
                    var owner = copy.Player1;
                    var chosen = target == null ? null : (ICharacter)copy.IdEntityDic[target.Id];
                    Check(copy.Process(PlayCardTask.Any(owner, copy.IdEntityDic[larva.Id], combo ? chosen : null)), "Larva play accepted");
                    Check(owner.Hero.HeroPower.Id == installed.Id && owner.Hero.HeroPower.IsExhausted
                        && owner.UsedMana == 1 && owner.NumTimesHeroPowerUsedThisGame == uses,
                        "Combo neither installs nor pays/refreshes/uses original power");
                }
                Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded),
                    "Combo clone/snapshot deterministic continuation");
                var temporary = p.GraveyardZone.OfType<HeroPower>().ToArray();
                Check(temporary.Length == (combo ? 1 : 0), "non-Combo does not activate a power");
                if (!combo) continue;
                Check(temporary.Single().Card.Id == BasicHeroPowerRules.PowerId(definition.Class)
                    && temporary.Single().Cost == (definition.Class == CardClass.DEMONHUNTER ? 1 : 2)
                    && !temporary.Single().IsExhausted, "temporary power exact identity, printed cost, no player use");
                bool effect = definition.Class switch {
                    CardClass.WARRIOR => p.Hero.Armor == 2,
                    CardClass.SHAMAN => p.BoardZone.Count == 2 && Cards.BasicTotems.Contains(p.BoardZone.Last().Card),
                    CardClass.ROGUE => p.Hero.Weapon?.Card.Id == "CS2_082" && p.Hero.Weapon.Durability == 2,
                    CardClass.PALADIN => p.BoardZone.Last().Card.Id == "CS2_101t",
                    CardClass.HUNTER => p.Opponent.Hero.Damage == 2,
                    CardClass.DRUID => p.Hero.AttackDamage == 1 && p.Hero.Armor == 1,
                    CardClass.WARLOCK => p.DeckZone.Count == deck - 1 && p.Hero.Damage == 7,
                    CardClass.MAGE => p.Opponent.Hero.Damage == 1,
                    CardClass.PRIEST => p.Hero.Damage == 3,
                    CardClass.DEMONHUNTER => p.Hero.AttackDamage == 1 && p.Hero.Armor == 0,
                    CardClass.DEATHKNIGHT => p.BoardZone.Last().Card.Id == "HERO_11bpt" && p.BoardZone.Last().HasCharge,
                    _ => false
                };
                Check(effect, id + " basic effect");
                ((Minion)larva).Silence();
                p.SetasideZone.Add(p.BoardZone.Remove((Minion)larva));
                End(g);
                if (definition.Class == CardClass.DRUID || definition.Class == CardClass.DEMONHUNTER)
                    Check(p.Hero.AttackDamage == 0, "temporary attack expires");
                if (definition.Class == CardClass.DEATHKNIGHT)
                    Check(p.BoardZone.Count == 0 && p.GraveyardZone.Any(x => x.Card.Id == "HERO_11bpt"), "Ghoul expires");
            }
        }
        foreach (bool full in new[] { false, true })
        {
            var g = ImbueProof.Make(CardClass.ROGUE); var p = g.Player1;
            if (full) while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
            else foreach (var totem in Cards.BasicTotems) Summon(p, totem.Id);
            p.IsComboActive = true;
            var larva = Add(p, "BE_720t2"); var rng = g.Random.CaptureState();
            Check(g.Process(PlayCardTask.Any(p, larva)), "Blue Larva accepted even when its summon cannot resolve");
            Check(p.BoardZone.Count == (full ? 7 : 5) && g.Random.CaptureState().Equals(rng),
                "basic power summon requirements fizzle without RNG on full board or all Totems present");
        }
        foreach (string token in new[] { "BE_720t1", "BE_720t8" })
        {
            var g = ImbueProof.Make(CardClass.ROGUE); var p = g.Player1;
            var target = Summon(p.Opponent, "CS2_182");
            target[GameTag.CANT_BE_TARGETED_BY_HERO_POWERS] = 1;
            target[GameTag.CANT_BE_TARGETED_BY_SPELLS] = 1;
            target.Damage = 3; p.IsComboActive = true;
            var larva = Add(p, token);
            Check(g.Process(PlayCardTask.Any(p, larva, target)), "Larva minion may choose Elusive target");
            Check(target.Damage == (token == "BE_720t1" ? 1 : 4), "Larva's chosen minion target receives triggered effect");
        }
        foreach (string cls in PoolRequirement.PlayableClasses)
        {
            var heroClass = Enum.Parse<CardClass>(cls);
            var g = ImbueProof.Make(CardClass.ROGUE); var p = g.Player1;
            p.AddHeroAndPower(Cards.HeroCard(heroClass));
            for (int i = 0; i < 50; i++)
            {
                foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
                Larvae.Generate(p, p.Hero, 3);
                Check(p.HandZone.Count == 3 && p.HandZone.All(x => x.Card.Class != p.Hero.Card.Class && x.Card.Id != "BE_720t12"),
                    "Unpinned_LarvaClassPoolAndTeal " + cls);
            }
        }
        Console.WriteLine("PASS basic power facility: eleven tokens, Combo/no Combo, effects, clone/snapshot, expiry, current-class pool");
    }
}
