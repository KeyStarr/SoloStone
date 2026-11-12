using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static ChooseOneProof;

internal static class ReforestationProof
{
    // Unpinned_ReforestationEnd Unpinned_HandFresh Unpinned_HandUpgradeCost Unpinned_HandProjection.
    internal static void Run() => StandardSetsProof.Case("EDR_843", (g, p, id) => {
        EmeraldDreamCardStateProof.Token("EDR_843a", 115932); EmeraldDreamCardStateProof.Token("EDR_843b", 115933);
        EmeraldDreamCardStateProof.Token("EDR_843t1", 117402);
        EmeraldDreamCardStateProof.HiddenUpgrade(id, "EDR_843t1", turnEnds: true);
        OptionsSurvive(g, p, id, "EDR_843a", "EDR_843b");
        PoolPilotProof.EmptyHand(p); Deck(p);
        PlayOption(g, p, id, 1);
        Core2026Proof.Check(p.HandZone.Single().Card.Id == "GAME_005", "spell branch draws spell only");
        PoolPilotProof.EmptyHand(p); Deck(p); PlayOption(g, p, id, 2);
        Core2026Proof.Check(p.HandZone.Single().Card.Id == "CS2_182", "minion branch draws minion only");
        PoolPilotProof.EmptyHand(p); Deck(p); Both(p); PlayOption(g, p, id, -1);
        Core2026Proof.Check(p.HandZone.Select(c => c.Card.Id).ToHashSet().SetEquals(new[] { "GAME_005", "CS2_182" }) && p.HandZone.Count == 2,
            "Unpinned_ReforestationEnd Choose Both draws each exactly once before upgrade");
        ParkBoard(p); PoolPilotProof.EmptyHand(p); Deck(p);
        var held = (Playable)Add(p, id); int slot = held.ZonePosition;
        Play(g, p, "GAME_005");
        Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3, "casts do not age Reforestation");
        End(g); Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "Unpinned_ReforestationEnd receiving owner end counts");
        End(g); Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "opponent end excluded");
        End(g); Core2026Proof.Check(held[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && held.Card.Id == id, "second owner end not yet upgraded");
        Effects.ReduceCost(1).ApplyTo(held);
        PoolPilotProof.Resume(g, next => { End(next); End(next); });
        var upgraded = g.IdEntityDic[held.Id];
        Core2026Proof.Check(upgraded.Card.Id == "EDR_843t1" && upgraded.ZonePosition == slot && upgraded.Cost == 2
            && upgraded[GameTag.TAG_SCRIPT_DATA_NUM_1] == 0 && !upgraded.Card.ChooseOne,
            "Unpinned_HandFresh Unpinned_HandUpgradeCost Unpinned_HandProjection same-id third-end upgrade drops cost and Choose One");
        End(g);
        foreach (var card in p.HandZone.Where(c => c.Id != upgraded.Id).ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        Deck(p);
        Both(p);
        PlayEntity(g, p, upgraded);
        Core2026Proof.Check(p.HandZone.Count == 2 && p.HandZone.Select(c => c.Card.Id).ToHashSet().SetEquals(new[] { "GAME_005", "CS2_182" }), "upgraded form draws both once");
        PoolPilotProof.EmptyHand(p); PoolPilotProof.ClassDeck(p);
        int damage = p.Hero.Damage; Play(g, p, "EDR_843t1");
        Core2026Proof.Check(p.HandZone.Count == 0 && p.Hero.Damage == damage, "no matching card gives no fatigue");
        Deck(p); PoolPilotProof.FillHand(p, 9); Play(g, p, "EDR_843t1");
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == 1, "draws run sequentially; second burns at hand capacity");
    });

    private static void Deck(Controller p)
    {
        PoolPilotProof.ClassDeck(p);
        foreach (string id in new[] { "GAME_005", "CS2_182", "CS2_106" }) Entity.FromCard(p, Cards.FromId(id), zone: p.DeckZone);
    }
}
