using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace NexChat.Data
{
    public class Message
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        [JsonIgnore]
        public Chat Chat { get; set; }
        public Sender Sender { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Indica si el mensaje está cifrado
        /// </summary>
        public bool IsEncrypted { get; set; } = false;

        /// <summary>
        /// Texto cifrado en Base64 (si IsEncrypted = true)
        /// </summary>
        public string? EncryptedContent { get; set; }

        /// <summary>
        /// Clave AES cifrada con RSA en Base64
        /// </summary>
        public string? EncryptedKey { get; set; }

        /// <summary>
        /// Vector de inicialización en Base64
        /// </summary>
        public string? IV { get; set; }

        /// <summary>
        /// Tag de autenticación GCM en Base64
        /// </summary>
        public string? AuthTag { get; set; }

        /// <summary>
        /// Firma digital del mensaje para verificar autenticidad
        /// </summary>
        public string? Signature { get; set; }

        /// <summary>
        /// Clave pública RSA del remitente en formato PEM
        /// </summary>
        public string? SenderPublicKey { get; set; }

        public Message()
        {
        }

        public Message(Chat _Chat, Sender _Sender, string _Content)
        {
            Chat = _Chat;
            Sender = _Sender;
            Content = _Content;
        }
    }
}
