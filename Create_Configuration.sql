/*
 * FILE          : Create_Configuration.sql
 * PROJECT       : Advanced SQL Project 
 * PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli
 * FIRST VERSION : 2026-03-05
 * DESCRIPTION   : This script creates the database and the initial Configuration table 
 * required for the Kanban manufacturing simulation. It also seeds the 
 * table with the default capacity and time scale values.
 */

-- Create the database if it doesn't exist
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'FogLampAssemblyDB')
BEGIN
    CREATE DATABASE FogLampAssemblyDB;
END
GO

USE FogLampAssemblyDB;
GO

-- Drop table if it exists to allow clean reruns
IF OBJECT_ID('dbo.Configuration', 'U') IS NOT NULL
BEGIN
    DROP TABLE dbo.Configuration;
END
GO

-- Create the Configuration table per Milestone 1 requirements
CREATE TABLE Configuration 
(
    configID int NOT NULL IDENTITY(1,1),
    settingName nvarchar(50) NOT NULL,
    settingValue decimal(10,2) NOT NULL,
    description nvarchar(200) NULL,
    CONSTRAINT PK_Configuration PRIMARY KEY CLUSTERED (configID ASC)
);
GO

-- Insert Initial Data Requirements
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