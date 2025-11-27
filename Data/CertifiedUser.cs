using System;

namespace NexChat.Data
{
    public class CertifiedUser
    {
        public string Id { get; set; }
        public string Nombre { get; set; }
        public string Llave { get; set; }
        public DateTime FechaCreacion { get; set; }

        public CertifiedUser()
        {
            Id = Guid.NewGuid().ToString();
            FechaCreacion = DateTime.Now;
            Nombre = string.Empty;
            Llave = string.Empty;
        }

        public CertifiedUser(string nombre, string llave)
        {
            Id = Guid.NewGuid().ToString();
            Nombre = nombre;
            Llave = llave;
            FechaCreacion = DateTime.Now;
        }
    }
}
