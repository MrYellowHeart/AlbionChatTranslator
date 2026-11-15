# Vendored: AlbionOnline-StatisticsAnalysis packet libraries

These folders are copied unmodified from
https://github.com/Triky313/AlbionOnline-StatisticsAnalysis
at commit `8ad2067d96c9372f4a9f07f5896a9e1ccb9f9b29` (2026-11-14), licensed GPL-3.0 (see `LICENSE`).

| Folder | Upstream path |
|---|---|
| `StatisticsAnalysisTool.Abstractions` | `src/StatisticsAnalysisTool.Abstractions` |
| `StatisticsAnalysisTool.Diagnostics` | `src/StatisticsAnalysisTool.Diagnostics` |
| `StatisticsAnalysisTool.PhotonPackageParser` | `src/StatisticsAnalysisTool.PhotonPackageParser` |
| `StatisticsAnalysisTool.Protocol18` | `src/StatisticsAnalysisTool.Protocol18` |
| `StatisticsAnalysisTool.Network` | `src/StatisticsAnalysisTool.Network` |
| `StatisticsAnalysisTool.Network/EventCodes.cs` | `src/StatisticsAnalysisTool/Network/EventCodes.cs` |

## Updating after an Albion patch

SBI renumbers event codes from time to time. StatisticsAnalysis tracks those changes in
`EventCodes.cs`. To pick them up, run `tools/update_vendored.sh` (or copy the folders above
from a newer upstream commit), update the commit hash here, then build and run the tests.

The translator looks events up by **name** (`EventCodes.ChatSay`, ...), never by number,
so a renumbering upstream is all that is needed.
