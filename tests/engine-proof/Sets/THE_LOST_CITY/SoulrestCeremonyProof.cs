using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static PoolPilotProof;

internal static class SoulrestCeremonyProof
{
    // STILL-UNPINNED per docs/design/rules-research.md, checked 2026-10-20.
    // Build 37.0.0.254477 exports DINO_417/DINO_417e: "at the end of your turn".
    // https://hearthstone.blizzard.com/en-us/cards/118559-soulrest-ceremony/
    // No official control/copy ruling or retail trace found. Literal lean: enchantment
    // controller's turn (caster for existing marks, copying player for new copies).
    // Next evidence: retail replay with control transfer/cross-player copy before turn end.
    private static void Unpinned_ControlChange(long seed)
    {
        var g = DeathKnightC1Proof.NewGame(seed); var p = g.Player1;
        End(g);
        var stolen = Summon(p, "CS2_182"); var returned = Summon(p, "CS2_182");
        Resolve(g, Entity.FromCard(p, Cards.FromId("DINO_417")));
        new ControlTask(EntityType.TARGET).Process(g, p.Opponent, p.Opponent.Hero, stolen);
        new ControlTask(EntityType.TARGET).Process(g, p.Opponent, p.Opponent.Hero, returned);
        new ControlTask(EntityType.TARGET).Process(g, p, p.Hero, returned);
        Core2026Proof.Check(stolen.Controller == p.Opponent && returned.Controller == p
            && new[] { stolen, returned }.All(m => m.AttackDamage == 5 && m.IsRush
                && m.AppliedEnchantments.Count == 1 && m.AppliedEnchantments.Single().Controller == p), "steal/return retains Soulrest mark and caster controller");
        Resume(g, next => {
            End(next);
            Core2026Proof.Check(next.IdEntityDic[stolen.Id].Zone == next.Player2.BoardZone
                && next.IdEntityDic[returned.Id].Zone == next.Player1.BoardZone, "opponent end keeps both marks pending");
            End(next);
            Core2026Proof.Check(next.IdEntityDic[stolen.Id].Zone == next.Player2.GraveyardZone
                && next.IdEntityDic[returned.Id].Zone == next.Player1.GraveyardZone
                && CorpseRules.Available(next.Player1) == 1 && CorpseRules.Available(next.Player2) == 1,
                "caster end kills stolen/returned bearers; current controllers gain Corpses");
        });
    }

    private static void Unpinned_CopiedMarks(long seed)
    {
        var g = DeathKnightC1Proof.NewGame(seed); var p = g.Player1;
        var original = Summon(p, "CS2_182");
        Play(g, p, "DINO_417"); Play(g, p, "DINO_417");
        var specialist = Entity.FromCard(p, Cards.FromId("CAP_804"));
        Resolve(g, specialist, original); Resolve(g, specialist, original);
        var summoned = p.BoardZone.Single(m => m.Id != original.Id);
        var generic = Generic.Copy(p, p.Hero, original, Zone.PLAY);
        var enemyCopy = Generic.Copy(p.Opponent, p.Opponent.Hero, original, Zone.PLAY);
        g.ProcessTasks(); g.DeathProcessingAndAuraUpdate();
        new ControlTask(EntityType.TARGET).Process(g, p.Opponent, p.Opponent.Hero, summoned);
        new ControlTask(EntityType.TARGET).Process(g, p, p.Hero, summoned);
        Core2026Proof.Check(original.IsRush && summoned.IsRush
            && new IPlayable[] { original, summoned, generic, enemyCopy }.All(m =>
            ((Minion)m).AttackDamage == 6
            && m.AppliedEnchantments.Count(e => e.Card.Id == "DINO_417e") == 2),
            "both copy paths preserve stacked Attack without reapplying buffs; copied marks survive steal/return");
        Resume(g, next => {
            End(next);
            Core2026Proof.Check(new[] { original.Id, summoned.Id, generic.Id }.All(id => next.IdEntityDic[id].Zone == next.Player1.GraveyardZone)
                && next.IdEntityDic[enemyCopy.Id].Zone == next.Player2.BoardZone && CorpseRules.Available(next.Player1) == 3,
                "original and friendly copies die once on enchantment controller end; enemy copy waits");
            End(next);
            Core2026Proof.Check(next.IdEntityDic[enemyCopy.Id].Zone == next.Player2.GraveyardZone
                && CorpseRules.Available(next.Player2) == 1, "enemy copy dies on copying player's end");
        });
    }

    internal static void Run() => DeathKnightC1Proof.Case("DINO_417", (g, p, id) => {
        Unpinned_ControlChange(g.Seed);
        Unpinned_CopiedMarks(g.Seed);
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 0 && CorpseRules.Available(p) == 0, "empty board has no delayed global effect");
        var location = LocationProofs.Location(p);
        var rushed = Summon(p, "CS2_182"); var silenced = Summon(p, "CS2_182"); var bounced = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_186");
        Play(g, p, id);
        Core2026Proof.Check(new[] { rushed, silenced, bounced }.All(m => m.AttackDamage == 5 && m.IsRush)
            && enemy.AttackDamage == 7 && !enemy.IsRush && (location.AppliedEnchantments == null || location.AppliedEnchantments.Count == 0), "only existing friendly minions get Attack/Rush");
        Core2026Proof.Check(p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == rushed.Id && t.Target?.Id == enemy.Id)
            && !p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.MINION_ATTACK && t.Source?.Id == rushed.Id && t.Target?.Id == p.Opponent.Hero.Id), "Rush attacks minion immediately, never face");
        new SilenceTask(EntityType.TARGET).Process(g, p, p.Hero, silenced);
        new ReturnHandTask(EntityType.TARGET).Process(g, p, p.Hero, bounced);
        var late = Summon(p, "CS2_182");
        Core2026Proof.Check(!late.IsRush && late.AttackDamage == 4 && !silenced.IsRush && silenced.AttackDamage == 4, "late summons unaffected; silence removes buff");
        Resume(g, End);
        Core2026Proof.Check(rushed.Zone == p.GraveyardZone && silenced.Zone == p.BoardZone && late.Zone == p.BoardZone && bounced.Zone == p.HandZone
            && CorpseRules.Available(p) == 1, "own end kills enchanted only; silence/bounce cancel death; one Corpse");
        var source = Entity.FromCard(p, Cards.FromId(id));
        Resolve(g, source);
        Resume(g, End);
        Core2026Proof.Check(silenced.Zone == p.BoardZone && late.Zone == p.BoardZone, "opponent end does not kill friendly enchantments");
        Play(g, p, id);
        Core2026Proof.Check(late.AttackDamage == 6 && late.IsRush, "repeated cast stacks Attack");
        PlayEntity(g, p, bounced);
        Core2026Proof.Check(!bounced.IsRush && bounced.AttackDamage == 4 && bounced.AppliedEnchantments.Count == 0, "bounce removes death mark before replay");
        Resume(g, End);
        Core2026Proof.Check(CorpseRules.Available(p) == 3 && p.BoardZone.SlotCount == 2 && bounced.Zone == p.BoardZone && enemy.Zone == p.Opponent.BoardZone,
            "stacked marks kill each minion once; location/enemy survive");
        End(g); EmptyHand(p); LocationProofs.Location(p);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        Play(g, p, id); Resume(g, End);
        Core2026Proof.Check(p.BoardZone.Count == 0 && p.BoardZone.SlotCount == 2 && CorpseRules.Available(p) == 8,
            "full board with two locations buffs/kills five minions only");
    });
}
