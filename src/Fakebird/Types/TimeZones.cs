using System.Globalization;

namespace Fakebird.Types;

/// <summary>
/// Firebird's time zones (TimeZoneUtil): a zone is a 16-bit id, either an offset (id = minutes + 1439, up to
/// ±14:00) or a region of Firebird's built-in list (id = 65535 - index). Region rules come from the bundled tz
/// data (TimeZoneRules). The session zone of the running statement is ambient (per thread), like Firebird's callbacks.
/// </summary>
public static class TimeZones
{
    public const ushort Gmt = 65535;
    const int MaxOffsetMinutes = 14 * 60;
    const long TicksPerMinute = 60 * Value.TicksPerSecond;
    /// <summary>TIME WITH TIME ZONE values of a region zone are converted at this date (2020-01-01).</summary>
    public const long TimeTzBaseDate = 58849;

    static readonly Dictionary<string, ushort> Ids = BuildIds();
    static readonly TimeZoneRules?[] Rules = new TimeZoneRules?[TimeZoneList.Names.Length];
    static readonly object[] Boxed = new object[ushort.MaxValue + 1];

    [ThreadStatic] static ushort _session;
    [ThreadStatic] static bool _hasSession;

    /// <summary>The time zone of the operating system (Firebird's default session zone).</summary>
    public static readonly ushort System = SystemZone();

    /// <summary>Session time zone of the statement running on this thread.</summary>
    public static ushort Session
    {
        get => _hasSession ? _session : System;
        set
        {
            _session = value;
            _hasSession = true;
        }
    }

    static Dictionary<string, ushort> BuildIds()
    {
        var d = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < TimeZoneList.Names.Length; i++) d.TryAdd(TimeZoneList.Names[i], (ushort)(Gmt - i));
        return d;
    }

    static ushort SystemZone()
    {
        var local = TimeZoneInfo.Local;
        if (Ids.TryGetValue(local.Id, out var id)) return id;
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(local.Id, out var iana) && Ids.TryGetValue(iana, out id)) return id;
        return FromOffset((int)local.BaseUtcOffset.TotalMinutes);
    }

    /// <summary>The boxed id, shared by all values of the zone (Value.O).</summary>
    public static object Box(ushort zone) => Boxed[zone] ??= zone;

    public static bool IsOffset(ushort zone) => zone <= 2 * 1439;
    public static ushort FromOffset(int minutes) => (ushort)(minutes + 1439);
    static int Displacement(ushort zone) => zone - 1439;

    public static IEnumerable<(ushort Id, string Name)> Regions()
    {
        for (var i = 0; i < TimeZoneList.Names.Length; i++) yield return ((ushort)(Gmt - i), TimeZoneList.Names[i]);
    }

    /// <summary>TimeZoneUtil::format: "+hh:mm" for offsets, the region's name otherwise.</summary>
    public static string Name(ushort zone)
    {
        if (!IsOffset(zone)) return TimeZoneList.Names[Gmt - zone];
        var d = Displacement(zone);
        var sign = d < 0 ? '-' : '+';
        d = Math.Abs(d);
        return string.Create(CultureInfo.InvariantCulture, $"{sign}{d / 60:00}:{d % 60:00}");
    }

    /// <summary>TimeZoneUtil::parse (strict): "+hh:mm" offsets or region names.</summary>
    public static ushort Parse(string str)
    {
        var p = 0;
        var end = str.Length;
        SkipSpaces(str, ref p);
        if (p < end && str[p] is '-' or '+')
        {
            var sign = str[p++] == '-' ? -1 : 1;
            SkipSpaces(str, ref p);
            var tzh = ParseNumber(str, ref p);
            if (tzh >= 0)
            {
                SkipSpaces(str, ref p);
                if (p < end && str[p] == ':')
                {
                    ++p;
                    SkipSpaces(str, ref p);
                    var tzm = ParseNumber(str, ref p);
                    if (tzm >= 0)
                    {
                        SkipSpaces(str, ref p);
                        if (p == end)
                        {
                            // makeFromOffset
                            if (tzm > 59 || tzh > 14 || (tzh == 14 && tzm > 0))
                                throw new StatusBuilder().Gds(FbMessages.invalid_timezone_offset)
                                    .Str((sign < 0 ? "-" : "+") + tzh.ToString("00", CultureInfo.InvariantCulture) + ":" + tzm.ToString("00", CultureInfo.InvariantCulture)).Build();
                            return FromOffset(sign * (tzh * 60 + tzm));
                        }
                    }
                }
            }
            throw new StatusBuilder().Gds(FbMessages.invalid_timezone_offset).Str(str).Build();
        }
        return ParseRegion(str, p);
    }

    static ushort ParseRegion(string str, int p)
    {
        var end = str.Length;
        SkipSpaces(str, ref p);
        var start = p;
        while (p < end && (str[p] is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_' or '/'
                           || (p != start && str[p] is >= '0' and <= '9' or '+' or '-')))
            p++;
        var name = str[start..p];
        SkipSpaces(str, ref p);
        if (p == end && Ids.TryGetValue(name, out var id)) return id;
        throw new StatusBuilder().Gds(FbMessages.invalid_timezone_region).Str(str[start..]).Build();
    }

    static void SkipSpaces(string s, ref int p)
    {
        while (p < s.Length && s[p] is ' ' or '\t') p++;
    }

    static int ParseNumber(string s, ref int p)
    {
        var start = p;
        var n = 0;
        while (p < s.Length && s[p] is >= '0' and <= '9') n = n * 10 + s[p++] - '0';
        return p == start ? -1 : n;
    }

    /// <summary>The zone's displacement from UTC, in minutes, at a UTC instant (timestamp ticks).</summary>
    public static int OffsetAt(ushort zone, long utcTicks)
    {
        if (zone == Gmt) return 0;
        if (IsOffset(zone)) return Displacement(zone);
        var rules = Rules[Gmt - zone] ??= TimeZoneRules.Find(TimeZoneList.Names[Gmt - zone]);
        if (rules == null) return 0;
        // ICU's milliseconds divided by minutes, as Firebird does: local mean times truncate toward zero
        return rules.OffsetAt(Math.DivRem(utcTicks, Value.TicksPerSecond, out var r) - (r < 0 ? 1 : 0) - UnixEpochSeconds) / 60;
    }

    /// <summary>1970-01-01 in timestamp seconds (days since 1858-11-17).</summary>
    const long UnixEpochSeconds = 40587L * 86400;

    /// <summary>Local wall time of a UTC instant in the zone.</summary>
    public static long ToLocal(long utcTicks, ushort zone) => utcTicks + OffsetAt(zone, utcTicks) * TicksPerMinute;

    /// <summary>
    /// localTimeStampToUtc: the UTC instant of a wall time in the zone (ICU with UCAL_WALLTIME_FIRST). A repeated
    /// wall time is its first occurrence; a skipped one is read with the offset in effect before the gap.
    /// </summary>
    public static long ToUtc(long localTicks, ushort zone)
    {
        if (zone == Gmt) return localTicks;
        if (IsOffset(zone)) return localTicks - Displacement(zone) * TicksPerMinute;
        var before = localTicks - OffsetAt(zone, localTicks - Value.TicksPerDay) * TicksPerMinute;
        var after = localTicks - OffsetAt(zone, localTicks + Value.TicksPerDay) * TicksPerMinute;
        var beforeValid = ToLocal(before, zone) == localTicks;
        var afterValid = ToLocal(after, zone) == localTicks;
        if (beforeValid && afterValid) return Math.Min(before, after);
        if (afterValid) return after;
        return before;
    }

    /// <summary>Local time of day of a TIME WITH TIME ZONE (UTC time of day), at the base date.</summary>
    public static long TimeToLocal(long utcTime, ushort zone)
        => Conv.SplitTimestamp(ToLocal(TimeTzBaseDate * Value.TicksPerDay + utcTime, zone)).time;

    /// <summary>UTC time of day of a local time of day in the zone, at the base date.</summary>
    public static long TimeToUtc(long localTime, ushort zone)
        => Conv.SplitTimestamp(ToUtc(TimeTzBaseDate * Value.TicksPerDay + localTime, zone)).time;

    /// <summary>The current date in the session zone (Firebird's getLocalDate).</summary>
    public static long LocalToday() => Conv.SplitTimestamp(ToLocal(Conv.Now().L, Session)).day;

    /// <summary>timeTzToTimeStampTz: today's date (in the session) with the value's local time, in its zone.</summary>
    public static long TimeTzToTimestampTz(long utcTime, ushort zone)
        => ToUtc(LocalToday() * Value.TicksPerDay + TimeToLocal(utcTime, zone), zone);

    /// <summary>timeTzToTime: the session's local time of day of a TIME WITH TIME ZONE.</summary>
    public static long TimeTzToTime(long utcTime, ushort zone)
        => Conv.SplitTimestamp(ToLocal(TimeTzToTimestampTz(utcTime, zone), Session)).time;

    /// <summary>timeStampTzToTimeTz: the local time of day, as a TIME WITH TIME ZONE of the same zone.</summary>
    public static long TimestampTzToTimeTz(long utcTicks, ushort zone)
        => TimeToUtc(Conv.SplitTimestamp(ToLocal(utcTicks, zone)).time, zone);
}
