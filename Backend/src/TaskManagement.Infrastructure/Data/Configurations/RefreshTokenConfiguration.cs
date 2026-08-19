using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskManagement.Domain.Entities;

namespace TaskManagement.Infrastructure.Data.Configurations
{
    public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Token).IsRequired().HasMaxLength(500);
            builder.Property(t => t.DeviceId).HasMaxLength(255);

            builder.HasOne(rt => rt.ProjectInvitation)
                   .WithMany()
                   .HasForeignKey(rt => rt.ProjectInvitationId)
                   .OnDelete(DeleteBehavior.SetNull);
            
            builder.HasOne(rt => rt.User)
                   .WithMany(u => u.RefreshTokens)
                   .HasForeignKey(rt => rt.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
