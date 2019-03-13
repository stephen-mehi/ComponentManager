using CommonServiceInterfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ComponentManagerAPI.Services.ErrorHandling
{
    public class CodeContract : ICodeContractService
    {
        /// <summary>
        /// Impose an exception-enforced constraint 
        /// </summary>
        /// <typeparam name="TException">The type of exception to throw if predicate does not hold true</typeparam>
        /// <param name="Predicate">The constraint predicate</param>
        /// <param name="Message">The exception message</param>
        public void Requires<TException>(bool Predicate, string Message)
            where TException : Exception
        {
            if (!Predicate)
            {
                TException ex = (TException)Activator.CreateInstance(typeof(TException), Message);
                throw ex;
            }
        }
    }
}
