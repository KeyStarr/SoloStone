using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class SmolderingStrengthProof
{
    internal static void Run() => StandardSetsProof.Case("FIR_914", (g, p, id) => SmolderingStrengthProof.Scenario(g, p, id));

    internal static void Scenario(Game g, Controller p, string id)
    {
        if (id == "FIR_914") { Automatic(); Spellbender(); }
        LongHistoryBound(id);
        ExtraTurns(id);
        StarsurgeProof.Hidden(id, (game, owner) => { End(game); End(game); });
        End(g); End(g); // Turns before creation never upgrade the new card.
        var source = Add(p, id); g.AuraUpdate();
        Core2026Proof.Check(source.Card.ConsumesHistory && source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && source[GameTag.TAG_SCRIPT_DATA_NUM_2] == 3, "new hand residence starts at 1 with three turns left");
        var immediate = g.Clone(); Resolve(immediate, immediate.Player1, (IPlayable)immediate.IdEntityDic[source.Id], 1);
        End(g);
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && source[GameTag.TAG_SCRIPT_DATA_NUM_2] == 2, "first owner end upgrades once");
        End(g);
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "enemy end does not upgrade");
        var branch = g.Clone(); End(branch); End(branch);
        var upgraded = (IPlayable)branch.IdEntityDic[source.Id];
        Core2026Proof.Check(upgraded[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 && upgraded[GameTag.TAG_SCRIPT_DATA_NUM_2] == 1, "third playable owner turn is final turn");
        if (id == "FIR_911")
        {
            var full = branch.Clone(); var owner = full.Player1;
            while (!owner.HandZone.IsFull) Add(owner, "CS2_182");
            int deck = owner.DeckZone.Count;
            PlayEntity(full, owner, (IPlayable)full.IdEntityDic[upgraded.Id]);
            Core2026Proof.Check(owner.HandZone.Count == 10 && owner.DeckZone.Count == deck - 3, "full hand admits one draw and burns remaining two");
        }
        Resolve(branch, branch.Player1, upgraded, 3);
        Core2026Proof.Check(source.Zone == p.HandZone && source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "clone progress does not mutate original");
        End(g); End(g); End(g);
        Core2026Proof.Check(source.Zone.Type == Zone.GRAVEYARD && p.DiscardedEntities.Contains(source.Id), "discard at third owner end");
        p.HandZone.Add(p.GraveyardZone.Remove(source)); g.AuraUpdate();
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1 && source[GameTag.TAG_SCRIPT_DATA_NUM_2] == 3, "reentry resets age");
        End(g);
        Core2026Proof.Check(source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "entry during opponent turn has no retroactive owner end");
        Resolve(g, p, source, 1);
    }

    private static void Automatic()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var legal = Summon(p, "CS2_182"); var elusive = Summon(p, "EDR_598");
        var enemy = Summon(p.Opponent, "CS2_182");
        var source = Entity.FromCard(p, Cards.FromId("FIR_914"));
        new SabberStoneCore.EffectSystem.EffectContext(g, p.Id, source.Id).CastSpell(source.Id, HistoryCastKind.Effect, elusive.Id);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(legal.AttackDamage == 5 && legal.Health == 6 && elusive.AttackDamage == 5 && enemy.AttackDamage == 4,
            "automatic Strength chooses legal friendly target, excludes Elusive and enemy, starts at base without hand residence");
    }

    private static void Spellbender()
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var friendly = Summon(p, "CS2_182");
        var source = Add(p, "FIR_914");
        End(g);
        Play(g, p.Opponent, "tt_010");
        End(g);
        PlayEntity(g, p, source, friendly);
        var token = p.Opponent.BoardZone.Single(m => m.Card.Id == "tt_010a");
        Core2026Proof.Check(token.AttackDamage == 3 && token.Health == 5
            && token.AppliedEnchantments.Single().Card.Id == "FIR_914e"
            && friendly.AttackDamage == 4 && friendly.Health == 5 && p.Opponent.SecretZone.Count == 0,
            "Spellbender redirects upgraded Strength to enemy token without rechecking friendly selection restriction");
    }

    private static void LongHistoryBound(string id)
    {
        var shortGame = HistoryFactsProof.Fixture(false);
        var longGame = HistoryFactsProof.Fixture(false);
        // Earlier owner turns must neither age the later card nor grow display work.
        for (int i = 0; i < 40; i++) End(longGame);
        foreach (var owner in new[] { longGame.Player1, longGame.Player2 })
            foreach (var card in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(card));
        var debit = Entity.FromCard(longGame.Player1, Cards.FromId("CS2_120"));
        while (longGame.HistoryFacts.Count < 10000) { longGame.Player1.UsedMana = 0; Generic.PayPhase(longGame.Player1, debit); }
        var shortCard = Add(shortGame.Player1, id);
        var longCard = Add(longGame.Player1, id);
        for (int i = 0; i < 1000; i++)
        {
            var owner = longCard.Controller;
            owner.SetasideZone.Add(owner.HandZone.Remove(longCard));
            owner.HandZone.Add(owner.SetasideZone.Remove(longCard));
        }
        End(shortGame); End(longGame);
        long Allocated(Game game)
        {
            long bytes = 0;
            foreach (var branch in new[] { game, game.CloneForSimulation(53), game.CloneForSimulation(54) })
            {
                for (int i = 0; i < 10; i++) branch.AuraUpdate(); // Warm both recipes and tag setters.
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 20; i++) branch.AuraUpdate();
                bytes += GC.GetAllocatedBytesForCurrentThread() - before;
            }
            return bytes;
        }
        long shortBytes = Allocated(shortGame), longBytes = Allocated(longGame);
        Core2026Proof.Check(longCard[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2 && longCard[GameTag.TAG_SCRIPT_DATA_NUM_2] == 2,
            "long history and repeated residences retain only latest residence age");
        Core2026Proof.Check(longBytes <= shortBytes + 8192,
            $"{id} display refresh work must not materialize full turn history: short={shortBytes}, long={longBytes}");
        Console.WriteLine($"PASS Smoldering display bound {id}: facts={longGame.HistoryFacts.Count}; refreshes=60; shortBytes={shortBytes}; longBytes={longBytes}");
    }

    private static void ExtraTurns(string id)
    {
        var g = HistoryFactsProof.Fixture(); var p = g.Player1;
        var source = Add(p, id);
        p.NumTurnsLeft = 3;
        End(g);
        Core2026Proof.Check(g.CurrentPlayer == p && source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2,
            "extra owner turn upgrades after first end");
        End(g);
        Core2026Proof.Check(g.CurrentPlayer == p && source.Zone == p.HandZone && source[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3,
            "second extra owner turn is final playable turn");
        End(g);
        Core2026Proof.Check(source.Zone == p.GraveyardZone && p.DiscardedEntities.Contains(source.Id),
            "third consecutive owner end discards");
    }

    internal static void Resolve(Game g, Controller p, IPlayable source, int magnitude)
    {
        source[GameTag.TAG_SCRIPT_DATA_NUM_1] = 99;
        if (source.Card.Id == "FIR_914")
        {
            var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == source.Id && t.Target?.Id == enemy.Id), "friendly-only target");
            PlayEntity(g, p, source, friendly);
            Core2026Proof.Check(friendly.AttackDamage == 4 + magnitude && friendly.Health == 5 + magnitude, "buff reads residence, exact magnitude");
            Core2026Proof.Check(friendly.AppliedEnchantments.Single().Card.Id == "FIR_914e", "exact buff enchantment");
            friendly.Silence();
            Core2026Proof.Check(friendly.AttackDamage == 4 && friendly.Health == 5, "silence removes buff");
        }
        else if (source.Card.Id == "FIR_916")
        {
            var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
            Summon(p, "CS2_142");
            PlayEntity(g, p, source);
            Core2026Proof.Check(enemy.Damage == magnitude + 1 && friendly.Damage == 0 && p.Opponent.Hero.Damage == 0, "AoE magnitude plus Spell Damage; only enemy minions");
        }
        else
        {
            int before = p.HandZone.Count;
            PlayEntity(g, p, source);
            Core2026Proof.Check(p.HandZone.Count == before - 1 + magnitude, "draw exact residence magnitude");
        }
    }
}
