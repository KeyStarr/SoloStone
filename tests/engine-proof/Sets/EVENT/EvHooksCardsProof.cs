using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class EvHooksCardsProof
{
    [GroupProof("ev-hooks-cards", ProofKind.Cards)]
    internal static void Run()
    {
        StandardSetsProof.Case("CATA_EVENT_110", Unpinned_Essences);
        StandardSetsProof.Case("TLC_EVENT_400", Unpinned_Sidequest);
        StandardSetsProof.Case("JAIL_EVENT_100", Unpinned_Suspicious);
        StandardSetsProof.Case("JAIL_EVENT_101", Unpinned_Star);
        StandardSetsProof.Case("TIME_EVENT_997", Unpinned_Reopen);
    }
    private static void Unpinned_Essences(Game g, Controller p, string id)
    {
        var black = Add(p, "CATA_EVENT_110t5"); var bronze = Add(p, "CATA_EVENT_110t4");
        var green = Add(p, "CATA_EVENT_110t6"); Add(p, "CS2_231"); var separate = Add(p, "CATA_EVENT_110t5");
        p.BaseMana = 10; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, bronze)), "middle Essence playable");
        Core2026Proof.Check(p.Hero.Armor == 12 && p.BoardZone.Single().Card.Id == "CATA_EVENT_110t6t"
            && separate.Zone == p.HandZone && black.Zone == p.GraveyardZone && green.Zone == p.GraveyardZone,
            "Unpinned_Essence connected run consumes once; barrier excludes separate Essence");
        Core2026Proof.Check(p.NumCardsPlayedThisTurn == 1 && p.UsedMana == 0, "adjoining casts free; one player play");
    }
    private static void Unpinned_Sidequest(Game g, Controller p, string id)
    {
        var quest = (Spell)Play(g, p, id);
        for (int i = 0; i < 3; i++) Play(g, p, "CS2_172");
        Core2026Proof.Check(quest.QuestProgress == 3 && p.Choice != null, "third Beast play opens crafting");
        Pick(g); Pick(g);
        var beast = p.HandZone.Single();
        Core2026Proof.Check(beast.Card.Id == "ICC_828t" && beast.Card.IsRace(Race.UNDEAD) && beast.Card.IsRace(Race.BEAST), "Zombeast identity and tribes");
        Core2026Proof.Check(beast.Card.Class == CardClass.HUNTER && beast.Card.Set == Cards.FromId("ICC_828t").Set,
            "composite retains Zombeast class/set metadata");
        Core2026Proof.Check(beast.Cost == Math.Max(0, beast.Card.Cost - 3), "reward separate -3 discount");
    }
    private static void Unpinned_Suspicious(Game g, Controller p, string id)
    {
        var source = (Minion)Play(g, p, id);
        var offers = p.Choice.Choices.Select(i => g.IdEntityDic[i]).ToArray();
        Core2026Proof.Check(offers.Length == 3 && offers.Count(e => e.Card.Id == "JAIL_EVENT_100t") == 1, "exactly one suspicious option");
        Pick(g, offers.Single(e => e.Card.Id == "JAIL_EVENT_100t").Id);
        Core2026Proof.Check(source.AttackDamage == 4 && source.Health == 3 && p.HandZone.Single().Card.Id == "JAIL_EVENT_100t", "correct pick buffs source and keeps discovered mutation");
    }
    private static void Unpinned_Star(Game g, Controller p, string id)
    {
        Play(g, p, id); var star = p.Hero.HeroPower;
        Core2026Proof.Check(star.Card.Id == "JAIL_EVENT_101hp" && star.Cost == 1, "install Star");
        p.UsedMana = 0; Core2026Proof.Check(g.Process(HeroPowerTask.Any(p)), "Star usable");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 2, "base Star damage");
        Play(g, p, id); Core2026Proof.Check(p.Hero.HeroPower.Id == star.Id && star.IsExhausted, "upgrade preserves exhausted entity");
        Summon(p, "CS2_065"); Core2026Proof.Check(!star.IsExhausted, "friendly Demon refresh");
        p.UsedMana = 0; Core2026Proof.Check(g.Process(HeroPowerTask.Any(p)), "refreshed Star usable");
        Core2026Proof.Check(p.Opponent.Hero.Damage == 5, "one permanent damage upgrade");
    }
    private static void Unpinned_Reopen(Game g, Controller p, string id)
    {
        var location = LocationProofs.Location(p); location.IsExhausted = true;
        location[GameTag.EXHAUSTED] = 1; location[GameTag.LOCATION_ACTION_COOLDOWN] = 1;
        var spell = Add(p, id); p.BaseMana = 10; p.UsedMana = 0;
        Core2026Proof.Check(g.Process(PlayCardTask.Any(p, spell, location)), "location-target spell legal");
        Core2026Proof.Check(!location.IsExhausted && location[GameTag.LOCATION_ACTION_COOLDOWN] == 0, "Reopen clears readiness only");
        location.Destroy(); Drain(g);
        Core2026Proof.Check(p.BoardZone.Count == 1 && p.BoardZone.Single().Card.Cost == 3, "location Deathrattle summons exact three");
    }
    internal static void Drain(Game g) {
        do { g.DeathProcessingAndAuraUpdate(); } while (g.SummonedMinions.Count != 0 || g.DeadMinions.Count != 0 || g.DeadBoardObjects.Count != 0);
    }
    internal static void Pick(Game g, int selected = 0)
    {
        var p = g.CurrentPlayer; var choice = p.Choice;
        var decision = g.GetPendingDecisions().Single();
        Core2026Proof.Check(choice != null && g.TryAnswer(new SabberStoneCore.Model.Decisions.DecisionAnswer(decision.Id,p.Id,
            new[] { selected == 0 ? choice.Choices[0] : selected }), out _), "choice accepted");
    }
}
