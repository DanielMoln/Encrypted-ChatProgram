using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatProgram
{
    internal class ClientMessage
    {
        public string Message { get; set; }
        public Guid Id { get; set; } = Guid.NewGuid();
    }
}
