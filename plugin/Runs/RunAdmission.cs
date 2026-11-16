using System;
using System.Collections.Generic;
using System.Linq;

// Strict content admission: a start deck, boss, bucket or treasure is offered only when every card it names is playable
// (implemented by the engine and, in the plugin, known to the client). Blocked entries stay in the data with the cards
// that block them, so the report is the engine work list. A run is ready when every round can find a boss and every
// offer the reward schedule makes can show its options.
public sealed class RunBlocked
{
    public string Kind, Id;
    public IReadOnlyList<string> Missing;
}

public sealed class RunAdmission
{
    // Each offer shows this many options (bundles or treasures).
    public const int OfferSize = 3;

    public RunDefinition Definition { get; private set; }
    public IReadOnlyCollection<string> Classes { get; private set; }
    public IReadOnlyCollection<string> Bosses { get; private set; }
    public IReadOnlyCollection<string> Buckets { get; private set; }
    public IReadOnlyCollection<string> PassiveTreasures { get; private set; }
    public IReadOnlyCollection<string> ActiveTreasures { get; private set; }
    public IReadOnlyList<RunBlocked> Blocked { get; private set; }
    private IReadOnlyDictionary<string, IReadOnlyList<string>> pools;

    // The cards a bundle of this bucket draws from: its list, and for a class-pool bucket also every playable collectible card
    // of its class (the classCards source given to Evaluate; none when it was not given).
    public IReadOnlyList<string> Pool(RunBucket bucket) =>
        bucket.ClassPool && pools.TryGetValue(bucket.Id, out var pool) ? pool : bucket.Cards;

    // playable: the engine implements the card and the client can show it. labelled: the client can show the card; used for
    // display-only cards (a loot bucket's title card), which never enter play.
    // classCards: the collectible card ids of a class (CardClass name), for class-pool buckets.
    public static RunAdmission Evaluate(RunDefinition definition, Func<string, bool> playable, Func<string, bool> labelled,
        Func<string, IEnumerable<string>> classCards = null)
    {
        if (definition == null) throw new ArgumentNullException(nameof(definition));
        if (playable == null) throw new ArgumentNullException(nameof(playable));
        if (labelled == null) throw new ArgumentNullException(nameof(labelled));
        var verdicts = new Dictionary<string, bool>(StringComparer.Ordinal);
        bool Playable(string id)
        {
            bool verdict;
            if (!verdicts.TryGetValue(id, out verdict)) verdicts[id] = verdict = playable(id);
            return verdict;
        }
        var blocked = new List<RunBlocked>();
        HashSet<string> Admit(string kind, IEnumerable<KeyValuePair<string, IEnumerable<string>>> entries, Func<string, string> label = null)
        {
            var admitted = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                string title = label == null ? null : label(entry.Key);
                var missing = entry.Value.Where(id => !Playable(id))
                    .Concat(title != null && !labelled(title) ? new[] { title } : new string[0])
                    .Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
                if (missing.Count == 0) admitted.Add(entry.Key);
                else blocked.Add(new RunBlocked { Kind = kind, Id = entry.Key, Missing = missing });
            }
            return admitted;
        }
        KeyValuePair<string, IEnumerable<string>> Entry(string id, IEnumerable<string> cards) =>
            new KeyValuePair<string, IEnumerable<string>>(id, cards);

        var pools = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        foreach (RunBucket bucket in definition.Buckets.Where(b => b.ClassPool))
            pools[bucket.Id] = bucket.Cards.Concat((classCards?.Invoke(bucket.Class) ?? new string[0]).Where(Playable))
                .Distinct(StringComparer.Ordinal).ToList();

        return new RunAdmission {
            Definition = definition,
            pools = pools,
            Classes = Admit("class", definition.Classes.Select(c => Entry(c.Class, c.Deck))),
            Bosses = Admit("boss", definition.Bosses.Select(b => Entry(b.Id, b.AllCards()))),
            Buckets = Admit("bucket", definition.Buckets.Select(b => Entry(b.Id, b.Cards)), id => definition.Bucket(id).Title),
            PassiveTreasures = Admit("passive", definition.PassiveTreasures.Select(t => Entry(t, new[] { t }))),
            ActiveTreasures = Admit("active", definition.ActiveTreasures.Select(t => Entry(t, new[] { t }))),
            Blocked = blocked,
        };
    }

    public bool IsReady => Problems().Count == 0;

    // Why the run cannot be offered yet; empty when it can.
    public IReadOnlyList<string> Problems()
    {
        var problems = new List<string>();
        foreach (RunClassStart start in Definition.Classes)
            if (!Classes.Contains(start.Class)) problems.Add("class " + start.Class + ": start deck is not playable");
        foreach (RunRound round in Definition.Rounds)
            if (!Definition.Bosses.Any(b => Bosses.Contains(b.Id) && b.Rounds.Contains(round.Number)))
                problems.Add("round " + round.Number + ": no admitted boss");
        var kinds = Definition.Rounds.SelectMany(r => r.Rewards).Distinct().ToList();
        if (kinds.Contains(RunRewardKind.Loot))
            foreach (RunClassStart start in Definition.Classes)
            {
                int count = Definition.Buckets.Count(b => b.Class == start.Class && Buckets.Contains(b.Id));
                if (count < OfferSize) problems.Add("class " + start.Class + ": " + count + " admitted loot buckets, needs " + OfferSize);
            }
        // A run never offers the same treasure twice, so each pool needs a full offer for every time it is offered.
        int passiveOffers = Definition.Rounds.Sum(r => r.Rewards.Count(k => k == RunRewardKind.PassiveTreasure));
        int activeOffers = Definition.Rounds.Sum(r => r.Rewards.Count(k => k == RunRewardKind.ActiveTreasure));
        int passiveNeeded = passiveOffers == 0 ? 0 : OfferSize + passiveOffers - 1;
        int activeNeeded = activeOffers == 0 ? 0 : OfferSize + activeOffers - 1;
        if (PassiveTreasures.Count < passiveNeeded) problems.Add(PassiveTreasures.Count + " admitted passive treasures, needs " + passiveNeeded);
        if (ActiveTreasures.Count < activeNeeded) problems.Add(ActiveTreasures.Count + " admitted active treasures, needs " + activeNeeded);
        return problems;
    }

    // Human-readable coverage: admitted counts, then each blocked entry with the cards that block it.
    public IReadOnlyList<string> Report()
    {
        var lines = new List<string> {
            Definition.Id + " v" + Definition.Version + ": classes " + Classes.Count + "/" + Definition.Classes.Count
                + ", bosses " + Bosses.Count + "/" + Definition.Bosses.Count + ", buckets " + Buckets.Count + "/" + Definition.Buckets.Count
                + ", passive treasures " + PassiveTreasures.Count + "/" + Definition.PassiveTreasures.Count
                + ", active treasures " + ActiveTreasures.Count + "/" + Definition.ActiveTreasures.Count,
        };
        foreach (RunBlocked entry in Blocked)
            lines.Add("  blocked " + entry.Kind + " " + entry.Id + ": " + string.Join(", ", entry.Missing));
        foreach (string problem in Problems()) lines.Add("  not ready: " + problem);
        return lines;
    }
}
