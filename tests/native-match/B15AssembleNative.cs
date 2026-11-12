using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Enums;
using System.Reflection;

internal static class B15AssembleNative
{
    private static void Check(bool value, string message)
    { if (!value) throw new Exception("Assemble native: " + message); }

    [GroupProof("b15-assemble-native", ProofKind.Native)]
    internal static void Run()
    {
        var fixture = new PoweredUpGlowTests.Fixture();
        var definition = SabberStoneCore.Model.Card.CreateAssembledCard(Cards.FromId("CORE_LOOT_413"), Cards.FromId("CORE_DMF_067"), Cards.FromId("CORE_LOOT_413"));
        var visible = Generic.DrawCard(fixture.Player, definition);
        var hidden = Generic.DrawCard(fixture.Player.Opponent, definition);
        fixture.Flush();
        var tags = fixture.Match.SnapshotVisibleTransportTags(visible);
        Check(tags.TryGetValue(4572, out int top) && top == Cards.FromId("CORE_DMF_067").AssetId
            && tags.TryGetValue(4573, out int bottom) && bottom == Cards.FromId("CORE_LOOT_413").AssetId,
            "retail component dbfIds reach native text builder");
        foreach (var entry in fixture.Entries)
        {
            var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
            if (entity?.Entity != hidden.Id) continue;
            Check(string.IsNullOrEmpty(entity.Name) && entity.Tags.All(t => t.Name != 4572 && t.Name != 4573),
                "opponent generated hand identity and component tags remain hidden");
        }
        var bot = fixture.Player.Opponent;
        bot.AddHeroAndPower(Cards.HeroCard(CardClass.MAGE));
        var source = fixture.Summon("CS2_182", bot);
        Cards.FromId("BE_257").Power.PowerTask.Process(fixture.Game, bot, source, null, new TaskStack());
        fixture.Flush();
        var privateIds = fixture.Game.IdEntityDic.Values.Where(c => c.Controller == bot
            && (c[Generic.PrivateDecisionOfferTag] == bot.PlayerId || c.Card[AssembleRules.FirstHalf] != c.Card[AssembleRules.SecondHalf]))
            .Select(c => c.Id).ToHashSet();
        foreach (var actor in fixture.Game.IdEntityDic.Values.OfType<SabberStoneCore.Model.Entities.Enchantment>())
            if (privateIds.Contains(actor.Target.Id)) privateIds.Add(actor.Id);
        Check(privateIds.Count >= 4, "actual opposing recipe creates donor and composite offers plus reward");
        foreach (var entry in fixture.Entries)
        {
            var entity = entry.HasFullEntity ? entry.FullEntity : entry.HasShowEntity ? entry.ShowEntity : entry.HasChangeEntity ? entry.ChangeEntity : null;
            if (entity == null || !privateIds.Contains(entity.Entity)) continue;
            Check(string.IsNullOrEmpty(entity.Name) && entity.Tags.All(t => t.Name != 4572 && t.Name != 4573),
                "actual opposing offers, reward and later snapshots do not reveal component identities");
        }
        var fair = FairBotState.Create(fixture.Game, fixture.Player.PlayerId, 77, DeckStyle.Value);
        var view = (SabberStoneCore.Model.Game)typeof(FairBotState).GetField("game", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(fair);
        FairViewHiddenOfferProof.AssertHiddenActors(fixture.Game, view, bot.PlayerId, privateIds.ToArray());
        Console.WriteLine("PASS Assemble native components, actual recipe privacy and fair-view redaction");
    }
}
