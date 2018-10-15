using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Web;
using HtmlAgilityPack;
using System.Net.Mail;
using System.Data.SQLite;
using SendGrid;
using SendGrid.Helpers.Mail;
using ConfigurationProvider;

namespace IPOService
{
    public struct IPODetails
    {
        public string Name;
        public string Type;
        public DateTime Start;
        public DateTime End;
        public int Count;
        public decimal Rating;
        public string Review;
        public string Url;
    }
    public class IPO
    {
        public List<IPODetails> finalIPO;
        public List<IPODetails> newIPO;
        public List<IPODetails> endingIPO;

        public void CheckIPO(string dbfile, string logfile)
        {
            try
            {
                if (!File.Exists(dbfile))
                {
                    CreateNewDB(dbfile);
                }
                GetIPOs(logfile, dbfile);
            }
            catch (Exception e)
            {
                LogError(e, logfile);
            }
        }

        public void LogError(Exception e, string logfile)
        {
            if (!File.Exists(logfile))
            {
                File.Create(logfile).Close();
            }
            using (StreamWriter writer = new StreamWriter(logfile, true))
            {
                writer.WriteLine();
                writer.WriteLine("------------------------------------------------------------------");
                writer.WriteLine(GetTime().ToString());
                writer.WriteLine(e.Message);
                writer.WriteLine(e.StackTrace);
                writer.WriteLine("------------------------------------------------------------------");
                writer.WriteLine();
            }
        }

        public void LogSuccess(int count, string logfile)
        {
            if (!File.Exists(logfile))
            {
                File.Create(logfile).Close();
            }
            using (StreamWriter writer = new StreamWriter(logfile, true))
            {
                writer.WriteLine();
                writer.WriteLine("------------------------------------------------------------------");
                writer.WriteLine(GetTime().ToString());
                writer.WriteLine("Success " + count + " records updated");
                writer.WriteLine("------------------------------------------------------------------");
                writer.WriteLine();
            }
        }

        public void GetIPOs(string logfile, string dbfile)
        {
            List<IPODetails> mainlineIPO;
            List<IPODetails> smeIPO;
            string mainlineUrl = "http://www.chittorgarh.com/ipo/ipo_list.asp?a=mainline";
            string smeUrl = "http://www.chittorgarh.com/ipo/ipo_list.asp?a=sme";
            mainlineIPO = GetIPOsByUrl(mainlineUrl, "MainLine");
            smeIPO = GetIPOsByUrl(smeUrl, "SME");
            finalIPO = new List<IPODetails>();
            finalIPO = smeIPO.Concat(mainlineIPO).ToList();
            if (finalIPO.Count > 0)
            {
                newIPO = new List<IPODetails>();
                for (int i = 0; i < finalIPO.Count; i++)
                {
                    GetIPODetails(i);
                    AddorUpdateIPO(i, dbfile);
                }
            }
            endingIPO = new List<IPODetails>();
            endingIPO = FetchEndingIPO(dbfile);
            if (newIPO.Count() + endingIPO.Count() > 0)
            {
                SendNotification();
            }
            LogSuccess(newIPO.Count() + endingIPO.Count(), logfile);
        }

        public List<IPODetails> FetchEndingIPO(string dbfile)
        {
            List<IPODetails> result = new List<IPODetails>();
            using (SQLiteConnection conn = new SQLiteConnection("data source=" + dbfile))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = "SELECT * FROM IPO WHERE Start <= '" + GetTime() + "' AND END >= '" + GetTime() + "'";
                    using (SQLiteDataReader rdr = cmd.ExecuteReader())
                    {
                        IPODetails temp = new IPODetails();
                        while (rdr.Read())
                        {
                            temp.Name = rdr.GetString(1);
                            temp.Type = rdr.GetString(2);
                            temp.Start = Convert.ToDateTime(rdr.GetString(3));
                            temp.End = Convert.ToDateTime(rdr.GetString(4));
                            temp.Count = rdr.GetInt32(5);
                            temp.Rating = rdr.GetDecimal(6);
                            temp.Review = rdr.GetString(7);
                            temp.Url = rdr.GetString(8);
                            result.Add(temp);
                        }
                    }
                }
                conn.Clone();
            }
            for (int i = 0; i < result.Count; i++)
            {
                if(IsAlreadyNotified(result[i], dbfile))
                {
                    result.RemoveAt(i);
                    i--;
                }
            }
            return result;
        }

        public bool IsAlreadyNotified(IPODetails ipo, string dbfile)
        {
            bool result = false;
            using (SQLiteConnection conn = new SQLiteConnection("data source=" + dbfile))
            {
                conn.Open();
                string temp = "";
                using (SQLiteCommand cmd = new SQLiteCommand(conn))
                {
                    cmd.CommandText = "SELECT Date FROM IPONotification WHERE Name = '" + ipo.Name + "'";
                    using (SQLiteDataReader rdr = cmd.ExecuteReader())
                    {
                        while (rdr.Read())
                        {
                            temp = rdr.GetString(0);
                        }
                    }
                    if(temp != "")
                    {
                        DateTime lastnotified = Convert.ToDateTime(temp);
                        if(GetTime() >= lastnotified.AddHours(12))
                        {
                            cmd.CommandText = "UPDATE IPONotification SET Date='" + GetTime() + "'";
                            cmd.ExecuteNonQuery();
                        }
                        else
                        {
                            result = true;
                        }
                    }
                    else
                    {
                        cmd.CommandText = "INSERT INTO IPONotification (Name,Date) VALUES ('" + ipo.Name + "','" + GetTime() + "')";
                        cmd.ExecuteNonQuery();
                    }

                }
                conn.Clone();
            }
            return result;
        }

        public List<IPODetails> GetIPOsByUrl(string url, string type)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(url);
            List<IPODetails> result = new List<IPODetails>();
            var tablelist = doc.DocumentNode.SelectNodes("//table").ToList();
            foreach (var table in tablelist)
            {
                if (table.InnerText.Contains("Upcoming IPO"))
                {
                    var trlist = table.SelectNodes(".//tr").ToList();
                    foreach (var tr in trlist)
                    {
                        if (!tr.HasClass("active"))
                        {
                            var tdlist = tr.SelectNodes(".//td").ToList();
                            if (tdlist.Count > 2)
                            {
                                IPODetails ipo = new IPODetails();
                                var namenode = tdlist[0].SelectSingleNode(".//a");
                                ipo.Name = namenode.Attributes["title"].Value;
                                ipo.Url = namenode.Attributes["href"].Value;
                                if(tdlist[2].InnerText != "" && tdlist[3].InnerText != "")
                                {
                                    ipo.Start = DateTime.ParseExact(tdlist[2].InnerText, "MMM d, yyyy", null);
                                    ipo.End = DateTime.ParseExact(tdlist[3].InnerText, "MMM d, yyyy", null);
                                    ipo.End = ipo.End.AddHours(15);
                                    ipo.Type = type;
                                    if (ipo.End > GetTime())
                                    {
                                        result.Add(ipo);
                                    }
                                }                                
                            }
                        }
                    }
                }
            }
            return result;
        }

        public void GetIPODetails(int i)
        {
            var ipo = finalIPO[i];
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load(ipo.Url);
            List<IPODetails> result = new List<IPODetails>();
            ipo.Count = Convert.ToInt32(doc.DocumentNode.SelectSingleNode("//span[@class='voteres']").InnerText);
            ipo.Rating = Convert.ToDecimal(doc.DocumentNode.SelectSingleNode("//span[@class='cyel']").InnerText);
            var reviewnode = doc.DocumentNode.SelectSingleNode("//a[@title='Dilip Davda Review']");
            if (reviewnode != null)
            {
                ipo.Review = doc.DocumentNode.SelectSingleNode("//a[@title='Dilip Davda Review']").InnerText.Replace("Dilip Davda - ", "");
            }
            finalIPO[i] = ipo;
        }

        public void SendNotification()
        {            
            //MailMessage mail = new MailMessage("it61916282@gmail.com", "kalisettisuresh@gmail.com");
            //SmtpClient client = new SmtpClient();
            //client.Port = 587;
            //client.DeliveryMethod = SmtpDeliveryMethod.Network;
            //client.UseDefaultCredentials = false;
            //client.Credentials = new System.Net.NetworkCredential("it61916282@gmail.com", "######");
            //client.EnableSsl = true;
            //client.Host = "smtp.gmail.com";
            //mail.Subject = "Important IPO Update";
            //mail.IsBodyHtml = true;
            string body = "<ol>";
            foreach (var ipo in newIPO)
            {
                string temp = "</br><li><b>"+ipo.Name+"</b>";
                temp += "<ul><li>Type   :   " + ipo.Type + "</li>";
                temp += "<li>Start  :   " + ipo.Start.ToShortDateString() + "</li>";
                temp += "<li>End    :   " + ipo.End.ToShortDateString() + "</li>";
                temp += "<li>Count  :   " + ipo.Count + "</li>";
                temp += "<li>Rating :   " + ipo.Rating + "</li>";
                temp += "<li>Review :   " + ipo.Review + "</li>";
                temp += "<li>Url    :   <a href = \""+ ipo.Url +"\">Click</a></li></ul></li></br>";
                body += temp;
            }
            foreach (var ipo in endingIPO)
            {
                string temp = "</br><li><b>" + ipo.Name + "</b>";
                temp += "<ul><li>Type   :   " + ipo.Type + "</li>";
                temp += "<li>Start  :   " + ipo.Start.ToShortDateString() + "</li>";
                temp += "<li>End    :   " + ipo.End.ToShortDateString() + "</li>";
                temp += "<li>Count  :   " + ipo.Count + "</li>";
                temp += "<li>Rating :   " + ipo.Rating + "</li>";
                temp += "<li>Review :   " + ipo.Review + "</li>";
                temp += "<li>Url    :   <a href = \"" + ipo.Url + "\">Click</a></li></ul></li></br>";
                body += temp;
            }
            body += "</ol>";
            //mail.Body = body;
            //client.Send(mail);
            var apiKey = Config.SendGridAPIKey;
            var client = new SendGridClient(apiKey);
            var msg = new SendGridMessage()
            {
                From = new EmailAddress("it61916282@gmail.com", "Automation"),
                Subject = "Important IPO Update",
                PlainTextContent = " ",
                HtmlContent = body
            };
            msg.AddTo(new EmailAddress("kalisettisuresh@gmail.com", "Suresh"));
            client.SendEmailAsync(msg);
        }

        public void AddorUpdateIPO(int i, string dbfile)
        {
            IPODetails ipo = finalIPO[i];
            using (SQLiteConnection conn = new SQLiteConnection("data source="+dbfile))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(conn))
                {
                    string selectQuery = "SELECT COUNT(*) FROM IPO WHERE Name = '" + ipo.Name + "'";
                    cmd.CommandText = selectQuery;
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    string query = "";
                    if (count > 0)
                    {
                        query = "UPDATE IPO SET Start='" + ipo.Start.ToShortDateString() + "',End='" + ipo.End.ToShortDateString() + "',Count=" + ipo.Count + ",Rating=" + ipo.Rating + ",Review='" + ipo.Review + "',Url='" + ipo.Url + "',UpdatedDate='" + GetTime().ToShortDateString() + "' WHERE Name='" + ipo.Name + "'";
                    }
                    else
                    {
                        query = "INSERT INTO IPO (Name,Type,Start,End,Count,Rating,Review,Url,UpdatedDate) VALUES ('" + ipo.Name + "','" + ipo.Type + "','" + ipo.Start + "','" + ipo.End + "'," + ipo.Count + "," + ipo.Rating + ",'" + ipo.Review + "','" + ipo.Url + "','" + GetTime().ToShortDateString() + "')";
                        newIPO.Add(ipo);
                    }
                    cmd.CommandText = query;
                    cmd.ExecuteNonQuery();
                }
                conn.Clone();
            }
        }

        public void CreateNewDB(string dbfile)
        {
            string tableQuery1 = @"CREATE TABLE IF NOT EXISTS
                                  [IPO] (
                                  [Id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                                  [Name] NVARCHAR(100) NOT NULL,
                                  [Type] NVARCHAR(50) NOT NULL,
                                  [Start] DATETIME NULL,
                                  [End] DATETIME NULL,
                                  [Count] INTEGER NULL,
                                  [Rating] DECIMAL(5,2) NULL,
                                  [Review] NVARCHAR(50) NULL,
                                  [Url] NVARCHAR(1000) NULL,
                                  [UpdatedDate] DATETIME NOT NULL)";
            string tableQuery2 = @"CREATE TABLE IF NOT EXISTS
                                  [IPONotification] (
                                  [Id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                                  [Name] NVARCHAR(100) NOT NULL,
                                  [Date] DATETIME NULL)";
            string tableQuery3 = @"CREATE TABLE IF NOT EXISTS
                                  [ICO] (
                                  [Id] INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT,
                                  [Name] NVARCHAR(100) NOT NULL,
                                  [Rating] NVARCHAR(100) NOT NULL,
                                  [UpdatedDate] DATETIME NOT NULL)";
            SQLiteConnection.CreateFile(dbfile);
            using (SQLiteConnection conn = new SQLiteConnection("data source="+dbfile))
            {
                using (SQLiteCommand cmd = new SQLiteCommand(conn))
                {
                    conn.Open();
                    cmd.CommandText = tableQuery1;
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = tableQuery2;
                    cmd.ExecuteNonQuery();
                    cmd.CommandText = tableQuery3;
                    cmd.ExecuteNonQuery();
                }
                conn.Clone();
            }
        }

        public DateTime GetTime()
        {
            TimeZoneInfo INDIAN_ZONE = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time");
            DateTime indianTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, INDIAN_ZONE);
            return indianTime;
        }
    }
}
