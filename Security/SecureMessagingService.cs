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
            Log.Information("?? [E2EE] Initializing SecureMessagingService...");
            
            _cryptoService = new CryptographyService();
            _publicKeyManager = new PublicKeyManager();
            
            Log.Information("? [E2EE] SecureMessagingService initialized");
        }

        /// <summary>
        /// Obtiene la clave pública del usuario actual para compartir
        /// </summary>
        public string GetMyPublicKey()
        {
            Log.Debug("?? [E2EE] Getting my public key for sharing");
            string publicKey = _cryptoService.GetPublicKeyPem();
            Log.Debug("?? [E2EE] My public key: {Length} chars", publicKey.Length);
            return publicKey;
        }

        /// <summary>
        /// Registra la clave pública de otro usuario
        /// </summary>
        public void RegisterUserPublicKey(string userIdHash, string publicKeyPem, string? displayName = null)
        {
            Log.Information("?? [E2EE] Registering public key for user: {DisplayName} ({UserIdHash})", 
                displayName ?? "Unknown", 
                userIdHash.Substring(0, Math.Min(16, userIdHash.Length)) + "...");
            
            Log.Debug("?? [E2EE] Public key length: {Length} chars", publicKeyPem?.Length ?? 0);
            
            if (string.IsNullOrWhiteSpace(publicKeyPem))
            {
                Log.Error("? [E2EE] Cannot register: public key is null or empty!");
                return;
            }
            
            try
            {
                _publicKeyManager.AddOrUpdatePublicKey(userIdHash, publicKeyPem, displayName);
                Log.Information("? [E2EE] Registered public key for user: {DisplayName}", displayName ?? userIdHash.Substring(0, 8) + "...");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "? [E2EE] Failed to register public key for {DisplayName}", displayName ?? "Unknown");
                throw;
            }
        }

        /// <summary>
        /// Cifra un mensaje para enviarlo de forma segura
        /// </summary>
        public Message EncryptMessage(Message message, string recipientUserIdHash)
        {
            try
            {
                Log.Information("?? [E2EE] Starting message encryption...");
                Log.Debug("?? [E2EE] Message ID: {MessageId}", message.Id);
                Log.Debug("?? [E2EE] Sender: {Sender}", message.Sender?.Name ?? "Unknown");
                Log.Debug("?? [E2EE] Content length: {Length} chars", message.Content?.Length ?? 0);
                Log.Debug("?? [E2EE] Recipient user ID hash: {UserIdHash}", 
                    recipientUserIdHash.Substring(0, Math.Min(16, recipientUserIdHash.Length)) + "...");
                
                // Verificar que tenemos la clave pública del destinatario
                Log.Debug("?? [E2EE] Checking if recipient public key is available...");
                
                if (!_publicKeyManager.HasPublicKey(recipientUserIdHash))
                {
                    Log.Error("? [E2EE] Cannot encrypt message: recipient public key NOT FOUND for {UserIdHash}", 
                        recipientUserIdHash.Substring(0, Math.Min(16, recipientUserIdHash.Length)) + "...");
                    
                    // Log all registered public keys for debugging
                    var allKeys = _publicKeyManager.GetAllPublicKeys();
                    Log.Debug("?? [E2EE] Currently registered public keys: {Count}", allKeys.Count);
                    foreach (var key in allKeys)
                    {
                        Log.Debug("  - {UserIdHash}: {DisplayName}", 
                            key.Key.Substring(0, Math.Min(16, key.Key.Length)) + "...", 
                            key.Value.DisplayName ?? "Unknown");
                    }
                    
                    throw new InvalidOperationException($"Recipient public key not found. Cannot send encrypted message.");
                }
                
                Log.Information("? [E2EE] Recipient public key found!");

                // Obtener la clave pública del destinatario
                Log.Debug("?? [E2EE] Loading recipient public key...");
                using var recipientPublicKey = _publicKeyManager.GetPublicKey(recipientUserIdHash);
                
                if (recipientPublicKey == null)
                {
                    Log.Error("? [E2EE] Failed to load recipient public key (null returned)");
                    throw new InvalidOperationException("Failed to load recipient public key");
                }
                
                Log.Information("? [E2EE] Recipient public key loaded successfully");

                // Cifrar el contenido del mensaje
                Log.Debug("?? [E2EE] Encrypting message content...");
                var encrypted = _cryptoService.EncryptMessage(message.Content, recipientPublicKey);

                // Firmar el mensaje para verificar autenticidad
                Log.Debug("?? [E2EE] Signing message...");
                string signature = _cryptoService.SignMessage(message.Content);

                // Obtener clave pública del remitente
                Log.Debug("?? [E2EE] Getting sender public key...");
                string senderPublicKey = _cryptoService.GetPublicKeyPem();

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
                    SenderPublicKey = senderPublicKey
                };

                Log.Information("? [E2EE] Message encrypted successfully for recipient {UserIdHash}", 
                    recipientUserIdHash.Substring(0, Math.Min(16, recipientUserIdHash.Length)) + "...");
                
                Log.Debug("?? [E2EE] Encrypted message details:");
                Log.Debug("  - IsEncrypted: {IsEncrypted}", encryptedMessage.IsEncrypted);
                Log.Debug("  - EncryptedContent length: {Length}", encryptedMessage.EncryptedContent?.Length ?? 0);
                Log.Debug("  - EncryptedKey length: {Length}", encryptedMessage.EncryptedKey?.Length ?? 0);
                Log.Debug("  - IV length: {Length}", encryptedMessage.IV?.Length ?? 0);
                Log.Debug("  - AuthTag length: {Length}", encryptedMessage.AuthTag?.Length ?? 0);
                Log.Debug("  - Signature length: {Length}", encryptedMessage.Signature?.Length ?? 0);

                return encryptedMessage;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "? [E2EE] Error encrypting message");
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
                Log.Information("?? [E2EE] Starting message decryption...");
                Log.Debug("?? [E2EE] Message ID: {MessageId}", encryptedMessage.Id);
                
                // Verificar que el mensaje está cifrado
                if (!encryptedMessage.IsEncrypted)
                {
                    Log.Debug("?? [E2EE] Message is not encrypted, returning as-is");
                    return encryptedMessage;
                }
                
                Log.Debug("?? [E2EE] Message is encrypted, checking required data...");

                // Verificar que tenemos todos los datos necesarios
                if (string.IsNullOrEmpty(encryptedMessage.EncryptedContent) ||
                    string.IsNullOrEmpty(encryptedMessage.EncryptedKey) ||
                    string.IsNullOrEmpty(encryptedMessage.IV) ||
                    string.IsNullOrEmpty(encryptedMessage.AuthTag))
                {
                    Log.Error("? [E2EE] Encrypted message is missing required data!");
                    Log.Debug("  - EncryptedContent: {Present}", !string.IsNullOrEmpty(encryptedMessage.EncryptedContent));
                    Log.Debug("  - EncryptedKey: {Present}", !string.IsNullOrEmpty(encryptedMessage.EncryptedKey));
                    Log.Debug("  - IV: {Present}", !string.IsNullOrEmpty(encryptedMessage.IV));
                    Log.Debug("  - AuthTag: {Present}", !string.IsNullOrEmpty(encryptedMessage.AuthTag));
                    
                    throw new InvalidOperationException("Encrypted message is missing required data");
                }
                
                Log.Information("? [E2EE] All encryption data present");

                // Descifrar el contenido
                Log.Debug("?? [E2EE] Decrypting message content...");
                var encrypted = new EncryptedMessage
                {
                    Ciphertext = encryptedMessage.EncryptedContent,
                    EncryptedKey = encryptedMessage.EncryptedKey,
                    IV = encryptedMessage.IV,
                    Tag = encryptedMessage.AuthTag
                };

                string decryptedContent = _cryptoService.DecryptMessage(encrypted);
                Log.Information("? [E2EE] Message decrypted successfully");
                Log.Debug("?? [E2EE] Decrypted content length: {Length} chars", decryptedContent.Length);

                // Verificar la firma si está presente
                if (!string.IsNullOrEmpty(encryptedMessage.Signature) && 
                    !string.IsNullOrEmpty(encryptedMessage.SenderPublicKey))
                {
                    Log.Debug("?? [E2EE] Verifying message signature...");
                    
                    bool isSignatureValid = VerifyMessageSignature(
                        decryptedContent, 
                        encryptedMessage.Signature, 
                        encryptedMessage.SenderPublicKey
                    );

                    if (!isSignatureValid)
                    {
                        Log.Warning("?? [E2EE] Message signature verification FAILED! Message may have been tampered with.");
                        // Podrías decidir rechazar el mensaje aquí
                        // throw new CryptographicException("Message signature is invalid");
                    }
                    else
                    {
                        Log.Information("? [E2EE] Message signature verified successfully");
                    }
                }
                else
                {
                    Log.Warning("?? [E2EE] No signature present, cannot verify message authenticity");
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

                Log.Information("? [E2EE] Message decryption completed successfully");
                return decryptedMessage;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "? [E2EE] Error decrypting message");
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
                Log.Debug("?? [E2EE] Verifying signature with sender's public key...");
                
                using var senderPublicKey = _cryptoService.ImportPublicKey(senderPublicKeyPem);
                bool isValid = _cryptoService.VerifySignature(message, signature, senderPublicKey);
                
                return isValid;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "? [E2EE] Error verifying message signature");
                return false;
            }
        }

        /// <summary>
        /// Verifica si podemos cifrar mensajes para un usuario
        /// </summary>
        public bool CanEncryptFor(string userIdHash)
        {
            bool hasKey = _publicKeyManager.HasPublicKey(userIdHash);
            
            Log.Debug("?? [E2EE] Can encrypt for {UserIdHash}? {CanEncrypt}", 
                userIdHash.Substring(0, Math.Min(16, userIdHash.Length)) + "...", 
                hasKey);
            
            return hasKey;
        }

        /// <summary>
        /// Intercambia claves públicas con otro usuario
        /// Esta información debe enviarse de forma segura (ej: a través de un canal HTTPS)
        /// </summary>
        public PublicKeyExchange CreatePublicKeyExchange(string myUserIdHash, string myDisplayName)
        {
            Log.Information("?? [E2EE] Creating public key exchange...");
            Log.Debug("?? [E2EE] My user ID hash: {UserIdHash}", 
                myUserIdHash.Substring(0, Math.Min(16, myUserIdHash.Length)) + "...");
            Log.Debug("?? [E2EE] My display name: {DisplayName}", myDisplayName);
            
            string publicKeyPem = _cryptoService.GetPublicKeyPem();
            
            var exchange = new PublicKeyExchange
            {
                UserIdHash = myUserIdHash,
                DisplayName = myDisplayName,
                PublicKeyPem = publicKeyPem,
                Timestamp = DateTime.UtcNow
            };
            
            Log.Information("? [E2EE] Public key exchange created");
            Log.Debug("?? [E2EE] Exchange data size: {Size} chars", publicKeyPem.Length);
            
            return exchange;
        }

        /// <summary>
        /// Procesa un intercambio de claves públicas recibido
        /// </summary>
        public void ProcessPublicKeyExchange(PublicKeyExchange exchange)
        {
            Log.Information("?? [E2EE] Processing public key exchange...");
            Log.Debug("?? [E2EE] From: {DisplayName} ({UserIdHash})", 
                exchange.DisplayName, 
                exchange.UserIdHash.Substring(0, Math.Min(16, exchange.UserIdHash.Length)) + "...");
            Log.Debug("?? [E2EE] Timestamp: {Timestamp}", exchange.Timestamp);
            Log.Debug("?? [E2EE] Public key length: {Length} chars", exchange.PublicKeyPem?.Length ?? 0);
            
            if (string.IsNullOrWhiteSpace(exchange.PublicKeyPem))
            {
                Log.Error("? [E2EE] Cannot process exchange: public key is null or empty!");
                return;
            }
            
            RegisterUserPublicKey(exchange.UserIdHash, exchange.PublicKeyPem, exchange.DisplayName);
            Log.Information("? [E2EE] Processed public key exchange from {DisplayName}", exchange.DisplayName);
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
                Log.Information("?? [E2EE] SecureMessagingService disposed");
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
