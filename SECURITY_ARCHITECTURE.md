# Security Architecture - NexChat

## Overview

NexChat implements a comprehensive security model to protect user privacy and prevent common attacks:

1. **End-to-End Encryption (E2EE)** for messages
2. **Digital Signatures** for message authentication
3. **TLS Certificate Validation** to prevent MITM attacks
4. **Secure User ID Hashing** with salt
5. **RSA + AES Hybrid Encryption**

---

## 1. End-to-End Encryption (E2EE)

### How it Works

NexChat uses a **hybrid encryption approach** combining RSA and AES:

#### Key Generation
- Each user generates a **2048-bit RSA key pair** on first run
- Private key is stored securely in `%LocalAppData%\NexChat\Keys\private.key`
- Public key is stored in `%LocalAppData%\NexChat\Keys\public.key`

#### Message Encryption Flow

```
1. Sender generates random 256-bit AES key
2. Message is encrypted with AES-256-GCM
3. AES key is encrypted with recipient's RSA public key
4. Encrypted message + encrypted key + IV + auth tag are sent
```

#### Message Decryption Flow

```
1. Recipient receives encrypted message bundle
2. Recipient decrypts AES key using their RSA private key
3. Message is decrypted using the AES key
4. Digital signature is verified
```

### Implementation

**Encryption:**
```csharp
var cryptoService = new CryptographyService();
var encryptedMessage = cryptoService.EncryptMessage(plaintext, recipientPublicKey);
```

**Decryption:**
```csharp
var decryptedText = cryptoService.DecryptMessage(encryptedMessage);
```

### Security Properties

- ? **Forward Secrecy**: Each message uses a unique AES key
- ? **Authenticated Encryption**: AES-GCM provides both confidentiality and authenticity
- ? **Strong Algorithms**: RSA-2048 and AES-256 are industry standard

---

## 2. Digital Signatures

### Purpose

Digital signatures ensure:
- **Authentication**: Verify sender identity
- **Integrity**: Detect message tampering
- **Non-repudiation**: Sender cannot deny sending

### How it Works

```
1. Sender signs message with their RSA private key (SHA-256 hash)
2. Signature is included with encrypted message
3. Recipient verifies signature using sender's public key
```

### Implementation

**Signing:**
```csharp
string signature = cryptoService.SignMessage(messageContent);
```

**Verification:**
```csharp
bool isValid = cryptoService.VerifySignature(message, signature, senderPublicKey);
```

---

## 3. TLS Certificate Validation

### Problem Addressed

Without proper TLS validation, attackers can perform **Man-in-the-Middle (MITM)** attacks by:
- Intercepting WebSocket connections
- Reading/modifying messages in transit
- Impersonating the server

### Solution

NexChat validates TLS certificates for all WebSocket connections:

```csharp
// Configure certificate validation callback
webSocket.Options.RemoteCertificateValidationCallback = ValidateServerCertificate;
```

### Validation Rules

| Scenario | Action |
|----------|--------|
| Valid certificate with no errors | ? Accept |
| Cloudflare certificate with name mismatch but valid chain | ? Accept (expected for dynamic subdomains) |
| Certificate chain errors | ? Reject |
| Expired or revoked certificates | ? Reject |
| Self-signed for localhost (dev only) | ?? Accept with warning |

### Implementation

See `ChatWebSocketService.ValidateServerCertificate()` for full validation logic.

---

## 4. Secure User ID Hashing

### Problem with Simple SHA-256

The vulnerability report identified that simple SHA-256 hashing is vulnerable to:
- **Rainbow table attacks**
- **Brute force attacks**
- **Identity spoofing**

### Solution: Salted Hash

```csharp
public static string HashUserId(string userId)
{
    const string SALT = "NexChat_v1_UserID_Salt_2025";
    string saltedInput = userId + SALT;
    byte[] hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(saltedInput));
    return Convert.ToBase64String(hashBytes);
}
```

### Security Properties

- ? **Salt prevents rainbow tables**: Precomputed hashes are useless
- ? **Application-specific**: Salt is unique to NexChat
- ? **Version-tagged**: Can rotate salt in future versions
- ?? **Note**: This is for privacy, not password storage (no pbkdf2 needed)

---

## 5. Public Key Management

### Key Exchange Protocol

1. **Initial Connection**:
   - User A starts chat, generates RSA key pair
   - User B joins, generates their key pair
   - Both exchange public keys via secure channel (HTTPS/Cloudflare)

2. **Key Storage**:
   - Public keys stored in `%LocalAppData%\NexChat\Keys\public_keys.json`
   - Keys are indexed by hashed user ID

3. **Key Rotation**:
   - Users can regenerate keys anytime
   - New keys are exchanged automatically on next connection

### Implementation

```csharp
var publicKeyManager = new PublicKeyManager();

// Register a user's public key
publicKeyManager.AddOrUpdatePublicKey(userIdHash, publicKeyPem, displayName);

// Get a user's public key for encryption
using var recipientKey = publicKeyManager.GetPublicKey(userIdHash);
```

---

## Security Best Practices

### For Users

1. ? **Use only on trusted networks** (avoid public WiFi)
2. ? **Verify chat codes** through a secondary channel (phone, etc.)
3. ? **Keep software updated** for security patches
4. ? **Never share your User ID** with untrusted parties
5. ? **Don't disable TLS validation**

### For Developers

1. ? **Always use `SecureMessagingService`** for message handling
2. ? **Validate all certificates** (don't blindly accept)
3. ? **Use salted hashing** for user IDs
4. ? **Log security events** for auditing
5. ? **Never store private keys unencrypted**
6. ? **Don't trust client-side data** without verification

---

## Attack Mitigation Summary

| Attack Type | Mitigation |
|-------------|------------|
| **MITM (Man-in-the-Middle)** | ? TLS certificate validation |
| **Message Interception** | ? E2EE with AES-256-GCM |
| **Message Tampering** | ? Digital signatures + GCM auth tags |
| **Identity Spoofing** | ? Salted hashing + public key verification |
| **Replay Attacks** | ? Message IDs + timestamps |
| **Rainbow Table** | ? Salted hashing |
| **Key Compromise** | ?? Forward secrecy (each message uses unique AES key) |

---

## Known Limitations

### 1. Trust on First Use (TOFU)

Currently, NexChat uses a **Trust on First Use** model:
- First time you chat with someone, you accept their public key
- Future messages from that key are trusted

**Mitigation**: Users should verify chat codes through a secondary channel.

### 2. Key Distribution

Public keys are exchanged via the chat connection itself.

**Future Improvement**: Implement out-of-band verification (QR codes, fingerprints).

### 3. Metadata Leakage

While message content is encrypted, metadata is visible:
- Timestamps
- Message sizes
- Sender/recipient IDs (hashed)

**Mitigation**: This is acceptable for a peer-to-peer chat application.

### 4. Local Storage

Messages are stored unencrypted locally after decryption.

**Future Improvement**: Optional local database encryption.

---

## Compliance

### GDPR Compliance

- ? **No cloud storage**: Data stays on user's device
- ? **User control**: Users can delete data anytime
- ? **Data minimization**: Only essential data is collected
- ? **Encryption**: Data in transit is encrypted

### OWASP Top 10

| Risk | Status |
|------|--------|
| A02:2021 – Cryptographic Failures | ? Mitigated (strong E2EE) |
| A03:2021 – Injection | ? N/A (no SQL/command execution) |
| A05:2021 – Security Misconfiguration | ? Secure defaults |
| A07:2021 – Identification and Authentication Failures | ? Mitigated (salted hashing + signatures) |

---

## Incident Response

If a security vulnerability is discovered:

1. Report via GitHub Security Advisory (private)
2. Do not disclose publicly until patched
3. Allow 90 days for patch development
4. Coordinated disclosure once fixed

See [SECURITY.md](SECURITY.md) for details.

---

## References

- [NIST: Recommendation for Key Management](https://nvlpubs.nist.gov/nistpubs/SpecialPublications/NIST.SP.800-57pt1r5.pdf)
- [OWASP Cryptographic Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Cryptographic_Storage_Cheat_Sheet.html)
- [RFC 8446: TLS 1.3](https://www.rfc-editor.org/rfc/rfc8446)
- [RFC 8446: AES-GCM](https://www.rfc-editor.org/rfc/rfc5116)

---

**Last Updated**: 2025-01-XX
**Version**: 1.0.0-security-patch
