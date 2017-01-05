using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EFCore.Practices
{
    public class NotVerifiedException 
        : Exception
    {
        public NotVerifiedException(string message, Exception innerException) 
            : base(message, innerException)
        {
        }
    }
}
