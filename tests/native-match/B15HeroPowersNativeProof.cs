using System;
using System.Linq;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks.SimpleTasks;
internal static partial class Program
{
    [GroupProof("b15-heropowers-native", ProofKind.Native)]
    private static void B15HeroPowersNative()
    {
        var f = new PoweredUpGlowTests.Fixture(); var p = f.Player;
        var second = BasicHeroPowerRules.InstallSecondary(p, p.Hero, "JAIL_446hp"); f.Flush();
        Check(f.Entries.Any(e => e.HasFullEntity && e.FullEntity.Entity == second.Id && e.FullEntity.Name == "JAIL_446hp"
            && TagValue(e.FullEntity.Tags, (GameTag)3919) == 1), "second power native FULL_ENTITY slot index");
        Check(f.Entries.Any(e => e.HasTagChange && e.TagChange.Entity == p.Id && e.TagChange.Tag == 4029
            && e.TagChange.Value == second.Id), "player native additional power entity reference");
        p[GameTag.CORPSES] = 3; f.Mana(0);
        var target = f.Summon("CS2_182", p);
        f.Process(p.Options().Single(t => t.Source?.Id == second.Id && t.Target?.Id == target.Id));
        Check(target.AttackDamage == 7 && p.UsedMana == 0 && CorpseRules.Available(p) == 0 && second.IsExhausted,
            "native dispatch selects second power and pays Corpses");
        Check(f.Entries.Any(e => SnapshotEntity(e) == second.Id && SnapshotTag(e, (GameTag)2837) == 3)
            && f.Entries.Any(e => e.HasTagChange && e.TagChange.Entity == p.Id && e.TagChange.Tag == 2639 && e.TagChange.Value == 3),
            "retail alternate cost and cumulative corpse spend projected");
        var fletcher = f.Summon("TIME_606", p); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        Check(fletcher[(GameTag)31291] == second.Id, "conditional secondary aura fixture owns tagged state");
        fletcher.Silence(); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        BasicHeroPowerRules.SwapForTwoUses(p, p.Hero); f.Flush();
        Check(!f.Entries.Any(e => e.HasTagChange && e.TagChange.Tag >= 31290 && e.TagChange.Tag <= 31292
            || e.HasFullEntity && e.FullEntity.Tags.Any(t => t.Name >= 31290 && t.Name <= 31292)
            || e.HasShowEntity && e.ShowEntity.Tags.Any(t => t.Name >= 31290 && t.Name <= 31292)
            || e.HasChangeEntity && e.ChangeEntity.Tags.Any(t => t.Name >= 31290 && t.Name <= 31292)), "internal restore pointer never reaches native wire");
        int temporaryId = p.Hero.HeroPower.Id;
        f.Mana(10);
        f.Process(p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Source == null));
        Check(p.Hero.HeroPower.Card.Id == "TLC_632t2" && f.Entries.Any(e => e.HasChangeEntity
            && e.ChangeEntity.Entity == temporaryId && e.ChangeEntity.Name == "TLC_632t2"), "last-use identity reaches native history");
        // A fixture refresh isolates restore projection; natural owner-turn refresh is covered by engine proof.
        p.Hero.HeroPower.IsExhausted = false; f.Mana(10);
        f.Process(p.Options().First(t => t.PlayerTaskType == PlayerTaskType.HERO_POWER && t.Source == null));
        Check(p.Hero.HeroPower.Id != temporaryId && !p.Hero.HeroPower.IsExhausted
            && f.Entries.Any(e => e.HasTagChange && e.TagChange.Entity == p.Hero.HeroPower.Id
                && e.TagChange.Tag == (int)GameTag.ZONE && e.TagChange.Value == 1), "saved power returns to native PLAY ready");
        // Opponent-generated deck identities and private hand transformations must stay anonymous.
        var enemy = f.Game.Player2; enemy.Hero.Card = Cards.FromId("HERO_04"); ImbueRules.Imbue(enemy, enemy.Hero);
        enemy.Hero.HeroPower.Power.PowerTask.Process(f.Game, enemy, enemy.Hero.HeroPower, null, new TaskStack());
        var portals = enemy.DeckZone.Where(c => c.Card.Id == "EDR_445pt3").ToArray(); f.Flush();
        Check(portals.Length == 2 && portals.All(portal => f.Entries.Where(e => SnapshotEntity(e) == portal.Id)
            .All(e => string.IsNullOrEmpty(SnapshotName(e)))), "opponent-generated Portals never leak identities");
        ImbueRules.Imbue(enemy, enemy.Hero); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        Check(portals.All(portal => !f.Entries.Any(e => e.HasTagChange && e.TagChange.Entity == portal.Id
            && e.TagChange.Tag == (int)GameTag.TAG_SCRIPT_DATA_NUM_1)), "hidden Portal magnitude updates never leak");
        foreach (var card in enemy.HandZone.ToArray()) enemy.SetasideZone.Add(enemy.HandZone.Remove(card));
        var genn = Generic.DrawCard(enemy, Cards.FromId("CATA_615")); f.Game.DeathProcessingAndAuraUpdate(); f.Flush();
        Check(genn.Card.Id == "CATA_615t" && f.Entries.Where(e => SnapshotEntity(e) == genn.Id)
            .All(e => string.IsNullOrEmpty(SnapshotName(e))), "opponent Genn transform remains private");
        Console.WriteLine("PASS b15 native: second slot/dispatch/payment; internal filtering; Portal/Genn negative privacy");
    }
}
