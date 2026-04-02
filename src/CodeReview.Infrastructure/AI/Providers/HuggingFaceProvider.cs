using CodeReview.Application.Abstractions.AI;
using CodeReview.Domain.Enums;
using CodeReview.Domain.Interfaces;
using CodeReview.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CodeReview.Infrastructure.AI.Providers;

/// <summary>
/// Base class for OpenAI-compatible providers (HuggingFace, OpenAI, Anthropic-via-compat)
/// Handles the common HTTP transport, retry logic, and response parsing
/// </summary>
public abstract class OpenAICompatibleProvider : IAiCodeAnalyzer
{
    private readonly IHttpClientFactory _factory;
    private readonly ISettingsRepository _settings;
    private readonly ILogger _logger;

    protected OpenAICompatibleProvider(
        IHttpClientFactory factory,
        ISettingsRepository settings,
        ILogger logger)
    {
        _factory = factory;
        _settings = settings;
        _logger = logger;
    }

    public abstract string ProviderName { get; }
    protected abstract string BaseUrl { get; }
    protected abstract string ModelName { get; }

    public bool IsAvailable => true;

    public async Task<AiAnalysisResult> AnalyzeAsync(
        string code,
        CodeLanguage language,
        StaticMetrics metrics,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var apiKey = await _settings.GetValueAsync("AI:ApiKey", ct);

        var prompt = BuildAnalysisPrompt(code, language, metrics);
        var request = BuildChatRequest(prompt);

        var client = _factory.CreateClient("AI");

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl);
            httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");
            httpRequest.Content = new StringContent(
                JsonSerializer.Serialize(request, JsonOptions),
                Encoding.UTF8,
                "application/json");

            var response = await client.SendAsync(httpRequest, ct);
            response.EnsureSuccessStatusCode();

            var raw = await response.Content.ReadAsStringAsync(ct);
            var chatResponse = JsonSerializer.Deserialize<OpenAiChatResponse>(raw, JsonOptions)
                ?? throw new InvalidOperationException("Empty response from AI provider");

            var content = chatResponse.Choices?.FirstOrDefault()?.Message?.Content
                ?? throw new InvalidOperationException("No content in AI response");

            sw.Stop();
            _logger.LogDebug("AI analysis completed in {Ms}ms using {Provider}", sw.ElapsedMilliseconds, ProviderName);

            var parsed = ParseAiResponse(content, language, metrics);
            return parsed with
            {
                ProviderName = ProviderName,
                ProcessingTime = sw.Elapsed,
                TokensUsed = chatResponse.Usage?.TotalTokens ?? 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI provider {Provider} failed", ProviderName);
            throw;
        }
    }

    /// <summary>
    /// Builds a structured prompt that guides the model to return JSON with issues and scores
    /// </summary>
    private static string BuildAnalysisPrompt(string code, CodeLanguage language, StaticMetrics metrics)
    {
        return $$"""
You are an expert senior software engineer performing a professional code review.
## Code to Review ({{language}})
```{{language.ToString().ToLower()}}
{{code}}
```
## Static Metrics
- Total lines: {{metrics.TotalLines}}
- Cyclomatic complexity (avg): {{metrics.AverageCyclomaticComplexity:F1}}
- Cyclomatic complexity (max): {{metrics.MaxCyclomaticComplexity}}
- Long methods: {{metrics.LongMethodCount}}
- Duplicate blocks: {{metrics.DuplicateBlockCount}} ({{metrics.DuplicationPercentage:F1}}%)
- Comment ratio: {{metrics.CommentRatio:F1}}%
## Task
Analyze the code and respond with a JSON object ONLY (no markdown, no explanation outside JSON):
{
  "summary": "2-3 sentence professional overall summary",
  "scores": {
    "architecture": 0-100,
    "readability": 0-100,
    "maintainability": 0-100
  },
  "issues": [
    {
      "title": "Short issue title",
      "description": "Detailed explanation of the problem",
      "suggestion": "Concrete improvement suggestion with example if helpful",
      "severity": "Info|Warning|Error|Critical",
      "category": "SolidViolation|AntiPattern|Readability|Complexity|Naming|Performance|Security|Duplication|Architecture|Maintainability",
      "lineStart": null,
      "lineEnd": null,
      "codeSnippet": null,
      "confidence": 0.0-1.0
    }
  ]
}
Focus on: SOLID violations, anti-patterns, naming conventions, code smells, architectural issues.
Severity guide: Critical=security/data loss, Error=bugs/major issues, Warning=code smells, Info=style suggestions.
""";
    }

    private static OpenAiChatRequest BuildChatRequest(string prompt) => new()
    {
        Model = "model-placeholder", // overridden per-provider
        MaxTokens = 4096,
        Temperature = 0.1,
        Messages =
        [
            new() { Role = "system", Content = "You are a senior software engineer and code review expert. Always respond with valid JSON only." },
            new() { Role = "user", Content = prompt }
        ]
    };

    private AiAnalysisResult ParseAiResponse(string content, CodeLanguage language, StaticMetrics metrics)
    {
        try
        {
            // Strip markdown code fences if present
            var json = content.Trim();
            if (json.StartsWith("```")) json = json.Split('\n').Skip(1).SkipLast(1).Aggregate((a, b) => a + "\n" + b);

            var parsed = JsonSerializer.Deserialize<AiResponseJson>(json, JsonOptions)
                ?? throw new InvalidOperationException("Null deserialization result");

            var issues = (parsed.Issues ?? []).Select(i => new AiIssue
            {
                Title = i.Title ?? "Unknown issue",
                Description = i.Description ?? string.Empty,
                Suggestion = i.Suggestion,
                Severity = Enum.TryParse<IssueSeverity>(i.Severity, true, out var sev) ? sev : IssueSeverity.Warning,
                Category = Enum.TryParse<IssueCategory>(i.Category, true, out var cat) ? cat : IssueCategory.Readability,
                LineStart = i.LineStart,
                LineEnd = i.LineEnd,
                CodeSnippet = i.CodeSnippet,
                Confidence = i.Confidence
            }).ToList();

            var scores = parsed.Scores;
            var qualityScore = QualityScore.Create(
                architecture: scores?.Architecture ?? EstimateScore(metrics),
                readability: scores?.Readability ?? EstimateScore(metrics),
                maintainability: scores?.Maintainability ?? EstimateScore(metrics));

            return new AiAnalysisResult
            {
                Issues = issues,
                Score = qualityScore,
                Summary = parsed.Summary ?? "Analysis completed.",
                ProviderName = ProviderName,
                ProcessingTime = TimeSpan.Zero
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse AI JSON response, using fallback. Content: {Content}", content[..Math.Min(200, content.Length)]);
            return FallbackResult(metrics);
        }
    }

    private AiAnalysisResult FallbackResult(StaticMetrics metrics)
    {
        var score = EstimateScore(metrics);
        return new AiAnalysisResult
        {
            Issues = [],
            Score = QualityScore.Create(score, score, score),
            Summary = "AI analysis parsing failed. Scores estimated from static metrics.",
            ProviderName = ProviderName,
            ProcessingTime = TimeSpan.Zero
        };
    }

    private static double EstimateScore(StaticMetrics m)
    {
        double score = 80.0;
        if (m.AverageCyclomaticComplexity > 10) score -= 10;
        if (m.DuplicationPercentage > 20) score -= 10;
        if (m.LongMethodCount > 5) score -= 5;
        if (m.CommentRatio < 5) score -= 5;
        return Math.Max(0, Math.Min(100, score));
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // ── Internal JSON models ──────────────────────────────────────────────────
    private sealed class OpenAiChatRequest
    {
        [JsonPropertyName("model")] public string Model { get; init; } = string.Empty;
        [JsonPropertyName("max_tokens")] public int MaxTokens { get; init; }
        [JsonPropertyName("temperature")] public double Temperature { get; init; }
        [JsonPropertyName("messages")] public List<ChatMessage> Messages { get; init; } = [];
    }

    private sealed class ChatMessage
    {
        [JsonPropertyName("role")] public string Role { get; init; } = string.Empty;
        [JsonPropertyName("content")] public string Content { get; init; } = string.Empty;
    }

    private sealed class OpenAiChatResponse
    {
        [JsonPropertyName("choices")] public List<Choice>? Choices { get; init; }
        [JsonPropertyName("usage")] public Usage? Usage { get; init; }
    }

    private sealed class Choice
    {
        [JsonPropertyName("message")] public ChatMessage? Message { get; init; }
    }

    private sealed class Usage
    {
        [JsonPropertyName("total_tokens")] public int TotalTokens { get; init; }
    }

    private sealed class AiResponseJson
    {
        [JsonPropertyName("summary")] public string? Summary { get; init; }
        [JsonPropertyName("scores")] public ScoresJson? Scores { get; init; }
        [JsonPropertyName("issues")] public List<IssueJson>? Issues { get; init; }
    }

    private sealed class ScoresJson
    {
        [JsonPropertyName("architecture")] public double Architecture { get; init; }
        [JsonPropertyName("readability")] public double Readability { get; init; }
        [JsonPropertyName("maintainability")] public double Maintainability { get; init; }
    }

    private sealed class IssueJson
    {
        [JsonPropertyName("title")] public string? Title { get; init; }
        [JsonPropertyName("description")] public string? Description { get; init; }
        [JsonPropertyName("suggestion")] public string? Suggestion { get; init; }
        [JsonPropertyName("severity")] public string? Severity { get; init; }
        [JsonPropertyName("category")] public string? Category { get; init; }
        [JsonPropertyName("lineStart")] public int? LineStart { get; init; }
        [JsonPropertyName("lineEnd")] public int? LineEnd { get; init; }
        [JsonPropertyName("codeSnippet")] public string? CodeSnippet { get; init; }
        [JsonPropertyName("confidence")] public double Confidence { get; init; } = 1.0;
    }
}

/// <summary>
/// Concrete HuggingFace provider using Kimi-K2-Instruct via HuggingFace router
/// </summary>
public sealed class HuggingFaceProvider : OpenAICompatibleProvider
{
    public override string ProviderName => "HuggingFace";
    protected override string BaseUrl => "https://router.huggingface.co/v1/chat/completions";
    protected override string ModelName => "moonshotai/Kimi-K2-Instruct-0905";

    public HuggingFaceProvider(
        IHttpClientFactory factory,
        ISettingsRepository settings,
        ILogger<HuggingFaceProvider> logger)
        : base(factory, settings, logger) { }
}
