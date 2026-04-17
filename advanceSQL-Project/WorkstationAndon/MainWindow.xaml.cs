/*
 * FILE          : MainWindow.xaml.cs
 * PROJECT       : Advanced SQL Project
 * PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli
 * FIRST VERSION : 2026-03-08
 * DESCRIPTION   : WPF Andon display for a single workstation. Polls vw_WorkstationStatus
 *                 every 2 seconds and highlights parts in red when stock falls to low threshold.
 *                 Multiple instances can run simultaneously, one per station.
 */

using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace WorkstationAndon
{
    public partial class MainWindow : Window
    {
        private const string kConnectionString = @"Server=localhost;Database=FogLampAssemblyDB;Trusted_Connection=True;";

        private DispatcherTimer refreshTimer;
        private int selectedStationID = 0;

        public MainWindow()
        {
            InitializeComponent();
            LoadStations();

            refreshTimer = new DispatcherTimer();
            refreshTimer.Interval = TimeSpan.FromSeconds(2);
            refreshTimer.Tick += RefreshTimer_Tick;
            refreshTimer.Start();
        }

        /*
         * FUNCTION    : LoadStations
         * DESCRIPTION : Populates the station combo box from the Workstation table.
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
                    string sql = "SELECT stationID, stationName FROM Workstation ORDER BY stationID";
                    SqlCommand cmd = new SqlCommand(sql, conn);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            cmbStation.Items.Add(new StationItem
                            {
                                StationID   = Convert.ToInt32(reader["stationID"]),
                                DisplayName = reader["stationName"].ToString()
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
         * FUNCTION    : CmbStation_SelectionChanged
         * DESCRIPTION : Updates the selected station ID and immediately refreshes data.
         * PARAMETERS  : object sender           : The ComboBox that raised the event
         *               SelectionChangedEventArgs e : Event data
         * RETURNS     : void
         */
        private void CmbStation_SelectionChanged(object sender,
            System.Windows.Controls.SelectionChangedEventArgs e)
        {
            StationItem item = cmbStation.SelectedItem as StationItem;
            if (item != null)
            {
                selectedStationID = item.StationID;
                RefreshData();
            }
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
         * DESCRIPTION : Queries vw_WorkstationStatus for the selected station and updates the
         *               DataGrid. Rows with isLowStock = 1 are highlighted red by a DataTrigger.
         * PARAMETERS  : None
         * RETURNS     : void
         */
        private void RefreshData()
        {
            if (selectedStationID == 0) return;

            try
            {
                List<PartRow> rows = new List<PartRow>();
                string stationStatus = "";

                using (SqlConnection conn = new SqlConnection(kConnectionString))
                {
                    conn.Open();
                    string sql = @"SELECT stationStatus, partName, currentQuantity, defaultCapacity, isLowStock
                                   FROM   vw_WorkstationStatus
                                   WHERE  stationID = @sid";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@sid", selectedStationID);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            stationStatus = reader["stationStatus"].ToString();
                            rows.Add(new PartRow
                            {
                                PartName = reader["partName"].ToString(),
                                Qty      = Convert.ToInt32(reader["currentQuantity"]),
                                Capacity = Convert.ToInt32(reader["defaultCapacity"]),
                                IsLow    = Convert.ToBoolean(reader["isLowStock"])
                            });
                        }
                    }
                }

                txtStatus.Text       = "Station Status: " + stationStatus;
                txtStatus.Foreground = stationStatus == "Blocked"
                    ? Brushes.Red
                    : Brushes.Green;

                // Show prominent runner banner when any bin is low
                bool anyLow = rows.Exists(r => r.IsLow);
                bannerRunner.Visibility = anyLow ? Visibility.Visible : Visibility.Collapsed;

                gridParts.ItemsSource = rows;
                txtRefresh.Text = "Last updated: " + DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Error: " + ex.Message;
            }
        }
    }

    public class PartRow
    {
        public string PartName { get; set; }
        public int    Qty      { get; set; }
        public int    Capacity { get; set; }
        public bool   IsLow    { get; set; }
        public string Status   => IsLow ? "LOW STOCK" : "OK";
    }

    public class StationItem
    {
        public int    StationID   { get; set; }
        public string DisplayName { get; set; }
        public override string ToString() => DisplayName;
    }
}
