using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Serilog;

namespace NexChat.Security
{
    /// <summary>
    /// Servicio de criptografía para cifrado E2EE de mensajes
    /// Usa RSA para intercambio de claves y AES-256-GCM para cifrado de mensajes
    /// </summary>
    public class CryptographyService : IDisposable
    {
        private RSA _rsaKeyPair;
        private readonly string _keysFolder;
        private readonly string _publicKeyPath;
        private readonly string _privateKeyPath;
        private bool _disposed = false;

        public CryptographyService()
        {
            Log.Information("?? [CRYPTO] Initializing CryptographyService...");
            
            _keysFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexChat",
                "Keys"
            );

            Log.Debug("?? [CRYPTO] Keys folder: {KeysFolder}", _keysFolder);
            Directory.CreateDirectory(_keysFolder);

            _publicKeyPath = Path.Combine(_keysFolder, "public.key");
            _privateKeyPath = Path.Combine(_keysFolder, "private.key");

            Log.Debug("?? [CRYPTO] Public key path: {PublicKeyPath}", _publicKeyPath);
            Log.Debug("?? [CRYPTO] Private key path: {PrivateKeyPath}", _privateKeyPath);

            _rsaKeyPair = RSA.Create(2048);
            
            LoadOrCreateKeys();
            
            Log.Information("? [CRYPTO] CryptographyService initialized with RSA-2048 key pair");
        }

        /// <summary>
        /// Carga las claves existentes o genera nuevas
        /// </summary>
        private void LoadOrCreateKeys()
        {
            try
            {
                bool privateKeyExists = File.Exists(_privateKeyPath);
                bool publicKeyExists = File.Exists(_publicKeyPath);
                
                Log.Debug("?? [CRYPTO] Private key exists: {PrivateKeyExists}", privateKeyExists);
                Log.Debug("?? [CRYPTO] Public key exists: {PublicKeyExists}", publicKeyExists);
                
                if (privateKeyExists && publicKeyExists)
                {
                    // Cargar claves existentes
                    Log.Information("?? [CRYPTO] Loading existing RSA key pair from disk...");
                    
                    string privateKeyPem = File.ReadAllText(_privateKeyPath);
                    _rsaKeyPair.ImportFromPem(privateKeyPem);
                    
                    // Verificar que la clave se cargó correctamente
                    string publicKeyPem = _rsaKeyPair.ExportRSAPublicKeyPem();
                    string publicKeyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(publicKeyPem))).Substring(0, 16);
                    
                    Log.Information("? [CRYPTO] Loaded existing RSA key pair from disk");
                    Log.Debug("?? [CRYPTO] Public key fingerprint: {Fingerprint}", publicKeyHash);
                }
                else
                {
                    // Generar nuevas claves
                    Log.Warning("?? [CRYPTO] Keys not found or incomplete, generating new key pair");
                    GenerateNewKeys();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "? [CRYPTO] Error loading keys, generating new ones");
                GenerateNewKeys();
            }
        }

        /// <summary>
        /// Genera y guarda un nuevo par de claves RSA
        /// </summary>
        private void GenerateNewKeys()
        {
            Log.Information("?? [CRYPTO] Generating new RSA-2048 key pair...");
            
            _rsaKeyPair = RSA.Create(2048);
            
            // Exportar claves en formato PEM
            string publicKeyPem = _rsaKeyPair.ExportRSAPublicKeyPem();
            string privateKeyPem = _rsaKeyPair.ExportRSAPrivateKeyPem();
            
            Log.Debug("?? [CRYPTO] Public key length: {PublicKeyLength} chars", publicKeyPem.Length);
            Log.Debug("?? [CRYPTO] Private key length: {PrivateKeyLength} chars", privateKeyPem.Length);
            
            // Guardar en archivos con permisos seguros
            File.WriteAllText(_publicKeyPath, publicKeyPem);
            File.WriteAllText(_privateKeyPath, privateKeyPem);
            
            Log.Information("?? [CRYPTO] Keys saved to disk");
            Log.Debug("?? [CRYPTO] Public key: {PublicKeyPath}", _publicKeyPath);
            Log.Debug("?? [CRYPTO] Private key: {PrivateKeyPath}", _privateKeyPath);
            
            // En Windows, establecer permisos restrictivos en la clave privada
            try
            {
                var fileInfo = new FileInfo(_privateKeyPath);
                var fileSecurity = fileInfo.GetAccessControl();
                fileSecurity.SetAccessRuleProtection(true, false);
                fileInfo.SetAccessControl(fileSecurity);
                Log.Debug("?? [CRYPTO] Set restrictive permissions on private key");
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "?? [CRYPTO] Could not set restrictive permissions on private key file");
            }
            
            // Log fingerprint
            string publicKeyHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(publicKeyPem))).Substring(0, 16);
            Log.Information("? [CRYPTO] Generated new RSA-2048 key pair");
            Log.Information("?? [CRYPTO] Public key fingerprint: {Fingerprint}", publicKeyHash);
        }

        /// <summary>
        /// Obtiene la clave pública en formato PEM para compartir
        /// </summary>
        public string GetPublicKeyPem()
        {
            string publicKeyPem = _rsaKeyPair.ExportRSAPublicKeyPem();
            string fingerprint = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(publicKeyPem))).Substring(0, 16);
            
            Log.Debug("?? [CRYPTO] Exporting public key (fingerprint: {Fingerprint})", fingerprint);
            
            return publicKeyPem;
        }

        /// <summary>
        /// Importa una clave pública de otro usuario
        /// </summary>
        public RSA ImportPublicKey(string publicKeyPem)
        {
            try
            {
                Log.Debug("?? [CRYPTO] Importing public key...");
                Log.Debug("?? [CRYPTO] Public key length: {Length} chars", publicKeyPem?.Length ?? 0);
                
                if (string.IsNullOrWhiteSpace(publicKeyPem))
                {
                    Log.Error("? [CRYPTO] Cannot import: public key is null or empty");
                    throw new ArgumentException("Public key is null or empty", nameof(publicKeyPem));
                }
                
                var rsa = RSA.Create();
                rsa.ImportFromPem(publicKeyPem);
                
                string fingerprint = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(publicKeyPem))).Substring(0, 16);
                Log.Information("? [CRYPTO] Public key imported successfully (fingerprint: {Fingerprint})", fingerprint);
                
                return rsa;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "? [CRYPTO] Failed to import public key");
                throw;
            }
        }

        /// <summary>
        /// Cifra un mensaje usando AES-256-GCM (cifrado simétrico rápido)
        /// La clave AES se genera aleatoriamente y se cifra con RSA
        /// </summary>
        public EncryptedMessage EncryptMessage(string plaintext, RSA recipientPublicKey)
        {
            try
            {
                Log.Information("?? [CRYPTO] Starting message encryption...");
                Log.Debug("?? [CRYPTO] Plaintext length: {Length} chars", plaintext?.Length ?? 0);
                
                if (string.IsNullOrEmpty(plaintext))
                {
                    Log.Warning("?? [CRYPTO] Plaintext is empty");
                }
                
                if (recipientPublicKey == null)
                {
                    Log.Error("? [CRYPTO] Recipient public key is null!");
                    throw new ArgumentNullException(nameof(recipientPublicKey));
                }
                
                // Generar clave AES aleatoria de 256 bits
                Log.Debug("?? [CRYPTO] Generating random AES-256 key...");
                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.GenerateKey();
                aes.GenerateIV();

                byte[] key = aes.Key;
                byte[] iv = aes.IV;
                
                Log.Debug("?? [CRYPTO] AES key: {KeySize} bits, IV: {IVSize} bytes", key.Length * 8, iv.Length);

                // Cifrar el mensaje con AES-GCM
                Log.Debug("?? [CRYPTO] Encrypting message with AES-GCM...");
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                byte[] ciphertext;
                byte[] tag;

                using (var gcm = new AesGcm(key))
                {
                    ciphertext = new byte[plaintextBytes.Length];
                    tag = new byte[AesGcm.TagByteSizes.MaxSize];
                    
                    gcm.Encrypt(iv, plaintextBytes, ciphertext, tag);
                }
                
                Log.Debug("?? [CRYPTO] Ciphertext: {Size} bytes, Tag: {TagSize} bytes", ciphertext.Length, tag.Length);

                // Cifrar la clave AES con RSA del destinatario
                Log.Debug("?? [CRYPTO] Encrypting AES key with recipient's RSA public key...");
                byte[] encryptedKey = recipientPublicKey.Encrypt(key, RSAEncryptionPadding.OaepSHA256);
                Log.Debug("?? [CRYPTO] Encrypted key: {Size} bytes", encryptedKey.Length);

                // Crear el mensaje cifrado
                var encrypted = new EncryptedMessage
                {
                    Ciphertext = Convert.ToBase64String(ciphertext),
                    EncryptedKey = Convert.ToBase64String(encryptedKey),
                    IV = Convert.ToBase64String(iv),
                    Tag = Convert.ToBase64String(tag)
                };

                Log.Information("? [CRYPTO] Message encrypted successfully");
                Log.Debug("?? [CRYPTO] Ciphertext (Base64): {Size} chars", encrypted.Ciphertext.Length);
                Log.Debug("?? [CRYPTO] Encrypted key (Base64): {Size} chars", encrypted.EncryptedKey.Length);
                Log.Debug("?? [CRYPTO] IV (Base64): {Size} chars", encrypted.IV.Length);
                Log.Debug("?? [CRYPTO] Tag (Base64): {Size} chars", encrypted.Tag.Length);
                
                return encrypted;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "? [CRYPTO] Error encrypting message");
                throw new CryptographicException("Failed to encrypt message", ex);
            }
        }

        /// <summary>
        /// Descifra un mensaje cifrado
        /// </summary>
        public string DecryptMessage(EncryptedMessage encrypted)
        {
            try
            {
                Log.Information("?? [CRYPTO] Starting message decryption...");
                
                if (encrypted == null)
                {
                    Log.Error("? [CRYPTO] Encrypted message is null!");
                    throw new ArgumentNullException(nameof(encrypted));
                }
                
                Log.Debug("?? [CRYPTO] Ciphertext (Base64): {Size} chars", encrypted.Ciphertext?.Length ?? 0);
                Log.Debug("?? [CRYPTO] Encrypted key (Base64): {Size} chars", encrypted.EncryptedKey?.Length ?? 0);
                Log.Debug("?? [CRYPTO] IV (Base64): {Size} chars", encrypted.IV?.Length ?? 0);
                Log.Debug("?? [CRYPTO] Tag (Base64): {Size} chars", encrypted.Tag?.Length ?? 0);
                
                // Descifrar la clave AES con nuestra clave privada RSA
                Log.Debug("?? [CRYPTO] Decrypting AES key with our RSA private key...");
                byte[] encryptedKey = Convert.FromBase64String(encrypted.EncryptedKey);
                byte[] key = _rsaKeyPair.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);
                Log.Debug("?? [CRYPTO] AES key decrypted: {KeySize} bits", key.Length * 8);

                // Descifrar el mensaje con AES-GCM
                Log.Debug("?? [CRYPTO] Decrypting message with AES-GCM...");
                byte[] ciphertext = Convert.FromBase64String(encrypted.Ciphertext);
                byte[] iv = Convert.FromBase64String(encrypted.IV);
                byte[] tag = Convert.FromBase64String(encrypted.Tag);
                byte[] plaintext = new byte[ciphertext.Length];

                using (var gcm = new AesGcm(key))
                {
                    gcm.Decrypt(iv, ciphertext, tag, plaintext);
                }

                string decryptedText = Encoding.UTF8.GetString(plaintext);
                
                Log.Information("? [CRYPTO] Message decrypted successfully");
                Log.Debug("?? [CRYPTO] Decrypted text length: {Length} chars", decryptedText.Length);
                
                return decryptedText;
            }
            catch (CryptographicException ex)
            {
                Log.Error(ex, "? [CRYPTO] Cryptographic error decrypting message (wrong key or corrupted data?)");
                throw new CryptographicException("Failed to decrypt message - wrong key or corrupted data", ex);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "? [CRYPTO] Error decrypting message");
                throw new CryptographicException("Failed to decrypt message", ex);
            }
        }

        /// <summary>
        /// Firma un mensaje con la clave privada para verificar autenticidad
        /// </summary>
        public string SignMessage(string message)
        {
            try
            {
                Log.Debug("?? [CRYPTO] Signing message...");
                Log.Debug("?? [CRYPTO] Message length: {Length} chars", message?.Length ?? 0);
                
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] signature = _rsaKeyPair.SignData(messageBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                
                string signatureBase64 = Convert.ToBase64String(signature);
                
                Log.Information("? [CRYPTO] Message signed successfully");
                Log.Debug("?? [CRYPTO] Signature (Base64): {Size} chars", signatureBase64.Length);
                
                return signatureBase64;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "? [CRYPTO] Error signing message");
                throw new CryptographicException("Failed to sign message", ex);
            }
        }

        /// <summary>
        /// Verifica la firma de un mensaje
        /// </summary>
        public bool VerifySignature(string message, string signatureBase64, RSA senderPublicKey)
        {
            try
            {
                Log.Debug("?? [CRYPTO] Verifying message signature...");
                Log.Debug("?? [CRYPTO] Message length: {Length} chars", message?.Length ?? 0);
                Log.Debug("?? [CRYPTO] Signature (Base64): {Size} chars", signatureBase64?.Length ?? 0);
                
                if (senderPublicKey == null)
                {
                    Log.Error("? [CRYPTO] Sender public key is null!");
                    return false;
                }
                
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] signature = Convert.FromBase64String(signatureBase64);
                
                bool isValid = senderPublicKey.VerifyData(messageBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                
                if (isValid)
                {
                    Log.Information("? [CRYPTO] Signature verification: VALID");
                }
                else
                {
                    Log.Warning("?? [CRYPTO] Signature verification: INVALID (message may be tampered!)");
                }
                
                return isValid;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "? [CRYPTO] Error verifying signature");
                return false;
            }
        }

        /// <summary>
        /// Genera un hash criptográfico del ID de usuario para privacidad
        /// Usa SHA-256 con salt para prevenir ataques de rainbow table
        /// </summary>
        public static string HashUserId(string userId)
        {
            Log.Debug("?? [CRYPTO] Hashing user ID...");
            
            // Agregar salt único por aplicación
            const string SALT = "NexChat_v1_UserID_Salt_2025";
            string saltedInput = userId + SALT;
            
            byte[] inputBytes = Encoding.UTF8.GetBytes(saltedInput);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            
            string hash = Convert.ToBase64String(hashBytes);
            
            Log.Debug("?? [CRYPTO] User ID hashed: {Original} -> {Hash}", 
                userId.Substring(0, Math.Min(8, userId.Length)) + "...", 
                hash.Substring(0, 16) + "...");
            
            return hash;
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
                _rsaKeyPair?.Dispose();
                Log.Information("?? [CRYPTO] CryptographyService disposed");
            }

            _disposed = true;
        }
    }

    /// <summary>
    /// Representa un mensaje cifrado con todos sus componentes
    /// </summary>
    public class EncryptedMessage
    {
        /// <summary>
        /// Texto cifrado en Base64
        /// </summary>
        public string Ciphertext { get; set; } = string.Empty;

        /// <summary>
        /// Clave AES cifrada con RSA del destinatario en Base64
        /// </summary>
        public string EncryptedKey { get; set; } = string.Empty;

        /// <summary>
        /// Vector de inicialización en Base64
        /// </summary>
        public string IV { get; set; } = string.Empty;

        /// <summary>
        /// Tag de autenticación GCM en Base64
        /// </summary>
        public string Tag { get; set; } = string.Empty;
    }
}
