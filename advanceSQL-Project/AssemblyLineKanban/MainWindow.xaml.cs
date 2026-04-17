/*
 * FILE          : MainWindow.xaml.cs
 * PROJECT       : Advanced SQL Project
 * PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli
 * FIRST VERSION : 2026-03-08
 * DESCRIPTION   : WPF Assembly Line Kanban board. Polls vw_ProductionSummary for global
 *                 metrics (order, producing, produced, yield) and shows a per-station
 *                 breakdown every 2 seconds. Blocked stations are highlighted in the grid.
 *                 "Producing" counts lamps currently mid-assembly (isComplete = 0);
 *                 "Produced" counts only completed lamps (isComplete = 1).
 */

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Threading;

namespace AssemblyLineKanban
{
    public partial class MainWindow : Window
    {
        private const string kConnectionString = @"Server=localhost;Database=FogLampAssemblyDB;Trusted_Connection=True;";

        private DispatcherTimer refreshTimer;

        public MainWindow()
        {
            InitializeComponent();
            RefreshData();

            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(2);
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
        }

        /*
         * FUNCTION    : RefreshTimer_Tick
         * DESCRIPTION : Timer callback that triggers a data refresh every 2 seconds.
         * PARAMETERS  : object sender : The DispatcherTimer
         *               EventArgs e   : Event data
         * RETURNS     : void
         */
        private void RefreshTimer_Tick(object sender, EventArgs e)
        {
            RefreshData();
        }

        /*
         * FUNCTION    : RefreshData
         * DESCRIPTION : Queries vw_ProductionSummary for overall Kanban metrics and a direct
         *               query for per-station stats. Updates all metric tiles and the station grid.
         *               In-process count comes from the number of currently Running workstations.
         * PARAMETERS  : None
         * RETURNS     : void
         */
        private void RefreshData()
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(kConnectionString))
                {
                    conn.Open();

                    // -- Overall Kanban summary --
                    string summarySQL = @"SELECT orderAmount, totalPassed, inProgress, yieldPercent, remainingOrder
                                          FROM   vw_ProductionSummary";

                    SqlCommand summaryCmd = new SqlCommand(summarySQL, conn);
                    using (SqlDataReader reader = summaryCmd.ExecuteReader())
                    {
                        if (reader.Read())
                        {
                            txtOrder.Text     = Convert.ToDecimal(reader["orderAmount"]).ToString("0");
                            txtProduced.Text  = reader["totalPassed"].ToString();
                            txtInProcess.Text = reader["inProgress"].ToString();
                            txtYield.Text     = Convert.ToDecimal(reader["yieldPercent"]).ToString("0.0") + "%";

                            decimal remaining = Convert.ToDecimal(reader["remainingOrder"]);
                            txtRemaining.Text = string.Format("Remaining to order: {0}",
                                remaining < 0 ? 0 : remaining);
                        }
                    }

                    // -- Per-station breakdown --
                    string stationSQL = @"
                        SELECT ws.stationName,
                               ws.status,
                               ISNULL(w.firstName + ' ' + w.lastName, 'Unassigned') AS workerName,
                               ISNULL(w.skillLevel, 'N/A') AS skillLevel,
                               SUM(CASE WHEN pl.isComplete = 0 THEN 1 ELSE 0 END) AS producing,
                               SUM(CASE WHEN pl.isComplete = 1 THEN 1 ELSE 0 END) AS produced,
                               SUM(CASE WHEN pl.isComplete = 1 AND pl.passedQA = 1 THEN 1 ELSE 0 END) AS passed,
                               SUM(CASE WHEN pl.isComplete = 1 AND pl.passedQA = 0 THEN 1 ELSE 0 END) AS failed
                        FROM       Workstation ws
                        LEFT JOIN  Worker w ON ws.currentWorkerID = w.workerID
                        LEFT JOIN  ProductionLog pl ON ws.stationID = pl.stationID
                        GROUP BY   ws.stationID, ws.stationName, ws.status,
                                   w.firstName, w.lastName, w.skillLevel
                        ORDER BY   ws.stationID";

                    SqlCommand stationCmd = new SqlCommand(stationSQL, conn);
                    List<StationRow> rows = new List<StationRow>();

                    using (SqlDataReader reader = stationCmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rows.Add(new StationRow
                            {
                                StationName   = reader["stationName"].ToString(),
                                WorkerName    = reader["workerName"].ToString(),
                                SkillLevel    = reader["skillLevel"].ToString(),
                                Producing     = reader["producing"] == DBNull.Value ? 0 : Convert.ToInt32(reader["producing"]),
                                Produced      = reader["produced"]  == DBNull.Value ? 0 : Convert.ToInt32(reader["produced"]),
                                Passed        = reader["passed"]    == DBNull.Value ? 0 : Convert.ToInt32(reader["passed"]),
                                Failed        = reader["failed"]    == DBNull.Value ? 0 : Convert.ToInt32(reader["failed"]),
                                StationStatus = reader["status"].ToString()
                            });
                        }
                    }

                    gridStations.ItemsSource = rows;
                }

                txtRefresh.Text = "Last updated: " + DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                txtRemaining.Text = "Error: " + ex.Message;
            }
        }
    }

    public class StationRow
    {
        public string StationName   { get; set; }
        public string WorkerName    { get; set; }
        public string SkillLevel    { get; set; }
        public int    Producing     { get; set; }
        public int    Produced      { get; set; }
        public int    Passed        { get; set; }
        public int    Failed        { get; set; }
        public string StationStatus { get; set; }
    }
}
