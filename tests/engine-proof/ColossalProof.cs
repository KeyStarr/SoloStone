using HsSim;
using SabberStoneCore.Actions;
using SabberStoneCore.EffectSystem;
using SabberStoneCore.Enchants;
using SabberStoneCore.Enums;
using SabberStoneCore.Model;
using SabberStoneCore.Model.Entities;
using SabberStoneCore.Tasks.SimpleTasks;
using SabberStoneCore.Tasks.PlayerTasks;
using static Core2026Proof;

internal static class ColossalProof
{
    private static readonly string[] Heads = Enumerable.Range(1, 4).Select(i => "CATA_432t" + i).ToArray();
    private static Game New() => HeraldProof.New(CardClass.PALADIN);
    private static void Layout(Minion body)
    {
        Core2026Proof.Check(body.Controller.BoardZone.Select(m => m.Card.Id).SequenceEqual(new[] { Heads[2], Heads[0], "CATA_432", Heads[1], Heads[3] }), "Lean_placement +4 order");
        Core2026Proof.Check(body.Controller.BoardZone.Select(m => m.ZonePosition).SequenceEqual(Enumerable.Range(0, 5)), "Lean_placement physical positions");
        var heads = ColossalRules.AppendagesOf(body);
        Core2026Proof.Check(heads.Count() == 4 && heads.All(h => h[GameTag.CREATOR] == body.Id && h[GameTag.DISPLAYED_CREATOR] == body.Id && ColossalRules.BodyOf(h) == body), "body links");
    }
    internal static void Run()
    {
        var restored = new HistoryDefinition(Cards.FromId("CATA_432").Clone()).Materialize();
        var restoredGame = New();
        var restoredBody = (Minion)Entity.FromCard(restoredGame.Player1, restored);
        Generic.SummonBlock(restoredGame, restoredBody, -1, null);
        Layout(restoredBody);
        var g = New(); var p = g.Player1; var body = (Minion)Play(g, p, "CATA_432"); Layout(body);
        var clone = g.CloneExact(history: true);
        Core2026Proof.Check(Canonical.State(g) == Canonical.State(clone), "midgame exact clone");
        var head = ColossalRules.AppendagesOf(body).First();
        Kill(clone, (Minion)clone.IdEntityDic[head.Id]);
        Core2026Proof.Check(ColossalRules.AppendagesOf(body).Count() == 4 && ColossalRules.AppendagesOf((Minion)clone.IdEntityDic[body.Id]).Count() == 3, "clone links resolve inside clone");
        body.Silence(); g.DeathProcessingAndAuraUpdate();
        Core2026Proof.Check(ColossalRules.AppendagesOf(body).Count() == 4, "Lean_body Silence leaves heads");
        Kill(g, body);
        Core2026Proof.Check(p.BoardZone.Count == 4 && p.BoardZone.All(h => ColossalRules.BodyOf(h) == null), "Lean_body death leaves heads, absent body unlinked");
        foreach (string path in new[] { "context", "task", "copy", "resurrect" })
        {
            g = New(); p = g.Player1;
            if (path == "context") new EffectContext(g, p.Id, p.Hero.Id).Summon("CATA_432");
            if (path == "task") new SummonTask("CATA_432").Process(g, p, p.Hero, null, new TaskStack());
            if (path == "copy" || path == "resurrect")
            {
                body = Summon(p, "CATA_432");
                foreach (var h in ColossalRules.AppendagesOf(body).ToArray()) p.SetasideZone.Add(p.BoardZone.Remove(h));
                if (path == "resurrect") Kill(g, body);
                else p.SetasideZone.Add(p.BoardZone.Remove(body));
                new SummonCopyTask(EntityType.TARGET).Process(g, p, p.Hero, body, new TaskStack());
            }
            g.DeathProcessingAndAuraUpdate(); body = p.BoardZone.Single(m => m.Card.Id == "CATA_432"); Layout(body);
        }
        g = New(); p = g.Player1; body = Summon(p, "CATA_432");
        foreach (var h in ColossalRules.AppendagesOf(body).ToArray()) p.SetasideZone.Add(p.BoardZone.Remove(h));
        new SummonCopyTask(EntityType.TARGET).Process(g, p, p.Hero, body, new TaskStack());
        g.DeathProcessingAndAuraUpdate();
        var boardCopy = p.BoardZone.Single(m => m.Card.Id == "CATA_432" && m.Id != body.Id);
        Core2026Proof.Check(ColossalRules.AppendagesOf(boardCopy).Count() == 4 && !ColossalRules.AppendagesOf(body).Any()
            && p.BoardZone.Skip(1).Select(m => m.Card.Id).SequenceEqual(new[] { Heads[2], Heads[0], "CATA_432", Heads[1], Heads[3] }),
            "Lean_board-source copy summons fresh linked heads beside new body");
        for (int occupied = 3; occupied <= 6; occupied++)
        {
            g = New(); p = g.Player1; var location = LocationProofs.Location(p);
            for (int i = 1; i < occupied; i++) Summon(p, "CS2_182");
            body = (Minion)Play(g, p, "CATA_432");
            Core2026Proof.Check(p.BoardZone.SlotCount == 7 && ColossalRules.AppendagesOf(body).OrderBy(m => m.Id).Select(m => m.Card.Id).SequenceEqual(Heads.Take(6 - occupied)), "Lean_partial board keeps first k appendages including location slot");
            Core2026Proof.Check(p.BoardZone.GetSlot(location.ZonePosition) == location, "location stays in physical board");
        }
        g = New(); p = g.Player1; while (!p.BoardZone.IsFull) Summon(p, "CS2_182");
        var held = Add(p, "CATA_432"); var before = Canonical.State(g);
        Core2026Proof.Check(!held.IsPlayableByPlayer && !g.Process(PlayCardTask.Any(p, held)) && before == Canonical.State(g), "full board body rejected without appendages");
        g = New(); p = g.Player1; var ordinary = Summon(p, "CS2_182"); before = Canonical.State(g); var history = Canonical.History(g);
        ColossalRules.OnBodyEntered(ordinary);
        Core2026Proof.Check(before == Canonical.State(g) && history == Canonical.History(g), "undeclared body byte-identical no-op");
        Lean_Ordering(); HeraldFactor(); CopySemantics();
        Console.WriteLine("PASS Colossal primitive: Lean_layout/partial/full/order/body-removal, play/context/task/copy/resurrect, links, clone, undeclared no-op, Herald guard");
    }
    private static void CopySemantics()
    {
        foreach (bool task in new[] { false, true })
        {
            Minion Copy(Minion source)
            {
                var p = source.Controller;
                if (!task) return (Minion)Generic.Copy(p, p.Hero, source, Zone.PLAY);
                new SummonCopyTask(EntityType.TARGET).Process(source.Game, p, p.Hero, source, new TaskStack());
                return p.BoardZone.Single(m => m.Card.Id == source.Card.Id && m.Id != source.Id);
            }
            foreach (bool silence in new[] { false, true })
            foreach (var row in new (string Head, string Ordinary, Func<Minion, bool> Has, Action<Minion> Remove, bool GenericRestores)[] {
                ("CATA_432t1", "CS2_179", m => m.HasTaunt, m => m.HasTaunt = false, false),
                ("CATA_432t2", "ICC_220", m => m.HasLifeSteal, m => m.HasLifeSteal = false, true),
                ("CATA_432t3", "NEW1_023", m => m.CantBeTargetedBySpells, m => m.CantBeTargetedBySpells = false, false),
                ("CATA_432t4", "EX1_008", m => m.HasDivineShield, m => m.HasDivineShield = false, true)
            })
            {
                var g = New(); var p = g.Player1;
                var ordinary = Summon(p, row.Ordinary);
                Core2026Proof.Check(row.Has(ordinary), "ordinary copy control has printed keyword " + row.Ordinary);
                if (silence) ordinary.Silence(); else row.Remove(ordinary);
                var control = Copy(ordinary);
                bool expected = !silence && !task && row.GenericRestores;
                Core2026Proof.Check(row.Has(control) == expected && control.IsSilenced == silence, "ordinary copy rule " + row.Ordinary);
                StandardSetsProof.ParkBoard(p);
                var body = Summon(p, "CATA_432");
                if (silence) body.Silence();
                else Kill(g, ColossalRules.AppendagesOf(body).Single(h => h.Card.Id == row.Head));
                foreach (var h in ColossalRules.AppendagesOf(body).ToArray()) p.SetasideZone.Add(p.BoardZone.Remove(h));
                var copy = Copy(body);
                Core2026Proof.Check(row.Has(copy) == expected && copy.IsSilenced == control.IsSilenced,
                    "Chromatus follows ordinary keyword loss/Silence copy rule " + row.Head);
            }
            var game = New(); var owner = game.Player1;
            var leper = Summon(owner, "EX1_029"); leper.Silence();
            var leperCopy = Copy(leper);
            Core2026Proof.Check(!leperCopy.HasDeathrattle && leperCopy.IsSilenced, "ordinary silenced Deathrattle copy rule");
            StandardSetsProof.ParkBoard(owner);
            var head = Summon(owner, "CATA_432t1"); ColossalRules.ConfigureAppendage(head, 2); head.Silence();
            var headCopy = Copy(head); ColossalRules.ConfigureAppendage(headCopy, 2);
            Core2026Proof.Check(headCopy.HasDeathrattle == leperCopy.HasDeathrattle && headCopy.IsSilenced == leperCopy.IsSilenced
                && headCopy.AttackDamage == head.Card.ATK && headCopy.BaseHealth == head.Card.Health,
                "silenced scaled appendage follows ordinary Deathrattle copy rule without rescaling");
        }
        Console.WriteLine("PASS Colossal copies: Generic.Copy/SummonCopyTask ordinary keyword loss/Silence controls, Chromatus parity, silenced scaled appendage Deathrattle parity");
    }
    private static void Lean_Ordering()
    {
        foreach (bool played in new[] { true, false })
        {
            var g = New(); var p = g.Player1; bool onPlay = false, after = false;
            var definition = Cards.FromId("CATA_432").Clone();
            definition.Power = new Power { PowerTask = new CustomTask((game, owner, source, target, stack) => {
                Core2026Proof.Check(ColossalRules.AppendagesOf((Minion)source).Count() == 4, "Lean_appendages before Battlecry"); owner.Hero.Armor++;
            }) };
            definition.Tags[GameTag.BATTLECRY] = 1;
            var body = (Minion)(played ? Generic.DrawCard(p, definition) : Entity.FromCard(p, definition));
            g.TriggerManager.PlayMinionTrigger += entity => {
                if (entity.Id == body.Id) { onPlay = true; Core2026Proof.Check(ColossalRules.AppendagesOf(body).Count() == 4, "Lean_appendages before OnPlay"); }
            };
            var order = new List<string>();
            g.TriggerManager.AfterSummonTrigger += entity => {
                if (entity.Id == body.Id) { after = true; Core2026Proof.Check(ColossalRules.AppendagesOf(body).Count() == 4, "Lean_appendages before AFTER_SUMMON"); }
                order.Add(entity.Card.Id);
            };
            if (played) PlayEntity(g, p, body); else Generic.SummonBlock(g, body, -1, null);
            Core2026Proof.Check(after && (!played || onPlay && p.Hero.Armor == 1), "Lean_order callbacks exercised");
            Core2026Proof.Check(order.SequenceEqual(Heads.Append("CATA_432")), "Lean_head AFTER_SUMMON before body");
        }
    }
    private static void HeraldFactor()
    {
        for (int tier = 0; tier <= 2; tier++)
        {
            var g = New(); var p = g.Player1;
            var definition = Cards.FromId("CATA_432").Clone(); definition.Tags[(GameTag)4538] = 1;
            p[HeraldRules.Progress] = tier * 2;
            var body = (Minion)Entity.FromCard(p, definition); Generic.SummonBlock(g, body, -1, null);
            foreach (var h in ColossalRules.AppendagesOf(body))
            {
                Core2026Proof.Check(h.AttackDamage == h.Card.ATK * (1 << tier) && h.BaseHealth == h.Card.Health * (1 << tier), "Herald body tag uses current tier");
                ColossalRules.ConfigureAppendage(h, 2);
                Core2026Proof.Check(h.BaseHealth == h.Card.Health * (1 << tier), "instance guard prevents double scale");
            }
            Core2026Proof.Check(Cards.FromId("CATA_432")[(GameTag)4538] == 0, "test configuration does not mutate shared definition");
            var effectHead = Cards.FromId("CATA_432t1").Clone();
            effectHead.Tags[GameTag.TAG_SCRIPT_DATA_NUM_1] = 3;
            var original = (Minion)Entity.FromCard(p, effectHead);
            ColossalRules.ConfigureAppendage(original, tier);
            var copied = (Minion)Generic.Copy(p, p.Hero, original, Zone.PLAY);
            ColossalRules.ConfigureAppendage(copied, 2);
            Core2026Proof.Check(copied.BaseHealth == effectHead.Health * (1 << tier)
                && copied.AttackDamage == effectHead.ATK * (1 << tier)
                && copied[GameTag.TAG_SCRIPT_DATA_NUM_1] == 3 * (1 << tier), "copy preserves realized factor and effect magnitude without double scaling");
            var magnitude = (Minion)Entity.FromCard(p, Cards.FromId("CATA_580t"));
            ColossalRules.ConfigureAppendage(magnitude, tier);
            Core2026Proof.Check(magnitude[GameTag.TAG_SCRIPT_DATA_NUM_1] == magnitude.Card[GameTag.TAG_SCRIPT_DATA_NUM_1] * (1 << tier), "effect magnitude scaling");
        }
    }
}
