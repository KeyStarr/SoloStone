using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static TransportInvariants.SelfChecks;

namespace TransportInvariants;
internal static class HeraldChecks
{
    internal static void Run()
    {
        HeraldCandidateChecks.Run();
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), "warrior", "mage", 0);
        session = new(match); match.Start(); match.SendChoices(match.HumanChoiceId, new());
        var g = match.Game; var p = g.Player1;
        g.PowerHistory.Last.Clear(); p.BaseMana = 10;
        var source = Generic.DrawCard(p, Cards.FromId("CATA_525")); session.Flush();
        Check(session.Client.Entities[source.Id].Get(HeraldRules.Profile) == (int)CardClass.WARRIOR
            && session.Client.Entities[p.Id].Get(HeraldRules.Profile) == (int)CardClass.WARRIOR,
            "Herald hand tooltip must use controller profile, not printed source class");
        for (int count = 0; count < 5; count++)
        {
            g.PowerHistory.Last.Clear();
            int id = new SabberStoneCore.EffectSystem.EffectContext(g, p.Id, source.Id).Herald();
            g.DeathProcessingAndAuraUpdate(); session.Flush();
            var m = (Minion)g.IdEntityDic[id]; var shown = session.Client.Entities[id];
            int factor = new[] { 1, 1, 2, 2, 4 }[count];
            Check(shown.CardId == "CATA_580t" && shown.Get(GameTag.ATK) == 2 * factor
                && shown.Get(GameTag.HEALTH) == factor && shown.Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 2 * factor,
                "Herald wire publishes class identity, actual doubling and effect magnitude");
            Check(HeraldRules.Count(p) == count + 1 && session.Client.Entities.Values.All(e => e.Get((GameTag)4327) == 0),
                "unqualified native amount tag 4327 stays blocked; engine progression still advances");
        }
        var soldier = p.BoardZone.Last();
        g.PowerHistory.Last.Clear(); soldier.Silence(); g.DeathProcessingAndAuraUpdate(); session.Flush();
        Check(session.Client.Entities[soldier.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 0,
            "Silence publishes explicit zero magnitude");
        g.PowerHistory.Last.Clear(); soldier = (Minion)Generic.ChangeEntityBlock(p, soldier, Cards.FromId("CS2_182"), true); session.Flush();
        Check(session.Client.Entities[soldier.Id].Get(HeraldRules.Profile) == 0
            && session.Client.Entities[soldier.Id].Get(HeraldRules.Progress) == 0
            && session.Client.Entities[soldier.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 0
            && session.Client.Entities[soldier.Id].Get(HeraldRules.RealizedFactor) == 0, "transform clears old Herald text state");
        g.PowerHistory.Last.Clear(); p.UsedMana = 0;
        var infusion = Generic.DrawCard(p, Cards.FromId("CATA_530")); session.Flush();
        session.Process(PlayCardTask.Any(p, infusion));
        int enchantmentId = p.Hero.AppliedEnchantments.Single(e => e.Card.Id == "CATA_530e").Id;
        Check(session.Client.Entities[p.Hero.Id].Get(GameTag.LIFESTEAL) == 1, "hero Lifesteal is visible");
        session.Process(EndTurnTask.Any(p));
        Check(session.Client.Entities[p.Hero.Id].Get(GameTag.LIFESTEAL) == 0
            && session.Client.Entities[enchantmentId].Get(GameTag.ZONE) == (int)SabberStoneCore.Enums.Zone.REMOVEDFROMGAME,
            "hero Lifesteal and enchantment both expire on wire");
        g.PowerHistory.Last.Clear();
        var hidden = SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent, Cards.FromId("CATA_525"), zone: p.Opponent.DeckZone);
        Generic.Draw(p.Opponent, hidden); session.Flush();
        Check(session.Client.Entities[hidden.Id].CardId == ""
            && session.Client.Entities[hidden.Id].Get(HeraldRules.Profile) == 0, "opponent hand profile stays private: card=" + session.Client.Entities[hidden.Id].CardId
                + "; profile=" + session.Client.Entities[hidden.Id].Get(HeraldRules.Profile)
                + "; zone=" + hidden.Zone.Type);
        CrossClass("priest", "test-standard-batch-2-rogue", new[] { ("CATA_785", CardClass.ROGUE), ("CATA_525", (CardClass)14), ("CATA_722", CardClass.ROGUE) }, CardClass.ROGUE);
        CrossClass("priest", "mage", new[] { ("CATA_785", CardClass.ROGUE), ("CATA_722", CardClass.INVALID) }, CardClass.INVALID);
        Console.WriteLine("PASS transport Herald: class substitution, scaled stats/magnitude, blocked unqualified amount, Silence/transform zero clears, hidden hand");
    }

    // Contract: the entity carries its own source-aware class; the CONTROLLER (the only tag the native text builder reads)
    // carries the source-independent class (own, else opponent). Neither is persisted state: it follows the live game.
    private static void CrossClass(string own, string opponent, (string Id, CardClass Expected)[] cards, CardClass controllerExpected)
    {
        NativeSession session = null;
        var match = new NativeMatch((id, packet) => session.Receive(id, packet), own, opponent, 0);
        session = new(match); match.Start(); match.SendChoices(match.HumanChoiceId, new());
        var g = match.Game; var p = g.Player1; g.PowerHistory.Last.Clear();
        foreach (var (cardId, expected) in cards)
        {
            var held = Generic.DrawCard(p, Cards.FromId(cardId)); session.Flush();
            Check(session.Client.Entities[held.Id].Get(HeraldRules.Profile) == (int)expected
                && session.Client.Entities[p.Id].Get(HeraldRules.Profile) == (int)controllerExpected,
                $"cross-class {own} vs {opponent}: {cardId} entity profile {expected}, controller profile {controllerExpected}; got entity="
                + session.Client.Entities[held.Id].Get(HeraldRules.Profile) + " controller=" + session.Client.Entities[p.Id].Get(HeraldRules.Profile));
        }
        p.BaseMana = 10; p.UsedMana = 0;
        var rite = p.HandZone.First(c => c.Card.Id == "CATA_785");
        session.Process(PlayCardTask.Any(p, rite)); session.Flush();
        Check(rite.Zone.Type == SabberStoneCore.Enums.Zone.GRAVEYARD, "cross-class Herald card is accepted over the wire");
        Check(session.Client.Entities.Values.Any(e => e.CardId == "CATA_158t"), "Sinestra reaches the client for a Priest");
        Check(p[HeraldRules.Profile] == 0, "fallback Herald does not persist a profile");
    }
}
