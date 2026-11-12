using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FreyaEmeraldArchitectProof
{
    internal static void Run() => StandardSetsProof.Case("BE_856", (g, p, id) => {
        var beast = (Minion)Add(p, "CS2_120"); var plain = (Minion)Add(p, "CS2_182");
        var deckBeast = (Minion)Entity.FromCard(p, Cards.FromId("CS2_120")); p.DeckZone.Add(deckBeast);
        var boardBeast = Summon(p, "CS2_120"); var enemy = (Minion)Add(p.Opponent, "CS2_120");
        beast.AttackDamage = 3; beast.BaseHealth = 5;
        Play(g, p, id); Core2026Proof.Check(beast.AttackDamage == 6 && beast.Health == 10 && deckBeast.AttackDamage == 4 && deckBeast.Health == 6, "doubles current Beast stats in hand and deck");
        Core2026Proof.Check(plain.AttackDamage == 4 && plain.Health == 5 && boardBeast.AttackDamage == 2 && enemy.AttackDamage == 2, "nonbeast, board and opponent unchanged");
        Play(g, p, id); Core2026Proof.Check(beast.AttackDamage == 12 && beast.Health == 20, "repeated buffs stack");
        PlayEntity(g, p, beast); Play(g, p, "BE_228", beast); Core2026Proof.Check(beast.AttackDamage == 2 && beast.Health == 3, "silence clears doubling");
    });
}
