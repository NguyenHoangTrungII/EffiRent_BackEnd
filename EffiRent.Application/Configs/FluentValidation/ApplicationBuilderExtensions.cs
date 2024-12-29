using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System.Text;

namespace EffiAP.Application.Configs.FluentValidation
{
    public static class ApplicationBuilderExtensions
    {
        public static void UseFluentValidationExceptionHandler(this IApplicationBuilder app)
        {
            app.UseExceptionHandler(x =>
            {
                x.Run(async context =>
                {
                    var errorFeature = context.Features.Get<IExceptionHandlerFeature>();
                    var exception = errorFeature!.Error;
                    if (exception is not ValidationException validationException)
                        throw exception;
                    var error =
                        validationException.Errors.Select(err => new
                        {
                            err.ErrorCode,
                            Message = err.ErrorMessage
                        }).FirstOrDefault();
                    var errorText = JsonConvert.SerializeObject(error);
                    context.Response.StatusCode = StatusCodes.Status400BadRequest;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(errorText, Encoding.UTF8);
                });
            });
        }
    }
}
