using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexChat.Data
{
    public class Configuration
    {
        public string idUsuario { get; set; }

        public string nombreUsuario { get; set; } = "Yo";

        public Configuration() 
        {
            idUsuario = Guid.NewGuid().ToString();
        }
    }
}
