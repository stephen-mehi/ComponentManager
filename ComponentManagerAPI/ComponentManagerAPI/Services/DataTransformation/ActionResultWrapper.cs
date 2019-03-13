using CommonServiceInterfaces;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;

namespace ComponentManagerAPI.Services.DataTransformation
{
    public class ActionResultWrapper : IActionResultWrapperService
    {
        public ActionResultWrapper(
            ICodeContractService codeContractDep)
        {
            _codeContractDependency = codeContractDep;
        }

        private readonly ICodeContractService _codeContractDependency;


        public IActionResult GenerateOkActionResult(object resultObject, Controller controller, string viewPath = null)
        {
            //ensure ref to controller supplied
            _codeContractDependency.Requires<ArgumentException>(controller != null, "");
            //_codeContractDependency.Requires<ArgumentException>(resultObject != null, "");

            IActionResult actionResult = null;

            bool isHtml = controller
                .ControllerContext
                .HttpContext
                .Request
                .Headers
                .FirstOrDefault(h => h.Key.Equals("Accept", StringComparison.OrdinalIgnoreCase))
                .Value.Any(v => v.Contains("html"));

            //if action result is not html
            if (!isHtml || string.IsNullOrEmpty(viewPath))
            {
                //allow .net runtime to serialize obj using proper output formatter
                actionResult = controller.Ok(resultObject);
            }
            else
            {
                //return partial
                actionResult = controller.PartialView(viewPath, resultObject);
            }

            return actionResult;
        }

        public IActionResult GenerateOkActionResult()
        {
            return new OkResult();

        }
    }
}
