using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B17TransformAttackCardsProof
{
    [GroupProof("b17-transform-attack-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("DINO_414", Unpinned_Card);
    }

    internal static void Unpinned_Card(Game g, Controller p, string id)
    {
        if (id == "EDR_529")
        {
            var podling = Summon(p, id);
            Generic.ChangeEntityBlock(p, podling, Cards.FromId("hexfrog"), true);
            Core2026Proof.Check(podling.Card.Cost == 2, "Podling intercepts destination");
        }
        else if (id == "DINO_414")
        {
            var first = Summon(p, "CS2_231");
            var spell = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "one selectable minion cannot pay for Tribute");
            var second = Summon(p.Opponent, "CS2_182");
            PlayEntity(g, p, spell, first);
            Core2026Proof.Check(p.Choice?.Choices.SequenceEqual(new[] { second.Id }) == true, "second target excludes first");
            CthunRulesProof.PickTarget(g, second.Id);
            Core2026Proof.Check(first.Card.Id == second.Card.Id && first.Controller == p, "Tribute transforms first target in place");
        }
        else
        {
            Play(g, p, id);
            if (id == "JAIL_458")
            {
                Core2026Proof.Check(p.Choice?.Choices.Count == 4, "Tiny Pal offers four distinct modes");
                CthunRulesProof.Pick(g, "JAIL_458t2");
            }
            p.Hero.IsExhausted = false;
            Core2026Proof.Check(g.Process(HeroAttackTask.Any(p, p.Opponent.Hero)), "hero attack accepted");
            Core2026Proof.Check(p.Choice?.Choices.Count == 3, "after-attack opens three choices");
            CthunRulesProof.PickTarget(g, p.Choice.Choices[0]);
            Core2026Proof.Check(p.Choice == null, "after-attack continuation completes");
        }
    }
}
