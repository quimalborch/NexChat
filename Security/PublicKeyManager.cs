using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Serilog;

namespace NexChat.Security
{
    /// <summary>
    /// Gestiona las claves públicas de los usuarios para E2EE
    /// Almacena y recupera claves públicas de forma segura
    /// </summary>
    public class PublicKeyManager
    {
        private readonly string _keysFolder;
        private readonly string _publicKeysFile;
        private Dictionary<string, UserPublicKey> _publicKeys;
        private readonly object _lock = new object();

        public PublicKeyManager()
        {
            _keysFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexChat",
                "Keys"
            );

            Directory.CreateDirectory(_keysFolder);

            _publicKeysFile = Path.Combine(_keysFolder, "public_keys.json");
            _publicKeys = new Dictionary<string, UserPublicKey>();

            LoadPublicKeys();
            
            Log.Information("PublicKeyManager initialized");
        }

        /// <summary>
        /// Carga las claves públicas desde el archivo
        /// </summary>
        private void LoadPublicKeys()
        {
            lock (_lock)
            {
                try
                {
                    if (File.Exists(_publicKeysFile))
                    {
                        string json = File.ReadAllText(_publicKeysFile);
                        var keys = JsonSerializer.Deserialize<Dictionary<string, UserPublicKey>>(json);
                        
                        if (keys != null)
                        {
                            _publicKeys = keys;
                            Log.Information("Loaded {Count} public keys from disk", _publicKeys.Count);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error loading public keys");
                    _publicKeys = new Dictionary<string, UserPublicKey>();
                }
            }
        }

        /// <summary>
        /// Guarda las claves públicas en el archivo
        /// </summary>
        private void SavePublicKeys()
        {
            lock (_lock)
            {
                try
                {
                    string json = JsonSerializer.Serialize(_publicKeys, new JsonSerializerOptions 
                    { 
                        WriteIndented = true 
                    });
                    File.WriteAllText(_publicKeysFile, json);
                    Log.Debug("Public keys saved to disk");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Error saving public keys");
                }
            }
        }

        /// <summary>
        /// Agrega o actualiza la clave pública de un usuario
        /// </summary>
        public void AddOrUpdatePublicKey(string userIdHash, string publicKeyPem, string? displayName = null)
        {
            lock (_lock)
            {
                // Verificar que la clave es válida antes de guardarla
                try
                {
                    using var rsa = RSA.Create();
                    rsa.ImportFromPem(publicKeyPem);
                    
                    // La clave es válida, guardarla
                    _publicKeys[userIdHash] = new UserPublicKey
                    {
                        UserIdHash = userIdHash,
                        PublicKeyPem = publicKeyPem,
                        DisplayName = displayName,
                        LastUpdated = DateTime.UtcNow
                    };

                    SavePublicKeys();
                    
                    Log.Information("Added/updated public key for user {UserIdHash}", userIdHash.Substring(0, 8) + "...");
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "Invalid public key provided for user {UserIdHash}", userIdHash);
                    throw new CryptographicException("Invalid public key format", ex);
                }
            }
        }

        /// <summary>
        /// Obtiene la clave pública de un usuario
        /// </summary>
        public RSA? GetPublicKey(string userIdHash)
        {
            lock (_lock)
            {
                if (_publicKeys.TryGetValue(userIdHash, out var userKey))
                {
                    try
                    {
                        var rsa = RSA.Create();
                        rsa.ImportFromPem(userKey.PublicKeyPem);
                        return rsa;
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error importing public key for user {UserIdHash}", userIdHash);
                        return null;
                    }
                }

                Log.Warning("Public key not found for user {UserIdHash}", userIdHash.Substring(0, 8) + "...");
                return null;
            }
        }

        /// <summary>
        /// Obtiene la clave pública en formato PEM
        /// </summary>
        public string? GetPublicKeyPem(string userIdHash)
        {
            lock (_lock)
            {
                if (_publicKeys.TryGetValue(userIdHash, out var userKey))
                {
                    return userKey.PublicKeyPem;
                }
                return null;
            }
        }

        /// <summary>
        /// Verifica si tenemos la clave pública de un usuario
        /// </summary>
        public bool HasPublicKey(string userIdHash)
        {
            lock (_lock)
            {
                return _publicKeys.ContainsKey(userIdHash);
            }
        }

        /// <summary>
        /// Elimina la clave pública de un usuario
        /// </summary>
        public void RemovePublicKey(string userIdHash)
        {
            lock (_lock)
            {
                if (_publicKeys.Remove(userIdHash))
                {
                    SavePublicKeys();
                    Log.Information("Removed public key for user {UserIdHash}", userIdHash.Substring(0, 8) + "...");
                }
            }
        }

        /// <summary>
        /// Obtiene todas las claves públicas almacenadas
        /// </summary>
        public IReadOnlyDictionary<string, UserPublicKey> GetAllPublicKeys()
        {
            lock (_lock)
            {
                return new Dictionary<string, UserPublicKey>(_publicKeys);
            }
        }
    }

    /// <summary>
    /// Representa la información de clave pública de un usuario
    /// </summary>
    public class UserPublicKey
    {
        public string UserIdHash { get; set; } = string.Empty;
        public string PublicKeyPem { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public DateTime LastUpdated { get; set; }
    }
}
