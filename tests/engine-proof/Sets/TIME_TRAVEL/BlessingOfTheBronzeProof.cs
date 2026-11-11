using System.Reflection;
using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class BlessingOfTheBronzeProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "Bronze: " + message);
    private static Game Fixture(int seed, int n = 1)
    {
        var g = ImbueProof.Make(CardClass.ROGUE, seed);
        ImbueRules.Imbue(g.Player1, g.Player1.Hero, n);
        Check(g.Player1.Hero.HeroPower.Card.Id == "END_000p", "Rogue installs Bronze");
        return g;
    }
    private static void Answer(Game g, bool rewind)
    {
        var t = g.Rewind;
        Check(t != null && g.TryAnswer(new DecisionAnswer(t.OfferChoiceId, t.ControllerId,
            new[] { rewind ? t.RewindEntityId : t.KeepEntityId }), out _), "answer accepted");
    }
    internal static void Run()
    {
        Identity();
        for (int seed = 1; seed <= 3; seed++)
        {
            Lean_BronzeOtherClass(seed);
            foreach (int n in new[] { 1, 2, 5 }) Lean_BronzeDiscount(seed, n);
            Lean_BronzeRewindHeroPower(seed);
            Lean_BronzeMorchieKeepBoth(seed);
            Boundaries(seed);
        }
        Console.WriteLine("PASS Bronze: Lean_BronzeOtherClass; Lean_BronzeDiscount; Lean_BronzeRewindHeroPower; Lean_BronzeMorchieKeepBoth; boundaries; seeds=1..3");
    }
    private static void Lean_BronzeOtherClass(int seed)
    {
        // Lean_ imbue-23: printed Rogue membership (including dual-class) and Neutral excluded.
        var g = Fixture(seed); var p = g.Player1;
        var candidates = OfflineCardPools.Candidates(g, p.HeroClass, new OfflinePoolFilter(classMode: PoolClassMode.OtherClass, type: CardType.MINION), 121243);
        var expected = candidates[g.Random.Clone().Next(candidates.Count)];
        Check(g.Process(HeroPowerTask.Any(p)), "activation accepted with null task.Source");
        var card = p.HandZone.Single();
        Check(card.Card.Type == CardType.MINION && expected.Collectible
            && (card.Card == expected || expected.Entourage?.Contains(card.Card.Id) == true)
            && !CardClassRules.IsAllowedInDeck(expected, CardClass.ROGUE) && expected.Class != CardClass.NEUTRAL, "other-class minion: " + card.Card.Id + "/" + card.Card.Class);
        Check(g.Rewind?.SourceId == p.Hero.HeroPower.Id, "power is choice source");
        Answer(g, false);
        var declaration = OfflinePoolDeclarations.Cards.Single(d => d.CardId == "END_000p");
        var requirement = declaration.Pools.Single();
        Check(requirement.Filter == new OfflinePoolFilter(classMode: PoolClassMode.OtherClass, type: CardType.MINION), "declared exact OtherClass MINION filter");
        var report = OfflinePoolAdmission.Evaluate(g.OfflinePools).Cards.Single(d => d.CardId == "END_000p");
        Check(report.PoolRequirements.All(r => r.Class == "ROGUE" && r.Count >= 3), "printed Rogue domain meets admission minimum");
        foreach (var cls in new[] { CardClass.MAGE, CardClass.DRUID, CardClass.SHAMAN })
        {
            g = ImbueProof.Make(cls, seed); p = g.Player1;
            new ReplaceHeroPower(Cards.FromId("END_000p")).Process(g, p, p.Hero, null);
            var hp = p.Hero.HeroPower; hp[GameTag.TAG_SCRIPT_DATA_NUM_1] = 2; hp.IsExhausted = true;
            int mana = p.UsedMana, uses = p.NumTimesHeroPowerUsedThisGame;
            for (int i = 0; i < 30; i++)
            {
                foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
                candidates = OfflineCardPools.Candidates(g, cls, new OfflinePoolFilter(classMode: PoolClassMode.OtherClass, type: CardType.MINION), 121243);
                expected = candidates[g.Random.Clone().Next(candidates.Count)];
                Check(ImbueRules.TriggerCurrentHeroPower(p) == SabberStoneCore.Tasks.TaskState.COMPLETE, "copied power direct effect");
                card = p.HandZone.Single();
                Check(card.Card == expected || expected.Entourage?.Contains(card.Card.Id) == true, "generated minion retains its selected definition or hand-entry form");
                var member = g.OfflinePools.Standard.Single(m => m.CardId == expected.Id);
                Check(member.Type == "MINION" && !member.Classes.Contains(cls.ToString()) && !member.Classes.Contains("NEUTRAL"), "copied power excludes controller class, Neutral and its multiclass cards");
            }
            Check(p.UsedMana == mana && p.NumTimesHeroPowerUsedThisGame == uses && hp.IsExhausted && g.Rewind == null,
                "borrowed effect neither pays nor exhausts nor Inspires nor starts Rewind");
        }
    }
    private static void Lean_BronzeDiscount(int seed, int n)
    {
        var g = Fixture(seed, n); var p = g.Player1;
        Check(g.Process(HeroPowerTask.Any(p)), "discount activation accepted");
        var card = p.HandZone.Single();
        Check(card.Cost == Math.Max(0, card.Card.Cost - n)
            && card.AppliedEnchantments.Single().Card.Id == "END_000e"
            && card[GameTag.CREATOR] == p.Hero.HeroPower.Id && card[GameTag.DISPLAYED_CREATOR] == p.Hero.HeroPower.Id,
            "scaled real enchantment and creators");
        Answer(g, false);
        g = Fixture(seed, n); p = g.Player1;
        var candidates = g.OfflinePools.Standard.Where(m => m.Type == "MINION" && !m.Classes.Contains("ROGUE") && !m.Classes.Contains("NEUTRAL")).ToArray();
        int cheap = Array.FindIndex(candidates, m => m.Cost <= n);
        Check(cheap >= 0, "zero-cost floor fixture exists");
        long cheapSeed = 1;
        while (new SabberStoneCore.Model.Util.DeepCloneableRandom(cheapSeed).Next(candidates.Length) != cheap) cheapSeed++;
        g.SetRandomSeed(cheapSeed); ImbueRules.TriggerCurrentHeroPower(p);
        Check(p.HandZone.Single().Card.Id == candidates[cheap].CardId && p.HandZone.Single().Cost == 0, "discount reaches zero floor at N=" + n);
    }
    private static void Lean_BronzeRewindHeroPower(int seed)
    {
        // Lean_ imbue-24: one retry per activation, baseline before payment, carried game RNG.
        foreach (bool rewind in new[] { false, true })
        {
            var g = Fixture(seed, 2); var p = g.Player1;
            Summon(p, "AT_100"); // Pinned Inspire summons CS2_101t once per paid activation.
            Check(g.Process(HeroPowerTask.Any(p)), "Rewind activation accepted");
            Check(g.Rewind != null, "activation offers Rewind");
            var t = g.Rewind;
            var baseline = Game.LoadSnapshot(t.Baseline);
            Check(baseline.Player1.UsedMana == 0 && !baseline.Player1.Hero.HeroPower.IsExhausted
                && baseline.Player1.HandZone.IsEmpty && t.Budget == 1 && t.Used == 0 && t.IsHeroPowerActivation,
                "baseline before effect/payment and activation-local budget");
            foreach (var bad in new[] { new DecisionAnswer(t.OfferChoiceId - 1, p.Id, new[] { t.RewindEntityId }),
                new DecisionAnswer(t.OfferChoiceId, p.Opponent.Id, new[] { t.KeepEntityId }),
                new DecisionAnswer(t.OfferChoiceId, p.Id, new[] { t.SourceId }),
                new DecisionAnswer(t.OfferChoiceId, p.Id, new[] { t.RewindEntityId, t.KeepEntityId }) })
            {
                var untouched = g.SaveSnapshot();
                Check(!g.TryAnswer(bad, out _) && untouched.SequenceEqual(g.SaveSnapshot()), "invalid answer mutation-free");
            }
            string parent = Canonical.State(g);
            var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
            Answer(clone, rewind);
            Check(Canonical.State(g) == parent, "pending clone cannot mutate parent");
            Answer(loaded, rewind); Answer(g, rewind);
            Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded), "pending clone/load future parity");
            p = g.Player1;
            Check(g.Rewind == null && p.UsedMana == 1 && p.Hero.HeroPower.IsExhausted
                && p.HeroPowerActivationsThisTurn == 1 && p.NumTimesHeroPowerUsedThisGame == 1
                && p.HandZone.Count == 1 && p.NumCardsPlayedThisTurn == 0 && p.PlayHistory.Count == 0
                && !g.HistoryFacts.Query.Plays().Any()
                && p.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 1, "one net payment/use; zero card plays; replay cannot offer again");
            Check(p.Hero.HeroPower[GameTag.USED_REWIND] == 0, "persistent power retry tag untouched");
            var bytes = g.SaveSnapshot();
            Check(!g.TryAnswer(new DecisionAnswer(t.OfferChoiceId, t.ControllerId, new[] { t.RewindEntityId }), out _)
                && bytes.SequenceEqual(g.SaveSnapshot()), "stale answer mutation-free");
            Check(g.Process(EndTurnTask.Any(p)) && g.Process(EndTurnTask.Any(g.CurrentPlayer)), "later own turn");
            Check(g.Process(HeroPowerTask.Any(g.Player1)) && g.Rewind != null, "next activation gets fresh budget");
            Answer(g, false);
        }
    }
    private static void Identity()
    {
        var export = ClientExport.TryCards();
        foreach (var (id, dbf) in new[] { ("END_000p", 121243), ("END_000e", 121257) })
        {
            var card = Cards.FromId(id);
            Check(card.Implemented && EffectModules.ByDbfId[dbf] == id && card.AssetId == dbf && card.ATK == 0 && card.Health == 0,
                "pinned identity/stats " + id);
            if (export == null) { ClientExport.Skip("Blessing of the Bronze identity, text and tags against the client export"); continue; }
            var raw = export[id];
            Check(card.Cost == raw.GetProperty("cost").GetInt32() && card.Text == raw.GetProperty("text").GetString(), "pinned cost/text " + id);
            foreach (var tag in raw.GetProperty("tags").EnumerateObject())
                Check(card.Tags.GetValueOrDefault((GameTag)int.Parse(tag.Name)) == tag.Value.GetInt32(), "pinned tags " + id);
        }
        Check(Cards.FromId("END_000p").ExcludeFromLegacyRandomPools && Cards.FromId("END_000p")[GameTag.REWIND] == 1,
            "hero power exclusion and native keyword");
    }

    private static void Lean_BronzeMorchieKeepBoth(int seed)
    {
        // Lean_ imbue-24 + rewind/morchie-keep-both: repeat effect only; retain both generated cards.
        foreach (bool rewind in new[] { false, true })
        {
            var g = Fixture(seed, 2); var p = g.Player1;
            QuestRules.AddPlayerEffect(p, p.Hero, "END_036e"); Summon(p, "AT_100");
            Check(g.Process(HeroPowerTask.Any(p)) && g.Rewind.KeepBoth, "Morchie active at offer");
            int first = p.HandZone.Single().Id;
            var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
            foreach (var game in new[] { g, clone, loaded }) Answer(game, rewind);
            Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded), "Morchie pending clone/load parity");
            Check(p.HandZone.Count == (rewind ? 2 : 1) && p.HandZone.Any(c => c.Id == first)
                && p.UsedMana == 1 && p.HeroPowerActivationsThisTurn == 1 && p.NumTimesHeroPowerUsedThisGame == 1
                && p.BoardZone.Count(m => m.Card.Id == "CS2_101t") == 1 && g.Rewind == null
                && !g.PowerHistory.Full.OfType<PowerHistoryResetGame>().Any()
                && p.HandZone.All(c => c.Cost == Math.Max(0, c.Card.Cost - 2)), "Morchie retains first; second effect only, no payment/use/Inspire/reset");
        }
    }

    private static void Boundaries(int seed)
    {
        var g = Fixture(seed, 2); var p = g.Player1;
        var source = p.Hero.HeroPower;
        foreach (var task in new[] { HeroPowerTask.Any(p.Opponent), HeroPowerTask.Any(p, chooseOne: 1), HeroPowerTask.Any(p, p.Opponent.Hero) })
            Check(!SabberStoneCore.Model.Rewind.RewindRules.WouldBegin(g, task), "wrong controller/choose-one/target cannot begin HP transaction");
        source.IsExhausted = true;
        Check(!SabberStoneCore.Model.Rewind.RewindRules.WouldBegin(g, HeroPowerTask.Any(p, skipPrePhase: true)), "forced exhausted HP cannot open transaction");
        source.IsExhausted = false; source[GameTag.REWIND] = 0;
        Check(!SabberStoneCore.Model.Rewind.RewindRules.WouldBegin(g, HeroPowerTask.Any(p)), "native REWIND required");
        source[GameTag.REWIND] = 1; source[GameTag.USED_REWIND] = 9;
        Check(g.Process(HeroPowerTask.Any(p)) && g.Rewind != null, "persistent USED_REWIND does not gate HP activation");
        Answer(g, true);
        Check(g.Player1.Hero.HeroPower[GameTag.USED_REWIND] == 9, "HP retry neither compares nor writes persistent USED_REWIND");
        g = Fixture(seed, 2); p = g.Player1;
        Check(g.Process(HeroPowerTask.Any(p)), "magnitude attempt");
        ImbueRules.Imbue(p, p.Hero, 3);
        new ReplaceHeroPower(Cards.FromId("CS2_034")).Process(g, p, p.Hero, null);
        Answer(g, true); p = g.Player1;
        Check(p.Hero.HeroPower.Card.Id == "END_000p" && p.Hero.HeroPower[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2
            && ImbueRules.Count(p) == 2 && p.HandZone.Single().Cost == Math.Max(0, p.HandZone.Single().Card.Cost - 2),
            "candidate replacement and magnitude changes roll back to baseline power");
        // Same-turn refreshed activation gets an independent retry, too.
        p.Hero.HeroPower.IsExhausted = false;
        Check(g.Process(HeroPowerTask.Any(p)) && g.Rewind != null, "refreshed same-turn activation gets fresh budget"); Answer(g, true);
        Check(p.Game.Player1.NumTimesHeroPowerUsedThisGame == 2 && g.Rewind == null, "same-turn replay commits second use");

        foreach (int capacity in new[] { 10, 11 })
        foreach (bool rewind in new[] { false, true })
        {
            g = Fixture(seed); p = g.Player1;
            p[GameTag.MAXHANDSIZE] = capacity;
            for (int i = 0; i < capacity; i++) Add(p, "CS2_182");
            while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
            int[] held = p.HandZone.Select(c => c.Id).ToArray();
            int entities = g.IdEntityDic.Count; var fullRng = g.Random.CaptureState();
            Check(!p.Hero.HeroPower.IsPlayable
                && p.Options().All(t => t.PlayerTaskType != PlayerTaskType.HERO_POWER)
                && !SabberStoneCore.Model.Rewind.RewindRules.WouldBegin(g, HeroPowerTask.Any(p))
                && !g.Process(HeroPowerTask.Any(p)), "full hand rejects Bronze activation/options/Rewind");
            Check(p.HandZone.Select(c => c.Id).SequenceEqual(held) && p.UsedMana == 0
                && p.NumTimesHeroPowerUsedThisGame == 0 && p.HeroPowerActivationsThisTurn == 0
                && !p.Hero.HeroPower.IsExhausted && p.Choice == null && g.Rewind == null
                && g.IdEntityDic.Count == entities && fullRng.Equals(g.Random.CaptureState()),
                "rejected Bronze activation preserves hand, payment, uses, exhaustion, choices, entities and RNG");
            Check(ImbueRules.TriggerCurrentHeroPower(p) == SabberStoneCore.Tasks.TaskState.COMPLETE,
                "full-hand direct Bronze effect still resolves");
            var burned = p.GraveyardZone.Where(c => c[GameTag.CREATOR] == p.Hero.HeroPower.Id).ToArray();
            Check(p.HandZone.Select(c => c.Id).SequenceEqual(held) && burned.Length == 1
                && burned[0].Card.Type == CardType.MINION && burned[0].AppliedEnchantments.Count == 0
                && p.UsedMana == 0 && p.Choice == null && g.Rewind == null,
                "direct Bronze effect burns without activation payment or Rewind");
            p.SetasideZone.Add(p.HandZone.Remove(p.HandZone.Last()));
            Check(p.Hero.HeroPower.IsPlayable && g.Process(HeroPowerTask.Any(p)) && g.Rewind != null
                && p.HandZone.Count == capacity, "one free slot permits Bronze and fills hand before Rewind");
            Answer(g, rewind); p = g.Player1;
            Check(p.HandZone.Count == capacity && p.UsedMana == 1 && p.Hero.HeroPower.IsExhausted
                && p.NumTimesHeroPowerUsedThisGame == 1 && p.HeroPowerActivationsThisTurn == 1
                && p.Choice == null && g.Rewind == null && p.BoardZone.SlotCount == 7,
                "Keep/Rewind from one free slot settles one net payment/use despite full candidate hand");
        }
        g = Fixture(seed); p = g.Player1;
        Summon(p, "AT_012"); p.Opponent.Hero.Damage = p.Opponent.Hero.BaseHealth - 4;
        Check(g.Process(HeroPowerTask.Any(p)) && g.State == State.COMPLETE && g.Rewind == null && p.Choice == null,
            "lethal Inspire settles game without Rewind offer");

        g = Fixture(seed); p = g.Player1;
        var xml = OfflinePoolFixtures.Catalog();
        foreach (var member in xml.Root.Element("Members").Elements("Card").Where(c => (string)c.Attribute("scope") == "Standard").ToArray()) member.Remove();
        typeof(Game).GetProperty(nameof(Game.OfflinePools)).SetValue(g, OfflinePoolFixtures.Manifest(xml));
        var rng = g.Random.CaptureState();
        Check(ImbueRules.TriggerCurrentHeroPower(p) == SabberStoneCore.Tasks.TaskState.COMPLETE
            && p.HandZone.IsEmpty && rng.Equals(g.Random.CaptureState()) && g.Rewind == null, "empty pool completes with no RNG/entity/choice");

        g = Fixture(seed); p = g.Player1;
        int count = g.OfflinePools.Standard.Count(m => m.Type == "MINION" && !m.Classes.Contains("ROGUE") && !m.Classes.Contains("NEUTRAL"));
        long identicalSeed = 1;
        for (;; identicalSeed++)
        {
            var random = new SabberStoneCore.Model.Util.DeepCloneableRandom(identicalSeed);
            if (random.Next(count) == random.Next(count)) break;
        }
        g.SetRandomSeed(identicalSeed);
        Check(g.Process(HeroPowerTask.Any(p)), "identical-reroll attempt");
        string firstCard = p.HandZone.Single().Card.Id;
        Answer(g, true);
        Check(g.Player1.HandZone.Single().Card.Id == firstCard && g.Rewind == null, "identical reroll permitted without exclusion/resampling");
    }

}
