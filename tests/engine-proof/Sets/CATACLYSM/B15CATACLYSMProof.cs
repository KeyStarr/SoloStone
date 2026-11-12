using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;
internal static class B15CATACLYSMProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_615", Unpinned_GennHandParityAndPinnedImbuedIdentity);
    private static void Unpinned_GennHandParityAndPinnedImbuedIdentity(Game g, Controller p, string id)
    {
        var even = Add(p, "CS2_182"); var odd = Add(p, "CS2_171"); var genn = (Minion)Add(p, id);
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(genn.Card.Id == id, "mixed current parity never transforms");
        B15HeroPowersProof.Replay(g, x => {
            x.IdEntityDic[even.Id].Cost = 3; x.DeathProcessingAndAuraUpdate();
            var transformed = (Minion)x.IdEntityDic[genn.Id];
            Core2026Proof.Check(transformed.Card.Id == "CATA_615t" && transformed.AttackDamage == 6 && transformed.Health == 5,
                "effective cost changes trigger exact 6/5 transform");
        });
        PlayEntity(g, p, genn);
        Core2026Proof.Check(p.Hero.HeroPower.Card.Id == "AT_132_ROGUE" && p.Hero.HeroPower.Cost == 1, "starting basic power upgraded with cost enchantment: " + p.Hero.HeroPower.Card.Id + "/" + p.Hero.HeroPower.Cost + "; base=" + p.BaseClass + "; basic=" + BasicHeroPowerRules.PowerId(p.BaseClass));
        genn.Silence(); Kill(g, genn);
        Core2026Proof.Check(p.Hero.HeroPower.Cost == 1, "completed Battlecry survives Silence/death");
        foreach (var card in p.HandZone.ToArray()) p.SetasideZone.Add(p.HandZone.Remove(card));
        var empty = (Minion)Add(p, id); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(empty.Card.Id == "CATA_615t", "Unpinned empty rest-of-hand qualifies");
        p.Hero.Card = Cards.FromId("HERO_08"); ImbueRules.Imbue(p, p.Hero);
        int imbued = p.Hero.HeroPower.Id;
        PlayEntity(g, p, empty);
        Core2026Proof.Check(p.Hero.HeroPower.Id == imbued && p.Hero.HeroPower.Card.Id == "EDR_851p",
            "PinnedRule Genn never restores basic power over Imbue");
    }
}
