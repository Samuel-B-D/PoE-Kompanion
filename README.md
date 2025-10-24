# PoE Kompanion
A small utility for PoE on Linux, currently only containing a Logout Macro.
Aim to be a replacement to Lutcikaur's amazing [LutBot](http://lutbot.com).

## Screenshots
![tray-screenshot.png](Doc/tray-screenshot.png)

![settings-screenshot.png](Doc/settings-screenshot.png)

## Command to properly build:
```bash
dotnet publish -r linux-x64 -p:PublishAot=true -c Release
```