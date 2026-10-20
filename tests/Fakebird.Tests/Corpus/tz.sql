-- TIME and TIMESTAMP WITH TIME ZONE (Firebird 4+): literals, casts in both directions through the session zone,
-- regions and offsets, DST gaps and overlaps, AT TIME ZONE / AT LOCAL, SET TIME ZONE, comparisons in UTC, arithmetic,
-- DATEADD / DATEDIFF / EXTRACT / FIRST_DAY / LAST_DAY, CASE and UNION types, sorting and grouping, columns, keys,
-- defaults, DDL type changes, RDB$TIME_ZONES and the errors. The session zone of the servers is Etc/UTC; with
-- DataTypeCompatibility 3.0 zoned results reach the client as TIMESTAMP / TIME in that zone.
-- @setup
create table tz (id integer not null primary key, ts timestamp with time zone, t time with time zone, lts timestamp, lt time);
create table tzk (k timestamp with time zone not null primary key, n integer);
create table tzd (id integer not null, created timestamp default current_timestamp, createdz timestamp with time zone default current_timestamp);
insert into tz values (1, '2024-01-15 10:00:00 America/Sao_Paulo', '10:00:00 -03:00', '2024-01-15 10:00:00', '10:00:00');
insert into tz values (2, '2024-01-15 13:00:00 +00:00', '13:00:00 +00:00', '2024-01-15 13:00:00', '13:00:00');
insert into tz values (3, '2024-07-01 12:00:00 Europe/Berlin', '12:00:00 +02:00', '2024-07-01 12:00:00', '12:00:00');
insert into tz values (4, '2024-03-10 01:59:59.9999 America/New_York', '23:30:00 +05:30', '2024-03-10 01:59:59.9999', '23:30:00');
insert into tz values (5, null, null, null, null);
insert into tz values (6, '2024-01-15 16:00:00 +03:00', '16:00:00 +03:00', '2024-01-15 16:00:00', '16:00:00');
-- @case
select rdb$get_context('SYSTEM', 'SESSION_TIMEZONE') from rdb$database
-- @case
select char_length(cast(current_timestamp as varchar(60))), substring(cast(current_timestamp as varchar(60)) from 25), char_length(cast(current_time as varchar(60))), substring(cast(current_time as varchar(60)) from 14) from rdb$database
-- @case
select current_timestamp, current_time, localtimestamp, localtime, current_date from rdb$database where 1 = 0
-- @case
select cast(current_timestamp as timestamp) = localtimestamp, current_timestamp = localtimestamp, cast(current_time as time) = localtime, current_timestamp - localtimestamp, current_time - localtime from rdb$database
-- @case
select extract(timezone_hour from current_timestamp), extract(timezone_minute from current_time), extract(timezone_name from current_timestamp), extract(timezone_hour from localtimestamp) from rdb$database
-- @case
select cast(current_timestamp as date) = current_date, cast(localtimestamp as date) = current_date from rdb$database
-- @case
select char_length(cast(current_timestamp(0) as varchar(60))), char_length(cast(localtimestamp(2) as varchar(60))), char_length(cast(current_time(1) as varchar(60))) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 America/Sao_Paulo', cast(timestamp '2024-01-15 10:00:00 America/Sao_Paulo' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00', cast(timestamp '2024-01-15 10:00:00 +03:00' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 -03:30', cast(timestamp '2024-01-15 10:00:00 -03:30' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00', cast(timestamp '2024-01-15 10:00:00' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 UTC', cast(timestamp '2024-01-15 10:00:00 UTC' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 GMT', cast(timestamp '2024-01-15 10:00:00 GMT' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 Etc/GMT+3', cast(timestamp '2024-01-15 10:00:00 Etc/GMT+3' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00   europe/LONDON', cast(timestamp '2024-01-15 10:00:00   europe/LONDON' as varchar(60)) from rdb$database
-- @case
select timestamp '15.01.2024 10:00 +01:00', cast(timestamp '15.01.2024 10:00 +01:00' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 +01:00', cast(timestamp '2024-01-15 +01:00' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00.1234 Asia/Kolkata', cast(timestamp '2024-01-15 10:00:00.1234 Asia/Kolkata' as varchar(60)) from rdb$database
-- @case
select time '10:00:00 +05:00', cast(time '10:00:00 +05:00' as varchar(60)) from rdb$database
-- @case
select time '10:00:00 America/Sao_Paulo', cast(time '10:00:00 America/Sao_Paulo' as varchar(60)) from rdb$database
-- @case
select time '10:00', cast(time '10:00' as varchar(60)) from rdb$database
-- @case
select time '23:59:59.9999 -14:00', cast(time '23:59:59.9999 -14:00' as varchar(60)) from rdb$database
-- @case
select time '00:00:00 +14:00', cast(time '00:00:00 +14:00' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +14:00', cast(timestamp '2024-01-15 10:00:00 +14:00' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 - 05 : 30', cast(timestamp '2024-01-15 10:00:00 - 05 : 30' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +15:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +14:01' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +3' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 Mars/Olympus' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:60' from rdb$database
-- @case
select time '10:00 +03:00 x' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 America/Sao_Paulo x' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 -' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 3:00' from rdb$database
-- @case
select date '2024-01-15 +03:00' from rdb$database
-- @case
select cast('2024-01-15 10:00:00 America/Sao_Paulo' as timestamp with time zone), cast(cast('2024-01-15 10:00:00 America/Sao_Paulo' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast('2024-01-15 10:00:00 +03:00' as timestamp with time zone), cast(cast('2024-01-15 10:00:00 +03:00' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast('2024-01-15 10:00:00' as timestamp with time zone), cast(cast('2024-01-15 10:00:00' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast('2024-01-15' as timestamp with time zone), cast(cast('2024-01-15' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast('10:00:00 +05:00' as timestamp with time zone), cast(cast('10:00:00 +05:00' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast('15-JAN-2024 10:00 Europe/Paris' as timestamp with time zone), cast(cast('15-JAN-2024 10:00 Europe/Paris' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast('2024-02-30 10:00:00 +00:00' as timestamp with time zone), cast(cast('2024-02-30 10:00:00 +00:00' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast('2024-01-15 25:00:00 +00:00' as timestamp with time zone), cast(cast('2024-01-15 25:00:00 +00:00' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast('2024-01-15 10:00:00 Bogus/Zone' as timestamp with time zone), cast(cast('2024-01-15 10:00:00 Bogus/Zone' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast('  2024-01-15 10:00:00   -02:00  ' as timestamp with time zone), cast(cast('  2024-01-15 10:00:00   -02:00  ' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast('2024-01-15 10:00:00 +02:00:00' as timestamp with time zone), cast(cast('2024-01-15 10:00:00 +02:00:00' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast('10:00:00 +05:00' as time with time zone), cast(cast('10:00:00 +05:00' as time with time zone) as varchar(40)) from rdb$database
-- @case
select cast('10:00:00 America/Sao_Paulo' as time with time zone), cast(cast('10:00:00 America/Sao_Paulo' as time with time zone) as varchar(40)) from rdb$database
-- @case
select cast('10:00:00' as time with time zone), cast(cast('10:00:00' as time with time zone) as varchar(40)) from rdb$database
-- @case
select cast('24:00:00 +00:00' as time with time zone), cast(cast('24:00:00 +00:00' as time with time zone) as varchar(40)) from rdb$database
-- @case
select cast('10:00:00.5 -01:00' as time with time zone), cast(cast('10:00:00.5 -01:00' as time with time zone) as varchar(40)) from rdb$database
-- @case
select cast('10:00 Europe/Lisbon' as time with time zone), cast(cast('10:00 Europe/Lisbon' as time with time zone) as varchar(40)) from rdb$database
-- @case
select cast('2024-01-15 10:00:00 +03:00' as timestamp) from rdb$database
-- @case
select cast('2024-01-15 10:00:00 America/Sao_Paulo' as timestamp) from rdb$database
-- @case
select cast('10:00:00 +03:00' as time) from rdb$database
-- @case
select cast('2024-01-15 23:00:00 -03:00' as date) from rdb$database
-- @case
select cast('2024-01-15 10:00:00 +03:00' as date) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as timestamp with time zone), cast(cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as time with time zone), cast(cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as time with time zone) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as timestamp), cast(cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as timestamp) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as time), cast(cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as time) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as date), cast(cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as date) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as varchar(60)), cast(cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as varchar(60)) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as char(60)), cast(cast(timestamp '2024-01-15 22:30:00 America/Sao_Paulo' as char(60)) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 01:30:00 +05:00' as timestamp with time zone), cast(cast(timestamp '2024-01-15 01:30:00 +05:00' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 01:30:00 +05:00' as time with time zone), cast(cast(timestamp '2024-01-15 01:30:00 +05:00' as time with time zone) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 01:30:00 +05:00' as timestamp), cast(cast(timestamp '2024-01-15 01:30:00 +05:00' as timestamp) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 01:30:00 +05:00' as time), cast(cast(timestamp '2024-01-15 01:30:00 +05:00' as time) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 01:30:00 +05:00' as date), cast(cast(timestamp '2024-01-15 01:30:00 +05:00' as date) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 01:30:00 +05:00' as varchar(60)), cast(cast(timestamp '2024-01-15 01:30:00 +05:00' as varchar(60)) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 01:30:00 +05:00' as char(60)), cast(cast(timestamp '2024-01-15 01:30:00 +05:00' as char(60)) as varchar(60)) from rdb$database
-- @case
select cast(time '22:30:00 -03:00' as time with time zone), cast(cast(time '22:30:00 -03:00' as time with time zone) as varchar(60)) from rdb$database
-- @case
select cast(time '22:30:00 -03:00' as time), cast(cast(time '22:30:00 -03:00' as time) as varchar(60)) from rdb$database
-- @case
select cast(time '22:30:00 -03:00' as date) from rdb$database
-- @case
select cast(time '22:30:00 -03:00' as varchar(60)), cast(cast(time '22:30:00 -03:00' as varchar(60)) as varchar(60)) from rdb$database
-- @case
select cast(time '22:30:00 -03:00' as char(60)), cast(cast(time '22:30:00 -03:00' as char(60)) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00' as timestamp with time zone), cast(cast(timestamp '2024-01-15 22:30:00' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00' as time with time zone), cast(cast(timestamp '2024-01-15 22:30:00' as time with time zone) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00' as timestamp), cast(cast(timestamp '2024-01-15 22:30:00' as timestamp) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00' as time), cast(cast(timestamp '2024-01-15 22:30:00' as time) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00' as date), cast(cast(timestamp '2024-01-15 22:30:00' as date) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00' as varchar(60)), cast(cast(timestamp '2024-01-15 22:30:00' as varchar(60)) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 22:30:00' as char(60)), cast(cast(timestamp '2024-01-15 22:30:00' as char(60)) as varchar(60)) from rdb$database
-- @case
select cast(time '22:30:00' as time with time zone), cast(cast(time '22:30:00' as time with time zone) as varchar(60)) from rdb$database
-- @case
select cast(time '22:30:00' as time), cast(cast(time '22:30:00' as time) as varchar(60)) from rdb$database
-- @case
select cast(time '22:30:00' as date) from rdb$database
-- @case
select cast(time '22:30:00' as varchar(60)), cast(cast(time '22:30:00' as varchar(60)) as varchar(60)) from rdb$database
-- @case
select cast(time '22:30:00' as char(60)), cast(cast(time '22:30:00' as char(60)) as varchar(60)) from rdb$database
-- @case
select cast(date '2024-01-15' as timestamp with time zone), cast(cast(date '2024-01-15' as timestamp with time zone) as varchar(60)) from rdb$database
-- @case
select cast(date '2024-01-15' as time with time zone), cast(cast(date '2024-01-15' as time with time zone) as varchar(60)) from rdb$database
-- @case
select cast(date '2024-01-15' as timestamp), cast(cast(date '2024-01-15' as timestamp) as varchar(60)) from rdb$database
-- @case
select cast(date '2024-01-15' as time), cast(cast(date '2024-01-15' as time) as varchar(60)) from rdb$database
-- @case
select cast(date '2024-01-15' as date), cast(cast(date '2024-01-15' as date) as varchar(60)) from rdb$database
-- @case
select cast(date '2024-01-15' as varchar(60)), cast(cast(date '2024-01-15' as varchar(60)) as varchar(60)) from rdb$database
-- @case
select cast(date '2024-01-15' as char(60)), cast(cast(date '2024-01-15' as char(60)) as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' as integer) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' as integer) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' as bigint) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' as bigint) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' as double precision) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' as double precision) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' as numeric(18,4)) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' as numeric(18,4)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' as boolean) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' as boolean) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' as blob sub_type text) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' as blob sub_type text) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' as varchar(10)) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' as varchar(10)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' as char(20)) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' as char(20)) from rdb$database
-- @case
select cast(12 as timestamp with time zone) from rdb$database
-- @case
select cast(true as time with time zone) from rdb$database
-- @case
select cast(1.5e0 as timestamp with time zone) from rdb$database
-- @case
select cast(null as timestamp with time zone), cast(null as time with time zone) from rdb$database
-- @case
select cast('2024-03-10 01:59:59 America/New_York' as timestamp with time zone), cast(cast('2024-03-10 01:59:59 America/New_York' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-03-10 01:59:59 America/New_York' as timestamp with time zone)), extract(timezone_minute from cast('2024-03-10 01:59:59 America/New_York' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-03-10 02:00:00 America/New_York' as timestamp with time zone), cast(cast('2024-03-10 02:00:00 America/New_York' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-03-10 02:00:00 America/New_York' as timestamp with time zone)), extract(timezone_minute from cast('2024-03-10 02:00:00 America/New_York' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-03-10 02:30:00 America/New_York' as timestamp with time zone), cast(cast('2024-03-10 02:30:00 America/New_York' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-03-10 02:30:00 America/New_York' as timestamp with time zone)), extract(timezone_minute from cast('2024-03-10 02:30:00 America/New_York' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-03-10 03:00:00 America/New_York' as timestamp with time zone), cast(cast('2024-03-10 03:00:00 America/New_York' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-03-10 03:00:00 America/New_York' as timestamp with time zone)), extract(timezone_minute from cast('2024-03-10 03:00:00 America/New_York' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-11-03 00:59:59 America/New_York' as timestamp with time zone), cast(cast('2024-11-03 00:59:59 America/New_York' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-11-03 00:59:59 America/New_York' as timestamp with time zone)), extract(timezone_minute from cast('2024-11-03 00:59:59 America/New_York' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-11-03 01:00:00 America/New_York' as timestamp with time zone), cast(cast('2024-11-03 01:00:00 America/New_York' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-11-03 01:00:00 America/New_York' as timestamp with time zone)), extract(timezone_minute from cast('2024-11-03 01:00:00 America/New_York' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-11-03 01:30:00 America/New_York' as timestamp with time zone), cast(cast('2024-11-03 01:30:00 America/New_York' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-11-03 01:30:00 America/New_York' as timestamp with time zone)), extract(timezone_minute from cast('2024-11-03 01:30:00 America/New_York' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-11-03 02:00:00 America/New_York' as timestamp with time zone), cast(cast('2024-11-03 02:00:00 America/New_York' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-11-03 02:00:00 America/New_York' as timestamp with time zone)), extract(timezone_minute from cast('2024-11-03 02:00:00 America/New_York' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-03-31 02:30:00 Europe/Berlin' as timestamp with time zone), cast(cast('2024-03-31 02:30:00 Europe/Berlin' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-03-31 02:30:00 Europe/Berlin' as timestamp with time zone)), extract(timezone_minute from cast('2024-03-31 02:30:00 Europe/Berlin' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-10-27 02:30:00 Europe/Berlin' as timestamp with time zone), cast(cast('2024-10-27 02:30:00 Europe/Berlin' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-10-27 02:30:00 Europe/Berlin' as timestamp with time zone)), extract(timezone_minute from cast('2024-10-27 02:30:00 Europe/Berlin' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-10-27 03:00:00 Europe/Berlin' as timestamp with time zone), cast(cast('2024-10-27 03:00:00 Europe/Berlin' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-10-27 03:00:00 Europe/Berlin' as timestamp with time zone)), extract(timezone_minute from cast('2024-10-27 03:00:00 Europe/Berlin' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-04-07 02:30:00 Australia/Sydney' as timestamp with time zone), cast(cast('2024-04-07 02:30:00 Australia/Sydney' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-04-07 02:30:00 Australia/Sydney' as timestamp with time zone)), extract(timezone_minute from cast('2024-04-07 02:30:00 Australia/Sydney' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-10-06 02:30:00 Australia/Sydney' as timestamp with time zone), cast(cast('2024-10-06 02:30:00 Australia/Sydney' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-10-06 02:30:00 Australia/Sydney' as timestamp with time zone)), extract(timezone_minute from cast('2024-10-06 02:30:00 Australia/Sydney' as timestamp with time zone)) from rdb$database
-- @case
select cast('2018-11-04 00:30:00 America/Sao_Paulo' as timestamp with time zone), cast(cast('2018-11-04 00:30:00 America/Sao_Paulo' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2018-11-04 00:30:00 America/Sao_Paulo' as timestamp with time zone)), extract(timezone_minute from cast('2018-11-04 00:30:00 America/Sao_Paulo' as timestamp with time zone)) from rdb$database
-- @case
select cast('2019-02-16 23:30:00 America/Sao_Paulo' as timestamp with time zone), cast(cast('2019-02-16 23:30:00 America/Sao_Paulo' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2019-02-16 23:30:00 America/Sao_Paulo' as timestamp with time zone)), extract(timezone_minute from cast('2019-02-16 23:30:00 America/Sao_Paulo' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-03-10 02:30:00 US/Eastern' as timestamp with time zone), cast(cast('2024-03-10 02:30:00 US/Eastern' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-03-10 02:30:00 US/Eastern' as timestamp with time zone)), extract(timezone_minute from cast('2024-03-10 02:30:00 US/Eastern' as timestamp with time zone)) from rdb$database
-- @case
select cast('1980-06-01 12:00:00 Europe/London' as timestamp with time zone), cast(cast('1980-06-01 12:00:00 Europe/London' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('1980-06-01 12:00:00 Europe/London' as timestamp with time zone)), extract(timezone_minute from cast('1980-06-01 12:00:00 Europe/London' as timestamp with time zone)) from rdb$database
-- @case
select cast('1900-01-01 00:00:00 Europe/Amsterdam' as timestamp with time zone), cast(cast('1900-01-01 00:00:00 Europe/Amsterdam' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('1900-01-01 00:00:00 Europe/Amsterdam' as timestamp with time zone)), extract(timezone_minute from cast('1900-01-01 00:00:00 Europe/Amsterdam' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-06-01 12:00:00 Asia/Kathmandu' as timestamp with time zone), cast(cast('2024-06-01 12:00:00 Asia/Kathmandu' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-06-01 12:00:00 Asia/Kathmandu' as timestamp with time zone)), extract(timezone_minute from cast('2024-06-01 12:00:00 Asia/Kathmandu' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-06-01 12:00:00 Pacific/Chatham' as timestamp with time zone), cast(cast('2024-06-01 12:00:00 Pacific/Chatham' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-06-01 12:00:00 Pacific/Chatham' as timestamp with time zone)), extract(timezone_minute from cast('2024-06-01 12:00:00 Pacific/Chatham' as timestamp with time zone)) from rdb$database
-- @case
select cast('2024-06-01 12:00:00 Australia/Eucla' as timestamp with time zone), cast(cast('2024-06-01 12:00:00 Australia/Eucla' as timestamp with time zone) as varchar(60)), extract(timezone_hour from cast('2024-06-01 12:00:00 Australia/Eucla' as timestamp with time zone)), extract(timezone_minute from cast('2024-06-01 12:00:00 Australia/Eucla' as timestamp with time zone)) from rdb$database
-- @case
select dateadd(1 hour to timestamp '2024-03-10 01:30:00 America/New_York'), cast(dateadd(1 hour to timestamp '2024-03-10 01:30:00 America/New_York') as varchar(60)) from rdb$database
-- @case
select cast(dateadd(1 day to timestamp '2024-03-09 02:30:00 America/New_York') as varchar(60)), cast(dateadd(-1 day to timestamp '2024-11-04 01:30:00 America/New_York') as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-03-10 01:30:00 America/New_York' + 1 as varchar(60)), cast(timestamp '2024-03-10 01:30:00 America/New_York' + 0.5 as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00' at time zone 'America/Sao_Paulo' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00' at time zone '+05:30' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00' at time zone '-00:00' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00' at time zone 'utc' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00' at time zone ' Asia/Tokyo ' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00' at time zone 'Europe/Berlin' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00' at local, cast(timestamp '2024-01-15 10:00:00' at local as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' at time zone 'America/Sao_Paulo' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' at time zone '+05:30' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' at time zone '-00:00' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' at time zone 'utc' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' at time zone ' Asia/Tokyo ' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' at time zone 'Europe/Berlin' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' at local, cast(timestamp '2024-01-15 10:00:00 +03:00' at local as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00' at time zone 'America/Sao_Paulo' as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00' at time zone '+05:30' as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00' at time zone '-00:00' as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00' at time zone 'utc' as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00' at time zone ' Asia/Tokyo ' as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00' at time zone 'Europe/Berlin' as varchar(60)) from rdb$database
-- @case
select time '10:00:00' at local, cast(time '10:00:00' at local as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' at time zone 'America/Sao_Paulo' as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' at time zone '+05:30' as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' at time zone '-00:00' as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' at time zone 'utc' as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' at time zone ' Asia/Tokyo ' as varchar(60)) from rdb$database
-- @case
select cast(time '10:00:00 +03:00' at time zone 'Europe/Berlin' as varchar(60)) from rdb$database
-- @case
select time '10:00:00 +03:00' at local, cast(time '10:00:00 +03:00' at local as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-07-01 10:00:00 Europe/Berlin' at time zone 'America/Sao_Paulo' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-07-01 10:00:00 Europe/Berlin' at time zone '+05:30' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-07-01 10:00:00 Europe/Berlin' at time zone '-00:00' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-07-01 10:00:00 Europe/Berlin' at time zone 'utc' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-07-01 10:00:00 Europe/Berlin' at time zone ' Asia/Tokyo ' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-07-01 10:00:00 Europe/Berlin' at time zone 'Europe/Berlin' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-07-01 10:00:00 Europe/Berlin' at local, cast(timestamp '2024-07-01 10:00:00 Europe/Berlin' at local as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00' at time zone 'America/Sao_Paulo', time '10:00:00' at time zone '-02:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00' at time zone 'Nowhere' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00' at time zone '+25:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00' at time zone null from rdb$database
-- @case
select cast(null as timestamp) at time zone 'UTC' from rdb$database
-- @case
select date '2024-01-15' at time zone 'UTC' from rdb$database
-- @case
select '2024-01-15 10:00:00' at time zone 'UTC' from rdb$database
-- @case
select 1 at local from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00' at time zone 'America/Sao_Paulo' at time zone 'Asia/Tokyo' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00' at time zone 'America/' || 'Sao_Paulo' as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00' at time zone ('America/' || 'Sao_Paulo') as varchar(60)) from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +01:00' at time zone 'UTC' collate unicode as varchar(60)) from rdb$database
-- @case
select cast(ts at time zone 'Asia/Tokyo' as varchar(60)), cast(lts at time zone 'Asia/Tokyo' as varchar(60)), cast(t at local as varchar(40)) from tz order by id
-- @case params: ["2024-01-15T10:00:00"]
select cast(? at time zone 'America/Sao_Paulo' as varchar(60)) from rdb$database
-- @case params: ["Europe/Rome"]
select cast(timestamp '2024-01-15 10:00:00' at time zone ? as varchar(60)) from rdb$database
-- @case params: ["+99:00"]
select cast(timestamp '2024-01-15 10:00:00' at time zone ? as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' = timestamp '2024-01-15 07:00:00 +00:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' = timestamp '2024-01-15 07:00:00' from rdb$database
-- @case
select timestamp '2024-01-15 07:00:00' = timestamp '2024-01-15 10:00:00 +03:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 America/Sao_Paulo' > timestamp '2024-01-15 12:59:59 +00:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 America/Sao_Paulo' < timestamp '2024-01-15 13:00:00.0001 UTC' from rdb$database
-- @case
select time '10:00:00 +03:00' = time '07:00:00' from rdb$database
-- @case
select time '10:00:00 +03:00' = time '07:00:00 +00:00' from rdb$database
-- @case
select time '01:00:00 +03:00' < time '23:00:00 +00:00' from rdb$database
-- @case
select timestamp '2024-01-15 00:00:00 +00:00' = date '2024-01-15' from rdb$database
-- @case
select date '2024-01-15' < timestamp '2024-01-15 00:00:00 -00:01' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' = '2024-01-15 07:00:00' from rdb$database
-- @case
select '2024-01-15 10:00:00 +03:00' = timestamp '2024-01-15 07:00:00 UTC' from rdb$database
-- @case
select time '10:00:00 +03:00' = '07:00:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' is distinct from timestamp '2024-01-15 07:00:00 +00:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' = time '07:00:00' from rdb$database
-- @case
select time '07:00:00 +00:00' = date '2024-01-15' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' = 1 from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' between timestamp '2024-01-15 07:00:00' and timestamp '2024-01-15 07:00:00 -00:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' in (timestamp '2024-01-15 07:00:00', timestamp '2024-01-15 10:00:00') from rdb$database
-- @case
select id from tz where ts = timestamp '2024-01-15 13:00:00 UTC' order by id
-- @case
select id from tz where ts = '2024-01-15 13:00:00' order by id
-- @case
select id from tz where t = time '13:00:00' order by id
-- @case
select id from tz where ts > lts order by id
-- @case
select id from tz where ts between timestamp '2024-01-15 12:00:00 +00:00' and timestamp '2024-07-01 10:00:00' order by id
-- @case
select a.id, b.id from tz a join tz b on a.ts = b.ts and a.id < b.id order by 1, 2
-- @case
select id, ts = lts, t = lt, ts > lts, t < lt from tz order by id
-- @case
select id, cast(ts as varchar(60)) from tz order by ts, id
-- @case
select id, cast(ts as varchar(60)) from tz order by ts desc nulls last, id
-- @case
select id, cast(t as varchar(40)) from tz order by t, id
-- @case
select cast(ts as varchar(60)), count(*) from tz group by ts order by 1
-- @case
select count(distinct ts), count(distinct t), count(*) from tz
-- @case
select cast(min(ts) as varchar(60)), cast(max(ts) as varchar(60)), cast(min(t) as varchar(40)), cast(max(t) as varchar(40)) from tz
-- @case
select min(ts), max(lts) from tz
-- @case
select cast(list(cast(ts as varchar(60)), '|') as varchar(500)) from (select ts from tz order by id)
-- @case
select distinct cast(ts as varchar(60)) from tz where id in (2, 6) order by 1
-- @case
select cast(x as varchar(60)) from (select distinct ts x from tz where id in (2, 6))
-- @case
select cast(ts as varchar(60)), id from tz where id in (2, 6) order by ts, id desc
-- @case
select timestamp '2024-01-15 23:00:00 -03:00' + 1, cast(timestamp '2024-01-15 23:00:00 -03:00' + 1 as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 23:00:00 -03:00' - 1.25, cast(timestamp '2024-01-15 23:00:00 -03:00' - 1.25 as varchar(60)) from rdb$database
-- @case
select 1 + timestamp '2024-01-15 23:00:00 Asia/Tokyo', cast(1 + timestamp '2024-01-15 23:00:00 Asia/Tokyo' as varchar(60)) from rdb$database
-- @case
select time '23:00:00 -03:00' + 3600, cast(time '23:00:00 -03:00' + 3600 as varchar(60)) from rdb$database
-- @case
select time '23:00:00 +03:00' - 86401, cast(time '23:00:00 +03:00' - 86401 as varchar(60)) from rdb$database
-- @case
select 7200 + time '23:00:00 Europe/Berlin', cast(7200 + time '23:00:00 Europe/Berlin' as varchar(60)) from rdb$database
-- @case
select date '2024-01-15' + time '10:00:00 +03:00', cast(date '2024-01-15' + time '10:00:00 +03:00' as varchar(60)) from rdb$database
-- @case
select time '10:00:00 +03:00' + date '2024-01-15', cast(time '10:00:00 +03:00' + date '2024-01-15' as varchar(60)) from rdb$database
-- @case
select date '2024-01-15' + time '10:00:00 America/Sao_Paulo', cast(date '2024-01-15' + time '10:00:00 America/Sao_Paulo' as varchar(60)) from rdb$database
-- @case
select timestamp '2024-12-31 23:00:00 +00:00' + 0.5, cast(timestamp '2024-12-31 23:00:00 +00:00' + 0.5 as varchar(60)) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' - timestamp '2024-01-15 10:00:00 +00:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' - timestamp '2024-01-15 07:00:00' from rdb$database
-- @case
select timestamp '2024-01-15 07:00:00' - timestamp '2024-01-15 10:00:00 +03:00' from rdb$database
-- @case
select timestamp '2024-01-16 00:00:00 America/Sao_Paulo' - timestamp '2024-01-15 00:00:00 Asia/Tokyo' from rdb$database
-- @case
select time '10:00:00 +03:00' - time '10:00:00 +00:00' from rdb$database
-- @case
select time '10:00:00 +03:00' - time '07:00:00' from rdb$database
-- @case
select time '07:00:00' - time '10:00:00 +03:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' - date '2024-01-15' from rdb$database
-- @case
select date '2024-01-16' - timestamp '2024-01-15 10:00:00 +03:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' - time '10:00:00' from rdb$database
-- @case
select time '10:00:00 +03:00' - timestamp '2024-01-15 10:00:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' + timestamp '2024-01-15 10:00:00 +03:00' from rdb$database
-- @case
select time '10:00:00 +03:00' + time '10:00:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' * 2 from rdb$database
-- @case
select time '10:00:00 +03:00' / 2 from rdb$database
-- @case
select -timestamp '2024-01-15 10:00:00 +03:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' - '2024-01-15 07:00:00' from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' + '1' from rdb$database
-- @case
select date '2024-01-15' + time '10:00:00 +03:00' + 1 from rdb$database
-- @case
select timestamp '9999-12-31 23:00:00 -03:00' + 1 from rdb$database
-- @case
select id, cast(ts + 1 as varchar(60)), ts - lts, t - lt, cast(t + 60 as varchar(40)) from tz order by id
-- @case
select cast(dateadd(2 year to timestamp '2024-01-31 22:30:00 America/Sao_Paulo') as varchar(60)) from rdb$database
-- @case
select cast(dateadd(2 month to timestamp '2024-01-31 22:30:00 America/Sao_Paulo') as varchar(60)) from rdb$database
-- @case
select cast(dateadd(2 week to timestamp '2024-01-31 22:30:00 America/Sao_Paulo') as varchar(60)) from rdb$database
-- @case
select cast(dateadd(2 day to timestamp '2024-01-31 22:30:00 America/Sao_Paulo') as varchar(60)) from rdb$database
-- @case
select cast(dateadd(2 hour to timestamp '2024-01-31 22:30:00 America/Sao_Paulo') as varchar(60)) from rdb$database
-- @case
select cast(dateadd(2 minute to timestamp '2024-01-31 22:30:00 America/Sao_Paulo') as varchar(60)) from rdb$database
-- @case
select cast(dateadd(2 second to timestamp '2024-01-31 22:30:00 America/Sao_Paulo') as varchar(60)) from rdb$database
-- @case
select cast(dateadd(2 millisecond to timestamp '2024-01-31 22:30:00 America/Sao_Paulo') as varchar(60)) from rdb$database
-- @case
select cast(dateadd(hour, -90, time '00:30:00 +05:00') as varchar(40)) from rdb$database
-- @case
select cast(dateadd(minute, -90, time '00:30:00 +05:00') as varchar(40)) from rdb$database
-- @case
select cast(dateadd(second, -90, time '00:30:00 +05:00') as varchar(40)) from rdb$database
-- @case
select cast(dateadd(millisecond, -90, time '00:30:00 +05:00') as varchar(40)) from rdb$database
-- @case
select cast(dateadd(day, -90, time '00:30:00 +05:00') as varchar(40)) from rdb$database
-- @case
select datediff(year, timestamp '2023-12-31 22:00:00 -03:00', timestamp '2024-01-01 02:00:00 +00:00'), datediff(year from timestamp '2024-01-01 01:00:00' to timestamp '2024-01-01 01:00:00 +03:00') from rdb$database
-- @case
select datediff(quarter, timestamp '2023-12-31 22:00:00 -03:00', timestamp '2024-01-01 02:00:00 +00:00'), datediff(quarter from timestamp '2024-01-01 01:00:00' to timestamp '2024-01-01 01:00:00 +03:00') from rdb$database
-- @case
select datediff(month, timestamp '2023-12-31 22:00:00 -03:00', timestamp '2024-01-01 02:00:00 +00:00'), datediff(month from timestamp '2024-01-01 01:00:00' to timestamp '2024-01-01 01:00:00 +03:00') from rdb$database
-- @case
select datediff(week, timestamp '2023-12-31 22:00:00 -03:00', timestamp '2024-01-01 02:00:00 +00:00'), datediff(week from timestamp '2024-01-01 01:00:00' to timestamp '2024-01-01 01:00:00 +03:00') from rdb$database
-- @case
select datediff(day, timestamp '2023-12-31 22:00:00 -03:00', timestamp '2024-01-01 02:00:00 +00:00'), datediff(day from timestamp '2024-01-01 01:00:00' to timestamp '2024-01-01 01:00:00 +03:00') from rdb$database
-- @case
select datediff(hour, timestamp '2023-12-31 22:00:00 -03:00', timestamp '2024-01-01 02:00:00 +00:00'), datediff(hour from timestamp '2024-01-01 01:00:00' to timestamp '2024-01-01 01:00:00 +03:00') from rdb$database
-- @case
select datediff(minute, timestamp '2023-12-31 22:00:00 -03:00', timestamp '2024-01-01 02:00:00 +00:00'), datediff(minute from timestamp '2024-01-01 01:00:00' to timestamp '2024-01-01 01:00:00 +03:00') from rdb$database
-- @case
select datediff(second, timestamp '2023-12-31 22:00:00 -03:00', timestamp '2024-01-01 02:00:00 +00:00'), datediff(second from timestamp '2024-01-01 01:00:00' to timestamp '2024-01-01 01:00:00 +03:00') from rdb$database
-- @case
select datediff(millisecond, timestamp '2023-12-31 22:00:00 -03:00', timestamp '2024-01-01 02:00:00 +00:00'), datediff(millisecond from timestamp '2024-01-01 01:00:00' to timestamp '2024-01-01 01:00:00 +03:00') from rdb$database
-- @case
select datediff(hour, time '10:00:00 +03:00', time '10:00:00 -02:00'), datediff(hour, time '10:00:00', time '10:00:00 +03:00') from rdb$database
-- @case
select datediff(minute, time '10:00:00 +03:00', time '10:00:00 -02:00'), datediff(minute, time '10:00:00', time '10:00:00 +03:00') from rdb$database
-- @case
select datediff(second, time '10:00:00 +03:00', time '10:00:00 -02:00'), datediff(second, time '10:00:00', time '10:00:00 +03:00') from rdb$database
-- @case
select datediff(millisecond, time '10:00:00 +03:00', time '10:00:00 -02:00'), datediff(millisecond, time '10:00:00', time '10:00:00 +03:00') from rdb$database
-- @case
select datediff(day, time '10:00:00 +03:00', time '10:00:00 -02:00'), datediff(day, time '10:00:00', time '10:00:00 +03:00') from rdb$database
-- @case
select datediff(hour, timestamp '2024-01-15 10:00:00 +03:00', time '10:00:00') from rdb$database
-- @case
select datediff(hour, date '2024-01-15', time '10:00:00 +03:00') from rdb$database
-- @case
select datediff(day, date '2024-01-15', timestamp '2024-01-15 23:00:00 -03:00') from rdb$database
-- @case
select extract(year from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(year from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(quarter from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(quarter from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(month from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(month from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(week from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(week from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(day from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(day from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(weekday from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(weekday from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(yearday from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(yearday from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(hour from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(hour from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(minute from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(minute from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(second from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(second from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(millisecond from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(millisecond from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(timezone_hour from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(timezone_hour from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(timezone_minute from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(timezone_minute from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(timezone_name from timestamp '2024-12-31 22:30:45.6789 America/Sao_Paulo'), extract(timezone_name from timestamp '2025-01-01 01:30:45.6789 +03:00') from rdb$database
-- @case
select extract(hour from time '23:30:45.6789 -09:30'), extract(hour from time '23:30:45.6789'), extract(hour from timestamp '2024-01-15 10:00:00') from rdb$database
-- @case
select extract(minute from time '23:30:45.6789 -09:30'), extract(minute from time '23:30:45.6789'), extract(minute from timestamp '2024-01-15 10:00:00') from rdb$database
-- @case
select extract(second from time '23:30:45.6789 -09:30'), extract(second from time '23:30:45.6789'), extract(second from timestamp '2024-01-15 10:00:00') from rdb$database
-- @case
select extract(millisecond from time '23:30:45.6789 -09:30'), extract(millisecond from time '23:30:45.6789'), extract(millisecond from timestamp '2024-01-15 10:00:00') from rdb$database
-- @case
select extract(timezone_hour from time '23:30:45.6789 -09:30'), extract(timezone_hour from time '23:30:45.6789'), extract(timezone_hour from timestamp '2024-01-15 10:00:00') from rdb$database
-- @case
select extract(timezone_minute from time '23:30:45.6789 -09:30'), extract(timezone_minute from time '23:30:45.6789'), extract(timezone_minute from timestamp '2024-01-15 10:00:00') from rdb$database
-- @case
select extract(timezone_name from time '23:30:45.6789 -09:30'), extract(timezone_name from time '23:30:45.6789'), extract(timezone_name from timestamp '2024-01-15 10:00:00') from rdb$database
-- @case
select extract(year from time '10:00:00 +03:00') from rdb$database
-- @case
select extract(day from time '10:00:00 +03:00') from rdb$database
-- @case
select extract(weekday from time '10:00:00 +03:00') from rdb$database
-- @case
select extract(timezone_hour from date '2024-01-15') from rdb$database
-- @case
select extract(timezone_hour from 1) from rdb$database
-- @case
select extract(timezone_name from date '2024-01-15') from rdb$database
-- @case
select extract(timezone_name from 1) from rdb$database
-- @case
select extract(timezone_hour from cast(null as timestamp with time zone)), extract(timezone_name from cast(null as time)) from rdb$database
-- @case
select extract(timezone_hour from timestamp '2024-01-15 10:00:00 -03:30'), extract(timezone_minute from timestamp '2024-01-15 10:00:00 -03:30'), extract(timezone_hour from timestamp '2024-01-15 10:00:00 -00:30'), extract(timezone_minute from timestamp '2024-01-15 10:00:00 -00:30') from rdb$database
-- @case
select cast(first_day(of year from timestamp '2024-03-10 01:30:00 America/New_York') as varchar(60)), cast(first_day(of year from timestamp '2024-01-01 01:00:00 +03:00') as varchar(60)) from rdb$database
-- @case
select cast(last_day(of year from timestamp '2024-03-10 01:30:00 America/New_York') as varchar(60)), cast(last_day(of year from timestamp '2024-01-01 01:00:00 +03:00') as varchar(60)) from rdb$database
-- @case
select cast(first_day(of quarter from timestamp '2024-03-10 01:30:00 America/New_York') as varchar(60)), cast(first_day(of quarter from timestamp '2024-01-01 01:00:00 +03:00') as varchar(60)) from rdb$database
-- @case
select cast(last_day(of quarter from timestamp '2024-03-10 01:30:00 America/New_York') as varchar(60)), cast(last_day(of quarter from timestamp '2024-01-01 01:00:00 +03:00') as varchar(60)) from rdb$database
-- @case
select cast(first_day(of month from timestamp '2024-03-10 01:30:00 America/New_York') as varchar(60)), cast(first_day(of month from timestamp '2024-01-01 01:00:00 +03:00') as varchar(60)) from rdb$database
-- @case
select cast(last_day(of month from timestamp '2024-03-10 01:30:00 America/New_York') as varchar(60)), cast(last_day(of month from timestamp '2024-01-01 01:00:00 +03:00') as varchar(60)) from rdb$database
-- @case
select cast(first_day(of week from timestamp '2024-03-10 01:30:00 America/New_York') as varchar(60)), cast(first_day(of week from timestamp '2024-01-01 01:00:00 +03:00') as varchar(60)) from rdb$database
-- @case
select cast(last_day(of week from timestamp '2024-03-10 01:30:00 America/New_York') as varchar(60)), cast(last_day(of week from timestamp '2024-01-01 01:00:00 +03:00') as varchar(60)) from rdb$database
-- @case
select first_day(of month from timestamp '2024-03-10 01:30:00 America/New_York'), last_day(of year from timestamp '2024-12-31 23:00:00 -03:00') from rdb$database
-- @case
select first_day(of month from time '10:00:00 +03:00') from rdb$database
-- @case
select last_day(of week from time '10:00:00') from rdb$database
-- @case
select coalesce(timestamp '2024-01-15 10:00:00 +03:00', timestamp '2024-01-15 10:00:00'), cast(coalesce(cast(null as timestamp), timestamp '2024-01-15 10:00:00 +03:00') as varchar(60)) from rdb$database
-- @case
select cast(coalesce(timestamp '2024-01-15 10:00:00', timestamp '2024-01-15 10:00:00 +03:00') as varchar(60)) from rdb$database
-- @case
select cast(coalesce(time '10:00:00', time '10:00:00 +03:00') as varchar(40)) from rdb$database
-- @case
select coalesce(time '10:00:00 +03:00', timestamp '2024-01-15 10:00:00') from rdb$database
-- @case
select coalesce(date '2024-01-15', timestamp '2024-01-15 10:00:00 +03:00') from rdb$database
-- @case
select cast(case when 1 = 1 then timestamp '2024-01-15 10:00:00' else timestamp '2024-01-15 10:00:00 Asia/Tokyo' end as varchar(60)) from rdb$database
-- @case
select iif(1 = 0, time '10:00:00 +01:00', time '10:00:00') from rdb$database
-- @case
select nullif(timestamp '2024-01-15 10:00:00 +03:00', timestamp '2024-01-15 07:00:00') from rdb$database
-- @case
select cast(maxvalue(timestamp '2024-01-15 10:00:00 +03:00', timestamp '2024-01-15 08:00:00') as varchar(60)), cast(minvalue(time '10:00:00 +03:00', time '08:00:00') as varchar(40)) from rdb$database
-- @case
select decode(timestamp '2024-01-15 10:00:00 +03:00', timestamp '2024-01-15 07:00:00', 'same', 'other') from rdb$database
-- @case
select cast(x as varchar(60)) from (select timestamp '2024-01-15 10:00:00' x from rdb$database union all select timestamp '2024-01-15 10:00:00 -05:00' from rdb$database) order by 1
-- @case
select x from (select time '10:00:00' x from rdb$database union all select time '10:00:00 -05:00' from rdb$database) order by 1
-- @case
select cast(x as varchar(60)) from (select timestamp '2024-01-15 10:00:00 +03:00' x from rdb$database union select timestamp '2024-01-15 07:00:00 UTC' from rdb$database)
-- @case
select x from (select date '2024-01-15' x from rdb$database union all select timestamp '2024-01-15 10:00:00 +03:00' from rdb$database)
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' || '', time '10:00:00 +03:00' || '!', char_length(timestamp '2024-01-15 10:00:00 Asia/Tokyo' || '') from rdb$database
-- @case
select trim(cast(timestamp '2024-01-15 10:00:00 +03:00' as char(60))), octet_length(cast(time '10:00:00 +03:00' as varchar(60))) from rdb$database
-- @case
select upper(timestamp '2024-01-15 10:00:00 america/sao_paulo'), substring(timestamp '2024-01-15 10:00:00 Asia/Tokyo' from 26) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +03:00' like '2024%+03:00', time '10:00:00 Europe/Paris' containing 'Paris' from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00 +03:00' as varchar(20)) from rdb$database
-- @case
select cast(time '10:00:00 America/Sao_Paulo' as varchar(20)) from rdb$database
-- @case params: ["2024-01-15T10:00:00"]
select cast(cast(? as timestamp with time zone) as varchar(60)) from rdb$database
-- @case params: ["10:00:00"]
select cast(cast(? as time with time zone) as varchar(40)) from rdb$database
-- @case params: ["2024-01-15T13:00:00"]
select id from tz where ts = ? order by id
-- @case params: ["13:00:00"]
select id from tz where t = ? order by id
-- @case params: ["2024-01-15T10:00:00"]
select ? - timestamp '2024-01-15 10:00:00 +03:00' from rdb$database
-- @case params: [1]
select timestamp '2024-01-15 10:00:00 +03:00' + ? from rdb$database
-- @case params: ["2024-01-15T10:00:00"]
select extract(timezone_hour from cast(? as timestamp with time zone)) from rdb$database
-- @case
select id, ts, t, cast(ts as varchar(60)), cast(t as varchar(40)) from tz order by id
-- @case
insert into tz (id, ts, t) values (10, '2024-05-05 05:05:05 Pacific/Auckland', '05:05:05 Pacific/Auckland')
-- @case
insert into tz (id, ts, t) values (11, timestamp '2024-05-05 05:05:05', time '05:05:05')
-- @case params: [12, "2024-05-05T05:05:05", "05:05:05"]
insert into tz (id, ts, t) values (?, ?, ?)
-- @case
insert into tz (id, lts, lt) values (13, timestamp '2024-05-05 05:05:05 +09:00', time '05:05:05 +09:00')
-- @case
insert into tz (id, ts) values (14, '2024-05-05 05:05:05 Nowhere/Land')
-- @case
insert into tz (id, t) values (15, '25:00:00 +00:00')
-- @case
select id, cast(ts as varchar(60)), cast(t as varchar(40)), lts, lt from tz where id >= 10 order by id
-- @case
update tz set ts = ts + 1, t = t at time zone '+01:00' where id = 10
-- @case
select cast(ts as varchar(60)), cast(t as varchar(40)) from tz where id = 10
-- @case
update tz set lts = ts, lt = t where id in (1, 3, 4)
-- @case
select id, lts, lt from tz where id in (1, 3, 4) order by id
-- @case
insert into tzk values ('2024-01-15 10:00:00 +03:00', 1)
-- @case
insert into tzk values ('2024-01-15 07:00:00 +00:00', 2)
-- @case
insert into tzk values ('2024-01-15 04:00:00 America/Sao_Paulo', 3)
-- @case
insert into tzk values ('2024-01-15 07:00:00.0001 +00:00', 4)
-- @case
select cast(k as varchar(60)), n from tzk order by k, n
-- @case
update or insert into tzk (k, n) values ('2024-01-15 08:00:00 +01:00', 9) matching (k)
-- @case
select cast(k as varchar(60)), n from tzk order by k, n
-- @case
merge into tzk d using (select timestamp '2024-01-15 02:00:00 -05:00' k from rdb$database) s on d.k = s.k when matched then update set n = 99 when not matched then insert values (s.k, 0)
-- @case
select cast(k as varchar(60)), n from tzk order by k, n
-- @case
insert into tzd (id) values (1)
-- @case
select created is not null, createdz is not null, extract(timezone_name from createdz), created = createdz, cast(createdz as timestamp) = created from tzd
-- @case
delete from tzk where k = '2024-01-15 07:00:00'
-- @case
select count(*) from tzk
-- @case
set time zone 'America/Sao_Paulo'
-- @case
select rdb$get_context('SYSTEM', 'SESSION_TIMEZONE'), extract(timezone_name from current_timestamp), extract(timezone_hour from current_time) from rdb$database
-- @case
select cast(cast('2024-01-15 10:00:00' as timestamp with time zone) as varchar(60)), cast(timestamp '2024-01-15 10:00:00 +00:00' as timestamp), cast(time '10:00:00 +00:00' as time), cast(timestamp '2024-01-15 01:00:00 +00:00' as date) from rdb$database
-- @case
select timestamp '2024-01-15 10:00:00 +00:00', time '10:00:00 +00:00', timestamp '2024-01-15 10:00:00 +00:00' = timestamp '2024-01-15 07:00:00' from rdb$database
-- @case
select cast(timestamp '2024-01-15 10:00:00' at local as varchar(60)), cast(timestamp '2024-01-15 10:00:00 +05:00' at local as varchar(60)) from rdb$database
-- @case
select datediff(hour, timestamp '2024-01-15 10:00:00', timestamp '2024-01-15 10:00:00 +00:00') from rdb$database
-- @case
select extract(timezone_hour from timestamp '2024-01-15 10:00:00'), extract(timezone_hour from time '10:00:00') from rdb$database
-- @case
select current_timestamp - localtimestamp, cast(current_timestamp as timestamp) = localtimestamp, cast(current_timestamp as date) = current_date from rdb$database
-- @case
select id, ts, t from tz where id in (1, 2, 3) order by id
-- @case params: ["2024-01-15T10:00:00"]
select id from tz where ts = ? order by id
-- @case params: [20, "2024-02-02T02:02:02", "02:02:02"]
insert into tz (id, ts, t) values (?, ?, ?)
-- @case
select cast(ts as varchar(60)), cast(t as varchar(40)) from tz where id = 20
-- @case
set time zone '-05:00'
-- @case
select rdb$get_context('SYSTEM', 'SESSION_TIMEZONE'), cast(cast('2024-01-15 10:00:00' as timestamp with time zone) as varchar(60)), timestamp '2024-01-15 10:00:00 +00:00', time '10:00:00 +00:00' from rdb$database
-- @case
set time zone '+5:7'
-- @case
select rdb$get_context('SYSTEM', 'SESSION_TIMEZONE') from rdb$database
-- @case
set time zone 'utc'
-- @case
select rdb$get_context('SYSTEM', 'SESSION_TIMEZONE') from rdb$database
-- @case
set time zone 'Atlantis/Lost'
-- @case
set time zone '+14:30'
-- @case
set time zone ''
-- @case
select rdb$get_context('SYSTEM', 'SESSION_TIMEZONE') from rdb$database
-- @case
set time zone local
-- @case
select rdb$get_context('SYSTEM', 'SESSION_TIMEZONE'), timestamp '2024-01-15 10:00:00 +03:00' from rdb$database
-- @case
create table tz2 (a timestamp with time zone, b time with time zone, c timestamp without time zone, d time without time zone)
-- @commit
-- @case
select trim(rf.rdb$field_name), f.rdb$field_type, f.rdb$field_length, f.rdb$field_sub_type from rdb$relation_fields rf join rdb$fields f on f.rdb$field_name = rf.rdb$field_source where rf.rdb$relation_name = 'TZ2' order by rf.rdb$field_position
-- @case
select a, b, c, d from tz2
-- @case
create domain dtz as timestamp with time zone default '2024-01-01 00:00:00 +03:00' not null
-- @commit
-- @case
create table tz3 (id integer, v dtz)
-- @commit
-- @case
insert into tz3 (id) values (1)
-- @case
select id, v, cast(v as varchar(60)) from tz3
-- @case
alter table tz2 alter a type timestamp
-- @case
alter table tz2 alter a type time with time zone
-- @case
alter table tz2 alter a type date
-- @case
alter table tz2 alter a type varchar(80)
-- @case
alter table tz2 alter a type varchar(10)
-- @case
alter table tz2 alter b type time
-- @case
alter table tz2 alter b type timestamp with time zone
-- @case
alter table tz2 alter c type timestamp with time zone
-- @case
alter table tz2 alter d type time with time zone
-- @case
alter table tz2 alter a type integer
-- @case
alter table tz2 alter b type varchar(60)
-- @rollback
-- @case
select count(*), min(rdb$time_zone_id), max(rdb$time_zone_id) from rdb$time_zones
-- @case
select rdb$time_zone_id, trim(rdb$time_zone_name) from rdb$time_zones where rdb$time_zone_name in ('UTC', 'America/Sao_Paulo', 'Etc/UTC', 'GMT', 'Europe/Kyiv', 'Asia/Kolkata') order by 1
-- @case
select first 3 rdb$time_zone_id, trim(rdb$time_zone_name) from rdb$time_zones order by rdb$time_zone_id
-- @case
select rdb$time_zone_id, rdb$time_zone_name from rdb$time_zones where rdb$time_zone_id = 65535
-- @case
execute block returns (a varchar(60), b varchar(60), c timestamp, d time) as
declare x timestamp with time zone = '2024-01-15 10:00:00 America/Sao_Paulo';
declare y time with time zone;
begin
  y = x;
  a = x;
  b = y;
  c = x;
  d = y;
  suspend;
  x = x at time zone 'Asia/Tokyo';
  a = x;
  b = dateadd(1 hour to y);
  suspend;
end
-- @case
execute block returns (z varchar(60)) as
begin
  z = rdb$get_context('SYSTEM', 'SESSION_TIMEZONE');
  suspend;
end
-- @case
select id, cast(lag(ts) over (order by id) as varchar(60)), cast(first_value(ts) over (order by ts, id) as varchar(60)), rank() over (order by ts) from tz where ts is not null order by id
-- @case
select id, cast(max(ts) over (partition by extract(month from ts)) as varchar(60)), count(*) over (order by t range between unbounded preceding and current row) from tz where ts is not null order by id
-- @case
create view vtz as select id, ts at time zone 'Asia/Tokyo' tokyo, cast(ts as timestamp) utc, t from tz
-- @commit
-- @case
select id, cast(tokyo as varchar(60)), utc, t from vtz where id <= 4 order by id
-- @case
create table tzc (a timestamp with time zone, b computed by (cast(a as varchar(60))), c computed by (a at time zone '+01:00'), check (a > timestamp '2000-01-01 00:00:00 +00:00'))
-- @commit
-- @case
insert into tzc (a) values ('2024-01-15 10:00:00 Asia/Tokyo')
-- @case
insert into tzc (a) values ('1999-12-31 23:00:00 -05:00')
-- @case
insert into tzc (a) values ('2000-01-01 00:00:00 +00:00')
-- @case
insert into tzc (a) values ('2000-01-01 01:00:00 +02:00')
-- @case
select a, b, cast(c as varchar(60)), c from tzc order by a
-- @case
create index tzc_a on tzc (a)
-- @commit
-- @case
select b from tzc where a = timestamp '2024-01-15 01:00:00 UTC'
-- @case
select b from tzc where a >= '2024-01-15 10:00:00 Asia/Tokyo' order by a
-- @case
create trigger tzc_bi for tzc before insert as
begin
  new.a = new.a at time zone 'America/Sao_Paulo';
end
-- @commit
-- @case
insert into tzc (a) values ('2024-06-01 12:00:00 Europe/Paris')
-- @case
select b from tzc where a > timestamp '2024-05-01 00:00:00 UTC'
-- @case
execute block returns (s varchar(60)) as begin execute statement 'select cast(timestamp ''2024-01-15 10:00:00 +03:00'' at time zone ''-01:00'' as varchar(60)) from rdb$database' into :s; suspend; end
-- @case
execute block returns (s varchar(60)) as begin execute statement ('select cast(cast(? as timestamp with time zone) as varchar(60)) from rdb$database') ('2024-01-15 10:00:00 Pacific/Fiji') into :s; suspend; end
