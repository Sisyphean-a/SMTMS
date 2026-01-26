using System.Net;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using ServiceNexusClient = SMTMS.NexusClient.Services.NexusClient;
using Xunit;

namespace SMTMS.Tests.Services;

public class NexusClientTests
{
    [Fact]
    public async Task GetModInfoAsync_ReturnsDto_WhenApiCallIsSuccessful()
    {
        // Arrange
        var modId = "12345";
        var apiKey = "test-api-key";
        var jsonResponse = @"{
            ""name"": ""Test Mod"",
            ""summary"": ""A test mod"",
            ""description"": ""Full description"",
            ""picture_url"": ""http://example.com/image.png"",
            ""mod_id"": 12345,
            ""downloads"": 100,
            ""endorsement_count"": 10
        }";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(jsonResponse)
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var loggerMock = new Mock<ILogger<ServiceNexusClient>>();
        var nexusClient = new ServiceNexusClient(httpClient, loggerMock.Object);

        // Act
        var result = await nexusClient.GetModInfoAsync(modId, apiKey);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("12345", result!.UniqueID);
        Assert.Equal("A test mod", result.Summary);
        Assert.Equal("Full description", result.Description);
        Assert.Equal("http://example.com/image.png", result.PictureUrl);
        Assert.Equal(100, result.DownloadCount);
        Assert.Equal(10, result.EndorsementCount);

        handlerMock
            .Protected()
            .Verify(
                "SendAsync",
                Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri!.ToString().Contains("/v1/games/stardewvalley/mods/12345.json") &&
                    req.Headers.Contains("apikey") &&
                    req.Headers.GetValues("apikey").First() == apiKey
                ),
                ItExpr.IsAny<CancellationToken>()
            );
    }

    [Fact]
    public async Task GetModInfoAsync_ReturnsNull_WhenApiCallFails()
    {
        // Arrange
        var modId = "12345";
        var apiKey = "test-api-key";

        var handlerMock = new Mock<HttpMessageHandler>();
        handlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>()
            )
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.NotFound
            });

        var httpClient = new HttpClient(handlerMock.Object);
        var loggerMock = new Mock<ILogger<ServiceNexusClient>>();
        var nexusClient = new ServiceNexusClient(httpClient, loggerMock.Object);

        // Act
        var result = await nexusClient.GetModInfoAsync(modId, apiKey);

        // Assert
        Assert.Null(result);

        // Verify error logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => true),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }

    [Theory]
    [InlineData("", "key")]
    [InlineData("id", "")]
    [InlineData(null, "key")]
    [InlineData("id", null)]
    public async Task GetModInfoAsync_ReturnsNull_WhenInputIsInvalid(string? modId, string? apiKey)
    {
        // Arrange
        var handlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(handlerMock.Object);
        var loggerMock = new Mock<ILogger<ServiceNexusClient>>();
        var nexusClient = new ServiceNexusClient(httpClient, loggerMock.Object);

        // Act
        // We use ! (null-forgiving) because the method expects non-null but checks for null internally/via IsNullOrWhiteSpace
        var result = await nexusClient.GetModInfoAsync(modId!, apiKey!);

        // Assert
        Assert.Null(result);

        // Verify warning logging
        loggerMock.Verify(
            x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("empty modId or apiKey")),
                It.IsAny<Exception>(),
                It.Is<Func<It.IsAnyType, Exception?, string>>((v, t) => true)),
            Times.Once);
    }
}
