# SevaDesk Translations & Localization Guide

This directory manages all localized strings for SevaDesk using an **Over-the-Air (OTA)** cloud sync architecture.

---

## Architecture Overview

SevaDesk uses a **3-tier offline-first localization model**:

1. **Bundled Base (Offline)**: `Strings/en-US.json` and `Strings/hi-IN.json` ship with the application, ensuring 100% offline availability out-of-the-box.
2. **Local Cache**: Downloaded/updated language files are cached in `%LocalAppData%\SevaDesk\Translations\` on the operator's machine.
3. **GitHub Cloud Sync (OTA)**: When new languages are committed to the GitHub repository (`ciizerr/SevaDesk`), client applications download them automatically or when the user clicks **"Check for Language Updates"** in Settings.

---

## How to Add a New Language (e.g. Marathi, Gujarati, etc.)

Adding a new language takes just **3 simple steps**:

### Step 1: Create the Translation File
1. Copy `en-US.json` to a new file named with your BCP-47 language tag (e.g., `mr-IN.json` for Marathi or `gu-IN.json` for Gujarati).
2. Translate the values on the right side of the `:` while preserving the keys:
   ```json
   {
     "App.Name": "सेवाडेस्क",
     "Nav.Dashboard": "डॅशबोर्ड",
     "Settings.Title": "सेटिंग्ज"
   }
   ```

### Step 2: Register in `languages.json`
Add your new language entry to `Strings/languages.json`:
```json
{
  "code": "mr-IN",
  "name": "Marathi (India)",
  "nativeName": "मराठी",
  "file": "mr-IN.json"
}
```

### Step 3: Commit & Push to GitHub
Commit and push your changes to `main`:
```bash
git add Strings/
git commit -m "Add Marathi (mr-IN) language support"
git push
```

**That's it!** All SevaDesk desktop apps worldwide will automatically detect the new language and show it in their **Settings → Appearance → Language** dropdown!

---

## Fallback Rule
If a translation key is missing in any regional language file, SevaDesk automatically falls back to `en-US.json`. The user will never experience broken UI or missing labels.
