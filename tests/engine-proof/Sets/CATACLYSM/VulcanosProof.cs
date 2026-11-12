using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;

internal static class VulcanosProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_488", (g, p, id) => {
        var body = (Minion)Play(g, p, id);
        TheBlackBloodProof.Lean_Appendages(body, new[] { "CATA_488t", "CATA_488t2" }, 1, 5);
        var plumes = ColossalRules.AppendagesOf(body).ToArray();
        var friendly = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var clone = g.CloneExact(history: true); End(clone); End(g);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "random Fire generation clone future parity");
        Core2026Proof.Check(body.Health == 8 && friendly.Health == 2 && enemy.Health == 2 && plumes.All(m => m.Health == 2), "owner end-turn deals 3 to every other friendly/enemy minion only");
        Core2026Proof.Check(p.Hero.Health == 30 && p.Opponent.Hero.Health == 30 && p.HandZone.Count == 2, "heroes excluded; each damaged plume generates once");
        CheckSpells(p.HandZone.Where(m => plumes.Any(plume => plume.Id == m[GameTag.DISPLAYED_CREATOR])), plumes.Select(m => m.Id));
        int count = p.HandZone.Count; End(g);
        Core2026Proof.Check(p.HandZone.Count == count + 1 && body.Health == 8, "opponent end only draws normal turn-start card");
        count = p.HandZone.Count;
        plumes[0].Silence(); plumes[0].TakeDamage(p.Opponent.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.HandZone.Count == count, "Silence removes damage trigger");
        plumes[1].TakeDamage(p.Opponent.Hero, 2); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.HandZone.Count == count + 1 && plumes[1].Zone.Type == Zone.GRAVEYARD, "lethal damage still generates discounted spell");
        var enemyPlume = Summon(p.Opponent, "CATA_488t"); int enemyHand = p.Opponent.HandZone.Count;
        enemyPlume.TakeDamage(p.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.HandZone.Count == count + 1 && p.Opponent.HandZone.Count == enemyHand + 1, "opponent plume generates only for its controller");
        CheckSpells(p.HandZone.Where(m => plumes.Any(plume => plume.Id == m[GameTag.DISPLAYED_CREATOR])), plumes.Select(m => m.Id));
        body.Silence(); int health = friendly.Health; End(g); End(g);
        Core2026Proof.Check(friendly.Health == health, "body Silence removes end-turn damage");
        ParkBoard(p); ParkBoard(p.Opponent); body = Summon(p, id); var survivor = ColossalRules.AppendagesOf(body).First(); Kill(g, body);
        count = p.HandZone.Count; survivor.TakeDamage(p.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.HandZone.Count == count + 1, "Lean_C4 plumes retain effect after body death");
        int survivorHealth = survivor.Health; End(g); End(g);
        Core2026Proof.Check(survivor.Health == survivorHealth, "departed body no longer deals end-turn damage");
        CheckSpells(p.HandZone.Where(m => m.Card.Type == CardType.SPELL), plumes.Select(m => m.Id).Append(survivor.Id));
        foreach (var h in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(h));
        while (!p.HandZone.IsFull) Add(p, "CS2_182");
        survivor.TakeDamage(p.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.HandZone.Count == 10, "full hand burns generated spell safely");
        foreach (var h in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(h));
        ParkBoard(p); ParkBoard(p.Opponent);
        var lone = Summon(p, "CATA_488t2");
        int before = p.HandZone.Count;
        lone.TakeDamage(p.Hero, 0); g.DeathProcessingAndAuraUpdate();
        lone.HasDivineShield = true; lone.TakeDamage(p.Hero, 1); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.HandZone.Count == before, "zero and Divine Shield-prevented damage generate nothing");
        CataclysmPoolProof.WithPool(g, id, 0, () => {
            var rng = g.Random.CaptureState(); lone.TakeDamage(p.Hero, 1); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(p.HandZone.Count == before && g.Random.CaptureState().Equals(rng), "empty Fire pool creates nothing and consumes no RNG");
        });
        CataclysmPoolProof.WithPool(g, id, 1, () => {
            lone.TakeDamage(p.Hero, 1); lone.TakeDamage(p.Hero, 1); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(p.HandZone.Count == before + 2 && p.HandZone.Select(c => c.Card.Id).Distinct().Count() == 1
                && p.HandZone.Select(c => c.Id).Distinct().Count() == 2, "singleton Fire pool samples with replacement into fresh entities");
            CheckSpells(p.HandZone, new[] { lone.Id });
        });
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(Game.LoadSnapshot(g.SaveSnapshot())), "discounted generated spells survive snapshot");
        TheBlackBloodProof.Lean_FullBoard(g, p, id);
    });
    private static void CheckSpells(IEnumerable<IPlayable> hand, IEnumerable<int> creators)
    {
        foreach (var card in hand)
            Core2026Proof.Check(card.Card.Type == CardType.SPELL && card.Card[(GameTag)1635] == 2
                && card.Cost == Math.Max(0, card.Card.Cost - 3 - (card.Card.Id == "JAIL_307" && card.Controller.DeckZone.Count >= 25 ? 2 : 0)) && card[GameTag.COST] == card.Cost
                && creators.Contains(card[GameTag.DISPLAYED_CREATOR]), $"exact Fire school and permanent visible 3-mana reduction: {card.Card.Id} cost={card.Cost} tag={card[GameTag.COST]} printed={card.Card.Cost}");
    }
}
