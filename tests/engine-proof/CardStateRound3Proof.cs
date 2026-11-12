using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;

internal static class CardStateRound3Proof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, message);
    internal static void Run(string only = null)
    {
        if (only == null) CardStateTransformProof.Run();
        foreach (bool history in new[] { false, true })
        {
            if (only == null || only == "temporary") Temporary(history);
            if (only == null || only == "dormant") Dormant(history);
            if (only == null || only == "fairbot") FairBot(history);
        }
    }
    private static void Temporary(bool history)
    {
        foreach (bool separator in new[] { false, true })
        {
            var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
            foreach (var c in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(c));
            IPlayable gap = separator ? Core2026Proof.Add(p, "CS2_182") : null;
            CostRules.AddNextTemporaryRule(p, 2);
            TemporaryRulesProof.Grant(p, "CATA_489");
            var cards = p.HandZone.Where(c => c.Card.Shatter != null).ToArray();
            Check(cards.Length == (separator ? 2 : 1) && cards.All(c => TemporaryRules.IsTemporary(c) && c.Cost == 2
                && c.AppliedEnchantments.Count(e => e.Card.Id == "GBL_999e") == 1),
                "Round3Temporary Unpinned_TemporaryResidence split and immediate join retain mark, unique helper and Spelunker discount");
            if (gap != null) p.SetasideZone.Add(p.HandZone.Remove(gap));
            var combined = p.HandZone.Single();
            Check(combined.Card.Id == "CATA_489" && TemporaryRules.IsTemporary(combined) && combined.Cost == 2
                && combined.AppliedEnchantments.Count(e => e.Card.Id == "GBL_999e") == 1,
                "Round3Temporary recombining marked halves retains one mark and discount");
            var copy = Generic.Copy(p, p.Hero, combined, Zone.HAND);
            foreach (var next in new[] { g, g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                TemporaryRulesProof.End(next);
                Check(new[] { combined.Id, copy.Id }.All(id => next.Player1.DiscardedEntities.Count(x => x == id) == 1),
                    "Round3Temporary Unpinned_TemporaryExpiry combined parent and gameplay copy really discard after clone/restore");
            }
        }
        var boundary = HeroDivineShieldProof.NewGame(history); var owner = boundary.Player1;
        foreach (var c in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(c));
        var expiresFirst = TemporaryRulesProof.Grant(owner);
        Core2026Proof.Add(owner, "CATA_489");
        TemporaryRules.Make(owner.HandZone.Single(c => c.Card.Id == "CATA_489t2"));
        TemporaryRulesProof.End(boundary);
        Check(owner.HandZone.Count == 0 && owner.DiscardedEntities.Count == 2
            && owner.GraveyardZone.Any(c => c.Card.Id == "CATA_489"),
            "Round3Temporary Unpinned_TemporaryEndOrder join during frozen expiry cohort carries marked half expiry ticket");
        boundary = HeroDivineShieldProof.NewGame(history); owner = boundary.Player1;
        var pending = Entity.FromCard(owner, Cards.FromId("CS2_182")); TemporaryRules.Make(pending);
        var grantedCopy = Generic.Copy(owner, owner.Hero, pending, Zone.HAND);
        Check(TemporaryRules.IsTemporary(grantedCopy), "Round3Temporary pre-grant gameplay copy retains mark");
        Console.WriteLine("PASS Round3Temporary: marked split/join/copy, Spelunker, unique helpers, real expiry, clone/restore");
    }
    private static void Dormant(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player1;
        var parent = HeroDivineShieldProof.Summon(g, "TIME_046"); DormantRules.Awaken(parent);
        var faceless = Core2026Proof.Play(g, p, "EX1_564", parent);
        Check(faceless.Card.Id == "TIME_046" && !((Minion)faceless).IsDormant
            && !(faceless.AppliedEnchantments?.Any(e => e.Card.Id == "TIME_046e") ?? false),
            "Round3Dormant Unpinned_DormantLifecycle actual Faceless copying awakened Patriarch stays awake");
        foreach (bool asleep in new[] { false, true })
        foreach (bool replaceSleeper in new[] { false, true })
        {
            g = HeroDivineShieldProof.NewGame(history); p = g.Player1;
            parent = HeroDivineShieldProof.Summon(g, "TIME_046");
            if (asleep) DormantRules.Accelerate(parent, 1); else DormantRules.Awaken(parent);
            var transformed = HeroDivineShieldProof.Summon(g, replaceSleeper ? "TIME_046" : "CS2_182");
            var oldHelper = transformed.AppliedEnchantments?.SingleOrDefault();
            g.CurrentEventData = new EventMetaData(transformed, parent);
            new TransformCopyTask().Process(g, p, transformed, parent, new TaskStack());
            Check(oldHelper == null || oldHelper[GameTag.ATTACHED] == 0,
                "Round3Dormant transform-copy retires previous sleep helper before replacing tags");
            var stack = new TaskStack();
            new SummonCopyTask(EntityType.TARGET, addToStack: true).Process(g, p, p.Hero, parent, stack);
            g.CurrentEventData = null;
            g.DeathProcessingAndAuraUpdate();
            foreach (var copy in new[] { transformed, (Minion)stack.Playables.Single() })
            {
                Check(copy.IsDormant == asleep && DormantRules.TurnsRemaining(copy) == (asleep ? 2 : 0),
                    "Round3Dormant transform/summon copy preserves current awake state or remaining duration");
                if (asleep) Check(copy.AppliedEnchantments.Count(e => e.Card.Id == "TIME_046e") == 1
                    && DormantRules.SleepGeneration(copy) != DormantRules.SleepGeneration(parent)
                    && copy.AppliedEnchantments.Single().Target == copy, "Round3Dormant copied sleeper owns independent helper");
            }
            foreach (var next in new[] { g, g.CloneExact(history), Game.LoadSnapshot(g.SaveSnapshot()) })
            {
                var copied = (Minion)next.IdEntityDic[transformed.Id];
                if (asleep) DormantRules.Awaken(copied);
                Check(!copied.IsDormant && ((Minion)next.IdEntityDic[parent.Id]).IsDormant == asleep,
                    "Round3Dormant copied wake isolated after clone/restore");
            }
        }
        Console.WriteLine("PASS Round3Dormant: actual Faceless; awake/asleep transform and summon copies; independent helpers; clone/restore");
    }
    private static void FairBot(bool history)
    {
        var g = HeroDivineShieldProof.NewGame(history); var p = g.Player2;
        var hidden = Core2026Proof.Add(p, "JAIL_801"); hidden[GameTag.TAG_SCRIPT_DATA_NUM_1] = 1;
        TemporaryRules.Make(hidden);
        Enchantment.GetInstance(p, p.Hero, hidden, Cards.FromId("CFM_305e"));
        var retired = Enchantment.GetInstance(p, p.Hero, hidden, Cards.FromId("CS2_087e")); retired.Remove();
        var visible = HeroDivineShieldProof.Summon(g, "CS2_182");
        var created = Enchantment.GetInstance(g.Player1, hidden, visible, Cards.FromId("CS2_087e"));
        created.CapturedCard = hidden.Card;
        var nested = Enchantment.GetInstance(p, p.Hero, hidden.AppliedEnchantments.Last(), Cards.FromId("CS2_087e"));
        var retiredParent = TemporaryRulesProof.Grant(p, "CATA_489");
        var fair = FairBotState.Create(g, g.Player1.PlayerId, 42, DeckStyle.Control);
        var masked = (Game)typeof(FairBotState).GetField("game", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(fair);
        foreach (var actor in masked.IdEntityDic.Values.OfType<Enchantment>())
        {
            Check(actor.Target.Id != hidden.Id, "Round3FairBot every live/retired hidden attachment removed");
            if (actor.Creator.Id == hidden.Id) Check(actor.Creator is Unknown && actor.Creator[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0,
                "Round3FairBot cached creator cannot expose hidden identity/countdown");
            Check(actor.Target is Controller || ReferenceEquals(actor.Target, masked.IdEntityDic[actor.Target.Id]),
                "Round3FairBot nested attachment cannot retain removed target object");
            Check(actor.CapturedCard?.Id != "JAIL_801", "Round3FairBot captured identity scrubbed with creator reference");
        }
        // Private residence ids and retired attachments disappear during canonicalization.
        var unknown = masked.Player2.HandZone[hidden.ZonePosition - 1];
        Check(!masked.IdEntityDic.Values.Any(e => e.Card.Id == nested.Card.Id && e is Enchantment attachment
                && attachment.Target.Card.Id == hidden.Card.Id), "Round3FairBot nested hidden attachment identity scrubbed");
        Check(!masked.IdEntityDic.Values.Any(e => e.Card.Id == retiredParent.Card.Id),
            "Round3FairBot retired hidden Shatter parent and its Temporary attachment scrubbed");
        Check(unknown is Unknown && unknown.NativeTags.Keys.All(t => !DormantRules.IsPrivateTag(t)
            && !TemporaryRules.IsPrivateTag(t) && !ShatterRules.IsInternalTag(t) && !HandProgressRules.IsInternalTag(t)),
            "Round3FairBot private card-state tags scrubbed");
        Check(hidden.Card.Id == "JAIL_801" && hidden[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && hidden.AppliedEnchantments.Count == 2,
            "Round3FairBot authoritative game unchanged");
        masked.CloneExact();
        Console.WriteLine("PASS Round3FairBot: all hidden attachments/retired actors/cached creators scrubbed; private tags; clone isolation");
    }
}
