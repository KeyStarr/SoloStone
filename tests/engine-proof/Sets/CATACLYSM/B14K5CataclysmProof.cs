using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14K5CataclysmProof
{
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_553", Ebyssian);
        StandardSetsProof.Case("CATA_563", Unpinned_Cloudstrider);
    }

    private static void Ebyssian(Game g, Controller p, string id)
    {
        var held = (Minion)Add(p, id);
        Play(g, p, "CS2_182"); Summon(p, "EX1_043");
        Core2026Proof.Check(held.Card.Id == id && held.AttackDamage == 6 && held.Health == 6 && !held.Card.IsRace(Race.DRAGON),
            "ordinary minion play and Dragon summon do not change held Ebyssian");
        End(g); Play(g, p.Opponent, "EX1_043"); End(g);
        Core2026Proof.Check(held.Card.Id == id, "enemy Dragon play does not transform hand");
        foreach (var next in B14K5ShamanHunterProof.Forks(g))
        {
            Play(next, next.Player1, "EX1_043");
            var transformed = (Minion)next.IdEntityDic[held.Id];
            Core2026Proof.Check(transformed.Card.Id == "CATA_553t" && transformed.Card.AssetId == 122872
                && transformed.AttackDamage == 12 && transformed.Health == 12 && transformed.Card.IsRace(Race.DRAGON)
                && transformed.Zone == next.Player1.HandZone, "held identity becomes exact 12/12 Dragon in every fork");
            PlayEntity(next, next.Player1, transformed);
            Core2026Proof.Check(transformed.IsRush && next.Player1.BoardZone.Where(m => m.Card.IsRace(Race.DRAGON)).All(m => m.IsRush)
                && next.Player1.BoardZone.Where(m => !m.Card.IsRace(Race.DRAGON)).All(m => !m.IsRush)
                && next.Player2.BoardZone.All(m => !m.IsRush), "permanent effect affects only friendly Dragons, including transformed source");
            transformed.Silence(); Kill(next, transformed);
            End(next); End(next);
            var future = Summon(next.Player1, "EX1_043");
            Core2026Proof.Check(future.IsRush, "player effect survives source Silence, death and turn end");
            future.Silence(); next.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(future.IsRush, "ongoing player aura remains on a silenced Dragon");
        }
        StandardSetsProof.ParkBoard(p);
        var plain = (Minion)Play(g, p, id);
        Core2026Proof.Check(plain.Card.Id == id && plain.AttackDamage == 6 && plain.Health == 6 && !plain.IsRush,
            "untransformed Ebyssian remains non-Dragon 6/6");
        var fresh = (Minion)Add(p, id); p.SetasideZone.Add(p.HandZone.Remove(fresh));
        Play(g, p, "EX1_043");
        Core2026Proof.Check(fresh.Card.Id == id, "offboard Ebyssian does not subscribe to hand trigger");
    }

    // STILL-UNPINNED (owner-approved): data/rules-evidence/deathrattle-copy/cloudstrider-cast-target.yaml
    // and data/rules-evidence/deathrattle-copy/cloudstrider-spell-modifications.yaml.
    // Tier E firsthand loss report; tier C automatic-cast targeting. Preserve definition only, not hand grants.
    private static void Unpinned_Cloudstrider(Game g, Controller p, string id)
    {
        var empty = (Minion)Play(g, p, id); Kill(g, empty);
        Core2026Proof.Check(p.Choice == null, "empty hand absorbs/casts nothing");
        var expensive = Add(p, "CS2_029"); expensive.Cost = 5;
        var cheap = Add(p, "CS2_029"); cheap.Cost = 4;
        Generic.AddEnchantmentBlock(g, Cards.FromId("CATA_458e"), p.Hero, cheap);
        var body = Add(p, "CS2_182");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.Choice != null && p.Choice.Choices.SequenceEqual(new[] { cheap.Id }), "only current-cost-at-most-four spells are offered");
        foreach (var next in B14K5ShamanHunterProof.Forks(g))
        {
            PlayerDecisionProof.Answer(next, cheap.Id);
            var owner = next.Player1; var cloud = (Minion)next.IdEntityDic[source.Id];
            var absorbed = next.IdEntityDic[cheap.Id];
            Core2026Proof.Check(absorbed.Zone != owner.HandZone && owner.DiscardedEntities.Count == 0
                && owner.HandZone.Select(c => c.Id).SequenceEqual(new[] { expensive.Id, body.Id }),
                "absorb removes exactly chosen card without discard bookkeeping");
            var capture = cloud.AppliedEnchantments.Single(e => e.Card.Id == "CATA_563e2");
            Core2026Proof.Check(capture.Card.AssetId == 122998 && capture.ScriptTag1 == Cards.FromId("CS2_029").AssetId,
                "exported Deathrattle actor carries captured definition for dynamic text");
            int before = owner.Hero.Damage + owner.Opponent.Hero.Damage;
            Kill(next, cloud);
            Core2026Proof.Check(owner.Choice == null && owner.Hero.Damage + owner.Opponent.Hero.Damage == before + 6,
                "Deathrattle casts base Fireball with an automatic target; individual Spell Damage was lost");
        }
        var silentSpell = Add(p, "GAME_005"); source = (Minion)Play(g, p, id);
        PlayerDecisionProof.Answer(g, silentSpell.Id); source.Silence();
        int mana = p.TemporaryMana; Kill(g, source);
        Core2026Proof.Check(p.TemporaryMana == mana, "Silence removes absorbed Deathrattle");
        source = (Minion)Play(g, p, id); Core2026Proof.Check(p.Choice == null, "remaining expensive spell and minion cannot be absorbed");
        StandardSetsProof.ParkBoard(p);
        var frost = Add(p, "CS2_024"); source = (Minion)Play(g, p, id); PlayerDecisionProof.Answer(g, frost.Id);
        End(g);
        Kill(g, source);
        Core2026Proof.Check(p.Choice == null && p.Hero.Damage + p.Opponent.Hero.Damage == 9, "automatic targeted cast works on opponent turn");
        End(g);
        // Repeated Battlecries retain independent definition captures; a living DR activation is repeatable.
        StandardSetsProof.ParkBoard(p); Summon(p, "LOE_077");
        var coin1 = Add(p, "GAME_005"); var coin2 = Add(p, "GAME_005");
        source = (Minion)Play(g, p, id); PlayerDecisionProof.Answer(g, coin1.Id); PlayerDecisionProof.Answer(g, coin2.Id);
        Core2026Proof.Check(source.AppliedEnchantments.Count(e => e.Card.Id == "CATA_563e2") == 2, "Brann stores both independently");
        mana = p.TemporaryMana; Kill(g, source);
        Core2026Proof.Check(p.TemporaryMana == mana + 2 && p.Choice == null, "both captured coins cast exactly once");

        foreach (bool reverse in new[] { false, true })
        {
            var next = g.CloneExact(history: true); var owner = next.Player1;
            StandardSetsProof.ParkBoard(owner); StandardSetsProof.ParkBoard(owner.Opponent);
            foreach (var held in owner.HandZone.ToArray()) owner.SetasideZone.Add(owner.HandZone.Remove(held));
            owner.Hero.Damage = 0; owner.Opponent.Hero.Damage = 0;
            var a = Summon(owner.Opponent, "CS2_182"); var b = Summon(owner.Opponent, "CS2_200");
            a.BaseHealth = 20; b.BaseHealth = 20;
            if (reverse) { owner.Opponent.BoardZone.Remove(a); owner.Opponent.BoardZone.Add(a); }
            var spell = Add(owner, "CS2_029"); var cloud = (Minion)Play(next, owner, id);
            PlayerDecisionProof.Answer(next, spell.Id);
            var oracle = next.CloneExact(); var oracleOwner = oracle.Player1;
            oracleOwner.SetasideZone.Add(oracleOwner.BoardZone.Remove((Minion)oracle.IdEntityDic[cloud.Id]));
            var profile = (Playable)Entity.FromCard(oracleOwner, Cards.FromId("CS2_029"));
            var targets = profile.ValidPlayTargets.Where(profile.IsValidPlayTarget).ToArray();
            int expected = targets[oracle.Random.Next(targets.Length)].Id;
            long draws = next.Random.DrawCount;
            foreach (var fork in B14K5ShamanHunterProof.Forks(next))
            {
                Kill(fork, (Minion)fork.IdEntityDic[cloud.Id]);
                Core2026Proof.Check(((Character)fork.IdEntityDic[expected]).Damage == 6
                    && targets.Where(t => t.Id != expected).All(t => ((Character)fork.IdEntityDic[t.Id]).Damage == 0)
                    && fork.Random.DrawCount == draws + 1, "seeded legal automatic target survives reordered boards and saved futures");
            }
        }
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        foreach (var held in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(held));
        var execute = Add(p, "CS2_108"); source = (Minion)Play(g, p, id);
        PlayerDecisionProof.Answer(g, execute.Id);
        long randomBefore = g.Random.DrawCount;
        Kill(g, source);
        Core2026Proof.Check(p.Choice == null && g.Random.DrawCount == randomBefore && p.Opponent.BoardZone.Count == 0,
            "captured required-target spell with no legal target fizzles without a prompt or RNG");
    }
}
