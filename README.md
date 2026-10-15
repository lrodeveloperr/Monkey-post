# Monkey Post

The Nigerian playground game, on Android. Play against bots offline, or with friends in the same
room over Bluetooth / Wi-Fi using Google's Nearby Connections. No servers, no running costs.

- **Design and decisions:** [docs/DESIGN.md](docs/DESIGN.md)
- **Rules engine and bots:** [`core/`](core). Pure C#, used by the Unity app as a package.

## Run the tests

Requires the .NET 8 SDK.

```sh
dotnet test dotnet/MonkeyPost.Core.Tests
```

Add `--logger "console;verbosity=detailed"` to see bot-vs-bot balance statistics per difficulty.
