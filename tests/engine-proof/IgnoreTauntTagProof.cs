using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;

// The client draws enemy Taunt frames translucent from the OPPOSING player entity's IGNORE_TAUNT tag (433), so that
// tag must follow CombatRules.IgnoresTaunt for each seat, in the same block as the source entering or leaving.
internal static class IgnoreTauntTagProof
{
    private const string Kayn = "CORE_BT_187";

    private static void Expect(Game g, bool one, bool two, string label) =>
        Core2026Proof.Check(g.Player1[GameTag.IGNORE_TAUNT] == (one ? 1 : 0) && g.Player2[GameTag.IGNORE_TAUNT] == (two ? 1 : 0)
            && CombatRules.IgnoresTaunt(g.Player1) == one && CombatRules.IgnoresTaunt(g.Player2) == two, "IGNORE_TAUNT " + label);

    private static bool Changed(Game g, int cursor, Controller player, int value) =>
        g.PowerHistory.Full.Skip(cursor).OfType<PowerHistoryTagChange>()
            .Any(t => t.EntityId == player.Id && t.Tag == GameTag.IGNORE_TAUNT && t.Value == value);

    [GroupProof("ignore-taunt-tag")]
    internal static void Unpinned_IgnoreTauntPlayerTag()
    {
        foreach (bool seatOne in new[] { true, false })
        {
            var g = ImbueProof.Make(CardClass.ROGUE);
            var owner = seatOne ? g.Player1 : g.Player2;
            var other = owner.Opponent;
            Expect(g, false, false, "starts off");

            int cursor = g.PowerHistory.Full.Count;
            var kayn = Core2026Proof.Summon(owner, Kayn);
            Expect(g, seatOne, !seatOne, "on after Kayn is summoned (seat " + (seatOne ? 1 : 2) + ")");
            Core2026Proof.Check(Changed(g, cursor, owner, 1), "PowerHistory carries IGNORE_TAUNT=1 on the owner's player entity");
            Core2026Proof.Check(!g.PowerHistory.Full.Skip(cursor).OfType<PowerHistoryTagChange>().Any(t => t.EntityId == other.Id && t.Tag == GameTag.IGNORE_TAUNT),
                "the other seat's player entity is untouched");

            // A copy of the history must keep the tag.
            Expect(g.CloneExact(history: true), seatOne, !seatOne, "survives CloneExact");
            Expect(Game.LoadSnapshot(g.SaveSnapshot()), seatOne, !seatOne, "survives snapshot");

            cursor = g.PowerHistory.Full.Count;
            kayn.Silence(); g.DeathProcessingAndAuraUpdate();
            Expect(g, false, false, "off after Silence");
            Core2026Proof.Check(Changed(g, cursor, owner, 0), "PowerHistory carries IGNORE_TAUNT=0 after Silence");

            var second = Core2026Proof.Summon(owner, Kayn);
            Expect(g, seatOne, !seatOne, "on again with a new Kayn");
            cursor = g.PowerHistory.Full.Count;
            Core2026Proof.Kill(g, second);
            Expect(g, false, false, "off after Kayn dies");
            Core2026Proof.Check(Changed(g, cursor, owner, 0), "PowerHistory carries IGNORE_TAUNT=0 after death");

            // Control change: the tag moves to the new controller.
            var third = Core2026Proof.Summon(other, Kayn);
            Expect(g, !seatOne, seatOne, "on for the other seat while it controls Kayn");
            new ControlTask(EntityType.TARGET).Process(g, owner, owner.Hero, third, new TaskStack());
            g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(third.Controller == owner, "control moved");
            Expect(g, seatOne, !seatOne, "follows Kayn to the new controller");
        }
    }
}
