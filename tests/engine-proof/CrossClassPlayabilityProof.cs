using SabberStoneCore.Actions;
using SabberStoneCore.Config;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

/// <summary>
/// Safeguard against class-gated play blocks (the Herald/Imbue bug class): the same card, in the same fixture, must be
/// playable (IsPlayableByPlayer), offered by Options(), and accepted by the play block for EVERY hero class. Printed card
/// requirements are identical across classes in one fixture, so any divergence is a class guard; each allowed divergence
/// below needs a one-line printed-rule reason.
/// </summary>
internal static class CrossClassPlayabilityProof
{
    internal static readonly CardClass[] Classes = {
        CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN, CardClass.PRIEST, CardClass.ROGUE,
        CardClass.SHAMAN, CardClass.WARLOCK, CardClass.WARRIOR, (CardClass)14, CardClass.DEATHKNIGHT };

    // card id (or prefix ending in '*') -> printed rule that legitimately depends on the player's class.
    // The first run on the old code must list nothing here for Herald/Imbue; add entries only with a printed reason.
    // Playability (IsPlayableByPlayer / Options() / play block) is never allowlisted: it must match for every class.
    private static readonly Dictionary<string, string> Allowlist = new();
    // Effect phase only. The effect itself may legitimately depend on the class.
    private static readonly Dictionary<string, string> EffectAllowlist = new()
    {
        ["EDR_519"] = "Wisprider: 'trigger your hero power' - what happens is each class's own hero power (Mage Fireblast damage, Warrior Armor, Druid/Rogue/Warlock/Demon Hunter their own effects), printed text",
    };

    private static Game Fixture(CardClass cls)
    {
        var g = new Game(new GameConfig { RandomSeed = 1, StartPlayer = 1, Shuffle = false, SkipMulligan = true,
            Logging = false, History = false, FormatType = FormatType.FT_STANDARD,
            Player1HeroClass = cls, Player2HeroClass = CardClass.MAGE,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_182"), 30).ToList() });
        g.StartGame();
        foreach (var p in new[] { g.Player1, g.Player2 })
        {
            foreach (var x in p.HandZone.ToArray()) { p.HandZone.Remove(x); p.SetasideZone.Add(x); }
            p.BaseMana = 10; p.UsedMana = 0;
        }
        // One minion per side so ordinary target requirements are met identically for every class.
        Core2026Proof.Summon(g.Player1, "CS2_182"); Core2026Proof.Summon(g.Player2, "CS2_182");
        return g;
    }

    private static bool Universe(Card c) => c.Collectible && c.Implemented
        && (c.Type == CardType.MINION || c.Type == CardType.SPELL || c.Type == CardType.WEAPON
            || c.Type == CardType.LOCATION || c.Type == CardType.HERO);

    internal static bool KeywordGated(Card c) => c[HeraldRules.Keyword] != 0 || ImbueRules.IsConsumer(c);

    private static string Signature(Game g, Card card, bool full)
    {
        var p = g.Player1;
        IPlayable e = null;
        try
        {
            e = Entity.FromCard(p, card, zone: p.HandZone);
            bool playable = e.IsPlayableByPlayer;
            bool offered = p.Options().OfType<PlayCardTask>().Any(t => t.Source?.Id == e.Id);
            string accepted = "-";
            if (full) accepted = Play(g, e).Accept;
            return $"playable={playable} offered={offered} block={accepted}";
        }
        catch (Exception x) { return "EXC:" + x.GetType().Name; }
        finally
        {
            foreach (var x in p.HandZone.ToArray()) { p.HandZone.Remove(x); p.SetasideZone.Add(x); }
            p.UsedMana = 0; p.BaseMana = 10;
        }
    }

    // The play block itself on a clone (the fixture stays untouched): the first legal option for the card, so a card that needs a
    // target gets one (and a position / Choose One branch). Cards the options do not offer go through the skipPrePhase path.
    // Core = facts no Soldier or Blessing can change (damage dealt, Armor, Lifesteal, mana paid); Full adds boards, hand and
    // graveyard, which Soldier summons also touch (Azshara, Sinestra, Onyxia, Al'Akir), so Full is only compared where the Soldier
    // is the same: the five classes without a Soldier of their own.
    internal static (string Accept, string Core, string Full) Play(Game g, IPlayable e)
    {
        var option = g.Player1.Options().OfType<PlayCardTask>().FirstOrDefault(t => t.Source?.Id == e.Id);
        var clone = g.CloneExact();
        var owner = clone.Player1; var source = clone.IdEntityDic[e.Id];
        var target = option?.ActionTarget == null ? null : clone.IdEntityDic[option.ActionTarget.Id] as IPlayable;
        try
        {
            bool ok = clone.Process(PlayCardTask.Any(owner, source, target, option?.ZonePosition ?? -1, option?.ChooseOne ?? 0, skipPrePhase: option == null));
            if (!ok) return ("N", "", "");
            while (owner.Choice != null) Generic.ChoicePick(owner, clone, owner.Choice.Choices.First());
            clone.DeathProcessingAndAuraUpdate();
        }
        catch (Exception x) { return ("EXC:" + x.GetType().Name, "", ""); }
        var enemy = owner.Opponent;
        string core = $"enemy={enemy.Hero.Damage}/{enemy.Hero.Armor} enemyBoard=" + string.Join(",", enemy.BoardZone.Select(m => m.Card.Id + ":" + m.Damage))
            + $" armor={owner.Hero.Armor} lifesteal={owner.Hero.HasLifeSteal} mana={owner.UsedMana}";
        string full = core + " board=" + string.Join(",", owner.BoardZone.Select(m => m.Card.Id).OrderBy(x => x))
            // Hand by count: Soldier of Sinestra/Onyxia add a card chosen from the hero's OTHER classes / exact cost (printed).
            + " hand=" + owner.HandZone.Count
            + $" atk={owner.Hero.AttackDamage} deck={owner.DeckZone.Count} grave={owner.GraveyardZone.Count}";
        return ("Y", core, full);
    }

    private static readonly string[] SoldierIds = { "CATA_780t", "CATA_525t", "CATA_158t", "CATA_725t", "CATA_565t", "CATA_580t" };
    private static int Count(string text, string key) => int.Parse(System.Text.RegularExpressions.Regex.Match(text, key + @"=(\d+)").Groups[1].Value);
    private static readonly Dictionary<string, (string What, Func<string, string, bool> Check)> Witnesses = new()
    {
        ["CATA_156"] = ("4 damage to the enemy minion", (core, full) => core.Contains("CS2_182:4")),
        ["CATA_530"] = ("Lifesteal this turn", (core, full) => core.Contains("lifesteal=True")),
        ["CATA_561"] = ("two Breezlings in hand", (core, full) => Count(full, "hand") >= 2),
        ["CATA_785"] = ("a Soldier, played by any class", (core, full) => SoldierIds.Any(id => full.Contains(id))),
    };

    internal static readonly CardClass[] NoSoldierClasses = {
        CardClass.DRUID, CardClass.HUNTER, CardClass.MAGE, CardClass.PALADIN, CardClass.PRIEST };

    private static List<string> Effects(Dictionary<CardClass, Game> games, Card card)
    {
        var results = new Dictionary<CardClass, (string Accept, string Core, string Full)>();
        foreach (var cls in Classes)
        {
            var g = games[cls]; var p = g.Player1;
            var e = Entity.FromCard(p, card, zone: p.HandZone);
            try { results[cls] = Play(g, e); }
            finally { foreach (var x in p.HandZone.ToArray()) { p.HandZone.Remove(x); p.SetasideZone.Add(x); } p.UsedMana = 0; p.BaseMana = 10; }
        }
        var found = new List<string>();
        // Absolute, not only differential: failing in all 11 classes (a script that returns early for everybody) must not pass.
        foreach (var pair in results.Where(r => r.Value.Accept.StartsWith("EXC", StringComparison.Ordinal)))
            found.Add($"{pair.Key}: play threw {pair.Value.Accept}");
        // Witnesses: the printed non-Soldier effect really happened, for EVERY class (an early return in all 11 classes is identical, so
        // the differential cannot see it). Only cards whose effect is a visible fact of the fixture have one; the rest rely on the differential.
        if (Witnesses.TryGetValue(card.Id, out var witness))
            foreach (var pair in results.Where(r => r.Value.Accept == "Y" && !witness.Check(r.Value.Core, r.Value.Full)))
                found.Add($"{pair.Key}: printed effect missing ({witness.What}): {pair.Value.Full}");
        if (card[HeraldRules.Keyword] != 0 && results.Any(r => r.Value.Accept != "Y"))
            found.Add("Herald card not accepted by: " + string.Join(",", results.Where(r => r.Value.Accept != "Y").Select(r => r.Key)));
        if (results.Values.Select(r => r.Core).Distinct().Count() > 1)
            found.Add("effect core differs: " + string.Join(" | ", results.GroupBy(r => r.Value.Core).Select(grp => $"{grp.Key} <- [{string.Join(",", grp.Select(x => x.Key))}]")));
        // Herald: the Soldier comes from the card/opponent for all five fallback classes, so everything must match.
        if (card[HeraldRules.Keyword] != 0 && NoSoldierClasses.Select(c => results[c].Full).Distinct().Count() > 1)
            found.Add("Herald fallback classes differ: " + string.Join(" | ", NoSoldierClasses.GroupBy(c => results[c].Full).Select(grp => $"{grp.Key} <- [{string.Join(",", grp)}]")));
        // Imbue consumers: board, hand and graveyard too, across all 11 classes (only a Blessing may differ, and it lives in the hero power).
        if (ImbueRules.IsConsumer(card) && results.Values.Select(r => r.Full).Distinct().Count() > 1)
            found.Add("Imbue consumer full state differs: " + string.Join(" | ", results.GroupBy(r => r.Value.Full).Select(grp => $"{grp.Key} <- [{string.Join(",", grp.Select(x => x.Key))}]")));
        return found;
    }

    private static bool Allowed(string id, out string reason)
    {
        foreach (var pair in Allowlist)
            if (pair.Key == id || pair.Key.EndsWith("*") && id.StartsWith(pair.Key.TrimEnd('*'))) { reason = pair.Value; return true; }
        reason = null; return false;
    }

    /// <summary>Differential: every universe card, same signature for all 11 classes.</summary>
    internal static void Run(bool keywordOnly)
    {
        var cards = Cards.All.Where(Universe).Where(c => !keywordOnly || KeywordGated(c)).OrderBy(c => c.Id).ToList();
        var games = Classes.ToDictionary(c => c, Fixture);
        var diverged = new List<string>(); int allowed = 0;
        foreach (var card in cards)
        {
            // Full play-block run only for keyword-gated cards (cheap set); the rest compare playability/options.
            bool full = KeywordGated(card);
            var sigs = Classes.ToDictionary(c => c, c => Signature(games[c], card, full));
            var effects = full ? Effects(games, card) : new List<string>();
            // Absolute: a Herald card is playable, offered and accepted for every class (a block in all 11 would still be "identical").
            if (card[HeraldRules.Keyword] != 0 && sigs.Values.Any(v => v != "playable=True offered=True block=Y"))
                effects.Add("Herald card must be playable/offered/accepted for every class: " + string.Join(" | ", sigs.GroupBy(x => x.Value).Select(x => $"{x.Key} <- [{string.Join(",", x.Select(y => y.Key))}]")));
            // Imbue consumers: offered implies accepted (a target-less fixture may legitimately offer nothing).
            if (ImbueRules.IsConsumer(card) && sigs.Values.Any(v => v.Contains("offered=True") && !v.EndsWith("block=Y")))
                effects.Add("Imbue consumer offered but rejected by the play block: " + string.Join(" | ", sigs.Select(x => x.Key + ":" + x.Value)));
            // The allowlist waives only the DIFFERENTIAL lines (the effect legitimately depends on the class); absolute checks (exceptions,
            // Herald accepted, Imbue offered => accepted, witnesses) always stand.
            if (effects.Count != 0 && EffectAllowlist.TryGetValue(card.Id, out string effectReason))
            {
                int waived = effects.RemoveAll(line => line.StartsWith("effect core differs", StringComparison.Ordinal)
                    || line.StartsWith("Herald fallback classes differ", StringComparison.Ordinal) || line.StartsWith("Imbue consumer full state differs", StringComparison.Ordinal));
                if (waived != 0) { allowed++; Console.WriteLine($"  effect phase differential allowed {card.Id}: {effectReason}"); }
            }
            if (sigs.Values.Distinct().Count() == 1 && effects.Count == 0) continue;
            if (Allowed(card.Id, out string reason)) { allowed++; Console.WriteLine($"  allowed {card.Id}: {reason}"); continue; }
            var groups = sigs.Values.Distinct().Count() == 1 ? Enumerable.Empty<string>()
                : sigs.GroupBy(x => x.Value).Select(x => $"{x.Key} <- [{string.Join(",", x.Select(y => y.Key))}]");
            diverged.Add($"{card.Id} {card.Name}: {string.Join(" | ", groups.Concat(effects))}");
        }
        foreach (var line in diverged.Take(60)) Console.WriteLine("  DIVERGES " + line);
        Core2026Proof.Check(diverged.Count == 0,
            $"CrossClassPlayability: {diverged.Count} card(s) differ across the 11 hero classes (first: {diverged.FirstOrDefault()})");
        Console.WriteLine("  scope: playability/options for every implemented collectible; play block + effect phase only for the keyword-gated cards "
            + "(Herald, Imbue consumers) - the other cards get no effect phase here (their class-dependence is covered by the per-mechanic proofs)");
        Console.WriteLine($"PASS CrossClassPlayability: {cards.Count} cards x {Classes.Length} classes identical"
            + $" ({allowed} allowlisted){(keywordOnly ? ", keyword-gated only" : "")}");
    }
}
