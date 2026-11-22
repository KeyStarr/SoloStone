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
        // #192: an opponent's Soldier in hand stays private when its owner's count (and so its text variant) moves.
        var enemySoldier = Generic.DrawCard(p.Opponent, Cards.FromId("CATA_580t")); session.Flush();
        g.PowerHistory.Last.Clear();
        new SabberStoneCore.EffectSystem.EffectContext(g, p.Opponent.Id, p.Opponent.Hero.Id).Herald();
        g.DeathProcessingAndAuraUpdate(); session.Flush();
        Check(HeraldRules.Count(p.Opponent) == 1 && session.Client.Entities[enemySoldier.Id].CardId == ""
            && session.Client.Entities[enemySoldier.Id].Get(TextVariant) == 0 && session.Client.Entities[enemySoldier.Id].Def.Count == 0,
            "opponent hand Soldier: no identity, text variant or definition reaches the client");
        // The human's own Soldier in hand shows the variant too, from its first SHOW and after every Herald.
        var heldSoldier = Generic.DrawCard(p, Cards.FromId("CATA_580t")); session.Flush();
        Check(session.Client.Entities[heldSoldier.Id].CardId == "CATA_580t" && session.Client.Entities[heldSoldier.Id].Get(TextVariant) == 0, "hand Soldier at count 0: variant 0");
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
            // Lean (from the card text): 955 = count >= 4 ? 2 : count % 2, on every live Soldier.
            int variant = new[] { 1, 0, 1, 2, 2 }[count];
            foreach (var live in p.BoardZone.Where(e => e.Card.Id == "CATA_580t"))
                Check(session.Client.Entities[live.Id].Get(TextVariant) == variant && Shows(session, live, live[HeraldRules.RealizedFactor]),
                    $"#192 after {count + 1} Heralds: Soldier {live.Id} shows text variant {variant} and its scaled printed stats as its definition; client 955="
                    + session.Client.Entities[live.Id].Get(TextVariant) + " def ATK=" + session.Client.Entities[live.Id].Def.GetValueOrDefault((int)GameTag.ATK));
            Check(session.Client.Entities[heldSoldier.Id].Get(TextVariant) == variant, $"#192 after {count + 1} Heralds: the hand Soldier shows variant {variant}");
        }
        var top = p.BoardZone.Last(e => e.Card.Id == "CATA_580t");
        g.PowerHistory.Last.Clear();
        new SabberStoneCore.Tasks.SimpleTasks.AddEnchantmentTask("CS2_009e", SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET).Process(g, p, p.Hero, top);
        g.DeathProcessingAndAuraUpdate(); session.Flush();
        Check(Shows(session, top, 4) && session.Client.Entities[top.Id].Get(GameTag.ATK) > session.Client.Entities[top.Id].Def[(int)GameTag.ATK],
            "#192: a real buff on a tier 3 Soldier stays above its definition (green)");
        var bounced = p.BoardZone.First(e => e.Card.Id == "CATA_580t" && e[HeraldRules.RealizedFactor] == 2);
        g.PowerHistory.Last.Clear();
        new SabberStoneCore.Tasks.SimpleTasks.ReturnHandTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET).Process(g, p, p.Hero, bounced, new SabberStoneCore.Model.TaskStack());
        g.DeathProcessingAndAuraUpdate(); session.Flush();
        Check(bounced.Zone == p.HandZone && Shows(session, bounced, 1) && session.Client.Entities[bounced.Id].Get(TextVariant) == 2,
            "#192: a tier 2 Soldier returned to hand shows its printed definition again and keeps the current variant");
        p.UsedMana = 0;
        session.Process(PlayCardTask.Any(p, bounced)); session.Flush();
        Check(bounced.Zone == p.BoardZone && bounced[HeraldRules.RealizedFactor] == 4 && Shows(session, bounced, 4)
            && session.Client.Entities[bounced.Id].Get(TextVariant) == 2, "#192: replayed from hand it shows the current tier as its own stats");
        var soldier = p.BoardZone.Last();
        g.PowerHistory.Last.Clear(); soldier.Silence(); g.DeathProcessingAndAuraUpdate(); session.Flush();
        Check(session.Client.Entities[soldier.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 0,
            "Silence publishes explicit zero magnitude");
        Check(soldier[HeraldRules.RealizedFactor] == 0 && Shows(session, soldier, 1),
            "#192: Silence resets an upgraded Soldier to its printed stats and printed definition");
        g.PowerHistory.Last.Clear(); soldier = (Minion)Generic.ChangeEntityBlock(p, soldier, Cards.FromId("CS2_182"), true); session.Flush();
        Check(session.Client.Entities[soldier.Id].Get(HeraldRules.Profile) == 0
            && session.Client.Entities[soldier.Id].Get(HeraldRules.Progress) == 0
            && session.Client.Entities[soldier.Id].Get(GameTag.TAG_SCRIPT_DATA_NUM_1) == 0
            && session.Client.Entities[soldier.Id].Get(HeraldRules.RealizedFactor) == 0, "transform clears old Herald text state");
        Check(session.Client.Entities[soldier.Id].Def.Count == 0 && session.Client.Entities[soldier.Id].Get(TextVariant) == 0,
            "#192: the transform reloads the new card's printed definition and drops the Soldier's text variant");
        // Side change: the variant follows the new controller's count (the opponent Heralded once).
        var stolen = p.BoardZone.First(e => e.Card.Id == "CATA_580t");
        g.PowerHistory.Last.Clear();
        new SabberStoneCore.Tasks.SimpleTasks.ControlTask(SabberStoneCore.Tasks.SimpleTasks.EntityType.TARGET).Process(g, p.Opponent, p.Opponent.Hero, stolen, new SabberStoneCore.Model.TaskStack());
        g.DeathProcessingAndAuraUpdate(); session.Flush();
        Check(stolen.Controller == p.Opponent && session.Client.Entities[stolen.Id].Get(TextVariant) == 1, "#192: a stolen Soldier shows its new controller's variant");
        // Herald Colossal appendages: the opponent's at count 1 (factor 1, variant 1, then 0 after its next Herald); then the
        // human's at count 5 (factor 4, white 4x stats, final text); Silence resets one to the printed definition.
        g.PowerHistory.Last.Clear();
        var theirs = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p.Opponent, Cards.FromId("CATA_150"));
        Generic.SummonBlock(g, theirs, -1, null); g.DeathProcessingAndAuraUpdate(); session.Flush();
        var theirArms = ColossalRules.AppendagesOf(theirs).ToArray();
        Check(theirArms.Length == 2 && theirArms.All(a => session.Client.Entities[a.Id].Get(TextVariant) == 1 && Shows(session, a, 1)), "#192: opponent appendages at count 1");
        g.PowerHistory.Last.Clear();
        new SabberStoneCore.EffectSystem.EffectContext(g, p.Opponent.Id, p.Opponent.Hero.Id).Herald();
        g.DeathProcessingAndAuraUpdate(); session.Flush();
        Check(theirArms.All(a => session.Client.Entities[a.Id].Get(TextVariant) == 0 && Shows(session, a, 1)), "#192: a Herald updates live appendages' text, not their stats");
        foreach (var m in p.BoardZone.ToArray()) m.ToBeDestroyed = true;
        g.DeathProcessingAndAuraUpdate();
        g.PowerHistory.Last.Clear();
        var ours = (Minion)SabberStoneCore.Model.Entities.Entity.FromCard(p, Cards.FromId("CATA_150"));
        Generic.SummonBlock(g, ours, -1, null); g.DeathProcessingAndAuraUpdate(); session.Flush();
        var arms = ColossalRules.AppendagesOf(ours).ToArray();
        Check(arms.Length == 2 && arms.All(a => a[ColossalRules.RealizedFactor] == 4 && session.Client.Entities[a.Id].Get(TextVariant) == 2 && Shows(session, a, 4)),
            "#192: tier 3 appendages show final text and their 4x printed stats as their own");
        g.PowerHistory.Last.Clear(); arms[0].Silence(); g.DeathProcessingAndAuraUpdate(); session.Flush();
        Check(Shows(session, arms[0], 1), "#192: a silenced appendage shows its printed stats and printed definition");
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

    // The client's definition stats (DefTags/ChangeDef, else printed) are the printed stats times the factor, and the entity's own
    // stats are the engine's: so a scaled base shows white and only a real buff shows green.
    private const GameTag TextVariant = (GameTag)955;

    private static bool Shows(NativeSession session, Minion minion, int factor)
    {
        var client = session.Client.Entities[minion.Id];
        factor = Math.Max(1, factor);
        return client.Def.GetValueOrDefault((int)GameTag.ATK, minion.Card.ATK) == minion.Card.ATK * factor
            && client.Def.GetValueOrDefault((int)GameTag.HEALTH, minion.Card.Health) == minion.Card.Health * factor
            && client.Get(GameTag.ATK) == minion.AttackDamage && client.Get(GameTag.HEALTH) == minion.BaseHealth;
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
