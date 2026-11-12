using SabberStoneCore.Actions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class RepeatLostTargetProof
{
    internal static void Run()
    {
        var failures = new List<string>();
        foreach (string id in new[] { "FIR_954", "CATA_585", "TIME_611", "TIME_750", "TIME_702", "BE_201", "BE_252", "TIME_712", "TIME_855", "JAIL_892", "BE_854", "EDR_261", "EDR_231", "EDR_848", "END_000", "FIR_939" })
        {
            try { LostTarget(id); Console.WriteLine("PASS lost repeat target " + id); }
            catch (Exception error) { failures.Add(id + ": " + error.Message); }
        }
        Core2026Proof.Check(failures.Count == 0, string.Join("; ", failures));
    }

    private static void LostTarget(string id)
    {
        var definition = Cards.FromId(id); var power = definition.Power;
        bool bounce = id is "BE_201" or "BE_252" or "EDR_261" or "EDR_231" or "EDR_848";
        try
        {
            if (bounce) definition.Power = new Power { PowerTask = StateTaskList.Chain(power.PowerTask, new ReturnHandTask(EntityType.TARGET)) };
            var game = HeraldProof.New(CardClass.DRUID); var p = game.Player1;
            var spell = Add(p, id);
            if (id == "TIME_702") Play(game, p, "CS2_231");
            if (id == "TIME_712") Play(game, p, "GAME_005");
            if (id == "TIME_750") Add(p, "EX1_298");
            var target = Summon(id == "BE_252" ? p : p.Opponent, id == "CATA_585" ? "CS2_182" : "CS2_120");
            if (id == "CATA_585") target.Damage = 1;
            else if (!bounce) target.BaseHealth = 1;
            if (id is "TIME_611" or "TIME_855")
                for (int i = 0; i < 2; i++) Summon(p.Opponent, "CS2_200");
            spell[RepeatRules.Remaining] = 1;
            foreach (var next in new[] { game, game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) })
            {
                var owner = next.Player1; int ownDeck = owner.DeckZone.Count, enemyDeck = owner.Opponent.DeckZone.Count;
                PlayEntity(next, owner, next.IdEntityDic[spell.Id], (ICharacter)next.IdEntityDic[target.Id]);
                int choices = 0;
                while (owner.Choice != null)
                {
                    Core2026Proof.Check(++choices <= 8, "repeat choices make progress");
                    PlayerDecisionProof.Answer(next, owner.Choice.Choices[0]);
                }
                Core2026Proof.Check(next.IdEntityDic[target.Id].Zone.Type == (bounce ? Zone.HAND : Zone.GRAVEYARD), id + " first target leaves play");
                switch (id)
                {
                    case "FIR_954": Core2026Proof.Check(owner.Opponent.DeckZone.Count == enemyDeck - 1, "missing target has no owner draw"); break;
                    case "CATA_585": Core2026Proof.Check(owner.HandZone.Count(c => c.Card.Id == id) == 1, "missing target creates no excess copy"); break;
                    case "TIME_611": Core2026Proof.Check(owner.Opponent.BoardZone.Count == 2 && owner.Opponent.BoardZone.All(m => m.IsFrozen), "independent freezes continue"); break;
                    case "TIME_750": Core2026Proof.Check(owner.DeckZone.Count == ownDeck - 2, "independent minion draw runs twice"); break;
                    case "TIME_702": Core2026Proof.Check(owner.Hero.Armor == 10, "independent Armor runs twice"); break;
                    case "BE_201": Core2026Proof.Check(owner.BoardZone.Count == 2 && owner.BoardZone.All(m => m.IsFrozen && m.Card.Cost == 5), "independent frozen summons run twice"); break;
                    case "BE_252": Core2026Proof.Check(owner.BoardZone.Count == 1, "missing target supplies no sampled Cost"); break;
                    case "TIME_712": Core2026Proof.Check(owner.BoardZone.Count == 2, "independent Combo summons run twice"); break;
                    case "TIME_855": Core2026Proof.Check(owner.Opponent.BoardZone.Count == 2 && owner.Opponent.BoardZone.Sum(m => m.Damage) + owner.Opponent.Hero.Damage == 8, "both independent splash portions resolve"); break;
                    case "JAIL_892": Core2026Proof.Check(owner.DeckZone.Count == ownDeck + 4, "independent Outcast deck grants run twice"); break;
                    case "BE_854": Core2026Proof.Check(owner.BoardZone.Count == 2, "independent three-Cost summons run twice"); break;
                    case "EDR_261": Core2026Proof.Check(owner.Opponent.BoardZone.Count == 0 && owner.BoardZone.Count == 0, "missing buff target has no effect"); break;
                    case "EDR_231": Core2026Proof.Check(owner.DeckZone.Count == ownDeck - 2 && ImbueRules.Count(owner) == 2, "independent draw and Imbue run twice"); break;
                    case "EDR_848": Core2026Proof.Check(owner.HandZone.Count == 6, "independent random spell grants run twice"); break;
                    case "END_000": Core2026Proof.Check(ImbueRules.Count(owner) == 2, "independent Imbue runs twice"); break;
                    case "FIR_939": Core2026Proof.Check(owner.HandZone.Count == 2, "independent Dark Gift Discover runs twice"); break;
                }
            }
        }
        finally { definition.Power = power; }
    }
}
