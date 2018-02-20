using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using unirest_net.http;

namespace StageTracker {
    class Program {
        public static Dictionary<int, string> ownersData = new Dictionary<int, string>();
        public static Dictionary<int, string> stageData = new Dictionary<int, string>();
        public static StreamWriter log;
        public static List<Owner> owners;
        public static List<Deal> conList;
        public static string token = "";
        public static string position = "";
        private static Random random = new Random();
        private static string connString = "Data Source=RALIMSQL1;Initial Catalog=CAMSRALFG;Integrated Security=SSPI;";
        private static string line = @"INSERT INTO [CAMSRALFG].[dbo].[Base_StageChanges] ([deal_id] ,[stage_id] ,[stage_name] ,"+
            "[owner_id] ,[owner_name] ,[contact_id] ,[ecd] ,[changed_at] ,[event_id] ,[event_type] ,[previous_stage_id] , " +
            "[previous_stage_name] ,[position]) VALUES (@deal_id ,@stage_id ,@stage_name ,@owner_id ,@owner_name ,@contact_id ," +
            "@ecd ,@changed_at ,@event_id ,@event_type ,@previous_stage_id ,@previous_stage_name ,@position); ";

        static void Main(string[] args) {

            string startURL = @"https://api.getbase.com/v3/deals/stream?limit=100&position=";
            owners = new List<Owner>();
            conList = new List<Deal>();
            var fs = new FileStream(@"C:\apps\NiceOffice\token", FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var ps = new FileStream(@"\\NAS3\NOE_Docs$\RALIM\Logs\Base\position", FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
            DateTime now = DateTime.Now;

            using (var tokenReader = new StreamReader(fs)) {
                token = tokenReader.ReadToEnd();
            }

            using (var posReader = new StreamReader(ps)) {
                position = posReader.ReadToEnd();
            }

            string logPath = @"\\NAS3\NOE_Docs$\RALIM\Logs\Base\SyncDeals_";
            logPath += now.ToString("ddMMyyyy") + ".txt";

            if (!File.Exists(logPath)) {
                using (StreamWriter sw = File.CreateText(logPath)) {
                    sw.WriteLine("Creating 1st appoitment log file for " + now.ToString("ddMMyyyy") + " at " + now.ToString());
                }
            }

            log = File.AppendText(logPath);
            log.WriteLine("Starting check at " + now );
            Console.WriteLine("Starting check " + now);

            string me = Environment.UserDomainName.ToString() + @"\" + Environment.UserName;
            SetStageData();
            SetOwnerData();

            bool top = false;

            while (!top) { // continue until there are less than 100 results
                string rawJSON = Get(startURL + position, token);
                JObject jsonObj = JObject.Parse(rawJSON) as JObject;

                var items = jsonObj["items"] as JArray;
                top = Convert.ToBoolean(jsonObj["meta"]["top"]);


                log.WriteLine("starting check of " + items.Count + " events at position " + position);
                Console.WriteLine("starting check of " + items.Count + " events at position " + position);

                foreach (var item in items) {
                    var data = item["data"];
                    var meta = item["meta"];

                    int owner_id = 0;
                    if (data["owner_id"] != null && data["owner_id"].ToString() != "") {
                        owner_id = Convert.ToInt32(data["owner_id"]);
                    }
                    string owner_name = "Unknown";
                    if (ownersData.ContainsKey(owner_id)) {//if the owner is missing, do not catalog status changes
                        owner_name = ownersData[owner_id].ToString();
                    } else continue;

                    int id = Convert.ToInt32(data["id"]);
                    int stage_id = Convert.ToInt32(data["stage_id"]);
                    string stage_name = stageData[stage_id];

                    string estimated_close_date = "";
                    if (data["estimated_close_date"] != null) {
                        estimated_close_date = data["estimated_close_date"].ToString();
                    }

                    int contact_id = 0;
                    if (data["contact_id"] != null && data["contact_id"].ToString() != "") {
                        contact_id = Convert.ToInt32(data["contact_id"]);
                    }

                    string event_id = meta["event_id"].ToString();
                    string event_type = meta["event_type"].ToString();
                    DateTime event_time = Convert.ToDateTime(meta["event_time"]).ToLocalTime();
                    int previous_stage_id = 0;
                    string previous_stage_name = "Unknown";

                    if (event_type == "updated" && meta["previous"].HasValues) {
                        if (meta["previous"]["stage_id"] != null && meta["previous"]["stage_id"].ToString() != "") {
                            previous_stage_id = Convert.ToInt32(meta["previous"]["stage_id"]);
                            previous_stage_name = stageData[previous_stage_id];
                        } else continue; // if this is updated but we don't know where it was, ignore it
                    }
                    conList.Add(new Deal(id, stage_id, stage_name, owner_id, owner_name, contact_id, estimated_close_date, 
                        event_time, event_id, event_type, previous_stage_id, previous_stage_name, position));
                }
                position = jsonObj["meta"]["position"].ToString();
                using (var posReader = new StreamWriter(@"\\NAS3\NOE_Docs$\RALIM\Logs\Base\position", false)) {
                    posReader.Write(position);
                }
            }
      
            if (conList.Count <= 0) {
                log.WriteLine("No updates to stages found");
                Console.WriteLine("No updates to stages found");
                log.Close();
                Environment.Exit(0);
            }

            using (SqlConnection connection = new SqlConnection(connString)) {

                foreach (var item in conList) {
                    string thisLine = "" + item.deal_id + ", " +
                        item.stage_id + ", " +
                        item.stage_name + ", " +
                        item.owner_id + ", " +
                        item.owner_name + ", " +
                        item.contact_id + ", " +
                        item.ecd + ", " +
                        item.changed_at + ", " +
                        item.event_id + ", " +
                        item.event_type + ", " +
                        item.previous_stage_id + ", " +
                        item.previous_stage_name + ", " +
                        item.position;
                    log.WriteLine(thisLine);
                    Console.WriteLine(thisLine);

                    using (SqlCommand command = new SqlCommand(line, connection)) {
                        command.Parameters.Add("@deal_id", SqlDbType.Int).Value = item.deal_id;
                        command.Parameters.Add("@stage_id", SqlDbType.Int).Value = item.stage_id;
                        command.Parameters.Add("@stage_name", SqlDbType.NVarChar).Value = item.stage_name;
                        command.Parameters.Add("@owner_id", SqlDbType.Int).Value = item.owner_id;
                        command.Parameters.Add("@owner_name", SqlDbType.NVarChar).Value = item.owner_name;
                        command.Parameters.Add("@contact_id", SqlDbType.Int).Value = item.contact_id;
                        command.Parameters.Add("@ecd", SqlDbType.NVarChar).Value = item.ecd;
                        command.Parameters.Add("@changed_at", SqlDbType.DateTime).Value = item.changed_at;
                        command.Parameters.Add("@event_id", SqlDbType.NVarChar).Value = item.event_id;
                        command.Parameters.Add("@event_type", SqlDbType.NVarChar).Value = item.event_type;
                        command.Parameters.Add("@previous_stage_id", SqlDbType.Int).Value = item.previous_stage_id;
                        command.Parameters.Add("@previous_stage_name", SqlDbType.NVarChar).Value = item.previous_stage_name;
                        command.Parameters.Add("@position", SqlDbType.NVarChar).Value = item.position;
                        string query = command.CommandText;

                        //foreach (SqlParameter p in command.Parameters) {
                        //    query = query.Replace(p.ParameterName, p.Value.ToString());
                        //}
                        //log.WriteLine(query);
                        //Console.WriteLine(query);


                        try {
                            connection.Open();
                            int result = command.ExecuteNonQuery();

                            if (result <= 0) {
                                log.WriteLine("INSERT failed for " + command.ToString());
                                Console.WriteLine("INSERT failed for " + command.ToString());
                            }
                        }
                        catch (Exception ex) {
                            log.WriteLine(ex);
                            Console.WriteLine(ex);
                            log.Flush();
                        }
                        finally {
                            connection.Close();
                        }
                    }
                }
            }

            log.WriteLine("Done!");
            Console.WriteLine("Done!");
            log.Close();
            //log.ReadLine();
        }

        public static string Get(string url, string token) {
            string body = "";
            try {
                HttpResponse<string> jsonReponse = Unirest.get(url)
                    .header("accept", "application/json")
                    .header("Authorization", "Bearer " + token)
                    .asJson<string>();
                body = jsonReponse.Body.ToString();
                return body;
            }
            catch (Exception ex) {
                log.WriteLine(ex);
                log.WriteLine(ex);

                log.Flush();
                return body;
            }
        }

        public static void SetOwnerData() {
            string testJSON = Get(@"https://api.getbase.com/v2/users?per_page=100&sort_by=created_at&status=active", token);
            JObject jObj = JObject.Parse(testJSON) as JObject;
            JArray jArr = jObj["items"] as JArray;
            foreach (var obj in jArr) {
                var data = obj["data"];

                if (data["group"].HasValues == false || Convert.ToInt32(data["group"]["id"]) != 84227) {
                    continue; //do not count agents not in sales group stats.
                }

                int tID = Convert.ToInt32(data["id"]);
                string tName = data["name"].ToString();
                ownersData.Add(tID, tName);
            }
        }

        public static void SetStageData() {
            string testJSON = Get(@"https://api.getbase.com/v2/stages?per_page=100", token);
            JObject jObj = JObject.Parse(testJSON) as JObject;
            JArray jArr = jObj["items"] as JArray;
            stageData.Add(0, "Unknown");

            foreach (var obj in jArr) {
                var data = obj["data"];
                int tID = Convert.ToInt32(data["id"]);
                string tName = data["name"].ToString();
                stageData.Add(tID, tName);
            }
        }
    }
}