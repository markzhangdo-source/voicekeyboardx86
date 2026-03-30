# VoiceKeyboard

Local AI voice transcription for x64 Windows.

## Building

```
dotnet build -c Release -r win-x64
```

Or publish as single file:
```
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

## First Run

1. Right-click the tray icon → **Settings**
2. Choose a Whisper model and click **Download** (Base ~142 MB recommended)
3. Set your preferred hotkey and mode
4. Click **Save**

## Default Hotkey

- **Toggle mode**: `Ctrl + Shift + Space` — press once to start, press again to stop & transcribe
- **Push to Talk**: Hold `Ctrl + Shift` — recording stops when you release

## How it works

1. Press your hotkey → microphone activates
2. Speak your text
3. Press hotkey again (or release in PTT mode)
4. Whisper transcribes locally on-device
5. Text is typed into whatever field was focused

## Models

| Model  | Size    | Speed  | Accuracy |
|--------|---------|--------|----------|
| Tiny   | 75 MB   | Fastest| Good     |
| Base   | 142 MB  | Fast   | Better   |
| Small  | 466 MB  | Medium | Great    |
| Medium | 1.5 GB  | Slow   | Best     |

Models are downloaded to `%AppData%\VoiceKeyboard\models\`
