using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class HideousHuskProof
{
    // data/rules-evidence/tie-break/{husk-current-bonus,lowest-health-hero-pool,husk-bonus-stacking,leech-health-transfer,lowest-health-ties}.yaml
    // A/C/D: pinned current Husk bonus/enemy heroes; owner assumptions: additive live bonus, uniform ties, max-Health transfer.
    internal static void Run() => StandardSetsProof.Case("EDR_810", Unpinned_HideousHusk);
    private static void Unpinned_HideousHusk(Game g, Controller p, string id)
    {
        var location = LocationProofs.Location(p); var left = Summon(p, "CS2_182"); var right = Summon(p, "CS2_182");
        var source = StandardSetsProof.PlayAt(g, p, id, 2);
        var leeches = p.BoardZone.Where(m => m.Card.Id == "EDR_810t").ToArray();
        Core2026Proof.Check(leeches.Length == 2 && leeches.All(m => m.AttackDamage == 0 && m.Health == 2 && m.Card.Cost == 1 && m.IsRace(Race.BEAST)), "exact 0/2 Leech tokens");
        Core2026Proof.Check(p.BoardZone.Slots.Select(o => o.Id).SequenceEqual(new[] { location.Id, left.Id, source.Id }.Concat(leeches.Select(m => m.Id)).Append(right.Id)), "living source summons right in creation order across physical slots");
        foreach (var leech in leeches) leech.Silence();
        int enemy = p.Opponent.Hero.Health; End(g); Core2026Proof.Check(p.Opponent.Hero.Health == enemy, "silenced Leeches do not drain"); End(g);
        StandardSetsProof.ParkBoard(p);
        ExerciseLeech(g, p);
        var interrupted = DeathKnightC1Proof.NewGame(g.Seed); var owner = interrupted.Player1;
        Summon(owner.Opponent, "EDR_815");
        var loc = LocationProofs.Location(owner); var l = Summon(owner, "CS2_182");
        var husk = Summon(owner, id); var r = Summon(owner, "CS2_182");
        CorpseRules.Gain(owner.Opponent, 2);
        PoolPilotProof.Resolve(interrupted, husk);
        var survivor = owner.BoardZone.Where(m => m.Card.Id == "EDR_810t").ToArray();
        Core2026Proof.Check(survivor.Length == 1 && owner.BoardZone.Slots.Select(o => o.Id)
            .SequenceEqual(new[] { loc.Id, l.Id, husk.Id, survivor[0].Id, r.Id }),
            "first summon killed during reaction: survivor remains right of source before original neighbor");
        StandardSetsProof.ParkBoard(p); DeathKnightC1Proof.FullBoard(p);
        Core2026Proof.Check(!DeathKnightC1Proof.HasPlay(p, Add(p, id)), "full mixed board rejects body");
        StandardSetsProof.ParkBoard(p); LocationProofs.Location(p);
        while (p.BoardZone.FreeSpace > 2) Summon(p, "CS2_182");
        Play(g, p, id);
        Core2026Proof.Check(p.BoardZone.SlotCount == 7 && p.BoardZone.Count(m => m.Card.Id == "EDR_810t") == 1, "only one slot after body creates one Leech");
    }

    internal static void ExerciseLeech(Game g, Controller p)
    {
        var leech = Summon(p, "EDR_810t");
        var target = Summon(p.Opponent, "CS2_182"); target.Damage = 3; // 2/5 Health.
        p.Hero.Damage = 5; p.Opponent.Hero.Armor = 7;
        int health = p.Hero.Health, max = p.Hero.BaseHealth;
        int damageFacts = g.HistoryFacts.Query.Damage().Count();
        int healFacts = g.HistoryFacts.Query.HealthChanges().Count(f => f.Reason == HistoryHealthChangeReason.Heal);
        PoolPilotProof.Resume(g, next => End(next));
        Core2026Proof.Check(target.BaseHealth == 4 && target.Health == 2 && p.Hero.BaseHealth == max + 1 && p.Hero.Health == health + 1
            && p.Opponent.Hero.Armor == 7 && target.AppliedEnchantments.Single().Card.Id == "EDR_810e", $"base drain: target={target.BaseHealth}/{target.Health}, hero={p.Hero.BaseHealth}/{p.Hero.Health} expected={max+1}/{health+1}, armor={p.Opponent.Hero.Armor}, ench={string.Join(",", target.AppliedEnchantments?.Select(e => e.Card.Id) ?? Array.Empty<string>())}");
        int after = target.BaseHealth; End(g);
        Core2026Proof.Check(target.BaseHealth == after, "opponent end does not trigger");
        Core2026Proof.Check(g.HistoryFacts.Query.Damage().Count() == damageFacts
            && g.HistoryFacts.Query.HealthChanges().Count(f => f.Reason == HistoryHealthChangeReason.Heal) == healFacts,
            "Health transfer emits no damage or healing events");
        var first = Summon(p, "EDR_810"); var second = Summon(p, "EDR_810");
        End(g);
        Core2026Proof.Check(target.BaseHealth == 1 && target.Health == 1, "two live Husks add two to single drain");
        End(g); first.Silence(); Kill(g, second); End(g);
        Core2026Proof.Check(target.Zone.Type == Zone.GRAVEYARD, "removed and silenced Husks stop bonus; final max Health reaches zero");
        End(g); p.Opponent.Hero.Damage = p.Opponent.Hero.BaseHealth - 1;
        int armor = p.Opponent.Hero.Armor, before = p.Hero.BaseHealth;
        End(g);
        Core2026Proof.Check(p.Opponent.Hero.Armor == armor && p.Hero.BaseHealth == before + 1, "lowest enemy hero eligible; Armor ignored");
        End(g);
        target = Summon(p.Opponent, "CS2_182"); target.Damage = 3; int beforeSilence = target.BaseHealth;
        leech.Silence(); End(g);
        Core2026Proof.Check(target.BaseHealth == beforeSilence, "Leech Silence removes trigger"); End(g);
        var departed = Summon(p, "EDR_810t"); Kill(g, departed); int beforeLeaving = p.Hero.BaseHealth;
        End(g); Core2026Proof.Check(p.Hero.BaseHealth == beforeLeaving, "removed Leech has no lingering trigger"); End(g);
        foreach (bool reverse in new[] { false, true })
        {
            var ties = DeathKnightC1Proof.NewGame(g.Seed); var q = ties.Player1;
            Summon(q, "EDR_810t");
            var order = new[] { "CS2_182", "CS2_200" };
            if (reverse) Array.Reverse(order);
            var victims = order.Select(cardId => Summon(q.Opponent, cardId)).Cast<Character>()
                .Append(q.Opponent.Hero).ToArray();
            foreach (var victim in victims) victim.Damage = victim.BaseHealth - 1;
            int[] maximums = victims.Select(m => m.BaseHealth).ToArray();
            // RNG v1's first Next(3) at seeds 1, 2, 3 selects first minion, second minion, hero.
            ties.Random.SetSeed(g.Seed);
            PoolPilotProof.Resume(ties, next => End(next));
            Core2026Proof.Check(victims.Select(m => m.BaseHealth).SequenceEqual(maximums.Select((value, index) =>
                value - (index == (int)g.Seed - 1 ? 1 : 0))) && q.Hero.BaseHealth == 31,
                $"seed {g.Seed}, reversed={reverse}: exact tied hero/minion drained, other Health unchanged");
        }
    }
}
