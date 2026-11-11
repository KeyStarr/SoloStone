using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static SilenceKeywordsProof;

internal static class RecklessDetectiveProof
{
    private static IPlayable[] Clothes(Controller p) => p.HandZone.Where(c => c.Card.Id == "JAIL_447t").ToArray();

    internal static void Run() => StandardSetsProof.Case("JAIL_447", (g, p, id) => {
        var clothesCard = Cards.FromId("JAIL_447t");
        Core2026Proof.Check(clothesCard.Type == CardType.SPELL && clothesCard.Cost == 4 && !clothesCard.Collectible
            && Cards.FromId("JAIL_447e").Type == CardType.ENCHANTMENT, "pinned Detective's Clothes spell and enchantment");
        var enemy = Summon(p.Opponent, "CS2_120");
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.IsRush && CanAttack(source, enemy) && !CanAttack(source, p.Opponent.Hero), "printed Rush: minions only on entry turn");
        Core2026Proof.Check(Clothes(p).Length == 0, "no card on play");
        var clone = g.CloneExact(history: true);
        Kill(clone, (Minion)clone.IdEntityDic[source.Id]);
        Core2026Proof.Check(Clothes(clone.Player1).Length == 1 && Clothes(p).Length == 0 && source.Zone == p.BoardZone,
            "cloned Deathrattle rewards only the clone");
        Kill(g, source);
        var clothes = Clothes(p);
        Core2026Proof.Check(clothes.Length == 1 && clothes[0].Cost == 4 && clothes[0] is Spell && Clothes(p.Opponent).Length == 0,
            "Deathrattle gives its owner one Detective's Clothes");
        var silenced = Summon(p, id); silenced.Silence(); Kill(g, silenced);
        Core2026Proof.Check(Clothes(p).Length == 1, "Silence removes Deathrattle");
        var enemyDetective = Summon(p.Opponent, id); Kill(g, enemyDetective);
        Core2026Proof.Check(Clothes(p.Opponent).Length == 1 && Clothes(p).Length == 1, "enemy Detective rewards only its owner");
        var bounced = Summon(p, id); Generic.ReturnToHandBlock(p, bounced);
        Core2026Proof.Check(Clothes(p).Length == 1, "leaving play without dying gives nothing");
        p.HandZone.Remove(bounced); p.SetasideZone.Add(bounced);

        // Clothes target any minion, never a hero; buffed fresh minion attacks minions only.
        var friendly = Summon(p, "CS2_182");
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == clothes[0].Id).ToArray();
        Core2026Proof.Check(options.Length > 0 && options.All(t => t.Target is Minion)
            && options.Select(t => t.Target.Id).Distinct().OrderBy(x => x).SequenceEqual(new[] { enemy.Id, friendly.Id }.OrderBy(x => x)),
            "Clothes requires a minion target, either side");
        PlayEntity(g, p, clothes[0], friendly);
        Core2026Proof.Check(friendly.AttackDamage == 8 && friendly.Health == 9 && friendly.IsRush
            && friendly.AppliedEnchantments.Count(e => e.Card.Id == "JAIL_447e") == 1, "pinned JAIL_447e +4/+4 and Rush");
        Core2026Proof.Check(CanAttack(friendly, enemy) && !CanAttack(friendly, p.Opponent.Hero), "fresh buffed minion attacks minions only");
        AttackOptionsProof.Lifecycle(g, friendly, enemy, rush: true);
        var enemyClothes = Add(p, "JAIL_447t");
        PlayEntity(g, p, enemyClothes, enemy);
        Core2026Proof.Check(enemy.AttackDamage == 6 && enemy.Health == 7 && enemy.IsRush, "Clothes can buff an enemy minion");

        // Full hand burns the generated card.
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        var full = Summon(p, id);
        Kill(g, full);
        Core2026Proof.Check(p.HandZone.Count == 10 && Clothes(p).Length == 0, "full hand burns Detective's Clothes");
    });
}
