# 💬 NexChat

<div align="center">

**A modern, secure chat application for Windows that lets you connect with anyone, anywhere.**

[![License: MIT](https://img.shields.io/badge/License-MIT-purple.svg)](LICENSE.txt)
[![.NET 8](https://img.shields.io/badge/.NET-10.0-512BD4)](https://dotnet.microsoft.com/)
[![CI/CD Velopack](https://github.com/quimalborch/NexChat/actions/workflows/velopack.yml/badge.svg)](https://github.com/quimalborch/NexChat/actions/workflows/velopack.yml)
[![Windows](https://img.shields.io/badge/Platform-Windows-0078D6)](https://www.microsoft.com/windows)
[![GitHub open issues by-label](https://img.shields.io/github/issues/quimalborch/NexChat/bug)](https://github.com/quimalborch/NexChat/issues?q=is%3Aissue+is%3Aopen+label%3Abug)
[![GitHub last commit](https://img.shields.io/github/last-commit/quimalborch/NexChat)](https://github.com/quimalborch/NexChat/commits/main)
[![Ask DeepWiki](https://deepwiki.com/badge.svg)](https://deepwiki.com/quimalborch/NexChat)

[Features](#-features) • [Getting Started](#-getting-started) • [How It Works](#-how-it-works) • [Technologies](#-technologies) • [Security](#-security)

</div>

---

## ✨ Features

### 🌐 **Connect Without Barriers**
- **Share Chats Instantly**: Create a chat and share a simple invitation code with anyone
- **No Account Required**: Start chatting immediately without sign-ups or personal information
- **Cross-Network Communication**: Connect with people across different networks and locations

### 🎨 **Beautiful & Customizable**
- **Modern Design**: Clean, intuitive interface built with the latest Windows UI
- **Theme Options**: Choose from multiple color themes (Purple, Red, Green) or match your system theme
- **Dark & Light Modes**: Perfect for day or night use

### 🔒 **Privacy First**
- **No Cloud Storage**: Your messages stay on your device
- **Secure Tunneling**: Uses Cloudflare's secure infrastructure for connections
- **Local Control**: You decide when to start or stop sharing your chats

### ⚡ **Real-Time Communication**
- **Instant Messaging**: Messages appear instantly with WebSocket technology
- **Live Updates**: See messages as they arrive without refreshing
- **Active Connection Status**: Know who's connected at all times

### 🚀 **Easy to Use**
- **One-Click Sharing**: Start sharing your chat with just one button
- **Simple Invitations**: Share a short code to invite others
- **Auto-Updates**: Always stay up to date with the latest features

---

## 🎯 Getting Started

### Installation

1. **Download NexChat** from the [Releases page](https://github.com/quimalborch/NexChat/releases)
2. **Run the installer** - it's that simple!
3. **Launch NexChat** and start chatting

### Creating Your First Chat

1. **Click "New Chat"** in the main window
2. **Give it a name** (e.g., "Weekend Plans")
3. **Start the server** by clicking the "Start" button
4. **Share the invitation code** with your friends

### Joining a Chat

1. **Click "Join Chat"** in the main window
2. **Enter the invitation code** you received
3. **Start messaging** instantly!

---

## 🛠️ How It Works

NexChat makes peer-to-peer communication simple and accessible:

### For Chat Creators (Hosts)
```
Your Computer → Local Web Server → Cloudflare Tunnel → Internet → Friend's Computer
```

When you create and start a chat:
1. NexChat starts a small web server on your computer
2. Opens a secure tunnel through Cloudflare (like a private bridge)
3. Generates a unique invitation code
4. Your friends use this code to connect directly to your chat

### For Chat Participants
```
Friend's Computer → Cloudflare Tunnel → Your Computer → Messages
```

When you join a chat:
1. Enter the invitation code
2. NexChat connects to the host through the secure tunnel
3. Start chatting in real-time with WebSocket technology
4. Messages flow instantly between all participants

**No servers, no accounts, no complexity** - just direct, secure communication.

---

## 🔧 Technologies

NexChat is built with modern, robust technologies:

### **Core Framework**
- **[.NET 8](https://dotnet.microsoft.com/)**: Microsoft's latest application platform for high performance and security
- **[WinUI 3](https://learn.microsoft.com/windows/apps/winui/)**: Modern Windows user interface framework for beautiful, native apps

### **Real-Time Communication**
- **WebSockets**: Industry-standard protocol for instant, bi-directional communication
- **HTTP/HTTPS**: Reliable web protocols for data exchange
- **[Cloudflare Tunnel](https://www.cloudflare.com/products/tunnel/)**: Enterprise-grade secure networking without port forwarding

### **Data & Logging**
- **System.Text.Json**: Fast, efficient data serialization
- **[Serilog](https://serilog.net/)**: Professional logging for troubleshooting and diagnostics

### **Updates & Deployment**
- **[Velopack](https://github.com/velopack/velopack)**: Modern, reliable auto-update system
- **Self-Contained Deployment**: Works without additional installations

### **Security**
- **SHA-256 Hashing**: Cryptographic verification for file integrity
- **TLS/SSL Encryption**: All internet communication is encrypted by default (via Cloudflare)
- **Local-First Architecture**: Your data stays on your device

---

## 🎨 Themes

NexChat comes with beautiful themes to match your style:

| Theme | Description |
|-------|-------------|
| 🟣 **Purple** | Default vibrant theme |
| 🔴 **Red** | Bold and energetic |
| 🟢 **Green** | Fresh and calming |
| 🌙 **Dark** | Easy on the eyes |
| ☀️ **Light** | Clean and bright |
| 🔄 **Automatic** | Match your system settings |

Change themes anytime from the settings menu!

---

## 🔐 Security

Your privacy and security are our top priorities:

### **Enhanced Security Features (v1.0+)**
- ✅ **End-to-End Encryption (E2EE)**: Messages encrypted with RSA-2048 + AES-256-GCM
- ✅ **Digital Signatures**: Every message is cryptographically signed for authenticity
- ✅ **TLS Certificate Validation**: Prevents Man-in-the-Middle (MITM) attacks
- ✅ **Salted Hashing**: User IDs protected with application-specific salt
- ✅ **Forward Secrecy**: Each message uses a unique encryption key

### **What We Do**
- ✅ All connections use Cloudflare's secure infrastructure with TLS 1.3
- ✅ Messages encrypted end-to-end (only you and recipient can read them)
- ✅ Messages stored encrypted locally on your device
- ✅ No user accounts or personal data collection
- ✅ Open source - audit the code yourself
- ✅ Regular security updates
- ✅ Secure key management with RSA-2048

### **What We Don't Do**
- ❌ No cloud storage of your messages
- ❌ No tracking or analytics
- ❌ No ads or monetization
- ❌ No selling your data
- ❌ No plain-text message transmission

### **Security Architecture**
For detailed information about NexChat's security implementation, see:
- [Security Architecture](SECURITY_ARCHITECTURE.md) - Technical details
- [Security Policy](SECURITY.md) - Vulnerability reporting

### **Reporting Security Issues**
Found a security concern? Please report it responsibly:
- Check our [Security Policy](SECURITY.md)
- Use GitHub's Private Vulnerability Reporting
- Report privately to [@quimalborch](https://github.com/quimalborch)

### **Verified Security**
- 🔒 **Encryption**: AES-256-GCM (AEAD)
- 🔑 **Key Exchange**: RSA-2048 with OAEP-SHA256 padding
- ✍️ **Signatures**: RSA-SHA256 with PKCS#1 padding
- 🔐 **TLS**: Certificate validation enforced
- 🛡️ **Hashing**: SHA-256 with application-specific salt

---

## 🤝 Contributing

We welcome contributions! Whether you're fixing bugs, adding features, or improving documentation:

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit your changes (`git commit -m 'Add some AmazingFeature'`)
4. Push to the branch (`git push origin feature/AmazingFeature`)
5. Open a Pull Request

---

## 📝 License

NexChat is open source software licensed under the [MIT License](LICENSE.txt).

This means you can:
- ✅ Use it for free, forever
- ✅ Modify it for your needs
- ✅ Share it with others
- ✅ Use it commercially

---

## 🌟 Show Your Support

If you find NexChat useful, please consider:
- ⭐ Starring the repository
- 🐛 Reporting bugs
- 💡 Suggesting new features
- 📢 Sharing with friends

---

## 📫 Contact

**Quim Alborch** - [@quimalborch](https://github.com/quimalborch)

**Project Link**: [https://github.com/quimalborch/NexChat](https://github.com/quimalborch/NexChat)

---

<div align="center">

**Made with 💜 by Quim Alborch**

*Connecting people, one chat at a time.*

</div>
