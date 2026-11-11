using System.Reflection;
using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class CoreIdentityProof
{
    internal static readonly (string Parent, string First, string Second)[] Options = {
        ("CORE_AT_037", "AT_037a", "AT_037b"),
        ("CORE_EX1_154", "EX1_154a", "EX1_154b"),
        ("CORE_EX1_160", "EX1_160a", "EX1_160b"),
        ("CORE_OG_047", "OG_047a", "OG_047b"),
        ("CORE_ONY_018", "ONY_018t", "ONY_018t2"),
        ("CORE_TSC_650", "TSC_650a", "TSC_650d"),
        ("Core_LOE_115", "LOE_115a", "LOE_115b")
    };

    [GroupProof("b14-f5-core-identity")]
    internal static void Run()
    {
        var resolve = typeof(Card).Assembly.GetType("SabberStoneCore.Loader.ChooseOneOptions")
            .GetMethod("Resolve", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var row in Options)
        {
            // Synthetic parent lets this proof run before its module/overlay exists.
            var parent = Cards.FromId("EX1_154").Clone();
            typeof(Card).GetProperty(nameof(Card.Id)).SetValue(parent, row.Parent);
            var choices = (Card[])resolve.Invoke(null, new object[] { parent });
            Core2026Proof.Check(choices.All(c => c != null) && choices.Select(c => c.Id).SequenceEqual(new[] { row.First, row.Second }),
                "Core export options: " + row.Parent);
        }
        // Existing ordinary and uppercase modern mappings remain byte-for-byte identities.
        foreach (var row in new[] { ("EX1_154", "EX1_154a", "EX1_154b"), ("EDR_525", "EDR_525A", "EDR_525B") })
        {
            var choices = (Card[])resolve.Invoke(null, new object[] { Cards.FromId(row.Item1) });
            Core2026Proof.Check(choices.Select(c => c.Id).SequenceEqual(new[] { row.Item2, row.Item3 }), "existing mapping unchanged");
        }
        var game = HeroDivineShieldProof.NewGame(true); var owner = game.Player1;
        var optionData = typeof(Card).Assembly.GetType("SabberStoneCore.Loader.ChooseOneOptions")
            .GetMethod("OptionData", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var parent in Cards.All.Where(c => c.ChooseOne && Options.All(row => row.Parent != c.Id)))
        {
            var tags = (IDictionary<SabberStoneCore.Enums.GameTag, int>)optionData.Invoke(null, new object[] { owner, 123, parent });
            Core2026Proof.Check(tags.Count == 2 && tags[SabberStoneCore.Enums.GameTag.CREATOR] == 123
                && tags[SabberStoneCore.Enums.GameTag.PARENT_CARD] == 123, "every existing Choose One parent retains exact previous tags: " + parent.Id);
        }
        foreach (var row in Options)
        {
            var original = Core2026Proof.Add(owner, "CS2_231");
            var transformed = Generic.ChangeEntityBlock(owner, original, Cards.FromId(row.Parent), true);
            Core2026Proof.Check(transformed.ChooseOnePlayables.Select(c => c.Card.Id).SequenceEqual(new[] { row.First, row.Second }), "transformed Core option actors");
            Core2026Proof.Check(transformed.ChooseOnePlayables.All(c => c[Generic.PrivateDecisionOfferTag] == owner.PlayerId), "transformed Core options stay private to owner");
            PoolPilotProof.Resume(game, next => {
                var entity = next.IdEntityDic[transformed.Id];
                Core2026Proof.Check(entity.ChooseOnePlayables.Select(c => c.Card.Id).SequenceEqual(new[] { row.First, row.Second }), "Core choices survive clone/load");
                Core2026Proof.Check(entity.ChooseOnePlayables.All(c => c[Generic.PrivateDecisionOfferTag] == owner.PlayerId), "Core option privacy survives clone/load");
            });
            owner.SetasideZone.Add(owner.HandZone.Remove(transformed));
        }
        Console.WriteLine("PASS Core identity: exact Core option ids; legacy mappings unchanged");
    }
}
