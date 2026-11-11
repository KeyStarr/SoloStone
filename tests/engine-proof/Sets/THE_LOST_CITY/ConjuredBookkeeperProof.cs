using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

// Tier D: pinned export TLC_226; draw only spells, independently on each copy's death.
internal static class ConjuredBookkeeperProof
{
    internal static void Run() => StandardSetsProof.Case("TLC_226", (g, p, id) => {
        CraterExperimentProof.KindredCopy(g, p, id);
        ParkBoard(p);
        var spell = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        var source = Summon(p, id);
        Kill(g, source);
        Core2026Proof.Check(spell.Zone == p.HandZone, "Deathrattle tutors spell past nonspells");
        source = Summon(p, id); source.Silence();
        spell = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        Kill(g, source);
        Core2026Proof.Check(spell.Zone == p.DeckZone, "Silence removes spell draw");
        p.SetasideZone.Add(p.DeckZone.Remove(spell));
        int damage = p.Hero.Damage, hand = p.HandZone.Count;
        Kill(g, Summon(p, id));
        Core2026Proof.Check(p.Hero.Damage == damage && p.HandZone.Count == hand, "no eligible spell causes no draw or fatigue");
        Play(g, p, "CS2_033"); End(g); End(g); ParkBoard(p); PoolPilotProof.EmptyHand(p);
        var first = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        var second = Entity.FromCard(p, Cards.FromId("CS2_024"), zone: p.DeckZone);
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.Count == 2, "Kindred source and one copy");
        PoolPilotProof.Resume(g, next => ClearBoard(next, next.Player1));
        Core2026Proof.Check(first.Zone == p.HandZone && second.Zone == p.HandZone, "source and copy each draw independently across clone/snapshot");
        spell = Entity.FromCard(p, Cards.FromId("CS2_029"), zone: p.DeckZone);
        PoolPilotProof.FillHand(p);
        Kill(g, Summon(p, id));
        Core2026Proof.Check(spell.Zone == p.GraveyardZone && p.HandZone.Count == 10, "full hand burns tutored spell");
    }, legacyPoolMember: true);
}
