using System.Security.Cryptography;
using System.Text;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

static void Check(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}

static string Hash(IEnumerable<string> ids) => Convert.ToHexString(
    SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", ids)))).ToLowerInvariant();

static string HashPairs(IEnumerable<KeyValuePair<string, int>> entries) => Hash(
    entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => entry.Key + ":" + entry.Value));

foreach (string id in new[] { "EX1_577", "EX1_614" })
    Check(LocalCardCatalog.Current.All(card => card.Id != id)
        && LocalCardCatalog.PackPool(2).All(card => card.Id != id)
        && LocalCardCatalog.QuarantinedIds.Contains(id), "native identity conflict must be quarantined: " + id);
foreach (string id in new[] { "EX1_614t", "EX1_finkle" })
    Check(!LocalCardCatalog.SupplementalGeneratedIds.Contains(id), "quarantined generated identity remains supported: " + id);
Check(LocalCardCatalog.Current.Count == 252, "current catalog count");
Check(LocalCardCatalog.LegacyV4.Count == 79, "legacy catalog count");
Check(LocalCardCatalog.PackPool(1).Count == 39, "legacy pool count");
Check(LocalCardCatalog.PackPool(2).Count == 169, "current pool count");
Check(LocalCardCatalog.NewBasicIds.Count == 43, "new Basic count");
Check(LocalCardCatalog.NewClassicIds.Count == 130, "new Classic count");
Check(LocalCardCatalog.SupplementalGeneratedIds.Count == 75, "generated closure count");
Check(LocalCardCatalog.LegacyGeneratedPublicIds.SequenceEqual(new[] {
    "CS2_tk1", "CS2_mirror", "CS2_101t", "EX1_110t", "skele21"
}), "legacy generated public audit closure drift");
Check(LocalCardCatalog.LegacyV4Starter.Count == 50, "legacy starter count");
Check(HashPairs(LocalCardCatalog.LegacyV4Starter) == "4742616f7d3cc7d2cba7eeaf757130680e5b4c030c5fc1bcf0b46c9904fc599a", "legacy starter hash");
Check(Hash(LocalCardCatalog.LegacyV4.Select(card => card.Id)) == "8ab20c076391d6604f4c10216035f2b1fd2670f4cfdd0024f2cb766648dfac82", "legacy catalog hash");
Check(Hash(LocalCardCatalog.Current.Select(card => card.Id)) == "580afabfbfd1a29424af55c8b4f0974785710a9b184f634aac81a887a673e870", "current catalog hash");
Check(Hash(LocalCardCatalog.PackPool(1).Select(card => card.Id)) == "86153fd3ef37378239324d7523851af458457bbb01f42b2f31301dcce5192111", "legacy pool hash");
Check(Hash(LocalCardCatalog.PackPool(2).Select(card => card.Id)) == "5c0f1b3a3c15a7aadf27338e8b14963a416ee1274524e1edc69ba383622d7950", "current pool hash");
Check(Hash(LocalCardCatalog.SupplementalGeneratedIds) == LocalCardCatalog.SupplementalGeneratedHash, "generated closure hash");
Check(LocalCardCatalog.Current.Select(card => card.Id).Distinct(StringComparer.Ordinal).Count() == 252, "duplicate current ID");
Check(LocalCardCatalog.Current.All(card => card.Collectible && card.Implemented && card.Type != CardType.HERO
    && (card.Class == CardClass.MAGE || card.Class == CardClass.PALADIN || card.Class == CardClass.PRIEST
        || card.Class == CardClass.WARRIOR || card.Class == CardClass.NEUTRAL)), "ineligible card admitted");
Check(LocalCardCatalog.Current.All(card => card.Set == CardSet.CORE || card.Set == CardSet.EXPERT1
    || new[] { "CS2_031", "EX1_016", "EX1_284", "EX1_298" }.Contains(card.Id)), "unsupported set admitted");
Check(LocalCardCatalog.SupplementalGeneratedIds.All(id => Cards.FromId(id)?.Implemented == true), "unresolved generated closure ID");
Check(LocalCardCatalog.LegacyGeneratedPublicIds.All(id => Cards.FromId(id) is Card card
    && card.Id == id && card.Implemented && card.Type == CardType.MINION && !card.Collectible),
    "unresolved or noncanonical legacy generated public ID");
Check(LocalCardCatalog.LegacyGeneratedPublicIds.All(id => LocalCardCatalog.Current.All(card => card.Id != id)
    && !LocalCardCatalog.SupplementalGeneratedIds.Contains(id)
    && LocalCardCatalog.PackPool(1).All(card => card.Id != id)
    && LocalCardCatalog.PackPool(2).All(card => card.Id != id)),
    "legacy generated audit identity entered collectible or supplemental policy");
Check(LocalCardCatalog.NewBasicIds.All(id => LocalCardCatalog.Current.Single(card => card.Id == id).Rarity == Rarity.FREE), "non-Free Basic grant");
Check(LocalCardCatalog.QuarantinedIds.SequenceEqual(new[] { "EX1_180", "EX1_188", "EX1_189", "EX1_577", "EX1_614" }), "quarantine drift");
Check(LocalCardCatalog.QuarantinedIds.All(id => LocalCardCatalog.Current.All(card => card.Id != id)), "quarantine admitted");
Check(LocalEconomy.StarterCollection.Count == 93 && LocalCardCatalog.NewBasicIds.All(id => LocalEconomy.StarterCollection[id] == 2), "Basic grants");
Check(LocalCardCatalog.NewClassicIds.All(id => !LocalEconomy.StarterCollection.ContainsKey(id)), "new Classic gifted");
Check(LocalEconomy.LegacyV4StarterCollection.SequenceEqual(LocalCardCatalog.LegacyV4Starter), "legacy starter was inferred");
Check(LocalEconomy.StarterCollection.Count == LocalCardCatalog.LegacyV4Starter.Count + LocalCardCatalog.NewBasicIds.Count
    && LocalCardCatalog.LegacyV4Starter.All(entry => LocalEconomy.StarterCollection.TryGetValue(entry.Key, out int count) && count == entry.Value),
    "current starter is not exact legacy plus new Basic delta");
Check(LocalCardCatalog.NativeAuditTags.SequenceEqual(new[] {
    "COLLECTIBLE", "CARD_SET", "CLASS", "CARDTYPE", "RARITY", "CARDRACE", "ELITE", "FACTION",
    "COST", "ATK", "HEALTH", "DAMAGE", "DURABILITY", "SPELLPOWER", "AFFECTED_BY_SPELL_POWER", "BATTLECRY",
    "DEATHRATTLE", "SECRET", "TAUNT", "CHARGE", "RUSH", "DIVINE_SHIELD", "STEALTH", "WINDFURY", "POISONOUS",
    "FREEZE", "CANT_ATTACK", "CANT_BE_TARGETED_BY_SPELLS", "CANT_BE_TARGETED_BY_HERO_POWERS", "AURA",
    "ADJACENT_BUFF", "ENRAGED", "SILENCE", "DISCARD_CARDS", "COST_BLOOD", "COST_FROST", "COST_UNHOLY", "CORPSE_SPENDER"
}), "direct native tag audit drift");
Check(LocalCardCatalog.NativeAuditReferencedTags.SequenceEqual(new[] {
    "CHARGE", "COUNTER", "DIVINE_SHIELD", "FREEZE", "SECRET", "SILENCE", "SPELLPOWER", "TAUNT", "WINDFURY"
}), "referenced native tag audit drift");
Check(LocalCardCatalog.NativeAuditMetadataTags.SequenceEqual(new[] {
    "CARD_SET", "FACTION", "AttackVisualType", "TRIGGER_VISUAL", "DevState", "TECH_LEVEL", "IS_BACON_POOL_MINION", "1429"
}), "native metadata decision audit drift");
Check(LocalEconomy.GeneratePack(LocalEconomy.StarterCollection.ToDictionary(pair => pair.Key, pair => pair.Value), 17, 0).PoolVersion == 2, "default pool version");
Check(string.Join(",", LocalEconomy.GeneratePack(LocalEconomy.StarterCollection.ToDictionary(pair => pair.Key, pair => pair.Value), 17, 0, 2).Cards.Select(card => card.Id + ":" + card.DuplicateGold))
    == "NEW1_026:0,EX1_366:0,EX1_332:0,CS2_117:0,CS2_169:0", "pool2 golden receipt");
Check(string.Join(",", LocalEconomy.GeneratePack(LocalEconomy.StarterCollection.ToDictionary(pair => pair.Key, pair => pair.Value), 0, 0, 2).Cards.Select(card => card.Id + ":" + card.DuplicateGold))
    == "NEW1_041:0,EX1_184:0,EX1_390:0,EX1_595:0,EX1_350:0", "pool2 post-quarantine Legendary golden receipt");
Check(string.Join(",", LocalEconomy.GeneratePack(LocalEconomy.LegacyV4StarterCollection.ToDictionary(pair => pair.Key, pair => pair.Value), 17, 0, 1).Cards.Select(card => card.Id + ":" + card.DuplicateGold))
    == "EX1_382:0,EX1_354:0,CS1_129:0,EX1_096:0,CS2_031:0", "pool1 golden receipt");
var reached = new HashSet<string>(StringComparer.Ordinal);
for (int seed = 0; seed < 20000 && reached.Count < LocalCardCatalog.PackPool(2).Count; seed++)
    foreach (var card in LocalEconomy.GeneratePack(new Dictionary<string, int>(StringComparer.Ordinal), seed, 0, 2).Cards)
        reached.Add(card.Id);
Check(reached.SetEquals(LocalCardCatalog.PackPool(2).Select(card => card.Id)), "pool2 card unreachable");
Check(reached.All(id => !LocalCardCatalog.NewBasicIds.Contains(id)), "Basic card entered pool2");
try { LocalEconomy.GeneratePack(new Dictionary<string, int>(), 1, 0, 3); throw new Exception("unknown pool accepted"); }
catch (ArgumentOutOfRangeException) { }

var admitted = LocalCardCatalog.Current.Where(card => LocalCardCatalog.LegacyV4.All(old => old.Id != card.Id)).ToArray();
Check(admitted.Length == 173, "admitted addition count");
var allowedGenerated = LocalCardCatalog.Current.Select(card => card.Id)
    .Concat(LocalCardCatalog.SupplementalGeneratedIds).ToHashSet(StringComparer.Ordinal);
foreach (var card in admitted) Smoke(card, allowedGenerated);

Scenario("target damage and heal", () => {
    var game = Fixture(CardClass.PRIEST); int enemy = game.Player2.Hero.Damage, friendly = game.Player1.Hero.Damage;
    Play(game, "EX1_624", task => task.Target?.Id == game.Player2.Hero.Id);
    Check(game.Player2.Hero.Damage - enemy == 5 && friendly - game.Player1.Hero.Damage == 5, "Holy Fire delta");
});
Scenario("aura", () => {
    var game = Fixture(CardClass.MAGE); int before = game.Player1.BoardZone.Sum(minion => minion.AttackDamage);
    Play(game, "CS2_122");
    Check(game.Player1.BoardZone.Where(minion => minion.Card.Id != "CS2_122").Sum(minion => minion.AttackDamage) - before == 2, "Raid Leader aura");
});
Scenario("deathrattle trigger", () => {
    var game = Fixture(CardClass.WARRIOR);
    var gnome = (Minion)Entity.FromCard(game.Player1, Cards.FromId("EX1_029"), zone: game.Player1.BoardZone);
    int damage = game.Player2.Hero.Damage; Play(game, "EX1_400");
    Check(game.Player1.BoardZone.All(minion => minion.Id != gnome.Id) && game.Player2.Hero.Damage - damage == 2, "Leper Gnome deathrattle");
});
Scenario("summon", () => {
    var game = Fixture(CardClass.MAGE); int before = game.Player1.BoardZone.Count; Play(game, "CS2_151");
    Check(game.Player1.BoardZone.Count == before + 2 && game.Player1.BoardZone.Any(minion => minion.Card.Id == "CS2_152"), "Silver Hand Knight summon");
});
Scenario("transform", () => {
    var game = Fixture(CardClass.MAGE); Play(game, "EX1_083");
    Check(game.Player1.BoardZone.Concat(game.Player2.BoardZone).Any(minion => minion.Card.Id == "EX1_tk28" || minion.Card.Id == "EX1_tk29"), "Tinkmaster transform");
});
Scenario("control", () => {
    var game = Fixture(CardClass.PRIEST); int target = game.Player2.BoardZone.First().Id;
    Play(game, "CS1_113", task => task.Target?.Id == target);
    Check(game.Player1.BoardZone.Any(minion => minion.Id == target) && game.Player2.BoardZone.All(minion => minion.Id != target), "Mind Control move");
});
Scenario("weapon", () => {
    var game = Fixture(CardClass.WARRIOR); Play(game, "EX1_398");
    Check(game.Player1.Hero.Weapon?.Card.Id == "EX1_398t", "Arathi Weaponsmith weapon");
});
Scenario("hero attack", () => {
    var game = Fixture(CardClass.WARRIOR); int before = game.Player1.Hero.AttackDamage; Play(game, "CS2_105");
    Check(game.Player1.Hero.AttackDamage - before == 4, "Heroic Strike attack");
});
Scenario("hero power replacement", () => {
    var game = Fixture(CardClass.PRIEST); Play(game, "EX1_625");
    Check(game.Player1.Hero.HeroPower.Card.Id == "EX1_625t", "Shadowform hero power");
});
Scenario("secret placement", () => {
    var game = Fixture(CardClass.MAGE); Play(game, "EX1_287");
    Check(game.Player1.SecretZone.Count == 1 && game.Player1.SecretZone[0].Card.Id == "EX1_287", "Counterspell placement");
});
Scenario("secret activation", () => {
    var game = Fixture(CardClass.MAGE);
    foreach (var card in game.Player2.HandZone.ToArray()) game.Player2.HandZone.Remove(card);
    Play(game, "EX1_287");
    Check(game.Process(EndTurnTask.Any(game.Player1)), "Counterspell turn transition rejected");
    game.Player2.BaseMana = 10; game.Player2.UsedMana = 0;
    var fireball = Entity.FromCard(game.Player2, Cards.FromId("CS2_029"), zone: game.Player2.HandZone);
    int damage = game.Player1.Hero.Damage;
    var cast = game.Player2.Options().First(task => task.Source?.Id == fireball.Id && task.Target?.Id == game.Player1.Hero.Id);
    Check(game.Process(cast), "opponent spell rejected");
    Check(game.Player1.SecretZone.Count == 0 && game.Player1.Hero.Damage == damage, "Counterspell did not cancel spell");
});
Scenario("random transform branches", () => {
    var results = new HashSet<string>(StringComparer.Ordinal);
    for (int seed = 1; seed <= 64 && results.Count < 2; seed++)
    {
        var game = Fixture(CardClass.MAGE, seed); Play(game, "EX1_083");
        foreach (string id in game.Player1.BoardZone.Concat(game.Player2.BoardZone).Select(minion => minion.Card.Id))
            if (id == "EX1_tk28" || id == "EX1_tk29") results.Add(id);
    }
    Check(results.SetEquals(new[] { "EX1_tk28", "EX1_tk29" }), "Tinkmaster branches not both reachable");
});
Scenario("delayed end-turn generation", () => {
    var game = Fixture(CardClass.MAGE); int before = game.Player1.HandZone.Count; Play(game, "EX1_572");
    Check(game.Process(EndTurnTask.Any(game.Player1)), "Ysera end turn rejected");
    Check(game.Player1.HandZone.Count == before + 1 && game.Player1.HandZone.Any(card => card.Card.Id.StartsWith("DREAM_", StringComparison.Ordinal)), "Ysera did not generate Dream card");
});

Scenario("random missiles exact damage", () => {
    var game = Fixture(CardClass.MAGE, 71); Clear(game.Player2.BoardZone);
    var first = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_200"), zone: game.Player2.BoardZone);
    var second = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_200"), zone: game.Player2.BoardZone);
    int before = game.Player2.Hero.Damage + first.Damage + second.Damage;
    Play(game, "EX1_277");
    Check(game.Player2.Hero.Damage + first.Damage + second.Damage - before == 3, "Arcane Missiles damage total");
});
Scenario("area damage and spell power", () => {
    var game = Fixture(CardClass.MAGE); var enemies = game.Player2.BoardZone.ToArray();
    Entity.FromCard(game.Player1, Cards.FromId("CS2_142"), zone: game.Player1.BoardZone);
    int hero = game.Player2.Hero.Damage; var damage = enemies.ToDictionary(minion => minion.Id, minion => minion.Damage);
    Play(game, "CS2_025");
    Check(enemies.All(minion => minion.Damage - damage[minion.Id] == 2) && game.Player2.Hero.Damage == hero,
        "Arcane Explosion spell-damage or area semantics");
});
Scenario("copy from opponent hand", () => {
    var game = Fixture(CardClass.PRIEST); Clear(game.Player2.HandZone);
    Entity.FromCard(game.Player2, Cards.FromId("CS2_029"), zone: game.Player2.HandZone);
    Play(game, "CS2_003");
    Check(game.Player1.HandZone.Count(card => card.Card.Id == "CS2_029") == 1, "Mind Vision copy");
});
Scenario("enrage activation and silence removal", () => {
    var game = Fixture(CardClass.WARRIOR);
    var berserker = (Minion)Entity.FromCard(game.Player1, Cards.FromId("EX1_399"), zone: game.Player1.BoardZone);
    int baseAttack = berserker.AttackDamage;
    Play(game, "EX1_607", task => task.Target?.Id == berserker.Id);
    Check(berserker.Damage == 1 && berserker.AttackDamage == baseAttack + 5, "Gurubashi enrage plus Inner Rage");
    Play(game, "EX1_332", task => task.Target?.Id == berserker.Id);
    Check(berserker.AttackDamage == baseAttack && berserker[GameTag.SILENCED] == 1, "silence did not remove enchant/enrage");
});
Scenario("removal bounce and freeze", () => {
    var removal = Fixture(CardClass.MAGE); var taunt = (Minion)Entity.FromCard(removal.Player2, Cards.FromId("CS2_121"), zone: removal.Player2.BoardZone);
    Play(removal, "EX1_002", task => task.Target?.Id == taunt.Id);
    Check(removal.Player2.BoardZone.All(minion => minion.Id != taunt.Id), "Black Knight removal");

    var bounce = Fixture(CardClass.MAGE); var friendly = bounce.Player1.BoardZone.First();
    Play(bounce, "EX1_049", task => task.Target?.Id == friendly.Id);
    Check(bounce.Player1.HandZone.Any(card => card.Id == friendly.Id), "Brewmaster bounce");

    var freeze = Fixture(CardClass.MAGE); var frozen = freeze.Player2.BoardZone.First();
    Play(freeze, "EX1_275", task => task.Target?.Id == frozen.Id);
    Check(frozen.IsFrozen && freeze.Player2.BoardZone.All(minion => minion.Damage >= 1), "Cone of Cold freeze/area damage");
});
Scenario("draw resource and cost recalculation", () => {
    var draw = Fixture(CardClass.MAGE); int before = draw.Player1.HandZone.Count; Play(draw, "CS2_147");
    Check(draw.Player1.HandZone.Count == before + 1, "Gnomish Inventor draw");

    var resource = Fixture(CardClass.MAGE); Clear(resource.Player2.HandZone);
    var enemySpell = Entity.FromCard(resource.Player2, Cards.FromId("CS2_029"), zone: resource.Player2.HandZone);
    Play(resource, "NEW1_029");
    Check(enemySpell.Cost == 0, "Millhouse opponent spell cost=" + enemySpell.Cost);

    var sparseGame = Fixture(CardClass.MAGE); var sparseGiant = Entity.FromCard(sparseGame.Player1, Cards.FromId("EX1_105"), zone: sparseGame.Player1.HandZone);
    var denseGame = Fixture(CardClass.MAGE);
    for (int i = 0; i < 4; i++) Entity.FromCard(denseGame.Player1, Cards.FromId("CS2_182"), zone: denseGame.Player1.HandZone);
    var denseGiant = Entity.FromCard(denseGame.Player1, Cards.FromId("EX1_105"), zone: denseGame.Player1.HandZone);
    Check(denseGiant.Cost == sparseGiant.Cost - 4, "Mountain Giant hand cost recalculation");
});
Scenario("target legality", () => {
    var game = Fixture(CardClass.WARRIOR); var healthy = game.Player1.BoardZone.First(minion => minion.Damage == 0);
    var damaged = game.Player1.BoardZone.First(minion => minion.Damage > 0); var enemy = game.Player2.BoardZone.First();
    var rampage = Entity.FromCard(game.Player1, Cards.FromId("CS2_104"), zone: game.Player1.HandZone);
    var rampageTargets = game.Player1.Options().Where(task => task.Source?.Id == rampage.Id).Select(task => task.Target?.Id ?? 0).ToHashSet();
    Check(rampageTargets.Contains(damaged.Id) && !rampageTargets.Contains(healthy.Id) && !rampageTargets.Contains(enemy.Id), "Rampage target requirements");
    var charge = Entity.FromCard(game.Player1, Cards.FromId("CS2_103"), zone: game.Player1.HandZone);
    var chargeTargets = game.Player1.Options().Where(task => task.Source?.Id == charge.Id).Select(task => task.Target?.Id ?? 0).ToHashSet();
    Check(chargeTargets.Contains(healthy.Id) && !chargeTargets.Contains(enemy.Id), "Charge friendly target requirement");
    var madness = Entity.FromCard(game.Player1, Cards.FromId("EX1_334"), zone: game.Player1.HandZone);
    enemy[GameTag.ATK] = 4;
    Check(game.Player1.Options().All(task => task.Source?.Id != madness.Id || task.Target?.Id != enemy.Id), "Shadow Madness max attack requirement");

    var tauntGame = Fixture(CardClass.MAGE); var plain = tauntGame.Player2.BoardZone.First();
    var taunt = (Minion)Entity.FromCard(tauntGame.Player2, Cards.FromId("CS2_121"), zone: tauntGame.Player2.BoardZone);
    var knight = Entity.FromCard(tauntGame.Player1, Cards.FromId("EX1_002"), zone: tauntGame.Player1.HandZone);
    var knightTargets = tauntGame.Player1.Options().Where(task => task.Source?.Id == knight.Id).Select(task => task.Target?.Id ?? 0).ToHashSet();
    Check(knightTargets.SetEquals(new[] { taunt.Id }) && !knightTargets.Contains(plain.Id), "Black Knight taunt requirement");

    var raceGame = Fixture(CardClass.MAGE); var murloc = raceGame.Player2.BoardZone.First(minion => minion.Card.Id == "CS2_168");
    var nonMurloc = (Minion)Entity.FromCard(raceGame.Player2, Cards.FromId("CS2_200"), zone: raceGame.Player2.BoardZone);
    var crab = Entity.FromCard(raceGame.Player1, Cards.FromId("NEW1_017"), zone: raceGame.Player1.HandZone);
    var crabTargets = raceGame.Player1.Options().Where(task => task.Source?.Id == crab.Id).Select(task => task.Target?.Id ?? 0).ToHashSet();
    Check(crabTargets.Contains(murloc.Id) && !crabTargets.Contains(nonMurloc.Id), "Hungry Crab race requirement");

    var slotGame = Fixture(CardClass.PRIEST); while (slotGame.Player1.BoardZone.Count < 7)
        Entity.FromCard(slotGame.Player1, Cards.FromId("CS2_182"), zone: slotGame.Player1.BoardZone);
    var mindControl = Entity.FromCard(slotGame.Player1, Cards.FromId("CS1_113"), zone: slotGame.Player1.HandZone);
    Check(slotGame.Player1.Options().All(task => task.Source?.Id != mindControl.Id), "Mind Control full-board requirement");
});
Scenario("aura leave through silence", () => {
    var game = Fixture(CardClass.MAGE); var existing = game.Player1.BoardZone.ToArray();
    Play(game, "CS2_122"); var leader = game.Player1.BoardZone.Single(minion => minion.Card.Id == "CS2_122");
    Check(existing.All(minion => minion.AttackDamage == minion.Card.ATK + 1), "Raid Leader aura enter");
    Play(game, "EX1_332", task => task.Target?.Id == leader.Id);
    Check(existing.All(minion => minion.AttackDamage == minion.Card.ATK), "Raid Leader aura silence leave");
});
Scenario("weapon durability buff and destruction", () => {
    var game = Fixture(CardClass.WARRIOR); Play(game, "EX1_398");
    Check(game.Player1.Hero.Weapon?.Durability == 2, "generated weapon durability");
    Play(game, "NEW1_024");
    Check(game.Player1.Hero.Weapon?.AttackDamage == 3 && game.Player1.Hero.Weapon?.Durability == 3, "Captain Greenskin weapon buff");
    Switch(game); Clear(game.Player2.HandZone); game.Player2.BaseMana = 10; game.Player2.UsedMana = 0;
    int hand = game.Player2.HandZone.Count; PlayFor(game, game.Player2, "EX1_558");
    Check(game.Player1.Hero.Weapon == null && game.Player2.HandZone.Count == hand + 3, "Harrison weapon destruction/draw");
});
Scenario("board full token suppression", () => {
    var game = Fixture(CardClass.WARRIOR); while (game.Player1.BoardZone.Count < 6)
        Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.BoardZone);
    Play(game, "CS2_196");
    Check(game.Player1.BoardZone.Count == 7 && game.Player1.BoardZone.All(minion => minion.Card.Id != "CS2_boar"), "Razorfen board-full token");
});
Scenario("copy control and transform families", () => {
    var thoughtsteal = Fixture(CardClass.PRIEST, 9); Clear(thoughtsteal.Player2.DeckZone);
    Entity.FromCard(thoughtsteal.Player2, Cards.FromId("CS2_168"), zone: thoughtsteal.Player2.DeckZone);
    Entity.FromCard(thoughtsteal.Player2, Cards.FromId("CS2_182"), zone: thoughtsteal.Player2.DeckZone);
    Play(thoughtsteal, "EX1_339");
    Check(new[] { "CS2_168", "CS2_182" }.All(id => thoughtsteal.Player1.HandZone.Any(card => card.Card.Id == id)), "Thoughtsteal copies");

    var mindgames = Fixture(CardClass.PRIEST, 9); Clear(mindgames.Player2.DeckZone);
    Entity.FromCard(mindgames.Player2, Cards.FromId("CS2_168"), zone: mindgames.Player2.DeckZone);
    Play(mindgames, "EX1_345");
    Check(mindgames.Player1.BoardZone.Any(minion => minion.Card.Id == "CS2_168"), "Mindgames copy");

    var tech = Fixture(CardClass.MAGE, 13); while (tech.Player2.BoardZone.Count < 4)
        Entity.FromCard(tech.Player2, Cards.FromId("CS2_168"), zone: tech.Player2.BoardZone);
    var enemyIds = tech.Player2.BoardZone.Select(minion => minion.Id).ToHashSet(); Play(tech, "EX1_085");
    Check(tech.Player1.BoardZone.Any(minion => enemyIds.Contains(minion.Id)), "Mind Control Tech controller move");

    var madness = Fixture(CardClass.PRIEST); var borrowed = madness.Player2.BoardZone.First(); int borrowedId = borrowed.Id;
    Play(madness, "EX1_334", task => task.Target?.Id == borrowedId);
    Check(madness.Player1.BoardZone.Any(minion => minion.Id == borrowedId), "Shadow Madness initial control");
    Switch(madness);
    Check(madness.Player2.BoardZone.Any(minion => minion.Id == borrowedId), "Shadow Madness return");

    var faceless = Fixture(CardClass.MAGE); var target = faceless.Player2.BoardZone.First();
    Play(faceless, "EX1_564", task => task.Target?.Id == target.Id);
    Check(faceless.Player1.BoardZone.Any(minion => minion.Card.Id == target.Card.Id && minion.Id != target.Id), "Faceless transform-copy");
});
Scenario("all Ysera outcomes", () => {
    var dreams = new HashSet<string>(StringComparer.Ordinal);
    for (int seed = 1; seed <= 512 && dreams.Count < 5; seed++)
    {
        var game = Fixture(CardClass.MAGE, seed); Play(game, "EX1_572"); Switch(game);
        foreach (var card in game.Player1.HandZone.Where(card => card.Card.Id.StartsWith("DREAM_", StringComparison.Ordinal))) dreams.Add(card.Card.Id);
    }
    Check(dreams.SetEquals(new[] { "DREAM_01", "DREAM_02", "DREAM_03", "DREAM_04", "DREAM_05" }), "Ysera outcome distribution: " + string.Join(",", dreams));
});

SecretScenario("Noble Sacrifice", CardClass.PALADIN, "EX1_130", (game, attacker, defender) => {
    int hero = game.Player1.Hero.Damage; Attack(game, game.Player2, attacker, game.Player1.Hero);
    Check(game.Player1.Hero.Damage == hero && game.IdEntityDic.Values.Any(entity => entity.Card?.Id == "EX1_130a")
        && game.Player1.BoardZone.All(minion => minion.Card.Id != "EX1_130a"), "Noble Sacrifice redirect/token");
});
SecretScenario("Eye for an Eye", CardClass.PALADIN, "EX1_132", (game, attacker, defender) => {
    int enemy = game.Player2.Hero.Damage; Attack(game, game.Player2, attacker, game.Player1.Hero);
    Check(game.Player2.Hero.Damage - enemy == attacker.AttackDamage, "Eye for an Eye reflected damage");
});
SecretScenario("Redemption", CardClass.PALADIN, "EX1_136", (game, attacker, defender) => {
    defender.Damage = defender.Health - 1; string id = defender.Card.Id; Attack(game, game.Player2, attacker, defender);
    Check(game.Player1.BoardZone.Any(minion => minion.Card.Id == id && minion.Health - minion.Damage == 1), "Redemption revive");
});
SecretScenario("Counterspell", CardClass.MAGE, "EX1_287", (game, attacker, defender) => {
    int damage = game.Player1.Hero.Damage; PlayFor(game, game.Player2, "CS2_029", task => task.Target?.Id == game.Player1.Hero.Id);
    Check(game.Player1.Hero.Damage == damage, "Counterspell cancellation");
});
SecretScenario("Ice Barrier", CardClass.MAGE, "EX1_289", (game, attacker, defender) => {
    int armor = game.Player1.Hero.Armor, damage = game.Player1.Hero.Damage, attack = attacker.AttackDamage;
    Attack(game, game.Player2, attacker, game.Player1.Hero);
    Check(game.Player1.Hero.Damage == damage && game.Player1.Hero.Armor - armor == 8 - attack, "Ice Barrier armor");
});
SecretScenario("Mirror Entity", CardClass.MAGE, "EX1_294", (game, attacker, defender) => {
    PlayFor(game, game.Player2, "CS2_182");
    Check(game.Player1.BoardZone.Count(minion => minion.Card.Id == "CS2_182") >= 2, "Mirror Entity copy");
});
SecretScenario("Repentance", CardClass.PALADIN, "EX1_379", (game, attacker, defender) => {
    var played = PlayFor(game, game.Player2, "CS2_182").Source as Minion;
    Check(played != null && played.Health == 1, "Repentance health");
});
SecretScenario("Vaporize", CardClass.MAGE, "EX1_594", (game, attacker, defender) => {
    int attackerId = attacker.Id; attacker.IsExhausted = false;
    game.Process(MinionAttackTask.Any(game.Player2, attacker, game.Player1.Hero));
    Check(game.Player2.BoardZone.All(minion => minion.Id != attackerId), "Vaporize destroy");
});
SecretScenario("Spellbender", CardClass.MAGE, "tt_010", (game, attacker, defender) => {
    int damage = defender.Damage; PlayFor(game, game.Player2, "CS2_029", task => task.Target?.Id == defender.Id);
    Check(defender.Damage == damage && game.IdEntityDic.Values.Any(entity => entity.Card?.Id == "tt_010a")
        && game.Player1.BoardZone.All(minion => minion.Card.Id != "tt_010a"), "Spellbender redirect/token");
});

Console.WriteLine($"PASS card catalog policy, {admitted.Length} legal plays, focused effects");

static Game Fixture(CardClass own, int seed = 1701)
{
    var game = new Game(new GameConfig {
        Player1HeroClass = own, Player2HeroClass = CardClass.WARRIOR,
        Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
        Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_168"), 30).ToList(),
        StartPlayer = 1, Shuffle = false, SkipMulligan = true, RandomSeed = seed, History = true, Logging = false
    });
    game.StartGame();
    foreach (var card in game.Player1.HandZone.ToArray()) game.Player1.HandZone.Remove(card);
    game.Player1.BaseMana = 10; game.Player1.UsedMana = 0; game.Player1.Hero.Damage = 8; game.Player2.Hero.Damage = 8;
    var friendly = (Minion)Entity.FromCard(game.Player1, Cards.FromId("CS2_168"), zone: game.Player1.BoardZone); friendly.IsExhausted = false;
    var hurtFriendly = (Minion)Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.BoardZone); hurtFriendly.Damage = 1; hurtFriendly.IsExhausted = false;
    var enemy = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_168"), zone: game.Player2.BoardZone); enemy.IsExhausted = false;
    var hurtEnemy = (Minion)Entity.FromCard(game.Player2, Cards.FromId("CS2_182"), zone: game.Player2.BoardZone); hurtEnemy.Damage = 1; hurtEnemy.IsExhausted = false;
    return game;
}

static void Smoke(Card card, HashSet<string> allowedGenerated)
{
    var game = Fixture(card.Class == CardClass.NEUTRAL ? CardClass.MAGE : card.Class);
    if (card.Id == "EX1_105") for (int index = 0; index < 8; index++) Entity.FromCard(game.Player1, Cards.FromId("CS2_182"), zone: game.Player1.HandZone);
    var beforeIds = game.IdEntityDic.Values.Select(entity => entity.Card?.Id).Where(id => !string.IsNullOrEmpty(id)).ToHashSet(StringComparer.Ordinal);
    var source = Entity.FromCard(game.Player1, card, zone: game.Player1.HandZone);
    var options = game.Player1.Options().Where(task => task.Source?.Id == source.Id).ToList();
    Check(options.Count > 0, "no legal play: " + card.Id);
    var selected = options.FirstOrDefault(task => task.Target?.Controller == game.Player2 && task.Target is Minion)
        ?? options.FirstOrDefault(task => task.Target != null) ?? options[0];
    string before = Fingerprint(game);
    Check(game.Process(selected), "engine rejected legal play: " + card.Id);
    Check(Fingerprint(game) != before, "play left state unchanged: " + card.Id);
    string[] unknown = game.IdEntityDic.Values.Select(entity => entity.Card?.Id)
        .Where(id => !string.IsNullOrEmpty(id) && !beforeIds.Contains(id) && id != card.Id && !allowedGenerated.Contains(id))
        .Distinct(StringComparer.Ordinal).ToArray();
    Check(unknown.Length == 0, "generated ID outside closure for " + card.Id + ": " + string.Join(",", unknown));
}

static string Fingerprint(Game game) => string.Join("|", game.IdEntityDic.Values.OrderBy(entity => entity.Id)
    .Select(entity => $"{entity.Id}:{entity.Card?.Id}:{entity[GameTag.ZONE]}:{entity[GameTag.ATK]}:{entity[GameTag.HEALTH]}:{entity[GameTag.DAMAGE]}:{entity[GameTag.CONTROLLER]}"));

static PlayerTask Play(Game game, string id, Func<PlayerTask, bool> choose = null)
{
    var source = Entity.FromCard(game.Player1, Cards.FromId(id), zone: game.Player1.HandZone);
    var options = game.Player1.Options().Where(task => task.Source?.Id == source.Id).ToList();
    var selected = choose == null ? options.First() : options.First(choose);
    Check(game.Process(selected), "scenario process failed: " + id);
    return selected;
}

static void Scenario(string name, Action test)
{
    try { test(); }
    catch (Exception error) { throw new Exception("scenario failed: " + name, error); }
}

static void Clear<T>(SabberStoneCore.Model.Zones.Zone<T> zone) where T : IPlayable
{
    foreach (var entity in zone.ToArray()) zone.Remove(entity);
}

static void Switch(Game game)
{
    Check(game.Process(EndTurnTask.Any(game.CurrentPlayer)), "turn transition rejected");
}

static PlayerTask PlayFor(Game game, SabberStoneCore.Model.Entities.Controller controller, string id, Func<PlayerTask, bool> choose = null)
{
    controller.BaseMana = 10; controller.UsedMana = 0;
    var source = Entity.FromCard(controller, Cards.FromId(id), zone: controller.HandZone);
    var options = controller.Options().Where(task => task.Source?.Id == source.Id).ToList();
    var selected = choose == null ? options.First() : options.First(choose);
    Check(game.Process(selected), "scenario process failed: " + id);
    return selected;
}

static void Attack(Game game, SabberStoneCore.Model.Entities.Controller controller, Minion source, ICharacter target)
{
    source.IsExhausted = false;
    Check(game.Process(MinionAttackTask.Any(controller, source, target)), "attack rejected: " + source.Card.Id);
}

static void SecretScenario(string name, CardClass ownClass, string secretId, Action<Game, Minion, Minion> trigger)
{
    Scenario("secret " + name, () => {
        var game = Fixture(ownClass); Play(game, secretId);
        Check(game.Player1.SecretZone.Count == 1 && game.Player1.SecretZone[0].Card.Id == secretId, name + " placement");
        Switch(game);
        Check(game.Player1.SecretZone.Count == 1, name + " triggered before relevant opponent action");
        Clear(game.Player2.HandZone); game.Player2.BaseMana = 10; game.Player2.UsedMana = 0;
        var attacker = game.Player2.BoardZone.First(); var defender = game.Player1.BoardZone.First();
        trigger(game, attacker, defender);
        Check(game.Player1.SecretZone.Count == 0, name + " did not activate");
    });
}
