using SabberStoneCore.Enums;
using SabberStoneCore.Kettle;

namespace HsSim;

// Structure rules over a game's whole power history (PowerHistory.Full), run on every golden game by `goldens check`.
// Pass/fail only, with no stored hash or baseline: a game that keeps its structure passes whatever the engine changes inside it,
// so two engine changes never conflict on this check. Only rules that hold on every golden game are here; a rule that needs an
// exception list belongs in the client-tier transport-invariants suite (tests/transport-invariants/HistoryShape.cs).
public static class HistoryShape
{
    // BLOCK_BALANCE: every BLOCK_END closes an open block, and no block is open at the end of the game.
    // SUB_SPELL_BALANCE: every SUB_SPELL_END closes an open sub-spell, a block closes only after the sub-spells opened inside it,
    //   and none is open at the end of the game.
    // ENTITY_REF: tag changes, SHOW/HIDE/CHANGE_ENTITY, block source and target and sub-spell source and targets name an entity
    //   already created (by CREATE_GAME or a FULL_ENTITY).
    // META_REF: every entity in a META_DATA's Info was already created (SHOW_BIG_CARD and CONTROLLER_AND_ZONE_CHANGE: the first
    //   slot only; the rest are parameters, as in the native client).
    // FULL_ENTITY_DUPLICATE: no FULL_ENTITY creates an id that already exists.
    // A RESET_GAME (rewind) creates the game again: the entities known before it are forgotten, and the FULL_ENTITYs that follow
    // it create them again.
    // Built against a prebuilt engine (no SOURCE_ENGINE) the check does nothing: rewinds and sub-spells are SoloStone entries.
    public static readonly string[] Rules = { "BLOCK_BALANCE", "SUB_SPELL_BALANCE", "ENTITY_REF", "META_REF", "FULL_ENTITY_DUPLICATE" };

    public readonly record struct Violation(string Rule, int Index, string Detail)
    {
        public override string ToString() => $"{Rule} at entry {Index}: {Detail}";
    }

    // What the checks walked through, summed over games, so a run shows it checked something.
    public sealed class Totals
    {
        public int Games, Entries, Blocks, SubSpells, Metadata, FullEntities, Resets;
        public override string ToString() =>
            $"games={Games} entries={Entries} blocks={Blocks} subSpells={SubSpells} metadata={Metadata} fullEntities={FullEntities} resets={Resets}";
    }

    public static List<Violation> Check(IReadOnlyList<IPowerHistoryEntry> history, Totals totals = null)
    {
        if (totals != null)
        {
            totals.Games++;
            totals.Entries += history.Count;
            totals.Blocks += history.Count(entry => entry is PowerHistoryBlockStart);
            totals.Metadata += history.Count(entry => entry is PowerHistoryMetaData);
            totals.FullEntities += history.Count(entry => entry is PowerHistoryFullEntity);
#if SOURCE_ENGINE
            totals.SubSpells += history.Count(entry => entry is PowerHistorySubSpellStart);
            totals.Resets += history.Count(entry => entry is PowerHistoryResetGame);
#endif
        }
        var violations = new List<Violation>();
#if SOURCE_ENGINE
        var known = new HashSet<int>();
        // Each open block with the sub-spells opened inside it and still open.
        var blocks = new Stack<int>();
        int subSpells = 0;
        void Flag(string rule, int index, string detail) => violations.Add(new Violation(rule, index, detail));
        void Reference(int index, int id, string what)
        {
            if (!known.Contains(id)) Flag("ENTITY_REF", index, $"{what} names entity {id} before it exists");
        }
        void Create(PowerHistoryCreateGame create)
        {
            known.Clear();
            known.Add(create.Game.Id);
            foreach (var player in create.Players) known.Add(player.PowerEntity.Id);
        }
        for (int i = 0; i < history.Count; i++)
        {
            switch (history[i])
            {
                case PowerHistoryCreateGame create:
                    Create(create);
                    break;
                case PowerHistoryResetGame reset:
                    Create(reset.CreateGame);
                    break;
                case PowerHistoryFullEntity full:
                    if (!known.Add(full.Entity.Id)) Flag("FULL_ENTITY_DUPLICATE", i, $"FULL_ENTITY creates entity {full.Entity.Id} again");
                    break;
                case PowerHistoryShowEntity show:
                    Reference(i, show.Entity.Id, "SHOW_ENTITY");
                    break;
                case PowerHistoryHideEntity hide:
                    Reference(i, hide.EntityID, "HIDE_ENTITY");
                    break;
                case PowerHistoryChangeEntity change:
                    Reference(i, change.Entity.Id, "CHANGE_ENTITY");
                    break;
                case PowerHistoryTagChange tag:
                    Reference(i, tag.EntityId, $"TAG_CHANGE {tag.Tag}");
                    break;
                case PowerHistoryBlockStart start:
                    Reference(i, start.Source, $"BLOCK_START {start.BlockType} source");
                    if (start.Target != 0) Reference(i, start.Target, $"BLOCK_START {start.BlockType} target");
                    blocks.Push(subSpells);
                    subSpells = 0;
                    break;
                case PowerHistoryBlockEnd:
                    if (blocks.Count == 0)
                    {
                        Flag("BLOCK_BALANCE", i, "BLOCK_END without an open block");
                        break;
                    }
                    if (subSpells > 0) Flag("SUB_SPELL_BALANCE", i, $"BLOCK_END while {subSpells} sub-spell(s) opened in the block are open");
                    subSpells = blocks.Pop();
                    break;
                case PowerHistorySubSpellStart sub:
                    if (sub.Source != 0) Reference(i, sub.Source, "SUB_SPELL_START source");
                    foreach (int target in sub.Targets) Reference(i, target, "SUB_SPELL_START target");
                    subSpells++;
                    break;
                case PowerHistorySubSpellEnd:
                    if (subSpells == 0) Flag("SUB_SPELL_BALANCE", i, "SUB_SPELL_END without an open sub-spell");
                    else subSpells--;
                    break;
                case PowerHistoryMetaData meta:
                    var info = meta.Info ?? new List<int>();
                    var references = meta.Type is MetaDataType.SHOW_BIG_CARD or MetaDataType.CONTROLLER_AND_ZONE_CHANGE
                        ? info.Take(1) : info;
                    foreach (int id in references)
                        if (!known.Contains(id)) Flag("META_REF", i, $"META_DATA {meta.Type} names entity {id} before it exists");
                    break;
            }
        }
        if (blocks.Count > 0) Flag("BLOCK_BALANCE", history.Count, $"{blocks.Count} block(s) open at the end of the game");
        if (subSpells > 0 || blocks.Any(open => open > 0))
            Flag("SUB_SPELL_BALANCE", history.Count, "sub-spell(s) open at the end of the game");
#endif
        return violations;
    }
}
