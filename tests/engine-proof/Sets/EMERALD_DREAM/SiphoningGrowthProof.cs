using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;
using static StandardSetsProof;
using static EmeraldDreamBatch3Proof;
using static EmeraldDreamBatch3Proof;

internal static class SiphoningGrowthProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_531", (g, p, id) => {
        var spell = Add(p, id); var enemy = Summon(p.Opponent, "CS2_182");
        Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "cannot play without friendly minion");
        var draw = InDeck(p, "EDR_486");
        var friendly = Summon(p, "EDR_485"); int hand = p.HandZone.Count;
        var targets = p.Options().Where(t => t.Source?.Id == spell.Id).ToArray();
        Core2026Proof.Check(targets.Length == 1 && targets[0].Target == friendly, "friendly minion only; heroes and enemies excluded");
        PlayEntity(g, p, spell, friendly);
        Core2026Proof.Check(friendly.Zone == p.GraveyardZone && p.Hero.Armor == 8 && enemy.Zone == p.Opponent.BoardZone, "destroy friendly minion and gain eight armor");
        Core2026Proof.Check(p.HandZone.Count == hand && draw.Zone == p.HandZone, "destroy runs target Deathrattle and draws eligible minion");
        friendly = Summon(p, "CS2_182"); friendly.HasDivineShield = true;
        Play(g, p, id, friendly);
        Core2026Proof.Check(friendly.Zone == p.GraveyardZone && p.Hero.Armor == 16, "destruction bypasses shield and armor stacks");
    });
}
