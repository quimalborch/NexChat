using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NexChat.Data
{
    public class Usuario
    {
        public string idUsuario { get; set; }

        public string nombreUsuario { get; set; }

        public Usuario(string nombreUsuario) 
        {
            idUsuario = Guid.NewGuid().ToString();
            this.nombreUsuario = nombreUsuario;
        }
    }
}
