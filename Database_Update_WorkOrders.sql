-- ============================================
-- Script untuk menambahkan kolom PlannedDate dan ShiftId ke tabel WorkOrders
-- Jalankan script ini di SQL Server Management Studio atau tool database lainnya
-- Database: OeeSystemDb
-- ============================================

USE [OeeSystemDb]
GO

-- Tambahkan kolom PlannedDate
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[WorkOrders]') AND name = 'PlannedDate')
BEGIN
    ALTER TABLE [dbo].[WorkOrders]
    ADD [PlannedDate] [datetime2](7) NULL;
    PRINT 'Kolom PlannedDate berhasil ditambahkan';
END
ELSE
BEGIN
    PRINT 'Kolom PlannedDate sudah ada';
END
GO

-- Tambahkan kolom ShiftId
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[dbo].[WorkOrders]') AND name = 'ShiftId')
BEGIN
    ALTER TABLE [dbo].[WorkOrders]
    ADD [ShiftId] [int] NULL;
    PRINT 'Kolom ShiftId berhasil ditambahkan';
END
ELSE
BEGIN
    PRINT 'Kolom ShiftId sudah ada';
END
GO

-- Tambahkan foreign key ke tabel Shifts (pastikan tabel Shifts sudah ada)
IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_WorkOrders_Shifts_ShiftId')
BEGIN
    IF EXISTS (SELECT * FROM sys.tables WHERE name = 'Shifts')
    BEGIN
        ALTER TABLE [dbo].[WorkOrders]
        ADD CONSTRAINT [FK_WorkOrders_Shifts_ShiftId] 
        FOREIGN KEY([ShiftId])
        REFERENCES [dbo].[Shifts] ([Id])
        ON DELETE SET NULL;
        PRINT 'Foreign key FK_WorkOrders_Shifts_ShiftId berhasil ditambahkan';
    END
    ELSE
    BEGIN
        PRINT 'WARNING: Tabel Shifts tidak ditemukan. Foreign key tidak dapat ditambahkan.';
    END
END
ELSE
BEGIN
    PRINT 'Foreign key FK_WorkOrders_Shifts_ShiftId sudah ada';
END
GO

-- Buat index untuk ShiftId
IF NOT EXISTS (SELECT * FROM sys.indexes WHERE name = 'IX_WorkOrders_ShiftId' AND object_id = OBJECT_ID('WorkOrders'))
BEGIN
    CREATE NONCLUSTERED INDEX [IX_WorkOrders_ShiftId] 
    ON [dbo].[WorkOrders] ([ShiftId] ASC);
    PRINT 'Index IX_WorkOrders_ShiftId berhasil dibuat';
END
ELSE
BEGIN
    PRINT 'Index IX_WorkOrders_ShiftId sudah ada';
END
GO

PRINT '============================================';
PRINT 'Script update database selesai!';
PRINT 'Kolom PlannedDate dan ShiftId sudah ditambahkan ke tabel WorkOrders';
PRINT '============================================';
GO

