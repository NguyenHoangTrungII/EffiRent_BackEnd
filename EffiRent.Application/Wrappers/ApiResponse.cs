using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EffiAP.Application.Wrappers
{
    public class ApiResponse<T>
    {
        public ApiResponse() { }
        public ApiResponse(T data, string message = null)
        {
            Succeeded = true;
            Message = message;
            Data = data;
        }
        public ApiResponse(string message, bool isSucess = false)
        {
            Succeeded = isSucess;
            Message = message;
        }

        public ApiResponse(IEnumerable<IdentityError> errors)
        {
            Succeeded = false;
            Message = "An error occurred while processing your request.";
            Errors = errors.Select(e => $"{e.Code}: {e.Description}").ToList();
        }

        public bool Succeeded { get; set; }
        public string Message { get; set; }
        public List<string> Errors { get; set; }
        public T Data { get; set; }
    }
}
