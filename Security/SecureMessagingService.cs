using NexChat.Data;
using Serilog;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace NexChat.Security
{
    /// <summary>
    /// Servicio de mensajería segura con cifrado E2EE
    /// Maneja el cifrado, descifrado, firma y verificación de mensajes
    /// </summary>
    public class SecureMessagingService : IDisposable
    {
        private readonly CryptographyService _cryptoService;
        private readonly PublicKeyManager _publicKeyManager;
        private bool _disposed = false;

        public SecureMessagingService()
        {
            _cryptoService = new CryptographyService();
            _publicKeyManager = new PublicKeyManager();
            
            Log.Information("SecureMessagingService initialized");
        }

        /// <summary>
        /// Obtiene la clave pública del usuario actual para compartir
        /// </summary>
        public string GetMyPublicKey()
        {
            return _cryptoService.GetPublicKeyPem();
        }

        /// <summary>
        /// Registra la clave pública de otro usuario
        /// </summary>
        public void RegisterUserPublicKey(string userIdHash, string publicKeyPem, string? displayName = null)
        {
            _publicKeyManager.AddOrUpdatePublicKey(userIdHash, publicKeyPem, displayName);
            Log.Information("Registered public key for user: {DisplayName}", displayName ?? userIdHash.Substring(0, 8) + "...");
        }

        /// <summary>
        /// Cifra un mensaje para enviarlo de forma segura
        /// </summary>
        public Message EncryptMessage(Message message, string recipientUserIdHash)
        {
            try
            {
                // Verificar que tenemos la clave pública del destinatario
                if (!_publicKeyManager.HasPublicKey(recipientUserIdHash))
                {
                    Log.Warning("Cannot encrypt message: recipient public key not found for {UserIdHash}", 
                        recipientUserIdHash.Substring(0, 8) + "...");
                    throw new InvalidOperationException($"Recipient public key not found. Cannot send encrypted message.");
                }

                // Obtener la clave pública del destinatario
                using var recipientPublicKey = _publicKeyManager.GetPublicKey(recipientUserIdHash);
                if (recipientPublicKey == null)
                {
                    throw new InvalidOperationException("Failed to load recipient public key");
                }

                // Cifrar el contenido del mensaje
                var encrypted = _cryptoService.EncryptMessage(message.Content, recipientPublicKey);

                // Firmar el mensaje para verificar autenticidad
                string signature = _cryptoService.SignMessage(message.Content);

                // Crear una copia del mensaje con datos cifrados
                var encryptedMessage = new Message
                {
                    Id = message.Id,
                    Sender = message.Sender,
                    Timestamp = message.Timestamp,
                    Chat = message.Chat,
                    
                    // Datos de cifrado
                    IsEncrypted = true,
                    Content = "[Encrypted]", // Placeholder para UI
                    EncryptedContent = encrypted.Ciphertext,
                    EncryptedKey = encrypted.EncryptedKey,
                    IV = encrypted.IV,
                    AuthTag = encrypted.Tag,
                    
                    // Firma y clave pública del remitente
                    Signature = signature,
                    SenderPublicKey = _cryptoService.GetPublicKeyPem()
                };

                Log.Information("Message encrypted successfully for recipient {UserIdHash}", 
                    recipientUserIdHash.Substring(0, 8) + "...");

                return encryptedMessage;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error encrypting message");
                throw new CryptographicException("Failed to encrypt message", ex);
            }
        }

        /// <summary>
        /// Descifra un mensaje recibido
        /// </summary>
        public Message DecryptMessage(Message encryptedMessage)
        {
            try
            {
                // Verificar que el mensaje está cifrado
                if (!encryptedMessage.IsEncrypted)
                {
                    Log.Debug("Message is not encrypted, returning as-is");
                    return encryptedMessage;
                }

                // Verificar que tenemos todos los datos necesarios
                if (string.IsNullOrEmpty(encryptedMessage.EncryptedContent) ||
                    string.IsNullOrEmpty(encryptedMessage.EncryptedKey) ||
                    string.IsNullOrEmpty(encryptedMessage.IV) ||
                    string.IsNullOrEmpty(encryptedMessage.AuthTag))
                {
                    throw new InvalidOperationException("Encrypted message is missing required data");
                }

                // Descifrar el contenido
                var encrypted = new EncryptedMessage
                {
                    Ciphertext = encryptedMessage.EncryptedContent,
                    EncryptedKey = encryptedMessage.EncryptedKey,
                    IV = encryptedMessage.IV,
                    Tag = encryptedMessage.AuthTag
                };

                string decryptedContent = _cryptoService.DecryptMessage(encrypted);

                // Verificar la firma si está presente
                if (!string.IsNullOrEmpty(encryptedMessage.Signature) && 
                    !string.IsNullOrEmpty(encryptedMessage.SenderPublicKey))
                {
                    bool isSignatureValid = VerifyMessageSignature(
                        decryptedContent, 
                        encryptedMessage.Signature, 
                        encryptedMessage.SenderPublicKey
                    );

                    if (!isSignatureValid)
                    {
                        Log.Warning("Message signature verification failed! Message may have been tampered with.");
                        // Podrías decidir rechazar el mensaje aquí
                        // throw new CryptographicException("Message signature is invalid");
                    }
                    else
                    {
                        Log.Debug("Message signature verified successfully");
                    }
                }

                // Crear una copia del mensaje con contenido descifrado
                var decryptedMessage = new Message
                {
                    Id = encryptedMessage.Id,
                    Sender = encryptedMessage.Sender,
                    Timestamp = encryptedMessage.Timestamp,
                    Chat = encryptedMessage.Chat,
                    Content = decryptedContent,
                    IsEncrypted = false,
                    Signature = encryptedMessage.Signature,
                    SenderPublicKey = encryptedMessage.SenderPublicKey
                };

                Log.Information("Message decrypted successfully");
                return decryptedMessage;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error decrypting message");
                throw new CryptographicException("Failed to decrypt message. You may not have the correct key.", ex);
            }
        }

        /// <summary>
        /// Verifica la firma digital de un mensaje
        /// </summary>
        private bool VerifyMessageSignature(string message, string signature, string senderPublicKeyPem)
        {
            try
            {
                using var senderPublicKey = _cryptoService.ImportPublicKey(senderPublicKeyPem);
                return _cryptoService.VerifySignature(message, signature, senderPublicKey);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error verifying message signature");
                return false;
            }
        }

        /// <summary>
        /// Verifica si podemos cifrar mensajes para un usuario
        /// </summary>
        public bool CanEncryptFor(string userIdHash)
        {
            return _publicKeyManager.HasPublicKey(userIdHash);
        }

        /// <summary>
        /// Intercambia claves públicas con otro usuario
        /// Esta información debe enviarse de forma segura (ej: a través de un canal HTTPS)
        /// </summary>
        public PublicKeyExchange CreatePublicKeyExchange(string myUserIdHash, string myDisplayName)
        {
            return new PublicKeyExchange
            {
                UserIdHash = myUserIdHash,
                DisplayName = myDisplayName,
                PublicKeyPem = _cryptoService.GetPublicKeyPem(),
                Timestamp = DateTime.UtcNow
            };
        }

        /// <summary>
        /// Procesa un intercambio de claves públicas recibido
        /// </summary>
        public void ProcessPublicKeyExchange(PublicKeyExchange exchange)
        {
            RegisterUserPublicKey(exchange.UserIdHash, exchange.PublicKeyPem, exchange.DisplayName);
            Log.Information("Processed public key exchange from {DisplayName}", exchange.DisplayName);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                _cryptoService?.Dispose();
                Log.Information("SecureMessagingService disposed");
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Representa un intercambio de claves públicas entre usuarios
    /// </summary>
    public class PublicKeyExchange
    {
        public string UserIdHash { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string PublicKeyPem { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
    }
}
