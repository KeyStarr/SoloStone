using System.Text.RegularExpressions;

// Source check for the "a card and its reprint are one card" rule (#225), like UntouchableBoardSourceCheck. Every rule path under
// engine/SabberStoneCore/src that asks "the same card?" uses Card.Identity / Card.IsSameCardAs / CardIdentity.Comparer. This check
// fails on a raw card comparison: `Card.Id`/`Card.AssetId` compared with a non-literal, GroupBy on a card id or dbf, Select(card).Distinct(),
// Select(card id).Distinct(), and Contains(card id). A site that must stay exact (an enchantment id, an entity pairing, a lookup of one
// printing, a catalog of ids) is on Allow with a one-line reason. An entry that matches nothing fails too, so the list cannot rot.
internal static class ReprintIdentitySourceCheck
{
    internal sealed record Finding(string File, int Line, string Snippet);

    // (file under engine/SabberStoneCore/src, a fragment of the line, why the exact printing is right).
    private static readonly (string File, string Fragment, string Reason)[] Allow =
    {
        ("Tasks/SpecificTask.cs", "p.Card.AssetId != original", "transform guard: is this entity still the card it started as"),
        ("Tasks/SpecificTask.cs", "minions[j].Card.AssetId != id", "lookup of the minion that carries one given dbf"),
        ("Auras/AdjacentAura.cs", "enchantments[i].Card.AssetId == cardId", "aura to enchantment link by enchantment dbf"),
        ("Auras/Aura.cs", "enchantments[i].Card.AssetId == cardId", "aura to enchantment link by enchantment dbf"),
        ("Conditions/SelfCondition.cs", "me.Card.Id == cardId", "IsCardId names one printing by literal id"),
        ("Model/HistoryFacts.Extensions.cs", "scope.Card.Id!=card.Id", "transfer bookkeeping on the very entity being moved"),
        ("Model/DarkGifts.cs", "e.Card.Id != except", "enchantment id filter"),
        ("Model/Rewind/RewindRules.cs", "source.Card.Id != t.SourceCardId", "rewind validation of the recorded source printing"),
        ("Model/Entities/Entity.cs", "p.Card.Id == options[", "choose-one option lookup by exact option card"),
        ("Actions/DecisionSpell.cs", "source.Card.AssetId != completion.DefinitionDbfId", "witness of the exact definition a decision was recorded with"),
        ("Actions/CostRules.cs", "e.Card.Id == original.Card.Id", "enchantment ordinal match when copying"),
        ("Actions/AutonomousTurnRules.cs", "Card.Id == ability", "lookup of a hero power by id"),
        ("Actions/CombatRules.cs", "FyrakkTheBlazing.DbfId", "literal dbf of a card with no reprint"),
        ("Actions/ShatterRules.cs", "Card.Id == definition.ParentId", "combine-half family by exact ids"),
        ("Actions/ShatterRules.cs", "family.FirstHalfId", "combine-half family by exact ids"),
        ("Actions/ShatterRules.cs", "card.Card.Id == target.Id", "ChangeEntity no-op: the transform target is one exact card"),
        ("Actions/CardStateBoundary.cs", "card.Card.Id == target.Id", "ChangeEntity no-op: the transform target is one exact card"),
        ("Actions/QuestRules.cs", "e.Card.Id == card.Id", "enchantment id"),
        ("Actions/QuestRules.cs", "e.Card.Id == enchantmentId", "enchantment id"),
        ("Actions/QuestRules.cs", "TemporaryPlayQuests.Contains(quest.Card.AssetId)", "catalog of quest dbfs, a lookup by id"),
        ("Actions/QuestRules.cs", "CorpseSpendQuests.Contains(quest.Card.AssetId)", "catalog of quest dbfs, a lookup by id"),
        ("Actions/QuestRules.cs", "c.Card.Id == pair.", "quest component pair by literal ids"),
        ("Actions/DeckbuildingRules.cs", "p.Card.Id == Beatrix", "literal id of one card with no reprint"),
        ("Actions/DeckbuildingRules.cs", "p.Card.Id == offer.Card.Id", "pairs one offered entity with its Underbelly slot; the offer is a slot's own card"),
        ("Actions/RepeatRules.cs", "entity.Card.AssetId == owner[PowerTargetDefinition]", "witness of the exact definition a repeat recorded"),
        ("Actions/RepeatRules.cs", "entity.Card.AssetId != spell[TargetDefinition]", "witness of the exact definition a repeat recorded"),
        ("Actions/StartOfGameRules.cs", "NeverOpening.Contains(e.Card.AssetId)", "catalog of dbfs, a lookup by id"),
        ("Actions/AssembleRules.cs", "result.Card.Id != definition.Id", "payload summon: the exact generated definition"),
        ("Actions/ImbueRules.cs", "power.Card.Id == id", "hero power id lookup"),
        ("Actions/CthunRules.cs", "Catalog.Contains(e.Card.Id)", "catalog of C'Thun ability tokens"),
        ("Actions/BasicHeroPowerRules.cs", "p.Card.Id == id", "hero power lookup by id"),
        ("Actions/DiscoverRealRules.cs", "original.Card.AssetId == offer[WatchedDefinition]", "witness tag: pairs a retained entity with its offer"),
        ("Actions/DiscoverRealRules.cs", "p.Card.Id == offer.Card.Id", "pairs an offer with the exact retained entity it came from; offers are distinct by identity"),
        ("Actions/ColossalRules.cs", "RefillDeclarations.Contains(body.Card.Id)", "catalog of ids, a lookup by id"),
        ("Actions/ColossalRules.cs", "ColossalAppendageIds?.Contains(appendage.Card.Id)", "appendage tokens of one exact body"),
        ("Actions/HandProgressRules.cs", "e.Card.Id == enchantmentId", "enchantment id"),
        ("CardSets/Client/Sets/CORE/DeathKnight/HarbingerOfWinter.cs", "LegacyFrostSpells.Contains(p.Card.AssetId)", "legacy school list: a reprint carries its SPELL_SCHOOL tag"),
        ("CardSets/Client/Sets/CORE/PriestPaladin/VeteranWarmedic.cs", "LegacyHolySpells.Contains(p.Card.AssetId)", "legacy school list: a reprint carries its SPELL_SCHOOL tag"),
        ("CardSets/Client/Sets/THE_LOST_CITY/VolcanicThrasher.cs", "LegacyFireSpells.Contains(p.Card.AssetId)", "legacy school list: a reprint carries its SPELL_SCHOOL tag"),
        ("CardSets/Client/Sets/THE_LOST_CITY/MechanizedMagma.cs", "LegacyFireSpells.Contains(p.Card.AssetId)", "legacy school list: a reprint carries its SPELL_SCHOOL tag"),
        ("CardSets/Client/Sets/EMERALD_DREAM/GroveShaper.cs", "LegacyNatureSpells.Contains(me.Card.AssetId)", "legacy school list: a reprint carries its SPELL_SCHOOL tag"),
        ("CardSets/Client/Sets/EMERALD_DREAM/Overheat.cs", "LegacyNatureSpells.Contains(p.Card.AssetId)", "legacy school list: a reprint carries its SPELL_SCHOOL tag"),
        ("CardSets/Client/Sets/EMERALD_DREAM/ScorchingWinds.cs", "LegacyFireSpells.Contains(p.Card.AssetId)", "legacy school list: a reprint carries its SPELL_SCHOOL tag"),
        ("CardSets/Client/Sets/EMERALD_DREAM/FelfireBlaze.cs", "LegacyFelSpells.Contains(p.Card.AssetId)", "legacy school list: a reprint carries its SPELL_SCHOOL tag"),
        ("CardSets/Client/Sets/EMERALD_DREAM/LivingFlame.cs", "LegacyFireSpells.Contains(me.Card.AssetId)", "legacy school list: a reprint carries its SPELL_SCHOOL tag"),
        ("CardSets/Client/Sets/THE_LOST_CITY/OddMap.cs", ".Select(p => p.Card).Distinct()", "the other offers of one discover, already different cards"),
        ("CardSets/Client/Sets/THE_LOST_CITY/OddMap.cs", ".Select(e => e.Card).Distinct()", "the stored offers of one discover, already different cards"),
        ("CardSets/Client/Sets/TIME_TRAVEL/FacelessEnigma.cs", "selected.Card.AssetId == stack.Number1", "which of the two stored offers was chosen: pairs an entity with its offer"),
        ("CardSets/Client/Sets/TIME_TRAVEL/LadyAzshara.cs", "location.Card.Id==keep", "named locations of one card family"),
        ("CardSets/Client/Sets/TIME_TRAVEL/LadyAzshara.cs", "location.Card.Id==destroy", "named locations of one card family"),
        ("CardSets/Client/Sets/TIME_TRAVEL/DejaVu.cs", "p.Card.Id == offer.Card.Id", "pairs an offer with the stored copy it came from; offers are distinct by identity"),
        ("CardSets/Client/Sets/TIME_TRAVEL/DejaVu.cs", "p.Card.Id == selected?.Card.Id", "pairs the chosen offer with the stored copy it came from"),
        ("CardSets/Client/Sets/TIME_TRAVEL/RoyalInformant.cs", "p.Card.Id == offer.Card.Id", "pairs an offer with the stored copy it came from"),
        ("CardSets/Client/Sets/TIME_TRAVEL/TalanjiOfTheGraves.cs", "Boons.Contains(e.Card.Id)", "enchantment ids"),
        ("CardSets/Client/Sets/TIME_TRAVEL/TalanjiOfTheGraves.cs", "e.Card.Id==Boons[i]", "enchantment ids"),
        ("CardSets/Client/Sets/EMERALD_DREAM/Shaladrassil.cs", "Ysera.Contains(m.Card.Id)", "a hand-written family that lists every Ysera printing"),
        ("CardSets/Client/Sets/CATACLYSM/GennCursedKing.cs", "power.Card.Id == BasicHeroPowerRules.PowerId", "hero power id"),
        ("Actions/AssembleRules.cs", "c.DbfId == sourceDbfId", "pool declaration lookup by the declaring card's dbf"),
        ("Actions/CompositeLocationRules.cs", "card.Cost).Distinct()", "distinct costs, not cards"),
        ("Actions/DiscoverRealRules.cs", "watch[WatchedDefinition] == played.Card.AssetId", "witness tag of the exact definition a watch recorded"),
        ("Actions/DiscoverRealRules.cs", "d.DbfId == (recipeDbfId ?? source.Card.AssetId)", "pool declaration lookup by the declaring card's dbf"),
        ("Actions/ShatterRules.cs", "Shatter?.ParentId == card.Card.Id", "combine-half family by exact ids"),
        ("Actions/SwapRules.cs", "selected.Contains(card)", "entities, not cards"),
        ("Actions/SwapRules.cs", "ownAuras.Contains(card)", "entities, not cards"),
        ("CardSets/Client/Sets/CATACLYSM/CataclysmHighlightConditions.cs", "card.Id != source.Id", "entity ids"),
        ("CardSets/Client/Sets/CATACLYSM/SylvanassTriumph.cs", "p.DbfId == DbfId", "history of this very card (literal self)"),
        ("CardSets/Client/Sets/EMERALD_DREAM/Ysondre.cs", "f.DbfId == DbfId", "history of this very card (literal self)"),
        ("CardSets/Client/Sets/ESCAPEFROM_VIOLET_HOLD/IridaSinseeker.cs", "card.Id != survivor.Id", "entity ids of deck entities"),
        ("CardSets/Client/Sets/ESCAPEFROM_VIOLET_HOLD/VoidSoul.cs", "c.DbfId == DbfId", "history of this very card (literal self)"),
        ("CardSets/Client/Sets/TIME_TRAVEL/RangerGeneralSylvanas.cs", "f.DbfId == first", "two named partner cards with no reprint"),
        ("Effects/DiscoverEntitiesWithDarkGiftThenContinueTask.cs", "p.Card.Identity).Distinct()", "already identity keys"),
        ("Effects/EffectContext.cs", "card.DbfId == (recipeDbfId ?? Source.Card.AssetId)", "pool declaration lookup by the declaring card's dbf"),
        ("Kettle/PowerHistory.cs", "Entity.Id == card.Id", "entity ids"),
        ("Loader/AssembleCatalog.cs", "entry.DbfId == card.AssetId", "catalog lookup of one exact printing"),
        ("Loader/ClientOverlay.cs", "c.AssetId == definition.DbfId", "overlay load integrity check"),
        ("Loader/TargetingPredicates.cs", "ent[m] == span[i].Card.AssetId", "targeting table lookup by dbf"),
        ("Model/DarkGifts.cs", "g.SpellId == spell.Card.Id", "gift registry lookup by gift spell id"),
        ("Model/Entities/Minion.cs", "card.AssetId == enchantment.Card.AssetId", "aura to enchantment link"),
        ("Model/OfflinePoolAdmission.cs", "id != source.CardId", "pool registry integrity"),
        ("Model/OfflinePoolPolicy.cs", "id != dependency.DbfId", "pool registry integrity"),
        ("Model/OfflinePoolPolicy.cs", "pair.Value == dependency.DbfId", "pool registry integrity"),
        ("Model/OfflinePoolPolicy.cs", "card.AssetId != member.DbfId", "pool registry integrity"),
        ("Model/OfflinePoolPolicy.cs", "card.AssetId != dependency.DbfId", "pool registry integrity"),
        ("Model/Zones/HandZone.cs", "e.Id == card.Id", "entity ids"),
        ("Tasks/SimpleTasks/DiscoverTask.cs", "cardsToDiscover[0].Distinct().ToArray()", "the frozen legacy-pool branch keeps its exact draws"),
        ("Tasks/SpecificTask.cs", "id == m.Card.Id", "legacy entourage token ids"),
        ("Tasks/SpecificTask.cs", "nextId == currentPower.Card.Id", "hero power id, Dr. Boom"),
    };

    internal static void Run()
    {
        SelfTest();
        string src = EngineSrc();
        var files = Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal).ToArray();
        if (files.Length == 0) throw new Exception("ReprintIdentitySourceCheck: no .cs file under " + src);
        var found = new List<Finding>();
        var used = new HashSet<int>();
        foreach (var f in files)
        {
            string rel = Path.GetRelativePath(src, f).Replace('\\', '/');
            foreach (var x in Scan(File.ReadAllText(f), rel))
            {
                int hit = Array.FindIndex(Allow, a => a.File == rel && x.Snippet.Contains(a.Fragment, StringComparison.Ordinal));
                if (hit >= 0) used.Add(hit); else found.Add(x);
            }
        }
        var stale = Enumerable.Range(0, Allow.Length).Where(i => !used.Contains(i)).Select(i => $"  {Allow[i].File}: {Allow[i].Fragment}").ToArray();
        var errors = new List<string>();
        if (found.Count != 0)
            errors.Add("ReprintIdentitySourceCheck: " + found.Count + " raw card comparison(s). Use Card.IsSameCardAs, Card.Identity or CardIdentity.Comparer, "
                + "or add the site to Allow with a reason.\n" + string.Join("\n", found.Select(x => $"  {x.File}:{x.Line}: {x.Snippet}")));
        if (stale.Length != 0) errors.Add("ReprintIdentitySourceCheck: Allow entries that match no line (remove them):\n" + string.Join("\n", stale));
        if (errors.Count != 0) throw new Exception(string.Join("\n", errors));
        Console.WriteLine($"PASS reprint identity source check: {files.Length} files, 0 raw card comparisons, {Allow.Length} allowlisted exact-printing sites");
    }

    private static string EngineSrc([System.Runtime.CompilerServices.CallerFilePath] string here = "")
    {
        foreach (string start in new[] { Path.GetDirectoryName(here), Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            for (var dir = string.IsNullOrEmpty(start) ? null : new DirectoryInfo(start); dir != null; dir = dir.Parent)
            {
                string candidate = Path.Combine(dir.FullName, "engine", "SabberStoneCore", "src");
                if (Directory.Exists(candidate)) return candidate;
            }
        throw new Exception("ReprintIdentitySourceCheck: cannot find engine/SabberStoneCore/src from " + here);
    }

    // A comparison with a number or a string literal names one card (or an enchantment) on purpose and is not a same-card test.
    private static readonly Regex Compare = new(@"\bCard\??\.(?:Id|AssetId)\s*(?:==|!=)\s*(?![\s""\d@-])", RegexOptions.Compiled);
    private static readonly Regex Group = new(@"GroupBy\([^;]*?\bCard\??\.(?:Id|AssetId)\b|GroupBy\(\s*\w+\s*=>\s*\w+\.DbfId\b", RegexOptions.Compiled);
    private static readonly Regex Distinct = new(@"Select\([^;]*?(?:\.Card|Card\??\.(?:Id|AssetId))\)\s*\.Distinct\(\)", RegexOptions.Compiled);
    // Wider shapes: a card id or dbf on the right of a comparison, a history DbfId compared with a variable, ids of two card-named values,
    // Distinct() and Contains(card) on Card values.
    private static readonly Regex CompareRight = new(@"(?<![\w""])(?!\d)[\w.?\[\]]+\s*(?:==|!=)\s*[\w.?\[\]]*\bCard\??\.(?:Id|AssetId)\b", RegexOptions.Compiled);
    private static readonly Regex DbfCompare = new(@"\.DbfId\s*(?:==|!=)\s*(?![\s""\d@-])|(?<![\d""])\b[\w.?]+\s*(?:==|!=)\s*[\w.?]*\.DbfId\b", RegexOptions.Compiled);
    private static readonly Regex CardNamed = new(@"\b\w*[cC]ard\w*\??\.(?:Id|AssetId)\s*(?:==|!=)\s*\w+\??\.(?:Id|AssetId)\b|\b\w+\??\.(?:Id|AssetId)\s*(?:==|!=)\s*\w*[cC]ard\w*\??\.(?:Id|AssetId)\b", RegexOptions.Compiled);
    private static readonly Regex DistinctCards = new(@"\b(?:Cards|DeckCards|cards|candidates|pool|offers)\b[^;]*?\.Distinct\(\)|Card\[\][^;]*?\.Distinct\(\)", RegexOptions.Compiled);
    private static readonly Regex ContainsCard = new(@"\.Contains\((?:card|pick|candidate|chosen|offer)\)", RegexOptions.Compiled);
    private static readonly Regex Contains = new(@"\.Contains\([^;()]*?\bCard\??\.(?:Id|AssetId)\)", RegexOptions.Compiled);

    internal static List<Finding> Scan(string source, string file)
    {
        var findings = new List<Finding>();
        var lines = source.Replace("\r\n", "\n").Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            int comment = line.IndexOf("//", StringComparison.Ordinal);
            string code = comment >= 0 && !line.Substring(0, comment).Contains('"') ? line.Substring(0, comment) : line;
            if (Compare.IsMatch(code) || Group.IsMatch(code) || Distinct.IsMatch(code) || Contains.IsMatch(code)
                || CompareRight.IsMatch(code) || DbfCompare.IsMatch(code) || CardNamed.IsMatch(code) || DistinctCards.IsMatch(code) || ContainsCard.IsMatch(code))
                findings.Add(new Finding(file, i + 1, line.Trim()));
        }
        return findings;
    }

    private static void SelfTest()
    {
        void Expect(string sample, int flagged, string what)
        {
            int n = Scan(sample, "sample.cs").Count;
            if (n != flagged) throw new Exception($"ReprintIdentitySourceCheck self-test '{what}': expected {flagged} finding(s), got {n}");
        }
        Expect("if (a.Card.Id == b.Card.Id) { }", 1, "id compared with an id");
        Expect("if (a.Card.AssetId != b.Card.AssetId) { }", 1, "dbf compared with a dbf");
        Expect("if (a.Card.Id == other.Id) { }", 1, "id compared with a variable");
        Expect("if (a.Card.Id == \"EX1_289\") { }", 0, "literal id");
        Expect("if (a.Card.AssetId == 621) { }", 0, "literal dbf");
        Expect("if (a.Card.IsSameCardAs(b.Card)) { }", 0, "the helper");
        Expect("var g = zone.GroupBy(p => p.Card.Id);", 1, "GroupBy on the card id");
        Expect("var g = zone.GroupBy(p => p.Card.Identity);", 0, "GroupBy on the identity");
        Expect("var g = facts.GroupBy(f => f.DbfId);", 1, "GroupBy on a history dbf");
        Expect("var d = zone.Select(p => p.Card).Distinct().ToArray();", 1, "Distinct on cards");
        Expect("var d = zone.Select(p => p.Card.Id).Distinct().ToArray();", 1, "Distinct on card ids");
        Expect("var d = zone.Select(p => p.Card).Distinct(CardIdentity.Comparer).ToArray();", 0, "Distinct with the comparer");
        Expect("var d = zone.Select(p => p.Cost).Distinct().ToArray();", 0, "Distinct on costs");
        Expect("if (ids.Contains(p.Card.AssetId)) { }", 1, "Contains of a dbf");
        Expect("if (ids.Contains(p.Card.Identity)) { }", 0, "Contains of an identity");
        Expect("if (x) { } // a.Card.Id == b.Card.Id", 0, "comment");
        Expect("f.Count(f => f.DbfId == p.Card.AssetId)", 1, "history dbf against a card dbf (right side)");
        Expect("if (dbf == p.Card.AssetId) { }", 1, "variable against a card dbf");
        Expect("if (f.DbfId == other) { }", 1, "history dbf against a variable");
        Expect("if (f.DbfId == 125983) { }", 0, "history dbf against a literal");
        Expect("if (card.Id == other.Id) { }", 1, "ids of a card-named value");
        Expect("var d = cards.Distinct().ToArray();", 1, "Distinct on cards");
        Expect("if (list.Contains(card)) { }", 1, "Contains of a card");
        Expect("if (e.Id == \"X\") { }", 0, "literal id on a plain value");
    }
}
