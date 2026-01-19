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
                    bool isSqlServer = context.Database.IsSqlServer();
                    if (isSqlServer) await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT produksi.tb_lwpmixing_Scw4MTypes ON");
                    await context.SaveChangesAsync();
                    if (isSqlServer) await context.Database.ExecuteSqlRawAsync("SET IDENTITY_INSERT produksi.tb_lwpmixing_Scw4MTypes OFF");
                }
                
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        });
    }

    private static async Task SeedRemarksAsync(ApplicationDbContext context)
    {
        var remarksData = new List<ScwRemark>
        {
            new() { Scw4MTypeId = 1, Description = "Rejection", DisplayOrder = 1 },
            new() { Scw4MTypeId = 1, Description = "Material Shortage", DisplayOrder = 2 },
            new() { Scw4MTypeId = 2, Description = "SOP Tak Sesuai Standar", DisplayOrder = 1 },
            new() { Scw4MTypeId = 3, Description = "Problem Mesin", DisplayOrder = 1 },
            new() { Scw4MTypeId = 4, Description = "Sakit", DisplayOrder = 1 },
            new() { Scw4MTypeId = 4, Description = "Izin", DisplayOrder = 2 },
            new() { Scw4MTypeId = 4, Description = "Alpha", DisplayOrder = 3 },
            new() { Scw4MTypeId = 4, Description = "Cuti", DisplayOrder = 4 },
            new() { Scw4MTypeId = 5, Description = "No Problem", DisplayOrder = 1 }
        };

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
        }
    }
}
