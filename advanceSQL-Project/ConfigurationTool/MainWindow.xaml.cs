/*
 * FILE          : MainWindow.xaml.cs
 * PROJECT       : Advanced SQL Project 
 * PROGRAMMER    : Tuan Thanh Nguyen, Burhan Shibli
 * FIRST VERSION : 2026-03-08
 * DESCRIPTION   : Code-behind for the Configuration Tool. Connects to SQL Server 
 * using ADO.NET to read and update simulation parameters.
 */

using System;
using System.Data;
using System.Data.SqlClient;
using System.Windows;

namespace ConfigurationTool
{
    public partial class MainWindow : Window
    {
        // connection string to connect to the local SQL Server instance and FogLampAssemblyDB database
        private const string kConnectionString = @"Server=localhost;Database=FogLampAssemblyDB;Trusted_Connection=True;";

        private SqlDataAdapter myDataAdapter;
        private DataTable myConfigTable;

        public MainWindow()
        {
            InitializeComponent();
            LoadConfigurationData();
        }

        /*
         * FUNCTION    : LoadConfigurationData
         * DESCRIPTION : Fetches data from the Configuration table and binds it to the WPF DataGrid.
         * PARAMETERS  : None
         * RETURNS     : void
         */
        private void LoadConfigurationData()
        {
            try
            {
                using (SqlConnection dbConnection = new SqlConnection(kConnectionString))
                {
                    string sqlQuery = "SELECT configID, settingName, settingValue, description FROM Configuration";
                    myDataAdapter = new SqlDataAdapter(sqlQuery, dbConnection);

                    // Automatically generate the UPDATE, INSERT, and DELETE commands based on the SELECT query
                    SqlCommandBuilder commandBuilder = new SqlCommandBuilder(myDataAdapter);

                    myConfigTable = new DataTable();
                    myDataAdapter.Fill(myConfigTable);

                    // Make configID read-only so users don't break the database primary key
                    if (myConfigTable.Columns.Contains("configID"))
                    {
                        myConfigTable.Columns["configID"].ReadOnly = true;
                    }

                    configDataGrid.ItemsSource = myConfigTable.DefaultView;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading configuration data: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*
         * FUNCTION    : BtnSaveChanges_Click
         * DESCRIPTION : Event handler that commits any edits made in the DataGrid back to the SQL Server database.
         * PARAMETERS  : object sender : The object that raised the event
         * RoutedEventArgs e : The event data
         * RETURNS     : void
         */
        private void BtnSaveChanges_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ensure any active cell edits are committed to the underlying DataTable before saving
                configDataGrid.CommitEdit();
                configDataGrid.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

                if (myConfigTable != null)
                {
                    // The connection used during Load was disposed when that using-block exited.
                    // Open a fresh connection and rebuild the adapter so Update() has a live connection.
                    using (SqlConnection conn = new SqlConnection(kConnectionString))
                    {
                        string sqlQuery = "SELECT configID, settingName, settingValue, description FROM Configuration";
                        SqlDataAdapter saveAdapter = new SqlDataAdapter(sqlQuery, conn);
                        SqlCommandBuilder commandBuilder = new SqlCommandBuilder(saveAdapter);
                        saveAdapter.Update(myConfigTable);
                    }
                    MessageBox.Show("Configuration parameters saved successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error saving configuration data: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /*
         * FUNCTION    : BtnReload_Click
         * DESCRIPTION : Reloads the configuration data from the database, discarding unsaved edits.
         * PARAMETERS  : object sender, RoutedEventArgs e
         * RETURNS     : void
         */
        private void BtnReload_Click(object sender, RoutedEventArgs e)
        {
            LoadConfigurationData();
        }

        /*
         * FUNCTION    : BtnResetSim_Click
         * DESCRIPTION : Calls sp_ResetSimulation to clear production logs and reset all bins.
         * PARAMETERS  : object sender, RoutedEventArgs e
         * RETURNS     : void
         */
        private void BtnResetSim_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show(
                "This will clear all production logs and reset all bins to default.\nAre you sure?",
                "Reset Simulation", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                try
                {
                    using (SqlConnection conn = new SqlConnection(kConnectionString))
                    {
                        conn.Open();
                        using (SqlCommand cmd = new SqlCommand("sp_ResetSimulation", conn))
                        {
                            cmd.CommandType = CommandType.StoredProcedure;
                            cmd.ExecuteNonQuery();
                        }
                    }
                    MessageBox.Show("Simulation reset successfully.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Error resetting simulation: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}