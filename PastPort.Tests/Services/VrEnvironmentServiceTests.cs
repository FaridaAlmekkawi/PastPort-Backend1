using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Moq.Protected;
using PastPort.Application.Common;
using PastPort.Infrastructure.ExternalServices.AI;
using Xunit;
using FluentAssertions;
using System.Text.Json;

namespace PastPort.Tests.Services;

public class VrEnvironmentServiceTests
{
    private readonly Mock<HttpMessageHandler> _handlerMock = new();
    private readonly HttpClient _httpClient;
    private readonly VrEnvironmentService _sut;

    public VrEnvironmentServiceTests()
    {
        _httpClient = new HttpClient(_handlerMock.Object);
        var settings = Options.Create(new VrGeneratorSettings { BaseUrl = "http://test" });
        _sut = new VrEnvironmentService(_httpClient, settings, NullLogger<VrEnvironmentService>.Instance);
    }

    [Fact]
    public async Task CheckHealthAsync_ReturnsTrue_OnSuccess()
    {
        _handlerMock.Protected().Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK });

        var result = await _sut.CheckHealthAsync();
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://gizmo-battering-moaning.ngrok-free.dev/docs", "https://gizmo-battering-moaning.ngrok-free.dev/")]
    [InlineData("https://gizmo-battering-moaning.ngrok-free.dev", "https://gizmo-battering-moaning.ngrok-free.dev/")]
    public void NormalizeBaseAddress_RemovesDocsPath(string input, string expected)
    {
        var result = VrEnvironmentService.NormalizeBaseAddress(input);

        result.Should().Be(expected);
    }
}
