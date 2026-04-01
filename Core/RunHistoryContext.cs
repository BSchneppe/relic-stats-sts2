using System.Collections.Generic;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Runs.History;

namespace RelicStats.Core;

/// <summary>
/// Holds the currently-displayed RunHistory so that hover tip patches
/// can derive effective turns/combats from MapPointHistory at display time.
/// </summary>
public static class RunHistoryContext
{
    public static RunHistory? Current { get; set; }
}
