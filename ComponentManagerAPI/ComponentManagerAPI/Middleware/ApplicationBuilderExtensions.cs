using ComponentManagerAPI.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using System.IO;
using System.Threading.Tasks;



//EXAMPLE OF MIDDLEWARE IMPLEMENTATION

namespace Microsoft.AspNetCore.Builder
{
    public static class MiddlewareExtensions
    {

        public static IApplicationBuilder UseComponentBinding(this IApplicationBuilder app)
        {
            return app.UseMiddleware<ComponentBindingMiddleware>();
        }

        public static IApplicationBuilder UseCustomAuthorization(this IApplicationBuilder app)
        {
            return app.UseMiddleware<CustomAuthorizationMiddleware>();
        }

        public static IApplicationBuilder UseCustomErrorHandler(this IApplicationBuilder app)
        {
            return app.UseMiddleware<CustomExceptionHandlerMiddleware>();
        }

    }

}

namespace ComponentManagerAPI.Middleware
{


    using System;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.Extensions.Logging;
    using System.Text;
    using System.Net;
    using Microsoft.Net.Http.Headers;

    public sealed class CustomExceptionHandlerMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger _logger;

        public CustomExceptionHandlerMiddleware(
            RequestDelegate next,
            ILoggerFactory loggerFactory)
        {
            _next = next;
            _logger = loggerFactory.
                    CreateLogger<CustomExceptionHandlerMiddleware>();
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (Exception ex)
            {

                try
                {
                    context.Response.Clear();
                    //write message to body async
                    await context.Response.WriteAsync(ex.Message, Encoding.UTF8);
                    context.Response.StatusCode = 500;
                    context.Response.ContentType = new MediaTypeHeaderValue("text/html").ToString();

                    return;
                    // if you don't want to rethrow the original exception
                    // then call return:
                    //return;


                }
                catch (Exception ex2)
                {
                    _logger.LogError(
                        0, ex2,
                        "An exception was thrown attempting to execute the error handler middleware.");

                    throw;
                }

                throw;

            }
        }

    }

    public sealed class CustomAuthorizationMiddleware
    {

        private readonly RequestDelegate _nextReferenceDependency;
        private readonly IConfiguration _configuration;

        public CustomAuthorizationMiddleware(
            RequestDelegate nextReferenceDependency,
            IConfiguration config)
        {
            _nextReferenceDependency = nextReferenceDependency;
            _configuration = config;
        }

        public async Task Invoke(HttpContext context)
        {
            //get req role from config
            var windowsGroup = _configuration.GetValue<string>("WindowsGroup");

            //if not authenticated or not in role
            if (!context.User.IsInRole(windowsGroup))
            {
                context.Response.StatusCode = 401;
                return;
            }

            await _nextReferenceDependency.Invoke(context);
        }
    }


    public sealed class ComponentBindingMiddleware
    {

        private readonly RequestDelegate _nextReferenceDependency;

        public ComponentBindingMiddleware(RequestDelegate nextReferenceDependency)
        {
            _nextReferenceDependency = nextReferenceDependency;
        }

        public async Task Invoke(
            HttpContext context)
        {

            //EXAMPLE OF READING FROM REQUEST BODY AND RESETTING STREAM

            //init string for body text
            string requestBodyText;

            //init local mem stream
            using (var requestBodyStream = new MemoryStream())
            {
                //copy body stream to local mem stream
                await context.Request.Body.CopyToAsync(requestBodyStream);
                //reset original stream reading position following the copy so downstream things can read it
                context.Request.Body.Seek(0, SeekOrigin.Begin);
                //set the local stream reading position to beginning
                requestBodyStream.Seek(0, SeekOrigin.Begin);
                //read text from body
                requestBodyText = new StreamReader(requestBodyStream).ReadToEnd();
            }

            // Call the next delegate/middleware in the pipeline
            await _nextReferenceDependency(context);
        }
    }
}