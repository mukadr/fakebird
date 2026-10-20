using System.Globalization;
using Fakebird.Types;

namespace Fakebird.Exec;

/// <summary>DATEADD, DATEDIFF and EXTRACT ported from Firebird 5 (jrd/SysFunction.cpp, dsql/ExprNodes.cpp).</summary>
public static class DateFuncs
{
    const long TicksPerDay = Value.TicksPerDay;
    const long TicksPerSecond = Value.TicksPerSecond;
    // TimeStamp::MIN_DATE / MAX_DATE (0001-01-01 and 9999-12-31) as Fakebird day numbers
    static readonly long MinDay = Conv.DateToDay(new DateOnly(1, 1, 1));
    static readonly long MaxDay = Conv.DateToDay(new DateOnly(9999, 12, 31));

    static FbError Sysf(int code, params string[] args)
    {
        var b = new StatusBuilder().Gds(Err.isc_expression_eval_err).Gds(code);
        foreach (var a in args) b.Str(a);
        return b.Build();
    }

    static FbError Range(VTag tag) => new StatusBuilder().Gds(tag switch
    {
        VTag.Timestamp => FbMessages.datetime_range_exceeded,
        VTag.Time => FbMessages.time_range_exceeded,
        _ => FbMessages.date_range_exceeded,
    }).Build();

    public static bool ValidDay(long day) => day >= MinDay && day <= MaxDay;

    /// <summary>evlDateAdd: the amount is rounded to an integer (milliseconds keep one decimal).</summary>
    public static Value DateAdd(string part, in Value quantityValue, in Value v)
    {
        // WITH TIME ZONE: the UTC value moves and the zone stays
        if (v.Tag == VTag.TimestampTz) return Value.TimestampTz(DateAdd(part, quantityValue, Value.Timestamp(v.L)).L, v.Zone);
        if (v.Tag == VTag.TimeTz) return Value.TimeTz(DateAdd(part, quantityValue, Value.Time(v.L)).L, v.Zone);
        long day, time;
        switch (v.Tag)
        {
            case VTag.Time:
                time = v.L;
                day = (MaxDay + MinDay) / 2;
                if (part is not ("HOUR" or "MINUTE" or "SECOND" or "MILLISECOND"))
                    throw Sysf(FbMessages.sysf_invalid_addpart_time, "DATEADD");
                break;
            case VTag.Date:
                day = v.L;
                time = 0;
                break;
            case VTag.Timestamp:
                (day, time) = Conv.SplitTimestamp(v.L);
                break;
            default:
                throw Sysf(FbMessages.sysf_invalid_add_datetime, "DATEADD");
        }
        // MOV_get_int64 rounds half away from zero; milliseconds are taken at scale -1 (tenths)
        var q = Conv.CastExact(quantityValue, part == "MILLISECOND" ? 1 : 0, TypeKind.BigInt).L;
        var span = MaxDay - MinDay;
        switch (part)
        {
            case "YEAR":
            {
                if (Math.Abs((decimal)q) > 9999) throw Range(v.Tag);
                var d = Conv.DayToDate(day);
                var y = d.Year + q;
                if (y is < 1 or > 9999) throw Range(v.Tag);
                // encode normalizes Feb 29 into Mar 1, which Firebird takes back to Feb 28
                var dim = DateTime.DaysInMonth((int)y, d.Month);
                day = Conv.DateToDay(new DateOnly((int)y, d.Month, Math.Min(d.Day, dim)));
                break;
            }
            case "MONTH":
            {
                if (Math.Abs((decimal)q) > 9999 * 12) throw Range(v.Tag);
                var d = Conv.DayToDate(day);
                int[] md = [31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
                var y = (int)(q / 12);
                var m = (int)(q % 12);
                var mon = d.Month - 1;
                var mday = d.Day;
                var ld = md[mon] - mday;
                var lm = mon;
                var year = d.Year + y;
                if ((mon += m) > 11)
                {
                    year++;
                    mon -= 12;
                }
                else if (mon < 0)
                {
                    year--;
                    mon += 12;
                }
                if ((year % 4 == 0 && year % 100 != 0) || year % 400 == 0) md[1]++;
                if (y >= 0 && m >= 0 && mday > md[lm]) mday = md[mon] - ld;
                if (mday > md[mon]) mday = md[mon];
                else if (mday < 1) mday = 1;
                if (year is < 1 or > 9999) throw Range(v.Tag);
                day = Conv.DateToDay(new DateOnly(year, mon + 1, mday));
                break;
            }
            case "DAY":
                if (Math.Abs((decimal)q) > span) throw Range(v.Tag);
                day += q;
                break;
            case "WEEK":
                if (Math.Abs((decimal)q) > span / 7 + 1) throw Range(v.Tag);
                day += q * 7;
                break;
            case "HOUR":
            case "MINUTE":
            case "SECOND":
            case "MILLISECOND":
            {
                var unitTicks = part switch { "HOUR" => 3600 * TicksPerSecond, "MINUTE" => 60 * TicksPerSecond, "SECOND" => TicksPerSecond, _ => 1L };
                var perDay = TicksPerDay / unitTicks;
                if (Math.Abs((decimal)q) > (decimal)(span + 1) * perDay) throw Range(v.Tag);
                if (v.Tag == VTag.Date)
                    day += q / perDay;
                else
                {
                    // add10msec: whole days and the remaining time, with carry
                    var total = (Int128)time + (Int128)q * unitTicks;
                    var carry = (long)(total / TicksPerDay);
                    var rest = (long)(total % TicksPerDay);
                    if (rest < 0)
                    {
                        rest += TicksPerDay;
                        carry--;
                    }
                    day += carry;
                    time = rest;
                }
                break;
            }
            default:
                throw Sysf(FbMessages.sysf_invalid_addpart_dtime, part, "DATEADD");
        }
        if (v.Tag != VTag.Time && !ValidDay(day)) throw Range(v.Tag);
        return v.Tag switch
        {
            VTag.Time => Value.Time(time),
            VTag.Date => Value.Date(day),
            _ => Value.Timestamp(day * TicksPerDay + time),
        };
    }

    /// <summary>evlDateDiff.</summary>
    public static Value DateDiff(string part, in Value a, in Value b)
    {
        // with a WITH TIME ZONE operand the values compare in UTC (a TIME or TIMESTAMP is in the session zone)
        if (a.IsTz || b.IsTz) return DateDiff(part, Utc(a), Utc(b));
        static (long Day, long Time) Split(in Value v) => v.Tag switch
        {
            VTag.Time => (0, v.L),
            VTag.Date => (v.L, 0),
            VTag.Timestamp => Conv.SplitTimestamp(v.L),
            _ => throw Sysf(FbMessages.sysf_invalid_diff_dtime, "DATEDIFF"),
        };
        var (d1, t1) = Split(a);
        var (d2, t2) = Split(b);
        switch (part)
        {
            case "HOUR":
                t1 -= t1 % (3600 * TicksPerSecond);
                t2 -= t2 % (3600 * TicksPerSecond);
                break;
            case "MINUTE":
                t1 -= t1 % (60 * TicksPerSecond);
                t2 -= t2 % (60 * TicksPerSecond);
                break;
            case "SECOND":
                t1 -= t1 % TicksPerSecond;
                t2 -= t2 % TicksPerSecond;
                break;
        }
        var aTime = a.Tag == VTag.Time;
        var bTime = b.Tag == VTag.Time;
        switch (part)
        {
            case "YEAR" or "QUARTER" or "MONTH" or "DAY" or "WEEK":
                if (aTime || bTime) throw Sysf(FbMessages.sysf_invalid_timediff, "DATEDIFF");
                break;
            case "HOUR" or "MINUTE" or "SECOND" or "MILLISECOND":
                if (a.Tag == VTag.Timestamp && bTime || aTime && b.Tag == VTag.Timestamp)
                    throw Sysf(FbMessages.sysf_invalid_tstamptimediff, "DATEDIFF");
                if (a.Tag == VTag.Date && bTime || aTime && b.Tag == VTag.Date)
                    throw Sysf(FbMessages.sysf_invalid_datetimediff, "DATEDIFF");
                break;
            default:
                throw Sysf(FbMessages.sysf_invalid_diffpart, part, "DATEDIFF");
        }
        switch (part)
        {
            case "YEAR":
                return Value.Int(Conv.DayToDate(d2).Year - Conv.DayToDate(d1).Year);
            case "MONTH":
            {
                var x = Conv.DayToDate(d1);
                var y = Conv.DayToDate(d2);
                return Value.Int(12L * (y.Year - x.Year) + y.Month - x.Month);
            }
            case "DAY": return Value.Int(d2 - d1);
            case "WEEK": return Value.Int((d2 - d1) / 7);
            case "HOUR": return Value.Int(24 * (d2 - d1) + (t2 - t1) / TicksPerSecond / 3600);
            case "MINUTE": return Value.Int(24 * 60 * (d2 - d1) + (t2 - t1) / TicksPerSecond / 60);
            case "SECOND": return Value.Int(86400 * (d2 - d1) + (t2 - t1) / TicksPerSecond);
            case "MILLISECOND": return Value.Int(86400 * (d2 - d1) * TicksPerSecond + (t2 - t1), 1);
            default: throw Sysf(FbMessages.sysf_invalid_diffpart, part, "DATEDIFF");
        }
    }

    static Value Utc(in Value v) => v.Tag switch
    {
        VTag.Time => Value.Time(TimeZones.TimeToUtc(v.L, TimeZones.Session)),
        VTag.Timestamp => Value.Timestamp(TimeZones.ToUtc(v.L, TimeZones.Session)),
        VTag.TimeTz => Value.Time(v.L),
        VTag.TimestampTz => Value.Timestamp(v.L),
        _ => v,
    };

    static readonly HashSet<string> ZoneParts = ["TIMEZONE_HOUR", "TIMEZONE_MINUTE", "TIMEZONE_NAME"];

    static readonly HashSet<string> DateParts = ["YEAR", "QUARTER", "MONTH", "DAY", "WEEKDAY", "YEARDAY", "WEEK"];
    static readonly HashSet<string> TimeParts = ["HOUR", "MINUTE", "SECOND", "MILLISECOND"];

    /// <summary>ExtractNode::dsqlPass: the part must exist in the argument's type (checked when preparing).</summary>
    public static void CheckExtract(string part, SqlType t)
    {
        if (t.Kind == TypeKind.Null) return;
        var ok = DateParts.Contains(part) ? t.Kind is TypeKind.Date or TypeKind.Timestamp or TypeKind.TimestampTz
            : TimeParts.Contains(part) ? t.Kind is TypeKind.Time or TypeKind.TimeTz or TypeKind.Timestamp or TypeKind.TimestampTz
            : ZoneParts.Contains(part) ? t.Kind is TypeKind.Time or TypeKind.TimeTz or TypeKind.Timestamp or TypeKind.TimestampTz
            : true;
        if (!ok)
            throw new StatusBuilder().Gds(Err.isc_dsql_error).Gds(Err.isc_sqlerr).Num(-105).Gds(FbMessages.extract_input_mismatch).Build();
    }

    /// <summary>ExtractNode::execute.</summary>
    public static Value Extract(string part, in Value v)
    {
        if (v.IsNull) return v;
        if (ZoneParts.Contains(part)) return ExtractZone(part, v);
        long day = 0, time = 0;
        switch (v.Tag)
        {
            case VTag.TimeTz:
                if (!TimeParts.Contains(part)) throw new StatusBuilder().Gds(Err.isc_expression_eval_err).Gds(FbMessages.invalid_extractpart_time).Build();
                time = TimeZones.TimeToLocal(v.L, v.Zone);
                break;
            case VTag.TimestampTz:
                (day, time) = Conv.SplitTimestamp(TimeZones.ToLocal(v.L, v.Zone));
                break;
            case VTag.Time:
                if (!TimeParts.Contains(part)) throw new StatusBuilder().Gds(Err.isc_expression_eval_err).Gds(FbMessages.invalid_extractpart_time).Build();
                time = v.L;
                break;
            case VTag.Date:
                if (TimeParts.Contains(part)) throw new StatusBuilder().Gds(Err.isc_expression_eval_err).Gds(FbMessages.invalid_extractpart_date).Build();
                day = v.L;
                break;
            case VTag.Timestamp:
                (day, time) = Conv.SplitTimestamp(v.L);
                break;
            default:
                throw new StatusBuilder().Gds(Err.isc_expression_eval_err).Gds(FbMessages.invalidarg_extract).Build();
        }
        var d = Conv.DayToDate(day);
        return part switch
        {
            "YEAR" => Value.Int(d.Year),
            "QUARTER" => Value.Int((d.Month - 1) / 3 + 1),
            "MONTH" => Value.Int(d.Month),
            "DAY" => Value.Int(d.Day),
            "WEEKDAY" => Value.Int((int)d.DayOfWeek),
            "YEARDAY" => Value.Int(d.DayOfYear - 1),
            "WEEK" => Value.Int(ISOWeek.GetWeekOfYear(d.ToDateTime(TimeOnly.MinValue))),
            "HOUR" => Value.Int(time / (3600 * TicksPerSecond)),
            "MINUTE" => Value.Int(time / (60 * TicksPerSecond) % 60),
            "SECOND" => Value.Int(time % (60 * TicksPerSecond), 4),
            "MILLISECOND" => Value.Int(time % TicksPerSecond, 1),
            _ => throw Err.Unsupported("EXTRACT(" + part + ")"),
        };
    }

    /// <summary>EXTRACT(TIMEZONE_HOUR | TIMEZONE_MINUTE | TIMEZONE_NAME): a value without zone is in the session's.</summary>
    static Value ExtractZone(string part, in Value v)
    {
        long utc;
        ushort zone;
        switch (v.Tag)
        {
            case VTag.Time:
            {
                var t = Conv.Cast(v, SqlType.TimeTz);
                (utc, zone) = (TimeZones.TimeTzBaseDate * TicksPerDay + t.L, t.Zone);
                break;
            }
            case VTag.TimeTz:
                (utc, zone) = (TimeZones.TimeTzBaseDate * TicksPerDay + v.L, v.Zone);
                break;
            case VTag.Timestamp:
            {
                var t = Conv.Cast(v, SqlType.TimestampTz);
                (utc, zone) = (t.L, t.Zone);
                break;
            }
            case VTag.TimestampTz:
                (utc, zone) = (v.L, v.Zone);
                break;
            case VTag.Date:
                throw new StatusBuilder().Gds(Err.isc_expression_eval_err).Gds(FbMessages.invalid_extractpart_date).Build();
            default:
                throw new StatusBuilder().Gds(Err.isc_expression_eval_err).Gds(FbMessages.invalidarg_extract).Build();
        }
        if (part == "TIMEZONE_NAME") return Value.Str(TimeZones.Name(zone));
        var offset = TimeZones.OffsetAt(zone, utc);
        var sign = offset < 0 ? -1 : 1;
        offset = Math.Abs(offset);
        return Value.Int(part == "TIMEZONE_HOUR" ? sign * (offset / 60) : sign * (offset % 60));
    }

    /// <summary>FIRST_DAY / LAST_DAY (OF YEAR | QUARTER | MONTH | WEEK FROM value).</summary>
    public static Value FirstLastDay(string part, bool last, in Value v)
    {
        // WITH TIME ZONE: computed on the local date and time, then back to UTC in the value's zone
        if (v.Tag == VTag.TimestampTz)
            return Value.TimestampTz(TimeZones.ToUtc(FirstLastDay(part, last, Value.Timestamp(TimeZones.ToLocal(v.L, v.Zone))).L, v.Zone), v.Zone);
        var (day, time) = v.Tag == VTag.Timestamp ? Conv.SplitTimestamp(v.L) : (v.L, 0L);
        if (v.Tag is not (VTag.Date or VTag.Timestamp)) throw Sysf(FbMessages.sysf_invalid_date_timestamp, last ? "LAST_DAY" : "FIRST_DAY");
        var d = Conv.DayToDate(day);
        DateOnly r = part switch
        {
            "YEAR" => last ? new DateOnly(d.Year, 12, 31) : new DateOnly(d.Year, 1, 1),
            "QUARTER" => QuarterStart(d) is var q && last ? q.AddMonths(3).AddDays(-1) : QuarterStart(d),
            "MONTH" => last ? new DateOnly(d.Year, d.Month, DateTime.DaysInMonth(d.Year, d.Month)) : new DateOnly(d.Year, d.Month, 1),
            // weeks start on Sunday
            _ => last ? d.AddDays(6 - (int)d.DayOfWeek) : d.AddDays(-(int)d.DayOfWeek),
        };
        var nd = Conv.DateToDay(r);
        if (!ValidDay(nd)) throw Range(v.Tag);
        return v.Tag == VTag.Timestamp ? Value.Timestamp(nd * TicksPerDay + time) : Value.Date(nd);

        static DateOnly QuarterStart(DateOnly d) => new(d.Year, (d.Month - 1) / 3 * 3 + 1, 1);
    }
}
