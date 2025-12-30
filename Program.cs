using Microsoft.EntityFrameworkCore;
using OeeSystem.Data;
using OeeSystem.Services;
using OeeSystem.Hubs;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// SignalR Service
builder.Services.AddSignalR();

// DbContext
// Ganti nama database untuk menghindari konflik schema lama di LocalDB
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
                     ?? "Server=(localdb)\\MSSQLLocalDB;Database=OeeSystemDbV2;Trusted_Connection=True;MultipleActiveResultSets=true";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// DbContext untuk db_HOSS
var hossConnectionString = builder.Configuration.GetConnectionString("HossConnection")
    ?? throw new InvalidOperationException("Connection string 'HossConnection' not found.");

// ✅ Aktifkan retry policy untuk koneksi ke DB_HOSS (mengatasi transient failure)
builder.Services.AddDbContext<HossDbContext>(options =>
    options.UseSqlServer(hossConnectionString, sqlOptions =>
    {
        sqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(30),
            errorNumbersToAdd: null);
    }));

// OEE service
builder.Services.AddScoped<IOeeService, OeeService>();

// Background service untuk Dandori timer
builder.Services.AddHostedService<DandoriTimerService>();

var app = builder.Build();

// Set default URL jika tidak ada dari command line
// Gunakan 0.0.0.0 agar bisa diakses dari device lain di jaringan yang sama
if (app.Urls.Count == 0)
{
    app.Urls.Add("http://0.0.0.0:6001");
    app.Urls.Add("https://0.0.0.0:6002");
    Console.WriteLine("🌐 Server listening on:");
    Console.WriteLine("   - HTTP:  http://0.0.0.0:6001");
    Console.WriteLine("   - HTTPS: https://0.0.0.0:6002");
    Console.WriteLine("📱 Akses dari device lain di jaringan yang sama:");
    Console.WriteLine("   - HTTP:  http://[IP_WIFI_PC_ANDA]:6001");
    Console.WriteLine("   - HTTPS: https://[IP_WIFI_PC_ANDA]:6002");
    Console.WriteLine("   - Contoh: http://10.14.180.197:6001");
    Console.WriteLine("   - Contoh: https://10.14.180.197:6002");
}

// Auto-create database schema & seed minimal data (tanpa CLI migrations)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Hapus dan buat ulang database untuk memastikan schema selalu up-to-date
        // PERHATIAN: Ini akan menghapus semua data yang ada!
        //db.Database.EnsureDeleted();
        //db.Database.EnsureCreated();
        
        // Test database connection with timeout
        bool canConnect = false;
        try
        {
            using (var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5)))
            {
                canConnect = await db.Database.CanConnectAsync(cts.Token);
            }
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("WARNING: Database connection timeout. Melanjutkan tanpa database...");
            canConnect = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING: Tidak dapat terhubung ke database: {ex.Message}");
            canConnect = false;
        }
        
        if (!canConnect)
        {
            Console.WriteLine("WARNING: Tidak dapat terhubung ke database. Pastikan SQL Server berjalan dan connection string benar.");
            Console.WriteLine("INFO: Aplikasi akan berjalan tanpa database. Fitur yang memerlukan database mungkin tidak berfungsi.");
        }
        else
        {
            // ✅ TAMBAHKAN: Tambahkan kolom Dandori ke tabel JobRuns TERLEBIH DAHULU (sebelum query apapun)
            // Ini penting untuk menghindari error "Token 2000000 is not valid" saat EF Core mencoba memetakan property Dandori
            try
            {
                Console.WriteLine("INFO: Memeriksa dan menambahkan kolom Dandori ke tabel JobRuns...");
                
                // Tambahkan kolom Dandori + kolom hasil scan jika belum ada (dalam satu batch untuk efisiensi)
                var result = await db.Database.ExecuteSqlRawAsync(@"
                    IF EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[JobRuns]') AND type in (N'U'))
                    BEGIN
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('JobRuns') AND name = 'DandoriStartTime')
                        BEGIN
                            ALTER TABLE JobRuns ADD DandoriStartTime DATETIME2 NULL;
                        END
                        
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('JobRuns') AND name = 'DandoriEndTime')
                        BEGIN
                            ALTER TABLE JobRuns ADD DandoriEndTime DATETIME2 NULL;
                        END
                        
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('JobRuns') AND name = 'DandoriDurationSeconds')
                        BEGIN
                            ALTER TABLE JobRuns ADD DandoriDurationSeconds INT NULL;
                        END

                        -- Kolom hasil scan produksi dari DB_HOSS
                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('JobRuns') AND name = 'ScannedPartNumber')
                        BEGIN
                            ALTER TABLE JobRuns ADD ScannedPartNumber NVARCHAR(100) NULL;
                        END

                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('JobRuns') AND name = 'ScannedLotNumber')
                        BEGIN
                            ALTER TABLE JobRuns ADD ScannedLotNumber NVARCHAR(100) NULL;
                        END

                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('JobRuns') AND name = 'ScannedKomponenId')
                        BEGIN
                            ALTER TABLE JobRuns ADD ScannedKomponenId INT NULL;
                        END

                        IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('JobRuns') AND name = 'ScannedJmlKomponen')
                        BEGIN
                            ALTER TABLE JobRuns ADD ScannedJmlKomponen INT NULL;
                        END
                    END");
                
                Console.WriteLine($"INFO: Kolom Dandori sudah tersedia di tabel JobRuns (result: {result})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: Error saat menambahkan kolom Dandori: {ex.Message}");
                Console.WriteLine($"ERROR: Stack trace: {ex.StackTrace}");
                // Jangan stop aplikasi, biarkan tetap berjalan
                // Tapi log error dengan jelas untuk debugging
            }

            // Create ProductNgTypes table if not exists
            try
            {
                // Create table
                await db.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.objects WHERE object_id = OBJECT_ID(N'[dbo].[ProductNgTypes]') AND type in (N'U'))
                    BEGIN
                        CREATE TABLE [dbo].[ProductNgTypes] (
                            [ProductId] INT NOT NULL,
                            [NgTypeId] INT NOT NULL,
                            CONSTRAINT [PK_ProductNgTypes] PRIMARY KEY CLUSTERED ([ProductId] ASC, [NgTypeId] ASC),
                            CONSTRAINT [FK_ProductNgTypes_Products] FOREIGN KEY ([ProductId]) 
                                REFERENCES [dbo].[Products] ([Id]) ON DELETE NO ACTION,
                            CONSTRAINT [FK_ProductNgTypes_NgTypes] FOREIGN KEY ([NgTypeId]) 
                                REFERENCES [dbo].[NgTypes] ([Id]) ON DELETE NO ACTION
                        )
                    END");

                // Create indexes if not exists
                await db.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProductNgTypes_ProductId' AND object_id = OBJECT_ID('ProductNgTypes'))
                    BEGIN
                        CREATE NONCLUSTERED INDEX [IX_ProductNgTypes_ProductId] 
                            ON [dbo].[ProductNgTypes] ([ProductId] ASC)
                    END");

                await db.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_ProductNgTypes_NgTypeId' AND object_id = OBJECT_ID('ProductNgTypes'))
                    BEGIN
                        CREATE NONCLUSTERED INDEX [IX_ProductNgTypes_NgTypeId] 
                            ON [dbo].[ProductNgTypes] ([NgTypeId] ASC)
                    END");

                Console.WriteLine("INFO: Tabel ProductNgTypes sudah dibuat atau sudah ada");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Error saat membuat tabel ProductNgTypes: {ex.Message}");
                // Jangan stop aplikasi, biarkan tetap berjalan
            }

            // ✅ PERBAIKAN: Tambahkan kolom InjectionGroup ke tabel ProductionCounts jika belum ada
            try
            {
                await db.Database.ExecuteSqlRawAsync(@"
                    IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID('ProductionCounts') AND name = 'InjectionGroup')
                    BEGIN
                        ALTER TABLE ProductionCounts
                        ADD InjectionGroup NVARCHAR(50) NULL;
                        PRINT 'Kolom InjectionGroup berhasil ditambahkan ke tabel ProductionCounts';
                    END");
                Console.WriteLine("INFO: Kolom InjectionGroup sudah tersedia di tabel ProductionCounts");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Error saat menambahkan kolom InjectionGroup: {ex.Message}");
                // Jangan stop aplikasi, biarkan tetap berjalan
            }


            // Rename kolom IdealCycleTimeSeconds menjadi StandarCycleTime jika masih ada
            try
            {
                // Cek apakah kolom IdealCycleTimeSeconds masih ada dan StandarCycleTime belum ada
                var checkOldColumn = await db.Database.ExecuteSqlRawAsync(@"
                    IF EXISTS (
                        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = 'Products' AND COLUMN_NAME = 'IdealCycleTimeSeconds'
                    ) AND NOT EXISTS (
                        SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = 'Products' AND COLUMN_NAME = 'StandarCycleTime'
                    )
                    BEGIN
                        EXEC sp_rename 'Products.IdealCycleTimeSeconds', 'StandarCycleTime', 'COLUMN'
                    END");
                Console.WriteLine("INFO: Kolom IdealCycleTimeSeconds sudah diubah menjadi StandarCycleTime (jika diperlukan)");
            }
            catch (Exception ex)
            {
                // Jika kolom sudah di-rename atau tidak ada, abaikan error
                Console.WriteLine($"INFO: Kolom sudah menggunakan nama StandarCycleTime atau tidak perlu diubah: {ex.Message}");
            }

            // Auto-update status lama ke status baru di database menggunakan raw SQL
            try
            {
                // Update status menggunakan raw SQL untuk menghindari conversion issues
                var updateCount1 = await db.Database.ExecuteSqlRawAsync(
                    "UPDATE Machines SET Status = N'Aktif' WHERE Status IN (N'Running', N'Idle', N'NoLoading')");
                
                var updateCount2 = await db.Database.ExecuteSqlRawAsync(
                    "UPDATE Machines SET Status = N'TidakAktif' WHERE Status = N'Down'");
                
                if (updateCount1 > 0 || updateCount2 > 0)
                {
                    Console.WriteLine($"INFO: Berhasil mengupdate {updateCount1 + updateCount2} machine dari status lama ke status baru.");
                    Console.WriteLine($"      - Running/Idle/NoLoading -> Aktif: {updateCount1} records");
                    Console.WriteLine($"      - Down -> Tidak Aktif: {updateCount2} records");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"WARNING: Error saat update status machine: {ex.Message}");
                // Jangan stop aplikasi, biarkan tetap berjalan
            }

            // Seed default shifts jika belum ada
    if (!db.Shifts.Any())
    {
        db.Shifts.AddRange(
            new OeeSystem.Models.Shift
            {
                Name = "Shift 1",
                StartTime = new TimeSpan(7, 30, 0),
                EndTime = new TimeSpan(19, 30, 0)
            },
            new OeeSystem.Models.Shift
            {
                Name = "Shift 2",
                StartTime = new TimeSpan(19, 30, 0),
                EndTime = new TimeSpan(7, 30, 0) // 07:30 hari berikutnya
            }
        );
        db.SaveChanges();
    }

    // Seed master data dummy jika kosong
    if (!db.Users.Any())
    {
        db.Users.AddRange(
            new OeeSystem.Models.User { Username = "admin", Role = OeeSystem.Models.UserRole.Admin, ProfileImageUrl = "/images/users/admin.jpg" },
            new OeeSystem.Models.User { Username = "budi_santoso", Role = OeeSystem.Models.UserRole.Operator, ProfileImageUrl = "/images/users/operator1.jpg" },
            new OeeSystem.Models.User { Username = "andi_mesin", Role = OeeSystem.Models.UserRole.Operator, ProfileImageUrl = "/images/users/operator2.jpg" }
        );
        db.SaveChanges();
    }

    if (!db.Plants.Any())
    {
        db.Plants.AddRange(
            new OeeSystem.Models.Plant { Code = "PLT01", Name = "Plant Cikarang" },
            new OeeSystem.Models.Plant { Code = "PLT02", Name = "Plant Karawang" }
        );
        db.SaveChanges();
    }

    if (!db.Products.Any())
    {
        var plt01Id = db.Plants.First(p => p.Code == "PLT01").Id;
        db.Products.AddRange(
            new OeeSystem.Models.Product 
            { 
                Name = "Bearing R-12 High Speed", 
                MaterialCode = "BRG-R12",
                UoM = "PCS",
                SLOC = "WH01",
                PlantId = plt01Id,
                ImageUrl = "https://placehold.co/200x200/png?text=Bearing+R12",
                StandarCycleTime = 10.5
            },
            new OeeSystem.Models.Product 
            { 
                Name = "Industrial Valve V-55", 
                MaterialCode = "VLV-V55",
                UoM = "PCS",
                SLOC = "WH01",
                PlantId = plt01Id,
                ImageUrl = "https://placehold.co/200x200/png?text=Valve+V55",
                StandarCycleTime = 45.0
            },
            new OeeSystem.Models.Product 
            { 
                Name = "Gear Shaft X-100", 
                MaterialCode = "GRS-X100",
                UoM = "PCS",
                SLOC = "WH01",
                PlantId = plt01Id,
                ImageUrl = "https://placehold.co/200x200/png?text=Gear+Shaft",
                StandarCycleTime = 5.0
            }
        );
        db.SaveChanges();
    }

    if (!db.DowntimeReasons.Any())
    {
        db.DowntimeReasons.AddRange(
            new OeeSystem.Models.DowntimeReason { Category = "Planned", Description = "Setup / Changeover" },
            new OeeSystem.Models.DowntimeReason { Category = "Planned", Description = "Rest Break" },
            new OeeSystem.Models.DowntimeReason { Category = "Unplanned", Description = "Machine Failure" },
            new OeeSystem.Models.DowntimeReason { Category = "Unplanned", Description = "Material Shortage" },
            new OeeSystem.Models.DowntimeReason { Category = "Unplanned", Description = "Tooling Broken" }
        );
        db.SaveChanges();
    }

    if (!db.Machines.Any())
    {
        var plt01Id = db.Plants.First(p => p.Code == "PLT01").Id;
        db.Machines.AddRange(
            new OeeSystem.Models.Machine { Id = "M001", Name = "Press Machine 01", LineId = "Line A", PlantId = plt01Id, Status = OeeSystem.Models.MachineStatus.Aktif, ImageUrl = "/images/machines/press01.jpg" },
            new OeeSystem.Models.Machine { Id = "M002", Name = "CNC Lathe 02", LineId = "Line A", PlantId = plt01Id, Status = OeeSystem.Models.MachineStatus.TidakAktif, ImageUrl = "/images/machines/cnc02.jpg" },
            new OeeSystem.Models.Machine { Id = "M003", Name = "Assembly Robot 03", LineId = "Line B", PlantId = plt01Id, Status = OeeSystem.Models.MachineStatus.Aktif, ImageUrl = "/images/machines/robot03.jpg" }
        );
        db.SaveChanges();
    }

    // Mapping contoh Machine <-> DowntimeReason
    if (!db.MachineDowntimeReasons.Any())
    {
        try
        {
            var setupId = db.DowntimeReasons.First(r => r.Category == "Planned" && r.Description == "Setup / Changeover").Id;
            var restId = db.DowntimeReasons.First(r => r.Category == "Planned" && r.Description == "Rest Break").Id;
            var failureId = db.DowntimeReasons.First(r => r.Category == "Unplanned" && r.Description == "Machine Failure").Id;
            var materialId = db.DowntimeReasons.First(r => r.Category == "Unplanned" && r.Description == "Material Shortage").Id;
            var toolingId = db.DowntimeReasons.First(r => r.Category == "Unplanned" && r.Description == "Tooling Broken").Id;

            // Cek apakah machine dengan ID tersebut ada di database
            var machineIds = new[] { "M001", "M002", "M003" };
            var existingMachines = db.Machines.Where(m => machineIds.Contains(m.Id)).Select(m => m.Id).ToList();
            
            var mappings = new List<OeeSystem.Models.MachineDowntimeReason>();
            
            if (existingMachines.Contains("M001"))
            {
                mappings.Add(new OeeSystem.Models.MachineDowntimeReason { MachineId = "M001", DowntimeReasonId = setupId });
                mappings.Add(new OeeSystem.Models.MachineDowntimeReason { MachineId = "M001", DowntimeReasonId = restId });
                mappings.Add(new OeeSystem.Models.MachineDowntimeReason { MachineId = "M001", DowntimeReasonId = failureId });
            }
            
            if (existingMachines.Contains("M002"))
            {
                mappings.Add(new OeeSystem.Models.MachineDowntimeReason { MachineId = "M002", DowntimeReasonId = failureId });
                mappings.Add(new OeeSystem.Models.MachineDowntimeReason { MachineId = "M002", DowntimeReasonId = materialId });
            }
            
            if (existingMachines.Contains("M003"))
            {
                mappings.Add(new OeeSystem.Models.MachineDowntimeReason { MachineId = "M003", DowntimeReasonId = toolingId });
                mappings.Add(new OeeSystem.Models.MachineDowntimeReason { MachineId = "M003", DowntimeReasonId = failureId });
            }
            
            if (mappings.Any())
            {
                db.MachineDowntimeReasons.AddRange(mappings);
                db.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING: Error saat seeding MachineDowntimeReasons: {ex.Message}");
            // Jangan stop aplikasi, biarkan tetap berjalan
        }
    }

    // Mapping contoh Product <-> Machine
    if (!db.ProductMachines.Any())
    {
        try
        {
            // Cek apakah product dan machine dengan ID tersebut ada di database
            var productIds = new[] { 1, 2, 3 };
            var machineIds = new[] { "M001", "M002", "M003" };
            
            var existingProducts = db.Products.Where(p => productIds.Contains(p.Id)).Select(p => p.Id).ToList();
            var existingMachines = db.Machines.Where(m => machineIds.Contains(m.Id)).Select(m => m.Id).ToList();
            
            var mappings = new List<OeeSystem.Models.ProductMachine>();
            
            if (existingProducts.Contains(1) && existingMachines.Contains("M001"))
            {
                mappings.Add(new OeeSystem.Models.ProductMachine { ProductId = 1, MachineId = "M001" });
            }
            if (existingProducts.Contains(1) && existingMachines.Contains("M002"))
            {
                mappings.Add(new OeeSystem.Models.ProductMachine { ProductId = 1, MachineId = "M002" });
            }
            if (existingProducts.Contains(2) && existingMachines.Contains("M002"))
            {
                mappings.Add(new OeeSystem.Models.ProductMachine { ProductId = 2, MachineId = "M002" });
            }
            if (existingProducts.Contains(3) && existingMachines.Contains("M003"))
            {
                mappings.Add(new OeeSystem.Models.ProductMachine { ProductId = 3, MachineId = "M003" });
            }
            
            if (mappings.Any())
            {
                db.ProductMachines.AddRange(mappings);
                db.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING: Error saat seeding ProductMachines: {ex.Message}");
            // Jangan stop aplikasi, biarkan tetap berjalan
        }
    }

    if (!db.WorkOrders.Any())
    {
        db.WorkOrders.AddRange(
            new OeeSystem.Models.WorkOrder { OrderNumber = "WO-2025-001", ProductId = 1, TargetQuantity = 1000, Status = OeeSystem.Models.WorkOrderStatus.InProgress },
            new OeeSystem.Models.WorkOrder { OrderNumber = "WO-2025-002", ProductId = 2, TargetQuantity = 500, Status = OeeSystem.Models.WorkOrderStatus.Planned },
            new OeeSystem.Models.WorkOrder { OrderNumber = "WO-2025-003", ProductId = 3, TargetQuantity = 200, Status = OeeSystem.Models.WorkOrderStatus.Completed }
        );
        db.SaveChanges();
    }

    // Seed NgTypes jika belum ada
    if (!db.NgTypes.Any())
    {
        db.NgTypes.AddRange(
            new OeeSystem.Models.NgType { Code = "NG01", Name = "Burr", Category = "Visual", Description = "Adanya burr pada produk" },
            new OeeSystem.Models.NgType { Code = "NG02", Name = "Scratch", Category = "Visual", Description = "Goresan pada permukaan" },
            new OeeSystem.Models.NgType { Code = "NG03", Name = "Dimension Out", Category = "Dimension", Description = "Dimensi tidak sesuai spesifikasi" },
            new OeeSystem.Models.NgType { Code = "NG04", Name = "Crack", Category = "Visual", Description = "Retak pada produk" },
            new OeeSystem.Models.NgType { Code = "NG05", Name = "Surface Defect", Category = "Visual", Description = "Cacat permukaan" }
        );
        db.SaveChanges();
    }

    if (!db.JobRuns.Any())
    {
        try
        {
            var now = DateTime.Now;
            
            // Cek apakah machine, workorder, dan operator dengan ID tersebut ada
            var machineIds = new[] { "M001", "M002", "M003" };
            var workOrderIds = new[] { 1, 2, 3 };
            var operatorIds = new[] { 2, 3 };
            
            var existingMachines = db.Machines.Where(m => machineIds.Contains(m.Id)).Select(m => m.Id).ToList();
            var existingWorkOrders = db.WorkOrders.Where(w => workOrderIds.Contains(w.Id)).Select(w => w.Id).ToList();
            var existingOperators = db.Users.Where(u => operatorIds.Contains(u.Id)).Select(u => u.Id).ToList();
            
            var jobs = new List<OeeSystem.Models.JobRun>();
            
            if (existingMachines.Contains("M001") && existingWorkOrders.Contains(1) && existingOperators.Contains(2))
            {
                jobs.Add(new OeeSystem.Models.JobRun
                {
                    MachineId = "M001",
                    WorkOrderId = 1,
                    OperatorId = 2,
                    StartTime = now.AddHours(-2),
                    EndTime = null
                });
            }
            
            if (existingMachines.Contains("M003") && existingWorkOrders.Contains(3) && existingOperators.Contains(3))
            {
                jobs.Add(new OeeSystem.Models.JobRun
                {
                    MachineId = "M003",
                    WorkOrderId = 3,
                    OperatorId = 3,
                    StartTime = now.AddHours(-5),
                    EndTime = now.AddHours(-1)
                });
            }
            
            if (existingMachines.Contains("M002") && existingWorkOrders.Contains(2) && existingOperators.Contains(2))
            {
                jobs.Add(new OeeSystem.Models.JobRun
                {
                    MachineId = "M002",
                    WorkOrderId = 2,
                    OperatorId = 2,
                    StartTime = now.AddHours(-4),
                    EndTime = null
                });
            }
            
            if (jobs.Any())
            {
                db.JobRuns.AddRange(jobs);
                db.SaveChanges();
                
                // Downtime aktif untuk mesin 2 (job3) jika ada
                if (jobs.Count >= 3 && jobs[2].Id > 0)
                {
                    var job3 = jobs[2];
                    var failureReasonId = db.DowntimeReasons.FirstOrDefault(r => r.Category == "Unplanned" && r.Description == "Machine Failure")?.Id;
                    if (failureReasonId.HasValue)
                    {
                        db.DowntimeEvents.Add(new OeeSystem.Models.DowntimeEvent
                        {
                            JobRunId = job3.Id,
                            ReasonId = failureReasonId.Value,
                            StartTime = now.AddMinutes(-30),
                            EndTime = null,
                            DurationSeconds = 0
                        });
                    }
                }
                
                // Production counts untuk job pertama jika ada
                if (jobs.Count > 0 && jobs[0].Id > 0)
                {
                    var job1 = jobs[0];
                    db.ProductionCounts.AddRange(
                        new OeeSystem.Models.ProductionCount
                        {
                            JobRunId = job1.Id,
                            Timestamp = now,
                            GoodCount = 50,
                            RejectCount = 0,
                            RejectReason = null
                        },
                        new OeeSystem.Models.ProductionCount
                        {
                            JobRunId = job1.Id,
                            Timestamp = now.AddMinutes(-15),
                            GoodCount = 48,
                            RejectCount = 2,
                            RejectReason = "Scratch"
                        }
                    );
                }
                
                db.SaveChanges();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"WARNING: Error saat seeding JobRuns: {ex.Message}");
            // Jangan stop aplikasi, biarkan tetap berjalan
        }
    }

    // Sinkronkan gambar produk dari mesin yang terhubung
    try
    {
        var productsToSync = db.Products
            .Include(p => p.ProductMachines)
                .ThenInclude(pm => pm.Machine)
            .Where(p => p.ProductMachines.Any())
            .ToList();
        
        bool hasChanges = false;
        foreach (var product in productsToSync)
        {
            // Ambil gambar dari mesin pertama yang memiliki gambar
            var machineWithImage = product.ProductMachines
                .Select(pm => pm.Machine)
                .FirstOrDefault(m => m != null && !string.IsNullOrEmpty(m.ImageUrl));
            
            if (machineWithImage != null && !string.IsNullOrEmpty(machineWithImage.ImageUrl))
            {
                if (product.ImageUrl != machineWithImage.ImageUrl)
                {
                    product.ImageUrl = machineWithImage.ImageUrl;
                    hasChanges = true;
                }
            }
        }
        
        if (hasChanges)
        {
            db.SaveChanges();
            Console.WriteLine("INFO: Gambar produk berhasil disinkronkan dengan gambar mesin.");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"WARNING: Error saat sinkronisasi gambar produk: {ex.Message}");
        // Jangan stop aplikasi, biarkan tetap berjalan
    }
        } // End of else block (canConnect)
    }
    catch (Exception ex)
    {
        Console.WriteLine($"ERROR saat seeding database: {ex.Message}");
        Console.WriteLine($"StackTrace: {ex.StackTrace}");
        // Jangan stop aplikasi, biarkan tetap berjalan
    }
}

// Configure the HTTP request pipeline.
// Enable detailed error pages in development untuk melihat error detail
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Enable HTTPS redirection untuk mendukung HTTPS
app.UseHttpsRedirection();

// Add Permissions Policy untuk mengizinkan akses kamera di HTTPS
app.Use(async (context, next) =>
{
    // Permissions Policy: Izinkan camera, microphone, dan autoplay
    context.Response.Headers.Append("Permissions-Policy", 
        "camera=(self), microphone=(self), autoplay=(self)");
    
    // Content Security Policy: Izinkan inline scripts dan external resources untuk scanner
    context.Response.Headers.Append("Content-Security-Policy", 
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://unpkg.com https://cdn.jsdelivr.net https://cdnjs.cloudflare.com https://serratus.github.io; " +
        "style-src 'self' 'unsafe-inline' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net; " +
        "img-src 'self' data: https: blob:; " +
        "font-src 'self' https://cdnjs.cloudflare.com https://cdn.jsdelivr.net data:; " +
        "connect-src 'self' https: wss: ws:; " +
        "media-src 'self' blob:; " +
        "frame-src 'self' blob:;");
    
    await next();
});

app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

// Map SignalR Hub
app.MapHub<OeeHub>("/oeeHub");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();


