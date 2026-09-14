using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Globalization;

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

    internal static IEnumerable<string> DeclaredTypes(string source)
    {
        var root = CSharpSyntaxTree.ParseText(source, cancellationToken: CancellationToken.None).GetRoot(CancellationToken.None);
        foreach (var declaration in root.DescendantNodes().OfType<BaseTypeDeclarationSyntax>())
        {
            if (declaration.Modifiers.Any(SyntaxKind.FileKeyword))
            {
                continue;
            }

            var owners = declaration.Ancestors().Reverse().Select(node => node switch
            {
                BaseNamespaceDeclarationSyntax ns => ns.Name.ToString(),
                TypeDeclarationSyntax type => TypeName(type),
                _ => null,
            }).Where(name => name is not null);
            var name = declaration is TypeDeclarationSyntax typeDeclaration ? TypeName(typeDeclaration) : declaration.Identifier.ValueText;
            yield return string.Join('.', owners.Append(name));
        }
    }

    private static string TypeName(TypeDeclarationSyntax type) => type.Identifier.ValueText + (type.TypeParameterList is { Parameters.Count: > 0 } parameters ? $"`{parameters.Parameters.Count.ToString(CultureInfo.InvariantCulture)}" : string.Empty);
}
