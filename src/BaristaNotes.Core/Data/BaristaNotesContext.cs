using Microsoft.EntityFrameworkCore;
using BaristaNotes.Core.Models;
using BaristaNotes.Core.Models.Enums;

namespace BaristaNotes.Core.Data;

public class BaristaNotesContext : DbContext
{
    public DbSet<Equipment> Equipment { get; set; } = null!;
    public DbSet<Bean> Beans { get; set; } = null!;
    public DbSet<Bag> Bags { get; set; } = null!;
    public DbSet<UserProfile> UserProfiles { get; set; } = null!;
    public DbSet<ShotRecord> ShotRecords { get; set; } = null!;
    public DbSet<ShotEquipment> ShotEquipments { get; set; } = null!;
    public DbSet<Recipe> Recipes { get; set; } = null!;
    public DbSet<GrinderProfile> GrinderProfiles { get; set; } = null!;
    public DbSet<GrindTranslationCache> GrindTranslationCache { get; set; } = null!;
    
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
        
        // Bag configuration
        modelBuilder.Entity<Bag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BeanId).IsRequired();
            entity.Property(e => e.RoastDate).IsRequired();
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.IsComplete).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.IsActive).IsRequired().HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            
            // Indexes for performance
            entity.HasIndex(e => e.BeanId);
            entity.HasIndex(e => new { e.BeanId, e.IsComplete, e.RoastDate }).IsDescending(false, false, true);
            entity.HasIndex(e => new { e.BeanId, e.RoastDate }).IsDescending(false, true);
            entity.HasIndex(e => e.SyncId).IsUnique();
            
            // Relationships
            entity.HasOne(e => e.Bean)
                .WithMany(b => b.Bags)
                .HasForeignKey(e => e.BeanId)
                .OnDelete(DeleteBehavior.Cascade);
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
            entity.Property(e => e.BagId).IsRequired();
            entity.Property(e => e.DoseIn).IsRequired().HasPrecision(5, 2);
            // Grind size in microns (canonical, grinder-agnostic). Nullable —
            // older rows pre-migration and "didn't record" cases legitimately
            // have no value.
            entity.Property(e => e.GrindMicrons);
            entity.Property(e => e.ExpectedTime).IsRequired().HasPrecision(5, 2);
            entity.Property(e => e.ExpectedOutput).IsRequired().HasPrecision(5, 2);
            entity.Property(e => e.DrinkType).IsRequired().HasMaxLength(50);
            entity.Property(e => e.BrewMethod)
                .IsRequired()
                .HasConversion<int>()
                .HasDefaultValue(BaristaNotes.Core.Models.Enums.BrewMethod.Espresso);
            entity.Property(e => e.ParametersJson);
            entity.Property(e => e.ActualTime).HasPrecision(5, 2);
            entity.Property(e => e.ActualOutput).HasPrecision(5, 2);
            entity.Property(e => e.PreinfusionTime).HasPrecision(5, 2);
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);
            
            entity.HasIndex(e => e.Timestamp).IsDescending();
            entity.HasIndex(e => e.BagId);
            entity.HasIndex(e => new { e.BagId, e.Rating });
            entity.HasIndex(e => e.MadeById);
            entity.HasIndex(e => e.MadeForId);
            entity.HasIndex(e => e.SyncId).IsUnique();
            
            // Relationships
            entity.HasOne(e => e.Bag)
                .WithMany(b => b.ShotRecords)
                .HasForeignKey(e => e.BagId)
                .OnDelete(DeleteBehavior.Cascade);
            
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
        
        // Recipe configuration
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.BeanId).IsRequired();
            entity.Property(e => e.BrewMethod).IsRequired();
            entity.Property(e => e.Source).IsRequired();
            entity.Property(e => e.SourceUrl).HasMaxLength(500);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.DoseIn).HasPrecision(5, 2);
            entity.Property(e => e.OutputAmount).HasPrecision(6, 2);
            entity.Property(e => e.GrindHint).HasMaxLength(100);
            entity.Property(e => e.BrewTempC).HasPrecision(5, 2);
            entity.Property(e => e.TotalTimeSeconds).HasPrecision(6, 2);
            entity.Property(e => e.ParametersJson).HasMaxLength(4000);
            entity.Property(e => e.Notes).HasMaxLength(2000);
            entity.Property(e => e.FetchedAt).IsRequired();
            entity.Property(e => e.IsEditedByUser).IsRequired().HasDefaultValue(false);
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

            entity.HasIndex(e => e.BeanId);
            entity.HasIndex(e => new { e.BeanId, e.BrewMethod });
            entity.HasIndex(e => e.SyncId).IsUnique();

            entity.HasOne(e => e.Bean)
                .WithMany(b => b.Recipes)
                .HasForeignKey(e => e.BeanId)
                .OnDelete(DeleteBehavior.Cascade);
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

        // GrinderProfile configuration
        modelBuilder.Entity<GrinderProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.EquipmentId).IsRequired();
            entity.Property(e => e.MinSetting).HasPrecision(6, 2);
            entity.Property(e => e.MaxSetting).HasPrecision(6, 2);
            entity.Property(e => e.StepSize).HasPrecision(6, 3);
            entity.Property(e => e.AnchorsJson).HasMaxLength(4000);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.LastModifiedAt).IsRequired();
            entity.Property(e => e.SyncId).IsRequired();
            entity.Property(e => e.IsDeleted).IsRequired().HasDefaultValue(false);

            entity.HasIndex(e => e.EquipmentId).IsUnique();
            entity.HasIndex(e => e.SyncId).IsUnique();

            entity.HasOne(e => e.Equipment)
                .WithMany()
                .HasForeignKey(e => e.EquipmentId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // GrindTranslationCache configuration
        modelBuilder.Entity<GrindTranslationCache>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.GrinderModelNormalized).IsRequired().HasMaxLength(100);
            entity.Property(e => e.GrindHintNormalized).IsRequired().HasMaxLength(100);
            entity.Property(e => e.BrewMethod).IsRequired().HasConversion<int>();
            entity.Property(e => e.MinSetting).HasPrecision(6, 2);
            entity.Property(e => e.MaxSetting).HasPrecision(6, 2);
            entity.Property(e => e.SuggestedSetting).HasPrecision(6, 2);
            entity.Property(e => e.Confidence).IsRequired().HasMaxLength(10);
            entity.Property(e => e.Source).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Explanation).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).IsRequired();
            entity.Property(e => e.ExpiresAt).IsRequired();

            entity.HasIndex(e => new { e.GrinderModelNormalized, e.GrindHintNormalized, e.BrewMethod })
                .IsUnique()
                .HasDatabaseName("IX_GrindTranslationCache_Key");
            entity.HasIndex(e => e.ExpiresAt);
        });
    }
}
