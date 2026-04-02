using CodeReview.Application.Abstractions.Services;
using CodeReview.Domain.Enums;
using CodeReview.Domain.ValueObjects;
using System.Text.RegularExpressions;

namespace CodeReview.Infrastructure.Services;

/// <summary>
/// Language-aware static code analyzer.
/// Computes cyclomatic complexity, method lengths, naming violations, duplication.
/// Does NOT use AI — pure algorithmic analysis.
/// </summary>
public sealed class StaticCodeAnalyzer : IStaticCodeAnalyzer
{
    private static readonly Dictionary<CodeLanguage, string[]> Extensions = new()
    {
        [CodeLanguage.CSharp] = [".cs"],
        [CodeLanguage.JavaScript] = [".js", ".mjs", ".cjs"],
        [CodeLanguage.TypeScript] = [".ts", ".tsx"],
        [CodeLanguage.Python] = [".py"],
        [CodeLanguage.Java] = [".java"],
        [CodeLanguage.Go] = [".go"],
    };

    public CodeLanguage DetectLanguage(string code, string? fileName = null)
    {
        if (fileName is not null)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            foreach (var (lang, exts) in Extensions)
                if (exts.Contains(ext)) return lang;
        }

        // Heuristic content-based detection
        if (code.Contains("namespace ") && code.Contains("class ") && code.Contains("using ")) return CodeLanguage.CSharp;
        if (code.Contains("def ") && code.Contains("import ") && !code.Contains("{")) return CodeLanguage.Python;
        if (code.Contains("interface ") || code.Contains(": void") || code.Contains("readonly ")) return CodeLanguage.TypeScript;
        if (code.Contains("func ") && code.Contains("package ")) return CodeLanguage.Go;
        if (code.Contains("public class") && code.Contains("@Override")) return CodeLanguage.Java;
        if (code.Contains("const ") || code.Contains("let ") || code.Contains("=>")) return CodeLanguage.JavaScript;

        return CodeLanguage.Unknown;
    }

    public Task<StaticMetrics> AnalyzeAsync(string code, CodeLanguage language, CancellationToken ct = default)
    {
        var lines = code.Split('\n');
        var (codeLines, commentLines, blankLines) = CountLineTypes(lines, language);

        var methodBodies = ExtractMethodBodies(code, language);
        var complexity = CalculateComplexities(methodBodies, language);
        var longMethodCount = methodBodies.Count(m => m.LineCount > 30);
        var duplication = CalculateDuplication(lines);

        var metrics = new StaticMetrics
        {
            TotalLines = lines.Length,
            CodeLines = codeLines,
            CommentLines = commentLines,
            BlankLines = blankLines,
            AverageCyclomaticComplexity = complexity.Count > 0 ? complexity.Average() : 1,
            MaxCyclomaticComplexity = complexity.Count > 0 ? complexity.Max() : 1,
            LongMethodCount = longMethodCount,
            DuplicateBlockCount = duplication.DuplicateBlocks,
            DuplicationPercentage = duplication.Percentage,
            TotalMethods = methodBodies.Count,
            TotalClasses = CountClasses(code, language),
            AverageMethodLength = methodBodies.Count > 0 ? methodBodies.Average(m => m.LineCount) : 0
        };

        return Task.FromResult(metrics);
    }

    private static (int code, int comment, int blank) CountLineTypes(string[] lines, CodeLanguage lang)
    {
        var code = 0; var comment = 0; var blank = 0;
        var inBlockComment = false;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed)) { blank++; continue; }

            if (lang is CodeLanguage.CSharp or CodeLanguage.JavaScript or CodeLanguage.TypeScript or CodeLanguage.Java)
            {
                if (inBlockComment)
                {
                    comment++;
                    if (trimmed.Contains("*/")) inBlockComment = false;
                    continue;
                }
                if (trimmed.StartsWith("/*")) { comment++; inBlockComment = !trimmed.Contains("*/"); continue; }
                if (trimmed.StartsWith("//")) { comment++; continue; }
            }
            else if (lang is CodeLanguage.Python && trimmed.StartsWith("#")) { comment++; continue; }

            code++;
        }
        return (code, comment, blank);
    }

    private static List<MethodBody> ExtractMethodBodies(string code, CodeLanguage language)
    {
        var result = new List<MethodBody>();

        if (language is CodeLanguage.CSharp or CodeLanguage.Java)
        {
            // Match method signatures followed by opening brace
            var methodPattern = new Regex(
                @"(?:public|private|protected|internal|static|\s)+[\w<>\[\]]+\s+\w+\s*\([^)]*\)\s*\{",
                RegexOptions.Multiline);

            var lines = code.Split('\n');
            foreach (Match match in methodPattern.Matches(code))
            {
                var startLine = code[..match.Index].Count(c => c == '\n');
                var body = ExtractBraceBody(code, match.Index + match.Length - 1);
                if (body is not null)
                {
                    var lineCount = body.Count(c => c == '\n') + 1;
                    result.Add(new MethodBody(match.Value.Trim(), startLine, lineCount, body));
                }
            }
        }
        else if (language is CodeLanguage.JavaScript or CodeLanguage.TypeScript)
        {
            var funcPattern = new Regex(
                @"(?:function\s+\w+|const\s+\w+\s*=\s*(?:async\s*)?\(|(?:async\s+)?\w+\s*\([^)]*\)\s*\{)",
                RegexOptions.Multiline);

            foreach (Match match in funcPattern.Matches(code))
            {
                var braceIdx = code.IndexOf('{', match.Index);
                if (braceIdx < 0) continue;
                var body = ExtractBraceBody(code, braceIdx);
                if (body is not null)
                {
                    var lineCount = body.Count(c => c == '\n') + 1;
                    result.Add(new MethodBody(match.Value.Trim(), 0, lineCount, body));
                }
            }
        }

        return result;
    }

    private static string? ExtractBraceBody(string code, int openBrace)
    {
        if (openBrace >= code.Length || code[openBrace] != '{') return null;

        var depth = 0;
        var sb = new System.Text.StringBuilder();
        for (var i = openBrace; i < code.Length; i++)
        {
            var c = code[i];
            sb.Append(c);
            if (c == '{') depth++;
            else if (c == '}')
            {
                depth--;
                if (depth == 0) return sb.ToString();
            }
        }
        return null;
    }

    private static List<int> CalculateComplexities(List<MethodBody> methods, CodeLanguage lang)
    {
        // Decision points that increase cyclomatic complexity
        var decisionPattern = new Regex(
            @"\b(?:if|else if|while|for|foreach|case|catch|\?\?|&&|\|\|)\b",
            RegexOptions.Multiline);

        return methods.Select(m =>
        {
            var decisions = decisionPattern.Matches(m.Body).Count;
            return decisions + 1; // CC = decisions + 1
        }).ToList();
    }

    private static (int DuplicateBlocks, double Percentage) CalculateDuplication(string[] lines)
    {
        const int blockSize = 6;
        if (lines.Length < blockSize * 2) return (0, 0);

        var blocks = new Dictionary<string, int>();
        var duplicateLines = 0;

        for (var i = 0; i <= lines.Length - blockSize; i++)
        {
            var block = string.Join('\n', lines.Skip(i).Take(blockSize)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0));

            if (block.Length < 50) continue; // Skip trivial blocks

            if (blocks.TryGetValue(block, out _))
                duplicateLines += blockSize;
            else
                blocks[block] = i;
        }

        var percentage = lines.Length > 0 ? (double)duplicateLines / lines.Length * 100 : 0;
        return (duplicateLines / blockSize, Math.Round(percentage, 2));
    }

    private static int CountClasses(string code, CodeLanguage language)
    {
        var pattern = language switch
        {
            CodeLanguage.CSharp => new Regex(@"\bclass\s+\w+", RegexOptions.Multiline),
            CodeLanguage.Java => new Regex(@"\bclass\s+\w+", RegexOptions.Multiline),
            CodeLanguage.TypeScript => new Regex(@"\bclass\s+\w+", RegexOptions.Multiline),
            CodeLanguage.Python => new Regex(@"^class\s+\w+", RegexOptions.Multiline),
            _ => null
        };
        return pattern?.Matches(code).Count ?? 0;
    }

    private sealed record MethodBody(string Signature, int StartLine, int LineCount, string Body);
}
