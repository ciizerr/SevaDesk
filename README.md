<div align="center">
  <img src="SevaDesk.App/Assets/AppIcon.ico" alt="SevaDesk Logo" width="128" />
  <h1>SevaDesk</h1>
  <p><strong>A beautifully designed, offline-first Cyber Café Customer & Document Management System for Windows.</strong></p>
  
  <p>
    <a href="https://github.com/ciizerr/SevaDesk/releases/latest"><img src="https://img.shields.io/github/v/release/ciizerr/SevaDesk?style=flat-square" alt="Latest Release"></a>
    <a href="https://github.com/ciizerr/SevaDesk/blob/main/LICENSE"><img src="https://img.shields.io/github/license/ciizerr/SevaDesk?style=flat-square" alt="License"></a>
  </p>
</div>

## 📌 Overview

SevaDesk is a desktop application custom-built for cyber cafes and local digital service centers. Built on the modern **WinUI 3 (Windows App SDK)** stack, it provides a blazingly fast, deeply native Windows experience (Windows 10 & 11) that works **100% offline**.

Keep track of walk-in customers, active computer sessions, billing, and document scans—all securely stored in a local SQLite database on your machine.

---

## ✨ Key Features

- 🌓 **Modern Windows UI**: Gorgeous Fluent Design, fully responsive, with automatic Light and Dark mode support. 
- 📴 **100% Offline-First**: No internet required. Your customer data never leaves your computer, ensuring maximum privacy and zero cloud subscriptions.
- 👥 **Customer & Session Management**: Track customer walk-ins, start timed sessions, and handle checkouts seamlessly.
- 💳 **Billing & Invoices**: Generate beautiful receipts and professional A4 PDF invoices. Complete with discount calculations and flexible payment methods.
- 📄 **Document Hub**: Easily store, view, and organize customer documents (ID cards, scans, prints) with Quick Rename tools.
- 🔒 **Privacy Mode**: One-click toggle on the dashboard to obscure sensitive customer data and daily revenue totals when customers are looking over the counter.
- 🔄 **In-App Updates**: Built-in update checker directly linked to GitHub Releases.

## 📸 Screenshots

*(Add screenshots of your Dashboard, Customer Profile, and Dark Mode here!)*

> **Tip:** We recommend showing the new Hero Dashboard and the deeply integrated Document Hub.

---

## 🚀 Installation

SevaDesk is distributed as a completely self-contained Windows application. You do not need to install the .NET runtime manually.

1. Go to the [Releases Page](https://github.com/ciizerr/SevaDesk/releases/latest).
2. Download `SevaDesk-Setup.exe`.
3. Run the installer. 
4. *(Optional)* If you prefer not to install anything, download the `SevaDesk-win-Portable.zip`, extract it, and run `SevaDesk.App.exe` directly!

---

## 🛠️ Development & Building from Source

SevaDesk is built using **C# 12**, **.NET 8**, and **WinUI 3**. It uses raw **SQLite** and **Dapper** for lightning-fast database queries.

### Prerequisites
- [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) (v17.8+)
- **Workloads required:**
  - .NET Desktop Development
  - Windows App SDK C# Templates
- **NSIS** (Nullsoft Scriptable Install System) v3.x (Required only if you want to build the installer).

### Getting Started
1. Clone the repository:
   ```bash
   git clone https://github.com/ciizerr/SevaDesk.git
   cd SevaDesk
   ```
2. Open `SevaDesk.sln` in Visual Studio 2022.
3. Set `SevaDesk.App` as the Startup Project.
4. Select `x64` architecture (WinUI 3 requires x64, x86, or ARM64, not "Any CPU").
5. Hit **F5** to build and run the application.

### Building the Release Package
We provide an automated PowerShell script that compiles the app into a self-contained folder and generates the final NSIS installer and Portable ZIP.

```powershell
Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
.\Package-Beta.ps1 -SkipPortable:$false
```
The final artifacts will be dropped in the `\dist\` folder.

---

## 🤝 Contributing

Contributions are welcome! Whether it's fixing bugs, improving the UI, or suggesting new features, feel free to open an Issue or submit a Pull Request.

## 📄 License

This project is licensed under the [MIT License](LICENSE). Free and open-source software.
