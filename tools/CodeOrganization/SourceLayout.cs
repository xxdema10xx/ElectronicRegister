using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal sealed class SourceLayout : CSharpSyntaxRewriter
{
    public static string Format(CompilationUnitSyntax unit)
    {
        var normalized = unit.NormalizeWhitespace("    ", "\n");
        var formatted = new SourceLayout().Visit(normalized)!;
        return formatted.ToFullString().TrimEnd() + "\n";
    }

    public override SyntaxNode? VisitFileScopedNamespaceDeclaration(FileScopedNamespaceDeclarationSyntax node)
    {
        var result = (FileScopedNamespaceDeclarationSyntax)base.VisitFileScopedNamespaceDeclaration(node)!;
        return result.WithSemicolonToken(result.SemicolonToken.WithTrailingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.LineFeed));
    }

    public override SyntaxNode? VisitParameterList(ParameterListSyntax node)
    {
        var result = (ParameterListSyntax)base.VisitParameterList(node)!;
        if (result.Parameters.Count < 4)
        {
            return result;
        }

        var indentation = node.Ancestors().OfType<TypeDeclarationSyntax>().Count() * 4;
        if (node.Parent is TypeDeclarationSyntax)
        {
            indentation -= 4;
        }

        var indent = new string(' ', indentation);
        var parameters = result.Parameters.Select(p => p.WithLeadingTrivia(
            new[] { SyntaxFactory.Whitespace(indent + "    ") }.Concat(p.GetLeadingTrivia()
                .Where(t => !t.IsKind(SyntaxKind.WhitespaceTrivia) && !t.IsKind(SyntaxKind.EndOfLineTrivia)))));
        var separators = result.Parameters.GetSeparators().Select(t => t.WithTrailingTrivia(SyntaxFactory.LineFeed));
        return result.WithOpenParenToken(result.OpenParenToken.WithTrailingTrivia(SyntaxFactory.LineFeed))
            .WithParameters(SyntaxFactory.SeparatedList(parameters, separators))
            .WithCloseParenToken(result.CloseParenToken.WithLeadingTrivia(SyntaxFactory.LineFeed, SyntaxFactory.Whitespace(indent)));
    }
}
