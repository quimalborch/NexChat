# ?? Fix: "File not found" en public_keys.json

## ? Problema

Tu amigo recibía este error:

```
?? Cannot encrypt: recipient public key not found
?? Sending UNENCRYPTED message - no public key available
```

Y en los logs:

```
File not found: C:\Users\[user]\AppData\Local\NexChat\Keys\public_keys.json
```

## ?? Causa Raíz

El archivo `public_keys.json` **no se creaba automáticamente** al inicializar `PublicKeyManager`. 

El método `LoadPublicKeys()` solo **leía** el archivo si existía, pero nunca lo **creaba** inicialmente. Esto causaba que:

1. Al unirse a un chat remoto ? intenta cargar claves públicas
2. No encuentra el archivo ? log: "File not found"
3. No puede cifrar mensajes ? log: "Cannot encrypt: recipient public key not found"
4. Envía mensajes **SIN CIFRAR** ??

## ? Solución Implementada

### Cambio 1: Crear archivo en el constructor

```csharp
public PublicKeyManager()
{
    // ...código existente...
    
    _publicKeysFile = Path.Combine(_keysFolder, "public_keys.json");
    _publicKeys = new Dictionary<string, UserPublicKey>();

    // ?? NUEVO: Crear archivo vacío si no existe
    if (!File.Exists(_publicKeysFile))
    {
        try
        {
            File.WriteAllText(_publicKeysFile, "{}");
            Log.Information("Created empty public keys file: {Path}", _publicKeysFile);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Could not create public keys file: {Path}", _publicKeysFile);
        }
    }

    LoadPublicKeys();
    
    Log.Information("PublicKeyManager initialized");
}
```

### Cambio 2: Mejorar manejo de JSON vacío

```csharp
private void LoadPublicKeys()
{
    lock (_lock)
    {
        try
        {
            if (File.Exists(_publicKeysFile))
            {
                string json = File.ReadAllText(_publicKeysFile);
                
                // ?? NUEVO: Manejar archivo vacío o solo con {}
                if (string.IsNullOrWhiteSpace(json) || json.Trim() == "{}")
                {
                    _publicKeys = new Dictionary<string, UserPublicKey>();
                    Log.Information("Public keys file is empty, starting with empty dictionary");
                    return;
                }
                
                // ...resto del código...
            }
        }
        catch (JsonException jex)
        {
            // ?? NUEVO: Recrear archivo si JSON es inválido
            Log.Error(jex, "Invalid JSON in public keys file, starting fresh");
            _publicKeys = new Dictionary<string, UserPublicKey>();
            
            try
            {
                File.WriteAllText(_publicKeysFile, "{}");
                Log.Information("Recreated public keys file with empty JSON");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Could not recreate public keys file");
            }
        }
        // ...resto del código...
    }
}
```

## ?? Ubicación del Archivo

El archivo se crea aquí:

```
Windows:
%LocalAppData%\NexChat\Keys\public_keys.json

Ejemplo:
C:\Users\TuAmigo\AppData\Local\NexChat\Keys\public_keys.json
```

## ?? Cómo Verificar la Solución

### Paso 1: Eliminar archivos antiguos (opcional)

Si tu amigo ya tiene la app instalada, puede eliminar la carpeta para empezar limpio:

```
%LocalAppData%\NexChat\Keys\
```

### Paso 2: Iniciar la aplicación

Al iniciar NexChat, verá en los logs:

```
[INF] Created empty public keys file: C:\Users\...\NexChat\Keys\public_keys.json
[INF] PublicKeyManager initialized
```

### Paso 3: Unirse a un chat remoto

Al unirse a un chat:

```
?? Fetching public key from xyz...
? Public key fetched successfully for user: John Doe
? Public key exchanged with remote chat host: John Doe
```

### Paso 4: Enviar mensaje cifrado

Al enviar un mensaje:

```
?? Attempting to encrypt message for remote chat 'Chat Remoto'
? Message encrypted successfully
```

## ?? Si Persiste el Problema

Si tu amigo sigue viendo "File not found", verifica:

### 1. Permisos de Carpeta

```powershell
# Verificar permisos
icacls "%LocalAppData%\NexChat\Keys"
```

Debería mostrar:
```
BUILTIN\Users:(OI)(CI)(F)
NT AUTHORITY\SYSTEM:(OI)(CI)(F)
```

### 2. Crear Manualmente (como Admin)

Si hay problemas de permisos, ejecutar como Administrador:

```powershell
# Crear carpeta
New-Item -Path "$env:LOCALAPPDATA\NexChat\Keys" -ItemType Directory -Force

# Crear archivo vacío
Set-Content -Path "$env:LOCALAPPDATA\NexChat\Keys\public_keys.json" -Value "{}"

# Dar permisos de usuario
icacls "$env:LOCALAPPDATA\NexChat\Keys" /grant:r "$env:USERNAME:(OI)(CI)F" /T
```

### 3. Antivirus/Firewall

Algunos antivirus bloquean la creación de archivos en `AppData`:

- Agregar excepción para `NexChat.exe`
- Agregar excepción para `%LocalAppData%\NexChat\`

## ?? Logs a Buscar

### ? Funcionamiento Correcto

```
[INF] Created empty public keys file
[INF] PublicKeyManager initialized
[INF] ?? Fetching public key from xyz
[INF] ? Public key fetched successfully
[INF] ? Public key exchanged with remote chat host
[INF] ?? Attempting to encrypt message
[INF] ? Message encrypted successfully
```

### ? Problema de Permisos

```
[ERR] Could not create public keys file: C:\...\public_keys.json
System.UnauthorizedAccessException: Access to the path '...' is denied.
```

### ? Problema de Intercambio de Claves

```
[WRN] ?? Could not exchange public keys - encryption will not be available
[WRN] ?? Cannot encrypt: recipient public key not found
```

## ?? Resumen

| Antes | Después |
|-------|---------|
| ? Archivo no se creaba automáticamente | ? Se crea con `{}` al iniciar |
| ? Error "File not found" | ? Archivo siempre existe |
| ? Mensajes sin cifrar | ? Mensajes cifrados E2EE |
| ? Log: "Cannot encrypt" | ? Log: "Message encrypted successfully" |

## ?? Para Distribuir

Asegúrate de que tu amigo:

1. **Descargue la última versión** con este fix
2. **Reinicie la aplicación** completamente
3. **Verifique los logs** en la primera ejecución
4. Si hay problemas de permisos ? ejecutar como Admin la primera vez

---

**Fix implementado por**: GitHub Copilot  
**Fecha**: 2025-01-XX  
**Versión**: 1.0.0+ con fix de public_keys.json
