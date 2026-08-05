using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Globalization;

namespace CrossMacro.Cli.Tests;

internal sealed class CliSourceBoundaryInspector
{
    private readonly IReadOnlyList<SourceDocument> _documents;

    internal CliSourceBoundaryInspector(IEnumerable<SourceDocument> documents)
    {
        _documents = documents.OrderBy(document => document.Path, StringComparer.Ordinal).ToArray();
    }

    internal static CliSourceBoundaryInspector FromRepositoryRoot(string repositoryRoot)
    {
        var documents = new[]
        {
            Path.Combine(repositoryRoot, "src", "CrossMacro.Cli"),
            Path.Combine(repositoryRoot, "src", "CrossMacro.Core"),
        }
            .SelectMany(directory => Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment => segment is "bin" or "obj"))
            .Order(StringComparer.Ordinal)
            .Select(path => new SourceDocument(
                Path.GetRelativePath(repositoryRoot, path).Replace(Path.DirectorySeparatorChar, '/').Replace(Path.AltDirectorySeparatorChar, '/'),
                File.ReadAllText(path)));

        return new CliSourceBoundaryInspector(documents);
    }

    internal static string FindRepositoryRoot(string startDirectory)
    {
        var directory = new DirectoryInfo(startDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "src")) && Directory.Exists(Path.Combine(directory.FullName, "tests")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the CrossMacro repository root from the test output directory.");
    }

    internal string[] FindCoreServiceConstructorDependencies()
    {
        var declarations = GetClassDeclarations();
        var coreServiceNames = _documents
            .SelectMany(document => Parse(document).GetRoot().DescendantNodes().OfType<TypeDeclarationSyntax>()
                .Where(declaration => GetNamespace(declaration).StartsWith("CrossMacro.Core.Services", StringComparison.Ordinal))
                .Select(declaration => declaration.Identifier.ValueText))
            .ToHashSet(StringComparer.Ordinal);
        var handlerNames = GetHandlerNames(declarations);

        return declarations
            .Where(declaration => declaration.Namespace is "CrossMacro.Cli.Commands" && !declaration.Class.Modifiers.Any(SyntaxKind.AbstractKeyword) && handlerNames.Contains(declaration.Class.Identifier.ValueText))
            .SelectMany(declaration => GetConstructorParameters(declaration.Class)
                .Where(parameter => ReferencesCoreService(parameter.Type, coreServiceNames))
                .Select(parameter => FormatViolation(declaration.Document, parameter, string.Create(CultureInfo.InvariantCulture, $"{declaration.FullName} -> {parameter.Type}"))))
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    internal string[] FindAvaloniaReferences()
    {
        return _documents
            .Where(document => document.Path.StartsWith("src/CrossMacro.Cli/", StringComparison.Ordinal))
            .SelectMany(document => GetRootNames(document)
                .Where(IsAvaloniaReference)
                .Select(name => FormatViolation(document, name, string.Create(CultureInfo.InvariantCulture, $"Avalonia reference '{name}'"))))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    internal string[] FindConstructorParameterTypes(string fullyQualifiedClassName)
    {
        return GetClassDeclarations()
            .Where(declaration => declaration.FullName == fullyQualifiedClassName)
            .SelectMany(declaration => GetConstructorParameters(declaration.Class))
            .Select(parameter => parameter.Type?.ToString())
            .Where(typeName => typeName is not null)
            .Select(typeName => typeName!)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    internal string[] FindDoctorServiceForbiddenReferences()
    {
        var doctorDocument = _documents.Single(document => document.Path == "src/CrossMacro.Cli/Services/Doctor/DoctorService.cs");
        return GetRootNames(doctorDocument)
            .Where(name => name.ToString().Contains("CrossMacro.Infrastructure.Helpers", StringComparison.Ordinal) || name.ToString() == "PathHelper")
            .Select(name => FormatViolation(doctorDocument, name, string.Create(CultureInfo.InvariantCulture, $"forbidden reference '{name}'")))
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<ClassDeclarationInfo> GetClassDeclarations()
    {
        return _documents
            .SelectMany(document => Parse(document).GetRoot().DescendantNodes().OfType<ClassDeclarationSyntax>()
                .Select(@class => new ClassDeclarationInfo(document, @class, GetNamespace(@class), GetFullName(@class))))
            .ToArray();
    }

    private static HashSet<string> GetHandlerNames(IReadOnlyList<ClassDeclarationInfo> declarations)
    {
        var handlerNames = declarations
            .Where(declaration => GetBaseTypeNames(declaration.Class).Contains("ICliCommandHandler", StringComparer.Ordinal))
            .Select(declaration => declaration.Class.Identifier.ValueText)
            .ToHashSet(StringComparer.Ordinal);

        var changed = true;
        while (changed)
        {
            changed = false;
            foreach (var declaration in declarations)
            {
                var handlerName = declaration.Class.Identifier.ValueText;
                if (handlerNames.Contains(handlerName) || !GetBaseTypeNames(declaration.Class).Any(handlerNames.Contains))
                {
                    continue;
                }

                changed = handlerNames.Add(handlerName);
            }
        }

        return handlerNames;
    }

    private static IEnumerable<string> GetBaseTypeNames(ClassDeclarationSyntax @class)
    {
        return @class.BaseList?.Types
            .Select(baseType => baseType.Type switch
            {
                GenericNameSyntax genericName => genericName.Identifier.ValueText,
                IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
                QualifiedNameSyntax qualifiedName => qualifiedName.Right.Identifier.ValueText,
                _ => baseType.Type.ToString(),
            })
            ?? [];
    }

    private static IEnumerable<ParameterSyntax> GetConstructorParameters(ClassDeclarationSyntax @class)
    {
        var primaryConstructorParameters = @class.ParameterList?.Parameters ?? [];
        return primaryConstructorParameters.Concat(@class.Members.OfType<ConstructorDeclarationSyntax>().SelectMany(constructor => constructor.ParameterList.Parameters));
    }

    private static bool ReferencesCoreService(TypeSyntax? type, ISet<string> coreServiceNames)
    {
        if (type is null)
        {
            return false;
        }

        var text = type.ToString().Replace("global::", string.Empty, StringComparison.Ordinal);
        return text.Contains("CrossMacro.Core.Services.", StringComparison.Ordinal)
            || type.DescendantNodesAndSelf().OfType<IdentifierNameSyntax>().Any(name => coreServiceNames.Contains(name.Identifier.ValueText));
    }

    private static IEnumerable<NameSyntax> GetRootNames(SourceDocument document)
    {
        return Parse(document).GetRoot().DescendantNodes().OfType<NameSyntax>().Where(name => name.Parent is not NameSyntax);
    }

    private static bool IsAvaloniaReference(NameSyntax name)
    {
        var text = name.ToString().Replace("global::", string.Empty, StringComparison.Ordinal);
        return text == "Avalonia" || text.StartsWith("Avalonia.", StringComparison.Ordinal);
    }

    private static SyntaxTree Parse(SourceDocument document)
    {
        return CSharpSyntaxTree.ParseText(document.Text, path: document.Path);
    }

    private static string GetNamespace(SyntaxNode declaration)
    {
        return string.Join(
            ".",
            declaration.AncestorsAndSelf().OfType<BaseNamespaceDeclarationSyntax>().Reverse().Select(@namespace => @namespace.Name.ToString()));
    }

    private static string GetFullName(ClassDeclarationSyntax @class)
    {
        var namespaceName = GetNamespace(@class);
        var containingTypes = @class.Ancestors().OfType<ClassDeclarationSyntax>().Reverse().Select(containingType => containingType.Identifier.ValueText);
        return string.Join(".", new[] { namespaceName }.Concat(containingTypes).Append(@class.Identifier.ValueText).Where(name => name.Length > 0));
    }

    private static string FormatViolation(SourceDocument document, SyntaxNode node, string description)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{document.Path}:{node.GetLocation().GetLineSpan().StartLinePosition.Line + 1}: {description}");
    }

    internal sealed record SourceDocument(string Path, string Text);

    private sealed record ClassDeclarationInfo(SourceDocument Document, ClassDeclarationSyntax Class, string Namespace, string FullName);
}
