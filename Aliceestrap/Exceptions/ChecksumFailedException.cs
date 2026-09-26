using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Aliceestrap.Exceptions
{
    internal class ChecksumFailedException : Exception
    {
        public ChecksumFailedException(string message) : base(message) 
        { 
        }
    }
}
