using System.IO.Compression;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

internal static class PreservationVerifier
{
    public static int Run(string root)
    {
        using var zip = ZipFile.OpenRead(Path.Combine(root, "docs/refactoring/validation/baseline-source.zip"));
        var current = Directory.GetFiles(Path.Combine(root, "Backend/ElectronicRegisterAPI/src"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Split(Path.DirectorySeparatorChar).Any(s => s is "obj" or "bin"))
            .SelectMany(p => ReadTypes(File.ReadAllText(p)).Select(t => new { Path = p, Layer = Path.GetRelativePath(root, p).Replace('\\', '/').Split('/')[3], Type = t }))
            .ToArray();
        var errors = new List<string>();
        var checkedTypes = 0;
        var generatedFiles = 0;
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".cs")))
        {
            using var reader = new StreamReader(entry.Open());
            var before = reader.ReadToEnd();
            var layer = entry.FullName.Split('/')[3];
            var oldTypes = ReadTypes(before).ToArray();
            foreach (var type in oldTypes)
            {
                var candidates = current.Where(c => c.Layer == layer && c.Type.Identifier.Text == type.Identifier.Text).ToArray();
                if (type.Identifier.Text is "NoCalendarFault" or "NoFormativePlanFault")
                {
                    candidates = current.Where(c => c.Layer == "Infrastructure" && c.Type.Identifier.Text == type.Identifier.Text).ToArray();
                }
                if (candidates.Length > 1)
                {
                    candidates = candidates.Where(c => Path.GetFileName(c.Path) == Path.GetFileName(entry.FullName)).ToArray();
                }
                if (candidates.Length != 1)
                {
                    errors.Add($"Cannot uniquely match {entry.FullName}: {type.Identifier.Text}");
                    continue;
                }
                var actual = candidates[0].Type;
                if (Canonical(type) != Canonical(actual))
                {
                    errors.Add($"Declaration tokens changed: {entry.FullName}: {type.Identifier.Text}");
                }
                checkedTypes++;
            }
            if (entry.FullName.Contains("/Persistence/Entities/") && oldTypes.All(t => t.Modifiers.Any(SyntaxKind.PartialKeyword)) || entry.FullName.EndsWith("/Persistence/ElectronicRegisterContext.cs"))
            {
                using var originalStream = entry.Open();
                using var originalBytes = new MemoryStream();
                originalStream.CopyTo(originalBytes);
                if (!originalBytes.ToArray().SequenceEqual(File.ReadAllBytes(Path.Combine(root, entry.FullName))))
                {
                    errors.Add("Generated file modified: " + entry.FullName);
                }
                generatedFiles++;
            }
            // Includes all top-level statements (DI, middleware, endpoints and host setup).
            if (entry.FullName.EndsWith("/Program.cs"))
            {
                var oldRoot = CSharpSyntaxTree.ParseText(before).GetCompilationUnitRoot();
                var newRoot = CSharpSyntaxTree.ParseText(File.ReadAllText(Path.Combine(root, entry.FullName))).GetCompilationUnitRoot();
                if (Canonical(oldRoot.WithUsings(default)) != Canonical(newRoot.WithUsings(default)))
                {
                    errors.Add("Program statements changed: " + entry.FullName);
                }
            }
        }
        var result = new { checkedTypes, generatedFilesUnchanged = generatedFiles, errors };
        File.WriteAllText(Path.Combine(root, "docs/refactoring/validation/preservation.json"), JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine(JsonSerializer.Serialize(result));
        return errors.Count == 0 ? 0 : 1;
    }
    private static IEnumerable<BaseTypeDeclarationSyntax> ReadTypes(string text) => CSharpSyntaxTree.ParseText(text).GetRoot().DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(t => t.Parent is BaseNamespaceDeclarationSyntax or CompilationUnitSyntax || t.Parent is TypeDeclarationSyntax && !t.Modifiers.Any(SyntaxKind.PrivateKeyword));
    private static string Canonical(SyntaxNode node)
    {
        var nested = node.DescendantNodes().OfType<BaseTypeDeclarationSyntax>().Where(t => t.Parent == node && !t.Modifiers.Any(SyntaxKind.PrivateKeyword)).ToArray();
        if (nested.Length > 0)
        {
            node = node.RemoveNodes(nested, SyntaxRemoveOptions.KeepNoTrivia)!;
        }
        var tokens = node.DescendantTokens().ToArray();
        var result = new List<string>();
        for (var i = 0; i < tokens.Length; i++)
        {
            // Namespace qualification changes are the purpose of this refactor. Keep the final type name.
            if (tokens[i].Text == "ElectronicRegisterAPI" && i + 1 < tokens.Length && tokens[i + 1].Text == ".")
            {
                while (i + 2 < tokens.Length && tokens[i + 1].Text == ".")
                {
                    i += 2;
                }
            }
            if (tokens[i].Text is "StudentExamPresentation" or "Mt3aOwnershipMigration" &&
                i + 2 < tokens.Length && tokens[i + 1].Text == "." &&
                tokens[i + 2].Text is "Order" or "TableDigest")
            {
                i += 2;
            }
            result.Add(tokens[i].Text);
        }
        return string.Join("\u001f", result);
    }
}
