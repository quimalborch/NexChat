using NexChat.Data;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace NexChat.Services
{
    public class ConfigurationService
    {
        private Configuration? _currentConfiguration;
        private readonly string _configFilePath;
        private readonly string _configDirectory;

        public event EventHandler<Configuration>? ConfigurationChanged;

        public Configuration? CurrentConfiguration => _currentConfiguration;

        public ConfigurationService()
        {
            _configDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "NexChat",
                "Config"
            );

            _configFilePath = Path.Combine(_configDirectory, "configuration.ncf");
            
            EnsureDirectoryExists();
            LoadConfiguration();
        }

        private void EnsureDirectoryExists()
        {
            if (!Directory.Exists(_configDirectory))
            {
                Directory.CreateDirectory(_configDirectory);
            }
        }

        public void LoadConfiguration()
        {
            try
            {
                if (!File.Exists(_configFilePath))
                {
                    _currentConfiguration = null;
                    Console.WriteLine("Configuration file not found. Creating new configuration when needed.");
                    return;
                }

                string jsonContent = File.ReadAllText(_configFilePath);
                
                if (string.IsNullOrWhiteSpace(jsonContent))
                {
                    _currentConfiguration = null;
                    return;
                }

                _currentConfiguration = JsonSerializer.Deserialize<Configuration>(jsonContent);
                
                if (_currentConfiguration != null)
                {
                    Console.WriteLine($"Configuration loaded successfully. User: {_currentConfiguration.nombreUsuario}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading configuration: {ex.Message}");
                _currentConfiguration = null;
            }
        }

        public async Task<bool> SaveConfigurationAsync(Configuration configuration)
        {
            try
            {
                EnsureDirectoryExists();

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(configuration, options);
                await File.WriteAllTextAsync(_configFilePath, json);

                _currentConfiguration = configuration;
                ConfigurationChanged?.Invoke(this, configuration);
                
                Console.WriteLine($"Configuration saved successfully. User: {configuration.nombreUsuario}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
                return false;
            }
        }

        public bool SaveConfiguration(Configuration configuration)
        {
            try
            {
                EnsureDirectoryExists();

                var options = new JsonSerializerOptions
                {
                    WriteIndented = true
                };

                string json = JsonSerializer.Serialize(configuration, options);
                File.WriteAllText(_configFilePath, json);

                _currentConfiguration = configuration;
                ConfigurationChanged?.Invoke(this, configuration);
                
                Console.WriteLine($"Configuration saved successfully. User: {configuration.nombreUsuario}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving configuration: {ex.Message}");
                return false;
            }
        }

        public async Task<bool> UpdateUserNameAsync(string newUserName)
        {
            if (string.IsNullOrWhiteSpace(newUserName))
            {
                Console.WriteLine("Username cannot be empty.");
                return false;
            }

            if (_currentConfiguration == null)
            {
                _currentConfiguration = new Configuration
                {
                    nombreUsuario = newUserName,
                    idUsuario = Guid.NewGuid().ToString()
                };
            }
            else
            {
                _currentConfiguration.nombreUsuario = newUserName;
                _currentConfiguration.idUsuario = Guid.NewGuid().ToString();
            }

            return await SaveConfigurationAsync(_currentConfiguration);
        }

        public bool UpdateUserName(string newUserName)
        {
            if (string.IsNullOrWhiteSpace(newUserName))
            {
                Console.WriteLine("Username cannot be empty.");
                return false;
            }

            if (_currentConfiguration == null)
            {
                _currentConfiguration = new Configuration
                {
                    nombreUsuario = newUserName
                };
            }
            else
            {
                _currentConfiguration.nombreUsuario = newUserName;
            }

            return SaveConfiguration(_currentConfiguration);
        }

        public string GetUserId()
        {
            if (_currentConfiguration == null)
            {
                var newConfig = new Configuration();
                SaveConfiguration(newConfig);
                return newConfig.idUsuario;
            }

            return _currentConfiguration.idUsuario;
        }

        public string GetUserName()
        {
            return _currentConfiguration?.nombreUsuario ?? "Usuario";
        }

        public bool HasConfiguration()
        {
            return _currentConfiguration != null;
        }

        public Configuration GetOrCreateConfiguration()
        {
            if (_currentConfiguration == null)
            {
                _currentConfiguration = new Configuration();
                SaveConfiguration(_currentConfiguration);
            }

            return _currentConfiguration;
        }

        public void ResetConfiguration()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    File.Delete(_configFilePath);
                }

                _currentConfiguration = null;
                Console.WriteLine("Configuration reset successfully.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error resetting configuration: {ex.Message}");
            }
        }

        // CRUD para Usuarios Certificados
        public async Task<bool> AddCertifiedUserAsync(string nombre, string llave)
        {
            if (string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(llave))
            {
                Console.WriteLine("Nombre y llave no pueden estar vacíos.");
                return false;
            }

            var configuration = GetOrCreateConfiguration();
            
            // Verificar si ya existe un usuario con la misma llave
            if (configuration.usuariosCertificados.Any(u => u.Llave.Equals(llave, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("Ya existe un usuario certificado con esta llave.");
                return false;
            }

            var newUser = new CertifiedUser(nombre, llave);
            configuration.usuariosCertificados.Add(newUser);

            return await SaveConfigurationAsync(configuration);
        }

        public async Task<bool> UpdateCertifiedUserAsync(string userId, string nuevoNombre, string nuevaLlave)
        {
            if (string.IsNullOrWhiteSpace(nuevoNombre) || string.IsNullOrWhiteSpace(nuevaLlave))
            {
                Console.WriteLine("Nombre y llave no pueden estar vacíos.");
                return false;
            }

            var configuration = GetOrCreateConfiguration();
            var user = configuration.usuariosCertificados.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                Console.WriteLine("Usuario certificado no encontrado.");
                return false;
            }

            // Verificar si la nueva llave ya existe en otro usuario
            if (configuration.usuariosCertificados.Any(u => u.Id != userId && u.Llave.Equals(nuevaLlave, StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine("Ya existe un usuario certificado con esta llave.");
                return false;
            }

            user.Nombre = nuevoNombre;
            user.Llave = nuevaLlave;

            return await SaveConfigurationAsync(configuration);
        }

        public async Task<bool> DeleteCertifiedUserAsync(string userId)
        {
            var configuration = GetOrCreateConfiguration();
            var user = configuration.usuariosCertificados.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                Console.WriteLine("Usuario certificado no encontrado.");
                return false;
            }

            configuration.usuariosCertificados.Remove(user);
            return await SaveConfigurationAsync(configuration);
        }

        public List<CertifiedUser> GetCertifiedUsers()
        {
            var configuration = GetOrCreateConfiguration();
            return configuration.usuariosCertificados ?? new List<CertifiedUser>();
        }

        public CertifiedUser? GetCertifiedUserById(string userId)
        {
            var configuration = GetOrCreateConfiguration();
            return configuration.usuariosCertificados.FirstOrDefault(u => u.Id == userId);
        }

        public bool IsCertifiedUser(string llave)
        {
            var configuration = GetOrCreateConfiguration();
            return configuration.usuariosCertificados.Any(u => u.Llave.Equals(llave, StringComparison.OrdinalIgnoreCase));
        }
    }
}
