# VoiceKeyboard — Microsoft Store Listing

## App Identity
- **Store name:** VoiceKeyboard – AI Voice Typer (x64)
- **Category:** Productivity
- **Sub-category:** Dictation & Speech
- **Pricing:** $6.99 (one-time purchase) — see Pricing Notes below
- **Age rating:** 3+ (no objectionable content)
- **Privacy policy URL:** https://voicekeyboard.app/privacy

---

## Short Description (up to 270 characters)
> Press a hotkey, speak, and your words appear in any app — no cloud, no subscription. 100% local AI voice transcription, runs entirely on your x64 Windows device.

---

## Full Description

**VoiceKeyboard** is the fastest way to type with your voice — no internet, no subscription, no data ever leaving your device.

Powered by OpenAI's Whisper AI running entirely on your device, VoiceKeyboard transcribes your speech in seconds and pastes it directly into whatever text box, document, or app you're working in.

---

### 🔒 100% Private & Local
Your voice **never leaves your device**. No cloud. No servers. No subscription required. The AI model runs entirely offline after a one-time download.

### ⚡ x64 Native — Built for Modern Windows Laptops
VoiceKeyboard is compiled natively for x64, making it fast and efficient on any modern Windows laptop or desktop.

### 🌍 Works Everywhere
Transcribed text is pasted into any text field — browsers, Word, Notepad, Outlook, Slack, Teams, VS Code, chat apps, and more.

### ⌨️ Fully Customizable Hotkey
Default hotkey is **Ctrl+Shift+Space**, but you can set any combination you like in Settings.

### 🎙️ Toggle or Push-to-Talk
- **Toggle mode** — Press once to start recording, press again to stop and transcribe
- **Push-to-Talk mode** — Hold the hotkey while speaking, release to transcribe

### 🤖 Multiple AI Model Sizes
Choose the right balance of speed vs. accuracy:
| Model  | Size   | Speed       | Accuracy  |
|--------|--------|-------------|-----------|
| Tiny   | 75 MB  | Instant     | Good      |
| Base   | 142 MB | Very fast   | Very good |
| Small  | 466 MB | Fast        | Great     |
| Medium | 1.5 GB | Moderate    | Excellent |

### 🌐 10+ Languages
English, Spanish, French, German, Japanese, Chinese, Korean, Portuguese, Italian, Russian, and auto-detect.

### 💡 Smart Paste
Enable "Paste to cursor position" and the text appears wherever your cursor is when transcription finishes — even if you clicked to a different app while it was processing.

---

## What's New (Version 1.0)
- Initial release
- x64 native — runs on any modern Windows laptop or desktop
- Toggle and Push-to-Talk recording modes
- Customizable hotkeys
- 4 Whisper model sizes (Tiny, Base, Small, Medium)
- Smart paste to current cursor position
- Minimal floating overlay indicator
- System tray integration

---

## Screenshots Required (take these before submitting)

### Screenshot 1 — Hero / Main UI
**Caption:** "Press a hotkey. Speak. Text appears anywhere."
- Show the recording overlay floating above a text editor with transcribed text

### Screenshot 2 — Settings Window
**Caption:** "Customize your hotkey, model, and language"
- Settings window showing model selection with Base downloaded (green dot)

### Screenshot 3 — Paste in Action
**Caption:** "Works in any app — browsers, Word, Notepad, Teams"
- Show transcribed text appearing in a browser text field or Word document

### Screenshot 4 — System Tray
**Caption:** "Lives quietly in your system tray — always ready"
- Tray icon context menu visible

### Screenshot 5 — Model Download
**Caption:** "One-time download of the AI model — then works 100% offline"
- Settings window showing the download progress bar

**Screenshot sizes required:**
- Desktop: 1366×768 or 1920×1080 minimum
- Minimum 1 screenshot, maximum 10
- PNG or JPG, under 50 MB each

---

## Store Keywords (search terms)
voice typing, voice keyboard, speech to text, dictation, whisper AI, local AI,
offline transcription, x64, voice input, productivity, voice control,
privacy, no cloud, Windows laptop

---

## Pricing Notes

**Recommended pricing: $6.99 one-time**

Rationale:
- Dragon Anywhere: $15/month subscription
- Windows built-in dictation: free but cloud-dependent
- VoiceKeyboard: one-time, offline, ARM64 optimized

Alternative pricing strategies:
- **Free** (build user base, add IAP later for features)
- **Free + $4.99 IAP** to unlock Small/Medium model downloads
- **$4.99** (lower barrier to entry)

---

## Publisher Requirements Checklist

Before submitting to Partner Center:

- [ ] Register at https://partner.microsoft.com ($19 one-time individual developer fee)
- [ ] Reserve app name "VoiceKeyboard – AI Voice Typer"
- [ ] Host privacy policy at a public URL (use PrivacyPolicy.html from this repo)
- [ ] Generate final icons (run Assets/GenerateIcons.ps1, then polish in Figma/Photoshop)
- [ ] Take the 5 screenshots above
- [ ] Build the MSIX: `.\Build-Store.ps1 -ForStore -Version 1.0.0.0` (produces `publish\VoiceKeyboard_1.0.0.0_x64.msix`)
- [ ] Upload MSIX + screenshots + description in Partner Center
- [ ] Submit for certification (typically 1-3 business days)

---

## App Support URL
https://voicekeyboard.app/support

## App Website
https://voicekeyboard.app
