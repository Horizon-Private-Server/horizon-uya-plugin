using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Net;
using System.Threading.Tasks;
using DotNetty.Common.Internal.Logging;
using Newtonsoft.Json;
using Server.Medius;
using Server.Plugins.Interface;
using Server.Common;
using System.Text.Json;
using Server.Database.Models;
using Server.Medius.Models;
using BCrypt.Net;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;

namespace Horizon.Plugin.UYA
{
    public class RoboDatabase
    {

        public static Plugin Plugin = null;
        public static Plugin Host = null;
        SqliteConnection Sql_con = null;
        public static string RoboSalt = Environment.GetEnvironmentVariable("ROBO_SALT");


        public RoboDatabase(Plugin host)
        {
            Host = host;

            if (Sql_con == null)
                Sql_con = new SqliteConnection("Data Source=/database/database.db;Version=3;New=False;");

            Host.DebugLog("Robo Database connecting to authenticate!");
            Host.DebugLog("Using SALT: " + RoboSalt);
            //Server.Medius.Program.Database.AmIAuthenticated();

            try
            {
                Sql_con.Open();
            }
            catch (Exception ex)
            {
                Host.DebugLog("Robo Database failed to load!");
                Host.DebugLog(ex.ToString());
            }

            //TestDb();
        }

        public List<RoboAccount> DumpUsers() {
            List<RoboAccount> accounts = new List<RoboAccount>();

            if (Sql_con == null) {
                return accounts;
            }

            // Create a command to select all rows from the table
            var command = new SqliteCommand("SELECT username, password, ladderstatswide FROM users", Sql_con);
            
            // Execute the command and create a data reader
            using (var reader = command.ExecuteReader())
            {
                // Loop through each row in the result set
                while (reader.Read())
                {
                    // Access the column values using the appropriate data type
                    string username = reader.GetString(0);
                    string password = reader.GetString(1);
                    string stats = reader.GetString(2);
                    //Host.DebugLog($"Found a user: {username} | {password}");

                    int[] CleanedStats = new int[100];
                    for (int i = 0; i < 100; i++)
                    {
                        string thisStat = stats.Substring(i * 8, 8); // get the next 8 characters starting from position i * 8
                        //int result = Convert.ToInt32(thisStat, 16); // base 16 for hex

                        byte[] bytes = new byte[4];

                        // Convert the hex string to a byte array in little-endian order
                        for (int j = 0; j < bytes.Length; j++)
                        {
                            bytes[j] = Convert.ToByte(thisStat.Substring(j * 2, 2), 16);
                        }
                        // Convert the byte array to an integer in little-endian order
                        int result = BitConverter.ToInt32(bytes, 0);

                        CleanedStats[i] = result;
                        //Host.DebugLog($"Got stat: {result.ToString()}");
                    }

                    accounts.Add(new RoboAccount
                    {
                        Username = username,
                        Password = password,
                        AppId = 10684,
                        Stats = CleanedStats
                    });                   
                }
            }

            return accounts;
        }

        public void QueryDb() {
            SqliteCommand sql_cmd = Sql_con.CreateCommand(); 
            string myQuery = "select username from users where account_id = 3;"; 
            sql_cmd.CommandText = myQuery;            
            string rw_maxid = (string)sql_cmd.ExecuteScalar();

            Host.DebugLog("Got result: " + rw_maxid);
        }

        public bool AccountExists(string username) {
            if (Sql_con == null)
                return false;

            Host.DebugLog("Querying Robo DB to check if username exists: " + username);
            string sql = "select username from users where lower(username) = @username;"; 

            using (var command = new SqliteCommand(sql, Sql_con))
            {
                command.Parameters.AddWithValue("@username", username.ToLower());

                string res = (string)command.ExecuteScalar();

                Host.DebugLog($"Username exists in robo db? : {username} | {(res != null).ToString()}");
                return res != null;
            }            
        }

        public string GetPassword(string username) {
            Host.DebugLog("Querying Robo DB password for username: " + username);
            string sql = "select password from users where lower(username) = @username;"; 

            using (var command = new SqliteCommand(sql, Sql_con))
            {
                command.Parameters.AddWithValue("@username", username.ToLower());

                string res = (string)command.ExecuteScalar();
                Host.DebugLog("Got password: " + res);
                return res;
            }
        }

        public string EncryptString(string s) {

            // First SHA512:
            string shad = Utils.ComputeSHA512(s).ToUpper();
            Host.DebugLog($"SHA512 String: Before: {s} After: {shad}");


            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(shad, RoboSalt);
            Host.DebugLog($"Encrypted String: Before: {s} After: {hashedPassword} | Using salt: {RoboSalt}");

            return hashedPassword;
        }

    }
}

