using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class RagnarosTheGreatFireProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_150", (g, p, id) => {
        for (int tier = 0; tier < 3; tier++)
        {
            ParkBoard(p); ParkBoard(p.Opponent); p[HeraldRules.Progress] = tier * 2;
            p.Opponent.Hero.Damage = 0;
            var body = (Minion)Play(g, p, id);
            int factor = 1 << tier;
            TheBlackBloodProof.Lean_Appendages(body, new[] { "CATA_150t", "CATA_150t1" }, 2 * factor, factor, 2 * factor);
            Core2026Proof.Check(body.AttackDamage == 8 && body.Health == 8 && body.Card[(GameTag)4538] == 1,
                "Herald scales hands, never Ragnaros");
            var clone = g.CloneExact(history: true); End(clone); End(g);
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "end-turn Deathrattles clone future parity");
            Core2026Proof.Check(p.Opponent.Hero.Damage == 4 * factor && ColossalRules.AppendagesOf(body).Count() == 2,
                "both living hands deal 2/4/8 each at Herald counts 0/2/4 without dying");
            End(g);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 4 * factor, "opponent end does not activate friendly Deathrattles");
            var hands = ColossalRules.AppendagesOf(body).ToArray();
            hands[0].Silence(); Kill(g, hands[0]); Kill(g, hands[1]);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 6 * factor, "killed hand deals scaled damage; silenced hand has no Deathrattle");
        }
        ParkBoard(p); ParkBoard(p.Opponent); p[HeraldRules.Progress] = 0; p.Opponent.Hero.Damage = 0;
        var rag = Summon(p, id); var hand = ColossalRules.AppendagesOf(rag).First();
        Kill(g, rag);
        End(g); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "departed body no longer triggers living hands");
        Kill(g, hand);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 2, "Lean_C4 independent hand Deathrattle survives body death");
        Lean_LeftToRight(g, p, id);
        GrantedDeathrattles(g, p, id);
        DeathrattlesBeforeLaterEndTurn(g, p, id);
        LivingLegacyDeathrattlePosition(g, p, id);
        Lean_DeathsAfterWholeTrigger(g, p, id);
        TheBlackBloodProof.Lean_FullBoard(g, p, id);
    });

    private static void Lean_LeftToRight(Game g, Controller p, string id)
    {
        ParkBoard(p); ParkBoard(p.Opponent); p.Opponent.Hero.Damage = 0;
        var right = Summon(p, "CATA_210"); right[GameTag.TAG_SCRIPT_DATA_NUM_1] = 7; right[GameTag.TAG_SCRIPT_DATA_NUM_2] = 7;
        var left = (Minion)Entity.FromCard(p, Cards.FromId("CATA_210"));
        Generic.SummonBlock(g, left, 0, null);
        left[GameTag.TAG_SCRIPT_DATA_NUM_1] = 3; left[GameTag.TAG_SCRIPT_DATA_NUM_2] = 3;
        var body = Summon(p, id);
        LocationProofs.Location(p, position: 1);
        var enemyEgg = Summon(p.Opponent, "FP1_007"); enemyEgg.BaseHealth = 100;
        End(g);
        var whelp = p.BoardZone.Single(m => m.Card.Id == "CATA_210t");
        Core2026Proof.Check(p.BoardZone.IsFull && whelp.AttackDamage == 3 && whelp.Health == 3 && whelp.ZonePosition == 1
            && whelp[GameTag.DISPLAYED_CREATOR] == left.Id && left.Zone == p.BoardZone && right.Zone == p.BoardZone,
            "Lean_LeftToRight: physical board order beats play order; first Deathrattle takes last slot beside living egg");
        Core2026Proof.Check(!p.Opponent.BoardZone.Any(m => m.Card.Id == "FP1_007t"), "enemy Deathrattle never activated");
        End(g); body.Silence(); int count = p.BoardZone.Count;
        int damage = p.Opponent.Hero.Damage + enemyEgg.Damage;
        End(g);
        Core2026Proof.Check(p.BoardZone.Count == count && p.Opponent.Hero.Damage + enemyEgg.Damage == damage,
            "Silence removes Ragnaros end-turn trigger");
        End(g);
    }

    private static void GrantedDeathrattles(Game g, Controller p, string id)
    {
        foreach (bool silenced in new[] { false, true })
        {
            ParkBoard(p); ParkBoard(p.Opponent); p.Opponent.Hero.Damage = 0;
            var body = Summon(p, id);
            foreach (var hand in ColossalRules.AppendagesOf(body)) hand.Silence();
            var egg = Summon(p, "FP1_007");
            if (silenced) egg.Silence();
            Generic.AddEnchantmentBlock(g, Cards.FromId("JAIL_906e"), egg, egg);
            End(g);
            Core2026Proof.Check(p.BoardZone.Count(m => m.Card.Id == "JAIL_906") == 1
                && p.BoardZone.Count(m => m.Card.Id == "FP1_007t") == (silenced ? 0 : 1) && egg.Zone == p.BoardZone,
                "living printed and granted Deathrattles run once each; silenced printed text stays removed");
            Core2026Proof.Check(p.Opponent.Hero.Damage == 0, "silenced living hands never activate printed damage");
            End(g);
        }
    }

    private static void DeathrattlesBeforeLaterEndTurn(Game g, Controller p, string id)
    {
        foreach (bool ragnarosFirst in new[] { true, false })
        {
            ParkBoard(p); ParkBoard(p.Opponent);
            if (!ragnarosFirst) Summon(p, "CATA_476");
            var body = Summon(p, id);
            foreach (var hand in ColossalRules.AppendagesOf(body)) hand.Silence();
            var egg = Summon(p, "CATA_210");
            if (ragnarosFirst) Summon(p, "CATA_476");
            LocationProofs.Location(p, position: 0);
            Core2026Proof.Check(p.BoardZone.FreeSpace == 1, "end-turn summoners compete for one physical slot");
            var clone = g.CloneExact(history: true); End(clone); End(g);
            Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "competing end-turn summons clone future parity");
            Core2026Proof.Check(p.BoardZone.IsFull
                && p.BoardZone.Count(m => m.Card.Id == "CATA_210t") == (ragnarosFirst ? 1 : 0)
                && p.BoardZone.Count(m => m.Card.Id == "CATA_476t") == (ragnarosFirst ? 0 : 1),
                "Ragnaros Deathrattle resolves inside its trigger; earlier end-turn summoner takes last slot");
            if (ragnarosFirst)
            {
                var whelp = p.BoardZone.Single(m => m.Card.Id == "CATA_210t");
                Core2026Proof.Check(whelp.AttackDamage == 2 && whelp.Health == 1 && whelp.ZonePosition == egg.ZonePosition + 1,
                    "winning Deathrattle summons exact Whelp beside living Egg");
            }
            End(g);
        }
    }

    private static void LivingLegacyDeathrattlePosition(Game g, Controller p, string id)
    {
        ParkBoard(p); ParkBoard(p.Opponent);
        var body = Summon(p, id);
        foreach (var hand in ColossalRules.AppendagesOf(body)) hand.Silence();
        var cairne = Summon(p, "CORE_EX1_110");
        var right = Summon(p, "CS2_182");
        var location = LocationProofs.Location(p, position: cairne.ZonePosition);
        cairne.LastBoardPosition = 0;
        var before = p.BoardZone.Slots.Select(e => e.Id).ToArray();
        var clone = g.CloneExact(history: true); End(clone); End(g);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "living legacy Deathrattle position clone future parity");
        var baine = p.BoardZone.Single(m => m.Card.Id == "EX1_110t");
        Core2026Proof.Check(baine.Card.AssetId == 318 && baine.AttackDamage == 5 && baine.Health == 5
            && p.BoardZone.IsFull && cairne.Zone == p.BoardZone && !cairne.IsDead
            && cairne.ZonePosition == location.ZonePosition + 1 && baine.ZonePosition == cairne.ZonePosition + 1
            && right.ZonePosition == baine.ZonePosition + 1
            && p.BoardZone.Slots.Where(e => e.Id != baine.Id).Select(e => e.Id).SequenceEqual(before),
            "legacy Baine summons right of living middle Cairne with location occupying physical slot: "
                + string.Join(",", p.BoardZone.Slots.Select(e => e.Card.Id)) + $"; Baine={baine.AttackDamage}/{baine.Health}");
        End(g);
        Kill(g, cairne);
        var actualDeathBaine = p.BoardZone.Single(m => m.Card.Id == "EX1_110t" && m.Id != baine.Id);
        Core2026Proof.Check(actualDeathBaine.ZonePosition == location.ZonePosition + 1 && baine.ZonePosition == actualDeathBaine.ZonePosition + 1,
            "later actual death overwrites living summon cursor and fills Cairne's vacated slot");
    }

    private static void Lean_DeathsAfterWholeTrigger(Game g, Controller p, string id)
    {
        ParkBoard(p); ParkBoard(p.Opponent); p.Opponent.Hero.Damage = 0;
        var sheep = Summon(p, "GVG_076"); sheep.BaseHealth = 3;
        var body = Summon(p, id);
        var enemy = Summon(p.Opponent, "CS2_182"); enemy.BaseHealth = 100;
        var clone = g.CloneExact(history: true); End(clone); End(g);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "random enemy damage and deferred deaths clone future parity");
        Core2026Proof.Check(sheep.Health == 1 && body.Health == 6 && !ColossalRules.AppendagesOf(body).Any()
            && enemy.Damage + p.Opponent.Hero.Damage == 10,
            "Lean_DeathsAfterWholeTrigger: both doomed hands activate before cleanup, then both actual Deathrattles resolve");
        End(g);
    }
}
