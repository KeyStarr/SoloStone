using System.Reflection;
using SabberStoneCore.Actions;
using SabberStoneCore.Auras;
using SabberStoneCore.Conditions;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Model.Snapshots;
using static Core2026Proof;

// det466 (2026-11-01): Aura.Activate lazily wrote the enchantment's effects into the shared catalogue prototype.
// A generated definition that shares that Power (Watfin's UseBehaviourFrom) is frozen into history and snapshots,
// so its bytes depended on whether any game in the process had activated the aura before: the live fingerprint and
// a later same-process replay disagreed although every entity was identical.
internal static class AuraPrototypeImmutabilityProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, message);
    private static readonly FieldInfo EffectsField = typeof(Aura).GetField("Effects", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingFieldException(typeof(Aura).FullName, "Effects");

    [GroupProof("aura-prototype-immutable")]
    internal static void Run()
    {
        // Fresh prototypes: catalogue prototypes may already be warmed by earlier proofs in this process.
        var aura = new Aura(AuraType.OP_BOARD, "CATA_898e");
        var custom = Cards.FromId("CATA_898").Clone();
        custom.Power = new Power { Aura = aura };
        string definition = SnapshotCodec.EncodeDefinition(custom);
        var game = HeraldProof.New(CardClass.DRUID);
        var player = game.Player1;
        var enemy = Summon(player.Opponent, "CS2_182");
        var lancer = (Minion)Entity.FromCard(player, custom);
        Generic.SummonBlock(game, lancer, -1, null);
        game.DeathProcessingAndAuraUpdate();
        Check(enemy.HasTaunt, "instance resolves enchantment effects (Scaled Lancer gives enemies Taunt)");
        Check(SnapshotCodec.EncodeDefinition(custom) == definition, "definition bytes do not depend on earlier activations");
        Check(EffectsField.GetValue(aura) == null, "Aura activation leaves the shared prototype unresolved");
        foreach (var copy in new[] { game.CloneExact(history: true), Game.LoadSnapshot(game.SaveSnapshot()) })
            Check(((Minion)copy.IdEntityDic[enemy.Id]).HasTaunt, "cloned/restored aura keeps resolved effects");

        var switching = new SwitchingAura(AuraType.HAND, SelfCondition.MinionsPlayedThisTurn(0), TriggerType.PLAY_MINION, "CATA_898e");
        var host = Summon(player, "CS2_182");
        switching.Activate(host);
        var instance = game.Auras.OfType<SwitchingAura>().Single(a => a.Owner == host);
        Check(EffectsField.GetValue(switching) == null, "SwitchingAura activation leaves the shared prototype unresolved");
        Check(EffectsField.GetValue(instance) != null, "SwitchingAura instance resolves enchantment effects");
        Console.WriteLine("PASS aura prototype immutable: activation never writes shared catalogue auras (det466)");
    }
}
