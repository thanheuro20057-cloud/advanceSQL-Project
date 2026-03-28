/*
 * FILE          : Create_Configuration.sql
 * PROJECT       : Advanced SQL Project 
 * PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli
 * FIRST VERSION : 2026-03-05
 * DESCRIPTION   : This script creates the database and the initial Configuration table 
 * required for the Kanban manufacturing simulation. It also seeds the 
 * table with the default capacity and time scale values.
 */

------------------------------------------ Create the database if it doesn't exist ------------------------------------------
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'FogLampAssemblyDB')
BEGIN
    CREATE DATABASE FogLampAssemblyDB;
END
GO

USE FogLampAssemblyDB;
GO

------------------------------------------ Drop table if it exists to allow clean reruns ------------------------------------------
IF OBJECT_ID('dbo.Configuration', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Configuration;
END
GO

------------------------------------------ Create the Configuration table per Milestone 1 requirements ------------------------------------------
CREATE TABLE Configuration 
(
    configID int NOT NULL IDENTITY(1,1),
    settingName nvarchar(50) NOT NULL,
    settingValue decimal(10,2) NOT NULL,
    description nvarchar(200) NULL,
    CONSTRAINT PK_Configuration PRIMARY KEY CLUSTERED (configID ASC)
);
GO

------------------------------------------ Insert Initial Data Requirements ------------------------------------------
INSERT INTO Configuration 
    (settingName, settingValue, description)
VALUES 
    ('TimeScaleMultiplier', 1.00, 'Multiplier for simulation time (1 real min = x sim mins)'),
    ('HarnessBinCapacity', 55.00, 'Starting capacity for Harness bins'),
    ('ReflectorBinCapacity', 35.00, 'Starting capacity for Reflector bins'),
    ('HousingBinCapacity', 24.00, 'Starting capacity for Housing bins'),
    ('LensBinCapacity', 40.00, 'Starting capacity for Lens bins'),
    ('BulbBinCapacity', 60.00, 'Starting capacity for Bulb bins'),
    ('BezelBinCapacity', 75.00, 'Starting capacity for Bezel bins'),
    ('TargetOrderAmount', 500.00, 'Total lamps required for the Kanban order');
GO


------------------------------------------ Create Worker table ------------------------------------------
-- stores employees and their skill level (rookie, normal, super)

IF OBJECT_ID('dbo.Worker', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Worker;
END
GO

CREATE TABLE dbo.Worker
(
    workerID int IDENTITY(1,1) PRIMARY KEY,
    firstName nvarchar(50) NOT NULL,
    lastName nvarchar(50) NOT NULL,
    skillLevel nvarchar(20) NOT NULL
);
GO

------------------------------------------ Create Workstation table ------------------------------------------
-- represents each assembly station 

IF OBJECT_ID('dbo.Workstation', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Workstation;
END
GO

CREATE TABLE dbo.Workstation
(
    stationID int IDENTITY(1,1) PRIMARY KEY,
    stationName nvarchar(50) NOT NULL,
    currentWorkerID int NULL,
    status nvarchar(20) NOT NULL
);
GO

------------------------------------------ Create Part table ------------------------------------------
-- stores the 6 fog lamp parts and their default bin capacity

IF OBJECT_ID('dbo.Part', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Part;
END
GO

CREATE TABLE dbo.Part
(
    partID int IDENTITY(1,1) PRIMARY KEY,
    partName nvarchar(50) NOT NULL,
    defaultCapacity int NOT NULL
);
GO

------------------------------------------ Create WorkstationBin table ------------------------------------------
-- tracks how many parts each station has

IF OBJECT_ID('dbo.WorkstationBin', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.WorkstationBin;
END
GO

CREATE TABLE dbo.WorkstationBin
(
    binID int IDENTITY(1,1) PRIMARY KEY,
    stationID int NOT NULL,
    partID int NOT NULL,
    currentQuantity int NOT NULL
);
GO

------------------------------------------ Create ProductionLog table ------------------------------------------
-- stores each lamp that gets built

IF OBJECT_ID('dbo.ProductionLog', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.ProductionLog;
END
GO

CREATE TABLE dbo.ProductionLog
(
    logID int IDENTITY(1,1) PRIMARY KEY,
    stationID int NOT NULL,
    workerID int NOT NULL,
    buildTimeSeconds int NOT NULL,
    passedQA bit NOT NULL,
    [timestamp] datetime NOT NULL
);
GO

------------------------------------------ Create LowStockAlert table ------------------------------------------
-- keeps track of when parts are running low at a station

IF OBJECT_ID('dbo.LowStockAlert', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.LowStockAlert;
END
GO

CREATE TABLE dbo.LowStockAlert
(
    alertID int IDENTITY(1,1) PRIMARY KEY,
    stationID int NOT NULL,
    partID int NOT NULL,
    alertTime datetime NOT NULL,
    isResolved bit NOT NULL
);
GO

------------------------------------------ Add foreign keys ------------------------------------------

ALTER TABLE dbo.Workstation
ADD CONSTRAINT FK_Workstation_Worker
FOREIGN KEY (currentWorkerID) REFERENCES dbo.Worker(workerID);
GO

ALTER TABLE dbo.WorkstationBin
ADD CONSTRAINT FK_WorkstationBin_Workstation
FOREIGN KEY (stationID) REFERENCES dbo.Workstation(stationID);
GO

ALTER TABLE dbo.WorkstationBin
ADD CONSTRAINT FK_WorkstationBin_Part
FOREIGN KEY (partID) REFERENCES dbo.Part(partID);
GO

ALTER TABLE dbo.ProductionLog
ADD CONSTRAINT FK_ProductionLog_Workstation
FOREIGN KEY (stationID) REFERENCES dbo.Workstation(stationID);
GO

ALTER TABLE dbo.ProductionLog
ADD CONSTRAINT FK_ProductionLog_Worker
FOREIGN KEY (workerID) REFERENCES dbo.Worker(workerID);
GO

ALTER TABLE dbo.LowStockAlert
ADD CONSTRAINT FK_LowStockAlert_Workstation
FOREIGN KEY (stationID) REFERENCES dbo.Workstation(stationID);
GO

ALTER TABLE dbo.LowStockAlert
ADD CONSTRAINT FK_LowStockAlert_Part
FOREIGN KEY (partID) REFERENCES dbo.Part(partID);
GO

------------------------------------------ insert the 6 parts with their starting capacities ------------------------------------------

INSERT INTO dbo.Part (partName, defaultCapacity)
VALUES
('Harness', 55),
('Reflector', 35),
('Housing', 24),
('Lens', 40),
('Bulb', 60),
('Bezel', 75);
GO

------------------------------------------ add 3 assembly stations ------------------------------------------

INSERT INTO dbo.Workstation (stationName, currentWorkerID, status)
VALUES
('Station 1', NULL, 'Running'),
('Station 2', NULL, 'Running'),
('Station 3', NULL, 'Running');
GO

------------------------------------------ add workers ------------------------------------------

INSERT INTO dbo.Worker (firstName, lastName, skillLevel)
VALUES
('Ali', 'Hassan', 'Rookie'),
('Omar', 'Khan', 'Normal'),
('Yusuf', 'Rahman', 'Super'),
('John', 'Smith', 'Normal'),
('Sara', 'Ahmed', 'Rookie'),
('Layla', 'Karim', 'Super');
GO

------------------------------------------ fill each station with all 6 part bins ------------------------------------------

INSERT INTO dbo.WorkstationBin (stationID, partID, currentQuantity)
SELECT w.stationID, p.partID, p.defaultCapacity
FROM dbo.Workstation w
CROSS JOIN dbo.Part p;
GO

------------------------------------------ assign workers to stations ------------------------------------------

UPDATE dbo.Workstation
SET currentWorkerID = 1
WHERE stationID = 1;

UPDATE dbo.Workstation
SET currentWorkerID = 2
WHERE stationID = 2;

UPDATE dbo.Workstation
SET currentWorkerID = 3
WHERE stationID = 3;
GO

------------------------------------------ builds 1 lamp at a station ------------------------------------------

IF OBJECT_ID('sp_BuildLamp', 'P') IS NOT NULL
    DROP PROCEDURE sp_BuildLamp;
GO

--run
CREATE PROCEDURE sp_BuildLamp
    @stationID int,
    @built int OUTPUT
AS
BEGIN
    DECLARE @workerID int;
    DECLARE @skill nvarchar(20);
    DECLARE @passed bit;
    DECLARE @rand float;
    DECLARE @buildTime int;

    SET @built = 0;

    IF EXISTS
    (
        SELECT *
        FROM WorkstationBin
        WHERE stationID = @stationID
          AND currentQuantity <= 0
    )
    BEGIN
        RETURN;
    END

    SELECT @workerID = currentWorkerID
    FROM Workstation
    WHERE stationID = @stationID;

    SELECT @skill = skillLevel
    FROM Worker
    WHERE workerID = @workerID;

    IF @skill = 'Rookie'
        SET @buildTime = 90;
    ELSE IF @skill = 'Normal'
        SET @buildTime = 60;
    ELSE
        SET @buildTime = 51;

    UPDATE WorkstationBin
    SET currentQuantity = currentQuantity - 1
    WHERE stationID = @stationID;

    SET @rand = RAND();

    IF @skill = 'Rookie'
        SET @passed = CASE WHEN @rand < 0.0085 THEN 0 ELSE 1 END;
    ELSE IF @skill = 'Normal'
        SET @passed = CASE WHEN @rand < 0.005 THEN 0 ELSE 1 END;
    ELSE
        SET @passed = CASE WHEN @rand < 0.0015 THEN 0 ELSE 1 END;

    INSERT INTO ProductionLog (stationID, workerID, buildTimeSeconds, passedQA, [timestamp])
    VALUES (@stationID, @workerID, @buildTime, @passed, GETDATE());

    SET @built = 1;
END;
GO
------------------------------------------ trigger to create low stock alerts when a bin gets low ------------------------------------------

CREATE TRIGGER trg_LowStockAlert
ON dbo.WorkstationBin
AFTER UPDATE
AS
BEGIN
    INSERT INTO dbo.LowStockAlert (stationID, partID, alertTime, isResolved)
    SELECT i.stationID, i.partID, GETDATE(), 0
    FROM inserted i
    WHERE i.currentQuantity <= 5
      AND NOT EXISTS
      (
          SELECT 1
          FROM dbo.LowStockAlert l
          WHERE l.stationID = i.stationID
            AND l.partID = i.partID
            AND l.isResolved = 0
      );
END;
GO

------------------------------------------ Refill Stock ------------------------------------------
IF OBJECT_ID('sp_RefillStock', 'P') IS NOT NULL
    DROP PROCEDURE sp_RefillStock;
GO

CREATE PROCEDURE sp_RefillStock
    @stationID int
AS
BEGIN
    UPDATE wb
    SET wb.currentQuantity = p.defaultCapacity
    FROM WorkstationBin wb
    JOIN Part p ON wb.partID = p.partID
    WHERE wb.stationID = @stationID;

    UPDATE LowStockAlert
    SET isResolved = 1
    WHERE stationID = @stationID
      AND isResolved = 0;
END;
GO