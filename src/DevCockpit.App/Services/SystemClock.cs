using System;
using DevCockpit.Core.Time;

namespace DevCockpit.App.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
}
