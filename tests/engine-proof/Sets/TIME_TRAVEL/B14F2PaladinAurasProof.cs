using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static PaladinAurasProof;

internal static class B14F2PaladinAurasCardsProof
{
    [GroupProof("b14-f2-paladin-auras-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("TIME_700", Unpinned_ChronologicalDuration);
        StandardSetsProof.Case("END_011", Pinned_AccelerationThreeFutureOwnerStarts);
        StandardSetsProof.Case("TIME_019", ManifestedTimeways);
        StandardSetsProof.Case("TIME_017", Unpinned_Tankgineer_GVG079Identity);
        StandardSetsProof.Case("TIME_009", GelbinOfTomorrow);
        B14F2CoreAuraProof.Run();
        B14F2EmeraldAuraProof.Run();
        B14F2VioletAuraProof.Run();
        B14F2CataclysmAuraProof.Run();
        B14F2OathbinderProof.Run();
    }

    private static void Unpinned_ChronologicalDuration(Game g, Controller p, string id)
    {
        var aura = Play(g, p, id);
        Core2026Proof.Check(Active(p).Single() == aura && p.BoardZone.Count == 0, "cast installs the public spell actor");
        for (int tick = 1; tick <= 3; tick++)
        {
            End(g);
            Core2026Proof.Check(p.BoardZone.Count == tick && p.BoardZone.All(m => m.Card.Id == "TIME_700t"
                && m.AttackDamage == 3 && m.Health == 5 && m.HasTaunt), "exported Dragon per owner end");
            Core2026Proof.Check(Active(p).Length == (tick == 3 ? 0 : 1), "last summon precedes expiry");
            End(g);
        }
        End(g);
        Core2026Proof.Check(p.BoardZone.Count == 3, "no fourth tick");
    }

    private static void Pinned_AccelerationThreeFutureOwnerStarts(Game g, Controller p, string id)
    {
        var aura = Play(g, p, id); p.BaseMana = 2;
        Core2026Proof.Check(p.TemporaryMana == 0, "casting turn grants no Mana and consumes no start tick");
        for (int tick = 1; tick <= 4; tick++)
        {
            End(g); End(g);
            Core2026Proof.Check(p.TemporaryMana == (tick <= 3 ? 1 : 0), "next three owner starts grant, fourth does not");
            Core2026Proof.Check(Active(p).Length == (tick < 3 ? 1 : 0), "grant before third-start expiry");
        }
    }

    private static void ManifestedTimeways(Game g, Controller p, string id)
    {
        var enemy = Summon(p.Opponent, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 0 && p.Opponent.Hero.Damage == 0, "no Aura means no damage");
        Install(p, "TIME_700");
        Play(g, p, id);
        Core2026Proof.Check(enemy.Damage == 3 && p.Opponent.Hero.Damage == 3, "active Aura gates all-enemy damage");
    }

    private static void Unpinned_Tankgineer_GVG079Identity(Game g, Controller p, string id)
    {
        Minion source = null;
        var board = StandardSetsProof.DeathrattleSlotBoard(p, () => source = StandardSetsProof.PlayAt(g, p, id, 2));
        source.AttackDamage = 12; source.BaseHealth = 12;
        Kill(g, source);
        var tank = p.BoardZone.Single(m => m.Card.Id == "GVG_079");
        Core2026Proof.Check(tank.Card.AssetId == 2047 && tank.Cost == 8 && tank.AttackDamage == 7 && tank.Health == 7
            && tank.HasDivineShield && tank.IsRace(Race.MECHANICAL), "fresh exported Tank identity and base state");
        StandardSetsProof.CheckDeathrattleSlot(p, board, new[] { tank }, "Tank occupies death slot");
        var muted = (Minion)Play(g, p, id); muted.Silence(); Kill(g, muted);
        Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "GVG_079") == 1, "Silence removes Tankgineer Deathrattle");
        tank.Silence(); Core2026Proof.Check(!tank.HasDivineShield, "Tank shield removable");
    }

    private static void GelbinOfTomorrow(Game g, Controller p, string id)
    {
        foreach (bool reverse in new[] { false, true })
        {
            var buffGame = HeroDivineShieldProof.NewGame(true); var owner = buffGame.Player1;
            var names = new[] { "CS2_182", "CS2_168", "CS2_171" };
            foreach (string name in reverse ? names.Reverse() : names) Summon(owner, name);
            Install(owner, "TIME_009t2"); NonFirstPick(buffGame, 3);
            var targets = owner.BoardZone.ToArray(); var rng = buffGame.Random.Clone();
            var expected = targets[rng.Next(3)]; int attack = expected.AttackDamage, health = expected.Health;
            End(buffGame);
            Core2026Proof.Check(expected.AttackDamage == attack + 4 && expected.Health == health + 4 && expected.HasDivineShield
                && targets.Count(m => m.HasDivineShield) == 1 && buffGame.Random.CaptureState().Equals(rng.CaptureState()),
                "Mekkatorque uses seeded reordered-board pick, one draw and one recipient");
        }
        foreach (var card in p.DeckZone.ToArray()) p.SetasideZone.Add(p.DeckZone.Remove(card));
        foreach (string aura in new[] { "TIME_700", "TIME_700", "TIME_009t1", "TIME_009t2", "END_011" })
            Entity.FromCard(p, Cards.FromId(aura), zone: p.DeckZone);
        int casts = g.HistoryFacts.Query.Casts().Count();
        var gelbin = (Minion)Play(g, p, id);
        Core2026Proof.Check(Active(p).Length == 4 && p.DeckZone.Count == 1 && p.DeckZone.Single().Card.Id == "TIME_700",
            "one original entity of each Aura identity enters directly, duplicate stays in deck");
        Core2026Proof.Check(g.HistoryFacts.Query.Casts().Count() == casts, "put into battlefield does not cast");
        gelbin.Silence(); Kill(g, gelbin); p.Hero.Damage = 12;
        var target = Summon(p, "CS2_182");
        End(g);
        Core2026Proof.Check(p.Hero.Damage == 8 && p.BoardZone.Any(m => m.Card.Id == "TIME_700t")
            && p.BoardZone.Any(m => m.HasDivineShield && m.AttackDamage >= 7), "bundled Auras act after Gelbin leaves");
    }
}
