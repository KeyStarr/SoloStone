global using HarmonyLib;
global using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

partial class NativeBundleHarness
{
    private sealed class ProductSectionDataModel
    {
        public string SectionId;
        public bool DisplayDivder { get; set; }
    }
    private sealed class DividerState { public ProductSectionDataModel Current = null; }

    private static void RunDeckDividers(Action<bool, string> check)
    {
        // The pinned iterator must still call DisplayDivder at least twice (the guard DeckSectionDividers rewrites). The exact
        // branch shape is enforced when the transpiler registers (see the Reject cases below), so no IL offsets are pinned here.
        string root = RepoRoot.Find("references/game/Assembly-CSharp.dll");
        string client = root == null ? null : Path.Combine(root, "references/game/Assembly-CSharp.dll");
        if (client == null) Console.WriteLine("SKIP pinned section loader IL shape: needs the client's Assembly-CSharp.dll (run.py --client)");
        else using (var game = new PEReader(File.OpenRead(client))) {
            var metadata = game.GetMetadataReader();
            var iterator = metadata.TypeDefinitions.Select(metadata.GetTypeDefinition)
                .Single(t => metadata.GetString(t.Name) == "<LoadSectionsCoroutine>d__65");
            var moveNext = iterator.GetMethods().Select(metadata.GetMethodDefinition).Single(m => metadata.GetString(m.Name) == "MoveNext");
            byte[] il = game.GetMethodBody(moveNext.RelativeVirtualAddress).GetILBytes();
            var reads = new List<int>();
            for (int at = 0; at + 5 <= il.Length; at++) {
                if (il[at] != 0x6f || il[at + 4] != 0x06) continue; // callvirt on a MethodDef token
                int row = BitConverter.ToInt32(il, at + 1) & 0xffffff;
                if (row < 1 || row > metadata.GetTableRowCount(TableIndex.MethodDef)) continue;
                var called = metadata.GetMethodDefinition(MetadataTokens.MethodDefinitionHandle(row));
                if (metadata.GetString(called.Name) == "get_DisplayDivder") reads.Add(at);
            }
            // Shape, relative to the two reads: ldarg.0 ldfld <read> brtrue.s ... ldloc.3 <read> brfalse (the guard DeckSectionDividers rewrites).
            check(reads.Count == 2 && il[reads[0] - 6] == 0x02 && il[reads[0] - 5] == 0x7b && il[reads[0] + 5] == 0x2d
                && il[reads[1] - 1] == 0x09 && il[reads[1] + 5] == 0x39,
                "pinned section loader has exactly the matched null/display divider branch shape");
        }

        var sections = BuildSections();
        foreach (var hero in NativeShopTabs.DeckClasses) {
            var rows = sections.SkipWhile(s => s.InternalName != hero.Id).Skip(1)
                .TakeWhile(s => !s.Attributes.ContainsKey("subtab_id") && !s.Attributes.ContainsKey("tab_id")).ToArray();
            var models = rows.Select(s => new ProductSectionDataModel {
                SectionId = s.InternalName, DisplayDivder = s.Attributes.ContainsKey("divider_options") }).ToArray();
            check(rows.Length > 0 && rows[0].Label == "Meta" && rows.Count(s => s.Label == "Meta") == 1
                && rows.Count(s => s.Label == "Fan favourite") == 1, hero.Id + ": exactly one plain Meta and Fan favourite header");
            check(models.Select((m, i) => ShouldFetchDivider(i == 0 ? null : models[i - 1], m)).All(v => v),
                hero.Id + ": first Meta and subsequent native headers fetched once each");
        }
        foreach (var id in new[] { "local-packs", "local-skins-hero_01", "local-themes-owned", "local-boards-sale", "retail", null }) {
            check(!ShouldFetchDivider(null, new ProductSectionDataModel { SectionId = id, DisplayDivder = true }),
                "first non-deck section unchanged even with divider options: " + id);
        }
        var deck = new ProductSectionDataModel { SectionId = "local-decks-hero_08-meta", DisplayDivder = true };
        check(!ShouldFetchDivider(null, null), "missing first model has no divider");
        Enabled = false;
        check(!ShouldFetchDivider(null, deck), "disabled shop keeps native first-section behavior");
        Enabled = true;
        deck.DisplayDivder = false;
        check(!ShouldFetchDivider(null, deck), "DeckHeaders=none keeps first deck divider absent");
        foreach (bool current in new[] { false, true })
            foreach (bool previous in new[] { false, true }) {
                deck.DisplayDivder = current;
                check(ShouldFetchDivider(new ProductSectionDataModel { DisplayDivder = previous }, deck) == (current || previous),
                    "between-section divider behavior unchanged");
            }

        var generator = new DynamicMethod("divider_labels", typeof(void), Type.EmptyTypes).GetILGenerator();
        Label skip = generator.DefineLabel(), fetch = generator.DefineLabel(), entry = generator.DefineLabel();
        var getterMethod = typeof(ProductSectionDataModel).GetProperty(nameof(ProductSectionDataModel.DisplayDivder)).GetMethod;
        CodeInstruction[] Guard() => new[] {
            new CodeInstruction(OpCodes.Ldloc_3), new CodeInstruction(OpCodes.Brfalse, skip),
            new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldfld, typeof(DividerState).GetField(nameof(DividerState.Current))),
            new CodeInstruction(OpCodes.Callvirt, getterMethod), new CodeInstruction(OpCodes.Brtrue_S, fetch),
            new CodeInstruction(OpCodes.Ldloc_3), new CodeInstruction(OpCodes.Callvirt, getterMethod), new CodeInstruction(OpCodes.Brfalse, skip) };
        var code = Guard(); code[0].labels.Add(entry);
        var rewritten = DeckSectionDividers(code).ToArray();
        check(rewritten.Length == 5 && ReferenceEquals(rewritten[0], code[0]) && rewritten[0].labels.Single().Equals(entry)
            && ReferenceEquals(rewritten[1], code[2]) && ReferenceEquals(rewritten[2], code[3])
            && rewritten[3].Calls(((Func<ProductSectionDataModel, ProductSectionDataModel, bool>)ShouldFetchDivider).Method)
            && ReferenceEquals(rewritten[4], code[8]), "iterator rewrite keeps model loads, entry labels and native skip target");
        code = Guard(); code[5].opcode = OpCodes.Brtrue; // Harmony normalizes short branches before calling transpilers.
        check(DeckSectionDividers(code).Count() == 5, "Harmony normalized long branch matches too");
        // Harmony can give each incoming branch its own label on the same target instruction.
        code = Guard(); Label alias = generator.DefineLabel(); code[1].operand = alias;
        var target = new CodeInstruction(OpCodes.Nop); target.labels.Add(skip); target.labels.Add(alias);
        check(DeckSectionDividers(code.Append(target)).Count() == 6, "distinct Harmony labels on the same skip target match");
        bool Reject(IEnumerable<CodeInstruction> changed) {
            try { DeckSectionDividers(changed).ToArray(); return false; } catch (InvalidOperationException) { return true; }
        }
        check(Reject(Array.Empty<CodeInstruction>()) && Reject(Guard().Concat(Guard())), "missing or duplicate native guards fail registration");
        code = Guard(); code[1].operand = generator.DefineLabel();
        check(Reject(code), "different skip targets fail registration");
        code = Guard(); code[1].labels.Add(entry);
        check(Reject(code), "unexpected branch into removed guard fails registration");
        code = Guard(); code[4].blocks.Add(new ExceptionBlock(ExceptionBlockType.BeginExceptionBlock));
        check(Reject(code), "unexpected exception boundary fails registration");
    }
}
