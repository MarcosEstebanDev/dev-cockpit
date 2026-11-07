namespace DevCockpit.Core.Time;

public interface IClock
{
    DateTimeOffset Now { get; }
}
