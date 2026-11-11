using System.IO.Compression;
using System.Text.Json;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.CardSets.Client;
using SabberStoneCore.Config;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Events;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class DarkGiftProof
{
    private static readonly (string Spell, int Dbf, string Enchantment, int EnchantmentDbf)[] Expected = {
        ("EDR_100t", 113796, "EDR_100te", 113803), ("EDR_100t1", 113797, "EDR_100t1e", 113804),
        ("EDR_100t2", 113798, "EDR_100t2e", 113805), ("EDR_100t3", 113799, "EDR_100t3e", 113806),
        ("EDR_100t5", 113801, "EDR_100t5e", 113808), ("EDR_100t6", 113802, "EDR_100t6e", 113809),
        ("EDR_100t7", 113882, "EDR_100t7e", 113883), ("EDR_100t8", 113884, "EDR_100t8e1", 115355),
        ("EDR_100t9", 113886, "EDR_100t9e", 113887), ("EDR_100t13", 120225, "EDR_100t13e", 120226)
    };
    private static readonly OfflinePoolFilter Filter = OfflinePoolDeclarations.PastFiveCostMinion();
    private static readonly SuspendableTask Recipe = StateTaskList.Suspendable("proof/dark-gift/v1",
        new DiscoverWithDarkGiftThenContinueTask(Filter), new CustomTask(Continue));
    private static void Check(bool value, string label)
    { if (!value) throw new Exception("dark gift: " + label); }
    private static Game New(long seed = 1, bool history = true)
    {
        var game = new Game(new GameConfig { RandomSeed = seed, StartPlayer = 1, Shuffle = false,
            SkipMulligan = true, History = history, Logging = false, Player1HeroClass = CardClass.MAGE,
            Player2HeroClass = CardClass.WARRIOR,
            Player1Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList(),
            Player2Deck = Enumerable.Repeat(Cards.FromId("CS2_231"), 30).ToList() });
        game.StartGame(); game.Player1.BaseMana = 10;
        return PinnedPoolFixture.Use(game);
    }
    private static IPlayable Source(Game g) => Entity.FromCard(g.Player1, Cards.FromId("TIME_040"), zone: g.Player1.SetasideZone);
    private static EffectContext Context(Game g, IPlayable source = null) => new(g, g.Player1.Id, (source ?? g.Player1.Hero).Id);
    private static Minion Minion(Game g, string id = "CS2_182", Zone zone = Zone.HAND) =>
        (Minion)Entity.FromCard(g.Player1, Cards.FromId(id), zone: zone == Zone.HAND ? g.Player1.HandZone :
            zone == Zone.PLAY ? g.Player1.BoardZone : zone == Zone.DECK ? g.Player1.DeckZone : g.Player1.SetasideZone);
    private static void Buff(Game g, IPlayable target, int gift) =>
        Generic.AddEnchantmentBlock(g, Cards.FromId(Expected[gift].Enchantment), g.Player1.Hero, target);
    private static DecisionAnswer Answer(Game g, int index = 0)
    {
        var view = g.GetPendingDecisions().Single();
        return new(view.Id, view.ControllerId, new[] { view.Options[index].OptionId });
    }
    private static void Accept(Game g, DecisionAnswer answer) => Check(g.TryAnswer(answer, out var error), error);
    private static string Pairings(Game g) => string.Join(",", g.Player1.Choice.Choices.Select(id =>
        g.IdEntityDic[g.IdEntityDic[id][GameTag.DARK_GIFT_ENTITY]].Card.Id));
    private static void Continue(Game g, Controller c, IEntity s, IPlayable t, TaskStack stack)
    {
        Check(s.Card.Id == "TIME_040", "continuation real source");
        var chosen = stack.Playables.Single();
        Check(DarkGifts.Has(chosen) && chosen[GameTag.HAS_DARK_GIFT] == 1, "gift before suspended continuation");
        Generic.RemoveFromZone(c, chosen); Generic.AddHandPhase(c, chosen);
        c.Hero.Armor++;
    }

    private static void Registry()
    {
        Check(DarkGifts.PolicyVersion == "dark-gift/1", "pinned gift policy version");
        Check(DarkGifts.Registry.Select(g => (g.SpellId, g.SpellDbfId, g.EnchantmentId, g.EnchantmentDbfId))
            .SequenceEqual(Expected), "exact ten ordered ids/dbfIds");
        bool immutable = false;
        try { ((IList<DarkGifts.Gift>)DarkGifts.Registry)[0] = DarkGifts.Registry[1]; }
        catch (NotSupportedException) { immutable = true; }
        Check(immutable, "immutable registry");
        var export = ClientExport.TryCards();
        foreach (var identity in Expected.SelectMany(row => new[] { (row.Spell, row.Dbf), (row.Enchantment, row.EnchantmentDbf) })
            .Concat(new[] { ("EDR_102t", 115323), ("EDR_100t5e5", 120020) }))
        {
            var card = Cards.FromId(identity.Item1);
            Check(card.AssetId == identity.Item2, "pinned dbfId " + identity.Item1);
            Check(!card.Collectible && card.Implemented && card.ExcludeFromLegacyRandomPools, "registered excluded token " + identity.Item1);
            if (export == null) { ClientExport.Skip("Dark Gift identity, text and tags against the client export"); continue; }
            var row = export[identity.Item1];
            Check(card.AssetId == row.GetProperty("dbfId").GetInt32() &&
                card.Name == row.GetProperty("name").GetString() && card.Text == row.GetProperty("text").GetString(), "pinned identity/name/text " + identity.Item1);
            Check(card.Type.ToString() == row.GetProperty("cardType").GetString(), "pinned card type " + identity.Item1);
            foreach (var tag in row.GetProperty("tags").EnumerateObject())
                Check(card[(GameTag)int.Parse(tag.Name)] == tag.Value.GetInt32(), "export tag " + identity.Item1 + "/" + tag.Name);
        }
        foreach (var row in Expected)
        {
            Check(Cards.FromId(row.Spell)[GameTag.IS_NIGHTMARE_BONUS] == 1 &&
                Cards.FromId(row.Enchantment)[GameTag.IS_NIGHTMARE_BONUS] == 1 &&
                Cards.FromId(row.Enchantment)[GameTag.TAG_SCRIPT_DATA_NUM_6] == row.Dbf, "gift definition tags");
        }
        Check((int)GameTag.IS_NIGHTMARE_BONUS == 3567 && (int)GameTag.HAS_DARK_GIFT == 3880 &&
            (int)GameTag.DARK_GIFT_ENTITY == 4865 && (int)GameTag.TAG_SCRIPT_DATA_NUM_6 == 2921 &&
            (int)GameTag.TAG_SCRIPT_DATA_ENT_1 == 4, "native tag numbers");
    }

    private static void Eligibility()
    {
        var g = New(); var m = Minion(g);
        for (int attack = 0; attack <= 3; attack++)
        {
            m.AttackDamage = attack;
            Check(DarkGifts.Registry[2].IsEligible(m) == (attack >= 3), "Short Claws attack boundary " + attack);
            Check(DarkGifts.Registry[5].IsEligible(m) == (attack >= 1), "Sleepwalker attack boundary " + attack);
        }
        m.AttackDamage = 3;
        foreach (var (gift, tag) in new[] { (0, GameTag.LIFESTEAL), (3, GameTag.TAUNT), (5, GameTag.CHARGE),
            (8, GameTag.REBORN), (9, GameTag.DIVINE_SHIELD), (9, GameTag.WINDFURY) })
        {
            Check(DarkGifts.Registry[gift].IsEligible(m), "keyword initially eligible " + tag);
            m[tag] = 1;
            Check(!DarkGifts.Registry[gift].IsEligible(m), "ANY existing keyword excludes " + tag);
            m[tag] = 0;
        }
        m.CantBeTargetedBySpells = true;
        Check(!DarkGifts.Registry[1].IsEligible(m) && m.CantBeTargetedByHeroPowers, "effective paired Elusive");
        m.CantBeTargetedBySpells = false;
        Check(DarkGifts.Registry[1].IsEligible(m), "Elusive absent");
        Check(!DarkGifts.Registry[6].IsEligible(m), "no Battlecry");
        m.HasBattleCry = true;
        Check(DarkGifts.Registry[6].IsEligible(m), "current Battlecry present");
        m.Silence();
        Check(!DarkGifts.Registry[6].IsEligible(m), "silenced Battlecry absent");
        Check(DarkGifts.Registry[4].IsEligible(m) && DarkGifts.Registry[7].IsEligible(m), "unrestricted gifts");
        Buff(g, m, 0);
        Check(!DarkGifts.Registry[0].IsEligible(m), "existing gift changes effective eligibility");
    }

    private static void EffectsAndRemoval()
    {
        foreach (bool history in new[] { false, true })
        for (int gift = 0; gift < Expected.Length; gift++)
        {
            var g = New(history: history); var m = Minion(g);
            int attack = m.AttackDamage, health = m.BaseHealth, cost = m.Cost;
            Buff(g, m, gift);
            Check(DarkGifts.Has(m) && Context(g).HasDarkGift(m.Id) && m[GameTag.HAS_DARK_GIFT] == 1, "live gift history parity");
            var e = DarkGifts.GiftsOf(m).Single();
            Check(e.Card.Id == Expected[gift].Enchantment && e[GameTag.IS_NIGHTMARE_BONUS] == 1 &&
                e[GameTag.TAG_SCRIPT_DATA_NUM_6] == Expected[gift].Dbf && e[GameTag.ATTACHED] == m.Id &&
                e[GameTag.CREATOR] == g.Player1.Hero.Id && e[GameTag.ZONE] == (int)Zone.PLAY &&
                e[GameTag.ENTITY_ID] == e.Id && e[GameTag.CONTROLLER] == g.Player1.PlayerId, "live attachment tags");
            int expectedAttack = attack + (gift == 0 ? 3 : gift == 1 ? 2 : gift == 2 ? -2 : gift == 7 ? 4 : 0);
            int expectedHealth = health + (gift == 1 ? 2 : gift == 3 ? 4 : gift == 7 ? 5 : 0);
            Check(m.AttackDamage == expectedAttack && m.BaseHealth == expectedHealth &&
                m.Cost == Math.Max(0, cost - (gift == 2 ? 2 : 0)) && m[GameTag.COST] == m.Cost, "gift stats/cost " + gift);
            Check(m.HasLifeSteal == (gift == 0) && m.CantBeTargetedBySpells == (gift == 1) &&
                m.CantBeTargetedByHeroPowers == (gift == 1) && m.HasTaunt == (gift == 3) && m.HasCharge == (gift == 5) &&
                m.HasDivineShield == (gift == 9) && m.HasWindfury == (gift == 9) && (m[GameTag.REBORN] > 0) == (gift == 8), "gift keywords " + gift);
            var copy = Generic.Copy(g.Player1, g.Player1.Hero, m, Zone.HAND);
            Check(DarkGifts.Has(copy) && copy[GameTag.HAS_DARK_GIFT] == 1 && copy.Cost == m.Cost &&
                ((Minion)copy).AttackDamage == m.AttackDamage && ((Minion)copy).BaseHealth == m.BaseHealth &&
                copy[GameTag.LIFESTEAL] == m[GameTag.LIFESTEAL] && copy[GameTag.CHARGE] == m[GameTag.CHARGE] &&
                copy[GameTag.DIVINE_SHIELD] == m[GameTag.DIVINE_SHIELD] && copy[GameTag.WINDFURY] == m[GameTag.WINDFURY] &&
                copy[GameTag.REBORN] == m[GameTag.REBORN], "hand copy keeps effects " + gift);
            Check(DarkGifts.GiftsOf(copy).Single().Id != e.Id && ReferenceEquals(DarkGifts.GiftsOf(copy).Single().Target, copy), "copy fresh attachment");
            var clone = g.CloneExact(history: history);
            Check(DarkGifts.Has(clone.IdEntityDic[m.Id]) && !ReferenceEquals(DarkGifts.GiftsOf(clone.IdEntityDic[m.Id]).Single(), e), "clone fresh graph");
            g.Player1.HandZone.Remove(m);
            Generic.ShuffleIntoDeck(g.Player1, g.Player1.Hero, m);
            Check(!DarkGifts.Has(m) && m[GameTag.HAS_DARK_GIFT] == 0 && m.AttackDamage == attack &&
                m.BaseHealth == health && m.Cost == cost, "hand shuffle strips gift effects " + gift);
        }
        foreach (int cost in new[] { 0, 1, 2, 3 })
        {
            var g = New(); var m = Minion(g); m.Cost = cost; Buff(g, m, 2);
            Check(m.Cost == Math.Max(0, cost - 2) && m[GameTag.COST] == m.Cost, "Short Claws zero floor");
        }
        {
            var g = New(); var m = Minion(g);
            Buff(g, m, 0); Buff(g, m, 0); Buff(g, m, 7);
            Check(DarkGifts.GiftsOf(m).Select(e => e.Card.Id).SequenceEqual(new[] { "EDR_100te", "EDR_100te", "EDR_100t8e1" }), "ordered duplicate gifts");
            // A plain put into deck bypasses ShuffleIntoDeck and must strip gifts too.
            g.Player1.DeckZone.Add(g.Player1.HandZone.Remove(m));
            Check(!DarkGifts.Has(m) && m.AttackDamage == m.Card.ATK && m.BaseHealth == m.Card.Health, "plain hand-to-deck");
        }
        foreach (bool bounce in new[] { false, true })
        {
            var g = New(); var m = Minion(g, zone: Zone.PLAY);
            Buff(g, m, 0); Buff(g, m, 5); Buff(g, m, 7);
            Check(m.HasCharge && !m.IsExhausted, "gift Charge readies fresh minion");
            if (bounce) Check(Generic.ReturnToHandBlock(g.Player1, m), "bounce succeeds");
            else m.Silence();
            Check(!DarkGifts.Has(m) && m[GameTag.HAS_DARK_GIFT] == 0 && m.AttackDamage == m.Card.ATK &&
                m.BaseHealth == m.Card.Health && !m.HasCharge, "silence/bounce removes gifts");
            if (!bounce) Check(m.IsExhausted && !m.CanAttack, "silence restores summoning sickness");
        }
        {
            var g = New(); var m = Minion(g);
            Generic.AddEnchantmentBlock(g, Cards.FromId("CS2_009e"), g.Player1.Hero, m);
            Buff(g, m, 7);
            g.Player1.DeckZone.Add(g.Player1.HandZone.Remove(m));
            Check(m.AppliedEnchantments.Single().Card.Id == "CS2_009e" && m.AttackDamage == m.Card.ATK + 2 &&
                m.BaseHealth == m.Card.Health + 2, "backward cleanup keeps non-gift attachment/effects");
            Buff(g, m, 0);
            Generic.ChangeEntityBlock(g.Player1, m, Cards.FromId("CS2_231"), true);
            Check(!DarkGifts.Has(m) && m[GameTag.HAS_DARK_GIFT] == 0, "transform clears gifts/flag");
        }
        {
            var g = New(); var m = Minion(g);
            Buff(g, m, 0);
            Generic.AddEnchantmentBlock(g, Cards.FromId("ICC_221e"), g.Player1.Hero, m);
            g.Player1.DeckZone.Add(g.Player1.HandZone.Remove(m));
            Check(m.HasLifeSteal && m.AppliedEnchantments.Single().Card.Id == "ICC_221e", "gift removal keeps another attachment's keyword");
        }
        {
            var g = New(); var m = Minion(g, zone: Zone.PLAY); Buff(g, m, 7);
            g.Player1.HandZone.Add(g.Player1.BoardZone.Remove(m));
            Check(!DarkGifts.Has(m) && m.AttackDamage == m.Card.ATK && m.BaseHealth == m.Card.Health, "direct board-to-hand strips gift");
            Buff(g, m, 7); m.PreserveDarkGiftsOnNextBackwardMove = true;
            g.Player1.DeckZone.Add(g.Player1.HandZone.Remove(m));
            Check(DarkGifts.Has(m) && m[GameTag.HAS_DARK_GIFT] == 1, "explicit Wallow Sweet Dreams preservation hook");
        }
    }

    private static void IndependentKeywords()
    {
        foreach (bool history in new[] { false, true })
        foreach (bool grantBefore in new[] { false, true })
        {
            var g = New(history: history); var m = Minion(g);
            if (grantBefore) Generic.AddEnchantmentBlock(g, Cards.FromId("ICC_221e"), g.Player1.Hero, m);
            Buff(g, m, 0); Buff(g, m, 0);
            if (!grantBefore) Generic.AddEnchantmentBlock(g, Cards.FromId("ICC_221e"), g.Player1.Hero, m);
            foreach (var game in new[] { g, g.CloneExact(history: history), PinnedPoolFixture.LoadSnapshot(g.SaveSnapshot()) })
            {
                var target = (Minion)game.IdEntityDic[m.Id];
                game.Player1.DeckZone.Add(game.Player1.HandZone.Remove(target));
                Check(target.HasLifeSteal && !DarkGifts.Has(target), "independent Lifesteal survives duplicate gifts history=" + history + " before=" + grantBefore);
            }
        }
        foreach (bool history in new[] { false, true })
        {
            var g = New(history: history); var printed = Minion(g, "CS2_179");
            Check(printed.Card[GameTag.TAUNT] == 1, "printed Taunt fixture");
            new SabberStoneCore.Enchants.Effect(GameTag.LIFESTEAL, SabberStoneCore.Enchants.EffectOperator.SET, 1).ApplyAuraTo(printed);
            printed.AuraEffects.Taunt = true;
            Buff(g, printed, 3);
            g.Player1.DeckZone.Add(g.Player1.HandZone.Remove(printed));
            printed.AuraEffects.Taunt = false;
            Check(printed.HasTaunt, "printed keyword survives gift cleanup beneath aura");
            var aura = Minion(g);
            new SabberStoneCore.Enchants.Effect(GameTag.LIFESTEAL, SabberStoneCore.Enchants.EffectOperator.SET, 1).ApplyAuraTo(aura);
            aura.AuraEffects.Taunt = true;
            Buff(g, aura, 0); Buff(g, aura, 3);
            g.Player1.DeckZone.Add(g.Player1.HandZone.Remove(aura));
            Check(aura.HasLifeSteal, "aura keyword survives gift cleanup");
            new SabberStoneCore.Enchants.Effect(GameTag.LIFESTEAL, SabberStoneCore.Enchants.EffectOperator.SET, 1).RemoveAuraFrom(aura);
            aura.AuraEffects.Taunt = false;
            Check(!aura.HasLifeSteal && !aura.HasTaunt, "gift cleanup leaves no phantom native aura keyword");
        }
        Console.WriteLine("PASS dark gift IndependentKeywords: before/after non-gift grants; duplicate gifts; printed/aura; history on/off; clone/snapshot");
    }

    private static void SilenceClearsBaselines()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = New(history: history); var m = Minion(g, zone: Zone.PLAY);
            Buff(g, m, 0);
            Generic.AddEnchantmentBlock(g, Cards.FromId("ICC_221e"), g.Player1.Hero, m);
            m.Silence();
            Check(!m.HasLifeSteal && !DarkGifts.Has(m), "Silence removes both keyword grants");
            Buff(g, m, 0);
            g.Player1.HandZone.Add(g.Player1.BoardZone.Remove(m));
            Check(!m.HasLifeSteal && !DarkGifts.Has(m), "backward move cannot resurrect silenced Lifesteal history=" + history);
        }
        Console.WriteLine("PASS dark gift SilenceClearsBaselines: gift/independent grant/Silence/regift/backward move; history on/off");
    }

    private static void ClonePreservesBackwardMove()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = New(history: history); var m = Minion(g, zone: Zone.PLAY);
            Buff(g, m, 7); m.PreserveDarkGiftsOnNextBackwardMove = true;
            int historyCount = history ? g.PowerHistory.Full.Count : 0;
            var clone = g.CloneExact(history: history);
            foreach (var game in new[] { g, clone })
            {
                var target = (Minion)game.IdEntityDic[m.Id];
                game.Player1.HandZone.Add(game.Player1.BoardZone.Remove(target));
                Check(DarkGifts.Has(target) && !target.PreserveDarkGiftsOnNextBackwardMove,
                    "original/clone preserve exactly next backward move history=" + history);
                game.Player1.DeckZone.Add(game.Player1.HandZone.Remove(target));
                Check(!DarkGifts.Has(target), "second backward move removes gift");
            }
            Check(Canonical.State(g) == Canonical.State(clone) && (!history ||
                Canonical.Value(g.PowerHistory.Full.Skip(historyCount)) == Canonical.Value(clone.PowerHistory.Full)),
                "original/clone movement parity history=" + history);
        }
        Console.WriteLine("PASS dark gift ClonePreservesBackwardMove: CloneExact retains one-use preservation; history on/off");
    }

    private static readonly DecisionBattlecryTask GiftBattlecry = new("proof/dark-gift/battlecry/v1",
        new DiscoverWithDarkGiftThenContinueTask(Filter), new CustomTask(Continue));

    private static void PendingBattlecrySnapshot()
    {
        var card = Cards.FromId("TIME_040"); var original = card.Power;
        try
        {
            card.Power = new SabberStoneCore.Enchants.Power { PowerTask = GiftBattlecry };
            foreach (bool history in new[] { false, true })
            {
                var g = New(history: history);
                var source = Entity.FromCard(g.Player1, card, zone: g.Player1.HandZone);
                source[GameTag.BATTLECRY] = 1;
                int mana = source.Cost, plays = g.Player1.PlayHistory.Count;
                Check(g.Process(PlayCardTask.Any(g.Player1, source)), "real gifted Battlecry PlayCard dispatch");
                Check(Generic.HasDecisionPlay(g.Player1) && g.Resolution.HasSuspendedEffect &&
                    g.Player1.Choice.SourceId != source.Id && !g.Player1.IsComboActive, "pending outer play with executor source");
                int historyCount = history ? g.PowerHistory.Full.Count : 0;
                var bytes = g.SaveSnapshot(); var restored = PinnedPoolFixture.LoadSnapshot(bytes); var clone = g.CloneExact(history: history);
                Check(bytes.SequenceEqual(restored.SaveSnapshot()), "pending Battlecry snapshot-v2 bytes");
                var answer = Answer(g);
                foreach (var game in new[] { g, restored, clone })
                {
                    Accept(game, answer);
                    Check(!Generic.HasDecisionPlay(game.Player1) && !game.Resolution.HasSuspendedEffect &&
                        game.Player1.Choice == null && game.Player1.IsComboActive && game.Player1.Hero.Armor == 1 &&
                        game.Player1.UsedMana == mana && game.Player1.PlayHistory.Count == plays + 1 &&
                        game.Player1.NumMinionsPlayedThisTurn == 1, "resumed Battlecry completes outer play exactly once");
                }
                Check(Canonical.State(g) == Canonical.State(restored) && Canonical.State(g) == Canonical.State(clone) &&
                    (!history || Canonical.History(g) == Canonical.History(restored) &&
                        Canonical.Value(g.PowerHistory.Full.Skip(historyCount)) == Canonical.Value(clone.PowerHistory.Full)) &&
                    g.Random.CaptureState().Equals(restored.Random.CaptureState()), "pending Battlecry future parity");
            }
        }
        finally { card.Power = original; }
        Console.WriteLine("PASS dark gift PendingBattlecrySnapshot: real PlayCard dispatch; pending snapshot-v2/CloneExact; one completion; history on/off");
    }

    private static void CopyCurrentKeywords()
    {
        foreach (bool history in new[] { false, true })
        {
            var g = New(history: history); var m = Minion(g, zone: Zone.PLAY);
            Buff(g, m, 9);
            m.TakeDamage(g.Player2.Hero, 1);
            Check(!m.HasDivineShield && m.HasWindfury, "consumed shield fixture");
            var copy = (Minion)Generic.Copy(g.Player1, g.Player1.Hero, m, Zone.PLAY);
            Check(!copy.HasDivineShield && copy.HasWindfury && DarkGifts.Has(copy), "Generic.Copy preserves consumed shield");
            int boardCount = g.Player1.BoardZone.Count;
            Check(new SummonCopyTask(EntityType.TARGET).Process(g, g.Player1, g.Player1.Hero, m) == TaskState.COMPLETE &&
                g.Player1.BoardZone.Count == boardCount + 1, "SummonCopyTask creates a new copy");
            copy = g.Player1.BoardZone.GetAll().Last();
            Check(copy.Id != m.Id && !copy.HasDivineShield && copy.HasWindfury && DarkGifts.Has(copy), "SummonCopyTask preserves consumed shield");

            var hand = Minion(g);
            Buff(g, hand, 0); Buff(g, hand, 3); Buff(g, hand, 5); Buff(g, hand, 9);
            Generic.AddEnchantmentBlock(g, Cards.FromId("ICC_221e"), g.Player1.Hero, hand);
            copy = (Minion)Generic.Copy(g.Player1, g.Player1.Hero, hand, Zone.HAND);
            Check(copy.HasLifeSteal && copy.HasTaunt && copy.HasCharge && copy.HasWindfury && copy.HasDivineShield,
                "hand copy keeps current Lifesteal/Taunt/Charge/Windfury/Divine Shield");
            g.Player1.DeckZone.Add(g.Player1.HandZone.Remove(copy));
            Check(copy.HasLifeSteal && !copy.HasCharge && !copy.HasTaunt && !copy.HasWindfury && !copy.HasDivineShield,
                "copy retains independent grant bookkeeping");
        }
        Console.WriteLine("PASS dark gift CopyCurrentKeywords: consumed shield Generic.Copy/SummonCopyTask; hand keywords; independent grants; history on/off");
    }

    private static void OverlappingOffers()
    {
        var g = New(); var ctx = Context(g, Source(g));
        var first = Minion(g, zone: Zone.DECK); var shared = Minion(g, zone: Zone.DECK);
        Check(ctx.DiscoverEntitiesWithDarkGift(new[] { first }), "first pending choice");
        Check(ctx.DiscoverEntitiesWithDarkGift(new[] { shared }), "disjoint queued choice");
        foreach (var game in new[] { g, g.CloneExact(history: true), PinnedPoolFixture.LoadSnapshot(g.SaveSnapshot()) })
        {
            var before = game.SaveSnapshot(); bool rejected = false;
            try { Context(game).DiscoverEntitiesWithDarkGift(new[] { game.IdEntityDic[first.Id], game.IdEntityDic[shared.Id] }); }
            catch (InvalidOperationException) { rejected = true; }
            Check(rejected && before.SequenceEqual(game.SaveSnapshot()), "overlapping head/tail offers reject without mutation");
            Accept(game, Answer(game)); Accept(game, Answer(game));
            Check(DarkGifts.Has(game.IdEntityDic[first.Id]) && DarkGifts.Has(game.IdEntityDic[shared.Id]) && game.Player1.Choice == null,
                "original queued offers still resolve");
        }
        Console.WriteLine("PASS dark gift OverlappingOffers: rejection preserves bytes/RNG/links; original queue resolves; clone/snapshot");
    }

    private static void PolicyCompatibility()
    {
        var legacy = Simulation.Create(new GameRecord { Id = "dark-gift-legacy", Suite = "classic", Player = "mage",
            Opponent = "warrior", Seed = 7, ReplaySchema = 2 }, replay: true);
        var ctx = Context(legacy, Source(legacy)); var offered = Minion(legacy, zone: Zone.DECK);
        foreach (Action action in new Action[] {
            () => ctx.GiveDarkGift(offered.Id),
            () => ctx.DiscoverWithDarkGift(new[] { offered.Card }),
            () => ctx.DiscoverEntitiesWithDarkGift(new[] { offered }) })
        {
            var before = legacy.SaveSnapshot(); bool rejected = false;
            try { action(); } catch (InvalidOperationException e) { rejected = e.Message.Contains("legacy replay"); }
            Check(rejected && before.SequenceEqual(legacy.SaveSnapshot()), "incompatible legacy replay rejects gift primitive before mutation");
        }
        var bytes = New().SaveSnapshot();
        using (var stream = new MemoryStream(bytes, writable: true))
        using (var reader = new BinaryReader(stream))
        {
            reader.ReadString(); reader.ReadInt32(); reader.ReadString();
            var mvid = reader.ReadString(); int start = (int)stream.Position - mvid.Length;
            bytes[start] = bytes[start] == (byte)'0' ? (byte)'1' : (byte)'0';
        }
        bool incompatible = false;
        try { PinnedPoolFixture.LoadSnapshot(bytes); }
        catch (System.Runtime.Serialization.SerializationException e) { incompatible = e.Message.Contains("engine schema"); }
        Check(incompatible, "snapshot rejects incompatible gift engine MVID");
        Console.WriteLine("PASS dark gift PolicyCompatibility: incompatible legacy replay rejected without mutation; changed engine MVID rejected");
    }

    private static void ReplayPolicyMetadata()
    {
        PoolSnapshots.Save();
        var record = new GameRecord { Id = "dark-gift-policy", Suite = "classic", Player = "mage", Opponent = "warrior", Seed = 7 };
        var played = Simulation.Run(record);
        Simulation.Finish(record, played);
        var json = JsonSerializer.SerializeToNode(record);
        foreach (string policy in new[] { "dark-gift/0", "" })
        {
            json["DarkGiftPolicyVersion"] = policy;
            int started = 0, actions = 0; bool rejected = false;
            try { Simulation.Run(json.Deserialize<GameRecord>(), replay: true, started: _ => started++, processed: _ => actions++); }
            catch (InvariantException e) { rejected = e.Invariant == "replay.dark-gift"; }
            Check(rejected && started == 0 && actions == 0, "mismatched schema-3 gift policy rejected before replay starts");
        }
        json = JsonSerializer.SerializeToNode(record);
        Check(json["DarkGiftPolicyVersion"]?.GetValue<string>() == "dark-gift/1", "new recording pins gift policy");
        var versioned = Simulation.Create(json.Deserialize<GameRecord>(), replay: true);
        Check(Context(versioned).GiveDarkGift(Minion(versioned).Id), "versioned schema-3 replay permits gift grant");
        json.AsObject().Remove("DarkGiftPolicyVersion");
        var unversioned = json.Deserialize<GameRecord>();
        var replayed = Simulation.Run(unversioned, replay: true);
        Check(Canonical.State(played) == Canonical.State(replayed) && Canonical.History(played) == Canonical.History(replayed),
            "schema-3 replay without gift metadata preserves gift-free canonical bytes");
        var game = Simulation.Create(unversioned, replay: true);
        var offered = Minion(game, zone: Zone.DECK);
        foreach (var candidate in new[] { game, game.CloneExact(history: true), game.CloneForSimulation(91), Game.LoadSnapshot(game.SaveSnapshot()) })
        {
            var ctx = Context(candidate);
            foreach (Action grant in new Action[] {
                () => ctx.GiveDarkGift(offered.Id),
                () => ctx.DiscoverWithDarkGift(new[] { offered.Card }),
                () => ctx.DiscoverEntitiesWithDarkGift(new[] { candidate.IdEntityDic[offered.Id] }) })
            {
                var before = candidate.SaveSnapshot(); bool rejected = false;
                try { grant(); } catch (InvalidOperationException e) { rejected = e.Message.Contains("recorded Dark Gift policy"); }
                Check(rejected && before.SequenceEqual(candidate.SaveSnapshot()), "unversioned schema-3 replay rejects gift before mutation across clone/load");
            }
        }
        Console.WriteLine("PASS dark gift ReplayPolicyMetadata: new policy pin; schema-3 mismatches rejected; absent policy accepts gift-free replay, blocks grants before mutation across clone/load");
    }

    private static void OpenAndChoose(bool suspended = false, bool deck = false)
    {
        var g = New(); var source = Source(g); var ctx = Context(g, source);
        int grants = 0;
        g.TriggerManager.Events.Subscribe<DarkGiftGivenEvent>(_ => {
            grants++; var data = g.CurrentEventData;
            Check(data.EventTarget[GameTag.HAS_DARK_GIFT] == 1 && DarkGifts.Has(data.EventTarget), "event after attachment");
        });
        var rng = g.Random.CaptureState(); int entities = g.IdEntityDic.Count;
        IPlayable[] existing = null; int[] deckBefore = g.Player1.DeckZone.Select(p => p.Id).ToArray();
        if (suspended) Check(Recipe.Process(g, g.Player1, source, null) == TaskState.SUSPENDED, "suspend gifted recipe");
        else if (deck)
        {
            existing = new[] { Minion(g, "CS2_182", Zone.DECK), Minion(g, "CS2_231", Zone.DECK), Minion(g, "EX1_028", Zone.DECK) };
            deckBefore = g.Player1.DeckZone.Select(p => p.Id).ToArray(); entities = g.IdEntityDic.Count;
            Check(ctx.DiscoverEntitiesWithDarkGift(existing), "deck offers open");
            Check(deckBefore.SequenceEqual(g.Player1.DeckZone.Select(p => p.Id)), "offers never leave deck");
        }
        else Check(ctx.DiscoverWithDarkGift(Filter, afterChoose: new ArmorTask(2)), "pool offers open");
        var choice = g.Player1.Choice; var offers = choice.Choices.Select(id => g.IdEntityDic[id]).ToArray();
        Check(offers.Length == 3 && (existing == null || offers.SequenceEqual(existing)), "offer identities/order");
        Check(g.Random.DrawCount == rng.DrawCount + 3, "exactly one gift RNG draw per offer (small pool no card sampling)");
        Check(g.IdEntityDic.Count == entities + (deck ? 4 : 7), "one executor + offers + spells");
        var executor = g.IdEntityDic[choice.SourceId];
        Check(executor.Card.Id == "EDR_102t" && executor.Zone == g.Player1.SetasideZone &&
            executor.Controller == g.Player1 && executor[GameTag.CREATOR] == source.Id &&
            g.GetPendingDecisions().Single().SourceId == executor.Id, "executor choice source/creator");
        Check(offers.Select(p => p[GameTag.DARK_GIFT_ENTITY]).Distinct().Count() == 3 && grants == 0, "preassigned distinct links, no grants");
        foreach (var offer in offers)
        {
            var spell = g.IdEntityDic[offer[GameTag.DARK_GIFT_ENTITY]];
            Check(spell.Card.Type == CardType.SPELL && spell.Zone == g.Player1.SetasideZone && spell[GameTag.CREATOR] == executor.Id &&
                spell.Controller == g.Player1 && !DarkGifts.Has(offer) && offer[GameTag.HAS_DARK_GIFT] == 0 && offer[GameTag.REVEALED] == 0,
                "spell creator chain; no premature attachment/reveal");
        }
        // External diagnostic handlers deliberately do not survive clone/snapshot.
        g.TriggerManager.Events.Unsubscribe<DarkGiftGivenEvent>(g.TriggerManager.Events.GetSubscriptions("DarkGiftGiven").Single().Handler);
        byte[] bytes = g.SaveSnapshot(); var loaded = PinnedPoolFixture.LoadSnapshot(bytes); var clone = g.CloneExact(history: true);
        Check(bytes.SequenceEqual(loaded.SaveSnapshot()), "open gifted choice snapshot-v2 byte round trip");
        Check(Pairings(g) == Pairings(clone) && Pairings(g) == Pairings(loaded), "pairings survive open clone/load");
        var answer = Answer(g, 1); int selected = answer.SelectedOptionIds.Single(); int historyCount = g.PowerHistory.Full.Count;
        var invalidBefore = g.SaveSnapshot();
        Check(!g.TryAnswer(new DecisionAnswer(answer.DecisionId, answer.ControllerId, new[] { int.MaxValue }), out _) &&
            invalidBefore.SequenceEqual(g.SaveSnapshot()), "invalid gifted choice leaves bytes/RNG unchanged");
        var afterAssignment = g.Random.CaptureState();
        int selectedSpell = g.IdEntityDic[selected][GameTag.DARK_GIFT_ENTITY];
        foreach (var game in new[] { g, clone, loaded })
        {
            int events = 0, draws = 0;
            TriggerManager.TriggerHandler handler = _ => events++;
            TriggerManager.TriggerHandler drawHandler = _ => draws++;
            game.TriggerManager.Events.Subscribe<DarkGiftGivenEvent>(handler);
            game.TriggerManager.Events.Subscribe<DrawEvent>(drawHandler);
            Accept(game, answer);
            Check(events == 1 && draws == (deck ? 1 : 0), "chosen grant and actual-deck draw events exactly once");
            game.TriggerManager.Events.Unsubscribe<DarkGiftGivenEvent>(handler);
            game.TriggerManager.Events.Unsubscribe<DrawEvent>(drawHandler);
            var chosen = game.IdEntityDic[selected]; var e = DarkGifts.GiftsOf(chosen).Single();
            Check(chosen.Zone == game.Player1.HandZone && chosen[GameTag.HAS_DARK_GIFT] == 1 && e.Creator.Id == selectedSpell &&
                e[GameTag.CREATOR] == selectedSpell && e[GameTag.TAG_SCRIPT_DATA_NUM_6] == game.IdEntityDic[selectedSpell].Card.AssetId,
                "chosen attachment before hand/continuation");
            Check(offers.Where(o => o.Id != selected).All(o => !DarkGifts.Has(game.IdEntityDic[o.Id]) &&
                game.IdEntityDic[o.Id][GameTag.DARK_GIFT_ENTITY] == o[GameTag.DARK_GIFT_ENTITY]), "unchosen links unchanged, no attachments");
            Check(game.Random.CaptureState().Equals(afterAssignment), "choice never rerolls gift");
            if (deck) Check(game.Player1.LastCardDrawn == selected &&
                game.Player1.DeckZone.Select(p => p.Id).SequenceEqual(deckBefore.Where(id => id != selected)), "actual deck card drawn, others untouched");
            Check(game.Player1.Hero.Armor == (suspended ? 1 : deck ? 0 : 2), "continuation exactly once");
            var saved = game.SaveSnapshot();
            Check(!game.TryAnswer(answer, out _) && saved.SequenceEqual(game.SaveSnapshot()), "stale answer no-op");
        }
        Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded), "answered clone/load identical state");
        Check(Canonical.History(g) == Canonical.History(loaded) &&
            Canonical.Value(g.PowerHistory.Full.Skip(historyCount)) == Canonical.Value(clone.PowerHistory.Full), "answered clone/load identical history");
    }

    private static void SamplingAndEvents()
    {
        var vectors = new List<string>();
        foreach (int seed in new[] { 1, 1, 2, 3 })
        {
            var g = New(seed); var ctx = Context(g, Source(g));
            var reference = g.Random.Clone(); var expected = new List<string>(); var used = new HashSet<string>();
            foreach (var card in OfflineCardPools.Candidates(g, g.Player1.HeroClass, Filter, Cards.FromId("TIME_040").AssetId))
            {
                var entity = Entity.FromCard(g.Player1, card);
                var eligible = DarkGifts.Registry.Where(gift => !used.Contains(gift.SpellId) && gift.IsEligible(entity)).ToArray();
                var gift = eligible[reference.Next(eligible.Length)]; expected.Add(gift.SpellId); used.Add(gift.SpellId);
            }
            Check(ctx.DiscoverWithDarkGift(Filter), "deterministic pool open");
            Check(Pairings(g) == string.Join(",", expected) && g.Random.CaptureState().Equals(reference.CaptureState()), "uniform eligible-unused exact RNG vector");
            vectors.Add(Pairings(g));
        }
        Check(vectors[0] == vectors[1] && vectors.Distinct().Count() == 3, "same seed same gifts; seeds 1-3 differ");
        {
            var g = New(); var source = Source(g); var ctx = Context(g, source);
            var before = g.SaveSnapshot();
            Check(!ctx.DiscoverWithDarkGift(Array.Empty<Card>()) && !ctx.DiscoverEntitiesWithDarkGift(Array.Empty<IPlayable>()) &&
                before.SequenceEqual(g.SaveSnapshot()), "empty fixed/entity offers create no executor/choice/RNG");
            var all = Enumerable.Range(0, 12).Select(_ => (IPlayable)Minion(g, zone: Zone.SETASIDE)).ToArray();
            foreach (var m in all) { m[GameTag.BATTLECRY] = 1; }
            long draws = g.Random.DrawCount;
            ctx.DiscoverEntitiesWithDarkGift(all);
            Check(g.Random.DrawCount == draws + 10 && all.Count(p => p[GameTag.DARK_GIFT_ENTITY] != 0) == 10,
                "exhausted gift registry: no eligible-unused gives no gift/no draw");
            Accept(g, Answer(g, 11));
            Check(!DarkGifts.Has(all[11]) && all[11].Zone == g.Player1.HandZone && g.Random.DrawCount == draws + 10,
                "ungifted exhaustion fallback can be chosen without reroll");
        }
        {
            var g = New(); var source = Source(g); var ctx = Context(g, source); var m = Minion(g);
            var reference = g.Random.Clone();
            var eligible = DarkGifts.Registry.Where(gift => gift.IsEligible(m)).ToArray();
            var expected = eligible[reference.Next(eligible.Length)]; int count = 0;
            TriggerManager.TriggerHandler handler = _ => {
                var data = g.CurrentEventData;
                Check(data.EventTarget == m && data.EventSource.Card.Id == expected.SpellId && data.EventNumber == expected.SpellDbfId, "gift event legacy payload");
                count++;
            };
            DarkGiftGivenEvent observed = default;
            g.TriggerManager.Events.Trace = (e, _, _) => { if (e is DarkGiftGivenEvent gift) observed = gift; };
            g.TriggerManager.Events.Subscribe<DarkGiftGivenEvent>(handler);
            Check(ctx.GiveDarkGift(m.Id), "single grant");
            Check(count == 1 && observed.ControllerId == g.Player1.Id && observed.TargetEntityId == m.Id && observed.GiftId == expected.SpellId &&
                g.Random.CaptureState().Equals(reference.CaptureState()), "event payload and single uniform draw");
            Generic.Copy(g.Player1, source, m, Zone.HAND);
            g.CloneExact();
            Check(count == 1, "copy/clone emit no grant");
            g.TriggerManager.Events.Unsubscribe<DarkGiftGivenEvent>(handler);
            var loaded = PinnedPoolFixture.LoadSnapshot(g.SaveSnapshot());
            Check(DarkGifts.GiftsOf(loaded.IdEntityDic[m.Id]).Single().Card.Id == expected.EnchantmentId, "attached gift restored without grant");
        }
        {
            var g = New(); var source = Source(g); var ctx = Context(g, source);
            var opponent = Entity.FromCard(g.Player2, Cards.FromId("CS2_182"), zone: g.Player2.DeckZone);
            int[] original = g.Player2.DeckZone.Select(p => p.Id).ToArray();
            ctx.DiscoverWithDarkGift(new[] { opponent.Card });
            int chosen = g.Player1.Choice.Choices.Single(); Accept(g, Answer(g));
            Check(chosen != opponent.Id && DarkGifts.Has(g.IdEntityDic[chosen]) && !DarkGifts.Has(opponent) &&
                original.SequenceEqual(g.Player2.DeckZone.Select(p => p.Id)), "Card[] creates copy, opponent deck untouched");
        }
        {
            var g = New(); var ctx = Context(g, Source(g));
            while (!g.Player1.HandZone.IsFull) Minion(g);
            ctx.DiscoverWithDarkGift(new[] { Cards.FromId("CS2_182") });
            int chosen = g.Player1.Choice.Choices.Single(); bool attachedBeforeBurn = false;
            TriggerManager.TriggerHandler handler = _ => attachedBeforeBurn = DarkGifts.Has(g.IdEntityDic[chosen]) &&
                g.IdEntityDic[chosen].Zone == g.Player1.SetasideZone;
            g.TriggerManager.Events.Subscribe<DarkGiftGivenEvent>(handler);
            Accept(g, Answer(g));
            Check(attachedBeforeBurn && g.IdEntityDic[chosen].Zone == g.Player1.GraveyardZone && !DarkGifts.Has(g.IdEntityDic[chosen]) &&
                g.IdEntityDic[chosen][GameTag.HAS_DARK_GIFT] == 0, "full hand grants first then normal burn removes attachment");
        }
    }

    private static void ValidationAndReplay()
    {
        var g = New(); var ctx = Context(g, Source(g));
        foreach (Action open in new Action[] {
            () => ctx.DiscoverWithDarkGift(OfflinePoolFilter.Discover(minCost: 999)),
            () => new DiscoverWithDarkGiftThenContinueTask(OfflinePoolFilter.Discover(minCost: 999)).Open(g, g.Player1, g.Player1.Hero, null, new TaskStack()) })
        {
            var before = g.SaveSnapshot(); bool rejected = false;
            try { open(); } catch (InvalidOperationException e) { rejected = e.Message.Contains("declared pool"); }
            Check(rejected && before.SequenceEqual(g.SaveSnapshot()), "undeclared recipe rejects with no mutation");
        }
        {
            var game = New(); var source = Minion(game, "EX1_028", Zone.SETASIDE); var context = Context(game, source);
            context.DiscoverWithDarkGift(Filter, afterChoose: new SetGameTagTask(GameTag.TAG_SCRIPT_DATA_NUM_1, 17, EntityType.SOURCE), recipeDbfId: 120782);
            Check(game.Player1.Choice.Choices.Count == 2 && game.Player1.Choice.Choices.All(id => game.IdEntityDic[id].Card.Id != source.Card.Id),
                "recipe override keeps runtime source exclusion");
            Accept(game, Answer(game));
            Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 17, "AfterChoose receives real source instead of executor");
            long count = game.Random.DrawCount;
            Check(!context.GiveDarkGift(game.Player1.Hero.Id) && count == game.Random.DrawCount, "no eligible single-grant target: no RNG");
            source[GameTag.HAS_DARK_GIFT] = 1;
            Check(!DarkGifts.Has(source), "flag alone never establishes gift identity");
        }
        OfflinePoolTestRecipes.WithDeclarations(() => {
            var game = New(); var source = game.Player1.Hero;
            var stack = new TaskStack { Number = 999, Playables = new List<IPlayable> { source } };
            var before = game.SaveSnapshot();
            var task = new DiscoverWithDarkGiftThenContinueTask(OfflinePoolTestRecipes.Past, PoolCostBinding.StackNumber);
            Check(task.Open(game, game.Player1, source, null, stack) == TaskState.COMPLETE && stack.Playables.Count == 0 &&
                before.SequenceEqual(game.SaveSnapshot()), "bound empty pool clears stale stack; no executor/choice/RNG");
            stack.Number = 5;
            Check(task.Open(game, game.Player1, source, null, stack) == TaskState.SUSPENDED &&
                game.Player1.Choice.Choices.All(id => game.IdEntityDic[id].Card.Cost == 5), "binding resolves current stack number");
        });
        string Replay()
        {
            var game = New(27); var source = Source(game); var context = Context(game, source);
            for (int stage = 0; stage < 3; stage++)
            {
                context.DiscoverWithDarkGift(Filter); Accept(game, Answer(game, stage));
                context.GiveDarkGift(game.Player1.HandZone.Last().Id);
            }
            return Canonical.Sha(Canonical.History(game));
        }
        Check(Replay() == Replay(), "recorded choices and grants replay same history hash");
    }

    internal static void Run()
    {
        SilenceClearsBaselines(); ClonePreservesBackwardMove(); PendingBattlecrySnapshot();
        ReplayPolicyMetadata();
        IndependentKeywords(); CopyCurrentKeywords(); OverlappingOffers(); PolicyCompatibility();
        Registry(); Eligibility(); EffectsAndRemoval(); SamplingAndEvents();
        OpenAndChoose(); OpenAndChoose(suspended: true); OpenAndChoose(deck: true); ValidationAndReplay();
        Console.WriteLine("PASS dark gift: pinned ten-gift registry; current-state eligibility; distinct deterministic exact RNG; executor/spell links; chosen-only grants; HAND/burn/STACK/deck/Card[]; effects/cost floor; Has/history parity; gift event; copy/clone; backwards/silence/transform; open clone/snapshot-v2; replay history hash");
    }
}
