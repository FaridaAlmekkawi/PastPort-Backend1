using FluentAssertions;
using Moq;
using PastPort.Application.DTOs.Request;
using PastPort.Application.Interfaces;
using PastPort.Application.Services;
using PastPort.Domain.Entities;
using PastPort.Domain.Interfaces;
using Xunit;

namespace PastPort.Tests.Services;

public class ConversationServiceTests
{
    private readonly Mock<IConversationRepository> _convRepo = new();
    private readonly Mock<ICharacterRepository> _charRepo = new();
    private readonly Mock<IAIConversationService> _aiService = new();
    private readonly ConversationService _sut;

    public ConversationServiceTests()
    {
        _sut = new ConversationService(_convRepo.Object, _charRepo.Object, _aiService.Object);
    }

    [Fact]
    public async Task CreateConversationAsync_CreatesAndPersists()
    {
        // Arrange
        var userId = "user1";
        var request = new CreateConversationRequestDto { CharacterId = Guid.NewGuid(), UserMessage = "Hi" };
        var character = new Character { Id = request.CharacterId, Name = "TestNPC" };
        
        _charRepo.Setup(r => r.GetByIdAsync(request.CharacterId)).ReturnsAsync(character);
        _convRepo.Setup(r => r.GetUserConversationsWithCharacterAsync(userId, request.CharacterId))
            .ReturnsAsync(new List<Conversation>());
        _aiService.Setup(s => s.GenerateCharacterResponseAsync(character, request.UserMessage, It.IsAny<List<string>>()))
            .ReturnsAsync("Hello there!");

        // Act
        var result = await _sut.CreateConversationAsync(userId, request);

        // Assert
        result.CharacterResponse.Should().Be("Hello there!");
        _convRepo.Verify(r => r.AddAsync(It.IsAny<Conversation>()), Times.Once);
    }
}
