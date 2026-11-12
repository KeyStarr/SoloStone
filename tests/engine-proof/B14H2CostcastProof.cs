using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class B14H2CostcastProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, message);
    [GroupProof("b14-h2-costcast")]
    internal static void Run()
    {
        var failures = new List<string>();
        void Probe(string label, Action test)
        {
            try { test(); Console.WriteLine("PASS H2 " + label); }
            catch (Exception e) { failures.Add(label + ": " + e.ToString()); Console.WriteLine("FAIL H2 " + failures.Last()); }
        }
        foreach (string id in new[] { "EDR_844", "TIME_030", "TIME_036", "TIME_039", "BE_302", "EDR_846", "CORE_LOOT_373", "TLC_106", "CATA_492" })
            Probe(id, () => Check(Cards.FromId(id).Implemented, "missing card implementation"));
        Probe("Portal ordinary copy", () => Portal(false));
        Probe("Portal captured copy", () => Portal(true));
        Probe("automatic fizzle Overload", Overload);
        Probe("natural Deathrattle multiplier", Deathrattle);
        Probe("Baron source aura", () => {
            var g=HeroDivineShieldProof.NewGame(true);var p=g.Player1;
            var baron=Summon(p,"FP1_031"); Kill(g,Summon(p,"EX1_029"));
            Check(p.Opponent.Hero.Damage==4,"Baron doubles natural printed Deathrattle");
            baron.Silence();Kill(g,Summon(p,"EX1_029"));
            Check(p.Opponent.Hero.Damage==6,"Silence removes Baron repeat aura");
        });
        Probe("living location Herald position", Herald);
        Probe("every card without presentation history", B14H2CostcastCardsProof.Historyless);
        Probe("lifecycle and token edges", B14H2CostcastEdgeProof.Run);
        Check(failures.Count == 0, string.Join("\n", failures));
    }

    private static void Portal(bool captured)
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        Summon(p, "EX1_315");
        var held = Add(p, "CS2_182"); g.AuraUpdate();
        Check(held.Cost == 2, "Portal fixture");
        var source = captured ? PayloadRules.Store(p.Hero, 91, held) : held;
        var copy = Generic.Copy(p.Opponent, p.Hero, source, Zone.HAND, preserveCapturedState: captured);
        g.AuraUpdate();
        Check(copy.Cost == 4 && held.Cost == 2, "copy excludes source Portal discount");
    }

    private static void Overload()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        var definition = Cards.FromId("EX1_238").Clone();
        var oldRequirements = definition.PlayRequirements;
        definition.SetPlayRequirements(new Dictionary<PlayReq,int> { { PlayReq.REQ_TARGET_TO_PLAY,0 }, { PlayReq.REQ_MINION_TARGET,0 } });
        var spell = (Spell)Entity.FromCard(p, definition, zone: p.SetasideZone);
        p.Opponent.Hero.IsImmune = true; p.Hero.IsImmune = true;
        try { Generic.CastSpell(p, g, spell, null, 0); }
        finally { definition.SetPlayRequirements(oldRequirements); }
        Check(p.OverloadOwed == 1, "completed indirect no-target cast owes Overload");
    }

    private static void Deathrattle()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        p[GameTag.EXTRA_DEATHRATTLES_BASE] = 1;
        Kill(g, Summon(p, "EX1_029"));
        Check(p.Opponent.Hero.Damage == 4, "player repeat tag doubles printed Deathrattle");
    }

    private static void Herald()
    {
        var g = HeroDivineShieldProof.NewGame(true); var p = g.Player1;
        p[HeraldRules.Profile] = (int)CardClass.WARLOCK;
        Summon(p, "CS2_182");
        var location = LocationProofs.Location(p);
        var right = Summon(p, "CS2_182");
        HeraldRules.Resolve(p, location);
        Check(p.BoardZone.Slots.Last().Id == right.Id && HeraldRules.IsSoldier(p.BoardZone.GetSlot(location.ZonePosition + 1).Card),
            "Herald appears immediately right of living location");
    }
}
