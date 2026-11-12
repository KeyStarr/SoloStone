using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RangerGeneralSylvanasProof
{
    internal static void Run() => StandardSetsProof.Case("TIME_609", (g, p, id) => {
        Core2026Proof.Check(FabledRules.Bundle(id).SequenceEqual(new[] { "TIME_609t1", "TIME_609t2" }), "exact Windrunner bundle");
        var friend = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_200");
        var body = (Minion)Play(g, p, id);
        Core2026Proof.Check(body.AttackDamage == 2 && body.Health == 4 && friend.Damage == 0 && enemy.Damage == 2
            && p.Opponent.Hero.Damage == 2 && p.Hero.Damage == 0, "base Battlecry damages all enemies only");
        // Summoning either partner, even on our side, is not playing it.
        ParkBoard(p); Summon(p, "TIME_609t1"); Summon(p, "TIME_609t2");
        Play(g, p, id); Core2026Proof.Check(enemy.Damage == 4, "summoned partners do not qualify");
        ParkBoard(p); Play(g, p, "TIME_609t1"); AnswerAll(g, 2);
        Play(g, p, "TIME_609t1"); AnswerAll(g, 2);
        int damage = p.Opponent.Hero.Damage;
        ParkBoard(p); Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 4, "duplicate Alleria plays count once");
        ParkBoard(p); End(g); Play(g, p.Opponent, "TIME_609t2"); End(g);
        damage = p.Opponent.Hero.Damage; Play(g, p, id);
        Core2026Proof.Check(p.Opponent.Hero.Damage == damage + 4, "opponent Vereesa play excluded across turns");
        ParkBoard(p);
        var clone = g.CloneExact(history: true);
        Play(clone, clone.Player1, "TIME_609t2"); ParkBoard(clone.Player1);
        int before = clone.Player2.Hero.Damage;
        Play(clone, clone.Player1, id);
        Core2026Proof.Check(clone.Player2.Hero.Damage == before + 6 && p.BoardZone.IsEmpty, "two distinct partners; clone history isolated");
        Play(g, p, "TIME_609t2"); ParkBoard(p);
        var shield = Summon(p.Opponent, "EX1_008");
        Play(g, p, id);
        Core2026Proof.Check(shield.Zone == p.Opponent.GraveyardZone, "repetitions are separate damage events through Divine Shield");
        body = p.BoardZone.Single(); body.Silence(); Kill(g, body);
        Core2026Proof.Check(p.BoardZone.IsEmpty, "Battlecry installs no persistent trigger");
        FullBoardRejects(g, p, id);
        Lean_DeathsAfterBattlecry(g, p, id);
    });

    private static void Lean_DeathsAfterBattlecry(Game g, Controller p, string id)
    {
        // Coordinator ruling (fix round 1): repeats form ONE Battlecry; deaths/Deathrattles resolve after the phase, not between waves.
        ParkBoard(p); ParkBoard(p.Opponent);
        p.Opponent.Hero.Health = 100;
        Summon(p, "TIME_856"); // Spell Damage +2 must not scale this fixed-2 Battlecry.
        var aura = Summon(p.Opponent, "CS2_122"); aura.Health = 2;
        var creeper = Summon(p.Opponent, "FP1_002");
        var survivor = Summon(p.Opponent, "CS2_200");
        Core2026Proof.Check(p.CurrentSpellPower == 2 && creeper.Health == 2 && creeper.AttackDamage == 2 && survivor.AttackDamage == 7,
            "Spell Damage +2 and Raid Leader attack aura active; Creeper dies to first wave");
        int waves = 0;
        void Observe(IEntity _)
        {
            waves++;
            Core2026Proof.Check(g.CurrentEventData.EventNumber == 2, "every wave deals fixed 2 despite Spell Damage and aura changes");
            if (waves > 1)
                Core2026Proof.Check(creeper.IsDead && creeper.Zone == p.Opponent.BoardZone
                    && aura.IsDead && aura.Zone == p.Opponent.BoardZone
                    && !p.Opponent.BoardZone.Any(m => m.Card.Id == "FP1_002t"),
                    "Lean_DeathsAfterBattlecry: first-wave deaths stay on board; no Deathrattle summons during later waves");
        }
        p.Opponent.Hero.TakeDamageTrigger += Observe;
        try { Play(g, p, id); }
        finally { p.Opponent.Hero.TakeDamageTrigger -= Observe; }
        var spiders = p.Opponent.BoardZone.Where(m => m.Card.Id == "FP1_002t").ToArray();
        Core2026Proof.Check(waves == 3 && creeper.Zone == p.Opponent.GraveyardZone && aura.Zone == p.Opponent.GraveyardZone
            && survivor.Damage == 6 && survivor.AttackDamage == 6,
            "three fixed-2 waves finish before deaths remove the attack aura");
        Core2026Proof.Check(spiders.Length == 2 && spiders.All(m => m.AttackDamage == 1 && m.Health == 1 && m.Damage == 0)
            && p.Opponent.BoardZone.Count == 3,
            "Lean_DeathsAfterBattlecry: two exact 1/1 Spectral Spiders appear after all waves, unharmed");
    }

    internal static void AnswerAll(Game g, int expected)
    {
        int count = 0;
        while (g.CurrentPlayer.Choice != null)
        {
            var p = g.CurrentPlayer;
            Core2026Proof.Check(++count <= 6, "bounded Discover chain");
            // Keep sequential-Discover fixtures at one hand slot per grant; Shatter has separate lifecycle proofs.
            PlayerDecisionProof.Answer(g, p.Choice.Choices.First(id => g.IdEntityDic[id].Card.Shatter == null));
        }
        Core2026Proof.Check(count == expected && !g.Resolution.HasSuspendedEffect, "exact number of sequential Discovers");
    }

    internal static void FullBoardRejects(Game g, Controller p, string id)
    {
        ParkBoard(p); PoolPilotProof.EmptyHand(p);
        var location = Entity.FromCard(p, Cards.FromId("TIME_044")); p.BoardZone.Add((BoardObject)location);
        for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        p.UsedMana = 0; var card = Add(p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && !p.Options().Any(t => t.Source?.Id == card.Id), "mixed full board prevents Battlecry play");
    }

    internal static void CompanionCase(string id, Action<Game, Controller> scenario)
    {
        var card = Cards.FromId(id);
        Core2026Proof.Check(card.Implemented && card.ExcludeFromLegacyRandomPools && !card.Collectible && FabledRules.IsCompanion(id), "registered excluded companion");
        if (ClientExport.TryCards() is { } export)
        {
            var data = export[id];
            Core2026Proof.Check(card.AssetId == data.GetProperty("dbfId").GetInt32() && card.Cost == data.GetProperty("cost").GetInt32()
                && card.ATK == data.GetProperty("atk").GetInt32() && card.Health == data.GetProperty("health").GetInt32()
                && card.Text == data.GetProperty("text").GetString(), "exact companion identity/stats/text");
            foreach (var tag in data.GetProperty("tags").EnumerateObject())
                Core2026Proof.Check(card.Tags.GetValueOrDefault((GameTag)int.Parse(tag.Name)) == tag.Value.GetInt32(), "exact companion tag " + tag.Name);
        }
        else ClientExport.Skip("Fabled companion identity, text and tags against the client export");
        for (int seed = 1; seed <= 3; seed++)
        {
            var g = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
                Player1HeroClass = CardClass.HUNTER, Player2HeroClass = CardClass.MAGE, History = true, Logging = false,
                Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 27).Concat(new[] { "TIME_609", "TIME_609t1", "TIME_609t2" }.Select(Cards.FromId)).ToList(),
                Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
            g.StartGame();
            foreach (var p in new[] { g.Player1, g.Player2 })
                foreach (var opening in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(opening));
            var drawn = g.Player1.DeckZone.Single(c => c.Card.Id == id);
            Generic.Draw(g.Player1, drawn);
            Core2026Proof.Check(drawn.Zone == g.Player1.HandZone && g.HistoryFacts.Query.StartedInDeck(drawn.Id, g.Player1.Id), "companion drawn as ordinary starting-deck card");
            scenario(g, g.Player1);
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(g.CloneExact(history: true)), "companion scenario clone");
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(Game.LoadSnapshot(g.SaveSnapshot())), "companion scenario snapshot");
        }
        Console.WriteLine("PASS Fabled companion " + id);
    }
}
