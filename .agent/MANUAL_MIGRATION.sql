-- Custom OEE Formula: Add Flags to DowntimeEvents
-- Execute this script manually in SQL Server Management Studio or Azure Data Studio

USE [Velasto];
GO

-- Check if columns already exist
IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[produksi].[tb_lwpmixing_DowntimeEvents]') AND name = 'IsRestBreak')
BEGIN
    PRINT 'Adding IsRestBreak column...';
    ALTER TABLE [produksi].[tb_lwpmixing_DowntimeEvents]
    ADD IsRestBreak BIT NOT NULL DEFAULT 0;
END
ELSE
BEGIN
    PRINT 'IsRestBreak column already exists.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[produksi].[tb_lwpmixing_DowntimeEvents]') AND name = 'IsNoLoading')
BEGIN
    PRINT 'Adding IsNoLoading column...';
    ALTER TABLE [produksi].[tb_lwpmixing_DowntimeEvents]
    ADD IsNoLoading BIT NOT NULL DEFAULT 0;
END
ELSE
BEGIN
    PRINT 'IsNoLoading column already exists.';
END
GO

IF NOT EXISTS (SELECT * FROM sys.columns WHERE object_id = OBJECT_ID(N'[produksi].[tb_lwpmixing_DowntimeEvents]') AND name = 'IsLineStop')
BEGIN
    PRINT 'Adding IsLineStop column...';
    ALTER TABLE [produksi].[tb_lwpmixing_DowntimeEvents]
    ADD IsLineStop BIT NOT NULL DEFAULT 0;
END
ELSE
BEGIN
    PRINT 'IsLineStop column already exists.';
END
GO

-- Update existing data based on Reason
PRINT 'Updating existing downtime events...';

UPDATE de
SET de.IsRestBreak = CASE 
    WHEN dr.Description LIKE '%Rest%' OR dr.Description LIKE '%Istirahat%' THEN 1 
    ELSE 0 
END,
de.IsNoLoading = CASE 
    WHEN dr.Description LIKE '%No Loading%' OR dr.Description LIKE '%NoLoading%' THEN 1 
    ELSE 0 
END,
de.IsLineStop = CASE 
    WHEN dr.Category = 'Unplanned' THEN 1 
    ELSE 0 
END
FROM [produksi].[tb_lwpmixing_DowntimeEvents] de
INNER JOIN [produksi].[tb_lwpmixing_DowntimeReasons] dr ON de.ReasonId = dr.Id;

PRINT 'Migration completed successfully!';

-- Verification query
SELECT 
    COUNT(*) as Total,
    SUM(CASE WHEN IsRestBreak = 1 THEN 1 ELSE 0 END) as RestBreaks,
    SUM(CASE WHEN IsNoLoading = 1 THEN 1 ELSE 0 END) as NoLoadings,
    SUM(CASE WHEN IsLineStop = 1 THEN 1 ELSE 0 END) as LineStops
FROM [produksi].[tb_lwpmixing_DowntimeEvents];
