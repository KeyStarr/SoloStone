using System.Text.Json;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

// Issue #117: Shadow Word: Death showed its Shadow school in the collection (the client's own card) and not in a match (the
// engine's card, from a card file whose old rows predate spell schools), so it counted for no Shadow-spell quest. Every card the
// engine plays must carry the spell school the client's definition of it carries: the match entity is what a quest, a Kindred
// check and a school filter read, and what the client prints on the card.
internal static class CardTagParityProof
{
    [GroupProof("card-tag-parity", ProofKind.Facility)]
    internal static void Run()
    {
        // Without the client export: schools of old spells that used to be missing (tag 1635: 1 Arcane, 2 Fire, 3 Frost, 4 Nature, 5 Holy, 6 Shadow, 7 Fel).
        foreach (var (id, school) in new[] { ("EX1_622", 6), ("CS2_234", 6), ("DS1_233", 6), ("CS1_130", 5), ("CS2_029", 2), ("CS2_024", 3), ("CS2_013", 4), ("EX1_277", 1) })
            Core2026Proof.Check(Cards.FromId(id)[GameTag.SPELL_SCHOOL] == school, id + " carries its spell school " + school);
        PlayedShadowWordDeath();
        ShadowSpellQuestProgress();

        var export = ClientExport.TryCards();
        if (export == null) { ClientExport.Skip("spell school of every card against the client export"); return; }
        var wrong = new List<string>();
        int compared = 0;
        foreach (var card in Cards.All)
        {
            if (!card.Implemented || !export.TryGetValue(card.Id, out var row)) continue;
            int school = row.GetProperty("tags").TryGetProperty("1635", out var value) ? value.GetInt32() : 0;
            compared++;
            if (card[GameTag.SPELL_SCHOOL] != school) wrong.Add(card.Id + " engine " + card[GameTag.SPELL_SCHOOL] + " client " + school);
        }
        Core2026Proof.Check(compared > 5000 && wrong.Count == 0, wrong.Count + " of " + compared + " cards differ in spell school from the client: " + string.Join(", ", wrong.Take(10)));
        Console.WriteLine("PASS card-tag-parity: spell school of " + compared + " cards equals the client's");
    }

    // The match entity: a played Shadow Word: Death is a Shadow spell in the play history, which is what a quest counts.
    private static void PlayedShadowWordDeath()
    {
        var game = QuestLifecycleProof.Fixture(); var p = game.Player1;
        var target = Core2026Proof.Summon(p.Opponent, "CS2_186");   // War Golem, 7 Attack
        var swd = Core2026Proof.Add(p, "EX1_622");
        Core2026Proof.Check(swd.Card[GameTag.SPELL_SCHOOL] == 6, "the entity in hand is a Shadow spell");
        Core2026Proof.Check(!game.HistoryFacts.Query.Casts().Any(f => f.SpellSchool == 6), "no Shadow cast yet");
        Core2026Proof.PlayEntity(game, p, swd, target);
        Core2026Proof.Check(target.Zone == p.Opponent.GraveyardZone, "Shadow Word: Death destroyed the 7-Attack minion");
        Core2026Proof.Check(game.HistoryFacts.Query.Casts().Any(f => f.ControllerId == p.Id && f.SpellSchool == 6), "history records the Shadow school of the cast");
    }

    // The reporter's case: a Shadow-spell quest (Reach Equilibrium, Shadow half) counts a played Shadow Word: Death.
    private static void ShadowSpellQuestProgress()
    {
        var game = QuestLifecycleProof.Fixture(); var p = game.Player1;
        Core2026Proof.PlayEntity(game, p, Core2026Proof.Add(p, "TLC_817"));
        var shadow = p.SecretZone.ModernQuests.Single(q => q.Card.Id == "TLC_817t2");
        var holy = p.SecretZone.ModernQuests.Single(q => q.Card.Id == "TLC_817t");
        var target = Core2026Proof.Summon(p.Opponent, "CS2_186");
        Core2026Proof.PlayEntity(game, p, Core2026Proof.Add(p, "EX1_622"), target);
        game.ProcessTasks();
        Core2026Proof.Check(shadow.QuestProgress == 1 && holy.QuestProgress == 0, "Shadow Word: Death advances the Shadow-spell quest (progress " + shadow.QuestProgress + ")");
    }
}
