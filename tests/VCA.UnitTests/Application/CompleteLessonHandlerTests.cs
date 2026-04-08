using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using VCA.Application.Gamification.CompleteLesson;
using VCA.Domain.Entities;
using VCA.Domain.Interfaces;

namespace VCA.UnitTests.Application;

/// <summary>
/// Testes unitários para o handler de conclusão de aula.
/// </summary>
public class CompleteLessonHandlerTests
{
    private readonly Mock<IUnitOfWork> _uowMock = new();
    private readonly Mock<IUserRepository> _userRepo = new();
    private readonly Mock<ILessonRepository> _lessonRepo = new();
    private readonly Mock<IUserProgressRepository> _progressRepo = new();
    private readonly Mock<IAiGenerationLogRepository> _aiLogRepo = new();
    private readonly Mock<ILogger<CompleteLessonHandler>> _loggerMock = new();

    public CompleteLessonHandlerTests()
    {
        _uowMock.Setup(u => u.Users).Returns(_userRepo.Object);
        _uowMock.Setup(u => u.Lessons).Returns(_lessonRepo.Object);
        _uowMock.Setup(u => u.UserProgresses).Returns(_progressRepo.Object);
        _uowMock.Setup(u => u.AiGenerationLogs).Returns(_aiLogRepo.Object);
    }

    [Fact]
    public async Task HandleAsync_WhenLessonAlreadyCompleted_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();

        _progressRepo.Setup(r => r.HasCompletedAsync(userId, lessonId, default)).ReturnsAsync(true);

        var handler = new CompleteLessonHandler(_uowMock.Object, _loggerMock.Object);
        var result = await handler.HandleAsync(new CompleteLessonCommand(userId, lessonId));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("já foi concluída");
    }

    [Fact]
    public async Task HandleAsync_WhenValidRequest_ShouldGrantXpAndReturnSuccess()
    {
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();
        var user = User.Create(userId, "dev@test.com", "Dev");
        var lesson = Lesson.Create(Guid.NewGuid(), "Intro ao C#", 1, xpReward: 10);

        _progressRepo.Setup(r => r.HasCompletedAsync(userId, lessonId, default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync(user);
        _lessonRepo.Setup(r => r.GetByIdAsync(lessonId, default)).ReturnsAsync(lesson);
        _progressRepo.Setup(r => r.AddAsync(It.IsAny<UserProgress>(), default)).Returns(Task.CompletedTask);
        _uowMock.Setup(u => u.SaveChangesAsync(default)).ReturnsAsync(1);

        var handler = new CompleteLessonHandler(_uowMock.Object, _loggerMock.Object);
        var result = await handler.HandleAsync(new CompleteLessonCommand(userId, lessonId));

        result.IsSuccess.Should().BeTrue();
        result.Value!.XpEarned.Should().Be(10);
        result.Value.TotalXp.Should().Be(10);
    }

    [Fact]
    public async Task HandleAsync_WhenUserNotFound_ShouldReturnFailure()
    {
        var userId = Guid.NewGuid();
        var lessonId = Guid.NewGuid();

        _progressRepo.Setup(r => r.HasCompletedAsync(userId, lessonId, default)).ReturnsAsync(false);
        _userRepo.Setup(r => r.GetByIdAsync(userId, default)).ReturnsAsync((User?)null);

        var handler = new CompleteLessonHandler(_uowMock.Object, _loggerMock.Object);
        var result = await handler.HandleAsync(new CompleteLessonCommand(userId, lessonId));

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Usuário não encontrado");
    }
}
