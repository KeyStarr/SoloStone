using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class DreadLeviathanProof
{
    // stats-health/dread-short-health.yaml STILL-UNPINNED, owner assumption: steal min(3,current Health).
    // A official bug-fix notes establish changed short-Health handling; exact formula remains assumed.
    internal static void Run() => StandardSetsProof.Case("CATA_699", Unpinned_DreadLeviathan);
    private static void Unpinned_DreadLeviathan(Game g, Controller p, string id)
    {
        var empty = (Minion)Play(g, p, id);
        Core2026Proof.Check(empty.HasTaunt && empty.AttackDamage == 8 && empty.Health == 8 && p.Choice == null,
            "empty enemy board completes three choices without Health gain; printed 8/8 Taunt");
        ParkBoard(p);
        var friendly = Summon(p, "CS2_182");
        var sleeper = Summon(p.Opponent, "EDR_840t2");
        var hidden = Summon(p.Opponent, "CS2_182"); hidden.HasStealth = true;
        var victim = Summon(p.Opponent, "EX1_561"); victim.Damage = 7;
        var survivor = Summon(p.Opponent, "EX1_572"); survivor.Damage = 1;
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.Choice.Choices.SequenceEqual(new[] { victim.Id, survivor.Id }) && source.Health == 8,
            "enemy minion selector excludes friendly/hero/Dormant/Stealth without early effect");
        Resume(g, next => {
            PlayerDecisionProof.Answer(next, victim.Id);
            var own = next.Player1;
            Core2026Proof.Check(((Minion)next.IdEntityDic[victim.Id]).Zone.Type == Zone.GRAVEYARD
                && ((Minion)next.IdEntityDic[source.Id]).Health == 9
                && own.Choice.Choices.SequenceEqual(new[] { survivor.Id }),
                $"Unpinned_DreadLeviathan: short victim zone={next.IdEntityDic[victim.Id].Zone.Type}, sourceHealth={((Minion)next.IdEntityDic[source.Id]).Health}, choices={string.Join(",", own.Choice?.Choices ?? new List<int>())}, expected={survivor.Id}");
            PlayerDecisionProof.Answer(next, survivor.Id);
            PlayerDecisionProof.Answer(next, survivor.Id);
            Core2026Proof.Check(own.Choice == null && !next.Resolution.HasSuspendedEffect
                && ((Minion)next.IdEntityDic[source.Id]).Health == 15
                && ((Minion)next.IdEntityDic[survivor.Id]).BaseHealth == 6
                && ((Minion)next.IdEntityDic[survivor.Id]).Health == 5,
                "Unpinned_DreadLeviathan: same surviving target may be selected repeatedly; max and current Health each transfer");
        });
        Core2026Proof.Check(friendly.Health == 5 && sleeper.Health == 5 && hidden.Health == 5 && survivor.Damage == 1,
            "steal changes no Attack, damage counter, friendly or ineligible minion");
        Core2026Proof.Check(source.AppliedEnchantments.Where(e => e.Card.Id == "CATA_699e2").Select(e => e.ScriptTag1).SequenceEqual(new[] { 1, 3, 3 })
            && survivor.AppliedEnchantments.Where(e => e.Card.Id == "CATA_699e").Select(e => e.ScriptTag1).SequenceEqual(new[] { 3, 3 }),
            "exported enchantments retain positive per-transfer payloads");
        End(g); End(g);
        Core2026Proof.Check(source.Health == 15 && survivor.Health == 5, "transferred Health persists across turns");
        source.Silence(); survivor.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(source.Health == 8 && survivor.BaseHealth == 12 && survivor.Health == 11, "Silence removes both stat grants");
        ParkBoard(p); ParkBoard(p.Opponent); EmptyHand(p);
        var golem = Summon(p.Opponent, "EX1_556");
        source = (Minion)Play(g, p, id); PlayerDecisionProof.Answer(g, golem.Id);
        var replacement = p.Opponent.BoardZone.Single();
        Core2026Proof.Check(replacement.Card.Id == "skele21" && p.Choice.Choices.SequenceEqual(new[] { replacement.Id }),
            "Deathrattle replacement enters next fresh choice");
        PlayerDecisionProof.Answer(g, replacement.Id);
        Core2026Proof.Check(p.Choice == null && source.Health == 12, "exhausted enemy board skips final empty choice");
        ParkBoard(p); ParkBoard(p.Opponent); EmptyHand(p);
        var target = Summon(p.Opponent, "CS2_182"); target.Health = 30;
        Summon(p, "LOE_077"); source = (Minion)Play(g, p, id);
        for (int i = 0; i < 6; i++) PlayerDecisionProof.Answer(g, target.Id);
        Core2026Proof.Check(p.Choice == null && source.Health == 26 && target.Health == 12, "Brann runs two complete three-choice activations");
    }
}
