using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VCA.Domain.Entities;

namespace VCA.Infrastructure.Data.Configurations;

/// <summary>
/// Configuração EF Core para a entidade User — mapeia para a tabela 'users'.
/// </summary>
public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");

        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();

        builder.Property(u => u.Email).IsRequired().HasMaxLength(255);
        builder.HasIndex(u => u.Email).IsUnique();

        builder.Property(u => u.Name).IsRequired().HasMaxLength(100);
        builder.Property(u => u.AvatarUrl).HasMaxLength(500);
        builder.Property(u => u.Xp).IsRequired().HasDefaultValue(0);
        builder.Property(u => u.Level).IsRequired();
        builder.Property(u => u.StreakDays).IsRequired().HasDefaultValue(0);
        builder.Property(u => u.CreatedAt).IsRequired();

        builder.HasMany(u => u.Progress).WithOne(p => p.User).HasForeignKey(p => p.UserId);
        builder.HasMany(u => u.Badges).WithOne(b => b.User).HasForeignKey(b => b.UserId);
        builder.HasMany(u => u.QuizAttempts).WithOne(a => a.User).HasForeignKey(a => a.UserId);
        builder.HasMany(u => u.Donations).WithOne(d => d.User).HasForeignKey(d => d.UserId);
        builder.HasMany(u => u.Rankings).WithOne(r => r.User).HasForeignKey(r => r.UserId);
    }
}
