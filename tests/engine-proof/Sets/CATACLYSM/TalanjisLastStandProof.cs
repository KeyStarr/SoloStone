using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static PoolPilotProof;

internal static class TalanjisLastStandProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_471", (g, p, id) => {
        var left = Summon(p, "CS2_182"); var bearer = Summon(p, "CORE_EX1_002"); var right = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "CS2_182");
        var location = LocationProofs.Location(p);
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.All(m => m.HasDeathrattle) && !enemy.HasDeathrattle && !location.HasDeathrattle, "grants only current friendly minions Deathrattle");
        Core2026Proof.Check(bearer.AppliedEnchantments.Single(e => e.Card.Id == "CATA_471e").Card.AssetId == 122880, "exact granted enchantment");
        var later = Summon(p, "CS2_182");
        Core2026Proof.Check(!later.HasDeathrattle, "later summons never inherit grant");
        End(g); End(g);
        Resume(g, next => Kill(next, (Minion)next.IdEntityDic[bearer.Id]));
        var generated = p.BoardZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == bearer.Id).ToArray();
        CataclysmPoolProof.Generated(g, id, bearer, generated, 1, Zone.PLAY);
        Core2026Proof.Check(generated[0].Card.Cost == 4 && p.BoardZone.GetSlot(1) == generated[0] && p.BoardZone.GetSlot(2) == right
            && p.BoardZone.GetSlot(3) == location, "departed bearer replaces its middle physical slot");
        left.Silence(); Kill(g, left);
        Core2026Proof.Check(!p.BoardZone.Any(c => c[GameTag.DISPLAYED_CREATOR] == left.Id), "Silence removes granted Deathrattle");
        p.SetasideZone.Add(p.BoardZone.Remove(right)); End(g); End(g);
        Core2026Proof.Check(!p.BoardZone.Any(c => c[GameTag.DISPLAYED_CREATOR] == right.Id), "grant never triggers on non-death exit");
        Kill(g, enemy);
        Core2026Proof.Check(p.Opponent.BoardZone.IsEmpty, "enemy minion death receives no grant");
        CataclysmPoolProof.PutBoardAside(p);
        bearer = Summon(p, "CS2_182"); Play(g, p, id);
        CataclysmPoolProof.WithPool(g, id, 0, () => {
            var rng = g.Random.CaptureState(); Kill(g, bearer);
            Core2026Proof.Check(p.BoardZone.IsEmpty && rng.Equals(g.Random.CaptureState()), "empty pool grant consumes no RNG and source dies");
        });
        CataclysmPoolProof.WithPool(g, id, 1, () => {
            bearer = Summon(p, "CS2_182"); Play(g, p, id); Play(g, p, id); Kill(g, bearer);
            CataclysmPoolProof.Generated(g, id, bearer, p.BoardZone, 2, Zone.PLAY);
            Core2026Proof.Check(p.BoardZone.Select(c => c.Card.Id).Distinct().Count() == 1, "multiple grants stack independent duplicate summons");
        });
        CataclysmPoolProof.PutBoardAside(p);
        bearer = Summon(p, "CS2_182"); Play(g, p, id);
        LocationProofs.Location(p);
        while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var deathrattle = bearer.AppliedEnchantments.Single(e => e.Card.Id == "CATA_471e").Power.DeathrattleTask;
        var before = g.Random.DrawCount; Kill(g, bearer);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && g.Random.DrawCount - before == 1, "death on full physical board fills only freed slot");
        Core2026Proof.Check(p.BoardZone.GetSlot(0)[GameTag.DISPLAYED_CREATOR] == bearer.Id, "full-board Deathrattle replaces bearer before location");
        deathrattle.Process(g, p, bearer, null, new TaskStack());
        Core2026Proof.Check(p.BoardZone.SlotCount == 7, "full board including location rejects extra summon");
        CataclysmPoolProof.PutBoardAside(p);
        bearer = Summon(p, "CS2_182"); location = LocationProofs.Location(p); right = Summon(p, "CS2_182");
        Play(g, p, id);
        bearer.AppliedEnchantments.Single(e => e.Card.Id == "CATA_471e").Power.DeathrattleTask.Process(g, p, bearer, null, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
        generated = p.BoardZone.Where(c => c[GameTag.DISPLAYED_CREATOR] == bearer.Id).ToArray();
        CataclysmPoolProof.Generated(g, id, bearer, generated, 1, Zone.PLAY);
        Core2026Proof.Check(p.BoardZone.Slots.Select(c => c.Id).SequenceEqual(new[] { bearer.Id, generated[0].Id, location.Id, right.Id }),
            "living bearer summons immediately right before physical location slot");
        CataclysmPoolProof.PutBoardAside(p);
        location = LocationProofs.Location(p); left = Summon(p, "CS2_182");
        bearer = Summon(p, "CS2_182"); var second = Summon(p, "CS2_182"); right = Summon(p, "CS2_182");
        Play(g, p, id); left.Silence(); right.Silence();
        Resume(g, next => {
            ((Minion)next.IdEntityDic[bearer.Id]).ToBeDestroyed = true;
            ((Minion)next.IdEntityDic[second.Id]).ToBeDestroyed = true;
            next.DeathProcessingAndAuraUpdate();
        });
        var firstOutput = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == bearer.Id);
        var secondOutput = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == second.Id);
        Core2026Proof.Check(p.BoardZone.Slots.Select(m => m.Id).SequenceEqual(new[] { location.Id, left.Id, firstOutput.Id, secondOutput.Id, right.Id }),
            "simultaneous granted deaths refill their last physical slots rightward");
        CataclysmPoolProof.PutBoardAside(p);
        location = LocationProofs.Location(p); left = Summon(p, "CS2_182"); bearer = Summon(p, "CS2_182"); right = Summon(p, "CS2_182");
        Play(g, p, id);
        var enchantment = bearer.AppliedEnchantments.Single(e => e.Card.Id == "CATA_471e");
        enchantment.Power.DeathrattleTask.Process(g, p, enchantment, null, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
        var fromEnchantment = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == enchantment.Id);
        Core2026Proof.Check(p.BoardZone.GetSlot(3) == fromEnchantment && p.BoardZone.GetSlot(4) == right,
            "enchantment source resolves living target and summons immediately right");
        p.SetasideZone.Add(p.BoardZone.Remove(fromEnchantment)); bearer.Silence(); Kill(g, bearer);
        enchantment.Power.DeathrattleTask.Process(g, p, enchantment, null, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
        fromEnchantment = p.BoardZone.Single(m => m[GameTag.DISPLAYED_CREATOR] == enchantment.Id);
        Core2026Proof.Check(p.BoardZone.GetSlot(2) == fromEnchantment && p.BoardZone.GetSlot(3) == right && p.BoardZone.GetSlot(0) == location,
            "enchantment source uses departed target's last middle slot");
    });
}
