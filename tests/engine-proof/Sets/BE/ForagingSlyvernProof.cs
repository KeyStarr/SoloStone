using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class ForagingSlyvernProof
{
    internal static void Run() => StandardSetsProof.Case("BE_859", (g, p, id) => {
        var source = (Minion)Play(g, p, id); Core2026Proof.Check(source.HasTaunt, "printed Taunt"); int before = p.HandZone.Count;
        source.AttackDamage = 4; Kill(g, source); Core2026Proof.Check(p.HandZone.Count == before + 4, "Deathrattle draws buffed Attack");
        source = Summon(p, id); source.AttackDamage = 0; before = p.HandZone.Count; Kill(g, source); Core2026Proof.Check(p.HandZone.Count == before, "zero Attack draws none");
        while (p.HandZone.Count < 9) Add(p, "CS2_182"); source = Summon(p, id); int deck = p.DeckZone.Count; Kill(g, source);
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 2, "hand cap burns excess draw");
        foreach (var card in p.DeckZone.ToArray()) { p.DeckZone.Remove(card); p.SetasideZone.Add(card); }
        source = Summon(p, id); source.AttackDamage = 4; int health = p.Hero.Health; Kill(g, source);
        Core2026Proof.Check(p.Hero.Fatigue == 4 && p.Hero.Health == health - 10, "four-Attack Deathrattle attempts all draws: 1+2+3+4 fatigue");
    });
}
