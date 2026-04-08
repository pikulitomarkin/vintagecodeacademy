using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VCA.Domain.Entities;

namespace VCA.Infrastructure.Data.Configurations;

public class TrailConfiguration : IEntityTypeConfiguration<Trail>
{
    public void Configure(EntityTypeBuilder<Trail> builder)
    {
        builder.ToTable("trails");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Title).IsRequired().HasMaxLength(200);
        builder.Property(t => t.Description).IsRequired().HasMaxLength(2000);
        builder.Property(t => t.Stack).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Level).IsRequired();
        builder.Property(t => t.Order).IsRequired();
        builder.Property(t => t.IsPublished).IsRequired().HasDefaultValue(false);
        builder.Property(t => t.CreatedAt).IsRequired();

        builder.HasMany(t => t.Modules).WithOne(m => m.Trail).HasForeignKey(m => m.TrailId);
    }
}
