using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ChatProgram
{
    internal class ChatClient
    {
        public string IPAddress { get; set; }
        public List<Guid> MessageIds { get; set; } = new List<Guid>();
    }
}
