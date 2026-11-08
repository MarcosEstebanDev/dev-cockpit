using DevCockpit.Core.Time;

namespace DevCockpit.Core.Tests.Time;

public class WorkdaysTests
{
    [Theory]
    [InlineData("2026-11-09", "2026-11-06")] // lunes -> viernes
    [InlineData("2026-11-10", "2026-11-09")] // martes -> lunes
    [InlineData("2026-11-08", "2026-11-06")] // domingo -> viernes
    [InlineData("2026-11-07", "2026-11-06")] // sábado -> viernes
    public void PreviousWorkday_skips_weekends(string today, string expected)
        => Assert.Equal(DateOnly.Parse(expected), Workdays.PreviousWorkday(DateOnly.Parse(today)));

    [Theory]
    [InlineData("2026-11-07", "2026-11-02")]
    [InlineData("2026-11-09", "2026-11-09")]
    [InlineData("2026-11-08", "2026-11-02")] // domingo pertenece a la semana del lunes anterior
    public void WeekStart_is_monday(string today, string expected)
        => Assert.Equal(DateOnly.Parse(expected), Workdays.WeekStart(DateOnly.Parse(today)));
}
