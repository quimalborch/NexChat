# Security Policy for NexChat

## Supported Versions

| Version         | Supported         | Security Status |
|-----------------|-------------------|-----------------|
| 1.0.x (latest)  | ✅                | ✅ E2EE enabled |
| < 1.0           | ❌                | ⚠️ Vulnerable  |

## Recently Fixed Vulnerabilities

### GHSA-jw54-89f2-m79v - Message Encryption & Authentication (Fixed in v1.0.0)

**Status**: ✅ **FIXED**

**Description**: Previous versions transmitted messages in plain text and used weak SHA256 hashing for user IDs, allowing message interception, modification, and identity spoofing.

**Fixed in**: Version 1.0.0

**Mitigations Applied**:
1. ✅ **End-to-End Encryption**: Messages now encrypted with AES-256-GCM
2. ✅ **Digital Signatures**: RSA-SHA256 signatures prevent message tampering
3. ✅ **TLS Validation**: Certificate validation prevents MITM attacks
4. ✅ **Salted Hashing**: User IDs now use salted SHA-256 to prevent rainbow table attacks

**Action Required**: 
- ⚠️ **All users should update to v1.0.0 or later immediately**
- Old messages sent before v1.0.0 were transmitted unencrypted
- New messages are automatically encrypted end-to-end

**Technical Details**: See [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md)

---

## Reporting a Vulnerability

If you find a potential security issue in NexChat, please do **not** open a public issue or pull request. Instead, report it privately so we can handle it safely.

To report a vulnerability:

- Go to the **Security → Report a vulnerability** section of this GitHub repository (if "Private vulnerability reporting" is enabled).
- Provide a clear description of the problem, including:
  - The type of vulnerability (e.g. buffer overflow, authentication bypass, etc.).  
  - The file(s) and exact location(s) in code (branch/commit/line number) involved.  
  - Steps or minimal repro instructions to trigger the issue.  
  - If possible: minimal proof-of-concept code, screenshots or logs.  
  - Impact: what could happen (data leak, remote code exec, loss of confidentiality, etc.).

After submitting the report, you'll be notified that we got it. We might ask follow-up questions if needed.

If for any reason private reporting is not available, as fallback send a message to **[@quimalborch](https://github.com/quimalborch)** — try to use a secure channel (like email or encrypted message), and mention "NexChat security issue" in the subject.

## Response Process & Timeline

| Step                     | Typical time to respond |
|--------------------------|-------------------------|
| Acknowledge receipt      | Within **48 hours**     |
| Request further info     | If needed, ASAP         |
| Fix & publish advisory   | As soon as possible — depends on severity |
| Public disclosure        | After fix is released and users are notified |

## Security Update Process

1. **Critical Vulnerabilities**: Emergency patch released within 48-72 hours
2. **High Severity**: Patch released within 1 week
3. **Medium/Low**: Included in next scheduled release
4. **Disclosure**: Coordinated disclosure 90 days after patch or when 95%+ users updated

## Important Guidelines

- Do **not** publicly disclose a vulnerability before a fix or advisory is ready.  
- Provide enough detail so we can reproduce and evaluate the issue — vague reports may delay resolution.  
- Respect coordinated disclosure: allow reasonable time to release patches before sharing exploit details.  
- Avoid posting sensitive data (keys, passwords, personal info) in public.

## Security Features

NexChat implements multiple layers of security:

### Cryptography
- **Encryption**: AES-256-GCM (Authenticated Encryption)
- **Key Exchange**: RSA-2048 with OAEP-SHA256 padding
- **Signatures**: RSA-SHA256 with PKCS#1 padding
- **Hashing**: SHA-256 with application-specific salt

### Network Security
- **TLS 1.3**: All internet communication encrypted
- **Certificate Validation**: Mandatory for all WebSocket connections
- **Cloudflare Tunnel**: Enterprise-grade secure networking

### Data Protection
- **E2EE**: Only sender and recipient can read messages
- **Local Storage**: Messages encrypted on disk
- **Key Protection**: Private keys stored with restrictive permissions
- **No Cloud Sync**: Messages never leave your device unencrypted

## Threat Model

NexChat is designed to protect against:

| Threat | Protection |
|--------|-----------|
| Message Interception | ✅ E2EE with AES-256-GCM |
| Message Tampering | ✅ Digital signatures + GCM auth tags |
| Identity Spoofing | ✅ Salted hashing + public key verification |
| MITM Attacks | ✅ TLS certificate validation |
| Rainbow Table | ✅ Salted hashing |
| Replay Attacks | ✅ Message IDs + timestamps |

## Out of Scope

The following are not considered security vulnerabilities:

- Metadata leakage (timestamps, message sizes, participant counts)
- Local access attacks (if attacker has physical access to unlocked device)
- Social engineering attacks
- Compromise of Cloudflare infrastructure (trusted third party)
- Bugs that require physical access or admin privileges

## Security Best Practices for Users

1. ✅ Keep NexChat updated to the latest version
2. ✅ Use only on trusted networks (avoid public WiFi)
3. ✅ Verify chat codes through a secondary channel
4. ✅ Log out when using shared computers
5. ❌ Never share your User ID with untrusted parties
6. ❌ Don't disable security features

---

**For technical security details, see**: [SECURITY_ARCHITECTURE.md](SECURITY_ARCHITECTURE.md)

Thanks for helping keep NexChat safe and secure 💜
