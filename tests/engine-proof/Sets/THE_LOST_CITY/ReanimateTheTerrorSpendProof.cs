using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Decisions;
using SabberStoneCore.Model.Entities;

// Issue #82: Reanimate the Terror (TLC_433) completes on the 15th Corpse spent, whatever spends it: Tyrax, Bone Terror goes
// to hand and the quest leaves play. Each case spends exactly the last Corpses through one card's own spending path.
internal static class ReanimateTheTerrorSpendProof
{
    [GroupProof("reanimate-terror-spends", ProofKind.Cards)]
    internal static void Run()
    {
        // dbfId, Corpses that path spends, how to make it spend.
        var paths = new (int Dbf, int Spend, Action<Game, Controller, string> Run)[] {
            (127535, 8, (g, p, id) => Core2026Proof.Play(g, p, id)),                                          // Corpse Farm: up to 8
            (111323, 5, (g, p, id) => { Core2026Proof.Summon(p.Opponent, "CS2_182"); Core2026Proof.Play(g, p, id); }), // Marrow Manipulator: up to 5
            (78352, 5, (g, p, id) => Core2026Proof.Play(g, p, id)),                                           // Army of the Dead: up to 5
            (117028, 4, (g, p, id) => Core2026Proof.Play(g, p, id)),                                          // Tomb Guardians: 4
            (120876, 4, (g, p, id) => { Core2026Proof.Summon(p, id); Core2026Proof.End(g); }),                // Malignant Horror: 4 at end of turn
            (80645, 5, (g, p, id) => Core2026Proof.Play(g, p, id)),                                           // Grave Strength: 5
            (78353, 1, (g, p, id) => { Core2026Proof.Summon(p, id); Core2026Proof.End(g); }),                 // Battlefield Necromancer: 1 at end of turn
            (128334, 6, (g, p, id) => Core2026Proof.Play(g, p, id)),                                          // Boneguard Commander: up to 6
            (111320, 1, (g, p, id) => { Core2026Proof.Play(g, p, id); Answer(g, p); }),                       // Hematurge: 1, then Discover
            (130395, 4, (g, p, id) => Core2026Proof.Play(g, p, id)),                                          // Bloodfiend: up to 4
            (118558, 3, (g, p, id) => { Core2026Proof.Summon(p, id); Core2026Proof.Kill(g, Core2026Proof.Summon(p, "CS2_182")); }), // Hollow Direhorn: 3 on a death
            (117430, 5, (g, p, id) => Core2026Proof.Play(g, p, id)),                                          // Paleomancy: 5, keep all
            (121060, 4, (g, p, id) => Core2026Proof.Play(g, p, id)),                                          // Bygone Echoes: 4
            (114303, 2, (g, p, id) => { Core2026Proof.Play(g, p, id); Answer(g, p); }),                       // Rite of Atrocity: 2 on the gifted pick
            (114306, 2, (g, p, id) => { Core2026Proof.Summon(p, id); Core2026Proof.Summon(p.Opponent, "CS2_182"); }), // Corpse Flower: 2 on an enemy summon
            (115748, 10, (g, p, id) => { Core2026Proof.Play(g, p, id); Answer(g, p); }),                      // Volcoross: 10
            (117434, 5, (g, p, id) => PayCorpses(g, p, id)),                                                  // Reanimated Pterrordax: costs 5 Corpses instead of Mana
            (122760, 8, (g, p, id) => { Core2026Proof.Kill(g, Core2026Proof.Summon(p, "CS2_182")); Core2026Proof.Play(g, p, id); Answer(g, p); }), // Chow Down: 8
        };
        foreach (var (dbf, spend, run) in paths)
        {
            string id = Cards.FromAssetId(dbf).Id;
            var game = DeathKnightC1Proof.NewGame(1); var p = game.Player1;
            var quest = (Spell)Core2026Proof.Play(game, p, "TLC_433");
            CorpseRules.Gain(p, 15);
            Core2026Proof.Check(CorpseRules.TrySpend(p, 15 - spend) && quest.QuestProgress == 15 - spend, id + ": quest counts earlier spends");
            run(game, p, id);
            Core2026Proof.Check(p[GameTag.CORPSES_SPENT_THIS_GAME] == 15, id + " spends exactly the last " + spend);
            Core2026Proof.Check(quest.Zone == p.GraveyardZone && !p.SecretZone.ModernQuests.Contains(quest), id + ": quest completes and leaves play");
            Core2026Proof.Check(p.HandZone.Count(c => c.Card.Id == "TLC_433t") == 1 && p.PendingQuestRewards.Count == 0, id + ": Tyrax in hand");
        }
    }

    // A Corpse-cost card is paid through CostRules, not by its own effect; Mana stays untouched.
    private static void PayCorpses(Game game, Controller p, string id)
    {
        var card = Core2026Proof.Add(p, id);
        p.BaseMana = 10; p.UsedMana = 0;
        var play = p.Options().FirstOrDefault(o => o.PlayerTaskType == SabberStoneCore.Tasks.PlayerTasks.PlayerTaskType.PLAY_CARD && o.Source?.Id == card.Id);
        Core2026Proof.Check(play != null && game.Process(play) && p.UsedMana == 0, id + " played for Corpses, not Mana");
    }

    private static void Answer(Game game, Controller p)
    {
        if (p.Choice == null) return;
        var decision = game.GetPendingDecisions().Single();
        Core2026Proof.Check(game.TryAnswer(new DecisionAnswer(decision.Id, decision.ControllerId, new[] { p.Choice.Choices[0] }), out var error), "answer: " + error);
    }
}
