using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net;
using System.Threading.Tasks;
using DotNetty.Common.Internal.Logging;
using Newtonsoft.Json;
using Server.Medius;
using Server.Plugins.Interface;
using System.Text.Json;
using Server.Database.Models;
using Server.Medius.Models;
using System.Data.SQLite;

namespace Horizon.Plugin.UYA
{
    public class RoboDatabase
    {

        public static Plugin Plugin = null;
        public static Plugin Host = null;
        SQLiteConnection Sql_con = null;

        public RoboDatabase(Plugin host)
        {
            Host = host;
            Sql_con = new SQLiteConnection("Data Source=/database/database.db;Version=3;New=False;");

            try
            {
                Sql_con.Open();
            }
            catch (Exception ex)
            {
                Host.DebugLog("Robo Database failed to load!");
                Host.DebugLog(ex.ToString());
            }

            SQLiteCommand sql_cmd = Sql_con.CreateCommand(); 
            string myQuery = "select username from users where account_id = 3;"; 
            sql_cmd.CommandText = myQuery;            
            string rw_maxid = (string)sql_cmd.ExecuteScalar();

            Host.DebugLog("Got result: " + rw_maxid);


            //TestDb();
        }

        // public void TestDb() {
        //        SetConnection();                        
        //         sql_con.Open();                    
        //         sql_cmd = sql_con.CreateCommand(); 

        //         myQuery = "select username from users where account_id = 3;"; 
        //         sql_cmd.CommandText = myQuery;            
        //         rw_maxid = (long)sql_cmd.ExecuteScalar();

        //         myQuery = "Select max(id) from myTable WHERE myColumn LIKE '" + myName+ "%';"; 
        //         Debug.WriteLine(myQuery);      
        //         sql_cmd.CommandText = myQuery;            
        //         rw_maxid = (long)sql_cmd.ExecuteScalar();
        // }

        public void CreateDbExample()
        {
            Host.DebugLog("TESTING DB FUNCTION!");

            string db_file = "database.db";
            string mode = "rwc";
            string _connectionString =  "file:" + db_file + "?mode=" + mode;


            SQLiteConnection sqlite_conn;
            // Create a new database connection:
            sqlite_conn = new SQLiteConnection("Data Source=/database/database.db;Version=3;New=False;");
            // Open the connection:
            try
            {
                sqlite_conn.Open();
            }
            catch (Exception ex)
            {

            }
            SQLiteCommand sqlite_cmd;
            string Createsql = "CREATE TABLE SampleTable (Col1 VARCHAR(20), Col2 INT)";
            string Createsql1 = "CREATE TABLE SampleTable1 (Col1 VARCHAR(20), Col2 INT)";
            sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = Createsql;
            sqlite_cmd.ExecuteNonQuery();
            sqlite_cmd.CommandText = Createsql1;
            sqlite_cmd.ExecuteNonQuery();

            sqlite_conn.Close();

            // using (var db = new SQLiteConnection(_connectionString))
            // {

            //     // using (var cmd = db.CreateCommand())
            //     // {
            //     //     db.Open();
            //     //     cmd.CommandText = "SELECT Title, URL FROM Songs ORDER BY Title";
            //     //     var reader = cmd.ExecuteReader();
            //     //     if (reader.HasRows)
            //     //     {
            //     //         while (reader.Read())
            //     //         {
            //     //             Log(InternalLogLevel.DEBUG, reader.GetString(0));
            //     //             // result.Add(new Song() { 
            //     //             //     Title = reader.GetString(0),
            //     //             //     URL = reader.GetString(1)
            //     //             // });
            //     //         }
            //     //     }
            //     //     db.Close();
            //     // }
            // }
        }
    }
}

