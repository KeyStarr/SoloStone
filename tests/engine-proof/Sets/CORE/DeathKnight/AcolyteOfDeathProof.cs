using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using static Core2026Proof;

internal static class AcolyteOfDeathProof
{
    internal static void Run() => StandardSetsProof.Case("CORE_RLK_121", (g, p, id) => {
        DeathOrder(g, id);
        GrantedDeathrattleOrder(g, id);
        RemovedListener(g, id);
        LegacyOrder(g, id);
        var source = (Minion)Play(g, p, id);
        Core2026Proof.Check(source.Cost == 3 && source.AttackDamage == 2 && source.Health == 4, "3 mana 2/4 Acolyte");
        int deck = p.DeckZone.Count;
        Kill(g, Summon(p, "RLK_958"));
        Core2026Proof.Check(p.DeckZone.Count == deck - 1 && p.HandZone.Count == 1, "friendly Undead draws actual deck card");
        Kill(g, Summon(p, "CS2_182"));
        Kill(g, Summon(p.Opponent, "RLK_958"));
        Core2026Proof.Check(p.DeckZone.Count == deck - 1, "other tribes and opposing Undead do not draw");
        var clone = g.CloneExact(history: true);
        Kill(clone, Summon(clone.Player1, "RLK_958"));
        Core2026Proof.Check(clone.Player1.DeckZone.Count == deck - 2 && p.DeckZone.Count == deck - 1, "clone trigger stays independent");
        source.Silence();
        Kill(g, Summon(p, "RLK_958"));
        Core2026Proof.Check(p.DeckZone.Count == deck - 1, "Silence removes death trigger");
        Kill(g, source);
        source = Summon(p, id);
        Kill(g, source);
        Kill(g, Summon(p, "RLK_958"));
        Core2026Proof.Check(p.DeckZone.Count == deck - 1, "departed source cannot trigger");
        source = Summon(p, id);
        var undead = Summon(p, "RLK_958");
        source.ToBeDestroyed = true; undead.ToBeDestroyed = true;
        g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(p.DeckZone.Count == deck - 1, "simultaneously dying Acolyte cannot trigger");
        source = Summon(p, id);
        End(g);
        deck = p.DeckZone.Count;
        Kill(g, Summon(p, "RLK_958"));
        Core2026Proof.Check(p.DeckZone.Count == deck - 1, "trigger persists into opposing turn");
        while (p.HandZone.Count < 10) Add(p, "CS2_182");
        Kill(g, Summon(p, "RLK_958"));
        Core2026Proof.Check(p.HandZone.Count == 10 && p.DeckZone.Count == deck - 2, "full hand burns draw");
    });

    private static void DeathOrder(Game game, string id)
    {
        var failures = new List<string>();
        foreach (string mode in new[] { "original", "clone", "snapshot" })
        foreach (var row in new[] { (spiritFirst: true, spirits: 1, damage: 1, enemy: 3),
            (spiritFirst: false, spirits: 1, damage: 0, enemy: 2),
            (spiritFirst: true, spirits: 2, damage: 2, enemy: 5) })
        {
            var branch = game.CloneExact(history: true); var owner = branch.Player1;
            foreach (var card in owner.DeckZone.ToArray()) { owner.DeckZone.Remove(card); owner.SetasideZone.Add(card); }
            owner.Hero.Damage = 0; owner.Hero.Fatigue = 0;
            var spirits = new List<Minion>();
            if (!row.spiritFirst) Play(branch, owner, id);
            for (int i = 0; i < row.spirits; i++) spirits.Add((Minion)Play(branch, owner, "CAP_803"));
            if (row.spiritFirst) Play(branch, owner, id);
            var original = branch;
            if (mode == "clone") branch = branch.CloneExact(history: true);
            if (mode == "snapshot") branch = Game.LoadSnapshot(branch.SaveSnapshot());
            owner = branch.Player1;
            foreach (var spirit in spirits) branch.IdEntityDic[spirit.Id].ToBeDestroyed = true;
            branch.DeathProcessingAndAuraUpdate();
            if (owner.Hero.Damage != row.damage || owner.Opponent.Hero.Damage != row.enemy
                || owner.Hero.Fatigue != row.spirits || owner.HandZone.Count != 0)
                failures.Add($"{mode}, spiritFirst={row.spiritFirst}, deaths={row.spirits}: damage={owner.Hero.Damage}/{row.damage}, enemy={owner.Opponent.Hero.Damage}/{row.enemy}, fatigue={owner.Hero.Fatigue}/{row.spirits}");
            if (mode != "original")
                Core2026Proof.Check(spirits.All(s => s.Zone.Type == Zone.PLAY) && original.Player1.Hero.Fatigue == 0,
                    "death resolution in clone/restored game leaves original intact");
        }
        Core2026Proof.Check(failures.Count == 0, "Acolyte death response play order: " + string.Join("; ", failures));
    }

    private static void GrantedDeathrattleOrder(Game game, string id)
    {
        foreach (string order in new[] { "UAHH", "AUHH", "UHAH", "UHHA" })
        {
            var branch = game.CloneExact(history: true); var owner = branch.Player1;
            Minion undead = null;
            // U = Undead, A = Acolyte, H = a separately timestamped Explorer's Hat.
            foreach (char step in order)
                if (step == 'U') undead = (Minion)Play(branch, owner, "RLK_958");
                else if (step == 'A') Play(branch, owner, id);
                else Play(branch, owner, "LOE_105", undead);
            Kill(branch, undead);
            var expected = order == "UHHA" ? new[] { "LOE_105", "LOE_105", "CS2_182" }
                : order == "UHAH" ? new[] { "LOE_105", "CS2_182", "LOE_105" }
                : new[] { "CS2_182", "LOE_105", "LOE_105" };
            Core2026Proof.Check(owner.HandZone.Select(c => c.Card.Id).SequenceEqual(expected),
                $"granted Deathrattles use enchantment application order ({order}): "
                + string.Join(",", owner.HandZone.Select(c => c.Card.Id)));
        }
    }

    private static void RemovedListener(Game game, string id)
    {
        foreach (string mode in new[] { "original", "clone", "snapshot" })
        foreach (bool ambusherFirst in new[] { true, false })
        {
            var branch = game.CloneExact(history: true); var owner = branch.Player1;
            Minion ambusher, sidekick;
            if (ambusherFirst)
            {
                ambusher = (Minion)Play(branch, owner, "FP1_026");
                sidekick = (Minion)Play(branch, owner, "RLK_958");
            }
            else
            {
                sidekick = (Minion)Play(branch, owner, "RLK_958");
                ambusher = (Minion)Play(branch, owner, "FP1_026");
            }
            var acolyte = Play(branch, owner, id);
            if (mode == "clone") branch = branch.CloneExact(history: true);
            if (mode == "snapshot") branch = Game.LoadSnapshot(branch.SaveSnapshot());
            owner = branch.Player1;
            int deck = owner.DeckZone.Count;
            branch.IdEntityDic[ambusher.Id].ToBeDestroyed = true;
            branch.IdEntityDic[sidekick.Id].ToBeDestroyed = true;
            branch.DeathProcessingAndAuraUpdate();
            Core2026Proof.Check(owner.BoardZone.Count == 0
                && owner.HandZone.Any(c => c.Id == acolyte.Id), "Ambusher returns sole survivor Acolyte");
            Core2026Proof.Check(owner.DeckZone.Count == deck - (ambusherFirst ? 0 : 1),
                $"removed Acolyte listener cannot resolve queued Sidekick draw ({mode}, ambusherFirst={ambusherFirst}): "
                + $"draws={deck - owner.DeckZone.Count}");
        }
    }

    private static void LegacyOrder(Game game, string id)
    {
        foreach (bool withAcolyte in new[] { false, true })
        {
            var branch = game.CloneExact(history: true); var owner = branch.Player1;
            foreach (var card in owner.DeckZone.ToArray()) { owner.DeckZone.Remove(card); owner.SetasideZone.Add(card); }
            var spirit = (Minion)Play(branch, owner, "CAP_803");
            spirit[GameTag.REBORN] = 0;
            if (withAcolyte) Play(branch, owner, id);
            Play(branch, owner, "EX1_595");
            Kill(branch, spirit);
            Core2026Proof.Check(owner.Hero.Damage == (withAcolyte ? 3 : 0)
                && owner.Opponent.Hero.Damage == (withAcolyte ? 3 : 2),
                "Cult Master keeps legacy FIFO alone; opted-in death shares play order with all listeners");
        }
    }
}
