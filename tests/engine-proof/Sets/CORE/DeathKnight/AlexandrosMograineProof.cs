using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class AlexandrosMograineProof
{
    internal static void Run() => DeathKnightC2Proof.Case("CORE_RLK_706", (g, p, id) => {
        DeathKnightC2Proof.Dependency("RLK_706e3", 87824);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(p.AppliedEnchantments.Count(e => e.Card.Id == "RLK_706e3") == 1
            && p.Opponent.Hero.Damage == 0, "exact player-bound enchantment; no immediate damage");
        new ControlTask(EntityType.TARGET).Process(g, p.Opponent, p.Opponent.Hero, source);
        source.Silence(); g.DeathProcessingAndAuraUpdate(); Kill(g, source);
        DeathKnightC2Proof.Continue(g, copy => {
            End(copy);
            Core2026Proof.Check(copy.Player2.Hero.Damage == 3 && copy.Player1.Hero.Damage == 0, "persists after control/silence/death on original owner's end turn");
            End(copy);
            Core2026Proof.Check(copy.Player2.Hero.Damage == 3, "opponent end turn does not trigger");
            var next = (Minion)Play(copy, copy.Player1, id); Kill(copy, next);
            End(copy);
            Core2026Proof.Check(copy.Player2.Hero.Damage == 9, "multiple Battlecries stack independently");
        });
        End(g); Summon(p, id); End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 15, "summoning does not add persistent effect");
        End(g); p.Opponent.Hero.IsImmune = true; End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 15, "Immune prevents persistent damage");
        End(g); p.Opponent.Hero.IsImmune = false;
        Play(g, p, "CORE_EX1_323");
        End(g);
        Core2026Proof.Check(p.Opponent.Hero.Damage == 21, "controller attachment survives hero change");
    });
}
