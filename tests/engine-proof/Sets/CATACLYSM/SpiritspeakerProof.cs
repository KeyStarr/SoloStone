using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;
using static CataclysmPoolProof;

internal static class SpiritspeakerProof
{
    private static void Offers(Game game, Controller player)
    {
        Core2026Proof.Check(player.Choice.ChoiceType == ChoiceType.GENERAL
            && player.Choice.Choices.Select(i => game.IdEntityDic[i].Card.Id)
                .SequenceEqual(new[] { "NEW1_032", "NEW1_033", "NEW1_034" })
            && player.Options().Count == 3 && player.Options().All(t => t is ChooseTask),
            "exact distinct Animal Companion choices and bot answers");
    }

    internal static void Run() => StandardSetsProof.Case("MEND_301", (g, p, id) => {
        // Tier D: pinned 37.0.0.254477 export identifies the three named Animal Companions;
        // CoreCardsGen registers these exact tokens, including Leokk's aura and Huffer's Charge.
        var enemy = Summon(p.Opponent, "CS2_182");
        for (int branch = 0; branch < 3; branch++)
        {
            PutBoardAside(p);
            var location = LocationProofs.Location(p, position: 0);
            var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
            var source = (Minion)Add(p, id);
            p.UsedMana = 0;
            Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source, zonePosition: 2)), "middle Spiritspeaker play");
            Core2026Proof.Check(source.AttackDamage == 2 && source.Health == 2 && source.HasBattleCry,
                "printed 2/2 Battlecry source");
            Offers(g, p);
            int chosen = p.Choice.Choices[branch];
            var rng = g.Random.CaptureState();
            var clone = g.CloneExact(history: true); var loaded = Game.LoadSnapshot(g.SaveSnapshot());
            foreach (var game in new[] { g, clone, loaded }) PlayerDecisionProof.Answer(game, chosen);
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone) && Canonical.State(g) == Canonical.State(loaded),
                "fixed choice clone/snapshot future parity");
            var token = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == source.Id);
            Core2026Proof.Check(token.Id != chosen && token.Id != source.Id && g.IdEntityDic[token.Id] == token
                && token.Controller == p && token.Zone == p.BoardZone && token.ZonePosition == 3
                && token.Card.IsRace(Race.BEAST) && token.Card.Cost == 3
                && p.BoardZone.GetSlot(0) == location && p.BoardZone.GetSlot(1) == left
                && p.BoardZone.GetSlot(2) == source && p.BoardZone.GetSlot(4) == right
                && p.Choice == null && g.Random.CaptureState().Equals(rng),
                "fresh companion beside actual source physical slot; fixed choice consumes no RNG");
            Core2026Proof.Check(token.Card.Id == new[] { "NEW1_032", "NEW1_033", "NEW1_034" }[branch]
                && token.Card.AssetId == new[] { 959, 226, 100 }[branch]
                && token.AttackDamage == (branch == 1 ? 2 : 4) && token.Health == (branch == 2 ? 2 : 4)
                && token.HasTaunt == (branch == 0) && token.HasCharge == (branch == 2), "selected exact companion stats/keywords");
            if (branch == 2)
            {
                Core2026Proof.Check(token.CanAttack && !token.IsExhausted, "Huffer has immediate Charge readiness");
                token.Silence();
                Core2026Proof.Check(!token.HasCharge && token.IsExhausted && !token.CanAttack, "Silence removes fresh Huffer readiness");
            }
            if (branch == 1)
            {
                Core2026Proof.Check(source.AttackDamage == 3 && left.AttackDamage == 5 && enemy.AttackDamage == 4,
                    "Leokk buffs only other friendly minions");
                var copy = g.CloneExact(history: true);
                ((Minion)copy.IdEntityDic[token.Id]).Silence(); copy.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(((Minion)copy.IdEntityDic[left.Id]).AttackDamage == 4 && left.AttackDamage == 5,
                    "Leokk Silence removes aura independently in clone");
                p.SetasideZone.Add(p.BoardZone.Remove(token)); g.DeathProcessingAndAuraUpdate();
                Core2026Proof.Check(source.AttackDamage == 2 && left.AttackDamage == 4, "Leokk departure removes aura");
            }
        }
        PutBoardAside(p);
        LocationProofs.Location(p, position: 0);
        while (p.BoardZone.SlotCount < 6) Summon(p, "CS2_182");
        var fullSource = (Minion)Play(g, p, id); Offers(g, p);
        PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
        Core2026Proof.Check(p.BoardZone.IsFull && p.BoardZone.SlotCount == 7
            && p.BoardZone.All(m => m[GameTag.DISPLAYED_CREATOR] != fullSource.Id), "full physical board blocks companion");

        PutBoardAside(p);
        var removed = (Minion)Play(g, p, id); Offers(g, p);
        Kill(g, removed); var last = Summon(p, "CS2_182");
        PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
        var appended = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == removed.Id);
        Core2026Proof.Check(appended.ZonePosition == p.BoardZone.SlotCount - 1
            && appended.ZonePosition == last.ZonePosition + 1, "committed Battlecry appends after source departure");

        PutBoardAside(p);
        Summon(p, "LOE_077"); var repeated = (Minion)Play(g, p, id);
        Offers(g, p); PlayerDecisionProof.Answer(g, p.Choice.Choices[0]);
        Offers(g, p); PlayerDecisionProof.Answer(g, p.Choice.Choices[2]);
        var companions = p.BoardZone.Where(m => m[GameTag.DISPLAYED_CREATOR] == repeated.Id).ToArray();
        Core2026Proof.Check(p.Choice == null && companions.Length == 2
            && companions[0].Card.Id == "NEW1_034" && companions[1].Card.Id == "NEW1_032",
            "Brann makes separate choices, each summon immediately right of source");
    });
}
