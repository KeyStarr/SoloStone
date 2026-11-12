using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class MossbindingProof
{
    internal static void Run() => StandardSetsProof.Case("CATA_135", (g, p, id) => {
        var spell = Add(p, id); p.BaseMana = 10; p.UsedMana = 0;
        int spent = p.TotalManaSpentThisGame;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, spell)), "Mossbinding play accepted");
        var tokens = p.BoardZone.ToArray();
        Core2026Proof.Check(tokens.Length == 2 && tokens.All(m => m.Card.Id == "CATA_135t" && m.AttackDamage == 9 && m.Health == 10), "two pinned Golems buffed by eight remaining Mana");
        Core2026Proof.Check(tokens[0].Id < tokens[1].Id && p.RemainingMana == 0 && p.TotalManaSpentThisGame == spent + 10, "creation order and all Mana recorded as spent");
        tokens[0].Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(tokens[0].AttackDamage == 1 && tokens[0].Health == 2, "Golem buff removable by Silence");
        End(g); End(g); Core2026Proof.Check(tokens[1].AttackDamage == 9 && tokens[1].Health == 10, "buff persists across turns");
        ClearBoard(g, p); spell = Add(p, id); p.BaseMana = 2; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, spell)), "zero-extra-Mana cast");
        Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.AttackDamage == 1 && m.Health == 2), "zero remaining Mana leaves base stats");
        ClearBoard(g, p); for (int i = 0; i < 6; i++) Summon(p, "CS2_182");
        spell = Add(p, id); p.BaseMana = 10; p.UsedMana = 5; p.TemporaryMana = 2; p.OverloadLocked = 1;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, spell)), "one-slot temporary-Mana cast");
        Core2026Proof.Check(p.BoardZone.Count == 7 && p.BoardZone.Last().Card.Id == "CATA_135t" && p.BoardZone.Last().AttackDamage == 5 && p.BoardZone.Last().Health == 6 && p.RemainingMana == 0 && p.TemporaryMana == 0 && p.OverloadLocked == 1, "one slot; temporary Mana consumed and locked crystal excluded");
        spell = Add(p, id); p.OverloadLocked = 0; p.BaseMana = 10; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, spell)) && p.BoardZone.Count == 7 && p.RemainingMana == 0, "full board still spends remaining Mana");
    });
}
