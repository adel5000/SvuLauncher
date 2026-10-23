using System.Globalization;
using SvuLauncher.App.Resources;
using SvuLauncher.App.ViewModels;
using SvuLauncher.Core;

namespace SvuLauncher.Tests;

public class SessionItemTests
{
    private static SessionItem Item(string status = "Planned", string start = "2026-10-20 08:00:00",
        string launched = "", string classId = "7", UserRole role = UserRole.Student) =>
        new(Fakes.Session with { Status = status, StartDate = start, LaunchedDate = launched, ClassId = classId }, role);

    [Fact]
    public void Parses_the_service_date_format()
    {
        var item = Item(launched: "2026-10-20 07:58:00");

        Assert.Equal(new DateTime(2026, 10, 20, 8, 0, 0), item.Start);
        Assert.Equal(new DateTime(2026, 10, 20, 7, 58, 0), item.Launched);
        Assert.True(item.HasLaunched);
        Assert.Equal(Strings.Format(Strings.Session_StartedAt, new DateTime(2026, 10, 20, 7, 58, 0).ToString("t", CultureInfo.CurrentUICulture)),
            item.LaunchedText);
    }

    [Fact]
    public void Missing_or_unparseable_dates_fall_back_safely()
    {
        var item = Item(start: "soon", launched: "");

        Assert.Null(item.Start);
        Assert.Equal("soon", item.StartText);
        Assert.False(item.HasLaunched);
        Assert.Null(item.LaunchedText);
    }

    [Theory]
    [InlineData("Started", SessionState.InProgress)]
    [InlineData("planned", SessionState.Planned)]
    [InlineData("Done", SessionState.Ended)]
    [InlineData("Cancelled", SessionState.Other)]
    public void Maps_service_status_to_state(string status, SessionState state)
    {
        var item = Item(status: status);

        Assert.Equal(state, item.State);
        Assert.Equal(state == SessionState.InProgress, item.IsInProgress);
        if (state == SessionState.Other)
            Assert.Equal(status, item.StatusLabel);
    }

    [Theory]
    [InlineData("0", UserRole.Student, true)]
    [InlineData("", UserRole.Student, true)]
    [InlineData("1426", UserRole.Student, false)]
    [InlineData("0", UserRole.Tutor, false)]
    public void Flags_student_sessions_with_no_class_like_legacy(string classId, UserRole role, bool outside)
    {
        Assert.Equal(outside, Item(classId: classId, role: role).IsOutsideMyClasses);
    }

    [Fact]
    public void Undated_sessions_sort_after_dated_ones()
    {
        var items = new[] { Item(start: "?"), Item(start: "2026-10-19 08:00:00"), Item(status: "Started", start: "?") };

        var ordered = SessionItem.Order(items).ToList();

        Assert.True(ordered[0].IsInProgress);
        Assert.Equal(new DateTime(2026, 10, 19, 8, 0, 0), ordered[1].Start);
        Assert.Null(ordered[2].Start);
    }
}
