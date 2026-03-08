Advanced SQL Project 
Author: Tuan Thanh Nguyen. Burhan Shibli
Date: March 8, 2026

System Requirements:

SQL Server Management Studio (SSMS) 2022

Visual Studio 2022 (.NET 8 or .NET Framework)

Setup Instructions:

Database Setup: >    - Open SSMS and connect to your local SQL Server instance.

Open the Database\Create_Configuration.sql script.

Execute the script. This will automatically create the FogLampAssemblyDB database, build the Configuration table, and insert the default simulation parameters.

Application Setup:

Open SourceCode\advanceSQL-Project\advanceSQL-Project.sln in Visual Studio 2022.

Open MainWindow.xaml.cs and ensure the kConnectionString variable on line 20 matches your local SQL Server instance name (it is currently set to "localhost").

Build and Run the project (F5).

Usage:

The WPF Configuration Tool will load the parameters from the database.

Edit any value in the settingValue column (e.g., change TargetOrderAmount).

Click "Save Changes to Database". You can restart the application to verify the data persisted.