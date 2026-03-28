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
        private int stationID = 3;
        private int lampCount = 0;

        public MainWindow()
        {
            InitializeComponent();

            txtStation.Text = "Station " + stationID;

            Thread simulationThread = new Thread(RunSimulation);
            simulationThread.IsBackground = true;
            simulationThread.Start();
        }

        private void RunSimulation()
        {
            while (true)
            {
                try
                {
                    int result = 0;

                    using (SqlConnection conn = new SqlConnection(kConnectionString))
                    {
                        conn.Open();

                        SqlCommand cmd = new SqlCommand("sp_BuildLamp", conn);
                        cmd.CommandType = CommandType.StoredProcedure;

                        cmd.Parameters.AddWithValue("@stationID", stationID);

                        SqlParameter builtParam = new SqlParameter("@built", SqlDbType.Int);
                        builtParam.Direction = ParameterDirection.Output;
                        cmd.Parameters.Add(builtParam);

                        cmd.ExecuteNonQuery();

                        result = Convert.ToInt32(builtParam.Value);
                    }

                    if (result == 1)
                    {
                        lampCount++;

                        Dispatcher.Invoke(() =>
                        {
                            txtStatus.Text = "Running... Lamps built: " + lampCount;
                        });
                    }
                    else
                    {
                        Dispatcher.Invoke(() =>
                        {
                            txtStatus.Text = "Waiting for refill...";
                        });
                    }
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() =>
                    {
                        txtStatus.Text = "Error: " + ex.Message;
                    });
                }

                Thread.Sleep(GetBuildDelay());
            }
        }

        private int GetBuildDelay()
        {
            int delay = 3000;

            try
            {
                using (SqlConnection conn = new SqlConnection(kConnectionString))
                {
                    conn.Open();

                    string sql = @"
                        SELECT w.skillLevel
                        FROM Workstation ws
                        JOIN Worker w ON ws.currentWorkerID = w.workerID
                        WHERE ws.stationID = @stationID";

                    SqlCommand cmd = new SqlCommand(sql, conn);
                    cmd.Parameters.AddWithValue("@stationID", stationID);

                    object result = cmd.ExecuteScalar();

                    if (result != null)
                    {
                        string skillLevel = result.ToString();

                        if (skillLevel == "Rookie")
                        {
                            delay = 5000;
                        }
                        else if (skillLevel == "Normal")
                        {
                            delay = 3000;
                        }
                        else if (skillLevel == "Super")
                        {
                            delay = 2000;
                        }
                    }
                }
            }
            catch
            {
                delay = 3000;
            }

            return delay;
        }


        private void btnRefill_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (SqlConnection conn = new SqlConnection(kConnectionString))
                {
                    conn.Open();

                    SqlCommand cmd = new SqlCommand("sp_RefillStock", conn);
                    cmd.CommandType = CommandType.StoredProcedure;
                    cmd.Parameters.AddWithValue("@stationID", stationID);

                    cmd.ExecuteNonQuery();
                }

                txtStatus.Text = "Stock refilled.";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Error: " + ex.Message;
            }
        }
    }
}