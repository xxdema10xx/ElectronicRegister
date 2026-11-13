using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Text.Json;
var root = Path.GetFullPath(args[0]);
var files = Directory.GetFiles(Path.Combine(root,"Backend/ElectronicRegisterAPI/src"),"*.cs",SearchOption.AllDirectories).Where(f=>!f.Split(Path.DirectorySeparatorChar).Any(s=>s is "bin" or "obj")).ToArray();
var methods = files.SelectMany(f=> CSharpSyntaxTree.ParseText(File.ReadAllText(f)).GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(m=>m.Body!=null || m.ExpressionBody!=null).Select(m=>new {
 File=Path.GetRelativePath(root,f).Replace('\\','/'), Method=m.Identifier.Text, Line=m.GetLocation().GetLineSpan().StartLinePosition.Line+1,
 Lines=m.GetLocation().GetLineSpan().EndLinePosition.Line-m.GetLocation().GetLineSpan().StartLinePosition.Line+1,
 BranchProxy=1+m.DescendantNodes().Count(n=>n is IfStatementSyntax or ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or CatchClauseSyntax or ConditionalExpressionSyntax or CaseSwitchLabelSyntax || n is BinaryExpressionSyntax b && (b.IsKind(SyntaxKind.LogicalAndExpression)||b.IsKind(SyntaxKind.LogicalOrExpression)))+m.DescendantNodes().OfType<SwitchExpressionArmSyntax>().Count(),
 Tokens=string.Join(" ",m.Body?.DescendantTokens().Select(t=>t.Text) ?? m.ExpressionBody!.DescendantTokens().Select(t=>t.Text))
})).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new { SourceFiles=files.Length, Methods=methods.Length, Definition="Syntax branch proxy, not certified McCabe/cognitive complexity; includes lambdas, excludes property accessors and switch pattern combinators. Exact token duplicates only, minimum 120 characters.", Top=methods.OrderByDescending(m=>m.BranchProxy).Take(20).Select(m=>new{m.File,m.Method,m.Line,m.Lines,m.BranchProxy}), Duplicates=methods.Where(m=>m.Tokens.Length>=120).GroupBy(m=>m.Tokens).Where(g=>g.Count()>1).Select(g=>g.Select(m=>new{m.File,m.Method,m.Line})) },new JsonSerializerOptions{WriteIndented=true}));
