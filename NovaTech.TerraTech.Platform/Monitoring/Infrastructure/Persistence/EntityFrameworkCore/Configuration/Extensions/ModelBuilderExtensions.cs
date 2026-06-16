using Microsoft.EntityFrameworkCore;
using NovaTech.TerraTech.Platform.Monitoring.Domain.Model.Aggregates;

namespace NovaTech.TerraTech.Platform.Monitoring.Infrastructure.Persistence.EntityFrameworkCore.Configuration.Extensions;

public static class ModelBuilderExtensions
{
    public static void ApplyMonitoringConfiguration(this ModelBuilder builder)
    {
        builder.Entity<Field>(field =>
        {
            field.HasKey(f => f.Id);
            field.Property(f => f.Id).IsRequired().ValueGeneratedOnAdd();

            // ProfileId
            field.OwnsOne(f => f.ProfileId, profile =>
            {
                profile.WithOwner().HasForeignKey("Id");
                profile.Property(p => p.Value).HasColumnName("ProfileId").IsRequired();
            });

            // Name
            field.OwnsOne(f => f.Name, name =>
            {
                name.WithOwner().HasForeignKey("Id");
                name.Property(n => n.Value).HasColumnName("Name").HasMaxLength(100).IsRequired();
            });

            // SizeM2
            field.OwnsOne(f => f.SizeM2, size =>
            {
                size.WithOwner().HasForeignKey("Id");
                size.Property(s => s.Value).HasColumnName("SizeM2").IsRequired();
            });

            // SoilType
            field.OwnsOne(f => f.SoilType, soil =>
            {
                soil.WithOwner().HasForeignKey("Id");
                soil.Property(s => s.Value).HasColumnName("SoilType").HasMaxLength(50).IsRequired();
            });

            // LocationLatLong
            field.OwnsOne(f => f.LocationLatLong, loc =>
            {
                loc.WithOwner().HasForeignKey("Id");
                loc.Property(l => l.Latitude).HasColumnName("Latitude").IsRequired();
                loc.Property(l => l.Longitude).HasColumnName("Longitude").IsRequired();
            });
        });
    }
}