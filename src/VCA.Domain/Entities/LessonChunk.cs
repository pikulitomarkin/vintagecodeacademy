namespace VCA.Domain.Entities;

/// <summary>
/// Fragmento de texto extraído de um PDF para processamento pela IA.
/// </summary>
public class LessonChunk
{
    public Guid Id { get; private set; }
    public Guid LessonId { get; private set; }
    public int ChunkIndex { get; private set; }
    public string RawText { get; private set; } = string.Empty;
    public DateTime GeneratedAt { get; private set; }

    public Lesson? Lesson { get; private set; }

    private LessonChunk() { }

    public static LessonChunk Create(Guid lessonId, int chunkIndex, string rawText)
    {
        return new LessonChunk
        {
            Id = Guid.NewGuid(),
            LessonId = lessonId,
            ChunkIndex = chunkIndex,
            RawText = rawText,
            GeneratedAt = DateTime.UtcNow
        };
    }
}
