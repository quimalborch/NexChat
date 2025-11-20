using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexChat.Data
{
    public class Sender
    {
        public string Id { get; set; }
        public string? Name { get; set; }

        public Sender() { }

        public Sender(string _Id) 
        {
            Id = _Id;
        }
    }
}
