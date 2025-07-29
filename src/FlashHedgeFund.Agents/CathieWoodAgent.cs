using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Models;
using FlashHedgeFund.Contracts;
using System.Text.Json;

namespace FlashHedgeFund.Agents;

/// <summary>
/// Cathie Wood investment style agent
/// </summary>
public sealed class CathieWoodAgent : IAgent
{
    private readonly Func<ChatMessage[], CancellationToken, Task<string>> _chatFunc;

    public CathieWoodAgent(OpenAIClient client)
        : this(async (messages, ct) =>
        {
            var resp = await client.GetChatCompletionsAsync("gpt-3.5-turbo", new ChatCompletionsOptions(messages), ct);
            return resp.Value.Choices[0].Message.Content;
        }) { }

    // for testing
    public CathieWoodAgent(Func<ChatMessage[], CancellationToken, Task<string>> chatFunc)
    {
        _chatFunc = chatFunc;
    }

    public string Name => "cathie_wood";

    public async Task<StockRating> EvaluateAsync(StockContext context, CancellationToken ct = default)
    {
        var system = @"You are growth-oriented investor Cathie Wood. Focus on:
- Disruptive innovation and technological breakthroughs
- High growth potential and market expansion
- Strong momentum and future scalability
- Revolutionary business models

Return ONLY a JSON object with these exact fields:
{
  ""recommendation"": ""Buy"" | ""Hold"" | ""Sell"",
  ""confidence"": 0.85,
  ""rationale"": ""Your detailed reasoning here""
}";

        var recentPrice = context.PriceSeries.LastOrDefault().Value;
        var user = $"Ticker: {context.Ticker}\nCurrent Price: ${recentPrice:F2}\nAnalyze innovation potential and growth momentum.";

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, system),
            new ChatMessage(ChatRole.User, user)
        };

        var content = await _chatFunc(messages, ct);

        // Parse JSON response
        try
        {
            var jsonDoc = JsonDocument.Parse(content);
            var root = jsonDoc.RootElement;
            
            var recommendation = Enum.Parse<Recommendation>(root.GetProperty("recommendation").GetString()!, true);
            var confidence = root.GetProperty("confidence").GetDouble();
            var rationale = root.GetProperty("rationale").GetString()!;

            return new StockRating(recommendation, confidence, rationale, new Dictionary<string, string>());
        }
        catch (Exception)
        {
            // Fallback to simple parsing
            var rec = Recommendation.Hold;
            if (content.Contains("Buy", StringComparison.OrdinalIgnoreCase)) rec = Recommendation.Buy;
            else if (content.Contains("Sell", StringComparison.OrdinalIgnoreCase)) rec = Recommendation.Sell;

            return new StockRating(rec, 0.65, content, new Dictionary<string, string>());
        }
    }
}
