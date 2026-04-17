/*
 * FILE          : FullDatabase.sql
 * PROJECT       : Advanced SQL Project 
 * PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli
 * FIRST VERSION : 2026-03-05
 * DESCRIPTION   : This script creates the database and all objects required for the
 *                 Kanban manufacturing simulation. Includes tables, views, functions,
 *                 stored procedures, triggers, and indices.
 */

------------------------------------------ Create the database if it doesn't exist ------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'FogLampAssemblyDB')
BEGIN
    CREATE DATABASE FogLampAssemblyDB;
END
GO

USE FogLampAssemblyDB;
GO

--========================================================================================================
-- DROP EXISTING OBJECTS (clean rerun)
--========================================================================================================
IF OBJECT_ID('trg_LowStockAlert', 'TR') IS NOT NULL DROP TRIGGER trg_LowStockAlert;
GO
IF OBJECT_ID('dbo.vw_WorkstationStatus', 'V') IS NOT NULL DROP VIEW dbo.vw_WorkstationStatus;
GO
IF OBJECT_ID('dbo.vw_ProductionSummary', 'V') IS NOT NULL DROP VIEW dbo.vw_ProductionSummary;
GO
IF OBJECT_ID('dbo.vw_ActiveAlerts', 'V') IS NOT NULL DROP VIEW dbo.vw_ActiveAlerts;
GO
IF OBJECT_ID('dbo.fn_GetBuildTime', 'FN') IS NOT NULL DROP FUNCTION dbo.fn_GetBuildTime;
GO
IF OBJECT_ID('dbo.fn_GetDefectRate', 'FN') IS NOT NULL DROP FUNCTION dbo.fn_GetDefectRate;
GO
IF OBJECT_ID('dbo.fn_GetConfigValue', 'FN') IS NOT NULL DROP FUNCTION dbo.fn_GetConfigValue;
GO
IF OBJECT_ID('dbo.sp_BuildLamp', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_BuildLamp;
GO
IF OBJECT_ID('dbo.sp_CompleteLamp', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_CompleteLamp;
GO
IF OBJECT_ID('dbo.sp_CancelLamp', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_CancelLamp;
GO
IF OBJECT_ID('dbo.sp_RefillBin', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_RefillBin;
GO
IF OBJECT_ID('dbo.sp_RefillAllBins', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_RefillAllBins;
GO
IF OBJECT_ID('dbo.sp_RefillStock', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_RefillStock;
GO
IF OBJECT_ID('dbo.sp_ResetSimulation', 'P') IS NOT NULL DROP PROCEDURE dbo.sp_ResetSimulation;
GO
IF OBJECT_ID('dbo.LowStockAlert', 'U') IS NOT NULL DROP TABLE dbo.LowStockAlert;
GO
IF OBJECT_ID('dbo.ProductionLog', 'U') IS NOT NULL DROP TABLE dbo.ProductionLog;
GO
IF OBJECT_ID('dbo.WorkstationBin', 'U') IS NOT NULL DROP TABLE dbo.WorkstationBin;
GO
IF OBJECT_ID('dbo.Workstation', 'U') IS NOT NULL DROP TABLE dbo.Workstation;
GO
IF OBJECT_ID('dbo.Part', 'U') IS NOT NULL DROP TABLE dbo.Part;
GO
IF OBJECT_ID('dbo.Worker', 'U') IS NOT NULL DROP TABLE dbo.Worker;
GO
IF OBJECT_ID('dbo.Configuration', 'U') IS NOT NULL DROP TABLE dbo.Configuration;
GO

--========================================================================================================
-- TABLES
--========================================================================================================

------------------------------------------ Configuration table ------------------------------------------
CREATE TABLE dbo.Configuration 
(
    configID     int            NOT NULL IDENTITY(1,1),
    settingName  nvarchar(50)   NOT NULL,
    settingValue decimal(10,2)  NOT NULL,
    description  nvarchar(200)  NULL,
    CONSTRAINT PK_Configuration PRIMARY KEY CLUSTERED (configID ASC),
    CONSTRAINT UQ_SettingName UNIQUE (settingName)
);
GO

INSERT INTO dbo.Configuration (settingName, settingValue, description)
VALUES 
    ('TimeScaleMultiplier',   1.00,   'Multiplier for simulation speed (e.g. 60 = 1 real sec per sim min)'),
    ('HarnessBinCapacity',    55.00,  'Starting capacity for Harness bins'),
    ('ReflectorBinCapacity',  35.00,  'Starting capacity for Reflector bins'),
    ('HousingBinCapacity',    24.00,  'Starting capacity for Housing bins'),
    ('LensBinCapacity',       40.00,  'Starting capacity for Lens bins'),
    ('BulbBinCapacity',       60.00,  'Starting capacity for Bulb bins'),
    ('BezelBinCapacity',      75.00,  'Starting capacity for Bezel bins'),
    ('TargetOrderAmount',     500.00, 'Total lamps required for the Kanban order'),
    ('LowStockThreshold',     5.00,   'Parts remaining when runner is notified'),
    ('RunnerIntervalMinutes', 5.00,   'Minutes between runner pickup cycles'),
    ('NumberOfStations',      3.00,   'Number of active assembly stations'),
    ('RookieBuildTimeSec',    90.00,  'Base build time for Rookie workers (60 * 1.5)'),
    ('NormalBuildTimeSec',    60.00,  'Base build time for Normal workers'),
    ('SuperBuildTimeSec',     51.00,  'Base build time for Super workers (60 * 0.85)'),
    ('BuildTimeVariancePct',  10.00,  'Plus/minus percent variance on build time'),
    ('RookieDefectRate',      0.85,   'Defect rate percent for Rookie workers'),
    ('NormalDefectRate',      0.50,   'Defect rate percent for Normal workers'),
    ('SuperDefectRate',       0.15,   'Defect rate percent for Super workers');
GO

------------------------------------------ Worker table ------------------------------------------
CREATE TABLE dbo.Worker
(
    workerID   int IDENTITY(1,1) PRIMARY KEY,
    firstName  nvarchar(50)  NOT NULL,
    lastName   nvarchar(50)  NOT NULL,
    skillLevel nvarchar(20)  NOT NULL
        CONSTRAINT CK_Worker_SkillLevel CHECK (skillLevel IN ('Rookie', 'Normal', 'Super'))
);
GO

------------------------------------------ Part table ------------------------------------------
CREATE TABLE dbo.Part
(
    partID          int IDENTITY(1,1) PRIMARY KEY,
    partName        nvarchar(50) NOT NULL,
    defaultCapacity int NOT NULL
);
GO

------------------------------------------ Workstation table ------------------------------------------
CREATE TABLE dbo.Workstation
(
    stationID       int IDENTITY(1,1) PRIMARY KEY,
    stationName     nvarchar(50)  NOT NULL,
    currentWorkerID int           NULL,
    status          nvarchar(20)  NOT NULL DEFAULT 'Idle'
        CONSTRAINT CK_Workstation_Status CHECK (status IN ('Running', 'Idle', 'Blocked')),
    CONSTRAINT FK_Workstation_Worker FOREIGN KEY (currentWorkerID) REFERENCES dbo.Worker(workerID)
);
GO

------------------------------------------ WorkstationBin table ------------------------------------------
CREATE TABLE dbo.WorkstationBin
(
    binID           int IDENTITY(1,1) PRIMARY KEY,
    stationID       int NOT NULL,
    partID          int NOT NULL,
    currentQuantity int NOT NULL,
    CONSTRAINT FK_WorkstationBin_Workstation FOREIGN KEY (stationID) REFERENCES dbo.Workstation(stationID),
    CONSTRAINT FK_WorkstationBin_Part FOREIGN KEY (partID) REFERENCES dbo.Part(partID),
    CONSTRAINT UQ_StationPart UNIQUE (stationID, partID)
);
GO

------------------------------------------ ProductionLog table ------------------------------------------
CREATE TABLE dbo.ProductionLog
(
    logID            int IDENTITY(1,1) PRIMARY KEY,
    stationID        int NOT NULL,
    workerID         int NOT NULL,
    buildTimeSeconds decimal(6,2) NOT NULL,
    passedQA         bit NOT NULL,
    isComplete       bit NOT NULL DEFAULT 0,
    [timestamp]      datetime NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_ProductionLog_Workstation FOREIGN KEY (stationID) REFERENCES dbo.Workstation(stationID),
    CONSTRAINT FK_ProductionLog_Worker FOREIGN KEY (workerID) REFERENCES dbo.Worker(workerID)
);
GO

------------------------------------------ LowStockAlert table ------------------------------------------
CREATE TABLE dbo.LowStockAlert
(
    alertID    int IDENTITY(1,1) PRIMARY KEY,
    stationID  int NOT NULL,
    partID     int NOT NULL,
    alertTime  datetime NOT NULL DEFAULT GETDATE(),
    isResolved bit NOT NULL DEFAULT 0,
    CONSTRAINT FK_LowStockAlert_Workstation FOREIGN KEY (stationID) REFERENCES dbo.Workstation(stationID),
    CONSTRAINT FK_LowStockAlert_Part FOREIGN KEY (partID) REFERENCES dbo.Part(partID)
);
GO

--========================================================================================================
-- INDICES
--========================================================================================================
CREATE NONCLUSTERED INDEX IX_ProductionLog_StationID  ON dbo.ProductionLog(stationID);
GO
CREATE NONCLUSTERED INDEX IX_ProductionLog_Timestamp  ON dbo.ProductionLog([timestamp]);
GO
CREATE NONCLUSTERED INDEX IX_ProductionLog_PassedQA   ON dbo.ProductionLog(passedQA) INCLUDE (stationID, workerID);
GO
CREATE NONCLUSTERED INDEX IX_WorkstationBin_StationID ON dbo.WorkstationBin(stationID) INCLUDE (partID, currentQuantity);
GO
CREATE NONCLUSTERED INDEX IX_LowStockAlert_Unresolved ON dbo.LowStockAlert(isResolved, stationID) WHERE isResolved = 0;
GO

--========================================================================================================
-- SEED DATA
--========================================================================================================

INSERT INTO dbo.Part (partName, defaultCapacity)
VALUES
    ('Harness', 55), ('Reflector', 35), ('Housing', 24),
    ('Lens', 40),    ('Bulb', 60),      ('Bezel', 75);
GO

INSERT INTO dbo.Worker (firstName, lastName, skillLevel)
VALUES
    ('Ali',   'Hassan', 'Rookie'),
    ('Omar',  'Khan',   'Normal'),
    ('Yusuf', 'Rahman', 'Super'),
    ('John',  'Smith',  'Normal'),
    ('Sara',  'Ahmed',  'Rookie'),
    ('Layla', 'Karim',  'Super');
GO

INSERT INTO dbo.Workstation (stationName, currentWorkerID, status)
VALUES
    ('Station 1', 1, 'Running'),
    ('Station 2', 2, 'Running'),
    ('Station 3', 3, 'Running');
GO

INSERT INTO dbo.WorkstationBin (stationID, partID, currentQuantity)
SELECT w.stationID, p.partID, p.defaultCapacity
FROM dbo.Workstation w
CROSS JOIN dbo.Part p;
GO

--========================================================================================================
-- FUNCTIONS
--========================================================================================================

------------------------------------------ fn_GetConfigValue ------------------------------------------
CREATE FUNCTION dbo.fn_GetConfigValue(@settingName nvarchar(50))
RETURNS decimal(10,2)
AS
BEGIN
    DECLARE @val decimal(10,2);
    SELECT @val = settingValue FROM dbo.Configuration WHERE settingName = @settingName;
    RETURN @val;
END;
GO

------------------------------------------ fn_GetBuildTime ------------------------------------------
-- Returns a randomized build time based on skill level with +/- variance from config
CREATE FUNCTION dbo.fn_GetBuildTime(@skillLevel nvarchar(20), @randomSeed float)
RETURNS decimal(6,2)
AS
BEGIN
    DECLARE @baseTime decimal(10,2);
    DECLARE @variance decimal(10,2);

    SET @baseTime = CASE @skillLevel
        WHEN 'Rookie' THEN (SELECT settingValue FROM dbo.Configuration WHERE settingName = 'RookieBuildTimeSec')
        WHEN 'Normal' THEN (SELECT settingValue FROM dbo.Configuration WHERE settingName = 'NormalBuildTimeSec')
        WHEN 'Super'  THEN (SELECT settingValue FROM dbo.Configuration WHERE settingName = 'SuperBuildTimeSec')
        ELSE 60.00
    END;

    SET @variance = (SELECT settingValue FROM dbo.Configuration WHERE settingName = 'BuildTimeVariancePct');

    -- Maps @randomSeed (0.0-1.0) to range: baseTime * (1 - var%) to baseTime * (1 + var%)
    RETURN @baseTime * (1.0 - @variance / 100.0 + (2.0 * @variance / 100.0) * @randomSeed);
END;
GO

------------------------------------------ fn_GetDefectRate ------------------------------------------
-- Returns defect rate as a fraction (0-1) from config
CREATE FUNCTION dbo.fn_GetDefectRate(@skillLevel nvarchar(20))
RETURNS float
AS
BEGIN
    DECLARE @rate float;
    SET @rate = CASE @skillLevel
        WHEN 'Rookie' THEN (SELECT settingValue FROM dbo.Configuration WHERE settingName = 'RookieDefectRate')
        WHEN 'Normal' THEN (SELECT settingValue FROM dbo.Configuration WHERE settingName = 'NormalDefectRate')
        WHEN 'Super'  THEN (SELECT settingValue FROM dbo.Configuration WHERE settingName = 'SuperDefectRate')
        ELSE 0.50
    END;
    RETURN @rate / 100.0;
END;
GO

--========================================================================================================
-- VIEWS
--========================================================================================================

------------------------------------------ vw_WorkstationStatus ------------------------------------------
-- Shows each station with its worker, all bins, and low-stock flag
CREATE VIEW dbo.vw_WorkstationStatus
AS
SELECT
    ws.stationID,
    ws.stationName,
    ws.status AS stationStatus,
    w.workerID,
    w.firstName + ' ' + w.lastName AS workerName,
    w.skillLevel,
    p.partName,
    wb.currentQuantity,
    p.defaultCapacity,
    CASE WHEN wb.currentQuantity <= dbo.fn_GetConfigValue('LowStockThreshold') THEN 1 ELSE 0 END AS isLowStock
FROM dbo.Workstation ws
LEFT JOIN dbo.Worker w ON ws.currentWorkerID = w.workerID
JOIN dbo.WorkstationBin wb ON ws.stationID = wb.stationID
JOIN dbo.Part p ON wb.partID = p.partID;
GO

------------------------------------------ vw_ProductionSummary ------------------------------------------
-- Kanban board: order amount, producing (in-progress), produced (complete), yield.
-- Only rows with isComplete = 1 count as produced; isComplete = 0 rows are "producing".
CREATE VIEW dbo.vw_ProductionSummary
AS
SELECT
    (SELECT settingValue FROM dbo.Configuration WHERE settingName = 'TargetOrderAmount') AS orderAmount,
    SUM(CASE WHEN isComplete = 1 THEN 1 ELSE 0 END) AS totalProduced,
    SUM(CASE WHEN isComplete = 1 AND passedQA = 1 THEN 1 ELSE 0 END) AS totalPassed,
    SUM(CASE WHEN isComplete = 1 AND passedQA = 0 THEN 1 ELSE 0 END) AS totalFailed,
    SUM(CASE WHEN isComplete = 0 THEN 1 ELSE 0 END) AS inProgress,
    CASE
        WHEN SUM(CASE WHEN isComplete = 1 THEN 1 ELSE 0 END) > 0
        THEN CAST(SUM(CASE WHEN isComplete = 1 AND passedQA = 1 THEN 1 ELSE 0 END) * 100.0
                / SUM(CASE WHEN isComplete = 1 THEN 1 ELSE 0 END) AS decimal(5,2))
        ELSE 0
    END AS yieldPercent,
    (SELECT settingValue FROM dbo.Configuration WHERE settingName = 'TargetOrderAmount')
        - SUM(CASE WHEN isComplete = 1 AND passedQA = 1 THEN 1 ELSE 0 END) AS remainingOrder
FROM dbo.ProductionLog;
GO

------------------------------------------ vw_ActiveAlerts ------------------------------------------
-- Unresolved low-stock alerts for the runner display
CREATE VIEW dbo.vw_ActiveAlerts
AS
SELECT
    la.alertID,
    la.stationID,
    ws.stationName,
    la.partID,
    p.partName,
    wb.currentQuantity,
    la.alertTime
FROM dbo.LowStockAlert la
JOIN dbo.Workstation ws ON la.stationID = ws.stationID
JOIN dbo.Part p ON la.partID = p.partID
JOIN dbo.WorkstationBin wb ON la.stationID = wb.stationID AND la.partID = wb.partID
WHERE la.isResolved = 0;
GO

--========================================================================================================
-- TRIGGER
--========================================================================================================

------------------------------------------ trg_LowStockAlert ------------------------------------------
-- Reads threshold from Configuration instead of hardcoding 5
CREATE TRIGGER trg_LowStockAlert
ON dbo.WorkstationBin
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @threshold int;
    SELECT @threshold = CAST(settingValue AS int) 
    FROM dbo.Configuration WHERE settingName = 'LowStockThreshold';

    INSERT INTO dbo.LowStockAlert (stationID, partID, alertTime, isResolved)
    SELECT i.stationID, i.partID, GETDATE(), 0
    FROM inserted i
    WHERE i.currentQuantity <= @threshold
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.LowStockAlert la
          WHERE la.stationID = i.stationID
            AND la.partID = i.partID
            AND la.isResolved = 0
      );
END;
GO

--========================================================================================================
-- STORED PROCEDURES
--========================================================================================================

------------------------------------------ sp_BuildLamp ------------------------------------------
-- Builds 1 lamp at a station. Returns build time, QA result, built flag, and logID as OUTPUT.
-- isComplete = 0 on insert; C# caller calls sp_CompleteLamp after the simulated duration
-- elapses so the Kanban "producing" count stays accurate while assembly is in progress.
CREATE PROCEDURE dbo.sp_BuildLamp
    @stationID int,
    @built     int OUTPUT,
    @buildTime decimal(6,2) OUTPUT,
    @passed    bit OUTPUT,
    @logID     int OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @workerID int;
    DECLARE @skill nvarchar(20);
    DECLARE @randTime float;
    DECLARE @randQA float;
    DECLARE @defectRate float;

    SET @built = 0;
    SET @buildTime = 0;
    SET @passed = 1;
    SET @logID = 0;

    -- Block if any bin is empty
    IF EXISTS
    (
        SELECT 1 FROM dbo.WorkstationBin
        WHERE stationID = @stationID AND currentQuantity <= 0
    )
    BEGIN
        UPDATE dbo.Workstation SET status = 'Blocked' WHERE stationID = @stationID;
        RETURN;
    END

    -- Get assigned worker
    SELECT @workerID = currentWorkerID
    FROM dbo.Workstation WHERE stationID = @stationID;

    IF @workerID IS NULL RETURN;

    SELECT @skill = skillLevel FROM dbo.Worker WHERE workerID = @workerID;

    -- True randomness using NEWID instead of RAND()
    SET @randTime = (ABS(CHECKSUM(NEWID())) % 10000) / 10000.0;
    SET @randQA   = (ABS(CHECKSUM(NEWID())) % 10000) / 10000.0;

    -- Get build time with +/- 10% variance from config
    SET @buildTime = dbo.fn_GetBuildTime(@skill, @randTime);

    -- Get defect rate from config
    SET @defectRate = dbo.fn_GetDefectRate(@skill);
    SET @passed = CASE WHEN @randQA < @defectRate THEN 0 ELSE 1 END;

    -- Decrement all 6 bins by 1
    UPDATE dbo.WorkstationBin
    SET currentQuantity = currentQuantity - 1
    WHERE stationID = @stationID;

    -- Log the production with isComplete = 0 (assembly is in progress, not yet done)
    INSERT INTO dbo.ProductionLog (stationID, workerID, buildTimeSeconds, passedQA, isComplete, [timestamp])
    VALUES (@stationID, @workerID, @buildTime, @passed, 0, GETDATE());

    SET @logID = SCOPE_IDENTITY();

    UPDATE dbo.Workstation SET status = 'Running' WHERE stationID = @stationID;

    SET @built = 1;
END;
GO

------------------------------------------ sp_CompleteLamp ------------------------------------------
-- Marks a production log entry as complete. Called by C# after the simulated assembly
-- duration elapses and isRunning is still true (i.e. the lamp was actually finished).
CREATE PROCEDURE dbo.sp_CompleteLamp
    @logID int
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.ProductionLog SET isComplete = 1 WHERE logID = @logID;
END;
GO

------------------------------------------ sp_CancelLamp ------------------------------------------
-- Removes an in-progress log entry when Stop is pressed mid-build. Only deletes rows
-- with isComplete = 0 to prevent accidental removal of finished records.
CREATE PROCEDURE dbo.sp_CancelLamp
    @logID int
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.ProductionLog WHERE logID = @logID AND isComplete = 0;
END;
GO

------------------------------------------ sp_RefillBin ------------------------------------------
-- Refills a single bin (remaining parts placed ON TOP of new bin per spec)
CREATE PROCEDURE dbo.sp_RefillBin
    @stationID int,
    @partID    int
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE wb
    SET wb.currentQuantity = wb.currentQuantity + p.defaultCapacity
    FROM dbo.WorkstationBin wb
    JOIN dbo.Part p ON wb.partID = p.partID
    WHERE wb.stationID = @stationID AND wb.partID = @partID;

    UPDATE dbo.LowStockAlert
    SET isResolved = 1
    WHERE stationID = @stationID AND partID = @partID AND isResolved = 0;

    IF NOT EXISTS (SELECT 1 FROM dbo.WorkstationBin WHERE stationID = @stationID AND currentQuantity <= 0)
    BEGIN
        UPDATE dbo.Workstation SET status = 'Running' WHERE stationID = @stationID AND status = 'Blocked';
    END
END;
GO

------------------------------------------ sp_RefillAllBins ------------------------------------------
-- Refills ALL low-stock bins at a station (runner presses button)
CREATE PROCEDURE dbo.sp_RefillAllBins
    @stationID int
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @threshold int;
    SELECT @threshold = CAST(settingValue AS int) 
    FROM dbo.Configuration WHERE settingName = 'LowStockThreshold';

    -- Remaining parts placed on top of new bin
    UPDATE wb
    SET wb.currentQuantity = wb.currentQuantity + p.defaultCapacity
    FROM dbo.WorkstationBin wb
    JOIN dbo.Part p ON wb.partID = p.partID
    WHERE wb.stationID = @stationID AND wb.currentQuantity <= @threshold;

    UPDATE dbo.LowStockAlert
    SET isResolved = 1
    WHERE stationID = @stationID AND isResolved = 0;

    UPDATE dbo.Workstation SET status = 'Running' WHERE stationID = @stationID AND status = 'Blocked';
END;
GO

------------------------------------------ sp_RefillStock (legacy alias) ------------------------------------------
-- Kept for backward compatibility, calls sp_RefillAllBins
CREATE PROCEDURE dbo.sp_RefillStock
    @stationID int
AS
BEGIN
    EXEC dbo.sp_RefillAllBins @stationID;
END;
GO

------------------------------------------ sp_ResetSimulation ------------------------------------------
CREATE PROCEDURE dbo.sp_ResetSimulation
AS
BEGIN
    SET NOCOUNT ON;

    DELETE FROM dbo.LowStockAlert;
    DELETE FROM dbo.ProductionLog;

    UPDATE wb
    SET wb.currentQuantity = p.defaultCapacity
    FROM dbo.WorkstationBin wb
    JOIN dbo.Part p ON wb.partID = p.partID;

    UPDATE dbo.Workstation SET status = 'Running' WHERE currentWorkerID IS NOT NULL;
END;
GO

PRINT 'FogLampAssemblyDB created and seeded successfully.';
GO