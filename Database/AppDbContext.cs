using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using NoteVault.Models;

namespace NoteVault.Database;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Folder> Folders => Set<Folder>();
    public DbSet<Pile> Piles => Set<Pile>();
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<NoteVersion> NoteVersions => Set<NoteVersion>();
    public DbSet<PileNote> PileNotes => Set<PileNote>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<NoteTag> NoteTags => Set<NoteTag>();
    public DbSet<AppSettings> AppSettings { get; set; } = null!;
    public DbSet<NoteShare> NoteShares { get; set; }
    public DbSet<FolderShare> FolderShares { get; set; }
    public DbSet<NoteEditLock> NoteEditLocks { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        //-------Open Registration-------
        modelBuilder.Entity<AppSettings>().HasData(new AppSettings
        {
            Id = 1,
            IsRegistrationOpen = false
        });
        
        //-------Folder-------
        modelBuilder.Entity<Folder>(entity =>
        {
            entity.HasOne(f => f.User)
                .WithMany(u => u.Folders)
                .HasForeignKey(f => f.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(f => f.Piles)
                .WithOne(p => p.Folder)
                .HasForeignKey(p => p.FolderId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(f => f.Notes)
                .WithOne(n => n.Folder)
                .HasForeignKey(n => n.FolderId)
                .OnDelete(DeleteBehavior.SetNull);

            entity.HasQueryFilter(f => f.DeletedAt == null);
        });
        
        //-------Pile-------
        modelBuilder.Entity<Pile>(entity =>
        {
            entity.HasQueryFilter(p => p.DeletedAt == null);
        });
        
        //-------Note-------
        modelBuilder.Entity<Note>(entity =>
        {
            entity.HasOne(n => n.User)
                .WithMany(u => u.Notes)
                .HasForeignKey(n => n.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasMany(n => n.Versions)
                .WithOne(v => v.Note)
                .HasForeignKey(v => v.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasQueryFilter(n => n.DeletedAt == null);
            
            entity.Property(n => n.SearchVector)
                .HasColumnType("tsvector")
                .HasComputedColumnSql(
                    "to_tsvector('english', coalesce(\"Title\", '') || ' ' || coalesce(regexp_replace(\"Content\", '<[^>]*>', ' ', 'g'), ''))",
                    stored: true);
            
            entity.HasIndex(n => n.SearchVector)
                .HasMethod("GIN");
        });
        
        //-------NoteVersion-------
        modelBuilder.Entity<NoteVersion>(entity =>
        {
            entity.HasIndex(v => new { v.NoteId, v.VersionNumber }).IsUnique();
        });
        
        //-------PileNote (join table)-------
        modelBuilder.Entity<PileNote>(entity =>
        {
            entity.HasKey(pn => new { pn.PileId, pn.NoteId });
            
            entity.HasOne(pn => pn.Pile)
                .WithMany(p => p.PileNotes)
                .HasForeignKey(pn => pn.PileId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(pn => pn.Note)
                .WithMany(n => n.PileNotes)
                .HasForeignKey(pn => pn.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        
        //-------Tag-------
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasOne(t => t.User)
                .WithMany(u => u.Tags)
                .HasForeignKey(t => t.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(t => new {t.UserId, t.Name }).IsUnique();
        });
        
        //-------NoteTag-------
        modelBuilder.Entity<NoteTag>(entity =>
        {
            entity.HasKey(nt => new { nt.NoteId, nt.TagId });
            
            entity.HasOne(nt => nt.Note)
                .WithMany(n => n.NoteTags)
                .HasForeignKey(nt => nt.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(nt => nt.Tag)
                .WithMany(t => t.NoteTags)
                .HasForeignKey(nt => nt.TagId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        //------Sharing constraints + indexes------
        modelBuilder.Entity<NoteShare>(entity =>
        {
            entity.HasIndex(ns => new {ns.NoteId, ns.SharedWithUserId})
                .IsUnique();
            
            entity.HasIndex(ns => ns.SharedWithUserId);
            
            entity.HasOne(ns => ns.Note)
                .WithMany(n => n.Shares)
                .HasForeignKey(ns => ns.NoteId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(ns => ns.SharedWithUser)
                .WithMany(u => u.NoteSharesReceived)
                .HasForeignKey(ns => ns.SharedWithUserId)
                .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(ns => ns.SharedByUser)
                .WithMany(u => u.NoteSharesInitiated)
                .HasForeignKey(ns => ns.SharedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasQueryFilter(ns => ns.Note.DeletedAt == null);
        });

        modelBuilder.Entity<FolderShare>(entity =>
        {
            entity.HasIndex(fs => new { fs.FolderId, fs.SharedWithUserId })
                .IsUnique();

            entity.HasIndex(fs => fs.SharedWithUserId);

            entity.HasOne(fs => fs.Folder)
                .WithMany(f => f.Shares)
                .HasForeignKey(fs => fs.FolderId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(fs => fs.SharedWithUser)
                .WithMany(u => u.FolderSharesReceived)
                .HasForeignKey(fs => fs.SharedWithUserId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(fs => fs.SharedByUser)
                .WithMany(u => u.FolderSharesInitiated)
                .HasForeignKey(fs => fs.SharedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
            
            entity.HasQueryFilter(fs => fs.Folder.DeletedAt == null);
        });
        
        modelBuilder.Entity<NoteEditLock>(entity =>
        {
            entity.HasIndex(el => el.NoteId)
                .IsUnique();
        });
    }
    
}