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

namespace ICOService
{
    public struct ICODetails
    {
        public string Name;
        public string OldRating;
        public string NewRating;
    }
    public class ICO
    {
        public void CheckICO(string dbfile, string logfile)
        {
            try
            {
                if (!File.Exists(dbfile))
                {
                    CreateNewDB(dbfile);
                }
                GetICOs(dbfile, logfile);
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

        public void GetICOs(string dbfile, string logfile)
        {
            HtmlWeb web = new HtmlWeb();
            HtmlDocument doc = web.Load("https://icodrops.com/category/upcoming-ico/");
            List <ICODetails> result = new List<ICODetails>();
            var icolist = doc.DocumentNode.SelectNodes("//div[@id='upcoming_ico']").ToList();
            foreach (var ico in icolist)
            {
                string name = ico.SelectSingleNode(".//h3/a").InnerText;
                string interest = ico.SelectSingleNode(".//div[@class='interest']").InnerText.Trim('\r', '\t', '\n');
                if(name == "")
                {

                }
                ICODetails temp = AddorUpdateICO(dbfile ,name, interest);
                if(temp.Name != null)
                {
                    result.Add(temp);
                }
            }
            if (result.Count > 0)
            {
                SendNotification(result);
            }
            LogSuccess(result.Count, logfile);
        }

        public ICODetails AddorUpdateICO(string dbfile, string name, string interest)
        {
            ICODetails icodetails = new ICODetails();
            using (SQLiteConnection conn = new SQLiteConnection("data source=" + dbfile))
            {
                conn.Open();
                using (SQLiteCommand cmd = new SQLiteCommand(conn))
                {
                    string selectQuery = "SELECT COUNT(*) FROM ICO WHERE Name = '" + name + "'";
                    cmd.CommandText = selectQuery;
                    int count = Convert.ToInt32(cmd.ExecuteScalar());
                    string query = "";
                    if (count > 0)
                    {
                        selectQuery = "SELECT Rating FROM ICO WHERE Name = '" + name + "'";
                        cmd.CommandText = selectQuery;
                        string oldrating = cmd.ExecuteScalar().ToString();
                        if(oldrating != interest)
                        {
                            query = "UPDATE ICO SET Rating='" + interest + "',UpdatedDate='" + GetTime().ToShortDateString() + "' WHERE Name='" + name + "'";
                            cmd.CommandText = query;
                            cmd.ExecuteNonQuery();
                            icodetails.Name = name;
                            icodetails.NewRating = interest;
                            icodetails.OldRating = oldrating;
                        }
                    }
                    else
                    {
                        query = "INSERT INTO ICO (Name,Rating,UpdatedDate) VALUES ('" + name + "','" + interest + "','" + GetTime().ToShortDateString() + "')";
                        cmd.CommandText = query;
                        cmd.ExecuteNonQuery();
                        icodetails.Name = name;
                        icodetails.NewRating = interest;
                        icodetails.OldRating = interest;
                    }                    
                }
                conn.Clone();
            }
            return icodetails;
        }

        public void SendNotification(List<ICODetails> icos)
        {
            //MailMessage mail = new MailMessage("it61916282@gmail.com", "kalisettisuresh@gmail.com");
            //SmtpClient client = new SmtpClient();
            //client.Port = 587;
            //client.DeliveryMethod = SmtpDeliveryMethod.Network;
            //client.UseDefaultCredentials = false;
            //client.Credentials = new System.Net.NetworkCredential("it61916282@gmail.com", "######");
            //client.EnableSsl = true;
            //client.Host = "smtp.gmail.com";
            //mail.Subject = "Important ICO Update";
            //mail.IsBodyHtml = true;
            string body = "<html><head> <style> table, td, th {     border: 1px solid black; }  table {     border-collapse: collapse;     width: 100%; }  th {     text-align: left; } </style> </head><body><table><tr><th>Name</th><th>Old Rating</th><th>New Rating</th></tr>";
            foreach (var ico in icos)
            {
                body = body + "<tr><td>" + ico.Name + "</td><td>" + ico.OldRating + "</td><td>" + ico.NewRating + "</td></tr>";
            }
            body = body + "</table></body></html>";
            //mail.Body = body;
            //client.Send(mail);
            var apiKey = Config.SendGridAPIKey;
            var client = new SendGridClient(apiKey);
            var msg = new SendGridMessage()
            {
                From = new EmailAddress("it61916282@gmail.com", "Automation"),
                Subject = "Important ICO Update",
                PlainTextContent = " ",
                HtmlContent = body
            };
            msg.AddTo(new EmailAddress("kalisettisuresh@gmail.com", "Suresh"));
            client.SendEmailAsync(msg);
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
                                  [Date] DATE NULL)";
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
