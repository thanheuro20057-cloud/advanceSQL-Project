/*
 * FILE          : MainWindow.xaml.cs
 * PROJECT       : Advanced SQL Project
 * PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli
 * FIRST VERSION : 2026-03-08
 * DESCRIPTION   : WPF runner display. Polls vw_ActiveAlerts every 2 seconds and shows
 *                 which bins need replacement. The runner can refill a selected station
 *                 or all stations at once by calling sp_RefillAllBins.
 */

using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace RunnerDisplay
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
         * DESCRIPTION : Timer callback that refreshes active alerts every 2 seconds.
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
         * DESCRIPTION : Queries vw_ActiveAlerts and populates the alerts DataGrid.
         *               The alert count label updates to show urgency.
         * PARAMETERS  : None
         * RETURNS     : void
         */
        private void RefreshData()
        {
            try
            {
                List<AlertRow> rows = new List<AlertRow>();

                using (SqlConnection conn = new SqlConnection(kConnectionString))
                {
                    conn.Open();
                    string sql = @"SELECT stationID, stationName, partName, currentQuantity, alertTime
                                   FROM   vw_ActiveAlerts
                                   ORDER  BY stationID, partName";

                    SqlCommand cmd = new SqlCommand(sql, conn);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            rows.Add(new AlertRow
                            {
                                StationID   = Convert.ToInt32(reader["stationID"]),
                                StationName = reader["stationName"].ToString(),
                                PartName    = reader["partName"].ToString(),
                                Qty         = Convert.ToInt32(reader["currentQuantity"]),
                                AlertTime   = Convert.ToDateTime(reader["alertTime"]).ToString("HH:mm:ss")
                            });
                        }
                    }
                }

                gridAlerts.ItemsSource = rows;

                if (rows.Count == 0)
                {
                    txtAlertCount.Text       = "All bins OK — no runner action needed";
                    bannerAlert.Background   = new SolidColorBrush(Color.FromRgb(232, 245, 233));
                    txtAlertCount.Foreground = Brushes.DarkGreen;
                }
                else
                {
                    txtAlertCount.Text       = string.Format("ACTION REQUIRED: {0} bin(s) need replacement!", rows.Count);
                    bannerAlert.Background   = new SolidColorBrush(Color.FromRgb(220, 53, 69));
                    txtAlertCount.Foreground = Brushes.White;
                }

                txtRefresh.Text = "Last updated: " + DateTime.Now.ToString("HH:mm:ss");
            }
            catch (Exception ex)
            {
                txtAlertCount.Text = "Error: " + ex.Message;
            }
        }

        /*
         * FUNCTION    : BtnRefillStation_Click
         * DESCRIPTION : Calls sp_RefillAllBins for the station of the currently selected row.
         * PARAMETERS  : object sender      : The button that raised the event
         *               RoutedEventArgs e  : Event data
         * RETURNS     : void
         */
        private void BtnRefillStation_Click(object sender, RoutedEventArgs e)
        {
            AlertRow selected = gridAlerts.SelectedItem as AlertRow;
            if (selected == null)
            {
                MessageBox.Show("Select a row to identify the station to refill.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                RefillStation(selected.StationID);
                RefreshData();
                MessageBox.Show(string.Format("Station '{0}' refilled.", selected.StationName),
                    "Done", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*
         * FUNCTION    : BtnRefillAll_Click
         * DESCRIPTION : Calls sp_RefillAllBins for every station that has an active alert.
         * PARAMETERS  : object sender      : The button that raised the event
         *               RoutedEventArgs e  : Event data
         * RETURNS     : void
         */
        private void BtnRefillAll_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                List<int> stationIDs = new List<int>();

                using (SqlConnection conn = new SqlConnection(kConnectionString))
                {
                    conn.Open();
                    SqlCommand cmd = new SqlCommand(
                        "SELECT DISTINCT stationID FROM vw_ActiveAlerts", conn);

                    using (SqlDataReader reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                            stationIDs.Add(Convert.ToInt32(reader["stationID"]));
                    }
                }

                foreach (int sid in stationIDs)
                    RefillStation(sid);

                RefreshData();
                MessageBox.Show("All stations refilled.", "Done",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Database Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*
         * FUNCTION    : RefillStation
         * DESCRIPTION : Executes sp_RefillAllBins stored procedure for the given station.
         * PARAMETERS  : int stationID : The ID of the station whose bins will be refilled
         * RETURNS     : void
         */
        private void RefillStation(int stationID)
        {
            using (SqlConnection conn = new SqlConnection(kConnectionString))
            {
                conn.Open();
                SqlCommand cmd = new SqlCommand("sp_RefillAllBins", conn);
                cmd.CommandType = CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@stationID", stationID);
                cmd.ExecuteNonQuery();
            }
        }
    }

    public class AlertRow
    {
        public int    StationID   { get; set; }
        public string StationName { get; set; }
        public string PartName    { get; set; }
        public int    Qty         { get; set; }
        public string AlertTime   { get; set; }
    }
}
