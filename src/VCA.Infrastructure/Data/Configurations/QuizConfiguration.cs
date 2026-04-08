using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VCA.Domain.Entities;

namespace VCA.Infrastructure.Data.Configurations;

public class QuizConfiguration : IEntityTypeConfiguration<Quiz>
{
    public void Configure(EntityTypeBuilder<Quiz> builder)
    {
        builder.ToTable("quizzes");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Question).IsRequired().HasMaxLength(1000);
        builder.Property(q => q.OptionsJson).IsRequired().HasColumnType("jsonb");
        builder.Property(q => q.CorrectIndex).IsRequired();
        builder.Property(q => q.Explanation).IsRequired().HasMaxLength(2000);
        builder.Property(q => q.CreatedAt).IsRequired();
    }
}
