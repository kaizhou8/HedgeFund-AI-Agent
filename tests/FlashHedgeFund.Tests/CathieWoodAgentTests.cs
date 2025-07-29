using System.Threading.Tasks;
using FlashHedgeFund.Agents;
using FlashHedgeFund.Contracts;
using Xunit;

namespace FlashHedgeFund.Tests;

/// <summary>
/// Cathie Wood agent tests
/// </summary>
public class CathieWoodAgentTests
{
    [Fact]
    public async Task EvaluateAsync_ReturnsBuy_WhenModelSaysBuy()
    {
        // Arrange: fake chat delegate always returns "Buy recommendation" text
        var fakeChat = new Func<Azure.AI.OpenAI.Models.ChatMessage[], CancellationToken, Task<string>>(
            (msgs, ct) => Task.FromResult(@"{""recommendation"": ""Buy"", ""confidence"": 0.85, ""rationale"": ""Strong innovation potential""}"));
        
        var agent = new CathieWoodAgent(fakeChat);
        var ctx = new StockContext("TEST", DateTime.UtcNow, 
            new Dictionary<string, double>(), 
            new Dictionary<DateTime, double> { { DateTime.UtcNow.Date, 123.45 } });

        // Act / 执行
        var rating = await agent.EvaluateAsync(ctx);

        // Assert / 断言
        Assert.Equal(Recommendation.Buy, rating.Recommendation);
        Assert.Equal(0.85, rating.Confidence);
        Assert.Contains("innovation", rating.Rationale);
    }

    [Fact]
    public async Task EvaluateAsync_FallsBackToSimpleParsing_WhenJsonInvalid()
    {
        // Arrange: fake chat returns non-JSON text with "Sell"
        var fakeChat = new Func<Azure.AI.OpenAI.Models.ChatMessage[], CancellationToken, Task<string>>(
            (msgs, ct) => Task.FromResult("I recommend to Sell this stock because..."));
        
        var agent = new CathieWoodAgent(fakeChat);
        var ctx = new StockContext("TEST", DateTime.UtcNow, 
            new Dictionary<string, double>(), 
            new Dictionary<DateTime, double> { { DateTime.UtcNow.Date, 100.0 } });

        // Act / 执行
        var rating = await agent.EvaluateAsync(ctx);

        // Assert / 断言
        Assert.Equal(Recommendation.Sell, rating.Recommendation);
        Assert.Contains("Sell", rating.Rationale);
    }
}
