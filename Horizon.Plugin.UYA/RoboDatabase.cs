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
using System.Data.SQLite;
using BCrypt.Net;

namespace Horizon.Plugin.UYA
{
    public class RoboDatabase
    {

        public static Plugin Plugin = null;
        public static Plugin Host = null;
        SQLiteConnection Sql_con = null;
        public static string RoboSalt = Environment.GetEnvironmentVariable("ROBO_SALT");


        public RoboDatabase(Plugin host)
        {
            Host = host;

            if (Sql_con == null)
                Sql_con = new SQLiteConnection("Data Source=/database/database.db;Version=3;New=False;");

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

        public void QueryDb() {
            SQLiteCommand sql_cmd = Sql_con.CreateCommand(); 
            string myQuery = "select username from users where account_id = 3;"; 
            sql_cmd.CommandText = myQuery;            
            string rw_maxid = (string)sql_cmd.ExecuteScalar();

            Host.DebugLog("Got result: " + rw_maxid);
        }

        public bool AccountExists(string username) {
            return true;
        }

        public string GetPassword(string username) {
            Host.DebugLog("Querying Robo DB password for username: " + username);
            string sql = "select password from users where lower(username) = @username;"; 

            using (var command = new SQLiteCommand(sql, Sql_con))
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

