using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;

// The call targets of one method body, read from client IL metadata (shared by native-bindings and native-match).
internal static class IlCalls
{
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
