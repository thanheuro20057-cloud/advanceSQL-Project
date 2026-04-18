# Advanced SQL Project – Fog Lamp Assembly Kanban Simulation

**Authors:** Tuan Thanh Nguyen, Burhan Shibli  
**Date:** March 8, 2026  
**Course:** PROG3071 – Advanced SQL

---

## System Requirements

| Requirement | Version |
|---|---|
| SQL Server | 2019 or 2022 (Express or higher) |
| SQL Server Management Studio (SSMS) | 19 or 20 |
| Visual Studio | 2022 (Community or higher) |
| .NET SDK | 8.0 (Windows) |

---

## Project Overview

This solution simulates a Kanban-based fog lamp assembly line. It consists of five
separate WPF programs that all communicate with a single SQL Server database:

| Program | Purpose |
|---|---|
| **ConfigurationTool** | Edit simulation parameters; reset simulation |
| **WorkstationSimulation** | Simulate a single assembly station building lamps |
| **WorkstationAndon** | Real-time bin-level Andon display for one station |
| **RunnerDisplay** | Shows all low-stock alerts; runner presses Refill |
| **AssemblyLineKanban** | Live Kanban board with order, produced, yield metrics |

Each program is independent. Multiple instances can run on separate machines as long
as they can reach the SQL Server instance.

---

## Step 1 – Database Setup

1. Open **SQL Server Management Studio (SSMS)** and connect to your SQL Server instance.
2. Open the file:
   ```
   Database\FogLampAssemblyDB.sql
   ```
3. Click **Execute** (F5). The script will:
   - Create the `FogLampAssemblyDB` database
   - Create all tables, views, functions, stored procedures, trigger, and indices
   - Insert default configuration values and seed data (workers, stations, parts)
4. Verify success: the Messages pane should show  
   `FogLampAssemblyDB created and seeded successfully.`

---

## Step 2 – Connection String

All five programs use the same connection string constant `kConnectionString` at the
top of each `MainWindow.xaml.cs`:

```csharp
private const string kConnectionString =
    @"Server=localhost;Database=FogLampAssemblyDB;Trusted_Connection=True;";
```

If your SQL Server instance name is not `localhost`, update **all five files** before
building. Common alternatives:

| Instance | Connection string value |
|---|---|
| SQL Server Express | `Server=localhost\SQLEXPRESS` |
| Named instance | `Server=MyPC\SQLSERVER2022` |


---

## Step 3 – Build the Solution

1. Open `advanceSQL-Project\advanceSQL-Project.sln` in **Visual Studio 2022**.
2. In Solution Explorer you will see five projects:
   - ConfigurationTool
   - WorkstationSimulation
   - WorkstationAndon
   - RunnerDisplay
   - AssemblyLineKanban
3. Right-click the Solution → **Restore NuGet Packages** (downloads `System.Data.SqlClient`).
4. Press **Ctrl+Shift+B** to build all projects.

---

## Step 4 – Running the Simulation

Start the programs in this order for the clearest demo:

### A. ConfigurationTool
- Launch to review or change simulation parameters (e.g., `TimeScaleMultiplier`,
  `TargetOrderAmount`).
- Set `TimeScaleMultiplier` to **60** to run at 60× speed (1 real second = 1 sim minute).
- Click **Save Changes to Database**.

### B. AssemblyLineKanban
- Launch once. Shows the Kanban board (order / in-process / produced / yield).
- Refreshes automatically every 2 seconds.

### C. WorkstationAndon (one per station)
- Launch up to 3 instances. Select Station 1, 2, or 3 from the dropdown.
- Shows live bin levels. Rows turn **red** when stock is at or below threshold (default: 5).

### D. RunnerDisplay
- Launch once. Lists every bin currently below the low-stock threshold.
- Click **Refill Selected Station** (select a row first) or **Refill All Stations** to
  call `sp_RefillAllBins` and clear alerts.

### E. WorkstationSimulation (one per station)
- Launch up to 3 instances. Select a different station per instance.
- Click **Start Simulation**. The window shows each lamp built: build time, PASS/FAIL,
  and running totals.
- When a bin empties the station shows **BLOCKED** until the runner refills it.

---

## Simulation Parameters (Configuration Table)

| Setting | Default | Description |
|---|---|---|
| TimeScaleMultiplier | 1.00 | Speed multiplier (60 = 1 real sec per sim minute) |
| TargetOrderAmount | 500 | Total lamps required |
| LowStockThreshold | 5 | Parts remaining before runner alert |
| RunnerIntervalMinutes | 5 | Runner refill cycle interval (informational) |
| HarnessBinCapacity | 55 | Starting bin size |
| ReflectorBinCapacity | 35 | Starting bin size |
| HousingBinCapacity | 24 | Starting bin size |
| LensBinCapacity | 40 | Starting bin size |
| BulbBinCapacity | 60 | Starting bin size |
| BezelBinCapacity | 75 | Starting bin size |
| RookieBuildTimeSec | 90 | Base build time for Rookie |
| NormalBuildTimeSec | 60 | Base build time for Normal |
| SuperBuildTimeSec | 51 | Base build time for Super |
| BuildTimeVariancePct | 10 | ± percent variance on build time |
| RookieDefectRate | 0.85 | Defect rate % for Rookie |
| NormalDefectRate | 0.50 | Defect rate % for Normal |
| SuperDefectRate | 0.15 | Defect rate % for Super |

---

## Resetting the Simulation

In **ConfigurationTool**, click **Reset Simulation**. This executes `sp_ResetSimulation`,
which clears `ProductionLog` and `LowStockAlert` and restores all bin quantities to their
default capacities.

---

## Multi-Machine Setup

Each program only needs network access to the SQL Server. To run on separate machines:

1. Enable **TCP/IP** in SQL Server Configuration Manager on the server machine.
2. Open firewall port ****.
3. Update `kConnectionString` in each program to point to the server's IP or hostname.
4. Use SQL Server Authentication if Windows Auth is not available across machines:
   ```csharp
   @"Server=localhost;Database=FogLampAssemblyDB;User Id=sa;Password=yourPassword;"
   ```

---

## Database Objects Summary

| Object | Type | Purpose |
|---|---|---|
| Configuration | Table | All simulation parameters |
| Worker | Table | Employees with skill levels |
| Part | Table | The 6 fog lamp components |
| Workstation | Table | Assembly stations |
| WorkstationBin | Table | Current part quantities per station |
| ProductionLog | Table | Log of every lamp built |
| LowStockAlert | Table | Alerts raised by trigger |
| fn_GetConfigValue | Function | Read a config value by name |
| fn_GetBuildTime | Function | Randomized build time by skill |
| fn_GetDefectRate | Function | Defect rate fraction by skill |
| vw_WorkstationStatus | View | Station + worker + all bin levels |
| vw_ProductionSummary | View | Kanban totals (order, produced, yield) |
| vw_ActiveAlerts | View | Unresolved low-stock alerts |
| trg_LowStockAlert | Trigger | Auto-raises alert on bin UPDATE |
| sp_BuildLamp | Procedure | Build one lamp; returns time and QA result |
| sp_RefillBin | Procedure | Refill a single bin |
| sp_RefillAllBins | Procedure | Refill all low-stock bins at a station |
| sp_ResetSimulation | Procedure | Clear logs and reset all bins |
