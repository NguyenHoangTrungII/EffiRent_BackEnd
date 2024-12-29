using EffiAP.Application.Wrappers;
using EffiAP.Domain.SeedWork;
using EffiRent.Domain.ViewModels.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiRent.Application.Services.Email
{
    public interface IEmailService : IScopedService
    {
        // Phương thức gửi email
        Task<ApiResponse<bool>> SendMail(EmailContent mailContent) ;

        // Phương thức gửi email với các tham số đơn giản
        Task SendEmailAsync(string toEmail, string subject, string htmlMessage);
    }
}
