-- Dates and times: literals, arithmetic, EXTRACT, DATEADD/DATEDIFF for every unit, boundaries and leap years.
-- @setup
create table dtt (id integer not null primary key, d date, t time, ts timestamp);
insert into dtt values (1, '2024-02-29', '13:14:15.1234', '2024-02-29 13:14:15.1234');
insert into dtt values (2, '0001-01-01', '00:00:00', '0001-01-01 00:00:00');
insert into dtt values (3, '9999-12-31', '23:59:59.9999', '9999-12-31 23:59:59.9999');
insert into dtt values (4, '1900-02-28', '12:00:00', '2000-03-01 00:00:00.0001');
insert into dtt values (5, null, null, null);
-- @case
select id, dateadd(year, 1, d), dateadd(year, -1, ts), dateadd(1 year to t), dateadd(year, 1, ts) from dtt order by id
-- @case
select id, datediff(year, d, date '2025-03-01'), datediff(year, ts, timestamp '2024-03-01 00:00:00'), datediff(year from t to time '01:02:03') from dtt order by id
-- @case
select id, dateadd(month, 1, d), dateadd(month, -1, ts), dateadd(1 month to t), dateadd(month, 1, ts) from dtt order by id
-- @case
select id, datediff(month, d, date '2025-03-01'), datediff(month, ts, timestamp '2024-03-01 00:00:00'), datediff(month from t to time '01:02:03') from dtt order by id
-- @case
select id, dateadd(week, 1, d), dateadd(week, -1, ts), dateadd(1 week to t), dateadd(week, 1, ts) from dtt order by id
-- @case
select id, datediff(week, d, date '2025-03-01'), datediff(week, ts, timestamp '2024-03-01 00:00:00'), datediff(week from t to time '01:02:03') from dtt order by id
-- @case
select id, dateadd(day, 1, d), dateadd(day, -1, ts), dateadd(1 day to t), dateadd(day, 1, ts) from dtt order by id
-- @case
select id, datediff(day, d, date '2025-03-01'), datediff(day, ts, timestamp '2024-03-01 00:00:00'), datediff(day from t to time '01:02:03') from dtt order by id
-- @case
select id, dateadd(hour, 1, d), dateadd(hour, -1, ts), dateadd(1 hour to t), dateadd(hour, 1, ts) from dtt order by id
-- @case
select id, datediff(hour, d, date '2025-03-01'), datediff(hour, ts, timestamp '2024-03-01 00:00:00'), datediff(hour from t to time '01:02:03') from dtt order by id
-- @case
select id, dateadd(minute, 1, d), dateadd(minute, -1, ts), dateadd(1 minute to t), dateadd(minute, 1, ts) from dtt order by id
-- @case
select id, datediff(minute, d, date '2025-03-01'), datediff(minute, ts, timestamp '2024-03-01 00:00:00'), datediff(minute from t to time '01:02:03') from dtt order by id
-- @case
select id, dateadd(second, 1, d), dateadd(second, -1, ts), dateadd(1 second to t), dateadd(second, 1, ts) from dtt order by id
-- @case
select id, datediff(second, d, date '2025-03-01'), datediff(second, ts, timestamp '2024-03-01 00:00:00'), datediff(second from t to time '01:02:03') from dtt order by id
-- @case
select id, dateadd(millisecond, 1, d), dateadd(millisecond, -1, ts), dateadd(1 millisecond to t), dateadd(millisecond, 1, ts) from dtt order by id
-- @case
select id, datediff(millisecond, d, date '2025-03-01'), datediff(millisecond, ts, timestamp '2024-03-01 00:00:00'), datediff(millisecond from t to time '01:02:03') from dtt order by id
-- @case
select dateadd(year, 1, time '10:00:00') from rdb$database
-- @case
select dateadd(month, 1, time '10:00:00') from rdb$database
-- @case
select dateadd(day, 1, time '10:00:00') from rdb$database
-- @case
select dateadd(hour, 1, date '2024-01-01'), datediff(hour, date '2024-01-01', date '2024-01-02') from rdb$database
-- @case
select dateadd(minute, 1, date '2024-01-01'), datediff(minute, date '2024-01-01', date '2024-01-02') from rdb$database
-- @case
select dateadd(second, 1, date '2024-01-01'), datediff(second, date '2024-01-01', date '2024-01-02') from rdb$database
-- @case
select dateadd(millisecond, 1, date '2024-01-01'), datediff(millisecond, date '2024-01-01', date '2024-01-02') from rdb$database
-- @case
select dateadd(month, 0, date '2024-01-31'), dateadd(day, 0, timestamp '2024-01-31 10:00:00'), dateadd(second, 0, time '23:59:59') from rdb$database
-- @case
select dateadd(month, 1, date '2024-01-31'), dateadd(day, 1, timestamp '2024-01-31 10:00:00'), dateadd(second, 1, time '23:59:59') from rdb$database
-- @case
select dateadd(month, -1, date '2024-01-31'), dateadd(day, -1, timestamp '2024-01-31 10:00:00'), dateadd(second, -1, time '23:59:59') from rdb$database
-- @case
select dateadd(month, 12, date '2024-01-31'), dateadd(day, 12, timestamp '2024-01-31 10:00:00'), dateadd(second, 12, time '23:59:59') from rdb$database
-- @case
select dateadd(month, 13, date '2024-01-31'), dateadd(day, 13, timestamp '2024-01-31 10:00:00'), dateadd(second, 13, time '23:59:59') from rdb$database
-- @case
select dateadd(month, 2147483647, date '2024-01-31'), dateadd(day, 2147483647, timestamp '2024-01-31 10:00:00'), dateadd(second, 2147483647, time '23:59:59') from rdb$database
-- @case
select dateadd(month, -2147483648, date '2024-01-31'), dateadd(day, -2147483648, timestamp '2024-01-31 10:00:00'), dateadd(second, -2147483648, time '23:59:59') from rdb$database
-- @case
select dateadd(month, 1.5, date '2024-01-31'), dateadd(day, 1.5, timestamp '2024-01-31 10:00:00'), dateadd(second, 1.5, time '23:59:59') from rdb$database
-- @case
select dateadd(month, -1.5, date '2024-01-31'), dateadd(day, -1.5, timestamp '2024-01-31 10:00:00'), dateadd(second, -1.5, time '23:59:59') from rdb$database
-- @case
select dateadd(month, 0.4, date '2024-01-31'), dateadd(day, 0.4, timestamp '2024-01-31 10:00:00'), dateadd(second, 0.4, time '23:59:59') from rdb$database
-- @case
select dateadd(month, null, date '2024-01-31'), dateadd(day, null, timestamp '2024-01-31 10:00:00'), dateadd(second, null, time '23:59:59') from rdb$database
-- @case
select dateadd(month, 2.5e0, date '2024-01-31'), dateadd(day, 2.5e0, timestamp '2024-01-31 10:00:00'), dateadd(second, 2.5e0, time '23:59:59') from rdb$database
-- @case
select id, extract(year from d), extract(year from ts) from dtt order by id
-- @case
select id, extract(year from t) from dtt order by id
-- @case
select id, extract(month from d), extract(month from ts) from dtt order by id
-- @case
select id, extract(month from t) from dtt order by id
-- @case
select id, extract(day from d), extract(day from ts) from dtt order by id
-- @case
select id, extract(day from t) from dtt order by id
-- @case
select id, extract(hour from d), extract(hour from ts) from dtt order by id
-- @case
select id, extract(hour from t) from dtt order by id
-- @case
select id, extract(minute from d), extract(minute from ts) from dtt order by id
-- @case
select id, extract(minute from t) from dtt order by id
-- @case
select id, extract(second from d), extract(second from ts) from dtt order by id
-- @case
select id, extract(second from t) from dtt order by id
-- @case
select id, extract(millisecond from d), extract(millisecond from ts) from dtt order by id
-- @case
select id, extract(millisecond from t) from dtt order by id
-- @case
select id, extract(weekday from d), extract(weekday from ts) from dtt order by id
-- @case
select id, extract(weekday from t) from dtt order by id
-- @case
select id, extract(yearday from d), extract(yearday from ts) from dtt order by id
-- @case
select id, extract(yearday from t) from dtt order by id
-- @case
select id, extract(week from d), extract(week from ts) from dtt order by id
-- @case
select id, extract(week from t) from dtt order by id
-- @case
select id, extract(quarter from d), extract(quarter from ts) from dtt order by id
-- @case
select id, extract(quarter from t) from dtt order by id
-- @case
select extract(week from date '2024-01-01'), extract(weekday from date '2024-01-01'), extract(yearday from date '2024-01-01'), extract(quarter from date '2024-01-01') from rdb$database
-- @case
select extract(week from date '2024-12-30'), extract(weekday from date '2024-12-30'), extract(yearday from date '2024-12-30'), extract(quarter from date '2024-12-30') from rdb$database
-- @case
select extract(week from date '2024-12-31'), extract(weekday from date '2024-12-31'), extract(yearday from date '2024-12-31'), extract(quarter from date '2024-12-31') from rdb$database
-- @case
select extract(week from date '2021-01-03'), extract(weekday from date '2021-01-03'), extract(yearday from date '2021-01-03'), extract(quarter from date '2021-01-03') from rdb$database
-- @case
select extract(week from date '2021-01-04'), extract(weekday from date '2021-01-04'), extract(yearday from date '2021-01-04'), extract(quarter from date '2021-01-04') from rdb$database
-- @case
select extract(week from date '2020-12-31'), extract(weekday from date '2020-12-31'), extract(yearday from date '2020-12-31'), extract(quarter from date '2020-12-31') from rdb$database
-- @case
select extract(week from date '2027-01-01'), extract(weekday from date '2027-01-01'), extract(yearday from date '2027-01-01'), extract(quarter from date '2027-01-01') from rdb$database
-- @case
select extract(week from date '2026-12-28'), extract(weekday from date '2026-12-28'), extract(yearday from date '2026-12-28'), extract(quarter from date '2026-12-28') from rdb$database
-- @case
select extract(week from date '1999-01-01'), extract(weekday from date '1999-01-01'), extract(yearday from date '1999-01-01'), extract(quarter from date '1999-01-01') from rdb$database
-- @case
select d + 1, d - 1, 1 + d, d + 1.9, d - 1.9, d + 0.5, d + 1e0, d + 2.9e0 from dtt order by id
-- @case
select ts + 1, ts - 1, ts + 0.5, ts - 0.25, ts + 1.000001, ts + 0.00001, ts + 1e-9 from dtt order by id
-- @case
select t + 1, t - 1, t + 3600, t - 86400, t + 0.5, t + 0.00005, t + 86400.5 from dtt order by id
-- @case
select d - date '2024-01-01', ts - timestamp '2024-01-01 00:00:00', t - time '00:00:00' from dtt order by id
-- @case
select date '2024-03-01' - date '2024-02-01', timestamp '2024-01-01 12:00:00' - timestamp '2024-01-01 00:00:00', time '00:00:00' - time '23:59:59' from rdb$database
-- @case
select date '2024-01-01' + time '10:11:12', time '23:59:59.9999' + date '2024-12-31' from rdb$database
-- @case
select d + t from dtt order by id
-- @case
select date '9999-12-31' + 1 from rdb$database
-- @case
select date '0001-01-01' - 1 from rdb$database
-- @case
select timestamp '9999-12-31 23:59:59.9999' + 0.0001 from rdb$database
-- @case
select dateadd(year, 1, date '9999-01-01') from rdb$database
-- @case
select dateadd(day, -1, date '0001-01-01') from rdb$database
-- @case
select dateadd(year, 1, date '2024-02-29'), dateadd(year, 4, date '2024-02-29'), dateadd(month, 1, date '2024-01-31'), dateadd(month, -1, date '2024-03-31') from rdb$database
-- @case
select dateadd(month, 1, timestamp '2024-01-31 23:59:59.9999'), dateadd(month, 12, date '2023-02-28') from rdb$database
-- @case
select datediff(day, date '2024-01-01', date '2023-12-31'), datediff(year, date '2023-12-31', date '2024-01-01'), datediff(month, date '2024-01-31', date '2024-02-01') from rdb$database
-- @case
select datediff(year, timestamp '2023-06-15 10:00:00', timestamp '2024-06-15 09:59:59'), datediff(hour, timestamp '2024-01-01 10:59:59', timestamp '2024-01-01 11:00:00') from rdb$database
-- @case
select datediff(millisecond, timestamp '2024-01-01 00:00:00', timestamp '2024-01-01 00:00:00.0015'), datediff(second, time '00:00:00.9', time '00:00:01.1') from rdb$database
-- @case
select datediff(week, date '2024-01-01', date '2024-01-14'), datediff(week, date '2024-01-07', date '2024-01-08') from rdb$database
-- @case
select datediff(day, d, ts) from dtt order by id
-- @case
select datediff(day, t, ts) from dtt order by id
-- @case
select datediff(hour, d, t) from dtt where id = 1
-- @case
select dateadd(hour, 1, d) from dtt where id = 1
-- @case
select dateadd(1 weekday to d) from dtt where id = 1
-- @case
select extract(hour from d) from dtt where id = 1
-- @case
select extract(year from t) from dtt where id = 1
-- @case
select extract(year from 'x') from rdb$database
-- @case
select cast(d as timestamp), cast(ts as date), cast(ts as time), cast(t as timestamp) = current_date + t from dtt order by id
-- @case
select cast(d as varchar(10)), cast(t as varchar(13)), cast(ts as varchar(24)), cast(d as char(10)), cast(ts as char(30)) from dtt order by id
-- @case
select cast(d as varchar(9)) from dtt where id = 1
-- @case
select cast(ts as varchar(23)) from dtt where id = 1
-- @case
select cast(t as varchar(12)) from dtt where id = 1
-- @case
select d || '', t || '', ts || '' from dtt order by id
-- @case
select d = '2024-02-29', ts > '2024-02-29', t < '14:00', d between '2024-01-01' and '2024-12-31', ts = d from dtt order by id
-- @case
select d = cast(ts as date), d < ts, ts > d from dtt order by id
-- @case
select max(d), min(ts), max(t), count(distinct d) from dtt
-- @case
select id from dtt order by t desc nulls last
-- @case
select d - ts from dtt where id = 1
-- @case
select d * 2 from dtt
-- @case
select ts / 2 from dtt
-- @case
select -d from dtt
-- @case
select d + d from dtt
-- @case
select t + t from dtt
-- @case
select ts + t from dtt
-- @case
select date '2024-01-01' + timestamp '2024-01-01 00:00:00' from rdb$database
-- @case
select time '10:00' - date '2024-01-01' from rdb$database
-- @case
select 1 - date '2024-01-01' from rdb$database
-- @case
select date '2024-02-30' from rdb$database
-- @case
select date '2023-02-29' from rdb$database
-- @case
select date '2024-13-01' from rdb$database
-- @case
select date '24-01-01', date '1/2/2024', date '01.02.2024', date '2024.02.01', date '1-JAN-2024', date 'JAN 1, 2024' from rdb$database
-- @case
select date '2024-1-1', date '2024-01-01  ', date '  2024-01-01', timestamp '2024-01-01 1:2:3.4', time '1:2', time '1:02:03.12345' from rdb$database
-- @case
select time '24:00:00' from rdb$database
-- @case
select time '23:60:00' from rdb$database
-- @case
select timestamp '2024-01-01T10:00:00' from rdb$database
-- @case
select timestamp '2024-01-01 10:00:00.12345' from rdb$database
-- @case
select date 'today' from rdb$database
-- @case
select date '2024-01-01 10:00:00' from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as date), cast('13:14:15' as time), cast('2024-02-29' as timestamp) from rdb$database
-- @case
select cast('29.02.2024' as date), cast('02/29/2024' as date), cast('29-FEB-2024' as date), cast('FEBRUARY 29 2024' as date), cast('2024-feb-29' as date) from rdb$database
-- @case
select cast('2024-02-29 25:00' as timestamp) from rdb$database
-- @case
select cast('2024-02-29 10:00:00 extra' as timestamp) from rdb$database
-- @case
select cast('10:00:00.' as time), cast('10:00:00.5' as time), cast('10' as time) from rdb$database
-- @case
select cast(current_date as varchar(10)) = cast(cast('today' as date) as varchar(10)), cast('tomorrow' as date) - cast('yesterday' as date), current_date - cast('now' as date) from rdb$database
-- @case
select extract(year from current_date) = extract(year from localtimestamp), current_time is not null, char_length(cast(localtime as varchar(20))) from rdb$database
-- @case
select first_day(of month from date '2024-02-15'), last_day(of month from date '2024-02-15'), first_day(of year from date '2024-02-15'), last_day(of year from timestamp '2024-02-15 10:00:00') from rdb$database
-- @case
select first_day(of week from date '2024-02-15'), last_day(of week from date '2024-02-15'), first_day(of quarter from date '2024-05-15'), last_day(of quarter from date '2024-05-15') from rdb$database
