using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VCA.Domain.Entities;

namespace VCA.Infrastructure.Data.Configurations;

public class LessonConfiguration : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("lessons");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Title).IsRequired().HasMaxLength(200);
        builder.Property(l => l.ContentJson).IsRequired().HasColumnType("jsonb");
        builder.Property(l => l.XpReward).IsRequired().HasDefaultValue(10);
        builder.Property(l => l.Order).IsRequired();
        builder.Property(l => l.Status).IsRequired();
        builder.Property(l => l.CreatedAt).IsRequired();

        builder.HasMany(l => l.Chunks).WithOne(c => c.Lesson).HasForeignKey(c => c.LessonId);
        builder.HasMany(l => l.Quizzes).WithOne(q => q.Lesson).HasForeignKey(q => q.LessonId);
        builder.HasMany(l => l.AiLogs).WithOne(a => a.Lesson).HasForeignKey(a => a.LessonId);
    }
}
