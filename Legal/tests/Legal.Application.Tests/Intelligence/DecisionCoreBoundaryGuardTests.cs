using System.Reflection;
using System.Reflection.Emit;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI admission-boundary guard tests — mechanically freeze the Core ↔ Domain separation.
//
// Two invariants the whole channel architecture depends on:
//   1. POLOXI Core (…Decision.Core) never takes a dependency on any channel/domain namespace, so a
//      legal concept can never leak into the deterministic decision engine.
//   2. The LegalChannelSignalAdapter only CONSTRUCTS DecisionBranchSignal values — it never invokes
//      DecisionRecompetition / DecisionCoreMath. The domain proposes; Core disposes.
//
// Enforced by IL inspection (call / callvirt / newobj tokens) so the guard catches real references,
// not just naming conventions.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionCoreBoundaryGuardTests
{
    private const string CoreNamespace = "Legal.Application.Features.Intelligence.Decision.Core";
    private const string ChannelsNamespace = "Legal.Application.Features.Intelligence.Decision.Channels";

    private static readonly Assembly ApplicationAssembly = typeof(DecisionBranchSignal).Assembly;

    [Fact]
    public void PoloxiCore_HasNoDependencyOnChannelNamespace()
    {
        var coreTypes = ApplicationAssembly.GetTypes()
            .Where(t => t.Namespace is { } ns && ns.StartsWith(CoreNamespace, StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(coreTypes); // sanity: we actually located Core

        var violations = new List<string>();
        foreach (var coreType in coreTypes)
        {
            foreach (var referenced in ReferencedTypes(coreType))
            {
                if (referenced.Namespace is { } ns && ns.StartsWith(ChannelsNamespace, StringComparison.Ordinal))
                    violations.Add($"{coreType.FullName} → {referenced.FullName}");
            }
        }

        Assert.True(violations.Count == 0,
            "POLOXI Core must not depend on the channel/domain layer. Violations:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void LegalChannelSignalAdapter_NeverInvokesCore()
    {
        var referenced = ReferencedTypes(typeof(LegalChannelSignalAdapter)).ToArray();

        var coreReferences = referenced
            .Where(t => t.Namespace is { } ns && ns.StartsWith(CoreNamespace, StringComparison.Ordinal))
            .Select(t => t.FullName!)
            .Distinct()
            .ToArray();

        Assert.True(coreReferences.Length == 0,
            "The adapter must only construct DecisionBranchSignal, never invoke POLOXI Core. Core references:\n"
            + string.Join("\n", coreReferences));

        // Positive assertion: it DOES construct the neutral admission contract.
        Assert.Contains(referenced, t => t == typeof(DecisionBranchSignal));
    }

    // Collects every type this type references through member signatures and through the IL of its
    // (and its nested types') method bodies (call / callvirt / newobj operand tokens).
    private static IEnumerable<Type> ReferencedTypes(Type type)
    {
        var seen = new HashSet<Type>();

        void Add(Type? t)
        {
            if (t is null) return;
            if (t.IsByRef || t.IsPointer || t.IsArray) t = t.GetElementType();
            if (t is null) return;
            if (t.IsGenericType)
                foreach (var arg in t.GetGenericArguments()) Add(arg);
            seen.Add(t);
        }

        foreach (var t in new[] { type }.Concat(type.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic)))
        {
            const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

            foreach (var f in t.GetFields(all)) Add(f.FieldType);
            foreach (var p in t.GetProperties(all)) Add(p.PropertyType);

            var methods = t.GetMethods(all).Cast<MethodBase>().Concat(t.GetConstructors(all));
            foreach (var m in methods)
            {
                if (m is MethodInfo mi) Add(mi.ReturnType);
                foreach (var par in m.GetParameters()) Add(par.ParameterType);

                MethodBody? body;
                try { body = m.GetMethodBody(); }
                catch { body = null; }
                if (body is null) continue;

                foreach (var member in ResolveCalledMembers(m, body))
                    Add(member?.DeclaringType);
            }
        }

        return seen;
    }

    // Walks IL bytes, decoding call/callvirt/newobj operand tokens into the referenced members.
    private static IEnumerable<MemberInfo?> ResolveCalledMembers(MethodBase method, MethodBody body)
    {
        var il = body.GetILAsByteArray();
        if (il is null) yield break;

        var module = method.Module;
        var typeArgs = method.DeclaringType?.IsGenericType == true ? method.DeclaringType.GetGenericArguments() : null;
        var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;

        for (var i = 0; i < il.Length;)
        {
            // Decode the opcode (one byte, or two bytes when prefixed with 0xFE).
            OpCode op;
            if (il[i] == 0xFE)
            {
                if (i + 1 >= il.Length) yield break;
                if (!TwoByteOpCodes.TryGetValue(il[i + 1], out op)) yield break;
                i += 2;
            }
            else
            {
                if (!OneByteOpCodes.TryGetValue(il[i], out op)) yield break;
                i += 1;
            }

            // call / callvirt / newobj carry a 4-byte metadata token we resolve to a member.
            if (op == OpCodes.Call || op == OpCodes.Callvirt || op == OpCodes.Newobj)
            {
                if (i + 4 > il.Length) yield break;
                var token = BitConverter.ToInt32(il, i);
                MemberInfo? member = null;
                try { member = module.ResolveMember(token, typeArgs, methodArgs); }
                catch { /* token not resolvable in this context; skip */ }
                yield return member;
            }

            i += OperandSize(op, il, i);
        }
    }

    private static readonly IReadOnlyDictionary<byte, OpCode> OneByteOpCodes = BuildOpCodeTable(single: true);
    private static readonly IReadOnlyDictionary<byte, OpCode> TwoByteOpCodes = BuildOpCodeTable(single: false);

    private static IReadOnlyDictionary<byte, OpCode> BuildOpCodeTable(bool single)
    {
        var table = new Dictionary<byte, OpCode>();
        foreach (var field in typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (field.GetValue(null) is not OpCode op) continue;
            var value = (ushort)op.Value;
            var isSingle = value <= 0xFF;
            if (isSingle == single)
                table[(byte)(value & 0xFF)] = op;
        }
        return table;
    }

    // Number of operand bytes following an opcode, derived from its OperandType.
    private static int OperandSize(OpCode op, byte[] il, int operandStart) => op.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI
            or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineBrTarget or OperandType.InlineField or OperandType.InlineI
            or OperandType.InlineMethod or OperandType.InlineSig or OperandType.InlineString
            or OperandType.InlineTok or OperandType.InlineType or OperandType.ShortInlineR => 4,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (operandStart + 4 <= il.Length ? BitConverter.ToInt32(il, operandStart) * 4 : 0),
        _ => 0,
    };
}
