using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

// Inspect pinned client IL without loading Unity or depending on decompiler text.
internal static class PackHookIlChecks
{
    internal static void Run(string root, Action<bool, string> check)
    {
        using var pe = new PEReader(File.OpenRead(Path.Combine(root, "references/game/Assembly-CSharp.dll")));
        var metadata = pe.GetMetadataReader();
        TypeDefinition Type(string name, string space = "") => metadata.GetTypeDefinition(metadata.TypeDefinitions.Single(handle => {
            var type = metadata.GetTypeDefinition(handle);
            return type.GetDeclaringType().IsNil && metadata.GetString(type.Name) == name && metadata.GetString(type.Namespace) == space;
        }));
        MethodDefinition Method(TypeDefinition type, string name) => metadata.GetMethodDefinition(type.GetMethods().Single(handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name) == name));
        byte[] Il(MethodDefinition method) => pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes();
        var fake = Method(Type("PackOpening"), "OnFakeBoosterOpened");
        var open = Method(Type("PackOpening"), "OpenBooster");
        var ready = Method(Type("PackOpeningDirector"), "NewHighlightsReadyImpl");
        var attach = Method(Type("MassPackOpeningHiddenCards", "Game.PackOpening"), "AttachMassPackOpeningHighlightCards");
        var state = metadata.GetTypeDefinition(Type("PackOpeningDirector").GetNestedTypes().Single(handle =>
            metadata.GetString(metadata.GetTypeDefinition(handle).Name).StartsWith("<NewHighlightsReadyImpl>", StringComparison.Ordinal)));
        var moveNext = Method(state, "MoveNext");
        Console.WriteLine($"PACK_IL fake={Il(fake).Length} open={Il(open).Length} ready={Il(ready).Length} attach={Il(attach).Length} MoveNext={Il(moveNext).Length}");
        check(Il(fake).Length == 14 && Il(ready).Length == 14
            && (fake.ImplAttributes & MethodImplAttributes.NoInlining) == 0
            && (ready.ImplAttributes & MethodImplAttributes.NoInlining) == 0,
            "pinned iterator factories are 14-byte inline candidates without NoInlining");
        var calls = Calls(Il(open), metadata);
        int factory = calls.IndexOf("PackOpening.OnFakeBoosterOpened");
        check(calls.Count(call => call == "PackOpening.OnFakeBoosterOpened") == 1
            && factory >= 0 && calls[factory + 1] == "MonoBehaviour.StartCoroutine",
            "pinned OpenBooster has exactly one fake factory call immediately consumed by StartCoroutine");
        var pageCalls = Calls(Il(moveNext), metadata);
        check(pageCalls.Count(call => call == "MassPackOpeningHiddenCards.AttachMassPackOpeningHighlightCards") == 1
            && pageCalls.Count(call => call == "HiddenCards.EnableReveal") == 1
            && pageCalls.IndexOf("MassPackOpeningHiddenCards.AttachMassPackOpeningHighlightCards") < pageCalls.IndexOf("HiddenCards.EnableReveal"),
            "pinned MoveNext exposes exact attach and post-delay readiness call sites");
    }

    internal static List<string> Calls(byte[] il, MetadataReader metadata)
    {
        var opcodes = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.FieldType == typeof(OpCode)).Select(field => (OpCode)field.GetValue(null))
            .ToDictionary(op => unchecked((ushort)op.Value));
        var calls = new List<string>();
        for (int i = 0; i < il.Length;)
        {
            ushort code = il[i++];
            if (code == 0xfe) code = (ushort)(0xfe00 | il[i++]);
            var op = opcodes[code];
            if (op == OpCodes.Call || op == OpCodes.Callvirt)
            {
                var handle = MetadataTokens.EntityHandle(BitConverter.ToInt32(il, i));
                if (handle.Kind == HandleKind.MethodDefinition)
                {
                    var method = metadata.GetMethodDefinition((MethodDefinitionHandle)handle);
                    calls.Add(metadata.GetString(metadata.GetTypeDefinition(method.GetDeclaringType()).Name) + "." + metadata.GetString(method.Name));
                }
                else if (handle.Kind == HandleKind.MemberReference)
                {
                    var member = metadata.GetMemberReference((MemberReferenceHandle)handle);
                    string type = member.Parent.Kind == HandleKind.TypeReference
                        ? metadata.GetString(metadata.GetTypeReference((TypeReferenceHandle)member.Parent).Name) : "generic";
                    calls.Add(type + "." + metadata.GetString(member.Name));
                }
            }
            i += op.OperandType switch {
                OperandType.InlineNone => 0,
                OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
                OperandType.InlineVar => 2,
                OperandType.InlineI8 or OperandType.InlineR => 8,
                OperandType.InlineSwitch => 4 + 4 * BitConverter.ToInt32(il, i),
                _ => 4
            };
        }
        return calls;
    }
}
