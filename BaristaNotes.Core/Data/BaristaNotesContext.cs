using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Data;

public class BaristaNotesContext : DbContext
{
    public DbSet<Equipment> Equipment { get; set; } = null!;
    public DbSet<Bean> Beans { get; set; } = null!;
    public DbSet<UserProfile> UserProfiles { get; set; } = null!;
    public DbSet<ShotRecord> ShotRecords { get; set; } = null!;
    public DbSet<ShotEquipment> ShotEquipments { get; set; } = null!;
    
    public BaristaNotesContext(DbContextOptions<BaristaNotesContext> options) 
        : base(options) { }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Equipment configuration
        modelBuilder.Entity<Equipment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Type).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            
            entity.HasIndex(e => new { e.Name, e.Type });
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.SyncId).IsUnique();
        });
        
        // Bean configuration
        modelBuilder.Entity<Bean>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
            entity.Property(e => e.Roaster).HasMaxLength(100);
            entity.Property(e => e.Origin).HasMaxLength(100);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            
            entity.HasIndex(e => new { e.Name, e.Roaster });
            entity.HasIndex(e => e.IsActive);
            entity.HasIndex(e => e.SyncId).IsUnique();
        });
        
        // UserProfile configuration
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
            entity.Property(e => e.AvatarPath).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            
            entity.HasIndex(e => e.Name);
            entity.HasIndex(e => e.SyncId).IsUnique();
        });
        
        // ShotRecord configuration
        modelBuilder.Entity<ShotRecord>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Timestamp).IsRequired();
            entity.Property(e => e.DoseIn).IsRequired().HasPrecision(5, 2);
            entity.Property(e => e.GrindSetting).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ExpectedTime).IsRequired().HasPrecision(5, 2);
            entity.Property(e => e.ExpectedOutput).IsRequired().HasPrecision(5, 2);
            entity.Property(e => e.DrinkType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.ActualTime).HasPrecision(5, 2);
            entity.Property(e => e.ActualOutput).HasPrecision(5, 2);
            entity.Property(e => e.PreinfusionTime).HasPrecision(5, 2);
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            
            entity.HasIndex(e => e.Timestamp).IsDescending();
            entity.HasIndex(e => e.BeanId);
            entity.HasIndex(e => e.MadeById);
            entity.HasIndex(e => e.MadeForId);
            entity.HasIndex(e => e.SyncId).IsUnique();
            
            // Relationships
            entity.HasOne(e => e.Bean)
                .WithMany(b => b.ShotRecords)
                .HasForeignKey(e => e.BeanId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Machine)
                .WithMany()
                .HasForeignKey(e => e.MachineId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.Grinder)
                .WithMany()
                .HasForeignKey(e => e.GrinderId)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.MadeBy)
                .WithMany(u => u.ShotsMadeBy)
                .HasForeignKey(e => e.MadeById)
                .OnDelete(DeleteBehavior.SetNull);
            
            entity.HasOne(e => e.MadeFor)
                .WithMany(u => u.ShotsMadeFor)
                .HasForeignKey(e => e.MadeForId)
                .OnDelete(DeleteBehavior.SetNull);
        });
        
        // ShotEquipment (junction) configuration
        modelBuilder.Entity<ShotEquipment>(entity =>
        {
            entity.HasKey(e => new { e.ShotRecordId, e.EquipmentId });
            
            entity.HasOne(e => e.ShotRecord)
                .WithMany(s => s.ShotEquipments)
                .HasForeignKey(e => e.ShotRecordId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(e => e.Equipment)
                .WithMany(eq => eq.ShotEquipments)
                .HasForeignKey(e => e.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
