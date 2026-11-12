using System.Linq;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;

internal static partial class PoweredUpGlowTests
{
    [GroupProof("powered-up-cataclysm", ProofKind.PoweredUp)]
    private static void RunCataclysm()
    {
        RunBeCataclysmConditions();
        GlowClearsAfterControlOrIdentityChange();
        Scenario("CATA_483", (f, card, met) => {
            f.Flush();
            f.Expected(card, 0, "Spellcaster before spell damage");
            if (met)
            {
                f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), f.Game.Player2.Hero));
                f.Expected(card, 1, "Spellcaster lights in damage spell flush");
            }
        }, (f, card) => PlayCardTask.Minion(f.Player, (Minion)card), (f, card) => {
            int count = f.Player.BoardZone.Count(m => m.Card.Id == card.Card.Id);
            Check(card.Zone == f.Player.BoardZone && (count == 1 || count == 2), "Spellcaster source/copy board count");
            return count == 2;
        });

        var fixture = new Fixture();
        var caster = fixture.Draw("CATA_483");
        fixture.Player.Hero.Damage = 5;
        fixture.Process(PlayCardTask.SpellTarget(fixture.Player, fixture.Draw("CS2_007"), fixture.Player.Hero));
        Check(fixture.Player.Hero.Damage == 0, "healing spell did not heal");
        fixture.Expected(caster, 0, "healing does not light Spellcaster");
        fixture.Process(PlayCardTask.Spell(fixture.Player, fixture.Draw("CS2_023")));
        fixture.Expected(caster, 0, "non-damage spell does not light Spellcaster");
        fixture.Mana(10);
        fixture.Process(PlayCardTask.SpellTarget(fixture.Player, fixture.Draw("CS2_029"), fixture.Game.Player2.Hero));
        fixture.Expected(caster, 1, "spell damage lights Spellcaster");
        fixture.Process(EndTurnTask.Any(fixture.Player));
        fixture.Expected(caster, 0, "opponent turn clears Spellcaster");
        var opponent = fixture.Game.Player2;
        opponent.BaseMana = 10; opponent.UsedMana = 0;
        var enemyCaster = SabberStoneCore.Actions.Generic.DrawCard(opponent, Cards.FromId("CATA_483"));
        var fireball = SabberStoneCore.Actions.Generic.DrawCard(opponent, Cards.FromId("CS2_029"));
        fixture.Process(PlayCardTask.SpellTarget(opponent, fireball, fixture.Player.Hero));
        Check(fixture.Game.HistoryFacts.Query.AnySpellDamage(opponent.Id, fixture.Game.HistoryFacts.Query.ThisTurn),
            "opponent spell-damage fixture did not qualify");
        Check(PoweredUp.IsMet(enemyCaster), "engine condition must qualify on its own controller's turn");
        Check(fixture.Glow(enemyCaster) == 0 && !fixture.Entries.Any(e => e.HasTagChange
            && e.TagChange.Entity == enemyCaster.Id && e.TagChange.Tag == (int)GameTag.POWERED_UP),
            "opponent Spellcaster received a POWERED_UP tag change");
        fixture.Process(EndTurnTask.Any(opponent));
        fixture.Expected(caster, 0, "next own turn clears spell-damage history condition");

        fixture = new Fixture();
        caster = fixture.Draw("CATA_483");
        fixture.Process(PlayCardTask.SpellTarget(fixture.Player, fixture.Draw("CS2_029"), fixture.Game.Player2.Hero));
        fixture.Expected(caster, 1, "Spellcaster before leaving hand");
        fixture.Player.SetasideZone.Add(fixture.Player.HandZone.Remove(caster));
        Check(fixture.Match.SnapshotVisibleTransportTags(caster)[(int)GameTag.POWERED_UP] == 1,
            "historical audit must keep the published glow until the reset is published");
        fixture.Flush();
        fixture.Expected(caster, 0, "Spellcaster reset outside hand");
        fixture.Player.HandZone.Add(fixture.Player.SetasideZone.Remove(caster));
        fixture.Flush();
        fixture.Expected(caster, 1, "Spellcaster restored to hand");
        int beforeShuffle = fixture.Entries.Count();
        fixture.Player.HandZone.Remove(caster);
        Check(SabberStoneCore.Actions.Generic.ShuffleIntoDeck(fixture.Player, fixture.Player, caster), "Spellcaster shuffle failed");
        fixture.Flush();
        var shuffled = fixture.Entries.Skip(beforeShuffle).ToList();
        int reset = shuffled.FindIndex(e => e.HasTagChange && e.TagChange.Entity == caster.Id
            && e.TagChange.Tag == (int)GameTag.POWERED_UP && e.TagChange.Value == 0);
        int hide = shuffled.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == caster.Id);
        Check(reset >= 0 && hide > reset && fixture.Glow(caster) == 0, "custom glow must clear before HIDE");
    }

    // Review regressions: a published glow must clear even when its card no longer qualifies for one.
    private static void GlowClearsAfterControlOrIdentityChange()
    {
        var f = new Fixture();
        var caster = f.Draw("CATA_483");
        f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), f.Game.Player2.Hero));
        f.Expected(caster, 1, "Spellcaster glowing before control change");
        f.Player.HandZone.Remove(caster);
        caster.Controller = f.Game.Player2;
        f.Game.Player2.HandZone.Add(caster);
        f.Flush();
        Check(f.Glow(caster) == 0, "glow published for the human must clear after the card changes control");

        f = new Fixture();
        caster = f.Draw("CATA_483");
        f.Process(PlayCardTask.SpellTarget(f.Player, f.Draw("CS2_029"), f.Game.Player2.Hero));
        f.Expected(caster, 1, "Spellcaster glowing before transform");
        var vanilla = SabberStoneCore.Actions.Generic.ChangeEntityBlock(f.Player, caster, Cards.FromId("CS2_182"), true);
        Check(vanilla.Id == caster.Id && !PoweredUp.HasCondition(vanilla.Card), "transform fixture");
        int before = f.Entries.Count();
        f.Player.HandZone.Remove(vanilla);
        Check(SabberStoneCore.Actions.Generic.ShuffleIntoDeck(f.Player, f.Player, vanilla), "shuffle fixture");
        f.Flush();
        var batch = f.Entries.Skip(before).ToList();
        int reset = batch.FindIndex(e => e.HasTagChange && e.TagChange.Entity == vanilla.Id
            && e.TagChange.Tag == (int)GameTag.POWERED_UP && e.TagChange.Value == 0);
        int hide = batch.FindIndex(e => e.HasHideEntity && e.HideEntity.Entity == vanilla.Id);
        Check(hide >= 0 && reset >= 0 && reset < hide && f.Glow(vanilla) == 0,
            "transformed card must clear its published glow before HIDE");
    }
}
