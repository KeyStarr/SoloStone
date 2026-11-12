using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class B14F5CoreIdentityProof
{
    [GroupProof("b14-f5-core-identity-cards", ProofKind.Cards)]
    internal static void Run()
    {
        foreach (string id in new[] { "CORE_AT_037", "CORE_BT_035", "CORE_BT_156", "CORE_EX1_154", "CORE_EX1_160",
            "CORE_OG_047", "CORE_ONY_018", "CORE_TSC_650", "EDR_001", "RLK_048", "Core_LOE_115", "Core_UNG_072" })
            StandardSetsProof.Case(id, Scenario);
    }

    private static void Scenario(Game g, Controller p, string id)
    {
        switch (id)
        {
            case "CORE_AT_037":
                Choose(g, p, id, 1, p.Opponent.Hero);
                Core2026Proof.Check(p.Opponent.Hero.Damage == 2 && p.BoardZone.Count == 0, "Roots damage branch only");
                Choose(g, p, id, 2);
                Core2026Proof.Check(p.BoardZone.Count == 2 && p.BoardZone.All(m => m.Card.Id == "AT_037t" && m.AttackDamage == 1 && m.Health == 1), "two Saplings");
                StandardSetsProof.ClearBoard(g, p); p.ChooseBoth = true;
                Choose(g, p, id, -1, p.Opponent.Hero);
                Core2026Proof.Check(p.Opponent.Hero.Damage == 4 && p.BoardZone.Count == 2, "Roots Choose Both"); p.ChooseBoth = false;
                break;
            case "CORE_EX1_154":
                var enemy = Summon(p.Opponent, "CS2_200");
                int deck = p.DeckZone.Count;
                Choose(g, p, id, 1, enemy); Core2026Proof.Check(enemy.Damage == 3 && p.DeckZone.Count == deck, "Wrath three damage");
                Choose(g, p, id, 2, enemy); Core2026Proof.Check(enemy.Damage == 4 && p.DeckZone.Count == deck - 1, "Wrath one and draw");
                var held = Add(p, id);
                Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == held.Id && t.Target is Hero), "Wrath excludes heroes");
                break;
            case "CORE_EX1_160":
                Choose(g, p, id, 1); var panther = p.BoardZone.Single();
                Core2026Proof.Check(panther.Card.Id == "EX1_160t" && panther.AttackDamage == 3 && panther.Health == 2, "export a is Panther");
                Choose(g, p, id, 2); Core2026Proof.Check(panther.AttackDamage == 4 && panther.Health == 3, "export b buffs");
                panther.Silence(); Core2026Proof.Check(panther.AttackDamage == 3 && panther.Health == 2, "buff silenced");
                StandardSetsProof.ClearBoard(g, p); p.ChooseBoth = true; Choose(g, p, id, -1);
                Core2026Proof.Check(p.BoardZone.Single().AttackDamage == 4 && p.BoardZone.Single().Health == 3, "Choose Both summons before buff"); p.ChooseBoth = false;
                break;
            case "CORE_OG_047":
                Choose(g, p, id, 1); Core2026Proof.Check(p.Hero.AttackDamage == 4 && p.Hero.Armor == 0, "Feral Rage Attack");
                Choose(g, p, id, 2); Core2026Proof.Check(p.Hero.AttackDamage == 4 && p.Hero.Armor == 8, "Feral Rage Armor");
                PoolPilotProof.Resume(g, End); Core2026Proof.Check(p.Hero.AttackDamage == 0 && p.Hero.Armor == 8, "temporary Attack expires, Armor persists");
                break;
            case "CORE_ONY_018":
                p.Hero.Damage = 12; Choose(g, p, id, 1);
                Core2026Proof.Check(p.Hero.Damage == 4 && p.Opponent.Hero.Damage == 0, "Boomkin heals owner only");
                Choose(g, p, id, 2, p.Opponent.Hero); Core2026Proof.Check(p.Opponent.Hero.Damage == 4 && p.Hero.Damage == 4, "Boomkin damage branch");
                break;
            case "CORE_TSC_650":
                Choose(g, p, id, 1); var orca = p.BoardZone.Single();
                Core2026Proof.Check(orca.Card.Id == "TSC_650t" && orca.AttackDamage == 6 && orca.Health == 6 && orca.HasTaunt, "6/6 Taunt Orca");
                StandardSetsProof.ClearBoard(g, p); Choose(g, p, id, 2);
                Core2026Proof.Check(p.BoardZone.Count == 6 && p.BoardZone.All(m => m.Card.Id == "TSC_650t4" && m.IsRush && m.AttackDamage == 1 && m.Health == 1), "six 1/1 Rush Otters");
                Choose(g, p, id, 2); Core2026Proof.Check(p.BoardZone.Count == 7, "limited capacity summons only one");
                var blocked = Add(p, id); Core2026Proof.Check(!p.Options().Any(t => t.Source?.Id == blocked.Id), "full board disables both branches");
                break;
            case "CORE_BT_035":
                int count = p.DeckZone.Count; Play(g, p, id);
                Core2026Proof.Check(p.Hero.AttackDamage == 2 && p.DeckZone.Count == count - 1 && p.HandZone.Count == 1, "Chaos Strike Attack and draw");
                PoolPilotProof.Resume(g, End); Core2026Proof.Check(p.Hero.AttackDamage == 0, "Chaos Strike expires after clone/load");
                break;
            case "CORE_BT_156":
                var vile = (Minion)Play(g, p, id);
                Core2026Proof.Check(vile.IsDormant && vile.Untouchable && vile[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "Vilefiend enters Dormant");
                End(g); Core2026Proof.Check(vile[GameTag.TAG_SCRIPT_DATA_NUM_1] == 2, "opponent turn does not tick");
                End(g); Core2026Proof.Check(vile.IsDormant && vile[GameTag.TAG_SCRIPT_DATA_NUM_1] == 1, "first owner turn");
                PoolPilotProof.Resume(g, next => { End(next); End(next); });
                Core2026Proof.Check(!vile.IsDormant && vile.IsRush && !vile.IsExhausted, "second owner turn wakes with Rush");
                vile.Silence(); Core2026Proof.Check(!vile.IsRush, "Silence removes Rush");
                break;
            case "RLK_048":
                var own = Summon(p, "CS2_200"); var other = Summon(p.Opponent, "CS2_200");
                Play(g, p, id);
                Core2026Proof.Check(own.AttackDamage == 7 && own.Health == 8 && own.CantBeTargetedBySpells && own.CantBeTargetedByHeroPowers
                    && other.AttackDamage == 6 && !other.CantBeTargetedBySpells, "Shell +1/+1 and both Elusive protections, own board only: " + own.AttackDamage + "/" + own.Health + "/" + own.CantBeTargetedBySpells + "/" + own.CantBeTargetedByHeroPowers + "; other=" + other.AttackDamage);
                PoolPilotProof.Resume(g, next => ((Minion)next.IdEntityDic[own.Id]).Silence());
                Core2026Proof.Check(own.AttackDamage == 6 && own.Health == 7 && !own.CantBeTargetedBySpells, "Shell Silence removes all grants");
                break;
            case "Core_LOE_115":
            case "Core_UNG_072":
                B14F5DiscoverProof.Run(g, p, id);
                break;
            case "EDR_001":
                B14F5DreamProof.Run(g, p, id);
                break;
        }
    }

    internal static IPlayable Choose(Game g, Controller p, string id, int branch, ICharacter target = null)
    {
        var card = Add(p, id); p.BaseMana = 10; p.UsedMana = 0; p.TemporaryMana = 0;
        var expected = CoreIdentityProof.Options.Single(row => row.Parent == id);
        Core2026Proof.Check(card.ChooseOnePlayables.Select(c => c.Card.Id).SequenceEqual(new[] { expected.First, expected.Second }), "real option actors");
        var task = p.Options().FirstOrDefault(t => t.Source?.Id == card.Id && t.ChooseOne == branch && t.Target?.Id == target?.Id);
        Core2026Proof.Check(task != null, "option enumerated: " + id + "/" + branch);
        PoolPilotProof.Resume(g, next => Core2026Proof.Check(next.Process(next.ControllerById(p.Id).Options().First(t => t.Source?.Id == card.Id
            && t.ChooseOne == branch && t.Target?.Id == target?.Id)), "Core choice accepted after clone/load"));
        return card;
    }
}
