using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using L = LocationProofs;

internal static class LocationCardSetProof
{
    internal static void Run()
    {
        var failures = new List<string>();
        void Case(string name, Action test)
        {
            try { test(); }
            catch (Exception e) { failures.Add(name + ": " + e.Message); }
        }
        foreach (bool mixed in new[] { false, true })
        {
            foreach (string id in new[] { "BE_730", "CATA_135" })
                Case($"append {id} mixed={mixed}", () => AppendSpell(id, mixed));
            foreach (string id in new[] { "BE_550", "EDR_492" })
            foreach (bool removed in new[] { false, true })
                Case($"right/fallback {id} mixed={mixed} removed={removed}", () => RightOrAppend(id, mixed, removed));
            foreach (string id in new[] { "CORE_RLK_062", "CORE_TTN_843", "CORE_SCH_181", "CORE_AV_337", "CORE_BAR_878" })
            foreach (bool removed in new[] { false, true })
                Case($"Core right/fallback {id} mixed={mixed} removed={removed}", () => CoreRightOrAppend(id, mixed, removed));
            Case($"LastBoardPosition mixed={mixed}", () => DeathPosition(mixed));
            Case($"death slot mixed={mixed}", () => DeathSlot(mixed));
            Case($"merge append mixed={mixed}", () => AncientOne(mixed));
            Case($"left/right and FreeSpace mixed={mixed}", () => Alanna(mixed));
            Case($"fill board mixed={mixed}", () => FillBoard(mixed));
            Case($"adjacency mixed={mixed}", () => Adjacency(mixed));
            Case($"Herald placement mixed={mixed}", () => HeraldPlacement(mixed));
        }
        Case(nameof(LocationFillsQuestBoard), LocationFillsQuestBoard);
        Case(nameof(LegendaryLocationDiscount), LegendaryLocationDiscount);
        Case(nameof(Unpinned_LootapaloozaPhysicalEdges), Unpinned_LootapaloozaPhysicalEdges);
        Case(nameof(PinnedRule_LocationBlocksAdjacency), PinnedRule_LocationBlocksAdjacency);
        L.Check(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine("PASS location card sets: append/right/left/death/adjacency/edges/capacity/fill/Herald; Unpinned_LootapaloozaPhysicalEdges; PinnedRule_LocationBlocksAdjacency");
    }

    private static IPlayable Play(Controller p, string id, int position = -1)
    {
        p.BaseMana = 10; p.UsedMana = 0;
        var source = Entity.FromCard(p, Cards.FromId(id), zone: p.HandZone);
        L.Check(p.Game.Process(PlayCardTask.Any(p, source, zonePosition: position)), "play " + id);
        return source;
    }

    private static void Order(Controller p, params IPlayable[] expected)
    {
        L.Check(p.BoardZone.Slots.Select(x => x.Id).SequenceEqual(expected.Select(x => x.Id)),
            "physical order: " + string.Join(",", p.BoardZone.Slots.Select(x => x.Card.Id)));
        L.Check(p.BoardZone.Slots.Select(x => x.ZonePosition).SequenceEqual(Enumerable.Range(0, expected.Length)), "physical positions contiguous");
    }

    private static IPlayable[] Prefix(Controller p, bool mixed)
    {
        var first = L.Minion(p);
        return mixed ? new IPlayable[] { first, L.Play(p, "CORE_REV_990", 1) } : new IPlayable[] { first };
    }

    private static void AppendSpell(string id, bool mixed)
    {
        var g = L.Game(); var p = g.Player1;
        var prefix = Prefix(p, mixed);
        Play(p, id);
        string token = id == "BE_730" ? "BE_730t" : "CATA_135t";
        var tokens = p.BoardZone.Where(m => m.Card.Id == token).OrderBy(m => m.Id).ToArray();
        L.Check(tokens.Length == 2, "two spell tokens");
        Order(p, prefix.Concat(tokens).ToArray());
    }

    private static void RightOrAppend(string id, bool mixed, bool removed)
    {
        var g = L.Game(); var p = g.Player1;
        var prefix = Prefix(p, mixed);
        var source = L.Minion(p, id); var right = L.Minion(p);
        // A removed Battlecry source is a supported indirect-resolution path.
        if (removed) { source.Silence(); source.Destroy(); g.DeathProcessingAndAuraUpdate(); }
        source.Card.Power.PowerTask.Process(g, p, source, null, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
        string token = id == "BE_550" ? id : "EDR_492t";
        var tokens = p.BoardZone.Where(m => m.Card.Id == token && m != source).OrderBy(m => m.Id).ToArray();
        L.Check(tokens.Length == (id == "BE_550" ? 2 : 3), "right token count");
        Order(p, removed ? prefix.Append(right).Concat(tokens).ToArray()
            : prefix.Append(source).Concat(tokens).Append(right).ToArray());
    }

    private static void LocationFillsQuestBoard()
    {
        foreach (bool locationLast in new[] { false, true })
        {
            var game = L.Game(); var p = game.Player1;
            var quest = (Spell)Play(p, "TLC_239");
            if (!locationLast) L.Play(p, "CORE_REV_990");
            for (int i = 0; i < (locationLast ? 6 : 5); i++) L.Minion(p);
            L.Check(quest.QuestProgress == 0, "six physical slots do not fill quest board");
            if (locationLast) L.Play(p, "CORE_REV_990");
            else L.Minion(p);
            L.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count == 6 && quest.QuestProgress == 1,
                "mixed board quest fill counts once, locationLast=" + locationLast);
        }
    }

    private static void LegendaryLocationDiscount()
    {
        var game = L.Game(); var p = game.Player1;
        var definition = Cards.FromId("CORE_REV_990").Clone();
        definition.Tags[GameTag.RARITY] = (int)Rarity.LEGENDARY;
        definition.Tags[GameTag.ELITE] = 1; // In-game Legendary identity is the ELITE tag (93d349f92).
        Entity.FromCard(p, definition, zone: p.BoardZone);
        var spell = Entity.FromCard(p, Cards.FromId("CATA_308"), zone: p.HandZone);
        game.AuraUpdate();
        L.Check(spell.Cost == 1, "Medivh's Triumph counts controlled legendary locations");
    }

    private static void CoreRightOrAppend(string id, bool mixed, bool removed)
    {
        var g = L.Game(); var p = g.Player1;
        var prefix = Prefix(p, mixed);
        var source = L.Minion(p, id); var right = L.Minion(p);
        var power = source.Card.Power;
        if (removed) { source.Silence(); source.Destroy(); g.DeathProcessingAndAuraUpdate(); }
        if (id == "CORE_SCH_181")
        {
            Entity.FromCard(p, Cards.FromId("CS2_065"), zone: p.HandZone);
            foreach (var card in p.DeckZone.ToArray()) p.DeckZone.Remove(card);
            Entity.FromCard(p, Cards.FromId("CS2_065"), zone: p.DeckZone);
        }
        var task = id == "CORE_AV_337" ? power.DeathrattleTask
            : id == "CORE_TTN_843" || id == "CORE_BAR_878" ? power.Trigger.SingleTask : power.PowerTask;
        var existing = p.BoardZone.Select(m => m.Id).ToHashSet();
        task.Process(g, p, source, null, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
        var tokens = p.BoardZone.Where(m => !existing.Contains(m.Id)).OrderBy(m => m.Id).ToArray();
        L.Check(tokens.Length == (id == "CORE_TTN_843" || id == "CORE_BAR_878" ? 1 : 2), "Core token count " + id);
        // A departed Deathrattle source keeps its last physical slot; other departed sources append.
        if (removed && id == "CORE_AV_337") Order(p, prefix.Concat(tokens).Append(right).ToArray());
        else Order(p, removed ? prefix.Append(right).Concat(tokens).ToArray()
            : prefix.Append(source).Concat(tokens).Append(right).ToArray());
    }

    private static void DeathPosition(bool mixed)
    {
        var g = L.Game(); var p = g.Player1;
        var prefix = Prefix(p, mixed);
        var right = L.Minion(p); var source = L.Minion(p, "CATA_723");
        source.Destroy(); g.DeathProcessingAndAuraUpdate();
        var tokens = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == source.Id).OrderBy(m => m.Id).ToArray();
        L.Check(tokens.Length == 2 && source.LastBoardPosition == prefix.Length + 1, "death snapshots physical position");
        Order(p, prefix.Append(right).Concat(tokens).ToArray());
    }

    private static void DeathSlot(bool mixed)
    {
        var g = L.Game(); var p = g.Player1;
        var prefix = Prefix(p, mixed);
        var source = L.Minion(p, "TLC_234"); var right = L.Minion(p);
        source.Destroy(); g.DeathProcessingAndAuraUpdate();
        var seedling = p.BoardZone.Single(m => m.Card.Id == "TLC_234t");
        Order(p, prefix.Append(seedling).Append(right).ToArray());
    }

    private static void AncientOne(bool mixed)
    {
        var g = L.Game(); var p = g.Player1;
        var prefix = Prefix(p, mixed);
        L.Minion(p, "OG_173"); L.Minion(p, "OG_173");
        L.End(g);
        var ancient = p.BoardZone.Single(m => m.Card.Id == "OG_173a");
        Order(p, prefix.Append(ancient).ToArray());
    }

    private static void Alanna(bool mixed)
    {
        var g = L.Game(); var p = g.Player1;
        // Three recorded expensive spells; no random targets or summoned entities.
        for (int i = 0; i < 3; i++) Play(p, "CS2_032");
        var prefix = Prefix(p, mixed);
        int free = p.BoardZone.FreeSpace - 1;
        var source = Play(p, "LOOT_535");
        var dragons = p.BoardZone.Where(m => m.Card.Id == "LOOT_535t").OrderBy(m => m.Id).ToArray();
        L.Check(free == (mixed ? 4 : 5) && dragons.Length == 4, "FreeSpace preserves legacy paired summons");
        Order(p, prefix.Concat(new IPlayable[] { dragons[0], dragons[2], source, dragons[3], dragons[1] }).ToArray());
    }

    private static void FillBoard(bool mixed)
    {
        var g = L.Game(); var p = g.Player1;
        var prefix = Prefix(p, mixed);
        var source = Play(p, "TRL_253");
        var copies = p.BoardZone.Where(m => m.Card.Id == "TRL_253" && m != source).OrderBy(m => m.Id).ToArray();
        L.Check(p.BoardZone.IsFull && p.BoardZone.FreeSpace == 0 && copies.Length == (mixed ? 4 : 5), "fill consumes only physical free slots");
        Order(p, prefix.Append(source).Concat(copies).ToArray());
        // Leave two physical slots for the modern IsFull loop.
        foreach (var copy in copies.Take(2)) { copy.Silence(); copy.Destroy(); }
        g.DeathProcessingAndAuraUpdate();
        var spell = Entity.FromCard(p, Cards.FromId("EDR_851p"));
        spell[GameTag.TAG_SCRIPT_DATA_NUM_1] = 3;
        spell.Card.Power.PowerTask.Process(g, p, spell, null, new TaskStack());
        var wisps = p.BoardZone.Where(m => m.Card.Id == "EDR_851t").OrderBy(m => m.Id).ToArray();
        L.Check(wisps.Length == 2 && p.BoardZone.IsFull, "Wisp loop stops at physical capacity");
        var expected = prefix.Append(source).Concat(copies.Skip(2)).Concat(wisps).ToArray();
        Order(p, expected);
        spell.Card.Power.PowerTask.Process(g, p, spell, null, new TaskStack());
        Order(p, expected);
    }

    private static void Adjacency(bool mixed)
    {
        var g = L.Game(); var p = g.Player1;
        var left = L.Minion(p, "CS2_231");
        var wolf = L.Minion(p, "EX1_162"); var right = L.Minion(p, "CS2_231");
        var location = mixed ? L.Play(p, "CORE_REV_990", 1) : null;
        g.AuraUpdate();
        L.Check(left.AttackDamage == (mixed ? 1 : 2) && right.AttackDamage == 2, "physical adjacency buff");
        Order(p, mixed ? new IPlayable[] { left, location, wolf, right } : new IPlayable[] { left, wolf, right });
    }

    private static void HeraldPlacement(bool mixed)
    {
        var g = HeraldProof.New(CardClass.WARLOCK); var p = g.Player1;
        var prefix = Prefix(p, mixed);
        var source = L.Minion(p); var right = L.Minion(p);
        var first = HeraldProof.Herald(g, p, source);
        var second = HeraldProof.Herald(g, p, source);
        Order(p, prefix.Concat(new IPlayable[] { source, first, second, right }).ToArray());
        L.End(g);
        L.Check(right.Zone.Type == Zone.PLAY && second.Zone.Type == Zone.GRAVEYARD, "first Cho'gall consumes second before its trigger");
        Order(p, prefix.Concat(new IPlayable[] { source, first, right }).ToArray());
    }

    private static void PinnedRule_LocationBlocksAdjacency()
    {
        var g = HeraldProof.New(CardClass.WARLOCK); var p = g.Player1;
        var soldier = HeraldProof.Herald(g, p);
        var location = L.Play(p, "CORE_REV_990", 1); var right = L.Minion(p);
        int attack = soldier.AttackDamage;
        L.End(g);
        Order(p, soldier, location, right);
        L.Check(soldier.AttackDamage == attack && !right.ToBeDestroyed, "Cho'gall cannot consume across location");
    }

    private static void Unpinned_LootapaloozaPhysicalEdges()
    {
        foreach (int edge in new[] { -1, 0, 3 })
        {
            var g = L.Game(); var p = g.Player1; var enemy = p.Opponent;
            var left = L.Minion(enemy); var middle = L.Minion(enemy); var right = L.Minion(enemy);
            BoardObject location = edge < 0 ? null : L.Location(enemy, position: edge);
            Play(p, "LOOT_522");
            if (edge == 0) Order(enemy, location, left, middle);
            else if (edge == 3) Order(enemy, middle, right, location);
            else Order(enemy, middle);
        }
        foreach (int minions in new[] { 0, 1 })
        {
            var g = L.Game(); var p = g.Player1; var enemy = p.Opponent;
            var location = L.Location(enemy);
            if (minions == 1) L.Minion(enemy);
            Play(p, "LOOT_522");
            Order(enemy, location);
        }
    }
}
