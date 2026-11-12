using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class AutomaticCastProof
{
    internal static void Cast(Game g, Spell spell, string path = "direct", ICharacter target = null, int mode = 0)
    {
        var p = spell.Controller;
        switch (path)
        {
            case "context": new EffectContext(g, p.Id, p.Hero.Id).CastSpell(spell.Id, HistoryCastKind.Repeat, target?.Id, mode); break;
            case "task": new PlayTask(PlayType.SPELL, true).Process(g, p, p.Hero, null,
                new TaskStack { Playables = new[] { (IPlayable)spell } }); break;
            case "random": new CastRandomSpellTask(c => c.Id == "EX1_287", phaseShift: false).Process(g, p, p.Hero, spell); break;
            default: Generic.CastSpell(p, g, spell, target, mode); break;
        }
        g.DeathProcessingAndAuraUpdate();
    }

    internal static void Attempt(Game g, Spell spell, HistoryCastKind kind = HistoryCastKind.Effect)
    {
        var facts = g.HistoryFacts.Query.Casts().Where(f => f.EntityId == spell.Id).ToArray();
        Core2026Proof.Check(facts.Length == 2 && facts[0].Outcome == HistoryCastOutcome.Started
            && facts[1].Outcome == HistoryCastOutcome.Completed && facts.All(f => f.CasterKind == kind)
            && facts[0].CastId == facts[1].CastId, "attempt records one start/completion, including fizzles");
        if (spell.IsSecret && spell.Zone == spell.Controller.GraveyardZone)
            Core2026Proof.Check(g.PowerHistory.Full.OfType<PowerHistoryShowEntity>().Any(e => e.Entity.Id == spell.Id
                && e.Entity.Name == spell.Card.Id), "failed automatic Secret is publicly revealed");
        Core2026Proof.Check(!g.HistoryFacts.Query.Plays().Any(f => f.EntityId == spell.Id), "effect cast is never a card play");
    }

    internal static void Run()
    {
        foreach (string path in new[] { "direct", "context", "task", "random" })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            var first = (Spell)Entity.FromCard(p, Cards.FromId("EX1_287")); Cast(g, first);
            PoolPilotProof.Resume(g, next => {
                var owner = next.Player1;
                var duplicate = (Spell)Entity.FromCard(owner, Cards.FromId("EX1_287"));
                int plays = owner.NumSpellsPlayedThisGame;
                Cast(next, duplicate, path);
                Core2026Proof.Check(owner.SecretZone.Count == 1 && owner.SecretZone.Single().Id == first.Id
                    && duplicate.Zone == owner.GraveyardZone && duplicate.ActivatedTrigger == null,
                    "Unpinned_DuplicateSecret: preserve active identity, fizzle without arming duplicate " + path);
                Attempt(next, duplicate, path == "context" ? HistoryCastKind.Repeat : HistoryCastKind.Effect);
                Core2026Proof.Check(owner.NumSpellsPlayedThisGame == plays, "automatic attempts give no hand-play credit");
            });
        }
        var reused = HistoryFactsProof.Fixture();
        var active = (Spell)Entity.FromCard(reused.Player1, Cards.FromId("EX1_287"));
        Cast(reused, active);
        PoolPilotProof.Resume(reused, next => {
            var original = (Spell)next.IdEntityDic[active.Id];
            Cast(next, original, "task");
            Core2026Proof.Check(next.Player1.SecretZone.SingleOrDefault()?.Id == active.Id && original.ActivatedTrigger != null
                && original.Zone == next.Player1.SecretZone && !next.Player1.GraveyardZone.Contains(original),
                "Unpinned_ReusedSecretEntity: automatic repeat preserves the active original");
            Core2026Proof.Check(next.HistoryFacts.Query.Casts().Count(f => f.EntityId == active.Id) == 4,
                "reused active entity records a second complete attempt");
        });
        FullZone();
        RequiredTarget();
        Modes();
        Console.WriteLine("PASS automatic casting: Unpinned_SecretAdmission/TargetFizzle/ModeBeforeTarget, attempted history, clone/snapshot");
    }

    private static void FullZone()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        foreach (string id in new[] { "EX1_287", "EX1_289", "EX1_294", "EX1_295", "tt_010" })
            Cast(g, (Spell)Entity.FromCard(p, Cards.FromId(id)));
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1; int[] active = owner.SecretZone.Select(s => s.Id).ToArray();
            var sixth = (Spell)Entity.FromCard(owner, Cards.FromId("EX1_594")); Cast(next, sixth);
            Core2026Proof.Check(owner.SecretZone.Select(s => s.Id).SequenceEqual(active)
                && sixth.Zone == owner.GraveyardZone && sixth.ActivatedTrigger == null, "Unpinned_FullSecretZone fizzles without overwrite");
            Attempt(next, sixth);
            End(next); End(next);
            Core2026Proof.Check(owner.SecretZone.Select(s => s.Id).SequenceEqual(active), "installed Secrets persist across turns");
        });
        int counterspell = p.SecretZone.First().Id;
        Play(g, p, "GAME_005");
        End(g); Play(g, p.Opponent, "GAME_005");
        Core2026Proof.Check(!p.SecretZone.Any(s => s.Id == counterspell), "installed Counterspell reacts only to opponent play, then leaves Secret zone");
    }

    private static void RequiredTarget()
    {
        foreach (string path in new[] { "direct", "context", "task", "invalid" })
        {
            var g = HistoryFactsProof.Fixture(); var p = g.Player1;
            var spell = (Spell)Entity.FromCard(p, Cards.FromId("CS2_004"));
            p.DeckZone.Add(spell); int count = p.DeckZone.Count;
            PoolPilotProof.Resume(g, next => {
                var owner = next.Player1; var automatic = (Spell)next.IdEntityDic[spell.Id];
                int hand = owner.HandZone.Count; var random = next.Random.CaptureState();
                Cast(next, automatic, path, path == "invalid" ? owner.Hero : null);
                Core2026Proof.Check(automatic.Zone == owner.GraveyardZone && owner.DeckZone.Count == count - 1
                    && owner.HandZone.Count == hand, "Unpinned_RequiredTargetFizzle: consume deck entity, never execute draw tail " + path);
                Attempt(next, automatic, path == "context" ? HistoryCastKind.Repeat : HistoryCastKind.Effect);
                Core2026Proof.Check(next.Random.CaptureState().Equals(random), "no legal target consumes no RNG");
            });
        }
    }

    private static void Modes()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1;
            var spell = (Spell)Entity.FromCard(owner, Cards.FromId("EX1_154"));
            Cast(next, spell, mode: 1);
            Core2026Proof.Check(spell.Zone == owner.GraveyardZone && owner.HandZone.Count == 0,
                "Unpinned_ModeTargetFizzle: selected Wrath branch cannot execute without target");
            Attempt(next, spell);
        });
    }
}
