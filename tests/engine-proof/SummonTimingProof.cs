using System.Text.RegularExpressions;
using SabberStoneCore.Actions;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Triggers;
using static Core2026Proof;

// Issue #207. Retail (hearthstone.wiki.gg Advanced rulebook, "Summon Resolution Step"): the step runs at the end of each
// outermost Phase, after the Aura Update and before the Death Creation Step; a Death Phase is one Phase, not one per
// Deathrattle. So "whenever you summon" reactions to Deathrattle and Reborn summons resolve after that Death Phase's
// Deathrattles and before its new deaths are created. "After you summon" reactions (Knife Juggler) stay per summon.
internal static class SummonTimingProof
{
    private static void Verify(bool condition, string message) => Core2026Proof.Check(condition, message);
    [GroupProof("summon-timing")]
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (var test in new Action[] { TidecallerDeathrattle, TidecallerThreeMurlocs, BuzzardDeathrattle, BuzzardMortallyWoundedInSameDeathPhase,
            KabalMastermindDeathrattleDraw, CollapsingStarDeathrattleDemon, UndertakerReborn, KnifeJugglerStillPerSummon,
            ImpfernalOverdrawFiresOnce, ReactionsAfterAllDeathrattlesOfThePhase, ReactionSummonGetsItsOwnPass })
            try { test(); } catch (Exception e) { failures.Add(test.Method.Name + ": " + e.Message); }
        failures.AddRange(Sweep());
        Verify(failures.Count == 0, string.Join("\n", failures));
        Console.WriteLine("PASS summon timing: whenever-you-summon reactions to Deathrattle and Reborn summons resolve in the same death cleanup");
    }

    private static Game Fixture() => HistoryFactsProof.Fixture();

    // One cleanup = the death phase plus its Summon Resolution Step; nothing may wait for a later cleanup.
    private static void Die(Game g, params Minion[] victims)
    {
        foreach (var victim in victims) victim.ToBeDestroyed = true;
        g.DeathProcessingAndAuraUpdate();
    }

    private static void NothingLeft(Game g) =>
        Verify(g.SummonedMinions.Count == 0, $"summon events left for a later cleanup: {g.SummonedMinions.Count}");

    private static void TidecallerDeathrattle()
    {
        var g = Fixture(); var p = g.Player1;
        var tide = Summon(p, "EX1_509");
        var hog = Summon(p, "DAL_743"); // Deathrattle: summon a 1/1 Murloc
        Die(g, hog);
        Verify(tide.AttackDamage == 2, $"Tidecaller gains +1 Attack in the death cleanup of Hench-Clan Hogsteed, got {tide.AttackDamage}");
        NothingLeft(g);
    }

    private static void TidecallerThreeMurlocs()
    {
        var g = Fixture(); var p = g.Player1;
        var tide = Summon(p, "CORE_EX1_509");
        var threshadon = Summon(p, "UNG_010"); // Deathrattle: summon three 1/1 Murlocs
        Die(g, threshadon);
        Verify(tide.AttackDamage == 4 && p.BoardZone.Count == 4, $"Tidecaller gains +3 Attack for three Deathrattle Murlocs, got {tide.AttackDamage}");
        NothingLeft(g);
    }

    private static void BuzzardDeathrattle()
    {
        var g = Fixture(); var p = g.Player1;
        Summon(p, "CS2_237");
        var wolf = Summon(p, "OG_216"); // Beast; Deathrattle: summon two 1/1 Spiders (Beasts)
        int hand = p.HandZone.Count;
        Die(g, wolf);
        Verify(p.HandZone.Count - hand == 2, $"Starving Buzzard draws two in Infested Wolf's death cleanup, got {p.HandZone.Count - hand}");
        NothingLeft(g);
    }

    // Mass death: the Ghoul (earlier in play order) mortally wounds the Buzzard, then the Wolf summons two Beasts, in one
    // Death Phase. The Summon Resolution Step comes before the next Death Creation Step, and a mortally wounded minion is
    // still in play, so the Buzzard draws for both Spiders before it dies.
    private static void BuzzardMortallyWoundedInSameDeathPhase()
    {
        var g = Fixture(); var p = g.Player1;
        var buzzard = Summon(p, "CS2_237");
        buzzard.Damage = buzzard.BaseHealth - 1;
        var ghoul = Summon(p, "FP1_024"); // Deathrattle: deal 1 damage to all minions
        var wolf = Summon(p, "OG_216");
        int hand = p.HandZone.Count;
        Die(g, ghoul, wolf);
        Verify(buzzard.Zone == p.GraveyardZone && p.BoardZone.Count(m => m.Card.Id == "OG_216a") == 2, "the Ghoul kills the Buzzard; both Spiders live");
        Verify(p.HandZone.Count - hand == 2, $"the mortally wounded Buzzard draws for both Spiders before it dies, got {p.HandZone.Count - hand}");
        NothingLeft(g);
    }

    // The opponent's Loot Hoarder draws an Imp-formant in its Deathrattle; the Imp summons for the Mastermind's owner.
    private static void KabalMastermindDeathrattleDraw()
    {
        var g = Fixture(); var p = g.Player1; var e = p.Opponent;
        Play(g, p, "CAP_406");
        foreach (var card in e.DeckZone.ToArray()) e.SetasideZone.Add(e.DeckZone.Remove(card));
        var imp = Entity.FromCard(e, Cards.FromId("CAP_400t2t"), zone: e.DeckZone);
        var hoarder = Summon(e, "EX1_096"); // Deathrattle: draw a card
        Die(g, hoarder);
        var summoned = p.BoardZone.FirstOrDefault(m => m.Card.Id == "CAP_400t2t");
        Verify(summoned != null, "the drawn Imp-formant is summoned for the Mastermind's owner");
        Verify(summoned.AttackDamage == 5 && summoned.Health == 5, $"Kabal Mastermind gives it +2/+2 in the same cleanup, got {summoned.AttackDamage}/{summoned.Health}");
        NothingLeft(g);
    }

    private static void CollapsingStarDeathrattleDemon()
    {
        var g = Fixture(); var p = g.Player1;
        Play(g, p, "JAIL_EVENT_101");
        Verify(p.Hero.HeroPower.Card.Id == "JAIL_EVENT_101hp", "Soul Immolation sets Collapsing Star");
        var voidlord = Summon(p, "LOOT_368"); // Demon; Deathrattle: summon three 1/3 Demons
        p.Hero.HeroPower.IsExhausted = true;
        Die(g, voidlord);
        Verify(!p.Hero.HeroPower.IsExhausted, "Collapsing Star refreshes in Voidlord's death cleanup");
        NothingLeft(g);
    }

    private static void UndertakerReborn()
    {
        var g = Fixture(); var p = g.Player1;
        var undertaker = Summon(p, "FP1_028");
        var hoarder = Summon(p, "EX1_096");
        hoarder[GameTag.REBORN] = 1;
        int attack = undertaker.AttackDamage;
        Die(g, hoarder);
        Verify(p.BoardZone.Any(m => m.Card.Id == "EX1_096" && m[GameTag.HAS_BEEN_REBORN] == 1), "Loot Hoarder is reborn");
        Verify(undertaker.AttackDamage == attack + 1, $"Undertaker gains +1 Attack for the Reborn Deathrattle minion in the same cleanup, got {undertaker.AttackDamage - attack}");
        NothingLeft(g);
    }

    // Control: an "after you summon" reaction still resolves per summon inside the Deathrattle, and only once.
    private static void KnifeJugglerStillPerSummon()
    {
        var g = Fixture(); var p = g.Player1;
        var juggler = Summon(p, "NEW1_019");
        var creeper = Summon(p, "FP1_002");
        int before = p.Opponent.Hero.Damage;
        Die(g, creeper);
        g.DeathProcessingAndAuraUpdate();
        Verify(p.Opponent.Hero.Damage - before == 2, $"Knife Juggler throws one knife per Spider, got {p.Opponent.Hero.Damage - before}");
        Verify(juggler.Zone == p.BoardZone, "juggler alive");
    }

    // Review F1: a reaction that reaches a nested cleanup must not fire the same summon again. Buzzard draws for the Raptor
    // into a full hand; the overdrawn IMPFERNAL!'s Deathrattle runs a nested cleanup (DrawReplacementRules.OffboardDeathrattle).
    private static void ImpfernalOverdrawFiresOnce()
    {
        var g = Fixture(); var p = g.Player1;
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        for (int i = 0; i < 6; i++) Entity.FromCard(p, Cards.FromId("JAIL_398"), zone: p.DeckZone);
        var buzzard = Summon(p, "CS2_237");
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        Summon(p, "CS2_172"); // one Beast
        int overdrawn = p.GraveyardZone.Count(c => c.Card.Id == "JAIL_398");
        Verify(p.DeckZone.Count == 5 && overdrawn == 1, $"one Beast draws one card (one IMPFERNAL! overdrawn), got deck {p.DeckZone.Count}, overdrawn {overdrawn}");
        Verify(buzzard.Zone == p.GraveyardZone, "IMPFERNAL!'s 3 damage kills the Buzzard");
        NothingLeft(g);
    }

    // Review F5: the step comes after all the Deathrattles of the Death Phase, not after each. Grandmother (earlier in play)
    // summons a Beast, then the Hoarder draws; the Buzzard's draw comes after the Hoarder's.
    private static void ReactionsAfterAllDeathrattlesOfThePhase()
    {
        var g = Fixture(); var p = g.Player1;
        var buzzard = Summon(p, "CS2_237");
        var grandmother = Summon(p, "KAR_005");
        var hoarder = Summon(p, "EX1_096");
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        int from = g.PowerHistory.Full.Count;
        Die(g, grandmother, hoarder);
        var sources = DrawSources(g, from, p);
        Verify(p.HandZone.Count == 2, $"the Hoarder and the Buzzard each draw one card in the death cleanup, got {p.HandZone.Count}");
        Verify(sources.Count == 2 && sources[0] == hoarder.Id && sources[1] == buzzard.Id,
            $"the Hoarder's card is drawn before the Buzzard's, got draw sources [{string.Join(",", sources)}] (hoarder {hoarder.Id}, buzzard {buzzard.Id})");
        NothingLeft(g);
    }

    // Engine choice (Game.SummonResolutionStep): a summon made by a reaction resolves in another pass of the same step, not
    // dropped and not left pending. No engine SUMMON reactor summons, so a test listener on a plain host stands in for one:
    // "whenever you summon a Beast, summon a Murloc Scout".
    private static void ReactionSummonGetsItsOwnPass()
    {
        var g = Fixture(); var p = g.Player1;
        var first = Summon(p, "EX1_509");
        var second = Summon(p, "EX1_509");
        var host = Summon(p, "CS2_182");
        new Trigger(TriggerType.SUMMON) { TriggerSource = TriggerSource.FRIENDLY, Condition = SelfCondition.IsRace(Race.BEAST),
            SingleTask = new SummonTask("EX1_506a") }.Activate(host);
        int a = first.AttackDamage, b = second.AttackDamage;
        Summon(p, "CS2_172"); // one Beast: the host summons one Murloc Scout
        Verify(p.BoardZone.Count(m => m.Card.Id == "EX1_506a") == 1, "the test listener summons one Murloc Scout");
        Verify(first.AttackDamage - a == 1 && second.AttackDamage - b == 1,
            $"each Tidecaller gains +1 for the Scout in the same step, got +{first.AttackDamage - a}/+{second.AttackDamage - b}");
        NothingLeft(g);
        g.DeathProcessingAndAuraUpdate();
        Verify(first.AttackDamage - a == 1 && second.AttackDamage - b == 1, "a second cleanup fires nothing more");
    }

    // The innermost Hoarder/Buzzard block around each card entering the hand, in history order.
    private static List<int> DrawSources(Game g, int from, Controller p)
    {
        var stack = new List<int>(); var result = new List<int>(); var seen = new HashSet<int>();
        var hand = p.HandZone.Select(c => c.Id).ToHashSet();
        foreach (var entry in g.PowerHistory.Full.Skip(from))
        {
            if (entry is PowerHistoryBlockStart start) stack.Add(start.Source);
            else if (entry is PowerHistoryBlockEnd && stack.Count != 0) stack.RemoveAt(stack.Count - 1);
            else if (entry is PowerHistoryTagChange tag && tag.Tag == GameTag.ZONE && tag.Value == (int)Zone.HAND
                     && hand.Contains(tag.EntityId) && seen.Add(tag.EntityId))
                result.Add(stack.LastOrDefault(id => g.IdEntityDic.TryGetValue(id, out var e) && e is Minion));
        }
        return result;
    }

    // ---- Class sweep: every engine card with a SUMMON or AFTER_SUMMON listener, both seats, friendly and enemy summons.
    private static readonly string[] Candidates = {
        "CS2_182", "CS2_231", "DAL_743t", "OG_216a", "AT_020", "BOT_066t", "AT_092", "CS2_050", "DAL_146t", "DRG_035t",
        "EX1_096", "CS2_189", "EX1_010", "CAP_400t2t", "ULD_430t" };

    internal static IEnumerable<Trigger> Triggers(Trigger trigger) => AttackTriggerSweepProof.Triggers(trigger);

    private static bool Reacts(Card card) => Triggers(card.Power?.Trigger).Any(t => t.Type is TriggerType.SUMMON or TriggerType.AFTER_SUMMON);

    private static readonly Regex ReactionText = new(@"\b(whenever|after)\b[^.]{0,40}?\b(you|your opponent|a player|friendly|enemy|another|a minion|minion)\b[^.]{0,30}?\bsummon(s|ed)?\b",
        RegexOptions.IgnoreCase | RegexOptions.Singleline);

    private static int Triggered(Game g, int from, IEntity source) => g.PowerHistory.Full.Skip(from).OfType<PowerHistoryBlockStart>()
        .Count(b => b.BlockType == BlockType.TRIGGER && b.Source == source.Id);

    private static IEntity Install(Game g, Controller owner, Card card)
    {
        var activation = Triggers(card.Power?.Trigger).FirstOrDefault()?.TriggerActivation ?? TriggerActivation.PLAY;
        if (activation is TriggerActivation.HAND or TriggerActivation.HAND_OR_DECK) return Add(owner, card.Id);
        switch (card.Type)
        {
            case CardType.MINION: return Summon(owner, card.Id);
            case CardType.WEAPON: new WeaponTask(card.Id).Process(g, owner, owner.Hero, null); return owner.Hero.Weapon;
            case CardType.HERO_POWER:
                new ReplaceHeroPower(card).Process(g, owner, owner.Hero, null);
                return owner.Hero.HeroPower;
            case CardType.ENCHANTMENT:
                var source = Enchantment.GetInstance(owner, owner.Hero, owner, card);
                card.Power.Trigger.Activate(source);
                return source.ActivatedTrigger != null ? source : null;
            case CardType.SPELL when card.IsQuest:
                var quest = (Spell)Entity.FromCard(owner, card);
                owner.SecretZone.Add(quest);
                if (quest.ActivatedTrigger == null) card.Power.Trigger.Activate(quest);
                return quest;
            default: return null;
        }
    }

    private enum Mode { Direct, Deathrattle, Reborn, MassDeath }

    // Trigger blocks of the reacting source in the one cleanup that ran the summons, then after a second cleanup.
    private static (int Same, int Later, IEntity Source) Measure(Card card, int seat, bool enemy, string candidate, Mode mode)
    {
        var g = Fixture();
        var owner = seat == 1 ? g.Player1 : g.Player2;
        var summoner = enemy ? owner.Opponent : owner;
        var source = Install(g, owner, card);
        if (source == null) return (-1, -1, null);
        g.DeathProcessingAndAuraUpdate();
        int from = g.PowerHistory.Full.Count;
        if (mode == Mode.Direct) Summon(summoner, candidate);
        else
        {
            var carriers = new List<Minion>();
            for (int i = 0; i < (mode == Mode.MassDeath ? 2 : 1); i++)
            {
                var carrier = (Minion)Entity.FromCard(summoner, Cards.FromId(candidate));
                Generic.SummonBlock(g, carrier, -1, null);
                carriers.Add(carrier);
            }
            if (mode == Mode.MassDeath) carriers.Add((Minion)Entity.FromCard(summoner, Cards.FromId("CS2_182")));
            if (carriers.Count == 3) Generic.SummonBlock(g, carriers[2], -1, null);
            g.DeathProcessingAndAuraUpdate();
            foreach (var carrier in carriers.Take(mode == Mode.MassDeath ? 2 : 1))
                if (mode == Mode.Reborn) carrier[GameTag.REBORN] = 1;
                else new AddEnchantmentTask("CS2_038e", EntityType.TARGET).Process(g, summoner, carrier, carrier);
            from = g.PowerHistory.Full.Count;
            foreach (var carrier in carriers) carrier.ToBeDestroyed = true;
            g.DeathProcessingAndAuraUpdate();
        }
        int same = Triggered(g, from, source);
        g.DeathProcessingAndAuraUpdate();
        return (same, Triggered(g, from, source), source);
    }

    internal static List<string> Sweep()
    {
        var failures = new List<string>();
        var reactors = Cards.All.Where(c => c.Implemented && Reacts(c)).OrderBy(c => c.Id, StringComparer.Ordinal).ToArray();
        int pass = 0, skipped = 0;
        foreach (var card in reactors)
        foreach (int seat in new[] { 1, 2 })
        foreach (bool enemy in new[] { false, true })
        {
            string label = $"{card.Id} ({card.Name}) seat={seat} {(enemy ? "enemy" : "friendly")} summon";
            try
            {
                string match = null; int perSummon = 0;
                foreach (string candidate in Candidates)
                {
                    var (direct, _, _) = Measure(card, seat, enemy, candidate, Mode.Direct);
                    if (direct < 0) break;
                    if (direct > 0) { match = candidate; perSummon = direct; break; }
                }
                if (match == null) { skipped++; Console.WriteLine($"SUMMON-SWEEP {label} SKIP no candidate triggers it"); continue; }
                var modes = new[] { (Mode.Deathrattle, 1), (Mode.Reborn, 1), (Mode.MassDeath, 2) };
                var results = modes.Select(m => (m.Item1, Expected: perSummon * m.Item2, Got: Measure(card, seat, enemy, match, m.Item1))).ToArray();
                var bad = results.Where(r => r.Got.Same != r.Expected || r.Got.Later != r.Expected).ToArray();
                string detail = string.Join(" ", results.Select(r => $"{r.Item1}={r.Got.Same}/{r.Got.Later}of{r.Expected}"));
                if (bad.Length == 0) { pass++; Console.WriteLine($"SUMMON-SWEEP {label} via {match} PASS {detail}"); }
                else { failures.Add($"{label} via {match}: {detail}"); Console.WriteLine($"SUMMON-SWEEP {label} via {match} FAIL {detail}"); }
            }
            catch (Exception error) { failures.Add($"{label}: {error.Message}"); Console.WriteLine($"SUMMON-SWEEP {label} FAIL {error.Message}"); }
        }
        // Text reactions with no SUMMON/AFTER_SUMMON listener are implemented by custom hooks; list them for review.
        foreach (var card in Cards.All.Where(c => c.Implemented && !Reacts(c) && c.Text != null
                     && ReactionText.IsMatch(Regex.Replace(c.Text, "<[^>]+>", ""))).OrderBy(c => c.Id, StringComparer.Ordinal))
            Console.WriteLine($"SUMMON-SWEEP-TEXT {card.Id} ({card.Name}) {card.Type}: {Regex.Replace(card.Text, "\\s+", " ")}");
        Console.WriteLine($"SUMMON-SWEEP {reactors.Length} cards, {pass} pass, {skipped} skipped, {failures.Count} fail");
        return failures;
    }
}
