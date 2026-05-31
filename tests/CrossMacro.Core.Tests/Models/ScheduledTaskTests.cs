using System;
using CrossMacro.Core.Models;
using FluentAssertions;
using Xunit;

namespace CrossMacro.Core.Tests.Models;

public class ScheduledTaskTests
{
    [Theory]
    [InlineData(IntervalUnit.Seconds, 10, 10000)]
    [InlineData(IntervalUnit.Minutes, 2, 120000)]
    [InlineData(IntervalUnit.Hours, 1, 3600000)]
    public void GetIntervalMs_ReturnsCorrectValues(IntervalUnit unit, int value, int expectedMs)
    {
        var task = new ScheduledTask
        {
            IntervalUnit = unit,
            IntervalValue = value
        };
        
        task.GetIntervalMs().Should().Be(expectedMs);
    }

    [Fact]
    public void GetInterval_WhenHoursValueIsLarge_ReturnsExpectedTimespanWithoutOverflow()
    {
        var task = new ScheduledTask
        {
            IntervalUnit = IntervalUnit.Hours,
            IntervalValue = 9999
        };

        task.GetInterval().Should().Be(TimeSpan.FromHours(9999));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void GetInterval_WhenIntervalValueIsNonPositive_UsesMinimumOneUnit(int invalidValue)
    {
        var task = new ScheduledTask
        {
            IntervalUnit = IntervalUnit.Seconds,
            IntervalValue = invalidValue
        };

        task.GetInterval().Should().Be(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void GetIntervalMs_WhenIntervalExceedsIntRange_ClampsToIntMaxValue()
    {
        var task = new ScheduledTask
        {
            IntervalUnit = IntervalUnit.Hours,
            IntervalValue = 9999
        };

        task.GetIntervalMs().Should().Be(int.MaxValue);
    }

    [Fact]
    public void CalculateNextRunTime_Interval_AddsIntervalToNow()
    {
        var now = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var task = new ScheduledTask
        {
            Type = ScheduleType.Interval,
            IntervalValue = 60,
            IntervalUnit = IntervalUnit.Seconds
        };
        
        task.CalculateNextRunTime(now);
        
        task.NextRunTime.Should().Be(now.AddSeconds(60));
    }

    [Fact]
    public void CalculateNextRunTime_Interval_WithLargeHoursValue_AddsExpectedInterval()
    {
        var now = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var task = new ScheduledTask
        {
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Hours,
            IntervalValue = 9999
        };

        task.CalculateNextRunTime(now);

        task.NextRunTime.Should().Be(now.AddHours(9999));
    }

    [Theory]
    [InlineData(IntervalUnit.Seconds, 2, 5)]
    [InlineData(IntervalUnit.Minutes, 2, 5)]
    [InlineData(IntervalUnit.Hours, 1, 3)]
    public void CalculateNextRunTime_Interval_WhenRandomized_UsesSelectedIntervalUnit(IntervalUnit unit, int minValue, int maxValue)
    {
        var now = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var task = new ScheduledTask
        {
            Type = ScheduleType.Interval,
            IntervalUnit = unit,
            UseRandomIntervalDelay = true,
            IntervalMinValue = minValue,
            IntervalMaxValue = maxValue
        };

        task.CalculateNextRunTime(now);

        task.NextRunTime.Should().NotBeNull();
        task.NextRunTime!.Value.Should().BeOnOrAfter(now + ToInterval(unit, minValue));
        task.NextRunTime.Value.Should().BeOnOrBefore(now + ToInterval(unit, maxValue));
    }

    [Fact]
    public void CalculateNextRunTime_Interval_WhenRandomRangeIsInverted_UsesMinimumAsMaximum()
    {
        var now = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var task = new ScheduledTask
        {
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Minutes,
            UseRandomIntervalDelay = true,
            IntervalMinValue = 10,
            IntervalMaxValue = 5
        };

        task.CalculateNextRunTime(now);

        task.IntervalMinValue.Should().Be(10);
        task.IntervalMaxValue.Should().Be(10);
        task.NextRunTime.Should().Be(now.AddMinutes(10));
    }

    [Fact]
    public void CalculateNextRunTime_Interval_WhenRandomRangeIsNonPositive_UsesMinimumOneSelectedUnit()
    {
        var now = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        var task = new ScheduledTask
        {
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Hours,
            UseRandomIntervalDelay = true,
            IntervalMinValue = -5,
            IntervalMaxValue = 0
        };

        task.CalculateNextRunTime(now);

        task.IntervalMinValue.Should().Be(1);
        task.IntervalMaxValue.Should().Be(1);
        task.NextRunTime.Should().Be(now.AddHours(1));
    }

    [Fact]
    public void CalculateNextRunTime_Interval_WhenDateTimeAdditionOverflows_ClampsToDateTimeMaxValue()
    {
        var now = new DateTime(DateTime.MaxValue.Ticks - 10, DateTimeKind.Utc);
        var task = new ScheduledTask
        {
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Seconds,
            IntervalValue = 1
        };

        task.CalculateNextRunTime(now);

        task.NextRunTime.Should().NotBeNull();
        task.NextRunTime!.Value.Ticks.Should().Be(DateTime.MaxValue.Ticks);
        task.NextRunTime.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void IsEnabled_WhenIntervalWouldOverflow_DoesNotThrowAndClampsNextRunTime()
    {
        var task = new ScheduledTask
        {
            Type = ScheduleType.Interval,
            IntervalUnit = IntervalUnit.Hours,
            IntervalValue = int.MaxValue,
            MacroFilePath = "test.macro"
        };

        var act = () => task.IsEnabled = true;

        act.Should().NotThrow();
        task.IsEnabled.Should().BeTrue();
        task.NextRunTime.Should().NotBeNull();
        task.NextRunTime!.Value.Ticks.Should().Be(DateTime.MaxValue.Ticks);
        task.NextRunTime.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void CalculateNextRunTime_SpecificTime_SetsExactTime()
    {
        var targetTime = new DateTime(2024, 1, 1, 15, 0, 0, DateTimeKind.Utc);
        var task = new ScheduledTask
        {
            Type = ScheduleType.SpecificTime,
            ScheduledDateTime = targetTime
        };
        
        task.CalculateNextRunTime(); // 'now' doesn't matter for specific time logic usually
        
        task.NextRunTime.Should().Be(targetTime);
    }

    [Fact]
    public void CalculateNextRunTime_SpecificTime_ConvertsLocalToUtc()
    {
        var localTime = new DateTime(2024, 1, 1, 12, 0, 0, DateTimeKind.Local);
        var expectedUtc = localTime.ToUniversalTime();
        
        var task = new ScheduledTask
        {
            Type = ScheduleType.SpecificTime,
            ScheduledDateTime = localTime
        };
        
        task.CalculateNextRunTime();
        
        task.NextRunTime.Should().Be(expectedUtc);
    }

    [Fact]
    public void CalculateNextRunTime_Weekly_WhenSelectedDayTimeIsAheadToday_UsesToday()
    {
        var now = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Local); // Monday
        var task = new ScheduledTask
        {
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.Monday | ScheduleDays.Wednesday,
            WeeklyTime = new TimeSpan(9, 0, 0)
        };

        task.CalculateNextRunTime(now);

        task.NextRunTime.Should().Be(new DateTime(2024, 1, 1, 9, 0, 0, DateTimeKind.Local).ToUniversalTime());
    }

    [Fact]
    public void CalculateNextRunTime_Weekly_WhenSelectedDayTimeHasPassedToday_UsesNextSelectedDay()
    {
        var now = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Local); // Monday
        var task = new ScheduledTask
        {
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.Monday | ScheduleDays.Wednesday,
            WeeklyTime = new TimeSpan(9, 0, 0)
        };

        task.CalculateNextRunTime(now);

        task.NextRunTime.Should().Be(new DateTime(2024, 1, 3, 9, 0, 0, DateTimeKind.Local).ToUniversalTime());
    }

    [Fact]
    public void CalculateNextRunTime_Weekly_WhenWeekdaysFromSunday_UsesMonday()
    {
        var now = new DateTime(2024, 1, 7, 12, 0, 0, DateTimeKind.Local); // Sunday
        var task = new ScheduledTask
        {
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.Weekdays,
            WeeklyTime = new TimeSpan(8, 30, 0)
        };

        task.CalculateNextRunTime(now);

        task.NextRunTime.Should().Be(new DateTime(2024, 1, 8, 8, 30, 0, DateTimeKind.Local).ToUniversalTime());
    }

    [Fact]
    public void CalculateNextRunTime_Weekly_WhenOnlySelectedDayTimeHasPassed_UsesSameDayNextWeek()
    {
        var now = new DateTime(2024, 1, 1, 10, 0, 0, DateTimeKind.Local); // Monday
        var task = new ScheduledTask
        {
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.Monday,
            WeeklyTime = new TimeSpan(9, 0, 0)
        };

        task.CalculateNextRunTime(now);

        task.NextRunTime.Should().Be(new DateTime(2024, 1, 8, 9, 0, 0, DateTimeKind.Local).ToUniversalTime());
    }

    [Fact]
    public void CalculateNextRunTime_Weekly_WhenNoDaysSelected_ClearsNextRunTime()
    {
        var now = new DateTime(2024, 1, 1, 8, 0, 0, DateTimeKind.Local);
        var task = new ScheduledTask
        {
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.None,
            WeeklyTime = new TimeSpan(9, 0, 0)
        };

        task.CalculateNextRunTime(now);

        task.NextRunTime.Should().BeNull();
    }

    [Fact]
    public void CanBeEnabled_WhenWeeklyHasNoSelectedDays_ReturnsFalse()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = "test.macro",
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.None
        };

        task.CanBeEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WhenWeeklyHasNoSelectedDays_DoesNotEnable()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = "test.macro",
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.None
        };

        task.IsEnabled = true;

        task.IsEnabled.Should().BeFalse();
        task.NextRunTime.Should().BeNull();
    }

    [Fact]
    public void WeeklyDays_WhenEnabledTaskLosesAllSelectedDays_DisablesTask()
    {
        var task = new ScheduledTask
        {
            MacroFilePath = "test.macro",
            Type = ScheduleType.Weekly,
            WeeklyDays = ScheduleDays.Monday,
            WeeklyTime = new TimeSpan(9, 0, 0)
        };
        task.IsEnabled = true;

        task.WeeklyDays = ScheduleDays.None;

        task.IsEnabled.Should().BeFalse();
        task.NextRunTime.Should().BeNull();
    }

    private static TimeSpan ToInterval(IntervalUnit unit, int value)
    {
        return unit switch
        {
            IntervalUnit.Seconds => TimeSpan.FromSeconds(value),
            IntervalUnit.Minutes => TimeSpan.FromMinutes(value),
            IntervalUnit.Hours => TimeSpan.FromHours(value),
            _ => TimeSpan.FromSeconds(value)
        };
    }
}
