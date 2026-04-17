/*
 * FILE          : MainWindow.xaml.cs
 * PROJECT       : Advanced SQL Project
 * PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli
 * FIRST VERSION : 2026-03-08
 * DESCRIPTION   : WPF workstation simulation. Calls sp_BuildLamp in a loop,
 *                 uses the returned build time and TimeScaleMultiplier for delay.
 *                 A DispatcherTimer fires every 100 ms to keep the stopwatch and
 *                 progress bar in sync with the simulation thread.
 *                 Station is selectable at startup so multiple instances can run.
 */

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace WorkstationSimulation
{
    public partial class MainWindow : Window
    {
        private const string kConnectionString = @"Server=localhost;Database=FogLampAssemblyDB;Trusted_Connection=True;";

        private int     stationID     = 0;
        private int     lastStationID = 0;   // counters reset only when station changes
        private int     lampCount  = 0;
        private int     passCount  = 0;
        private int     failCount  = 0;
        private Thread  simulationThread;
        private volatile bool isRunning      = false;

        // Shared state written by simulation thread, read by UI timer
        private long buildStartTicks  = 0;
        private volatile int  buildDurationMs  = 0;
        private volatile bool buildInProgress  = false;
        private volatile bool isBlocked        = false;

        private DispatcherTimer uiTimer;

        public MainWindow()
        {
            InitializeComponent();
            LoadStations();

            uiTimer          = new DispatcherTimer();
            uiTimer.Interval = TimeSpan.FromMilliseconds(100);
            uiTimer.Tick    += UiTimer_Tick;
        }

        /*
         * FUNCTION    : LoadStations
         * DESCRIPTION : Populates the combo box with available stations and assigned workers.
         * PARAMETERS  : None
         * RETURNS     : void
         */
        private void LoadStations()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(kConnectionString))
                {
                    conn.Open();
                    string sql = @"SELECT ws.stationID,
                                          ws.stationName + ' - '
                                          + ISNULL(w.firstName + ' ' + w.lastName, 'Unassigned')
                                          + ' (' + ISNULL(w.skillLevel, 'N/A') + ')' AS displayName
                                   FROM   Workstation ws
                                   LEFT JOIN Worker w ON ws.currentWorkerID = w.workerID
                                   ORDER  BY ws.stationID";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbStation.Items.Add(new StationItem
                            {
                                StationID   = Convert.ToInt32(reader["stationID"]),
                                DisplayName = reader["displayName"].ToString()
                            });
                        }
                    }
                }

                if (cmbStation.Items.Count > 0)
                    cmbStation.SelectedIndex = 0;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "DB Error: " + ex.Message;
            }
        }

        /*
         * FUNCTION    : btnStart_Click
         * DESCRIPTION : Starts the simulation thread and UI timer for the selected station.
         * PARAMETERS  : object sender     : The button that raised the event
         *               RoutedEventArgs e : Event data
         * RETURNS     : void
         */
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning) return;

            StationItem selected = cmbStation.SelectedItem as StationItem;
            if (selected == null)
            {
                MessageBox.Show("Please select a station.", "No Station",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            stationID = selected.StationID;

            // Reset counters only when the user switches to a different station.
            // Stop → Start on the same station resumes totals from where they left off.
            if (stationID != lastStationID)
            {
                lampCount     = 0;
                passCount     = 0;
                failCount     = 0;
                lastStationID = stationID;
                txtLastResult.Text = "—";
            }

            buildInProgress = false;
            isBlocked       = false;
            isRunning       = true;

            cmbStation.IsEnabled = false;
            btnStart.IsEnabled   = false;
            btnStop.IsEnabled    = true;
            txtStation.Text      = selected.DisplayName;
            txtStatus.Text       = "Starting simulation...";

            simulationThread = new Thread(RunSimulation);
            simulationThread.IsBackground = true;
            simulationThread.Start();

            uiTimer.Start();
        }

        /*
         * FUNCTION    : btnStop_Click
         * DESCRIPTION : Stops the simulation thread and UI timer, resets the stopwatch.
         * PARAMETERS  : object sender     : The button that raised the event
         *               RoutedEventArgs e : Event data
         * RETURNS     : void
         */
        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            isRunning       = false;
            buildInProgress = false;
            isBlocked       = false;

            uiTimer.Stop();

            btnStop.IsEnabled    = false;
            btnStart.IsEnabled   = true;
            cmbStation.IsEnabled = true;
            txtStatus.Text       = "Simulation stopped.";
            txtStopwatch.Text    = "--:--.--";
            progressBuild.Value  = 0;
        }

        /*
         * FUNCTION    : UiTimer_Tick
         * DESCRIPTION : Fires every 100 ms on the UI thread. Reads shared volatile state
         *               set by the simulation thread to update the stopwatch label and
         *               progress bar without any cross-thread marshalling overhead.
         * PARAMETERS  : object sender : The DispatcherTimer
         *               EventArgs e   : Event data
         * RETURNS     : void
         */
        private void UiTimer_Tick(object sender, EventArgs e)
        {
            if (isBlocked)
            {
                txtStopwatch.Text         = "BLOCKED";
                txtTargetTime.Text        = "";
                progressBuild.Value       = 0;
                progressBuild.Foreground  = Brushes.OrangeRed;
                return;
            }

            if (!buildInProgress || buildDurationMs <= 0)
            {
                txtStopwatch.Text   = "--:--.--";
                txtTargetTime.Text  = "";
                progressBuild.Value = 0;
                return;
            }

            double elapsedMs = TimeSpan.FromTicks(DateTime.Now.Ticks - Interlocked.Read(ref buildStartTicks)).TotalMilliseconds;
            elapsedMs = Math.Min(elapsedMs, buildDurationMs);

            TimeSpan ts = TimeSpan.FromMilliseconds(elapsedMs);
            txtStopwatch.Text  = string.Format("{0}:{1:00}.{2:00}",
                (int)ts.TotalMinutes, ts.Seconds, ts.Milliseconds / 10);
            txtTargetTime.Text = string.Format("/ {0:F1}s", buildDurationMs / 1000.0);

            double pct = elapsedMs / buildDurationMs * 100.0;
            progressBuild.Value      = pct;
            progressBuild.Foreground = pct < 85.0
                ? new SolidColorBrush(Color.FromRgb(40, 167, 69))   // green
                : new SolidColorBrush(Color.FromRgb(255, 193, 7));  // amber near end
        }

        /*
         * FUNCTION    : RunSimulation
         * DESCRIPTION : Main simulation loop running on a background thread. Calls sp_BuildLamp,
         *               then sets volatile state for the UI timer before sleeping for the scaled
         *               build duration. Signals buildInProgress = false when the cycle ends so
         *               the timer resets for the next lamp.
         * PARAMETERS  : None
         * RETURNS     : void
         */
        private void RunSimulation()
        {
            while (isRunning)
            {
                try
                {
                    int     built     = 0;
                    decimal buildTime = 0;
                    bool    passed    = true;
                    int     logID     = 0;
                    decimal timeScale = 1;

                    using (SqlConnection conn = new SqlConnection(kConnectionString))
                    {
                        conn.Open();

                        SqlCommand cmd = new SqlCommand("sp_BuildLamp", conn);
                        cmd.CommandType = CommandType.StoredProcedure;
                        cmd.Parameters.AddWithValue("@stationID", stationID);

                        SqlParameter builtParam = new SqlParameter("@built", SqlDbType.Int)
                            { Direction = ParameterDirection.Output };
                        cmd.Parameters.Add(builtParam);

                        SqlParameter buildTimeParam = new SqlParameter("@buildTime", SqlDbType.Decimal)
                            { Direction = ParameterDirection.Output, Precision = 6, Scale = 2 };
                        cmd.Parameters.Add(buildTimeParam);

                        SqlParameter passedParam = new SqlParameter("@passed", SqlDbType.Bit)
                            { Direction = ParameterDirection.Output };
                        cmd.Parameters.Add(passedParam);

                        SqlParameter logIDParam = new SqlParameter("@logID", SqlDbType.Int)
                            { Direction = ParameterDirection.Output };
                        cmd.Parameters.Add(logIDParam);

                        cmd.ExecuteNonQuery();

                        built     = Convert.ToInt32(builtParam.Value);
                        buildTime = Convert.ToDecimal(buildTimeParam.Value);
                        passed    = Convert.ToBoolean(passedParam.Value);
                        logID     = Convert.ToInt32(logIDParam.Value);

                        // Read TimeScaleMultiplier on the same open connection — no extra round-trip.
                        SqlCommand cfgCmd = new SqlCommand(
                            "SELECT settingValue FROM Configuration WHERE settingName = 'TimeScaleMultiplier'", conn);
                        object cfgVal = cfgCmd.ExecuteScalar();
                        timeScale = cfgVal != null ? Convert.ToDecimal(cfgVal) : 1;
                    }

                    if (built == 1)
                    {
                        isBlocked = false;

                        if (timeScale <= 0) timeScale = 1;
                        int sleepMs = (int)((double)buildTime / (double)timeScale * 1000.0);
                        if (sleepMs < 50) sleepMs = 50;

                        buildDurationMs = sleepMs;
                        Interlocked.Exchange(ref buildStartTicks, DateTime.Now.Ticks);
                        buildInProgress = true;

                        Dispatcher.BeginInvoke(new Action(() =>
                        {
                            txtStatus.Text = "Assembling...";
                        }));

                        // Interruptible sleep: check real elapsed time, not a counter.
                        // Thread.Sleep(50) on Windows rounds up to ~62 ms per tick; using a
                        // counter (slept += 50) accumulates that error across every iteration
                        // and causes visible stalls at the end of each build cycle.
                        long sleepStart = DateTime.Now.Ticks;
                        while (isRunning)
                        {
                            double remaining = sleepMs
                                - TimeSpan.FromTicks(DateTime.Now.Ticks - sleepStart).TotalMilliseconds;
                            if (remaining <= 0) break;
                            Thread.Sleep((int)Math.Min(50, remaining));
                        }

                        buildInProgress = false;

                        // Only count the lamp if the simulation is still running.
                        // If Stop was pressed mid-build, cancel the in-progress log row.
                        if (isRunning)
                        {
                            lampCount++;
                            if (passed) passCount++; else failCount++;

                            // Capture loop-locals so the closures below are safe.
                            int     capturedLogID  = logID;
                            int     capturedNum    = lampCount;
                            decimal capturedBT     = buildTime;
                            bool    capturedPassed = passed;
                            int     capturedPass   = passCount;
                            int     capturedFail   = failCount;

                            // Complete DB record off the sim thread — no blocking round-trip.
                            System.Threading.ThreadPool.QueueUserWorkItem(
                                _ => CallProc("sp_CompleteLamp", capturedLogID));

                            // Post UI update without waiting — sim thread loops back immediately.
                            Dispatcher.BeginInvoke(new Action(() =>
                            {
                                txtLastResult.Text = string.Format(
                                    "Lamp #{0} | {1:F1}s | {2} | Pass: {3}  Fail: {4}",
                                    capturedNum, capturedBT,
                                    capturedPassed ? "PASS" : "FAIL",
                                    capturedPass, capturedFail);
                                txtLastResult.Foreground = capturedPassed
                                    ? Brushes.DarkGreen : Brushes.DarkRed;
                            }));
                        }
                        else if (logID > 0)
                        {
                            CallProc("sp_CancelLamp", logID);
                        }
                    }
                    else
                    {
                        buildInProgress = false;
                        isBlocked       = true;

                        Dispatcher.Invoke(() =>
                        {
                            txtStatus.Text = "BLOCKED — waiting for runner to refill bins...";
                        });

                        if (timeScale <= 0) timeScale = 1;
                        int waitMs = (int)(2000.0 / (double)timeScale);
                        if (waitMs < 100) waitMs = 100;

                        long waitStart = DateTime.Now.Ticks;
                        while (isRunning)
                        {
                            double remaining = waitMs
                                - TimeSpan.FromTicks(DateTime.Now.Ticks - waitStart).TotalMilliseconds;
                            if (remaining <= 0) break;
                            Thread.Sleep((int)Math.Min(50, remaining));
                        }
                    }
                }
                catch (Exception ex)
                {
                    buildInProgress = false;
                    Dispatcher.Invoke(() =>
                    {
                        txtStatus.Text = "Error: " + ex.Message;
                    });
                    for (int i = 0; i < 60 && isRunning; i++)
                        Thread.Sleep(50);
                }
            }
        }

        /*
         * FUNCTION    : CallProc
         * DESCRIPTION : Executes a stored procedure that takes a single @logID int parameter.
         *               Used for sp_CompleteLamp and sp_CancelLamp.
         * PARAMETERS  : string procName : Name of the stored procedure
         *               int    logID    : The production log row to act on
         * RETURNS     : void
         */
        private void CallProc(string procName, int logID)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(kConnectionString))
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand(procName, conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@logID", logID);
                    cmd.ExecuteNonQuery();
                }
            }
            catch { /* best-effort; don't crash the simulation thread */ }
        }

        /*
         * FUNCTION    : GetConfigValue
         * DESCRIPTION : Reads a single configuration value from the database by setting name.
         * PARAMETERS  : string settingName : The name of the setting to retrieve
         * RETURNS     : decimal : The setting value, or 1 if not found or on error
         */
        private decimal GetConfigValue(string settingName)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(kConnectionString))
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand(
                        "SELECT settingValue FROM Configuration WHERE settingName = @name", conn);
                    cmd.Parameters.AddWithValue("@name", settingName);
                    object result = cmd.ExecuteScalar();
                    return result != null ? Convert.ToDecimal(result) : 1;
                }
            }
            catch
            {
                return 1;
            }
        }
    }

    /*
     * CLASS       : StationItem
     * DESCRIPTION : Helper class for the station combo box binding.
     */
    public class StationItem
    {
        public int    StationID   { get; set; }
        public string DisplayName { get; set; }
        public override string ToString() => DisplayName;
    }
}
