## Command to properly build:
```bash
dotnet publish -r linux-x64 -p:PublishAot=true -c Release
```

or, without AoT:
```bash
dotnet publish -r linux-x64 --self-contained=true -p:PublishReadyToRun=true -c Release
```