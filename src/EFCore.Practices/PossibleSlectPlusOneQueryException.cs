using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace EFCore.Practices
{
    public class PossibleSlectPlusOneQueryException 
        : Exception
    {
        private string CommandText { get; }
        private int Count { get; }

        public PossibleSlectPlusOneQueryException(string commandText, int count) :
            base(string.Format($"Query \"{commandText}\" was called {count} times."))
        {
            this.CommandText = commandText;
            this.Count = count;
        }
    }
}
