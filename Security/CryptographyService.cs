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
            _keysFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexChat",
                "Keys"
            );

            Directory.CreateDirectory(_keysFolder);

            _publicKeyPath = Path.Combine(_keysFolder, "public.key");
            _privateKeyPath = Path.Combine(_keysFolder, "private.key");

            _rsaKeyPair = RSA.Create(2048);
            
            LoadOrCreateKeys();
            
            Log.Information("CryptographyService initialized with RSA-2048 key pair");
        }

        /// <summary>
        /// Carga las claves existentes o genera nuevas
        /// </summary>
        private void LoadOrCreateKeys()
        {
            try
            {
                if (File.Exists(_privateKeyPath) && File.Exists(_publicKeyPath))
                {
                    // Cargar claves existentes
                    string privateKeyPem = File.ReadAllText(_privateKeyPath);
                    _rsaKeyPair.ImportFromPem(privateKeyPem);
                    
                    Log.Information("Loaded existing RSA key pair from disk");
                }
                else
                {
                    // Generar nuevas claves
                    GenerateNewKeys();
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error loading keys, generating new ones");
                GenerateNewKeys();
            }
        }

        /// <summary>
        /// Genera y guarda un nuevo par de claves RSA
        /// </summary>
        private void GenerateNewKeys()
        {
            _rsaKeyPair = RSA.Create(2048);
            
            // Exportar claves en formato PEM
            string publicKeyPem = _rsaKeyPair.ExportRSAPublicKeyPem();
            string privateKeyPem = _rsaKeyPair.ExportRSAPrivateKeyPem();
            
            // Guardar en archivos con permisos seguros
            File.WriteAllText(_publicKeyPath, publicKeyPem);
            File.WriteAllText(_privateKeyPath, privateKeyPem);
            
            // En Windows, establecer permisos restrictivos en la clave privada
            try
            {
                var fileInfo = new FileInfo(_privateKeyPath);
                var fileSecurity = fileInfo.GetAccessControl();
                fileSecurity.SetAccessRuleProtection(true, false);
                fileInfo.SetAccessControl(fileSecurity);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Could not set restrictive permissions on private key file");
            }
            
            Log.Information("Generated new RSA-2048 key pair and saved to disk");
        }

        /// <summary>
        /// Obtiene la clave pública en formato PEM para compartir
        /// </summary>
        public string GetPublicKeyPem()
        {
            return _rsaKeyPair.ExportRSAPublicKeyPem();
        }

        /// <summary>
        /// Importa una clave pública de otro usuario
        /// </summary>
        public RSA ImportPublicKey(string publicKeyPem)
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(publicKeyPem);
            return rsa;
        }

        /// <summary>
        /// Cifra un mensaje usando AES-256-GCM (cifrado simétrico rápido)
        /// La clave AES se genera aleatoriamente y se cifra con RSA
        /// </summary>
        public EncryptedMessage EncryptMessage(string plaintext, RSA recipientPublicKey)
        {
            try
            {
                // Generar clave AES aleatoria de 256 bits
                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.GenerateKey();
                aes.GenerateIV();

                byte[] key = aes.Key;
                byte[] iv = aes.IV;

                // Cifrar el mensaje con AES-GCM
                byte[] plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                byte[] ciphertext;
                byte[] tag;

                using (var gcm = new AesGcm(key))
                {
                    ciphertext = new byte[plaintextBytes.Length];
                    tag = new byte[AesGcm.TagByteSizes.MaxSize];
                    
                    gcm.Encrypt(iv, plaintextBytes, ciphertext, tag);
                }

                // Cifrar la clave AES con RSA del destinatario
                byte[] encryptedKey = recipientPublicKey.Encrypt(key, RSAEncryptionPadding.OaepSHA256);

                // Crear el mensaje cifrado
                var encrypted = new EncryptedMessage
                {
                    Ciphertext = Convert.ToBase64String(ciphertext),
                    EncryptedKey = Convert.ToBase64String(encryptedKey),
                    IV = Convert.ToBase64String(iv),
                    Tag = Convert.ToBase64String(tag)
                };

                Log.Debug("Message encrypted successfully");
                return encrypted;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error encrypting message");
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
                // Descifrar la clave AES con nuestra clave privada RSA
                byte[] encryptedKey = Convert.FromBase64String(encrypted.EncryptedKey);
                byte[] key = _rsaKeyPair.Decrypt(encryptedKey, RSAEncryptionPadding.OaepSHA256);

                // Descifrar el mensaje con AES-GCM
                byte[] ciphertext = Convert.FromBase64String(encrypted.Ciphertext);
                byte[] iv = Convert.FromBase64String(encrypted.IV);
                byte[] tag = Convert.FromBase64String(encrypted.Tag);
                byte[] plaintext = new byte[ciphertext.Length];

                using (var gcm = new AesGcm(key))
                {
                    gcm.Decrypt(iv, ciphertext, tag, plaintext);
                }

                string decryptedText = Encoding.UTF8.GetString(plaintext);
                Log.Debug("Message decrypted successfully");
                return decryptedText;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error decrypting message");
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
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] signature = _rsaKeyPair.SignData(messageBytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                
                Log.Debug("Message signed successfully");
                return Convert.ToBase64String(signature);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error signing message");
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
                byte[] messageBytes = Encoding.UTF8.GetBytes(message);
                byte[] signature = Convert.FromBase64String(signatureBase64);
                
                bool isValid = senderPublicKey.VerifyData(messageBytes, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                
                Log.Debug("Signature verification result: {IsValid}", isValid);
                return isValid;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error verifying signature");
                return false;
            }
        }

        /// <summary>
        /// Genera un hash criptográfico del ID de usuario para privacidad
        /// Usa SHA-256 con salt para prevenir ataques de rainbow table
        /// </summary>
        public static string HashUserId(string userId)
        {
            // Agregar salt único por aplicación
            const string SALT = "NexChat_v1_UserID_Salt_2025";
            string saltedInput = userId + SALT;
            
            byte[] inputBytes = Encoding.UTF8.GetBytes(saltedInput);
            byte[] hashBytes = SHA256.HashData(inputBytes);
            
            return Convert.ToBase64String(hashBytes);
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
                Log.Information("CryptographyService disposed");
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
