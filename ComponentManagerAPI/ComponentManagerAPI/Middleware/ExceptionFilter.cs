using CommonServiceInterfaces;
using Logger;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ComponentManagerAPI.Middleware
{
    public class ApiExceptionFilter : ExceptionFilterAttribute
    {
        public ApiExceptionFilter(
            ICodeContractService codeContractDep,
            IEmailMessenger emailMessengerDep,
            IConfiguration configurationDep,
            ILoggerFactory loggerDep)
        {
            _codeContractDependency = codeContractDep ?? throw new ArgumentNullException("Failed in exception filter constructor. Code contract dependency cannot be null");
            _codeContractDependency.Requires<ArgumentNullException>(emailMessengerDep != null, "Failed in exception filter constructor. Email messenger dependency cannot be null");
            _emailMessengerDependency = emailMessengerDep;
            _codeContractDependency.Requires<ArgumentNullException>(configurationDep != null, "Failed in exception filter constructor. Configuration dependency cannot be null");
            _configuration = configurationDep;
            _codeContractDependency.Requires<ArgumentNullException>(loggerDep != null, "Failed in exception filter constructor. Logger dependency cannot be null");
            _loggerDep = new SimpleFileLogger();

        }

        private readonly ICodeContractService _codeContractDependency;
        private readonly IEmailMessenger _emailMessengerDependency;
        private readonly IConfiguration _configuration;
        private readonly SimpleFileLogger _loggerDep;

        public override void OnException(ExceptionContext context)
        {

            //get exception object
            var ex = context?.Exception;
            //return if no exception or context
            if (ex == null)
                return;

            //init body message 
            string bodyMessage = string.Empty;

            //try to dump information from request body
            var req = context.HttpContext.Request;
            string formContent = req.HasFormContentType ? JsonConvert.SerializeObject(req.Form, Formatting.Indented) : "No form content";

            string requestText =
                "CONTENT TYPE: " + req.ContentType + Environment.NewLine +
                "CONTENT LENGTH: " + req.ContentLength + Environment.NewLine +
                "HAS FORM CONTENT: " + req.HasFormContentType + Environment.NewLine +
                "HOST: " + req.Host + Environment.NewLine +
                "IS HTTPS: " + req.IsHttps + Environment.NewLine +
                "HTTP METHOD: " + req.Method + Environment.NewLine +
                "PROTOCOL: " + req.Protocol + Environment.NewLine +
                "QUERY STRING: " + req.QueryString + Environment.NewLine +
                "HEADERS: " + JsonConvert.SerializeObject(req.Headers, Formatting.Indented) + Environment.NewLine +
                "FORM CONTENT: " + formContent + Environment.NewLine;

            string bodyText = "BODY:";

            using (var reader = new StreamReader(req.Body))
            {
                bodyText += reader.ReadToEnd();

                if (string.IsNullOrEmpty(bodyText))
                    bodyText = "BODY: No http request body information found!";
            }

            requestText += Environment.NewLine + bodyText;

            #region LOGGING

            //attempt log on seperate thread. Fire and forget task
            Task.Run(() =>
             {
                 _loggerDep.LogToAppData(content: context.Exception.Message + Environment.NewLine + context.Exception.StackTrace + Environment.NewLine + requestText);

             }).ContinueWith(failed =>
             {
                //do nothing on failure 
            }, TaskContinuationOptions.OnlyOnFaulted);

            #endregion

            #region EMAIL
            //Fire and forget task. We cant handle an exception caused by the email task so we catch, log, and swallow
            Task.Run(() =>
            {

                string outputLocationMessage = "Logs can be found on machine: " + System.Environment.MachineName + " at the following path: " + _loggerDep.GetLogDirectory();

                //construct formatted exception message to email
                string message = requestText + Environment.NewLine + ExceptionFormatter(ex) + Environment.NewLine + outputLocationMessage;

                //init list of recipient emails
                List<string> recipientEmailAddr =
                _configuration
                .GetValue<string>("ErrorEmailRecipientAddresses")
                .Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(str => str.Trim())
                .ToList();

                //send email message
                _emailMessengerDependency.SendMessage("Component Manager API Error: " + Environment.MachineName, message, recipientEmailAddr);

            })
            //handle email exception task by logging
            .ContinueWith(failed =>
            {
                //if task exception exists
                if (failed.Exception != null)
                {
                    _loggerDep.LogToAppData(content: failed.Exception.Message + Environment.NewLine + failed.Exception.StackTrace);
                }
            }, TaskContinuationOptions.OnlyOnFaulted);

            #endregion

            #region WEB_RESPONSE

            try
            {
                //add fixed error text
                bodyMessage += "A problem occurred while processing the request: ";

                //try to access inner exc message
                string innerExcMessage = ex.InnerException?.Message;

                //if exception was thrown out of a method invoked by reflection and the inner exc is non empty
                if (ex.GetType() == typeof(TargetInvocationException) && !string.IsNullOrEmpty(innerExcMessage))
                {
                    // add inner exc to message
                    bodyMessage += innerExcMessage + Environment.NewLine;
                }
                else
                {
                    //just add main exc message
                    bodyMessage += ex.Message +
                    Environment.NewLine;
                }


                //Write the exception message to the response body as text
                context.Result = new ContentResult()
                {
                    Content = bodyMessage,
                    StatusCode = 500,
                    ContentType = "text/html"
                };

                //let pipeline know exception was handled
                context.ExceptionHandled = true;
            }
            catch (Exception)
            {
                //swallow any exception and return to let framework handle 
                return;
            }

            #endregion

        }

        private string ExceptionFormatter(Exception ex)
        {

            if (ex == null)
                throw new ArgumentNullException("Failed to format exception. Exception object cannot be null");

            string message = "Exception Message: " + ex.Message +
                Environment.NewLine +
                Environment.NewLine +
                "Stack Trace: " + ex.StackTrace +
                Environment.NewLine +
                Environment.NewLine +
                "Exception Type: " + ex.GetType() +
                Environment.NewLine +
                "Target Method: " + ex.TargetSite;


            while (ex.InnerException != null)
            {
                ex = ex.InnerException;

                message +=
                    Environment.NewLine +
                    Environment.NewLine +
                    "Exception Message: " + ex.Message +
                    Environment.NewLine +
                    Environment.NewLine +
                    "Stack Trace: " + ex.StackTrace +
                    Environment.NewLine +
                    Environment.NewLine +
                    "Exception Type: " + ex.GetType() +
                    Environment.NewLine +
                    "Target Method: " + ex.TargetSite;

            }

            return message;

        }

    }
}
