using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SabberStoneCore.Enums;
using SabberStoneCore.Tasks.PlayerTasks;

// WP2 owns this file: skill catalog, SkilledPilot.Choose determinism/bounds, mulligan mistakes.
internal static class SkillSuite
{
    private const int OwnHero = 4, EnemyHero = 6;
    private static readonly int[] EnemyMinions = { 30, 31, 32 };

    internal static void Run()
    {
        var skills = SkillCatalog.Parse(Program.Data("skills-v1.json"));
        Program.Require(skills.Tiers.Count >= 5, "skill tiers");
        Program.Require(skills.Tiers.Zip(skills.Tiers.Skip(1), (a, b) => a.RatingMax <= b.RatingMin + 1e-9).All(x => x), "tier ranges ordered");
        Catalog(skills);
        Bounds(skills);
        Kinds(skills);
        Mulligans(skills);
        Variants(skills);
    }

    private static void Catalog(SkillCatalog skills)
    {
        Program.Require(skills.Tiers.Select(t => t.Id).SequenceEqual(new[] { "novice", "casual", "regular", "skilled", "expert", "master", "elite" }), "seven named tiers");
        Program.Require(skills.Tiers.Select(t => t.Tier).SequenceEqual(Enumerable.Range(1, 7)), "tier numbers 1..7");
        Program.Require(skills.Find("elite").IsPerfect && !skills.Find("master").IsPerfect, "only elite is perfect");
        Program.Require(skills.ForRating(0).Id == "novice" && skills.ForRating(5000).Id == "elite", "ForRating clamps");
        foreach (var tier in skills.Tiers)
        {
            Program.Require(skills.ForRating(tier.RatingMin).Id == tier.Id, "ForRating at range start " + tier.Id);
            Program.Require(skills.ForRating((tier.RatingMin + tier.RatingMax) / 2).Id == tier.Id, "ForRating mid range " + tier.Id);
        }
        // Mistake rates fall monotonically with tier; humans never miss a one- or two-action lethal.
        foreach (MistakeKind kind in Enum.GetValues(typeof(MistakeKind)))
            Program.Require(skills.Tiers.Zip(skills.Tiers.Skip(1), (a, b) => a.Chance(kind) >= b.Chance(kind) - 1e-12).All(x => x), "monotone " + kind);
        Program.Require(skills.Tiers.SelectMany(t => t.Mistakes).Where(m => m.Kind == MistakeKind.LethalMiss).All(m => m.MinLethalActions >= 3), "lethalMiss >= 3 actions");
        bool rejected = false;
        try { SkillCatalog.Parse(Program.Data("skills-v1.json").Replace("\"minLethalActions\": 3", "\"minLethalActions\": 2")); }
        catch (System.IO.InvalidDataException) { rejected = true; }
        Program.Require(rejected, "short lethalMiss rejected at load");
    }

    // Random candidate sets: same seed => same pick; pick within the fired rule's bound; elite = argmax; terminal rule.
    private static void Bounds(SkillCatalog skills)
    {
        var random = new Random(20261025);
        var picks = new Dictionary<string, int>();
        for (int trial = 0; trial < 4000; trial++)
        {
            int count = 1 + random.Next(9);
            var candidates = new List<KeyValuePair<BotAction, double>>();
            double best = random.Next(-40, 40);
            for (int i = 0; i < count; i++)
            {
                double value = i == 0 ? best : best - random.NextDouble() * 12;
                if (i > 0 && random.Next(10) == 0) value = -1000000 + random.Next(100);
                candidates.Add(new KeyValuePair<BotAction, double>(RandomAction(random, i), value));
            }
            var ordered = candidates.Take(1).Concat(candidates.Skip(1).OrderByDescending(c => c.Value)).ToList();
            // Random sources rarely offer the same attacker two legal targets; give master eligible near trades too.
            if (trial % 10 == 0) ordered = new List<KeyValuePair<BotAction, double>> {
                Pair(Attack(10, 30), best), Pair(Attack(10, 31), best - 1) };
            var context = Context(trial % 2 == 0 ? new int[0] : new[] { ordered[0].Key.Source });
            int seed = trial * 7919 + 13;
            foreach (var skill in skills.Tiers)
            {
                var choice = SkilledPilot.Select(ordered, skill, seed, context);
                var again = SkilledPilot.Select(ordered, skill, seed, context);
                Program.Require(choice.Index == again.Index && choice.Kind == again.Kind, "deterministic " + skill.Id);
                Program.Require(SkilledPilot.Choose(ordered, skill, seed, context) == choice.Index, "Choose == Select " + skill.Id);
                Program.Require(choice.Index >= 0 && choice.Index < ordered.Count, "index in range");
                if (skill.IsPerfect) Program.Require(choice.Index == 0, "elite is argmax");
                double loss = choice.Kind.HasValue ? (skill.Rule(choice.Kind.Value)?.MaxValueLoss ?? skill.SoftmaxMaxValueLoss) : skill.SoftmaxMaxValueLoss;
                Program.Require(ordered[choice.Index].Value >= ordered[0].Value - loss - 1e-9, "maxValueLoss " + skill.Id);
                Program.Require(choice.Index == 0 || ordered[choice.Index].Value > -999000, "terminal rule " + skill.Id);
                if (choice.Index != 0) picks[skill.Id] = (picks.ContainsKey(skill.Id) ? picks[skill.Id] : 0) + 1;
            }
        }
        // Winning lines are never perturbed.
        var win = new List<KeyValuePair<BotAction, double>> { Pair(Attack(10, EnemyHero), 1000000), Pair(Attack(10, 30), 999999) };
        Program.Require(skills.Tiers.All(t => Enumerable.Range(0, 200).All(s => SkilledPilot.Choose(win, t, s, Context()) == 0)), "lethal never perturbed");
        int novice = picks.ContainsKey("novice") ? picks["novice"] : 0, master = picks.ContainsKey("master") ? picks["master"] : 0;
        Program.Require(novice > master && master > 0 && !picks.ContainsKey("elite"), "mistake frequency falls with tier, master remains imperfect");
    }

    private static void Kinds(SkillCatalog skills)
    {
        var novice = skills.Find("novice");
        // Trade target: the same attacker hits another enemy minion; never its own side.
        var trade = new List<KeyValuePair<BotAction, double>> {
            Pair(Attack(10, 30), 20), Pair(Spell(11, 12), 19.5), Pair(Attack(10, 31), 18), Pair(Attack(10, 32), 17) };
        var kinds = Enumerable.Range(0, 3000).Select(s => SkilledPilot.Select(trade, novice, s, Context())).ToArray();
        Program.Require(kinds.Any(c => c.Kind == MistakeKind.TradeTarget && trade[c.Index].Key.Source == 10 && trade[c.Index].Key.Target == 31), "tradeTarget fires");
        Program.Require(kinds.Where(c => c.Kind == MistakeKind.TradeTarget).All(c => trade[c.Index].Key.Source == 10), "tradeTarget same attacker");
        var ownSide = new List<KeyValuePair<BotAction, double>> { Pair(Spell(11, 30), 20), Pair(Spell(11, 20), 19.9) };
        Program.Require(Enumerable.Range(0, 3000).All(s => SkilledPilot.Select(ownSide, novice, s, Context()).Kind != MistakeKind.TradeTarget), "trade never flips to own side");
        // Face bias: a near-equal face attack is sometimes preferred to the right trade.
        var face = new List<KeyValuePair<BotAction, double>> { Pair(Attack(10, 30), 20), Pair(Attack(10, EnemyHero), 16) };
        Program.Require(Enumerable.Range(0, 3000).Any(s => SkilledPilot.Select(face, novice, s, Context()).Kind == MistakeKind.FaceBias), "faceBias fires");
        double beyond = Math.Max(novice.SoftmaxMaxValueLoss, novice.Mistakes.Max(m => m.MaxValueLoss)) + 1;
        var farFace = new List<KeyValuePair<BotAction, double>> { Pair(Attack(10, 30), 20), Pair(Attack(10, EnemyHero), 20 - beyond) };
        Program.Require(Enumerable.Range(0, 3000).All(s => SkilledPilot.Choose(farFace, novice, s, Context()) == 0), "faceBias bounded");
        // Hold too long: keeps only that card (attacks instead), then stays consistent for it for the rest of the turn.
        var hold = new List<KeyValuePair<BotAction, double>> { Pair(Play(40), 20), Pair(Attack(10, 30), 17), Pair(End(), 19) };
        var holds = Enumerable.Range(0, 3000).Select(s => SkilledPilot.Select(hold, novice, s, Context())).ToArray();
        Program.Require(holds.Any(c => c.Kind == MistakeKind.HoldTooLong) && holds.Where(c => c.Kind == MistakeKind.HoldTooLong).All(c => c.Index == 1), "holdTooLong keeps the card, not the turn");
        Program.Require(Enumerable.Range(0, 200).All(s => SkilledPilot.Select(hold, skills.Find("elite"), s, Context(new[] { 40 })).Index == 0), "elite ignores held set");
        Program.Require(Enumerable.Range(0, 200).All(s => SkilledPilot.Select(hold, novice, s, Context(new[] { 40 })).Index == 1), "held card stays held");
        // Ending the turn is a hold only when the held card is all that is left to do.
        var last = new List<KeyValuePair<BotAction, double>> { Pair(Play(40), 20), Pair(End(), 17) };
        Program.Require(Enumerable.Range(0, 3000).All(s => SkilledPilot.Select(last, novice, s, Context()).Kind != MistakeKind.HoldTooLong), "no end-turn hold with work left");
        Program.Require(Enumerable.Range(0, 3000).Any(s => SkilledPilot.Select(last, novice, s, Context(null, null, true)).Kind == MistakeKind.HoldTooLong), "end-turn hold when only the card remains");
        // Trades stay on enemy minions: never face, never the bot's own side.
        var hero = new List<KeyValuePair<BotAction, double>> { Pair(Attack(10, 30), 20), Pair(Attack(10, EnemyHero), 19.9) };
        Program.Require(Enumerable.Range(0, 3000).All(s => SkilledPilot.Select(hero, novice, s, Context()).Kind != MistakeKind.TradeTarget), "trade never to face");
        // Unsearched alternatives never enter typed mistakes or softmax, even with plausible-looking prices.
        var priced = new List<KeyValuePair<BotAction, double>> { Pair(Play(40), 20), Pair(Play(41), 19.99), Pair(Play(42), 19.98) };
        Program.Require(skills.Tiers.All(t => Enumerable.Range(0, 2000).All(s => {
            var c = SkilledPilot.Select(priced, t, s, Context(), 1); return c.Index == 0; })), "all mistakes ignore unsearched alternatives");
        Program.Require(Enumerable.Range(0, 2000).Any(s => SkilledPilot.Select(priced, novice, s, Context()).Index != 0), "softmax uses searched candidates");
        // Terminal rule: an alternative that dies on the spot is never taken.
        var doom = new List<KeyValuePair<BotAction, double>> { Pair(Attack(10, 30), 20), Pair(Attack(10, 31), -1000000), Pair(End(), -999500) };
        Program.Require(skills.Tiers.All(t => Enumerable.Range(0, 2000).All(s => SkilledPilot.Choose(doom, t, s, Context()) == 0)), "never a losing alternative");
        // Misorder: only plan steps when the plan line is known.
        var order = new List<KeyValuePair<BotAction, double>> { Pair(Attack(10, 30), 20), Pair(Play(41), 19) };
        var line = Context(null, new[] { Attack(10, 30), Play(42) });
        Program.Require(Enumerable.Range(0, 3000).All(s => SkilledPilot.Select(order, novice, s, line).Kind != MistakeKind.Misorder), "misorder needs a later plan step");
    }

    private static void Mulligans(SkillCatalog skills)
    {
        var plan = DeckPlans.ForId("dragon-warrior.v1");
        var deck = plan.MulliganRules.Select(r => r.CardId).Distinct().ToArray();
        var view = new MulliganView(new[] { new MulliganCard(1, deck[0], 1), new MulliganCard(2, "CS2_182", 4),
            new MulliganCard(3, "EX1_116", 6), new MulliganCard(4, "GAME_005", 0) }, true, deck, CardClass.MAGE);
        var basic = new DeckPlanMulligan(plan).Keep(view);
        Program.Require(Enumerable.Range(0, 200).All(s => new SkilledMulligan(plan, skills.Find("elite"), s).Keep(view).SequenceEqual(basic)), "elite mulligan = plan");
        int greedy = 0;
        for (int s = 0; s < 400; s++)
        {
            var policy = new SkilledMulligan(plan, skills.Find("novice"), s);
            var keep = policy.Keep(view);
            Program.Require(keep.SequenceEqual(new SkilledMulligan(plan, skills.Find("novice"), s).Keep(view)), "mulligan deterministic");
            Program.Require(!keep.Contains(4), "never keeps the coin");
            if (policy.Greedy) { greedy++; Program.Require(keep.Contains(3) && keep.Count == basic.Count + 1, "greedy keeps the expensive card"); }
            else Program.Require(keep.SequenceEqual(basic), "non-greedy = plan");
        }
        double rate = greedy / 400.0, chance = skills.Find("novice").Chance(MistakeKind.GreedyMulligan);
        Program.Require(Math.Abs(rate - chance) < 0.1, "greedy rate ~ chance");
    }

    private static void Variants(SkillCatalog skills)
    {
        var golden = NpcCatalog.All[0];
        var skill = skills.Find("regular");
        var noReply = SkilledPilot.Tune(golden, skill, SkilledVariant.NoReply);
        var noLethal = SkilledPilot.Tune(golden, skill, SkilledVariant.NoLethal);
        var tuned = SkilledPilot.Tune(golden, skill, SkilledVariant.None);
        Program.Require(noReply.Id == golden.Id && noLethal.Id == golden.Id && tuned.PlanId == golden.PlanId, "variants keep identity");
        Program.Require(noReply.Budget.ReplyL == 0 && noReply.Budget.ReplyR > 2 * noReply.Budget.PlanNodes, "no-reply budget");
        Program.Require(noLethal.Budget.LethalNodes == 0 && noLethal.Budget.ReplyK == skill.Budget.ReplyK, "no-lethal budget");
        Program.Require(Math.Abs(tuned.Personality.ReplyBlendBeta - golden.Personality.ReplyBlendBeta * skill.ReplyBlendScale) < 1e-12, "reply blend scaled");
        var elite = SkilledPilot.Tune(golden, skills.Find("elite"), SkilledVariant.None);
        Program.Require(elite.Personality == golden.Personality, "elite keeps the golden personality");
        // Capped-search fixes reach only the noiseless tiers; softmax tiers keep their candidate spread, so their mistake rate.
        foreach (var tier in skills.Tiers) {
            var fixes = SkilledPilot.Tune(golden, tier, SkilledVariant.None).Fixes;
            bool noiseless = tier.Id == "master" || tier.Id == "elite";
            Program.Require(fixes.On("starved-replies") == noiseless && fixes.On("replan-reserve") == noiseless, "search fixes by tier: " + tier.Id);
            Program.Require(fixes.On("plan-reuse"), "other fixes stay on: " + tier.Id);
        }
    }

    private static MistakeContext Context(IEnumerable<int> held = null, IEnumerable<BotAction> line = null, bool onlyHeld = false) =>
        new MistakeContext(5, false, 0, false, EnemyHero, EnemyMinions, line, held, onlyHeld);
    private static KeyValuePair<BotAction, double> Pair(BotAction action, double value) => new KeyValuePair<BotAction, double>(action, value);
    private static BotAction RandomAction(Random random, int i)
    {
        switch (random.Next(5))
        {
            case 0: return Attack(10 + random.Next(3), random.Next(2) == 0 ? EnemyHero : EnemyMinions[random.Next(3)]);
            case 1: return Spell(11, random.Next(2) == 0 ? EnemyMinions[random.Next(3)] : 20 + random.Next(3));
            case 2: return Play(40 + i);
            case 3: return End();
            default: return Make(PlayerTaskType.HERO_POWER, 50, random.Next(2) == 0 ? 0 : EnemyHero, -1);
        }
    }
    private static BotAction Attack(int source, int target) => Make(PlayerTaskType.MINION_ATTACK, source, target, -1);
    private static BotAction Spell(int source, int target) => Make(PlayerTaskType.PLAY_CARD, source, target, -1);
    private static BotAction Play(int source) => Make(PlayerTaskType.PLAY_CARD, source, 0, 0);
    private static BotAction End() => Make(PlayerTaskType.END_TURN, OwnHero, 0, -1);

    // BotAction is engine-built (From(PlayerTask)); unit tests set its public readonly fields directly.
    private static BotAction Make(PlayerTaskType type, int source, int target, int position)
    {
        var action = (BotAction)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(BotAction));
        Set(action, "Type", type); Set(action, "Source", source); Set(action, "Target", target);
        Set(action, "ChooseOne", 0); Set(action, "Position", position); Set(action, "choices", "");
        return action;
    }
    private static void Set(BotAction action, string field, object value) =>
        typeof(BotAction).GetField(field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(action, value);
}
