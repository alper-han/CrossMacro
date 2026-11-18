using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CrossMacro.Core.Tests.Architecture;
/// <summary>Examines language tokens and qualified type ownership rather than comments or file naming.</summary>
internal static class SourceArchitectureInspector
{
    internal static IEnumerable<(string Pattern, int Line)> FindReferences(string source, IEnumerable<string> patterns)
    {
        var root = CSharpSyntaxTree.ParseText(source, cancellationToken: CancellationToken.None).GetRoot(CancellationToken.None);
        var code = root.DescendantTokens().Where(token => token.Parent is not LiteralExpressionSyntax && !token.IsKind(SyntaxKind.InterpolatedStringTextToken)).ToArray();
        foreach (var pattern in patterns)
        {
            var expected = SyntaxFactory.ParseTokens(pattern).Where(token => !token.IsKind(SyntaxKind.EndOfFileToken)).Select(token => token.Text).ToArray();
            for (var index = 0; expected.Length > 0 && index <= code.Length - expected.Length; index++)
            {
                if (expected.Where((text, offset) => !string.Equals(code[index + offset].Text, text, StringComparison.Ordinal)).Any())
                {
                    continue;
                }

                yield return (pattern, code[index].GetLocation().GetLineSpan().StartLinePosition.Line + 1);
                break;
            }
        }
    }
}
