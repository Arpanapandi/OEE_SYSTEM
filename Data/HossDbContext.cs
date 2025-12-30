using Microsoft.EntityFrameworkCore;
using OeeSystem.Models;

namespace OeeSystem.Data;

public class HossDbContext : DbContext
{
    public HossDbContext(DbContextOptions<HossDbContext> options)
        : base(options)
    {
    }

    public DbSet<Komponen> Komponen { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // Map ke tabel komponen di DB_HOSE
        // Tabel: dbo.komponen (schema: dbo, table: komponen)
        modelBuilder.Entity<Komponen>(entity =>
        {
            entity.ToTable("komponen", "dbo"); // Schema: dbo, Table: komponen
            entity.HasKey(e => e.Id);
            
            // Mapping kolom (sesuaikan dengan struktur tabel sebenarnya)
            entity.Property(e => e.PartNumber)
                .HasColumnName("Part_Number") // Nama kolom di tabel
                .HasMaxLength(100);

            entity.Property(e => e.JmlKomponen)
                .HasColumnName("jml_komponen"); // Nama kolom di tabel
        });
    }
}

