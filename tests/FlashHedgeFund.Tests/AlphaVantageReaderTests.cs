using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using FlashHedgeFund.Data;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FlashHedgeFund.Tests;

/// <summary>
/// Alpha Vantage reader tests
/// </summary>
public class AlphaVantageReaderTests
{
    private const string SampleJson = @"{""Time Series (Daily)"": {""2025-07-28"": {""4. close"": ""123.45""}}}";

    private static HttpClient CreateMockClient()
    {
        var handler = new DelegatingHandlerStub((req, ct) =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(SampleJson)
            };
            return Task.FromResult(response);
        });
        return new HttpClient(handler);
    }

    [Fact]
    public async Task GetStockContextAsync_ReturnsPriceSeries()
    {
        // Arrange
        var http = CreateMockClient();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new[] 
        { 
            new KeyValuePair<string, string>("AlphaVantage:ApiKey", "demo") 
        }).Build();
        var reader = new AlphaVantageReader(http, NullLogger<AlphaVantageReader>.Instance, config);

        // Act
        var ctx = await reader.GetStockContextAsync("TEST");

        // Assert
        Assert.Equal("TEST", ctx.Ticker);
        Assert.True(ctx.PriceSeries.Count > 0);
    }

    private sealed class DelegatingHandlerStub : DelegatingHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;
        
        public DelegatingHandlerStub(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }
        
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) 
            => _handler(request, cancellationToken);
    }
}
