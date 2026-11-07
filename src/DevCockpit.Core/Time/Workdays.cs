namespace DevCockpit.Core.Time;

public static class Workdays
{
    /// <summary>Último día hábil anterior a <paramref name="today"/> (lunes a viernes).</summary>
    public static DateOnly PreviousWorkday(DateOnly today)
    {
        var day = today.AddDays(-1);
        while (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
            day = day.AddDays(-1);
        return day;
    }

    /// <summary>Lunes de la semana de <paramref name="today"/> (la semana va de lunes a domingo).</summary>
    public static DateOnly WeekStart(DateOnly today)
    {
        var offset = ((int)today.DayOfWeek + 6) % 7; // lunes = 0 ... domingo = 6
        return today.AddDays(-offset);
    }
}
