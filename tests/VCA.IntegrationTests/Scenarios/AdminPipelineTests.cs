using System.Net;
using System.Net.Http.Headers;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using VCA.Domain.Entities;
using VCA.Domain.Enums;
using VCA.Domain.Interfaces;
using VCA.IntegrationTests.Infrastructure;

namespace VCA.IntegrationTests.Scenarios;

/// <summary>
/// Pipeline admin: upload de PDF mock → IA mockada gera conteúdo + quiz →
/// rascunho persistido → publicação valida e marca a aula como Published.
/// </summary>
[Collection("Vca")]
public class AdminPipelineTests
{
    private readonly VcaWebApplicationFactory _factory;
    public AdminPipelineTests(VcaWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ProcessPdf_ThenPublish_LessonGoesFromDraftToPublished()
    {
        // Seed admin + lesson em status Draft
        var (adminId, lessonId) = await SeedAdminAndLessonAsync();

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, adminId.ToString());
        client.DefaultRequestHeaders.Add(TestAuthHandler.RoleHeader, "Admin");

        // Upload de PDF mock (qualquer bytes — o extractor é fake).
        using var content = new MultipartFormDataContent();
        var pdfBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
        var pdfPart = new ByteArrayContent(pdfBytes);
        pdfPart.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        content.Add(pdfPart, "pdf", "fake.pdf");
        content.Add(new StringContent("Intermediate"), "difficulty");
        content.Add(new StringContent("csharp"), "stack");
        content.Add(new StringContent("true"), "generateQuiz");
        content.Add(new StringContent("10"), "quizQuestionCount");

        var processResp = await client.PostAsync(
            $"/api/admin/lessons/{lessonId}/process-pdf", content);

        processResp.StatusCode.Should().Be(HttpStatusCode.OK);

        // Verifica persistência: conteúdo + pelo menos 5 quizzes (mínimo p/ publicar)
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var lesson = await uow.Lessons.GetByIdAsync(lessonId);
            lesson.Should().NotBeNull();
            lesson!.ContentJson.Should().NotBeNullOrWhiteSpace();
            lesson.Status.Should().Be(LessonStatus.PendingReview);

            var quizzes = await uow.Quizzes.GetByLessonAsync(lessonId);
            quizzes.Count.Should().BeGreaterOrEqualTo(5);
        }

        // Publicação
        var publishResp = await client.PutAsync(
            $"/api/admin/lessons/{lessonId}/publish", content: null);
        publishResp.StatusCode.Should().Be(HttpStatusCode.NoContent);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var lesson = await uow.Lessons.GetByIdAsync(lessonId);
            lesson!.Status.Should().Be(LessonStatus.Published);
            lesson.PublishedAt.Should().NotBeNull();
        }
    }

    [Fact]
    public async Task ProcessPdf_WithoutAdminRole_ReturnsForbidden()
    {
        var (_, lessonId) = await SeedAdminAndLessonAsync();
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.UserIdHeader, Guid.NewGuid().ToString());
        // Sem role Admin → 403.

        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(new byte[] { 1, 2, 3 }), "pdf", "x.pdf" }
        };
        var resp = await client.PostAsync($"/api/admin/lessons/{lessonId}/process-pdf", content);
        resp.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private async Task<(Guid adminId, Guid lessonId)> SeedAdminAndLessonAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var admin = User.Create(Guid.NewGuid(), $"admin-{Guid.NewGuid():N}@t.com", "Admin");
        await uow.Users.AddAsync(admin);

        var trail = Trail.Create("T", "d", "csharp", TrailLevel.Beginner, 1);
        await uow.Trails.AddAsync(trail);
        var module = Module.Create(trail.Id, "M", 1);
        await uow.Modules.AddAsync(module);
        var lesson = Lesson.Create(module.Id, "Aula Pipeline", 1, xpReward: 30);
        await uow.Lessons.AddAsync(lesson);

        await uow.SaveChangesAsync();
        return (admin.Id, lesson.Id);
    }
}
