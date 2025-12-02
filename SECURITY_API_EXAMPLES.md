# Security API Examples - NexChat

This document provides practical examples of using the new security features.

---

## Basic Usage Examples

### 1. Initializing Security Services

```csharp
using NexChat.Security;

// Initialize services (usually in application startup)
var cryptoService = new CryptographyService();
var publicKeyManager = new PublicKeyManager();
var secureMessaging = new SecureMessagingService();
```

### 2. Getting Your Public Key

```csharp
// Get your public key to share with others
string myPublicKey = cryptoService.GetPublicKeyPem();

// Share this with other users (it's safe to share publicly)
Console.WriteLine($"My public key:\n{myPublicKey}");
```

### 3. Registering a Contact's Public Key

```csharp
// When you receive someone's public key
string contactIdHash = CryptographyService.HashUserId(contactId);
string contactPublicKeyPem = "-----BEGIN RSA PUBLIC KEY-----\n...";

secureMessaging.RegisterUserPublicKey(
    contactIdHash, 
    contactPublicKeyPem, 
    displayName: "John Doe"
);
```

---

## Encrypting and Sending Messages

### Simple Example

```csharp
// Create a message
var message = new Message(chat, sender, "Hello, secure world!");

// Get recipient's hashed ID
string recipientIdHash = CryptographyService.HashUserId(recipientId);

// Encrypt the message
Message encryptedMessage = secureMessaging.EncryptMessage(message, recipientIdHash);

// Send via WebSocket (encryptedMessage contains encrypted data)
await webSocket.SendAsync(encryptedMessage);
```

### Complete Example with Error Handling

```csharp
public async Task<bool> SendSecureMessage(Chat chat, string content, string recipientId)
{
    try
    {
        // Hash recipient ID
        string recipientIdHash = CryptographyService.HashUserId(recipientId);
        
        // Check if we have recipient's public key
        if (!secureMessaging.CanEncryptFor(recipientIdHash))
        {
            Log.Warning("Cannot encrypt: recipient public key not found");
            
            // Option 1: Request public key exchange
            await RequestPublicKeyExchange(recipientIdHash);
            
            // Option 2: Send unencrypted with warning
            // return SendUnencryptedWithWarning(chat, content);
            
            return false;
        }
        
        // Create message
        var sender = new Sender(CryptographyService.HashUserId(_currentUserId))
        {
            Name = _currentUserName
        };
        
        var message = new Message(chat, sender, content);
        
        // Encrypt
        var encrypted = secureMessaging.EncryptMessage(message, recipientIdHash);
        
        // Send
        bool sent = await SendViaWebSocket(encrypted);
        
        if (sent)
        {
            Log.Information("? Encrypted message sent successfully");
            return true;
        }
        
        return false;
    }
    catch (CryptographicException ex)
    {
        Log.Error(ex, "? Encryption failed");
        return false;
    }
}
```

---

## Receiving and Decrypting Messages

### Simple Example

```csharp
// Message received via WebSocket
Message receivedMessage = await ReceiveMessage();

// Check if encrypted
if (receivedMessage.IsEncrypted)
{
    // Decrypt
    Message decrypted = secureMessaging.DecryptMessage(receivedMessage);
    
    // Display decrypted content
    Console.WriteLine($"Received: {decrypted.Content}");
}
else
{
    // Plain text message
    Console.WriteLine($"?? Unencrypted: {receivedMessage.Content}");
}
```

### Complete Example with Verification

```csharp
public Message ProcessReceivedMessage(Message received)
{
    try
    {
        // Log receipt
        Log.Information($"?? Message received from {received.Sender.Name}");
        
        // Check if encrypted
        if (!received.IsEncrypted)
        {
            Log.Warning("?? Received unencrypted message");
            // Optionally reject or show warning in UI
            return received;
        }
        
        // Decrypt
        Message decrypted = secureMessaging.DecryptMessage(received);
        
        // Verify signature (already done in DecryptMessage, but showing explicitly)
        if (!string.IsNullOrEmpty(received.Signature) && 
            !string.IsNullOrEmpty(received.SenderPublicKey))
        {
            Log.Information("? Message signature verified");
        }
        else
        {
            Log.Warning("?? Message has no signature");
        }
        
        // Update UI
        Log.Information($"? Decrypted: {decrypted.Content}");
        
        return decrypted;
    }
    catch (CryptographicException ex)
    {
        Log.Error(ex, "? Failed to decrypt message");
        
        // Show error in UI
        ShowDecryptionError("Could not decrypt message. You may not have the correct keys.");
        
        // Return placeholder
        return new Message 
        { 
            Content = "[Unable to decrypt]",
            Sender = received.Sender,
            Timestamp = received.Timestamp
        };
    }
}
```

---

## Public Key Exchange

### Creating Key Exchange Data

```csharp
// Create public key exchange object
var myIdHash = CryptographyService.HashUserId(_currentUserId);
var exchange = secureMessaging.CreatePublicKeyExchange(
    myIdHash, 
    _currentUserName
);

// Serialize to JSON for transmission
string json = JsonSerializer.Serialize(exchange);

// Send via secure channel (HTTPS/Cloudflare)
await SendKeyExchange(json);
```

### Processing Received Key Exchange

```csharp
// Received key exchange JSON
string receivedJson = await ReceiveKeyExchange();

// Deserialize
var exchange = JsonSerializer.Deserialize<PublicKeyExchange>(receivedJson);

// Process and store public key
secureMessaging.ProcessPublicKeyExchange(exchange);

Log.Information($"? Registered public key for {exchange.DisplayName}");
```

### Automatic Exchange on First Contact

```csharp
public async Task InitiateSecureChat(string chatCode)
{
    // Join chat
    await JoinChat(chatCode);
    
    // Automatically exchange public keys
    var myExchange = secureMessaging.CreatePublicKeyExchange(
        CryptographyService.HashUserId(_currentUserId),
        _currentUserName
    );
    
    // Send as special "key_exchange" message
    await SendSystemMessage(new 
    { 
        type = "key_exchange", 
        data = myExchange 
    });
    
    Log.Information("? Public key exchange initiated");
}
```

---

## WebSocket with TLS Validation

### Connecting Securely

```csharp
var webSocket = new ChatWebSocketService();

// TLS validation is automatic
bool connected = await webSocket.ConnectAsync(serverUrl, chatId);

if (connected)
{
    Log.Information("? Connected securely with TLS validation");
}
else
{
    Log.Error("? Connection failed - certificate validation rejected");
}
```

### Handling Certificate Errors

```csharp
// Subscribe to connection status
webSocket.ConnectionStatusChanged += (sender, status) =>
{
    if (status.StartsWith("Error"))
    {
        // Certificate validation failed
        Log.Warning($"?? Connection error: {status}");
        
        // Show warning to user
        ShowSecurityWarning(
            "Could not verify server identity",
            "The connection may not be secure. Do not send sensitive information."
        );
    }
};
```

---

## User ID Hashing

### Hashing User IDs

```csharp
// ALWAYS use this method for user IDs
string userId = _configurationService.GetUserId();
string hashedId = CryptographyService.HashUserId(userId);

// Use hashedId for all external communication
var sender = new Sender(hashedId) { Name = userName };
```

### Verifying Sender Identity

```csharp
public bool IsMessageFromCurrentUser(Message message)
{
    string myHashedId = CryptographyService.HashUserId(_currentUserId);
    return message.Sender.Id == myHashedId;
}
```

---

## Key Management

### Checking if Keys Exist

```csharp
var crypto = new CryptographyService();

// Keys are generated automatically on first instantiation
string publicKey = crypto.GetPublicKeyPem();

Console.WriteLine("? Keys ready");
```

### Regenerating Keys

```csharp
public void RegenerateKeys()
{
    // Delete existing keys
    string keysFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NexChat",
        "Keys"
    );
    
    if (Directory.Exists(keysFolder))
    {
        Directory.Delete(keysFolder, recursive: true);
    }
    
    // Create new keys
    var crypto = new CryptographyService();
    
    Log.Information("? New keys generated");
    
    // Re-exchange keys with all contacts
    await ReexchangeKeysWithAllContacts();
}
```

### Backing Up Keys

```csharp
public async Task<bool> BackupKeys(string backupPath)
{
    try
    {
        string keysFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexChat",
            "Keys"
        );
        
        // Copy entire Keys folder
        await Task.Run(() => 
        {
            CopyDirectory(keysFolder, backupPath);
        });
        
        Log.Information($"? Keys backed up to {backupPath}");
        return true;
    }
    catch (Exception ex)
    {
        Log.Error(ex, "? Key backup failed");
        return false;
    }
}
```

---

## Integration with ChatService

### Modified ChatService.AddMessage()

```csharp
public async Task AddMessage(string chatId, Message message)
{
    Chat? chat = GetChatById(chatId);
    if (chat is null) return;
    
    // Remote chat - encrypt and send
    if (chat.IsInvited && _webSocketConnections.TryGetValue(chatId, out var wsService))
    {
        try
        {
            // Get recipient ID from chat
            string recipientIdHash = GetRecipientIdHash(chat);
            
            // Encrypt message
            var secureMessaging = new SecureMessagingService();
            var encrypted = secureMessaging.EncryptMessage(message, recipientIdHash);
            
            // Send encrypted via WebSocket
            bool sent = await wsService.SendMessageAsync(encrypted);
            
            if (sent)
            {
                Log.Information("? Encrypted message sent");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "? Error sending encrypted message");
            throw;
        }
    }
    else
    {
        // Local chat - store as-is
        chat.Messages.Add(message);
        SaveChats();
    }
}
```

---

## Testing Examples

### Unit Test: Encryption/Decryption

```csharp
[Test]
public void TestEncryptDecrypt()
{
    // Arrange
    var crypto1 = new CryptographyService(); // User A
    var crypto2 = new CryptographyService(); // User B
    
    string plaintext = "Secret message";
    
    // User B shares public key with User A
    var publicKey2 = crypto2.GetPublicKeyPem();
    var recipientKey = crypto1.ImportPublicKey(publicKey2);
    
    // Act
    var encrypted = crypto1.EncryptMessage(plaintext, recipientKey);
    var decrypted = crypto2.DecryptMessage(encrypted);
    
    // Assert
    Assert.AreEqual(plaintext, decrypted);
}
```

### Unit Test: Digital Signatures

```csharp
[Test]
public void TestSignVerify()
{
    // Arrange
    var crypto = new CryptographyService();
    string message = "Test message";
    var publicKey = crypto.ImportPublicKey(crypto.GetPublicKeyPem());
    
    // Act
    string signature = crypto.SignMessage(message);
    bool valid = crypto.VerifySignature(message, signature, publicKey);
    bool invalid = crypto.VerifySignature("Tampered message", signature, publicKey);
    
    // Assert
    Assert.IsTrue(valid);
    Assert.IsFalse(invalid);
}
```

### Integration Test: Full Message Flow

```csharp
[Test]
public async Task TestSecureMessageFlow()
{
    // Arrange
    var alice = new SecureMessagingService();
    var bob = new SecureMessagingService();
    
    string aliceId = CryptographyService.HashUserId("alice");
    string bobId = CryptographyService.HashUserId("bob");
    
    // Exchange public keys
    alice.RegisterUserPublicKey(bobId, bob.GetMyPublicKey(), "Bob");
    bob.RegisterUserPublicKey(aliceId, alice.GetMyPublicKey(), "Alice");
    
    // Create message
    var message = new Message(
        new Chat("Test"), 
        new Sender(aliceId) { Name = "Alice" },
        "Hello Bob!"
    );
    
    // Act
    var encrypted = alice.EncryptMessage(message, bobId);
    var decrypted = bob.DecryptMessage(encrypted);
    
    // Assert
    Assert.AreEqual("Hello Bob!", decrypted.Content);
    Assert.IsTrue(encrypted.IsEncrypted);
    Assert.IsFalse(decrypted.IsEncrypted);
}
```

---

## Error Handling Best Practices

### Graceful Degradation

```csharp
public async Task<bool> SendMessage(Message message, string recipientId)
{
    string recipientIdHash = CryptographyService.HashUserId(recipientId);
    
    try
    {
        // Try encrypted send first
        if (secureMessaging.CanEncryptFor(recipientIdHash))
        {
            var encrypted = secureMessaging.EncryptMessage(message, recipientIdHash);
            return await SendEncrypted(encrypted);
        }
        else
        {
            // Fallback: Send unencrypted with warning
            Log.Warning("?? Cannot encrypt - recipient key not found");
            
            bool consent = await ShowWarningDialog(
                "This message will be sent unencrypted. Continue?"
            );
            
            if (consent)
            {
                return await SendUnencrypted(message);
            }
            
            return false;
        }
    }
    catch (CryptographicException ex)
    {
        Log.Error(ex, "? Encryption failed");
        
        // Show error to user
        await ShowErrorDialog("Failed to encrypt message. Please try again.");
        
        return false;
    }
}
```

### Logging Security Events

```csharp
// Always log security-related events
Log.Information("?? User keys generated");
Log.Information("?? Public key registered for {User}", userName);
Log.Information("?? Message encrypted for {Recipient}", recipientId);
Log.Information("?? Message decrypted from {Sender}", senderId);
Log.Warning("?? Certificate validation failed");
Log.Error("? Decryption failed - wrong keys?");
```

---

## Performance Optimization

### Caching Public Keys

```csharp
// PublicKeyManager already caches keys in memory
// No need to call GetPublicKey repeatedly

var recipientKey = _publicKeyManager.GetPublicKey(recipientIdHash);
// Key is loaded from cache, not disk
```

### Reusing Crypto Service

```csharp
// ? Good: Reuse instance
var crypto = new CryptographyService(); // Create once
for (int i = 0; i < 100; i++)
{
    var encrypted = crypto.EncryptMessage(messages[i], recipientKey);
}

// ? Bad: Create new instance every time
for (int i = 0; i < 100; i++)
{
    var crypto = new CryptographyService(); // Expensive!
    var encrypted = crypto.EncryptMessage(messages[i], recipientKey);
}
```

---

## Common Pitfalls

### ? DON'T: Use Simple SHA256

```csharp
// ? WRONG: Vulnerable to rainbow tables
string badHash = Convert.ToBase64String(
    SHA256.HashData(Encoding.UTF8.GetBytes(userId))
);
```

### ? DO: Use Salted Hash

```csharp
// ? CORRECT: Protected against rainbow tables
string goodHash = CryptographyService.HashUserId(userId);
```

### ? DON'T: Skip Certificate Validation

```csharp
// ? NEVER DO THIS
webSocket.Options.RemoteCertificateValidationCallback = (a,b,c,d) => true;
```

### ? DO: Validate Certificates

```csharp
// ? Use the built-in validation
var webSocket = new ChatWebSocketService(); // Already configured
```

### ? DON'T: Share Private Keys

```csharp
// ? NEVER expose private key
var privateKey = crypto._rsaKeyPair.ExportRSAPrivateKey(); // Wrong!
```

### ? DO: Share Only Public Keys

```csharp
// ? Safe to share
var publicKey = crypto.GetPublicKeyPem();
```

---

## Migration from Old Code

### Before (Insecure)

```csharp
// Old code - plain text
var message = new Message(chat, sender, "Hello");
await webSocket.SendAsync(message);
```

### After (Secure)

```csharp
// New code - encrypted
var message = new Message(chat, sender, "Hello");
var encrypted = secureMessaging.EncryptMessage(message, recipientIdHash);
await webSocket.SendAsync(encrypted);
```

---

## Additional Resources

- **Full Documentation**: [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md)
- **Migration Guide**: [MIGRATION_SECURITY_v1.0.md](MIGRATION_SECURITY_v1.0.md)
- **Changelog**: [CHANGELOG.md](CHANGELOG.md)

---

**Last Updated**: 2025-01-XX
**Version**: 1.0.0
