using Microsoft.EntityFrameworkCore;
using OeeSystem.Models;

namespace OeeSystem.Data;

public static class ScwDataSeeder
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        // 1. Seed Scw4MTypes
        await SeedTypesAsync(context);

        // 2. Seed ScwRemarks
        await SeedRemarksAsync(context);
    }

    private static async Task SeedTypesAsync(ApplicationDbContext context)
    {
        var types = new List<Scw4MType>
        {
            new() { Id = 1, Name = "Material", Code = "MATERIAL", DisplayOrder = 1 },
            new() { Id = 2, Name = "Methode", Code = "METHOD", DisplayOrder = 2 },
            new() { Id = 3, Name = "Machine", Code = "MACHINE", DisplayOrder = 3 },
            new() { Id = 4, Name = "Man", Code = "MAN", DisplayOrder = 4 },
            new() { Id = 5, Name = "No Problem", Code = "NO_PROBLEM", DisplayOrder = 5 }
        };

        bool hasChanges = false;
        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await context.Database.BeginTransactionAsync();
            try
            {
                foreach (var t in types)
                {
                    var existing = await context.Scw4MTypes.FindAsync(t.Id);
                    if (existing == null)
                    {
                        context.Scw4MTypes.Add(t);
                        hasChanges = true;
                    }
                    else
                    {
                        if (existing.Name != t.Name || existing.Code != t.Code || existing.DisplayOrder != t.DisplayOrder)
                        {
                            existing.Name = t.Name;
                            existing.Code = t.Code;
                            existing.DisplayOrder = t.DisplayOrder;
                            hasChanges = true;
                        }
                    }
                }

                if (hasChanges)
                {
                    // Enable IDENTITY_INSERT inside the transaction
                    await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT produksi.tb_lwpmixing_Scw4MTypes ON");
                    await context.SaveChangesAsync();
                    await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT produksi.tb_lwpmixing_Scw4MTypes OFF");
                    
                    await transaction.CommitAsync();
                    Console.WriteLine("✅ SCW 4M Types seeded/updated.");
                }
                else
                {
                    // No changes needed, but checking/updating might have opened transaction
                     await transaction.RollbackAsync(); 
                     Console.WriteLine("✅ SCW 4M Types up to date.");
                }
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                throw new Exception($"Failed to seed SCW Types: {ex.Message}", ex);
            }
        });
    }

    private static async Task SeedRemarksAsync(ApplicationDbContext context)
    {
        // Define Remarks Data
        // Format: (TypeId, Description, DisplayOrder)
        // Type 1: Material
        // Type 2: Methode
        // Type 3: Machine
        // Type 4: Man
        // Type 5: No Problem
        
        var remarksData = new List<ScwRemark>
        {
            // Material
            new() { Scw4MTypeId = 1, Description = "Rejection", DisplayOrder = 1 },
            new() { Scw4MTypeId = 1, Description = "Material Shortage", DisplayOrder = 2 },

            // Methode
            new() { Scw4MTypeId = 2, Description = "SOP Tak Sesuai Standar", DisplayOrder = 1 },

            // Machine
            new() { Scw4MTypeId = 3, Description = "Problem Mesin", DisplayOrder = 1 },

            // Man
            new() { Scw4MTypeId = 4, Description = "Sakit", DisplayOrder = 1 },
            new() { Scw4MTypeId = 4, Description = "Izin", DisplayOrder = 2 },
            new() { Scw4MTypeId = 4, Description = "Alpha", DisplayOrder = 3 },
            new() { Scw4MTypeId = 4, Description = "Cuti", DisplayOrder = 4 },

            // No Problem
            new() { Scw4MTypeId = 5, Description = "No Problem", DisplayOrder = 1 }
        };

        // Check Existing Remarks
        // We match by (Scw4MTypeId, Description) to avoid duplicates if IDs are auto-generated
        var existingRemarks = await context.ScwRemarks.ToListAsync();
        bool hasChanges = false;

        foreach (var remark in remarksData)
        {
            var exists = existingRemarks.Any(r => r.Scw4MTypeId == remark.Scw4MTypeId && 
                                                  r.Description.ToLower() == remark.Description.ToLower());
            if (!exists)
            {
                context.ScwRemarks.Add(remark);
                hasChanges = true;
            }
        }

        if (hasChanges)
        {
            await context.SaveChangesAsync();
            Console.WriteLine("✅ SCW Remarks seeded.");
        }
        else
        {
            Console.WriteLine("✅ SCW Remarks up to date.");
        }
    }

    private static async Task OpenIdentityInsertAsync(ApplicationDbContext context, string tableName)
    {
        try 
        {
            await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT produksi.{tableName} ON");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"⚠️ Could not set IDENTITY_INSERT ON for {tableName}: {ex.Message}");
        }
    }

    private static async Task CloseIdentityInsertAsync(ApplicationDbContext context, string tableName)
    {
        try 
        {
            await context.Database.ExecuteSqlRawAsync($"SET IDENTITY_INSERT produksi.{tableName} OFF");
        }
        catch (Exception ex)
        {
           Console.WriteLine($"⚠️ Could not set IDENTITY_INSERT OFF for {tableName}: {ex.Message}");
        }
    }
}
