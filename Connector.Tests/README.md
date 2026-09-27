# Connector UI tests

Run on Windows with the .NET 8 SDK or newer:

```powershell
dotnet test Connector.sln -c Release
```

Use Release to avoid the plugin's existing Debug post-build deployment to the local NINA installation.

The tests instantiate the compiled options template with NINA's actual theme resources on an STA thread. They exercise shared button automation, keyboard command bindings, routed drag/drop events, per-profile persistence and boundary behavior. Native mouse dragging and physical equipment are not required.
