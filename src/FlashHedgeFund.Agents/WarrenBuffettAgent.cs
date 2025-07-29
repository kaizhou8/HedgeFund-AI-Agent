using Azure.AI.OpenAI;
using Azure.AI.OpenAI.Models;
using FlashHedgeFund.Contracts;
using System.Text.Json;

namespace FlashHedgeFund.Agents;

/// <summary>
/// Warren Buffett investment style agent
/// </summary>
public sealed class WarrenBuffettAgent : IAgent
{
    private readonly OpenAIClient _client;

    public WarrenBuffettAgent(OpenAIClient client)
    {
        _client = client;
    }

    public string Name => "warren_buffett";

    public async Task<StockRating> EvaluateAsync(StockContext context, CancellationToken ct = default)
    {
        var system = @"You are legendary investor Warren Buffett. Analyze stocks based on:
- Long-term value and competitive moats
- Strong management and business fundamentals  
- Reasonable price relative to intrinsic value
- Predictable earnings and cash flows

Return ONLY a JSON object with these exact fields:
{
  ""recommendation"": ""Buy"" | ""Hold"" | ""Sell"",
  ""confidence"": 0.85,
  ""rationale"": ""Your detailed reasoning here""
}";

        var fundamentals = string.Join(", ", context.FundamentalData.Select(kv => $"{kv.Key}:{kv.Value}"));
        var recentPrice = context.PriceSeries.LastOrDefault().Value;
        var user = $"Ticker: {context.Ticker}\nCurrent Price: ${recentPrice:F2}\nFundamentals: {fundamentals}";

        var messages = new[]
        {
            new ChatMessage(ChatRole.System, system),
            new ChatMessage(ChatRole.User, user)
        };

        var response = await _client.GetChatCompletionsAsync(
            model: "gpt-3.5-turbo",
            new ChatCompletionsOptions(messages), ct);

        var content = response.Value.Choices[0].Message.Content;

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

            return new StockRating(rec, 0.7, content, new Dictionary<string, string>());
        }
    }
}
