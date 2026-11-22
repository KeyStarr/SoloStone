using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using SabberStoneCore.Tasks;
using SabberStoneCore.Tasks.SimpleTasks;
using static Core2026Proof;

internal static class TaughtSpellTargetProof
{
    private static void Check(bool value, string message) => Core2026Proof.Check(value, message);
    private static void Teach(IPlayable token, string spell)
    {
        var taught = PayloadRules.StoreCard(token, 1, Cards.FromId(spell),
            PayloadPolicy.Copy | PayloadPolicy.Silence | PayloadPolicy.ShowOwner);
        token[GameTag.TAG_SCRIPT_DATA_NUM_1] = taught.Id;
    }
    [GroupProof("taught-spell-target")]
    internal static void Run()
    {
        foreach (string id in new[] { "MEND_046t", "TIME_704t" })
        foreach (bool second in new[] { false, true })
        {
            var g = ImbueProof.Make(CardClass.DRUID);
            if (second) End(g);
            var p = g.CurrentPlayer;
            var friendly = Summon(p, "CS2_182");
            var enemy = Summon(p.Opponent, "CS2_182");
            var hidden = Summon(p.Opponent, "CS2_231"); hidden.HasStealth = true;
            Summon(p.Opponent, "EDR_471");
            var token = Add(p, id);
            Teach(token, "EX1_246");
            var options = p.Options().OfType<PlayCardTask>().Where(t => t.Source.Id == token.Id).ToArray();
            Check(options.Select(t => t.Target?.Id ?? 0).ToHashSet().SetEquals(new[] { friendly.Id, enemy.Id }),
                id + " Hex offers exactly friendly/enemy minions, no heroes, stealth, Elusive or null; seat=" + p.PlayerId);
            Check(!token.IsValidPlayTarget(null) && !token.IsValidPlayTarget((ICharacter)token)
                && token.ValidPlayTargets.Select(t => t.Id).ToHashSet()
                .SetEquals(new[] { friendly.Id, enemy.Id }), "target validation matches options");
            var fair = FairBotState.Create(g, p.PlayerId, 21, DeckStyle.Control,
                new FairOptions { RemoveOpponentSecrets = true, ShowOwnCarriedCards = true });
            Check(fair.Actions.Where(a => a.Source == token.Id).Select(a => a.Target).ToHashSet()
                .SetEquals(new[] { friendly.Id, enemy.Id }), "NPC fair view keeps taught spell target options");
            foreach (var next in new[] { g.CloneExact(history: true), Game.LoadSnapshot(g.SaveSnapshot()), g })
            {
                var owner = next.CurrentPlayer;
                var chosen = (Minion)next.IdEntityDic[enemy.Id];
                PlayEntity(next, owner, next.IdEntityDic[token.Id], chosen);
                Check(chosen.Card.Id == "hexfrog" && next.IdEntityDic[friendly.Id].Card.Id == "CS2_182",
                    id + " casts Hex on chosen target after clone/snapshot/play");
            }

            g = ImbueProof.Make(CardClass.DRUID); if (second) End(g); p = g.CurrentPlayer;
            token = Add(p, id); Teach(token, "CS2_005");
            int armor = p.Hero.Armor;
            Check(token.ValidPlayTargets.Count() == 0 && token.IsValidPlayTarget(null), "Claw has no arrow");
            PlayEntity(g, p, token);
            Check(p.Hero.Armor == armor + 2, id + " casts untargeted Claw");

            g = ImbueProof.Make(CardClass.DRUID); if (second) End(g); p = g.CurrentPlayer;
            token = Add(p, id); Teach(token, "CS2_004");
            int hand = p.HandZone.Count;
            Check(token.IsValidPlayTarget(null) && !token.ValidPlayTargets.Any(), "empty board allows targetless Battlecry");
            PlayEntity(g, p, token);
            Check(p.HandZone.Count == hand && ((Minion)token).Health == 2,
                id + " targetless Power Word: Shield draws without retargeting newly played body");

            g = ImbueProof.Make(CardClass.DRUID); if (second) End(g); p = g.CurrentPlayer;
            friendly = Summon(p, "CS2_182"); friendly.Damage = 1;
            enemy = Summon(p.Opponent, "CS2_182");
            token = Add(p, id); Teach(token, "CS2_108");
            Check(!token.ValidPlayTargets.Any() && token.IsValidPlayTarget(null), "Execute excludes friendly damaged and enemy undamaged");
            enemy.Damage = 1;
            Check(token.ValidPlayTargets.Single().Id == enemy.Id, "Execute derives enemy and damaged requirements");
            enemy.Damage = 0;
            PlayEntity(g, p, token);
            Check(p.GraveyardZone.Any(c => c.Card.Id == "CS2_108") && enemy.Health == enemy.BaseHealth,
                id + " targetless Execute casts and retires without choosing another target");

            g = ImbueProof.Make(CardClass.DRUID); if (second) End(g); p = g.CurrentPlayer;
            enemy = Summon(p.Opponent, "CS2_182");
            token = Add(p, id); Teach(token, "EX1_154");
            int randomEvents = 0;
            g.RandomHappenedEvent += (_, _) => randomEvents++;
            PlayEntity(g, p, token, enemy);
            Check((enemy.Damage == 1 || enemy.Damage == 3) && randomEvents == 1,
                id + " chooses a Wrath mode and keeps the chosen target");
        }
        var randomGame = ImbueProof.Make(CardClass.DRUID);
        Summon(randomGame.Player2, "CS2_182");
        var randomSpell = Entity.FromCard(randomGame.Player1, Cards.FromId("EX1_154"));
        int notifications = 0;
        randomGame.RandomHappenedEvent += (_, _) => notifications++;
        new PlayTask(PlayType.SPELL, true).Process(randomGame, randomGame.Player1, randomGame.Player1.Hero, null,
            new TaskStack { Playables = new[] { randomSpell } });
        Check(notifications == 1, "existing random Choose One cast still notifies RNG once");
        Console.WriteLine("PASS taught spells: chosen Hex targets, both seats, clone/snapshot, Claw, no-target draw");
    }

    [GroupProof("taught-spell-granted-elusive")]
    internal static void GrantedElusive() => Elusive(granted: true);

    [GroupProof("taught-spell-printed-elusive")]
    internal static void PrintedElusive() => Elusive(granted: false);

    private static void Elusive(bool granted)
    {
        foreach (string id in new[] { "MEND_046t", "TIME_704t" })
        foreach (bool second in new[] { false, true })
        {
            var g = ImbueProof.Make(CardClass.DRUID);
            if (second) End(g);
            var p = g.CurrentPlayer;
            var protectedMinion = Summon(granted ? p : p.Opponent, granted ? "CS2_182" : "EDR_471");
            if (granted) PlayEntity(g, p, Add(p, "RLK_048"));
            Check(protectedMinion.CantBeTargetedBySpells, "printed/Anti-Magic Shell Elusive is active");
            var token = Add(p, id);
            string spell = granted ? "CATA_138" : "CS2_004";
            Teach(token, spell);
            Check(!token.IsValidPlayTarget(protectedMinion) && !token.ValidPlayTargets.Any()
                && token.IsValidPlayTarget(null)
                && p.Options().Where(t => t.Source?.Id == token.Id).All(t => t.Target == null),
                id + (granted ? " Forest's Gift excludes friendly granted-Elusive target" : " Power Word: Shield excludes enemy printed-Elusive target"));
            int attack = protectedMinion.AttackDamage, health = protectedMinion.Health, hand = p.HandZone.Count;
            PlayEntity(g, p, token);
            Check(p.GraveyardZone.Any(c => c.Card.Id == spell) && protectedMinion.AttackDamage == attack
                && protectedMinion.Health == health && ((Minion)token).Health == 2,
                id + " targetless cast retires without affecting Elusive target or newly played body");
            if (!granted) Check(p.HandZone.Count == hand, id + " targetless Power Word: Shield still draws");
        }
        Console.WriteLine("PASS taught spells: " + (granted ? "friendly granted Elusive" : "enemy printed Elusive") + ", both tokens/seats, targetless cast");
    }
}
