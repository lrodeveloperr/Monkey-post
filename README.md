# Monkey Post

Nigerian street football to tiny goals, on Android. 5-a-side: 4 outfield players and a keeper who
can only use their feet. Play against bots offline, or 1v1 with a friend in the same room over
Bluetooth / Wi-Fi using Google's Nearby Connections. No servers, no running costs.

- **Design and decisions:** [docs/DESIGN.md](docs/DESIGN.md)
- **Rules, ball physics and bots:** [`core/`](core). Pure C#, used by the Unity app as a package.

## Run the tests

Requires the .NET 8 SDK.

```sh
dotnet test dotnet/MonkeyPost.Core.Tests
```

Add `--logger "console;verbosity=detailed"` to see bot-vs-bot match statistics per difficulty.
