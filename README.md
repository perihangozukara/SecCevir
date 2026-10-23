# Seç-Çevir (Select & Translate)

A small Windows tool for people reading English: select English (or other foreign-language) text with the mouse in any app, and its Turkish translation appears right under the selection. Turkish text is ignored.

- Works from the system tray; can be switched on/off and set to start with Windows
- Can read the English text aloud
- Single C# file, no installer

## Build
Run `derle.bat` (uses the C# compiler that ships with Windows / .NET Framework 4). It creates `SecCevir.exe`.

## Troubleshooting
The app writes skip reasons to `SecCevir.log` next to the exe (never the selected text). Create an empty `SecCevir.debug` file there for step-by-step logging.

## Privacy
The selected text is sent to Google Translate to get the translation. Do not use it on confidential text.

Built with the help of an AI coding assistant as part of my learning journey.
