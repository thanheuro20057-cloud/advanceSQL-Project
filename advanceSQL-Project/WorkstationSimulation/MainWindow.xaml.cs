/*
 * FILE          : MainWindow.xaml.cs
 * PROJECT       : Advanced SQL Project 
 * PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli
 * FIRST VERSION : 2026-03-08
 * DESCRIPTION   : WPF workstation simulation. Calls sp_BuildLamp in a loop,
 *                 uses the returned build time and TimeScaleMultiplier for delay.
 *                 Station is selectable at startup so multiple instances can run.
 */

using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using System.Windows;

namespace WorkstationSimulation
{
    public partial class MainWindow : Window
    {
        private const string kConnectionString = @"Server=localhost;Database=FogLampAssemblyDB;Trusted_Connection=True;";
        private int stationID = 0;
        private int lampCount = 0;
        private int passCount = 0;
        private int failCount = 0;
        private Thread simulationThread;
        private volatile bool isRunning = false;

        public MainWindow()
        {
            InitializeComponent();
            LoadStations();
        }

        /// <summary>
        /// Loads available stations into the combo box for selection
        /// </summary>
        private void LoadStations()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(kConnectionString))
                {
                    conn.Open();
                    string sql = @"SELECT ws.stationID, 
                                          ws.stationName + ' - ' + ISNULL(w.firstName + ' ' + w.lastName, 'Unassigned') 
                                          + ' (' + ISNULL(w.skillLevel, 'N/A') + ')' AS displayName
                                   FROM Workstation ws
                                   LEFT JOIN Worker w ON ws.currentWorkerID = w.workerID
                                   ORDER BY ws.stationID";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbStation.Items.Add(new StationItem
                            {
                                StationID = Convert.ToInt32(reader["stationID"]),
                                DisplayName = reader["displayName"].ToString()
                            });
                        }
                    }
                }

                if (cmbStation.Items.Count > 0)
                {
                    cmbStation.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "DB Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Starts the simulation for the selected station
        /// </summary>
        private void btnStart_Click(object sender, RoutedEventArgs e)
        {
            if (isRunning) return;

            StationItem selected = cmbStation.SelectedItem as StationItem;
            if (selected == null)
            {
                MessageBox.Show("Please select a station.", "No Station", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            stationID = selected.StationID;
            lampCount = 0;
            passCount = 0;
            failCount = 0;
            isRunning = true;

            cmbStation.IsEnabled = false;
            btnStart.IsEnabled = false;
            btnStop.IsEnabled = true;
            txtStation.Text = selected.DisplayName;
            txtStatus.Text = "Starting simulation...";

            simulationThread = new Thread(RunSimulation);
            simulationThread.IsBackground = true;
            simulationThread.Start();
        }

        /// <summary>
        /// Stops the simulation
        /// </summary>
        private void btnStop_Click(object sender, RoutedEventArgs e)
        {
            isRunning = false;
            btnStop.IsEnabled = false;
            btnStart.IsEnabled = true;
            cmbStation.IsEnabled = true;
            txtStatus.Text = "Simulation stopped.";
        }

        /// <summary>
        /// Main simulation loop — calls sp_BuildLamp and sleeps for scaled build time
        /// </summary>
        private void RunSimulation()
        {
            while (isRunning)
            {
                try
                {
                    int built = 0;
                    decimal buildTime = 0;
                    bool passed = true;

                    using (SqlConnection conn = new SqlConnection(kConnectionString))
                    {
                        conn.Open();

                        SqlCommand cmd = new SqlCommand("sp_BuildLamp", conn);
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@stationID", stationID);

                        SqlParameter builtParam = new SqlParameter("@built", SqlDbType.Int);
                        builtParam.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(builtParam);

                        SqlParameter buildTimeParam = new SqlParameter("@buildTime", SqlDbType.Decimal);
                        buildTimeParam.Direction = ParameterDirection.Output;
                        buildTimeParam.Precision = 6;
                        buildTimeParam.Scale = 2;
                        cmd.Parameters.Add(buildTimeParam);

                        SqlParameter passedParam = new SqlParameter("@passed", SqlDbType.Bit);
                        passedParam.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(passedParam);

                        cmd.ExecuteNonQuery();

                        built = Convert.ToInt32(builtParam.Value);
                        buildTime = Convert.ToDecimal(buildTimeParam.Value);
                        passed = Convert.ToBoolean(passedParam.Value);
                    }

                    if (built == 1)
                    {
                        lampCount++;
                        if (passed) passCount++; else failCount++;

                        Dispatcher.Invoke(() =>
                        {
                            txtStatus.Text = string.Format("Running... Lamp #{0} | {1:F1}s | {2} | Pass:{3} Fail:{4}",
                                lampCount, buildTime, passed ? "PASS" : "FAIL", passCount, failCount);
                        });

                        // Sleep for the build time, scaled by TimeScaleMultiplier
                        decimal timeScale = GetConfigValue("TimeScaleMultiplier");
                        if (timeScale <= 0) timeScale = 1;
                        int sleepMs = (int)((double)buildTime / (double)timeScale * 1000.0);
                        if (sleepMs < 50) sleepMs = 50;
                        Thread.Sleep(sleepMs);
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            txtStatus.Text = "BLOCKED - waiting for runner to refill bins...";
                        });

                        // Wait 2 seconds (scaled) then retry
                        decimal timeScale = GetConfigValue("TimeScaleMultiplier");
                        if (timeScale <= 0) timeScale = 1;
                        int waitMs = (int)(2000.0 / (double)timeScale);
                        if (waitMs < 100) waitMs = 100;
                        Thread.Sleep(waitMs);
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtStatus.Text = "Error: " + ex.Message;
                    });
                    Thread.Sleep(3000);
                }
            }
        }

        /// <summary>
        /// Reads a configuration value from the database
        /// </summary>
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
                    return result != null ? Convert.ToDecimal(result) : 0;
                }
            }
            catch
            {
                return 1;
            }
        }
    }

    /// <summary>
    /// Helper class for the station combo box items
    /// </summary>
    public class StationItem
    {
        public int StationID { get; set; }
        public string DisplayName { get; set; }

        public override string ToString()
        {
            return DisplayName;
        }
    }
}