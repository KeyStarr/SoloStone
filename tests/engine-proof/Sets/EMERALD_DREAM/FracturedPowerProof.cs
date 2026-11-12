using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;
using static StandardSetsProof;

internal static class FracturedPowerProof
{
    internal static void Run() => StandardSetsProof.Case("EDR_483", (g, p, id) => {
        var source = Add(p, id);
        p.BaseMana = 5; p.UsedMana = 0;
        foreach (bool automatic in new[] { false, true })
        {
            var freeGame = g.Clone(); var owner = freeGame.Player1;
            var spell = owner.HandZone.Single(x => x.Id == source.Id);
            if (automatic)
            {
                owner.DeckZone.Add(owner.HandZone.Remove(spell));
                new PlayTask(PlayType.SPELL, true).Process(freeGame, owner, owner.Hero, null,
                    new TaskStack { Playables = new List<IPlayable> { spell } });
            }
            else
            {
                spell.Cost = 0;
                Core2026Proof.Check(freeGame.Process(PlayCardTask.Any(owner, spell)), "zero-cost fractured play");
            }
            Core2026Proof.Check(spell.Zone == owner.GraveyardZone && owner.AppliedEnchantments.Any(x => x.Card.Id == "EDR_483e"), "free cast resolves and installs timer");
            Core2026Proof.Check(owner.BaseMana == 4 && owner.UsedMana == 0 && owner.RemainingMana == 4,
                automatic ? "automatic cast destroys full crystal without negative used mana" : "zero-cost cast destroys full crystal without negative used mana");
        }
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, source)), "fractured play");
        Core2026Proof.Check(p.BaseMana == 4 && p.UsedMana == 1 && p.RemainingMana == 3, "destroys one spent crystal immediately");
        End(g); Core2026Proof.Check(p.BaseMana == 4, "opponent turn not counted"); End(g);
        Core2026Proof.Check(p.BaseMana == 5, "first own start gives normal crystal only");
        var clone = g.Clone(); End(clone); End(clone);
        Core2026Proof.Check(clone.Player1.BaseMana == 8 && p.BaseMana == 5, "second own start grants two full crystals; clone independent");
        End(g); End(g);
        Core2026Proof.Check(p.BaseMana == 8 && p.RemainingMana == 8, "delayed full crystals usable at second start");
        Core2026Proof.Check(!p.AppliedEnchantments.Any(x => x.Card.Id == "EDR_483e"), "timer removed");
        End(g); End(g); Core2026Proof.Check(p.BaseMana == 9, "grant fires once");
    });
}
