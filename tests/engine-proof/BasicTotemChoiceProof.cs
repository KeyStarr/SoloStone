using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using L = LocationProofs;

// Totemic Slam (AT_132_SHAMAN) and Kobold Hermit (LOOT_062) offer the basic-totem choice cards AT_132_SHAMANa-d. Those only
// display the offer: they have no Totem race, keywords or effects. Before the fix the choice card itself was summoned, so a
// "Stoneclaw Totem" had no Taunt, Wrath of Air no Spell Damage, Healing Totem never healed, and Thunder Bluff Valiant did
// not count any of them as a Totem. Picking one now summons the basic totem it shows.
internal static class BasicTotemChoiceProof
{
    private static readonly (string Choice, string Totem)[] Pairs =
    {
        ("AT_132_SHAMANa", "NEW1_009"), ("AT_132_SHAMANb", "CS2_050"), ("AT_132_SHAMANc", "CS2_051"), ("AT_132_SHAMANd", "CS2_052"),
    };

    private static string TotemOf(string choice) => Pairs.Single(pair => pair.Choice == choice).Totem;

    // Plays Kobold Hermit and picks the given choice card; returns the summoned minion and the picked offer.
    private static (Minion Totem, IPlayable Offer) Hermit(Game g, Controller p, string choice)
    {
        var hermit = Entity.FromCard(p, Cards.FromId("LOOT_062"), zone: p.HandZone);
        p.UsedMana = 0;
        L.Check(g.Process(PlayCardTask.Any(p, hermit)), "play Kobold Hermit");
        L.Check(p.Choice != null && p.Choice.Choices.Count == 4
            && p.Choice.Choices.Select(id => g.IdEntityDic[id].Card.Id).OrderBy(id => id).SequenceEqual(Pairs.Select(pair => pair.Choice)),
            "Kobold Hermit offers the four choice cards");
        int pick = p.Choice.Choices.Single(id => g.IdEntityDic[id].Card.Id == choice);
        var offer = g.IdEntityDic[pick];
        int before = p.BoardZone.Count;
        L.Check(g.Process(ChooseTask.Pick(p, pick)), "pick " + choice);
        L.Check(p.BoardZone.Count == before + 1, "one totem summoned for " + choice);
        return (p.BoardZone[p.BoardZone.Count - 1], offer);
    }

    [GroupProof("basic-totem-choice")]
    internal static void Run()
    {
        foreach (bool history in new[] { true, false })
        {
            // Each choice summons its basic totem, a real Totem; the choice card stays set aside.
            foreach (var (choice, totem) in Pairs)
            {
                var g = L.Game(history: history); var p = g.Player1;
                var (summoned, offer) = Hermit(g, p, choice);
                L.Check(summoned.Card.Id == totem && summoned.IsRace(Race.TOTEM) && summoned != offer,
                    $"{choice} summons {totem} (got {summoned.Card.Id})");
                L.Check(!p.BoardZone.Contains(offer) && offer.Zone?.Type == Zone.SETASIDE, $"{choice} stays set aside");
            }

            // Stoneclaw Totem has Taunt: an enemy minion cannot attack past it.
            {
                var g = L.Game(history: history); var p = g.Player1;
                var (stoneclaw, _) = Hermit(g, p, "AT_132_SHAMANc");
                var ogre = L.Minion(g.Player2, "CS2_200");
                L.Check(stoneclaw.HasTaunt, "Stoneclaw Totem has Taunt");
                L.End(g);
                L.Check(!g.Process(MinionAttackTask.Any(g.Player2, ogre, p.Hero)), "Taunt blocks an attack on the hero");
                L.Check(g.Process(MinionAttackTask.Any(g.Player2, ogre, stoneclaw)), "the Ogre may attack the Stoneclaw");
            }

            // Wrath of Air Totem gives Spell Damage +1.
            {
                var g = L.Game(history: history); var p = g.Player1;
                Hermit(g, p, "AT_132_SHAMANd");
                L.Check(p.CurrentSpellPower == 1, $"Wrath of Air Totem gives Spell Damage +1 (got {p.CurrentSpellPower})");
                var fireball = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.HandZone);
                p.UsedMana = 0;
                L.Check(g.Process(PlayCardTask.SpellTarget(p, fireball, g.Player2.Hero)), "Fireball the enemy hero");
                L.Check(g.Player2.Hero.Health == 23, $"Fireball deals 7 (hero at {g.Player2.Hero.Health})");
            }

            // Healing Totem restores 1 Health to friendly minions at the end of the turn.
            {
                var g = L.Game(history: history); var p = g.Player1;
                var ogre = L.Minion(p, "CS2_200"); ogre.Damage = 3;
                Hermit(g, p, "AT_132_SHAMANa");
                L.End(g);
                L.Check(ogre.Damage == 2, $"Healing Totem heals 1 at end of turn (damage {ogre.Damage})");
            }

            // Thunder Bluff Valiant's Inspire gives a chosen totem +2 Attack.
            {
                var g = L.Game(history: history); var p = g.Player1;
                var valiant = L.Minion(p, "AT_049");
                var (stoneclaw, _) = Hermit(g, p, "AT_132_SHAMANc");
                new ReplaceHeroPower(Cards.FromId("CS2_049")).Process(g, p, p.Hero, null);
                p.UsedMana = 0;
                L.Check(g.Process(HeroPowerTask.Any(p)), "Totemic Call");
                L.Check(stoneclaw.AttackDamage == 2, $"Inspire gives the chosen Stoneclaw +2 Attack (attack {stoneclaw.AttackDamage})");
            }

            // Totemic Slam: whichever choice is offered summons its basic totem.
            {
                var g = L.Game(history: history); var p = g.Player1;
                new ReplaceHeroPower(Cards.FromId("AT_132_SHAMAN")).Process(g, p, p.Hero, null);
                L.Check(g.Process(HeroPowerTask.Any(p)), "Totemic Slam");
                L.Check(p.Choice != null && p.Choice.Choices.All(id => Pairs.Any(pair => pair.Choice == g.IdEntityDic[id].Card.Id)),
                    "Totemic Slam offers choice cards");
                string choice = g.IdEntityDic[p.Choice.Choices[0]].Card.Id;
                L.Check(g.Process(ChooseTask.Pick(p, p.Choice.Choices[0])), "pick " + choice);
                L.Check(p.BoardZone.Count == 1 && p.BoardZone[0].Card.Id == TotemOf(choice) && p.BoardZone[0].IsRace(Race.TOTEM),
                    $"Totemic Slam's {choice} summons {TotemOf(choice)}");
            }
        }
        Console.WriteLine("PASS basic totem choices summon the basic totems");
    }
}
