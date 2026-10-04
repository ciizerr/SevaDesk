<div align="center">
  <img src="SevaDesk.App/Assets/AppIcon.ico" alt="SevaDesk Logo" width="128" />

  # SevaDesk

  **Offline customer, session, billing, and document management for Cyber Cafés and Digital Service Centers.**

  <p>
    <a href="https://github.com/ciizerr/SevaDesk/releases/latest">
      <img src="https://img.shields.io/github/v/release/ciizerr/SevaDesk?style=flat-square" alt="Latest Release">
    </a>
    <a href="https://github.com/ciizerr/SevaDesk/blob/main/LICENSE">
      <img src="https://img.shields.io/github/license/ciizerr/SevaDesk?style=flat-square" alt="License">
    </a>
  </p>
</div>

---

## About

**SevaDesk** is a Windows application designed for Cyber Cafés and Digital Service Centers.

It brings common counter-side tasks into one place:

- Managing customers
- Tracking active work sessions
- Organizing documents
- Handling downloads and scanned files
- Billing and payments
- Generating receipts and invoices
- Keeping customer records locally

The goal is simple: **let the operator focus on the customer instead of managing folders, files, and paperwork manually.**

---

## Features

### 👤 Customer Management

Create and manage customer profiles and keep their basic information available for future visits.

Customer records can be reused instead of entering the same information every time a customer returns.

### 🖥️ Session Management

Start a session when a customer begins their work and keep track of what is being done.

Multiple customers can have active sessions at the same time. You can pause one session, switch to another customer, and return to it later.

This is useful when, for example, you are:

- Filling a form for one customer
- Scanning documents for another
- Waiting for a website or payment to load
- Switching between multiple ongoing jobs

### 📁 Document Hub

Keep customer documents organized without relying entirely on manually created Windows folders.

Documents can be associated with the appropriate customer and accessed from within SevaDesk.

The document workflow is designed around the way Cyber Cafés actually work, where files may come from browsers, scanners, downloads, or other applications.

### 📥 Automatic File Capture

SevaDesk can monitor configured working locations such as Downloads and scanner output folders.

New files can be captured and placed into the active customer's working area instead of being left behind in a growing Downloads folder.

This helps prevent problems such as:

> "Which customer's document is this?"

Files can first be kept in an unorganized area and sorted later, allowing the operator to continue serving customers without interrupting the workflow.

### 🔄 Multiple Active Customers

Cyber Café work rarely happens one customer at a time.

SevaDesk allows multiple customer sessions to remain active so you can switch between them without losing track of the work or documents belonging to each customer.

### 💰 Billing & Payments

Record services and payments directly from the customer's session.

SevaDesk supports:

- Service-based billing
- Discounts
- Multiple payment methods
- Paid / unpaid tracking
- Receipts
- A4 PDF invoices

### 🔒 Privacy Mode

Privacy Mode temporarily hides sensitive information from the main interface.

This is useful when a customer is standing at the counter and you don't want customer information or business figures openly visible on screen.

### 💾 Offline-First

SevaDesk uses a local SQLite database.

Customer information, billing records, and application data remain on the computer instead of being stored in a cloud service.

There is no required monthly cloud subscription and the application is designed to continue working without an internet connection.

> **Important:** Offline storage does not automatically mean your data is backed up. Regular backups are recommended.

### 🪟 Native Windows UI

SevaDesk is built with **WinUI 3** and follows the Windows design language.

It supports:

- Light and Dark themes
- Responsive layouts
- Native Windows controls
- System theme integration

### 🔄 In-App Updates

SevaDesk can check GitHub for new releases and install updates from within the application.

Updates are designed to leave the local database untouched.

---

## Screenshots

<!-- Add screenshots here -->

### Dashboard

_Add dashboard screenshot here._

### Customer & Session Management

_Add screenshot here._

### Document Hub

_Add screenshot here._

### Billing

_Add billing screenshot here._

---

## Download

The latest release is available on the **[Releases page](https://github.com/ciizerr/SevaDesk/releases/latest)**.

### Installer

Download:

```text
SevaDesk-Setup.exe
```

Run the installer and follow the setup instructions.

### Portable

If you don't want to install SevaDesk, download:

```text
SevaDesk-win-Portable.zip
```

Extract the archive and run:

```text
SevaDesk.App.exe
```

The portable version can be placed in another folder or on a USB drive.

> **Tip:** Keep your application data and backups on reliable storage. A portable installation does not replace a proper backup strategy.

---

## Requirements

SevaDesk is currently developed for Windows using WinUI 3 and the Windows App SDK.

**Supported Windows versions:** See the requirements listed in the latest release.

> Windows version support may change between releases depending on the Windows App SDK version used by SevaDesk.

---

## Tech Stack

SevaDesk is built using:

- **C# 12**
- **.NET 8**
- **WinUI 3 / Windows App SDK**
- **SQLite**
- **Dapper**

The application is designed as a local-first Windows application rather than a web application or cloud service.

---

## Building from Source

### Prerequisites

- Windows
- Visual Studio 2022
- Windows App SDK / WinUI 3 development tools
- .NET 8 SDK

### Clone the repository

```bash
git clone https://github.com/ciizerr/SevaDesk.git
cd SevaDesk
```

Open:

```text
SevaDesk.sln
```

in Visual Studio 2022.

Set:

```text
SevaDesk.App
```

as the startup project.

Select the appropriate architecture (`x64`, `x86`, or `ARM64`) and build the project.

### Creating a Release Package

The repository includes a PowerShell script for creating the release package.

```powershell
powershell -ExecutionPolicy Bypass -File .\Package-Beta.ps1 -SkipPortable:$false
```

The generated installer and portable package are placed in:

```text
dist\
```

---

## Data & Backups

SevaDesk stores its application data locally.

Because the application is offline-first, **the user is responsible for keeping backups of their data**.

Before moving to another computer or reinstalling Windows, make sure the SevaDesk data and backup files have been copied to another storage device.

---

## Contributing

Contributions are welcome.

You can help by:

- Reporting bugs
- Suggesting workflow improvements
- Improving documentation
- Translating the application
- Fixing issues
- Submitting pull requests

For bugs and feature requests, please open an **Issue**.

For general ideas and discussions, use **GitHub Discussions**.

---

## License

SevaDesk is open-source software licensed under the GNU General Public License v3.0 (GPLv3).

See [`LICENSE`](LICENSE) for the terms under which the project can be used, modified, and redistributed.

---

## Project Status

SevaDesk is currently in **beta**.

The application is usable, but some features and workflows may change as development continues.

Feedback from real Cyber Café and Digital Service Center workflows is especially useful for improving the application.