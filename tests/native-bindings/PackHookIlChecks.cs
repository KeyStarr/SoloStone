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
// The pack-opening hooks patch methods of the shipped client. Two of them (OnFakeBoosterOpened, NewHighlightsReadyImpl)
// are 14-byte iterator factories without NoInlining, so the runtime may inline them and bypass a method patch (once, a
// mass open received only five cards). The hooks therefore patch the OpenBooster call site and the iterator MoveNext
// instead; these checks fail if a client update changes those IL shapes.
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
        var calls = IlCalls.Calls(Il(open), metadata);
        int factory = calls.IndexOf("PackOpening.OnFakeBoosterOpened");
        check(calls.Count(call => call == "PackOpening.OnFakeBoosterOpened") == 1
            && factory >= 0 && calls[factory + 1] == "MonoBehaviour.StartCoroutine",
            "pinned OpenBooster has exactly one fake factory call immediately consumed by StartCoroutine");
        var pageCalls = IlCalls.Calls(Il(moveNext), metadata);
        check(pageCalls.Count(call => call == "MassPackOpeningHiddenCards.AttachMassPackOpeningHighlightCards") == 1
            && pageCalls.Count(call => call == "HiddenCards.EnableReveal") == 1
            && pageCalls.IndexOf("MassPackOpeningHiddenCards.AttachMassPackOpeningHighlightCards") < pageCalls.IndexOf("HiddenCards.EnableReveal"),
            "pinned MoveNext exposes exact attach and post-delay readiness call sites");
    }
}
