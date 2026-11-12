using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class PoisonBreathProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_EDR_002", (g, p, id) => {
        var card = Cards.FromId(id);
        Core2026Proof.Check(card.Type == CardType.SPELL && card.Class == CardClass.DEATHKNIGHT && card.Cost == 2,
            "Death Knight spell costs 2 without a rune requirement");
        var ordinary = Summon(p, "CS2_182");
        var enemy = Summon(p.Opponent, "RLK_708");
        var spell = Add(p, id);
        Core2026Proof.Check(!p.Options().Any(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id),
            "cannot play without a friendly Undead; enemy Undead and friendly non-Undead are illegal");
        var friendly = Summon(p, "RLK_708");
        var other = Summon(p, "RLK_708");
        Core2026Proof.Check(friendly.IsRace(Race.UNDEAD) && friendly.IsRace(Race.DRAENEI) && friendly.Card.Implemented,
            "registered transport fixture is dual-type Undead");
        p.BaseMana = 2; p.UsedMana = 0;
        var options = p.Options().Where(t => t.PlayerTaskType == PlayerTaskType.PLAY_CARD && t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(options.Length == 2 && options.All(t => t.Target?.Id == friendly.Id || t.Target?.Id == other.Id),
            "only friendly Undead legal; enemy, non-Undead, heroes and no-target excluded");
        Core2026Proof.Check(g.Process(options.Single(t => t.Target?.Id == friendly.Id)) && p.UsedMana == 2,
            "plays with exactly 2 mana on harness hero without runes");
        Core2026Proof.Check(friendly.Poisonous && friendly.AttackDamage == 2 && friendly.Health == 2
            && friendly.AppliedEnchantments.Single().Card.Id == "CORE_EDR_002e"
            && Cards.FromId("CORE_EDR_002e").AssetId == 117309 && Cards.FromId("CORE_EDR_002e").Implemented
            && !other.Poisonous && !ordinary.Poisonous && !enemy.Poisonous,
            "exact Deathly Poison enchantment grants only chosen Undead Poisonous without changing stats");
        var clone = g.CloneExact(history: true);
        var copied = (Minion)clone.IdEntityDic[friendly.Id];
        copied.Silence();
        Core2026Proof.Check(!copied.Poisonous && copied.AppliedEnchantments.Count == 0 && friendly.Poisonous,
            "clone Silence removes enchantment and Poisonous without changing original");
        End(g); End(g);
        Core2026Proof.Check(friendly.Poisonous, "Poisonous persists across turns after spell leaves play");
        friendly.Silence();
        Core2026Proof.Check(!friendly.Poisonous && friendly.AppliedEnchantments.Count == 0,
            "Silence removes granted Poisonous and exact enchantment");
        while (p.BoardZone.Count < 7) Summon(p, "CS2_182");
        Play(g, p, id, friendly);
        Core2026Proof.Check(p.BoardZone.Count == 7 && friendly.Poisonous, "spell remains playable on full board");
        var victim = Summon(p.Opponent, "CS2_200");
        Core2026Proof.Check(victim.Health > friendly.AttackDamage, "combat victim survives ordinary attack damage");
        Attack(g, friendly, victim);
        Core2026Proof.Check(victim.Zone.Type == Zone.GRAVEYARD, "granted Poisonous destroys damaged high-Health minion");
    });
}
