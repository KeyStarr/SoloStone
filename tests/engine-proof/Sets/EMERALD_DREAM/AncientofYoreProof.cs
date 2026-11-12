using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class AncientofYoreProof
{
    // Unpinned_DormantWhile Unpinned_DormantClock Unpinned_DormantExclusions
    // Unpinned_DormantLifecycle Unpinned_DormantCounterProjection Unpinned_DormantReplace.
    internal static void Run() => StandardSetsProof.Case("EDR_979", (g, p, id) => {
        EmeraldDreamCardStateProof.Token("EDR_979e", 117303); EmeraldDreamCardStateProof.Token("EDR_979e2", 125970);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsDormant && source.AttackDamage == 5 && source.BaseHealth == 5 && DormantRules.TurnsRemaining(source) == 2
            && source.AppliedEnchantments.Select(e => e.Card.Id).ToHashSet().SetEquals(new[] { "EDR_979e", "EDR_979e2" }), "exact sleeping Yore and two helpers");
        source.Silence(); source.TakeDamage(p.Opponent.Hero, 99);
        Core2026Proof.Check(!source.IsSilenced && source.Damage == 0, "Dormant Silence/damage excluded");
        int deck = p.DeckZone.Count;
        PoolPilotProof.Resume(g, next => {
            var owner = next.Player1; var m = (Minion)next.IdEntityDic[source.Id];
            End(next);
            Core2026Proof.Check(owner.Hero.Armor == 3 && owner.DeckZone.Count == deck - 1 && m.IsDormant,
                "Unpinned_DormantWhile summon-turn own end gains three Armor then draws one");
            End(next);
            Core2026Proof.Check(owner.Hero.Armor == 3 && owner.DeckZone.Count == deck - 2 && DormantRules.TurnsRemaining(m) == 1, "opponent end excluded, own start draws normally");
            End(next);
            Core2026Proof.Check(owner.Hero.Armor == 6 && owner.DeckZone.Count == deck - 3, "second sleeping end grants exactly once");
            End(next);
            Core2026Proof.Check(!m.IsDormant && m.IsExhausted && m.AppliedEnchantments.Count == 0 && owner.DeckZone.Count == deck - 4, "second start wakes and removes both helpers");
            End(next);
            Core2026Proof.Check(owner.Hero.Armor == 6 && owner.DeckZone.Count == deck - 4, "awake end no longer grants Armor or card");
            End(next);
        });
        source.Silence(); g.DeathProcessingAndAuraUpdate();
        DormantRules.Make(source, 1, "EDR_979e", p.Hero); deck = p.DeckZone.Count;
        End(g);
        Core2026Proof.Check(p.Hero.Armor == 6 && p.DeckZone.Count == deck && source.AppliedEnchantments.Single().Card.Id == "EDR_979e", "awake Silence prevents asleep-listener restoration");
        End(g); ParkBoard(p); PoolPilotProof.EmptyHand(p);
        source = Summon(p, id); Generic.ReturnToHandBlock(p, source); deck = p.DeckZone.Count;
        End(g);
        Core2026Proof.Check(p.Hero.Armor == 6 && p.DeckZone.Count == deck && source.AppliedEnchantments.Count == 0, "source leaves removes asleep listener");
        End(g); PoolPilotProof.EmptyHand(p);
        source = Summon(p, id);
        for (int i = 0; i < 10; i++) Add(p, "CS2_182"); deck = p.DeckZone.Count;
        End(g);
        Core2026Proof.Check(p.Hero.Armor == 9 && p.HandZone.Count == 10 && p.DeckZone.Count == deck - 1, "full hand burns card but grants Armor");
        End(g); ParkBoard(p); PoolPilotProof.EmptyHand(p);
        source = Summon(p, id); EmeraldDreamBatch3Proof.EmptyDeck(p);
        p.Hero.Armor = 0; p.Hero.Damage = 0; p.Hero.Fatigue = 0;
        End(g);
        Core2026Proof.Check(p.Hero.Armor == 2 && p.Hero.Damage == 0 && p.Hero.Fatigue == 1, "Armor before draw absorbs one fatigue damage");
        End(g); ParkBoard(p); PoolPilotProof.EmptyHand(p);
        foreach (string filler in new[] { "CS2_182", "CS2_182", "CS2_182" }) Entity.FromCard(p, Cards.FromId(filler), zone: p.DeckZone);
        source = Summon(p, id); Summon(p, "BE_600"); deck = p.DeckZone.Count;
        End(g);
        Core2026Proof.Check(p.DeckZone.Count == deck - 2 && p.HandZone.Count == 2,
            "asleep Armor is a real armor gain: Animator of Iron's on-Armor draw fires alongside Yore's draw");
    });
}
