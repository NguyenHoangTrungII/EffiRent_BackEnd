using EffiAP.Application.Wrappers;
using EffiRent.Domain.ViewModels.Users;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using MailKit.Security;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Email
{
    public class EmailService : IEmailService
    {
        private readonly MailSetting _mailSettings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<MailSetting> mailSettings, ILogger<EmailService> logger)
        {
            _mailSettings = mailSettings.Value;
            _logger = logger;
            _logger.LogInformation("Create EmailService");
        }

        public async Task<ApiResponse<bool>> SendMail(EmailContent mailContent)
        {
            var email = new MimeMessage();
            email.Sender = new MailboxAddress(_mailSettings.DisplayName, _mailSettings.Mail);
            email.From.Add(new MailboxAddress(_mailSettings.DisplayName, _mailSettings.Mail));
            email.To.Add(MailboxAddress.Parse(mailContent.To));
            email.Subject = mailContent.Subject;

            var builder = new BodyBuilder
            {
                HtmlBody = mailContent.Body
            };
            email.Body = builder.ToMessageBody();

            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            try
            {
                smtp.Connect(_mailSettings.Host, _mailSettings.Port, SecureSocketOptions.StartTls);
                smtp.Authenticate(_mailSettings.Mail, _mailSettings.Password);
                await smtp.SendAsync(email);
            }
            catch (Exception ex)
            {
                // Ghi log lỗi và lưu email vào thư mục "mailssave" nếu gửi thất bại
                System.IO.Directory.CreateDirectory("mailssave");
                var emailsavefile = string.Format(@"mailssave/{0}.eml", Guid.NewGuid());
                await email.WriteToAsync(emailsavefile);

                _logger.LogInformation("Error email at- " + emailsavefile);
                _logger.LogError(ex.Message);

                return new ApiResponse<bool>(message: ex.Message);
            }

            smtp.Disconnect(true);
            _logger.LogInformation("Send mail to " + mailContent.To);
            return new ApiResponse<bool>( message: "Send mail to " + mailContent.To + " success", true);
        }

        public async Task SendEmailAsync(string toEmail, string subject, string htmlMessage)
        {
            await SendMail(new EmailContent()
            {
                To = toEmail,
                Subject = subject,
                Body = htmlMessage
            });
        }
    }
}
