using System;
using System.Linq;
using System.Data.SQLite;
using System.IO;
using System.Configuration;
using System.Collections.Generic;
namespace CY
{
    static class ManageDataGrids
    {
        static public SQLiteConnection DB;

        public static string FilesFolder { get; } = "./files";
        public static string DBPath { get; } = Path.Combine(FilesFolder, "GirlsDB.db");
        public static string VideoGirlsPath { get; } = Path.Combine(FilesFolder, "VideoGirls.yaml");
        public static string DeletedCSV { get; } = Path.Combine(FilesFolder, "Deleted.csv");
        public static string EntryIdPath { get; } = Path.Combine(FilesFolder, "EntryId.txt");
        public static string UpdatedVideos { get; } = Path.Combine(FilesFolder, "UpdatedVideos.txt");
        public static GirlList ReturnGirlsInfo(string command)
        {
            GirlList girlList = new GirlList();
            using (DB = new SQLiteConnection($"Data Source={DBPath}"))
            {
                DB.Open();

                using (SQLiteCommand CMD = DB.CreateCommand())
                {
                    CMD.CommandText = command;
                    SQLiteDataReader SQL = CMD.ExecuteReader();
                    if (SQL.HasRows)
                    {
                        while (SQL.Read())
                        {
                            Girl girl = new Girl();
                            girl.Name = (string)SQL["name"];
                            girl.City = (string)SQL["city"];
                            girl.AddDate = Convert.ToDateTime((string)SQL["adddate"]);
                            girl.DateOfState = Convert.ToDateTime((string)SQL["dateofstate"]);
                            girl.Images = ((string)SQL["images"]).Split('\n').ToList();
                            string vids = ((string)SQL["videos"]);
                            girl.Videos = vids==""?(new List<string>()) : vids.Split('\n').ToList();
                            girl.BirthDate = Convert.ToDateTime((string)SQL["birthdate"]);
                            girl.AgeThen = (decimal)SQL["agethen"];
                            girl.Link = (string)SQL["link"];
                            girl.BirthDateAsIs = (string)SQL["birthdatestr"];
                            girl.Rating = Convert.ToInt32((decimal)SQL["rating"]);
                            girl.Views = Convert.ToInt32((decimal)SQL["views"]);
                            girl.Notes = SQL["notes"]==DBNull.Value?null:(string)SQL["notes"];
                            girl.Id = Convert.ToInt32((decimal)SQL["linkid"]);
                            girl.Ava = (string)SQL["ava"];
                            girl.LastAccess = SQL["lastAccess"] == DBNull.Value ? null : (string)SQL["lastAccess"];
                            girl.LastUpdate = SQL["lastUpdate"] == DBNull.Value ? null : (string)SQL["lastUpdate"];
                            girlList.ListOfGirls.Add(girl);
                        }
                    }
                }
            }
            return girlList;
        }
    }
}
