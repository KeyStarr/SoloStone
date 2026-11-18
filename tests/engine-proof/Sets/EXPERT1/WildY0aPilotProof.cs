using HsSim;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class WildY0aPilotProof
{
    // Held cases: proof code kept, never run and never registered with WildSetsProof.Case.
    private static readonly List<(string Id, Action<Game, Controller, string> Scenario)> HeldForStandardCases = new();
    private static void HeldForStandard(string id, Action<Game, Controller, string> scenario) => HeldForStandardCases.Add((id, scenario));

    [GroupProof("wild-y0a-pilot-cards", ProofKind.WildCards)]
    internal static void Run()
    {
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_028", (g, p, id) => {
            var friend = Summon(p, "CS2_182");
            var enemy = Summon(p.Opponent, "CS2_182");
            var other = Summon(p.Opponent, "CS2_182");
            Play(g, p, id);
            Core2026Proof.Check(enemy.Damage == 2 && other.Damage == 2 && enemy.IsFrozen && other.IsFrozen,
                "Blizzard damages and Freezes every enemy minion");
            Core2026Proof.Check(friend.Damage == 0 && !friend.IsFrozen && p.Hero.Damage == 0 && p.Opponent.Hero.Damage == 0
                && !p.Opponent.Hero.IsFrozen, "Blizzard excludes friendly minions and heroes");
            Summon(p, "EX1_012"); Play(g, p, id);
            Core2026Proof.Check(enemy.Zone.Type == Zone.GRAVEYARD && other.Zone.Type == Zone.GRAVEYARD,
                "Blizzard scales with Spell Damage and kills at exact remaining Health");
            Play(g, p, id);
            Core2026Proof.Check(p.Opponent.BoardZone.Count == 0 && friend.Damage == 0, "Blizzard accepts empty enemy board");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_031", (g, p, id) => {
            var target = Summon(p.Opponent, "CS2_182");
            Play(g, p, id, target);
            Core2026Proof.Check(target.IsFrozen && target.Damage == 0, "Ice Lance Freezes unfrozen target without damage");
            Play(g, p, id, target);
            Core2026Proof.Check(target.IsFrozen && target.Damage == 4 && target.Health == 1, "Ice Lance instead deals four to already Frozen minion");
            Play(g, p, id, p.Hero); Core2026Proof.Check(p.Hero.IsFrozen && p.Hero.Damage == 0, "Ice Lance allows friendly hero");
            Summon(p, "EX1_012"); Play(g, p, id, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 5, "Ice Lance damage branch scales with Spell Damage");
            Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.IsFrozen && p.Opponent.Hero.Damage == 0, "Ice Lance allows enemy hero without initial damage");
        });
        WildSetsProof.Case("CS2_059", (g, p, id) => {
            var imp = (Minion)Play(g, p, id); var enemy = Summon(p.Opponent, "CS2_182");
            Core2026Proof.Check(imp.HasStealth, "Blood Imp has printed Stealth");
            End(g); Core2026Proof.Check(imp.Health == 1 && enemy.Health == 5, "Blood Imp alone cannot buff itself or enemy");
            End(g);
            var friend = Summon(p, "CS2_182");
            Core2026Proof.Check(friend.Health == 5, "Blood Imp does not buff on summon");
            Resume(g, next => {
                End(next);
                Core2026Proof.Check(((Minion)next.IdEntityDic[friend.Id]).Health == 6 && ((Minion)next.IdEntityDic[imp.Id]).Health == 1
                    && ((Minion)next.IdEntityDic[enemy.Id]).Health == 5, "Blood Imp buffs only other friendly minion at owner end");
                End(next);
                Core2026Proof.Check(((Minion)next.IdEntityDic[friend.Id]).Health == 6, "Blood Imp ignores opponent end");
            });
            End(g); End(g);
            var second = Summon(p, "CS2_182"); End(g);
            Core2026Proof.Check(friend.Health + second.Health == 12 && (friend.Health == 7 || second.Health == 6),
                "Blood Imp chooses exactly one of multiple eligible minions");
            End(g); imp.Silence(); g.DeathProcessingAndAuraUpdate(); End(g);
            Core2026Proof.Check(friend.Health + second.Health == 12 && !imp.HasStealth, "Silence removes Blood Imp trigger and Stealth");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_104", (g, p, id) => {
            var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
            var spell = Add(p, id);
            Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == spell.Id), "Rampage unavailable without damaged minion");
            friend.Damage = 1; enemy.Damage = 1; p.Hero.Damage = 1; p.Opponent.Hero.Damage = 1;
            var options = p.Options().Where(t => t.Source?.Id == spell.Id).ToArray();
            Core2026Proof.Check(options.Any(t => t.Target?.Id == friend.Id) && options.Any(t => t.Target?.Id == enemy.Id)
                && options.All(t => t.Target is Minion m && m.Damage > 0), "Rampage permits damaged minions on either side, excludes heroes");
            PlayEntity(g, p, spell, friend); Play(g, p, id, enemy);
            Core2026Proof.Check(friend.AttackDamage == 7 && friend.Health == 7 && friend.Damage == 1
                && enemy.AttackDamage == 7 && enemy.Health == 7, "Rampage gives +3/+3 without healing existing damage");
            End(g); Core2026Proof.Check(friend.AttackDamage == 7 && enemy.Health == 7, "Rampage persists past turn end");
            friend.Silence(); g.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(friend.AttackDamage == 4 && friend.Health == 5 && friend.Damage == 0, "Silence removes Rampage buff and its absorbed damage");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_117", (g, p, id) => {
            p.Hero.Damage = 5; Play(g, p, id, p.Hero);
            Core2026Proof.Check(p.Hero.Damage == 2, "Farseer restores exactly three Health to hero");
            var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 1;
            var source = (Minion)Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.Damage == 0 && enemy.Health == 5, "Farseer heals enemy minion, capped at maximum Health");
            enemy.Damage = 2; source.Silence(); Kill(g, source); Summon(p, id);
            Core2026Proof.Check(enemy.Damage == 2, "Farseer summon, Silence and death do not repeat Battlecry");
            p.Opponent.Hero.Damage = 4; Play(g, p, id, p.Opponent.Hero);
            Core2026Proof.Check(p.Opponent.Hero.Damage == 1, "Farseer may heal opponent hero");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_188", TemporaryAttack);
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("CS2_203", (g, p, id) => {
            var enemy = Summon(p.Opponent, "CS2_179");
            Core2026Proof.Check(enemy.HasTaunt, "Owl target has Taunt before Silence");
            Play(g, p, id, enemy);
            Core2026Proof.Check(enemy.IsSilenced && !enemy.HasTaunt && enemy.AttackDamage == 3 && enemy.Health == 5,
                "Owl Silences enemy minion without damage");
            var imp = Summon(p, "CS2_059"); Play(g, p, id, imp);
            Core2026Proof.Check(imp.IsSilenced && !imp.HasStealth, "Owl may Silence friendly Stealth minion");
            var held = Add(p, id);
            Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == held.Id).All(t => t.Target is Minion), "Owl excludes hero targets");
            End(g); Core2026Proof.Check(enemy.Health == 5 && imp.Health == 1, "Silenced Blood Imp trigger stays removed"); End(g);
            var sylvanas = Summon(p.Opponent, "EX1_016"); Play(g, p, id, sylvanas);
            int count = p.BoardZone.Count; Kill(g, sylvanas);
            Core2026Proof.Check(p.BoardZone.Count == count && p.Opponent.BoardZone.All(m => m.Id == enemy.Id), "Owl removes enemy Deathrattle");
            StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
            Play(g, p, id); Core2026Proof.Check(p.BoardZone.Single().Card.Id == id, "Owl playable without another minion");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_012", (g, p, id) => {
            var source = (Minion)Play(g, p, id); var target = Summon(p.Opponent, "CS2_182");
            Play(g, p, "CS2_008", target);
            Core2026Proof.Check(target.Damage == 2, "Thalnos adds one Spell Damage to one-damage spell");
            int hand = p.HandZone.Count, deck = p.DeckZone.Count, enemyHand = p.Opponent.HandZone.Count;
            Resume(g, next => {
                Kill(next, (Minion)next.IdEntityDic[source.Id]);
                Core2026Proof.Check(next.Player1.HandZone.Count == hand + 1 && next.Player1.DeckZone.Count == deck - 1
                    && next.Player2.HandZone.Count == enemyHand, "Thalnos Deathrattle draws exactly one for owner");
                Core2026Proof.Check(next.Player1.CurrentSpellPower == 0, "Thalnos Spell Damage ends on death");
            });
            source.Silence(); g.DeathProcessingAndAuraUpdate();
            target = Summon(p.Opponent, "CS2_182"); Play(g, p, "CS2_008", target);
            Core2026Proof.Check(target.Damage == 1 && target.Health == 4, "Silence removes Thalnos Spell Damage");
            hand = p.HandZone.Count; deck = p.DeckZone.Count; Kill(g, source);
            Core2026Proof.Check(p.HandZone.Count == hand && p.DeckZone.Count == deck, "Silence removes Thalnos draw Deathrattle");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_016", (g, p, id) => {
            var source = (Minion)Play(g, p, id);
            var enemy = Summon(p.Opponent, "CS2_182"); enemy.Damage = 2;
            Core2026Proof.Check(enemy.Controller == p.Opponent, "Sylvanas does not steal before death");
            Resume(g, next => {
                Kill(next, (Minion)next.IdEntityDic[source.Id]);
                var stolen = (Minion)next.IdEntityDic[enemy.Id];
                Core2026Proof.Check(stolen.Controller == next.Player1 && stolen.Health == 3 && next.Player2.BoardZone.Count == 0,
                    "Sylvanas steals enemy minion, retaining identity and damage");
            });
            var second = Summon(p.Opponent, "CS2_179"); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == 1
                && new[] { enemy, second }.Count(m => m.Controller == p) == 1, "Sylvanas steals exactly one random enemy");
            source = Summon(p, id); source.Silence(); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 1 && p.Opponent.BoardZone.Count == 1, "Silenced Sylvanas steals nothing");
            StandardSetsProof.ParkBoard(p.Opponent); source = Summon(p, id); Kill(g, source);
            Core2026Proof.Check(p.BoardZone.Count == 1, "Sylvanas with no enemies cannot steal friendly minion");
        });
        // held: used by Standard (plugin/LocalCardCatalog.cs) until #87
        HeldForStandard("EX1_046", TemporaryAttack);
    }

    private static void TemporaryAttack(Game g, Controller p, string id)
    {
        var friend = Summon(p, "CS2_182"); var enemy = Summon(p.Opponent, "CS2_182");
        var source = (Minion)Play(g, p, id, friend);
        Core2026Proof.Check(friend.AttackDamage == 6 && friend.Health == 5 && source.AttackDamage == source.Card.ATK
            && enemy.AttackDamage == 4, id + " gives target exactly +2 Attack, leaves others unchanged");
        source.Silence(); Kill(g, source);
        Core2026Proof.Check(friend.AttackDamage == 6, id + " buff outlives source Silence/death");
        Resume(g, next => { End(next); Core2026Proof.Check(((Minion)next.IdEntityDic[friend.Id]).AttackDamage == 4, id + " expires at end of turn"); });
        Play(g, p, id, enemy);
        Core2026Proof.Check(enemy.AttackDamage == 6, id + " may buff enemy minion");
        enemy.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(enemy.AttackDamage == 4, id + " recipient Silence removes buff");
        var held = Add(p, id);
        Core2026Proof.Check(p.Options().Where(t => t.Source?.Id == held.Id).All(t => t.Target is Minion), id + " excludes heroes");
        End(g); Core2026Proof.Check(friend.AttackDamage == 4 && enemy.AttackDamage == 4, id + " temporary buffs do not persist"); End(g);
        StandardSetsProof.ParkBoard(p); StandardSetsProof.ParkBoard(p.Opponent);
        Play(g, p, id); Core2026Proof.Check(p.BoardZone.Single().AttackDamage == Cards.FromId(id).ATK, id + " playable without target; cannot buff itself");
    }

    private static void Resume(Game game, Action<Game> continuation)
    {
        foreach (var next in new[] { game.CloneExact(history: true), Game.LoadSnapshotWithPools(game.SaveSnapshot(), game.OfflinePools) })
        {
            Core2026Proof.Check(Canonical.State(game) == Canonical.State(next), "pilot state before continuation");
            continuation(next);
        }
    }
}
