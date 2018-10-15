using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SendGrid;
using SendGrid.Helpers.Mail;
using ConfigurationProvider;

namespace NotificationHelper
{
    public class NotificationHelper
    {
        public void TestMail()
        {
            SendSampleNotification();
        }

        public void SendSampleNotification()
        {
            string body = "<html><head></head><body>Sample Notification</body>";
            var apiKey = Config.SendGridAPIKey;
            var client = new SendGridClient(apiKey);
            var msg = new SendGridMessage()
            {
                From = new EmailAddress("it61916282@gmail.com", "Automation"),
                Subject = "Sample Notification",
                PlainTextContent = " ",
                HtmlContent = body
            };
            msg.AddTo(new EmailAddress("kalisettisuresh@gmail.com", "Suresh"));
            client.SendEmailAsync(msg);
        }
    }
}
