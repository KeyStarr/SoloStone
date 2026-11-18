using SabberStoneCore.Auras;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

// A minion keeps one OngoingEffect. Thunder Bluff Valiant's +2 Attack (AT_049e) is a stacking OngoingEnchant; given to
// Flametongue Totem it took the slot from the totem's own AdjacentAura, which then outlived the totem: after the totem died
// or was Silenced, its left neighbour kept +2 Attack and the minion that slid into its right slot gained +2. A fair-view
// search that later shrank the board read past it (IndexOutOfRangeException in AdjacentAura.Update).
internal static class OwnAuraOngoingEnchantProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, "own aura + ongoing enchant: " + message);

    private static (Game Game, Minion Left, Minion Totem, Minion Right, Minion Valiant) Inspired()
    {
        var game = HeraldProof.New(CardClass.SHAMAN);
        var p = game.Player1;
        var left = Summon(p, "CS2_231"); var totem = Summon(p, "EX1_565"); var right = Summon(p, "CS2_231");
        var valiant = Summon(p, "AT_049");
        Check(left.AttackDamage == 3 && right.AttackDamage == 3 && valiant.AttackDamage == 3, "Flametongue buffs both neighbours");
        Check(game.Process(HeroPowerTask.Any(p)), "Totemic Call accepted");
        Check(totem.AttackDamage == 2, "Inspire gives Flametongue +2 Attack");
        Check(totem.OngoingEffect is AdjacentAura, "Flametongue keeps its own aura after the buff");
        return (game, left, totem, right, valiant);
    }

    [GroupProof("own-aura-ongoing-enchant")]
    internal static void Run()
    {
        var (game, left, totem, right, valiant) = Inspired();
        Kill(game, totem);
        Check(left.AttackDamage == 1 && right.AttackDamage == 1, "a dead Flametongue's neighbours lose its buff");
        Check(valiant.AttackDamage == 3, "the minion sliding next to the dead Flametongue gains nothing");
        Check(!game.Auras.OfType<AdjacentAura>().Any(), "no adjacent aura outlives its owner");
        foreach (var minion in game.Player1.BoardZone.ToArray()) Kill(game, minion);
        Check(game.Player1.BoardZone.Count == 0, "an empty board updates without a stale aura");

        (game, left, totem, right, valiant) = Inspired();
        totem.Silence();
        game.DeathProcessingAndAuraUpdate();
        Check(left.AttackDamage == 1 && right.AttackDamage == 1, "a Silenced Flametongue stops buffing");
        Check(totem.AttackDamage == 0, "Silence removes the Inspire buff too");

        (game, left, totem, right, valiant) = Inspired();
        Check(game.Process(EndTurnTask.Any(game.Player1)) && game.Process(EndTurnTask.Any(game.Player2)), "turns pass");
        Check(game.Process(HeroPowerTask.Any(game.Player1)), "second Totemic Call accepted");
        Check(totem.AttackDamage == 4, "a second Inspire stacks (+4 Attack)");
        var copy = game.CloneExact();
        var copyTotem = (Minion)copy.IdEntityDic[totem.Id];
        Check(copyTotem.OngoingEffect is AdjacentAura && copyTotem.AttackDamage == 4, "a clone keeps the aura and both buffs");
        Kill(copy, copyTotem);
        Check(((Minion)copy.IdEntityDic[left.Id]).AttackDamage == 1 && ((Minion)copy.IdEntityDic[right.Id]).AttackDamage == 1,
            "in a clone, the dead Flametongue's neighbours lose its buff");
        Console.WriteLine("PASS own aura + ongoing enchant: Flametongue keeps its aura through Thunder Bluff Valiant's buff");
    }
}
