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
        public PaletaColoresSeleccionada paletaColoresSeleccionada { get; set; } = PaletaColoresSeleccionada.Automatico;

        public Configuration() 
        {
            idUsuario = Guid.NewGuid().ToString();
        }

        public enum PaletaColoresSeleccionada
        {
            Automatico,
            Oscuro,
            Claro,
            Rojo,
            Verde,
            Morado
        }
    }
}
