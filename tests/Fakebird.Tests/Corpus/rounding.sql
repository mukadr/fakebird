-- Rounding everywhere: casts and assignments between scales, ROUND/TRUNC/CEIL/FLOOR, divisions that truncate,
-- averages, half-way values, negative values, binary-inexact doubles and client parameters.
-- @setup
create table r (id integer not null primary key, n0 numeric(9,0), n1 numeric(9,1), n2 numeric(9,2), n4 numeric(4,2),
    d2 decimal(18,2), d6 decimal(18,6), i integer, sm smallint, b bigint, f float, dp double precision);
create table src (id integer not null primary key, n2 numeric(9,2), n4 numeric(18,4), dp double precision);
insert into src values (1, 1.25, 1.2345, 1.25);
insert into src values (2, -1.25, -1.2345, -1.25);
insert into src values (3, 2.35, 2.3450, 2.35);
insert into src values (4, -2.35, -2.3450, -2.35);
insert into src values (5, 0.05, 0.0050, 0.05);
insert into src values (6, 99.99, 99.9999, 99.99);
-- @case
select cast(0.5 as numeric(9,0)) from rdb$database
-- @case
select cast(0.5e0 as numeric(9,0)) from rdb$database
-- @case
select cast('0.5' as numeric(9,0)) from rdb$database
-- @case
select cast(0.5 as numeric(9,1)) from rdb$database
-- @case
select cast(0.5e0 as numeric(9,1)) from rdb$database
-- @case
select cast('0.5' as numeric(9,1)) from rdb$database
-- @case
select cast(0.5 as numeric(9,2)) from rdb$database
-- @case
select cast(0.5e0 as numeric(9,2)) from rdb$database
-- @case
select cast('0.5' as numeric(9,2)) from rdb$database
-- @case
select cast(0.5 as numeric(4,2)) from rdb$database
-- @case
select cast(0.5e0 as numeric(4,2)) from rdb$database
-- @case
select cast('0.5' as numeric(4,2)) from rdb$database
-- @case
select cast(0.5 as numeric(18,3)) from rdb$database
-- @case
select cast(0.5e0 as numeric(18,3)) from rdb$database
-- @case
select cast('0.5' as numeric(18,3)) from rdb$database
-- @case
select cast(0.5 as decimal(18,6)) from rdb$database
-- @case
select cast(0.5e0 as decimal(18,6)) from rdb$database
-- @case
select cast('0.5' as decimal(18,6)) from rdb$database
-- @case
select cast(0.5 as integer) from rdb$database
-- @case
select cast(0.5e0 as integer) from rdb$database
-- @case
select cast('0.5' as integer) from rdb$database
-- @case
select cast(0.5 as smallint) from rdb$database
-- @case
select cast(0.5e0 as smallint) from rdb$database
-- @case
select cast('0.5' as smallint) from rdb$database
-- @case
select cast(0.5 as bigint) from rdb$database
-- @case
select cast(0.5e0 as bigint) from rdb$database
-- @case
select cast('0.5' as bigint) from rdb$database
-- @case
select cast(0.5 as float) from rdb$database
-- @case
select cast(0.5e0 as float) from rdb$database
-- @case
select cast('0.5' as float) from rdb$database
-- @case
select cast(0.5 as double precision) from rdb$database
-- @case
select cast(0.5e0 as double precision) from rdb$database
-- @case
select cast('0.5' as double precision) from rdb$database
-- @case
select cast(1.5 as numeric(9,0)) from rdb$database
-- @case
select cast(1.5e0 as numeric(9,0)) from rdb$database
-- @case
select cast('1.5' as numeric(9,0)) from rdb$database
-- @case
select cast(1.5 as numeric(9,1)) from rdb$database
-- @case
select cast(1.5e0 as numeric(9,1)) from rdb$database
-- @case
select cast('1.5' as numeric(9,1)) from rdb$database
-- @case
select cast(1.5 as numeric(9,2)) from rdb$database
-- @case
select cast(1.5e0 as numeric(9,2)) from rdb$database
-- @case
select cast('1.5' as numeric(9,2)) from rdb$database
-- @case
select cast(1.5 as numeric(4,2)) from rdb$database
-- @case
select cast(1.5e0 as numeric(4,2)) from rdb$database
-- @case
select cast('1.5' as numeric(4,2)) from rdb$database
-- @case
select cast(1.5 as numeric(18,3)) from rdb$database
-- @case
select cast(1.5e0 as numeric(18,3)) from rdb$database
-- @case
select cast('1.5' as numeric(18,3)) from rdb$database
-- @case
select cast(1.5 as decimal(18,6)) from rdb$database
-- @case
select cast(1.5e0 as decimal(18,6)) from rdb$database
-- @case
select cast('1.5' as decimal(18,6)) from rdb$database
-- @case
select cast(1.5 as integer) from rdb$database
-- @case
select cast(1.5e0 as integer) from rdb$database
-- @case
select cast('1.5' as integer) from rdb$database
-- @case
select cast(1.5 as smallint) from rdb$database
-- @case
select cast(1.5e0 as smallint) from rdb$database
-- @case
select cast('1.5' as smallint) from rdb$database
-- @case
select cast(1.5 as bigint) from rdb$database
-- @case
select cast(1.5e0 as bigint) from rdb$database
-- @case
select cast('1.5' as bigint) from rdb$database
-- @case
select cast(1.5 as float) from rdb$database
-- @case
select cast(1.5e0 as float) from rdb$database
-- @case
select cast('1.5' as float) from rdb$database
-- @case
select cast(1.5 as double precision) from rdb$database
-- @case
select cast(1.5e0 as double precision) from rdb$database
-- @case
select cast('1.5' as double precision) from rdb$database
-- @case
select cast(2.5 as numeric(9,0)) from rdb$database
-- @case
select cast(2.5e0 as numeric(9,0)) from rdb$database
-- @case
select cast('2.5' as numeric(9,0)) from rdb$database
-- @case
select cast(2.5 as numeric(9,1)) from rdb$database
-- @case
select cast(2.5e0 as numeric(9,1)) from rdb$database
-- @case
select cast('2.5' as numeric(9,1)) from rdb$database
-- @case
select cast(2.5 as numeric(9,2)) from rdb$database
-- @case
select cast(2.5e0 as numeric(9,2)) from rdb$database
-- @case
select cast('2.5' as numeric(9,2)) from rdb$database
-- @case
select cast(2.5 as numeric(4,2)) from rdb$database
-- @case
select cast(2.5e0 as numeric(4,2)) from rdb$database
-- @case
select cast('2.5' as numeric(4,2)) from rdb$database
-- @case
select cast(2.5 as numeric(18,3)) from rdb$database
-- @case
select cast(2.5e0 as numeric(18,3)) from rdb$database
-- @case
select cast('2.5' as numeric(18,3)) from rdb$database
-- @case
select cast(2.5 as decimal(18,6)) from rdb$database
-- @case
select cast(2.5e0 as decimal(18,6)) from rdb$database
-- @case
select cast('2.5' as decimal(18,6)) from rdb$database
-- @case
select cast(2.5 as integer) from rdb$database
-- @case
select cast(2.5e0 as integer) from rdb$database
-- @case
select cast('2.5' as integer) from rdb$database
-- @case
select cast(2.5 as smallint) from rdb$database
-- @case
select cast(2.5e0 as smallint) from rdb$database
-- @case
select cast('2.5' as smallint) from rdb$database
-- @case
select cast(2.5 as bigint) from rdb$database
-- @case
select cast(2.5e0 as bigint) from rdb$database
-- @case
select cast('2.5' as bigint) from rdb$database
-- @case
select cast(2.5 as float) from rdb$database
-- @case
select cast(2.5e0 as float) from rdb$database
-- @case
select cast('2.5' as float) from rdb$database
-- @case
select cast(2.5 as double precision) from rdb$database
-- @case
select cast(2.5e0 as double precision) from rdb$database
-- @case
select cast('2.5' as double precision) from rdb$database
-- @case
select cast(-0.5 as numeric(9,0)) from rdb$database
-- @case
select cast(-0.5e0 as numeric(9,0)) from rdb$database
-- @case
select cast('-0.5' as numeric(9,0)) from rdb$database
-- @case
select cast(-0.5 as numeric(9,1)) from rdb$database
-- @case
select cast(-0.5e0 as numeric(9,1)) from rdb$database
-- @case
select cast('-0.5' as numeric(9,1)) from rdb$database
-- @case
select cast(-0.5 as numeric(9,2)) from rdb$database
-- @case
select cast(-0.5e0 as numeric(9,2)) from rdb$database
-- @case
select cast('-0.5' as numeric(9,2)) from rdb$database
-- @case
select cast(-0.5 as numeric(4,2)) from rdb$database
-- @case
select cast(-0.5e0 as numeric(4,2)) from rdb$database
-- @case
select cast('-0.5' as numeric(4,2)) from rdb$database
-- @case
select cast(-0.5 as numeric(18,3)) from rdb$database
-- @case
select cast(-0.5e0 as numeric(18,3)) from rdb$database
-- @case
select cast('-0.5' as numeric(18,3)) from rdb$database
-- @case
select cast(-0.5 as decimal(18,6)) from rdb$database
-- @case
select cast(-0.5e0 as decimal(18,6)) from rdb$database
-- @case
select cast('-0.5' as decimal(18,6)) from rdb$database
-- @case
select cast(-0.5 as integer) from rdb$database
-- @case
select cast(-0.5e0 as integer) from rdb$database
-- @case
select cast('-0.5' as integer) from rdb$database
-- @case
select cast(-0.5 as smallint) from rdb$database
-- @case
select cast(-0.5e0 as smallint) from rdb$database
-- @case
select cast('-0.5' as smallint) from rdb$database
-- @case
select cast(-0.5 as bigint) from rdb$database
-- @case
select cast(-0.5e0 as bigint) from rdb$database
-- @case
select cast('-0.5' as bigint) from rdb$database
-- @case
select cast(-0.5 as float) from rdb$database
-- @case
select cast(-0.5e0 as float) from rdb$database
-- @case
select cast('-0.5' as float) from rdb$database
-- @case
select cast(-0.5 as double precision) from rdb$database
-- @case
select cast(-0.5e0 as double precision) from rdb$database
-- @case
select cast('-0.5' as double precision) from rdb$database
-- @case
select cast(-1.5 as numeric(9,0)) from rdb$database
-- @case
select cast(-1.5e0 as numeric(9,0)) from rdb$database
-- @case
select cast('-1.5' as numeric(9,0)) from rdb$database
-- @case
select cast(-1.5 as numeric(9,1)) from rdb$database
-- @case
select cast(-1.5e0 as numeric(9,1)) from rdb$database
-- @case
select cast('-1.5' as numeric(9,1)) from rdb$database
-- @case
select cast(-1.5 as numeric(9,2)) from rdb$database
-- @case
select cast(-1.5e0 as numeric(9,2)) from rdb$database
-- @case
select cast('-1.5' as numeric(9,2)) from rdb$database
-- @case
select cast(-1.5 as numeric(4,2)) from rdb$database
-- @case
select cast(-1.5e0 as numeric(4,2)) from rdb$database
-- @case
select cast('-1.5' as numeric(4,2)) from rdb$database
-- @case
select cast(-1.5 as numeric(18,3)) from rdb$database
-- @case
select cast(-1.5e0 as numeric(18,3)) from rdb$database
-- @case
select cast('-1.5' as numeric(18,3)) from rdb$database
-- @case
select cast(-1.5 as decimal(18,6)) from rdb$database
-- @case
select cast(-1.5e0 as decimal(18,6)) from rdb$database
-- @case
select cast('-1.5' as decimal(18,6)) from rdb$database
-- @case
select cast(-1.5 as integer) from rdb$database
-- @case
select cast(-1.5e0 as integer) from rdb$database
-- @case
select cast('-1.5' as integer) from rdb$database
-- @case
select cast(-1.5 as smallint) from rdb$database
-- @case
select cast(-1.5e0 as smallint) from rdb$database
-- @case
select cast('-1.5' as smallint) from rdb$database
-- @case
select cast(-1.5 as bigint) from rdb$database
-- @case
select cast(-1.5e0 as bigint) from rdb$database
-- @case
select cast('-1.5' as bigint) from rdb$database
-- @case
select cast(-1.5 as float) from rdb$database
-- @case
select cast(-1.5e0 as float) from rdb$database
-- @case
select cast('-1.5' as float) from rdb$database
-- @case
select cast(-1.5 as double precision) from rdb$database
-- @case
select cast(-1.5e0 as double precision) from rdb$database
-- @case
select cast('-1.5' as double precision) from rdb$database
-- @case
select cast(-2.5 as numeric(9,0)) from rdb$database
-- @case
select cast(-2.5e0 as numeric(9,0)) from rdb$database
-- @case
select cast('-2.5' as numeric(9,0)) from rdb$database
-- @case
select cast(-2.5 as numeric(9,1)) from rdb$database
-- @case
select cast(-2.5e0 as numeric(9,1)) from rdb$database
-- @case
select cast('-2.5' as numeric(9,1)) from rdb$database
-- @case
select cast(-2.5 as numeric(9,2)) from rdb$database
-- @case
select cast(-2.5e0 as numeric(9,2)) from rdb$database
-- @case
select cast('-2.5' as numeric(9,2)) from rdb$database
-- @case
select cast(-2.5 as numeric(4,2)) from rdb$database
-- @case
select cast(-2.5e0 as numeric(4,2)) from rdb$database
-- @case
select cast('-2.5' as numeric(4,2)) from rdb$database
-- @case
select cast(-2.5 as numeric(18,3)) from rdb$database
-- @case
select cast(-2.5e0 as numeric(18,3)) from rdb$database
-- @case
select cast('-2.5' as numeric(18,3)) from rdb$database
-- @case
select cast(-2.5 as decimal(18,6)) from rdb$database
-- @case
select cast(-2.5e0 as decimal(18,6)) from rdb$database
-- @case
select cast('-2.5' as decimal(18,6)) from rdb$database
-- @case
select cast(-2.5 as integer) from rdb$database
-- @case
select cast(-2.5e0 as integer) from rdb$database
-- @case
select cast('-2.5' as integer) from rdb$database
-- @case
select cast(-2.5 as smallint) from rdb$database
-- @case
select cast(-2.5e0 as smallint) from rdb$database
-- @case
select cast('-2.5' as smallint) from rdb$database
-- @case
select cast(-2.5 as bigint) from rdb$database
-- @case
select cast(-2.5e0 as bigint) from rdb$database
-- @case
select cast('-2.5' as bigint) from rdb$database
-- @case
select cast(-2.5 as float) from rdb$database
-- @case
select cast(-2.5e0 as float) from rdb$database
-- @case
select cast('-2.5' as float) from rdb$database
-- @case
select cast(-2.5 as double precision) from rdb$database
-- @case
select cast(-2.5e0 as double precision) from rdb$database
-- @case
select cast('-2.5' as double precision) from rdb$database
-- @case
select cast(0.45 as numeric(9,0)) from rdb$database
-- @case
select cast(0.45e0 as numeric(9,0)) from rdb$database
-- @case
select cast('0.45' as numeric(9,0)) from rdb$database
-- @case
select cast(0.45 as numeric(9,1)) from rdb$database
-- @case
select cast(0.45e0 as numeric(9,1)) from rdb$database
-- @case
select cast('0.45' as numeric(9,1)) from rdb$database
-- @case
select cast(0.45 as numeric(9,2)) from rdb$database
-- @case
select cast(0.45e0 as numeric(9,2)) from rdb$database
-- @case
select cast('0.45' as numeric(9,2)) from rdb$database
-- @case
select cast(0.45 as numeric(4,2)) from rdb$database
-- @case
select cast(0.45e0 as numeric(4,2)) from rdb$database
-- @case
select cast('0.45' as numeric(4,2)) from rdb$database
-- @case
select cast(0.45 as numeric(18,3)) from rdb$database
-- @case
select cast(0.45e0 as numeric(18,3)) from rdb$database
-- @case
select cast('0.45' as numeric(18,3)) from rdb$database
-- @case
select cast(0.45 as decimal(18,6)) from rdb$database
-- @case
select cast(0.45e0 as decimal(18,6)) from rdb$database
-- @case
select cast('0.45' as decimal(18,6)) from rdb$database
-- @case
select cast(0.45 as integer) from rdb$database
-- @case
select cast(0.45e0 as integer) from rdb$database
-- @case
select cast('0.45' as integer) from rdb$database
-- @case
select cast(0.45 as smallint) from rdb$database
-- @case
select cast(0.45e0 as smallint) from rdb$database
-- @case
select cast('0.45' as smallint) from rdb$database
-- @case
select cast(0.45 as bigint) from rdb$database
-- @case
select cast(0.45e0 as bigint) from rdb$database
-- @case
select cast('0.45' as bigint) from rdb$database
-- @case
select cast(0.45 as float) from rdb$database
-- @case
select cast(0.45e0 as float) from rdb$database
-- @case
select cast('0.45' as float) from rdb$database
-- @case
select cast(0.45 as double precision) from rdb$database
-- @case
select cast(0.45e0 as double precision) from rdb$database
-- @case
select cast('0.45' as double precision) from rdb$database
-- @case
select cast(0.55 as numeric(9,0)) from rdb$database
-- @case
select cast(0.55e0 as numeric(9,0)) from rdb$database
-- @case
select cast('0.55' as numeric(9,0)) from rdb$database
-- @case
select cast(0.55 as numeric(9,1)) from rdb$database
-- @case
select cast(0.55e0 as numeric(9,1)) from rdb$database
-- @case
select cast('0.55' as numeric(9,1)) from rdb$database
-- @case
select cast(0.55 as numeric(9,2)) from rdb$database
-- @case
select cast(0.55e0 as numeric(9,2)) from rdb$database
-- @case
select cast('0.55' as numeric(9,2)) from rdb$database
-- @case
select cast(0.55 as numeric(4,2)) from rdb$database
-- @case
select cast(0.55e0 as numeric(4,2)) from rdb$database
-- @case
select cast('0.55' as numeric(4,2)) from rdb$database
-- @case
select cast(0.55 as numeric(18,3)) from rdb$database
-- @case
select cast(0.55e0 as numeric(18,3)) from rdb$database
-- @case
select cast('0.55' as numeric(18,3)) from rdb$database
-- @case
select cast(0.55 as decimal(18,6)) from rdb$database
-- @case
select cast(0.55e0 as decimal(18,6)) from rdb$database
-- @case
select cast('0.55' as decimal(18,6)) from rdb$database
-- @case
select cast(0.55 as integer) from rdb$database
-- @case
select cast(0.55e0 as integer) from rdb$database
-- @case
select cast('0.55' as integer) from rdb$database
-- @case
select cast(0.55 as smallint) from rdb$database
-- @case
select cast(0.55e0 as smallint) from rdb$database
-- @case
select cast('0.55' as smallint) from rdb$database
-- @case
select cast(0.55 as bigint) from rdb$database
-- @case
select cast(0.55e0 as bigint) from rdb$database
-- @case
select cast('0.55' as bigint) from rdb$database
-- @case
select cast(0.55 as float) from rdb$database
-- @case
select cast(0.55e0 as float) from rdb$database
-- @case
select cast('0.55' as float) from rdb$database
-- @case
select cast(0.55 as double precision) from rdb$database
-- @case
select cast(0.55e0 as double precision) from rdb$database
-- @case
select cast('0.55' as double precision) from rdb$database
-- @case
select cast(1.005 as numeric(9,0)) from rdb$database
-- @case
select cast(1.005e0 as numeric(9,0)) from rdb$database
-- @case
select cast('1.005' as numeric(9,0)) from rdb$database
-- @case
select cast(1.005 as numeric(9,1)) from rdb$database
-- @case
select cast(1.005e0 as numeric(9,1)) from rdb$database
-- @case
select cast('1.005' as numeric(9,1)) from rdb$database
-- @case
select cast(1.005 as numeric(9,2)) from rdb$database
-- @case
select cast(1.005e0 as numeric(9,2)) from rdb$database
-- @case
select cast('1.005' as numeric(9,2)) from rdb$database
-- @case
select cast(1.005 as numeric(4,2)) from rdb$database
-- @case
select cast(1.005e0 as numeric(4,2)) from rdb$database
-- @case
select cast('1.005' as numeric(4,2)) from rdb$database
-- @case
select cast(1.005 as numeric(18,3)) from rdb$database
-- @case
select cast(1.005e0 as numeric(18,3)) from rdb$database
-- @case
select cast('1.005' as numeric(18,3)) from rdb$database
-- @case
select cast(1.005 as decimal(18,6)) from rdb$database
-- @case
select cast(1.005e0 as decimal(18,6)) from rdb$database
-- @case
select cast('1.005' as decimal(18,6)) from rdb$database
-- @case
select cast(1.005 as integer) from rdb$database
-- @case
select cast(1.005e0 as integer) from rdb$database
-- @case
select cast('1.005' as integer) from rdb$database
-- @case
select cast(1.005 as smallint) from rdb$database
-- @case
select cast(1.005e0 as smallint) from rdb$database
-- @case
select cast('1.005' as smallint) from rdb$database
-- @case
select cast(1.005 as bigint) from rdb$database
-- @case
select cast(1.005e0 as bigint) from rdb$database
-- @case
select cast('1.005' as bigint) from rdb$database
-- @case
select cast(1.005 as float) from rdb$database
-- @case
select cast(1.005e0 as float) from rdb$database
-- @case
select cast('1.005' as float) from rdb$database
-- @case
select cast(1.005 as double precision) from rdb$database
-- @case
select cast(1.005e0 as double precision) from rdb$database
-- @case
select cast('1.005' as double precision) from rdb$database
-- @case
select cast(1.0049 as numeric(9,0)) from rdb$database
-- @case
select cast(1.0049e0 as numeric(9,0)) from rdb$database
-- @case
select cast('1.0049' as numeric(9,0)) from rdb$database
-- @case
select cast(1.0049 as numeric(9,1)) from rdb$database
-- @case
select cast(1.0049e0 as numeric(9,1)) from rdb$database
-- @case
select cast('1.0049' as numeric(9,1)) from rdb$database
-- @case
select cast(1.0049 as numeric(9,2)) from rdb$database
-- @case
select cast(1.0049e0 as numeric(9,2)) from rdb$database
-- @case
select cast('1.0049' as numeric(9,2)) from rdb$database
-- @case
select cast(1.0049 as numeric(4,2)) from rdb$database
-- @case
select cast(1.0049e0 as numeric(4,2)) from rdb$database
-- @case
select cast('1.0049' as numeric(4,2)) from rdb$database
-- @case
select cast(1.0049 as numeric(18,3)) from rdb$database
-- @case
select cast(1.0049e0 as numeric(18,3)) from rdb$database
-- @case
select cast('1.0049' as numeric(18,3)) from rdb$database
-- @case
select cast(1.0049 as decimal(18,6)) from rdb$database
-- @case
select cast(1.0049e0 as decimal(18,6)) from rdb$database
-- @case
select cast('1.0049' as decimal(18,6)) from rdb$database
-- @case
select cast(1.0049 as integer) from rdb$database
-- @case
select cast(1.0049e0 as integer) from rdb$database
-- @case
select cast('1.0049' as integer) from rdb$database
-- @case
select cast(1.0049 as smallint) from rdb$database
-- @case
select cast(1.0049e0 as smallint) from rdb$database
-- @case
select cast('1.0049' as smallint) from rdb$database
-- @case
select cast(1.0049 as bigint) from rdb$database
-- @case
select cast(1.0049e0 as bigint) from rdb$database
-- @case
select cast('1.0049' as bigint) from rdb$database
-- @case
select cast(1.0049 as float) from rdb$database
-- @case
select cast(1.0049e0 as float) from rdb$database
-- @case
select cast('1.0049' as float) from rdb$database
-- @case
select cast(1.0049 as double precision) from rdb$database
-- @case
select cast(1.0049e0 as double precision) from rdb$database
-- @case
select cast('1.0049' as double precision) from rdb$database
-- @case
select cast(1.0050 as numeric(9,0)) from rdb$database
-- @case
select cast(1.0050e0 as numeric(9,0)) from rdb$database
-- @case
select cast('1.0050' as numeric(9,0)) from rdb$database
-- @case
select cast(1.0050 as numeric(9,1)) from rdb$database
-- @case
select cast(1.0050e0 as numeric(9,1)) from rdb$database
-- @case
select cast('1.0050' as numeric(9,1)) from rdb$database
-- @case
select cast(1.0050 as numeric(9,2)) from rdb$database
-- @case
select cast(1.0050e0 as numeric(9,2)) from rdb$database
-- @case
select cast('1.0050' as numeric(9,2)) from rdb$database
-- @case
select cast(1.0050 as numeric(4,2)) from rdb$database
-- @case
select cast(1.0050e0 as numeric(4,2)) from rdb$database
-- @case
select cast('1.0050' as numeric(4,2)) from rdb$database
-- @case
select cast(1.0050 as numeric(18,3)) from rdb$database
-- @case
select cast(1.0050e0 as numeric(18,3)) from rdb$database
-- @case
select cast('1.0050' as numeric(18,3)) from rdb$database
-- @case
select cast(1.0050 as decimal(18,6)) from rdb$database
-- @case
select cast(1.0050e0 as decimal(18,6)) from rdb$database
-- @case
select cast('1.0050' as decimal(18,6)) from rdb$database
-- @case
select cast(1.0050 as integer) from rdb$database
-- @case
select cast(1.0050e0 as integer) from rdb$database
-- @case
select cast('1.0050' as integer) from rdb$database
-- @case
select cast(1.0050 as smallint) from rdb$database
-- @case
select cast(1.0050e0 as smallint) from rdb$database
-- @case
select cast('1.0050' as smallint) from rdb$database
-- @case
select cast(1.0050 as bigint) from rdb$database
-- @case
select cast(1.0050e0 as bigint) from rdb$database
-- @case
select cast('1.0050' as bigint) from rdb$database
-- @case
select cast(1.0050 as float) from rdb$database
-- @case
select cast(1.0050e0 as float) from rdb$database
-- @case
select cast('1.0050' as float) from rdb$database
-- @case
select cast(1.0050 as double precision) from rdb$database
-- @case
select cast(1.0050e0 as double precision) from rdb$database
-- @case
select cast('1.0050' as double precision) from rdb$database
-- @case
select cast(-1.005 as numeric(9,0)) from rdb$database
-- @case
select cast(-1.005e0 as numeric(9,0)) from rdb$database
-- @case
select cast('-1.005' as numeric(9,0)) from rdb$database
-- @case
select cast(-1.005 as numeric(9,1)) from rdb$database
-- @case
select cast(-1.005e0 as numeric(9,1)) from rdb$database
-- @case
select cast('-1.005' as numeric(9,1)) from rdb$database
-- @case
select cast(-1.005 as numeric(9,2)) from rdb$database
-- @case
select cast(-1.005e0 as numeric(9,2)) from rdb$database
-- @case
select cast('-1.005' as numeric(9,2)) from rdb$database
-- @case
select cast(-1.005 as numeric(4,2)) from rdb$database
-- @case
select cast(-1.005e0 as numeric(4,2)) from rdb$database
-- @case
select cast('-1.005' as numeric(4,2)) from rdb$database
-- @case
select cast(-1.005 as numeric(18,3)) from rdb$database
-- @case
select cast(-1.005e0 as numeric(18,3)) from rdb$database
-- @case
select cast('-1.005' as numeric(18,3)) from rdb$database
-- @case
select cast(-1.005 as decimal(18,6)) from rdb$database
-- @case
select cast(-1.005e0 as decimal(18,6)) from rdb$database
-- @case
select cast('-1.005' as decimal(18,6)) from rdb$database
-- @case
select cast(-1.005 as integer) from rdb$database
-- @case
select cast(-1.005e0 as integer) from rdb$database
-- @case
select cast('-1.005' as integer) from rdb$database
-- @case
select cast(-1.005 as smallint) from rdb$database
-- @case
select cast(-1.005e0 as smallint) from rdb$database
-- @case
select cast('-1.005' as smallint) from rdb$database
-- @case
select cast(-1.005 as bigint) from rdb$database
-- @case
select cast(-1.005e0 as bigint) from rdb$database
-- @case
select cast('-1.005' as bigint) from rdb$database
-- @case
select cast(-1.005 as float) from rdb$database
-- @case
select cast(-1.005e0 as float) from rdb$database
-- @case
select cast('-1.005' as float) from rdb$database
-- @case
select cast(-1.005 as double precision) from rdb$database
-- @case
select cast(-1.005e0 as double precision) from rdb$database
-- @case
select cast('-1.005' as double precision) from rdb$database
-- @case
select cast(2.675 as numeric(9,0)) from rdb$database
-- @case
select cast(2.675e0 as numeric(9,0)) from rdb$database
-- @case
select cast('2.675' as numeric(9,0)) from rdb$database
-- @case
select cast(2.675 as numeric(9,1)) from rdb$database
-- @case
select cast(2.675e0 as numeric(9,1)) from rdb$database
-- @case
select cast('2.675' as numeric(9,1)) from rdb$database
-- @case
select cast(2.675 as numeric(9,2)) from rdb$database
-- @case
select cast(2.675e0 as numeric(9,2)) from rdb$database
-- @case
select cast('2.675' as numeric(9,2)) from rdb$database
-- @case
select cast(2.675 as numeric(4,2)) from rdb$database
-- @case
select cast(2.675e0 as numeric(4,2)) from rdb$database
-- @case
select cast('2.675' as numeric(4,2)) from rdb$database
-- @case
select cast(2.675 as numeric(18,3)) from rdb$database
-- @case
select cast(2.675e0 as numeric(18,3)) from rdb$database
-- @case
select cast('2.675' as numeric(18,3)) from rdb$database
-- @case
select cast(2.675 as decimal(18,6)) from rdb$database
-- @case
select cast(2.675e0 as decimal(18,6)) from rdb$database
-- @case
select cast('2.675' as decimal(18,6)) from rdb$database
-- @case
select cast(2.675 as integer) from rdb$database
-- @case
select cast(2.675e0 as integer) from rdb$database
-- @case
select cast('2.675' as integer) from rdb$database
-- @case
select cast(2.675 as smallint) from rdb$database
-- @case
select cast(2.675e0 as smallint) from rdb$database
-- @case
select cast('2.675' as smallint) from rdb$database
-- @case
select cast(2.675 as bigint) from rdb$database
-- @case
select cast(2.675e0 as bigint) from rdb$database
-- @case
select cast('2.675' as bigint) from rdb$database
-- @case
select cast(2.675 as float) from rdb$database
-- @case
select cast(2.675e0 as float) from rdb$database
-- @case
select cast('2.675' as float) from rdb$database
-- @case
select cast(2.675 as double precision) from rdb$database
-- @case
select cast(2.675e0 as double precision) from rdb$database
-- @case
select cast('2.675' as double precision) from rdb$database
-- @case
select cast(0.125 as numeric(9,0)) from rdb$database
-- @case
select cast(0.125e0 as numeric(9,0)) from rdb$database
-- @case
select cast('0.125' as numeric(9,0)) from rdb$database
-- @case
select cast(0.125 as numeric(9,1)) from rdb$database
-- @case
select cast(0.125e0 as numeric(9,1)) from rdb$database
-- @case
select cast('0.125' as numeric(9,1)) from rdb$database
-- @case
select cast(0.125 as numeric(9,2)) from rdb$database
-- @case
select cast(0.125e0 as numeric(9,2)) from rdb$database
-- @case
select cast('0.125' as numeric(9,2)) from rdb$database
-- @case
select cast(0.125 as numeric(4,2)) from rdb$database
-- @case
select cast(0.125e0 as numeric(4,2)) from rdb$database
-- @case
select cast('0.125' as numeric(4,2)) from rdb$database
-- @case
select cast(0.125 as numeric(18,3)) from rdb$database
-- @case
select cast(0.125e0 as numeric(18,3)) from rdb$database
-- @case
select cast('0.125' as numeric(18,3)) from rdb$database
-- @case
select cast(0.125 as decimal(18,6)) from rdb$database
-- @case
select cast(0.125e0 as decimal(18,6)) from rdb$database
-- @case
select cast('0.125' as decimal(18,6)) from rdb$database
-- @case
select cast(0.125 as integer) from rdb$database
-- @case
select cast(0.125e0 as integer) from rdb$database
-- @case
select cast('0.125' as integer) from rdb$database
-- @case
select cast(0.125 as smallint) from rdb$database
-- @case
select cast(0.125e0 as smallint) from rdb$database
-- @case
select cast('0.125' as smallint) from rdb$database
-- @case
select cast(0.125 as bigint) from rdb$database
-- @case
select cast(0.125e0 as bigint) from rdb$database
-- @case
select cast('0.125' as bigint) from rdb$database
-- @case
select cast(0.125 as float) from rdb$database
-- @case
select cast(0.125e0 as float) from rdb$database
-- @case
select cast('0.125' as float) from rdb$database
-- @case
select cast(0.125 as double precision) from rdb$database
-- @case
select cast(0.125e0 as double precision) from rdb$database
-- @case
select cast('0.125' as double precision) from rdb$database
-- @case
select cast(0.135 as numeric(9,0)) from rdb$database
-- @case
select cast(0.135e0 as numeric(9,0)) from rdb$database
-- @case
select cast('0.135' as numeric(9,0)) from rdb$database
-- @case
select cast(0.135 as numeric(9,1)) from rdb$database
-- @case
select cast(0.135e0 as numeric(9,1)) from rdb$database
-- @case
select cast('0.135' as numeric(9,1)) from rdb$database
-- @case
select cast(0.135 as numeric(9,2)) from rdb$database
-- @case
select cast(0.135e0 as numeric(9,2)) from rdb$database
-- @case
select cast('0.135' as numeric(9,2)) from rdb$database
-- @case
select cast(0.135 as numeric(4,2)) from rdb$database
-- @case
select cast(0.135e0 as numeric(4,2)) from rdb$database
-- @case
select cast('0.135' as numeric(4,2)) from rdb$database
-- @case
select cast(0.135 as numeric(18,3)) from rdb$database
-- @case
select cast(0.135e0 as numeric(18,3)) from rdb$database
-- @case
select cast('0.135' as numeric(18,3)) from rdb$database
-- @case
select cast(0.135 as decimal(18,6)) from rdb$database
-- @case
select cast(0.135e0 as decimal(18,6)) from rdb$database
-- @case
select cast('0.135' as decimal(18,6)) from rdb$database
-- @case
select cast(0.135 as integer) from rdb$database
-- @case
select cast(0.135e0 as integer) from rdb$database
-- @case
select cast('0.135' as integer) from rdb$database
-- @case
select cast(0.135 as smallint) from rdb$database
-- @case
select cast(0.135e0 as smallint) from rdb$database
-- @case
select cast('0.135' as smallint) from rdb$database
-- @case
select cast(0.135 as bigint) from rdb$database
-- @case
select cast(0.135e0 as bigint) from rdb$database
-- @case
select cast('0.135' as bigint) from rdb$database
-- @case
select cast(0.135 as float) from rdb$database
-- @case
select cast(0.135e0 as float) from rdb$database
-- @case
select cast('0.135' as float) from rdb$database
-- @case
select cast(0.135 as double precision) from rdb$database
-- @case
select cast(0.135e0 as double precision) from rdb$database
-- @case
select cast('0.135' as double precision) from rdb$database
-- @case
select cast(99.995 as numeric(9,0)) from rdb$database
-- @case
select cast(99.995e0 as numeric(9,0)) from rdb$database
-- @case
select cast('99.995' as numeric(9,0)) from rdb$database
-- @case
select cast(99.995 as numeric(9,1)) from rdb$database
-- @case
select cast(99.995e0 as numeric(9,1)) from rdb$database
-- @case
select cast('99.995' as numeric(9,1)) from rdb$database
-- @case
select cast(99.995 as numeric(9,2)) from rdb$database
-- @case
select cast(99.995e0 as numeric(9,2)) from rdb$database
-- @case
select cast('99.995' as numeric(9,2)) from rdb$database
-- @case
select cast(99.995 as numeric(4,2)) from rdb$database
-- @case
select cast(99.995e0 as numeric(4,2)) from rdb$database
-- @case
select cast('99.995' as numeric(4,2)) from rdb$database
-- @case
select cast(99.995 as numeric(18,3)) from rdb$database
-- @case
select cast(99.995e0 as numeric(18,3)) from rdb$database
-- @case
select cast('99.995' as numeric(18,3)) from rdb$database
-- @case
select cast(99.995 as decimal(18,6)) from rdb$database
-- @case
select cast(99.995e0 as decimal(18,6)) from rdb$database
-- @case
select cast('99.995' as decimal(18,6)) from rdb$database
-- @case
select cast(99.995 as integer) from rdb$database
-- @case
select cast(99.995e0 as integer) from rdb$database
-- @case
select cast('99.995' as integer) from rdb$database
-- @case
select cast(99.995 as smallint) from rdb$database
-- @case
select cast(99.995e0 as smallint) from rdb$database
-- @case
select cast('99.995' as smallint) from rdb$database
-- @case
select cast(99.995 as bigint) from rdb$database
-- @case
select cast(99.995e0 as bigint) from rdb$database
-- @case
select cast('99.995' as bigint) from rdb$database
-- @case
select cast(99.995 as float) from rdb$database
-- @case
select cast(99.995e0 as float) from rdb$database
-- @case
select cast('99.995' as float) from rdb$database
-- @case
select cast(99.995 as double precision) from rdb$database
-- @case
select cast(99.995e0 as double precision) from rdb$database
-- @case
select cast('99.995' as double precision) from rdb$database
-- @case
select cast(-99.995 as numeric(9,0)) from rdb$database
-- @case
select cast(-99.995e0 as numeric(9,0)) from rdb$database
-- @case
select cast('-99.995' as numeric(9,0)) from rdb$database
-- @case
select cast(-99.995 as numeric(9,1)) from rdb$database
-- @case
select cast(-99.995e0 as numeric(9,1)) from rdb$database
-- @case
select cast('-99.995' as numeric(9,1)) from rdb$database
-- @case
select cast(-99.995 as numeric(9,2)) from rdb$database
-- @case
select cast(-99.995e0 as numeric(9,2)) from rdb$database
-- @case
select cast('-99.995' as numeric(9,2)) from rdb$database
-- @case
select cast(-99.995 as numeric(4,2)) from rdb$database
-- @case
select cast(-99.995e0 as numeric(4,2)) from rdb$database
-- @case
select cast('-99.995' as numeric(4,2)) from rdb$database
-- @case
select cast(-99.995 as numeric(18,3)) from rdb$database
-- @case
select cast(-99.995e0 as numeric(18,3)) from rdb$database
-- @case
select cast('-99.995' as numeric(18,3)) from rdb$database
-- @case
select cast(-99.995 as decimal(18,6)) from rdb$database
-- @case
select cast(-99.995e0 as decimal(18,6)) from rdb$database
-- @case
select cast('-99.995' as decimal(18,6)) from rdb$database
-- @case
select cast(-99.995 as integer) from rdb$database
-- @case
select cast(-99.995e0 as integer) from rdb$database
-- @case
select cast('-99.995' as integer) from rdb$database
-- @case
select cast(-99.995 as smallint) from rdb$database
-- @case
select cast(-99.995e0 as smallint) from rdb$database
-- @case
select cast('-99.995' as smallint) from rdb$database
-- @case
select cast(-99.995 as bigint) from rdb$database
-- @case
select cast(-99.995e0 as bigint) from rdb$database
-- @case
select cast('-99.995' as bigint) from rdb$database
-- @case
select cast(-99.995 as float) from rdb$database
-- @case
select cast(-99.995e0 as float) from rdb$database
-- @case
select cast('-99.995' as float) from rdb$database
-- @case
select cast(-99.995 as double precision) from rdb$database
-- @case
select cast(-99.995e0 as double precision) from rdb$database
-- @case
select cast('-99.995' as double precision) from rdb$database
-- @case
select cast(999.9995 as numeric(9,0)) from rdb$database
-- @case
select cast(999.9995e0 as numeric(9,0)) from rdb$database
-- @case
select cast('999.9995' as numeric(9,0)) from rdb$database
-- @case
select cast(999.9995 as numeric(9,1)) from rdb$database
-- @case
select cast(999.9995e0 as numeric(9,1)) from rdb$database
-- @case
select cast('999.9995' as numeric(9,1)) from rdb$database
-- @case
select cast(999.9995 as numeric(9,2)) from rdb$database
-- @case
select cast(999.9995e0 as numeric(9,2)) from rdb$database
-- @case
select cast('999.9995' as numeric(9,2)) from rdb$database
-- @case
select cast(999.9995 as numeric(4,2)) from rdb$database
-- @case
select cast(999.9995e0 as numeric(4,2)) from rdb$database
-- @case
select cast('999.9995' as numeric(4,2)) from rdb$database
-- @case
select cast(999.9995 as numeric(18,3)) from rdb$database
-- @case
select cast(999.9995e0 as numeric(18,3)) from rdb$database
-- @case
select cast('999.9995' as numeric(18,3)) from rdb$database
-- @case
select cast(999.9995 as decimal(18,6)) from rdb$database
-- @case
select cast(999.9995e0 as decimal(18,6)) from rdb$database
-- @case
select cast('999.9995' as decimal(18,6)) from rdb$database
-- @case
select cast(999.9995 as integer) from rdb$database
-- @case
select cast(999.9995e0 as integer) from rdb$database
-- @case
select cast('999.9995' as integer) from rdb$database
-- @case
select cast(999.9995 as smallint) from rdb$database
-- @case
select cast(999.9995e0 as smallint) from rdb$database
-- @case
select cast('999.9995' as smallint) from rdb$database
-- @case
select cast(999.9995 as bigint) from rdb$database
-- @case
select cast(999.9995e0 as bigint) from rdb$database
-- @case
select cast('999.9995' as bigint) from rdb$database
-- @case
select cast(999.9995 as float) from rdb$database
-- @case
select cast(999.9995e0 as float) from rdb$database
-- @case
select cast('999.9995' as float) from rdb$database
-- @case
select cast(999.9995 as double precision) from rdb$database
-- @case
select cast(999.9995e0 as double precision) from rdb$database
-- @case
select cast('999.9995' as double precision) from rdb$database
-- @case
select cast(0.0000005 as numeric(9,0)) from rdb$database
-- @case
select cast(0.0000005e0 as numeric(9,0)) from rdb$database
-- @case
select cast('0.0000005' as numeric(9,0)) from rdb$database
-- @case
select cast(0.0000005 as numeric(9,1)) from rdb$database
-- @case
select cast(0.0000005e0 as numeric(9,1)) from rdb$database
-- @case
select cast('0.0000005' as numeric(9,1)) from rdb$database
-- @case
select cast(0.0000005 as numeric(9,2)) from rdb$database
-- @case
select cast(0.0000005e0 as numeric(9,2)) from rdb$database
-- @case
select cast('0.0000005' as numeric(9,2)) from rdb$database
-- @case
select cast(0.0000005 as numeric(4,2)) from rdb$database
-- @case
select cast(0.0000005e0 as numeric(4,2)) from rdb$database
-- @case
select cast('0.0000005' as numeric(4,2)) from rdb$database
-- @case
select cast(0.0000005 as numeric(18,3)) from rdb$database
-- @case
select cast(0.0000005e0 as numeric(18,3)) from rdb$database
-- @case
select cast('0.0000005' as numeric(18,3)) from rdb$database
-- @case
select cast(0.0000005 as decimal(18,6)) from rdb$database
-- @case
select cast(0.0000005e0 as decimal(18,6)) from rdb$database
-- @case
select cast('0.0000005' as decimal(18,6)) from rdb$database
-- @case
select cast(0.0000005 as integer) from rdb$database
-- @case
select cast(0.0000005e0 as integer) from rdb$database
-- @case
select cast('0.0000005' as integer) from rdb$database
-- @case
select cast(0.0000005 as smallint) from rdb$database
-- @case
select cast(0.0000005e0 as smallint) from rdb$database
-- @case
select cast('0.0000005' as smallint) from rdb$database
-- @case
select cast(0.0000005 as bigint) from rdb$database
-- @case
select cast(0.0000005e0 as bigint) from rdb$database
-- @case
select cast('0.0000005' as bigint) from rdb$database
-- @case
select cast(0.0000005 as float) from rdb$database
-- @case
select cast(0.0000005e0 as float) from rdb$database
-- @case
select cast('0.0000005' as float) from rdb$database
-- @case
select cast(0.0000005 as double precision) from rdb$database
-- @case
select cast(0.0000005e0 as double precision) from rdb$database
-- @case
select cast('0.0000005' as double precision) from rdb$database
-- @case
select cast(123456.789012345 as numeric(9,0)) from rdb$database
-- @case
select cast(123456.789012345e0 as numeric(9,0)) from rdb$database
-- @case
select cast('123456.789012345' as numeric(9,0)) from rdb$database
-- @case
select cast(123456.789012345 as numeric(9,1)) from rdb$database
-- @case
select cast(123456.789012345e0 as numeric(9,1)) from rdb$database
-- @case
select cast('123456.789012345' as numeric(9,1)) from rdb$database
-- @case
select cast(123456.789012345 as numeric(9,2)) from rdb$database
-- @case
select cast(123456.789012345e0 as numeric(9,2)) from rdb$database
-- @case
select cast('123456.789012345' as numeric(9,2)) from rdb$database
-- @case
select cast(123456.789012345 as numeric(4,2)) from rdb$database
-- @case
select cast(123456.789012345e0 as numeric(4,2)) from rdb$database
-- @case
select cast('123456.789012345' as numeric(4,2)) from rdb$database
-- @case
select cast(123456.789012345 as numeric(18,3)) from rdb$database
-- @case
select cast(123456.789012345e0 as numeric(18,3)) from rdb$database
-- @case
select cast('123456.789012345' as numeric(18,3)) from rdb$database
-- @case
select cast(123456.789012345 as decimal(18,6)) from rdb$database
-- @case
select cast(123456.789012345e0 as decimal(18,6)) from rdb$database
-- @case
select cast('123456.789012345' as decimal(18,6)) from rdb$database
-- @case
select cast(123456.789012345 as integer) from rdb$database
-- @case
select cast(123456.789012345e0 as integer) from rdb$database
-- @case
select cast('123456.789012345' as integer) from rdb$database
-- @case
select cast(123456.789012345 as smallint) from rdb$database
-- @case
select cast(123456.789012345e0 as smallint) from rdb$database
-- @case
select cast('123456.789012345' as smallint) from rdb$database
-- @case
select cast(123456.789012345 as bigint) from rdb$database
-- @case
select cast(123456.789012345e0 as bigint) from rdb$database
-- @case
select cast('123456.789012345' as bigint) from rdb$database
-- @case
select cast(123456.789012345 as float) from rdb$database
-- @case
select cast(123456.789012345e0 as float) from rdb$database
-- @case
select cast('123456.789012345' as float) from rdb$database
-- @case
select cast(123456.789012345 as double precision) from rdb$database
-- @case
select cast(123456.789012345e0 as double precision) from rdb$database
-- @case
select cast('123456.789012345' as double precision) from rdb$database
-- @case
select cast(0.0049999999 as numeric(9,0)) from rdb$database
-- @case
select cast(0.0049999999e0 as numeric(9,0)) from rdb$database
-- @case
select cast('0.0049999999' as numeric(9,0)) from rdb$database
-- @case
select cast(0.0049999999 as numeric(9,1)) from rdb$database
-- @case
select cast(0.0049999999e0 as numeric(9,1)) from rdb$database
-- @case
select cast('0.0049999999' as numeric(9,1)) from rdb$database
-- @case
select cast(0.0049999999 as numeric(9,2)) from rdb$database
-- @case
select cast(0.0049999999e0 as numeric(9,2)) from rdb$database
-- @case
select cast('0.0049999999' as numeric(9,2)) from rdb$database
-- @case
select cast(0.0049999999 as numeric(4,2)) from rdb$database
-- @case
select cast(0.0049999999e0 as numeric(4,2)) from rdb$database
-- @case
select cast('0.0049999999' as numeric(4,2)) from rdb$database
-- @case
select cast(0.0049999999 as numeric(18,3)) from rdb$database
-- @case
select cast(0.0049999999e0 as numeric(18,3)) from rdb$database
-- @case
select cast('0.0049999999' as numeric(18,3)) from rdb$database
-- @case
select cast(0.0049999999 as decimal(18,6)) from rdb$database
-- @case
select cast(0.0049999999e0 as decimal(18,6)) from rdb$database
-- @case
select cast('0.0049999999' as decimal(18,6)) from rdb$database
-- @case
select cast(0.0049999999 as integer) from rdb$database
-- @case
select cast(0.0049999999e0 as integer) from rdb$database
-- @case
select cast('0.0049999999' as integer) from rdb$database
-- @case
select cast(0.0049999999 as smallint) from rdb$database
-- @case
select cast(0.0049999999e0 as smallint) from rdb$database
-- @case
select cast('0.0049999999' as smallint) from rdb$database
-- @case
select cast(0.0049999999 as bigint) from rdb$database
-- @case
select cast(0.0049999999e0 as bigint) from rdb$database
-- @case
select cast('0.0049999999' as bigint) from rdb$database
-- @case
select cast(0.0049999999 as float) from rdb$database
-- @case
select cast(0.0049999999e0 as float) from rdb$database
-- @case
select cast('0.0049999999' as float) from rdb$database
-- @case
select cast(0.0049999999 as double precision) from rdb$database
-- @case
select cast(0.0049999999e0 as double precision) from rdb$database
-- @case
select cast('0.0049999999' as double precision) from rdb$database
-- @case
select cast(-0.0000001 as numeric(9,0)) from rdb$database
-- @case
select cast(-0.0000001e0 as numeric(9,0)) from rdb$database
-- @case
select cast('-0.0000001' as numeric(9,0)) from rdb$database
-- @case
select cast(-0.0000001 as numeric(9,1)) from rdb$database
-- @case
select cast(-0.0000001e0 as numeric(9,1)) from rdb$database
-- @case
select cast('-0.0000001' as numeric(9,1)) from rdb$database
-- @case
select cast(-0.0000001 as numeric(9,2)) from rdb$database
-- @case
select cast(-0.0000001e0 as numeric(9,2)) from rdb$database
-- @case
select cast('-0.0000001' as numeric(9,2)) from rdb$database
-- @case
select cast(-0.0000001 as numeric(4,2)) from rdb$database
-- @case
select cast(-0.0000001e0 as numeric(4,2)) from rdb$database
-- @case
select cast('-0.0000001' as numeric(4,2)) from rdb$database
-- @case
select cast(-0.0000001 as numeric(18,3)) from rdb$database
-- @case
select cast(-0.0000001e0 as numeric(18,3)) from rdb$database
-- @case
select cast('-0.0000001' as numeric(18,3)) from rdb$database
-- @case
select cast(-0.0000001 as decimal(18,6)) from rdb$database
-- @case
select cast(-0.0000001e0 as decimal(18,6)) from rdb$database
-- @case
select cast('-0.0000001' as decimal(18,6)) from rdb$database
-- @case
select cast(-0.0000001 as integer) from rdb$database
-- @case
select cast(-0.0000001e0 as integer) from rdb$database
-- @case
select cast('-0.0000001' as integer) from rdb$database
-- @case
select cast(-0.0000001 as smallint) from rdb$database
-- @case
select cast(-0.0000001e0 as smallint) from rdb$database
-- @case
select cast('-0.0000001' as smallint) from rdb$database
-- @case
select cast(-0.0000001 as bigint) from rdb$database
-- @case
select cast(-0.0000001e0 as bigint) from rdb$database
-- @case
select cast('-0.0000001' as bigint) from rdb$database
-- @case
select cast(-0.0000001 as float) from rdb$database
-- @case
select cast(-0.0000001e0 as float) from rdb$database
-- @case
select cast('-0.0000001' as float) from rdb$database
-- @case
select cast(-0.0000001 as double precision) from rdb$database
-- @case
select cast(-0.0000001e0 as double precision) from rdb$database
-- @case
select cast('-0.0000001' as double precision) from rdb$database
-- @case
select round(0.5, -2), round(0.5, -1), round(0.5, 0), round(0.5, 1), round(0.5, 2), round(0.5, 3), round(0.5, 4), round(0.5) from rdb$database
-- @case
select trunc(0.5, -2), trunc(0.5, -1), trunc(0.5, 0), trunc(0.5, 1), trunc(0.5, 2), trunc(0.5, 3), trunc(0.5, 4), trunc(0.5) from rdb$database
-- @case
select ceil(0.5), floor(0.5), sign(0.5), abs(0.5) from rdb$database
-- @case
select round(0.5e0, -2), round(0.5e0, -1), round(0.5e0, 0), round(0.5e0, 1), round(0.5e0, 2), round(0.5e0, 3), round(0.5e0, 4), round(0.5e0) from rdb$database
-- @case
select trunc(0.5e0, -2), trunc(0.5e0, -1), trunc(0.5e0, 0), trunc(0.5e0, 1), trunc(0.5e0, 2), trunc(0.5e0, 3), trunc(0.5e0, 4), trunc(0.5e0) from rdb$database
-- @case
select ceil(0.5e0), floor(0.5e0), sign(0.5e0), abs(0.5e0) from rdb$database
-- @case
select round(1.5, -2), round(1.5, -1), round(1.5, 0), round(1.5, 1), round(1.5, 2), round(1.5, 3), round(1.5, 4), round(1.5) from rdb$database
-- @case
select trunc(1.5, -2), trunc(1.5, -1), trunc(1.5, 0), trunc(1.5, 1), trunc(1.5, 2), trunc(1.5, 3), trunc(1.5, 4), trunc(1.5) from rdb$database
-- @case
select ceil(1.5), floor(1.5), sign(1.5), abs(1.5) from rdb$database
-- @case
select round(1.5e0, -2), round(1.5e0, -1), round(1.5e0, 0), round(1.5e0, 1), round(1.5e0, 2), round(1.5e0, 3), round(1.5e0, 4), round(1.5e0) from rdb$database
-- @case
select trunc(1.5e0, -2), trunc(1.5e0, -1), trunc(1.5e0, 0), trunc(1.5e0, 1), trunc(1.5e0, 2), trunc(1.5e0, 3), trunc(1.5e0, 4), trunc(1.5e0) from rdb$database
-- @case
select ceil(1.5e0), floor(1.5e0), sign(1.5e0), abs(1.5e0) from rdb$database
-- @case
select round(2.5, -2), round(2.5, -1), round(2.5, 0), round(2.5, 1), round(2.5, 2), round(2.5, 3), round(2.5, 4), round(2.5) from rdb$database
-- @case
select trunc(2.5, -2), trunc(2.5, -1), trunc(2.5, 0), trunc(2.5, 1), trunc(2.5, 2), trunc(2.5, 3), trunc(2.5, 4), trunc(2.5) from rdb$database
-- @case
select ceil(2.5), floor(2.5), sign(2.5), abs(2.5) from rdb$database
-- @case
select round(2.5e0, -2), round(2.5e0, -1), round(2.5e0, 0), round(2.5e0, 1), round(2.5e0, 2), round(2.5e0, 3), round(2.5e0, 4), round(2.5e0) from rdb$database
-- @case
select trunc(2.5e0, -2), trunc(2.5e0, -1), trunc(2.5e0, 0), trunc(2.5e0, 1), trunc(2.5e0, 2), trunc(2.5e0, 3), trunc(2.5e0, 4), trunc(2.5e0) from rdb$database
-- @case
select ceil(2.5e0), floor(2.5e0), sign(2.5e0), abs(2.5e0) from rdb$database
-- @case
select round(-0.5, -2), round(-0.5, -1), round(-0.5, 0), round(-0.5, 1), round(-0.5, 2), round(-0.5, 3), round(-0.5, 4), round(-0.5) from rdb$database
-- @case
select trunc(-0.5, -2), trunc(-0.5, -1), trunc(-0.5, 0), trunc(-0.5, 1), trunc(-0.5, 2), trunc(-0.5, 3), trunc(-0.5, 4), trunc(-0.5) from rdb$database
-- @case
select ceil(-0.5), floor(-0.5), sign(-0.5), abs(-0.5) from rdb$database
-- @case
select round(-0.5e0, -2), round(-0.5e0, -1), round(-0.5e0, 0), round(-0.5e0, 1), round(-0.5e0, 2), round(-0.5e0, 3), round(-0.5e0, 4), round(-0.5e0) from rdb$database
-- @case
select trunc(-0.5e0, -2), trunc(-0.5e0, -1), trunc(-0.5e0, 0), trunc(-0.5e0, 1), trunc(-0.5e0, 2), trunc(-0.5e0, 3), trunc(-0.5e0, 4), trunc(-0.5e0) from rdb$database
-- @case
select ceil(-0.5e0), floor(-0.5e0), sign(-0.5e0), abs(-0.5e0) from rdb$database
-- @case
select round(-1.5, -2), round(-1.5, -1), round(-1.5, 0), round(-1.5, 1), round(-1.5, 2), round(-1.5, 3), round(-1.5, 4), round(-1.5) from rdb$database
-- @case
select trunc(-1.5, -2), trunc(-1.5, -1), trunc(-1.5, 0), trunc(-1.5, 1), trunc(-1.5, 2), trunc(-1.5, 3), trunc(-1.5, 4), trunc(-1.5) from rdb$database
-- @case
select ceil(-1.5), floor(-1.5), sign(-1.5), abs(-1.5) from rdb$database
-- @case
select round(-1.5e0, -2), round(-1.5e0, -1), round(-1.5e0, 0), round(-1.5e0, 1), round(-1.5e0, 2), round(-1.5e0, 3), round(-1.5e0, 4), round(-1.5e0) from rdb$database
-- @case
select trunc(-1.5e0, -2), trunc(-1.5e0, -1), trunc(-1.5e0, 0), trunc(-1.5e0, 1), trunc(-1.5e0, 2), trunc(-1.5e0, 3), trunc(-1.5e0, 4), trunc(-1.5e0) from rdb$database
-- @case
select ceil(-1.5e0), floor(-1.5e0), sign(-1.5e0), abs(-1.5e0) from rdb$database
-- @case
select round(-2.5, -2), round(-2.5, -1), round(-2.5, 0), round(-2.5, 1), round(-2.5, 2), round(-2.5, 3), round(-2.5, 4), round(-2.5) from rdb$database
-- @case
select trunc(-2.5, -2), trunc(-2.5, -1), trunc(-2.5, 0), trunc(-2.5, 1), trunc(-2.5, 2), trunc(-2.5, 3), trunc(-2.5, 4), trunc(-2.5) from rdb$database
-- @case
select ceil(-2.5), floor(-2.5), sign(-2.5), abs(-2.5) from rdb$database
-- @case
select round(-2.5e0, -2), round(-2.5e0, -1), round(-2.5e0, 0), round(-2.5e0, 1), round(-2.5e0, 2), round(-2.5e0, 3), round(-2.5e0, 4), round(-2.5e0) from rdb$database
-- @case
select trunc(-2.5e0, -2), trunc(-2.5e0, -1), trunc(-2.5e0, 0), trunc(-2.5e0, 1), trunc(-2.5e0, 2), trunc(-2.5e0, 3), trunc(-2.5e0, 4), trunc(-2.5e0) from rdb$database
-- @case
select ceil(-2.5e0), floor(-2.5e0), sign(-2.5e0), abs(-2.5e0) from rdb$database
-- @case
select round(0.45, -2), round(0.45, -1), round(0.45, 0), round(0.45, 1), round(0.45, 2), round(0.45, 3), round(0.45, 4), round(0.45) from rdb$database
-- @case
select trunc(0.45, -2), trunc(0.45, -1), trunc(0.45, 0), trunc(0.45, 1), trunc(0.45, 2), trunc(0.45, 3), trunc(0.45, 4), trunc(0.45) from rdb$database
-- @case
select ceil(0.45), floor(0.45), sign(0.45), abs(0.45) from rdb$database
-- @case
select round(0.45e0, -2), round(0.45e0, -1), round(0.45e0, 0), round(0.45e0, 1), round(0.45e0, 2), round(0.45e0, 3), round(0.45e0, 4), round(0.45e0) from rdb$database
-- @case
select trunc(0.45e0, -2), trunc(0.45e0, -1), trunc(0.45e0, 0), trunc(0.45e0, 1), trunc(0.45e0, 2), trunc(0.45e0, 3), trunc(0.45e0, 4), trunc(0.45e0) from rdb$database
-- @case
select ceil(0.45e0), floor(0.45e0), sign(0.45e0), abs(0.45e0) from rdb$database
-- @case
select round(0.55, -2), round(0.55, -1), round(0.55, 0), round(0.55, 1), round(0.55, 2), round(0.55, 3), round(0.55, 4), round(0.55) from rdb$database
-- @case
select trunc(0.55, -2), trunc(0.55, -1), trunc(0.55, 0), trunc(0.55, 1), trunc(0.55, 2), trunc(0.55, 3), trunc(0.55, 4), trunc(0.55) from rdb$database
-- @case
select ceil(0.55), floor(0.55), sign(0.55), abs(0.55) from rdb$database
-- @case
select round(0.55e0, -2), round(0.55e0, -1), round(0.55e0, 0), round(0.55e0, 1), round(0.55e0, 2), round(0.55e0, 3), round(0.55e0, 4), round(0.55e0) from rdb$database
-- @case
select trunc(0.55e0, -2), trunc(0.55e0, -1), trunc(0.55e0, 0), trunc(0.55e0, 1), trunc(0.55e0, 2), trunc(0.55e0, 3), trunc(0.55e0, 4), trunc(0.55e0) from rdb$database
-- @case
select ceil(0.55e0), floor(0.55e0), sign(0.55e0), abs(0.55e0) from rdb$database
-- @case
select round(1.005, -2), round(1.005, -1), round(1.005, 0), round(1.005, 1), round(1.005, 2), round(1.005, 3), round(1.005, 4), round(1.005) from rdb$database
-- @case
select trunc(1.005, -2), trunc(1.005, -1), trunc(1.005, 0), trunc(1.005, 1), trunc(1.005, 2), trunc(1.005, 3), trunc(1.005, 4), trunc(1.005) from rdb$database
-- @case
select ceil(1.005), floor(1.005), sign(1.005), abs(1.005) from rdb$database
-- @case
select round(1.005e0, -2), round(1.005e0, -1), round(1.005e0, 0), round(1.005e0, 1), round(1.005e0, 2), round(1.005e0, 3), round(1.005e0, 4), round(1.005e0) from rdb$database
-- @case
select trunc(1.005e0, -2), trunc(1.005e0, -1), trunc(1.005e0, 0), trunc(1.005e0, 1), trunc(1.005e0, 2), trunc(1.005e0, 3), trunc(1.005e0, 4), trunc(1.005e0) from rdb$database
-- @case
select ceil(1.005e0), floor(1.005e0), sign(1.005e0), abs(1.005e0) from rdb$database
-- @case
select round(1.0049, -2), round(1.0049, -1), round(1.0049, 0), round(1.0049, 1), round(1.0049, 2), round(1.0049, 3), round(1.0049, 4), round(1.0049) from rdb$database
-- @case
select trunc(1.0049, -2), trunc(1.0049, -1), trunc(1.0049, 0), trunc(1.0049, 1), trunc(1.0049, 2), trunc(1.0049, 3), trunc(1.0049, 4), trunc(1.0049) from rdb$database
-- @case
select ceil(1.0049), floor(1.0049), sign(1.0049), abs(1.0049) from rdb$database
-- @case
select round(1.0049e0, -2), round(1.0049e0, -1), round(1.0049e0, 0), round(1.0049e0, 1), round(1.0049e0, 2), round(1.0049e0, 3), round(1.0049e0, 4), round(1.0049e0) from rdb$database
-- @case
select trunc(1.0049e0, -2), trunc(1.0049e0, -1), trunc(1.0049e0, 0), trunc(1.0049e0, 1), trunc(1.0049e0, 2), trunc(1.0049e0, 3), trunc(1.0049e0, 4), trunc(1.0049e0) from rdb$database
-- @case
select ceil(1.0049e0), floor(1.0049e0), sign(1.0049e0), abs(1.0049e0) from rdb$database
-- @case
select round(1.0050, -2), round(1.0050, -1), round(1.0050, 0), round(1.0050, 1), round(1.0050, 2), round(1.0050, 3), round(1.0050, 4), round(1.0050) from rdb$database
-- @case
select trunc(1.0050, -2), trunc(1.0050, -1), trunc(1.0050, 0), trunc(1.0050, 1), trunc(1.0050, 2), trunc(1.0050, 3), trunc(1.0050, 4), trunc(1.0050) from rdb$database
-- @case
select ceil(1.0050), floor(1.0050), sign(1.0050), abs(1.0050) from rdb$database
-- @case
select round(1.0050e0, -2), round(1.0050e0, -1), round(1.0050e0, 0), round(1.0050e0, 1), round(1.0050e0, 2), round(1.0050e0, 3), round(1.0050e0, 4), round(1.0050e0) from rdb$database
-- @case
select trunc(1.0050e0, -2), trunc(1.0050e0, -1), trunc(1.0050e0, 0), trunc(1.0050e0, 1), trunc(1.0050e0, 2), trunc(1.0050e0, 3), trunc(1.0050e0, 4), trunc(1.0050e0) from rdb$database
-- @case
select ceil(1.0050e0), floor(1.0050e0), sign(1.0050e0), abs(1.0050e0) from rdb$database
-- @case
select round(-1.005, -2), round(-1.005, -1), round(-1.005, 0), round(-1.005, 1), round(-1.005, 2), round(-1.005, 3), round(-1.005, 4), round(-1.005) from rdb$database
-- @case
select trunc(-1.005, -2), trunc(-1.005, -1), trunc(-1.005, 0), trunc(-1.005, 1), trunc(-1.005, 2), trunc(-1.005, 3), trunc(-1.005, 4), trunc(-1.005) from rdb$database
-- @case
select ceil(-1.005), floor(-1.005), sign(-1.005), abs(-1.005) from rdb$database
-- @case
select round(-1.005e0, -2), round(-1.005e0, -1), round(-1.005e0, 0), round(-1.005e0, 1), round(-1.005e0, 2), round(-1.005e0, 3), round(-1.005e0, 4), round(-1.005e0) from rdb$database
-- @case
select trunc(-1.005e0, -2), trunc(-1.005e0, -1), trunc(-1.005e0, 0), trunc(-1.005e0, 1), trunc(-1.005e0, 2), trunc(-1.005e0, 3), trunc(-1.005e0, 4), trunc(-1.005e0) from rdb$database
-- @case
select ceil(-1.005e0), floor(-1.005e0), sign(-1.005e0), abs(-1.005e0) from rdb$database
-- @case
select round(2.675, -2), round(2.675, -1), round(2.675, 0), round(2.675, 1), round(2.675, 2), round(2.675, 3), round(2.675, 4), round(2.675) from rdb$database
-- @case
select trunc(2.675, -2), trunc(2.675, -1), trunc(2.675, 0), trunc(2.675, 1), trunc(2.675, 2), trunc(2.675, 3), trunc(2.675, 4), trunc(2.675) from rdb$database
-- @case
select ceil(2.675), floor(2.675), sign(2.675), abs(2.675) from rdb$database
-- @case
select round(2.675e0, -2), round(2.675e0, -1), round(2.675e0, 0), round(2.675e0, 1), round(2.675e0, 2), round(2.675e0, 3), round(2.675e0, 4), round(2.675e0) from rdb$database
-- @case
select trunc(2.675e0, -2), trunc(2.675e0, -1), trunc(2.675e0, 0), trunc(2.675e0, 1), trunc(2.675e0, 2), trunc(2.675e0, 3), trunc(2.675e0, 4), trunc(2.675e0) from rdb$database
-- @case
select ceil(2.675e0), floor(2.675e0), sign(2.675e0), abs(2.675e0) from rdb$database
-- @case
select round(0.125, -2), round(0.125, -1), round(0.125, 0), round(0.125, 1), round(0.125, 2), round(0.125, 3), round(0.125, 4), round(0.125) from rdb$database
-- @case
select trunc(0.125, -2), trunc(0.125, -1), trunc(0.125, 0), trunc(0.125, 1), trunc(0.125, 2), trunc(0.125, 3), trunc(0.125, 4), trunc(0.125) from rdb$database
-- @case
select ceil(0.125), floor(0.125), sign(0.125), abs(0.125) from rdb$database
-- @case
select round(0.125e0, -2), round(0.125e0, -1), round(0.125e0, 0), round(0.125e0, 1), round(0.125e0, 2), round(0.125e0, 3), round(0.125e0, 4), round(0.125e0) from rdb$database
-- @case
select trunc(0.125e0, -2), trunc(0.125e0, -1), trunc(0.125e0, 0), trunc(0.125e0, 1), trunc(0.125e0, 2), trunc(0.125e0, 3), trunc(0.125e0, 4), trunc(0.125e0) from rdb$database
-- @case
select ceil(0.125e0), floor(0.125e0), sign(0.125e0), abs(0.125e0) from rdb$database
-- @case
select round(0.135, -2), round(0.135, -1), round(0.135, 0), round(0.135, 1), round(0.135, 2), round(0.135, 3), round(0.135, 4), round(0.135) from rdb$database
-- @case
select trunc(0.135, -2), trunc(0.135, -1), trunc(0.135, 0), trunc(0.135, 1), trunc(0.135, 2), trunc(0.135, 3), trunc(0.135, 4), trunc(0.135) from rdb$database
-- @case
select ceil(0.135), floor(0.135), sign(0.135), abs(0.135) from rdb$database
-- @case
select round(0.135e0, -2), round(0.135e0, -1), round(0.135e0, 0), round(0.135e0, 1), round(0.135e0, 2), round(0.135e0, 3), round(0.135e0, 4), round(0.135e0) from rdb$database
-- @case
select trunc(0.135e0, -2), trunc(0.135e0, -1), trunc(0.135e0, 0), trunc(0.135e0, 1), trunc(0.135e0, 2), trunc(0.135e0, 3), trunc(0.135e0, 4), trunc(0.135e0) from rdb$database
-- @case
select ceil(0.135e0), floor(0.135e0), sign(0.135e0), abs(0.135e0) from rdb$database
-- @case
select round(99.995, -2), round(99.995, -1), round(99.995, 0), round(99.995, 1), round(99.995, 2), round(99.995, 3), round(99.995, 4), round(99.995) from rdb$database
-- @case
select trunc(99.995, -2), trunc(99.995, -1), trunc(99.995, 0), trunc(99.995, 1), trunc(99.995, 2), trunc(99.995, 3), trunc(99.995, 4), trunc(99.995) from rdb$database
-- @case
select ceil(99.995), floor(99.995), sign(99.995), abs(99.995) from rdb$database
-- @case
select round(99.995e0, -2), round(99.995e0, -1), round(99.995e0, 0), round(99.995e0, 1), round(99.995e0, 2), round(99.995e0, 3), round(99.995e0, 4), round(99.995e0) from rdb$database
-- @case
select trunc(99.995e0, -2), trunc(99.995e0, -1), trunc(99.995e0, 0), trunc(99.995e0, 1), trunc(99.995e0, 2), trunc(99.995e0, 3), trunc(99.995e0, 4), trunc(99.995e0) from rdb$database
-- @case
select ceil(99.995e0), floor(99.995e0), sign(99.995e0), abs(99.995e0) from rdb$database
-- @case
select round(-99.995, -2), round(-99.995, -1), round(-99.995, 0), round(-99.995, 1), round(-99.995, 2), round(-99.995, 3), round(-99.995, 4), round(-99.995) from rdb$database
-- @case
select trunc(-99.995, -2), trunc(-99.995, -1), trunc(-99.995, 0), trunc(-99.995, 1), trunc(-99.995, 2), trunc(-99.995, 3), trunc(-99.995, 4), trunc(-99.995) from rdb$database
-- @case
select ceil(-99.995), floor(-99.995), sign(-99.995), abs(-99.995) from rdb$database
-- @case
select round(-99.995e0, -2), round(-99.995e0, -1), round(-99.995e0, 0), round(-99.995e0, 1), round(-99.995e0, 2), round(-99.995e0, 3), round(-99.995e0, 4), round(-99.995e0) from rdb$database
-- @case
select trunc(-99.995e0, -2), trunc(-99.995e0, -1), trunc(-99.995e0, 0), trunc(-99.995e0, 1), trunc(-99.995e0, 2), trunc(-99.995e0, 3), trunc(-99.995e0, 4), trunc(-99.995e0) from rdb$database
-- @case
select ceil(-99.995e0), floor(-99.995e0), sign(-99.995e0), abs(-99.995e0) from rdb$database
-- @case
select round(999.9995, -2), round(999.9995, -1), round(999.9995, 0), round(999.9995, 1), round(999.9995, 2), round(999.9995, 3), round(999.9995, 4), round(999.9995) from rdb$database
-- @case
select trunc(999.9995, -2), trunc(999.9995, -1), trunc(999.9995, 0), trunc(999.9995, 1), trunc(999.9995, 2), trunc(999.9995, 3), trunc(999.9995, 4), trunc(999.9995) from rdb$database
-- @case
select ceil(999.9995), floor(999.9995), sign(999.9995), abs(999.9995) from rdb$database
-- @case
select round(999.9995e0, -2), round(999.9995e0, -1), round(999.9995e0, 0), round(999.9995e0, 1), round(999.9995e0, 2), round(999.9995e0, 3), round(999.9995e0, 4), round(999.9995e0) from rdb$database
-- @case
select trunc(999.9995e0, -2), trunc(999.9995e0, -1), trunc(999.9995e0, 0), trunc(999.9995e0, 1), trunc(999.9995e0, 2), trunc(999.9995e0, 3), trunc(999.9995e0, 4), trunc(999.9995e0) from rdb$database
-- @case
select ceil(999.9995e0), floor(999.9995e0), sign(999.9995e0), abs(999.9995e0) from rdb$database
-- @case
select round(0.0000005, -2), round(0.0000005, -1), round(0.0000005, 0), round(0.0000005, 1), round(0.0000005, 2), round(0.0000005, 3), round(0.0000005, 4), round(0.0000005) from rdb$database
-- @case
select trunc(0.0000005, -2), trunc(0.0000005, -1), trunc(0.0000005, 0), trunc(0.0000005, 1), trunc(0.0000005, 2), trunc(0.0000005, 3), trunc(0.0000005, 4), trunc(0.0000005) from rdb$database
-- @case
select ceil(0.0000005), floor(0.0000005), sign(0.0000005), abs(0.0000005) from rdb$database
-- @case
select round(0.0000005e0, -2), round(0.0000005e0, -1), round(0.0000005e0, 0), round(0.0000005e0, 1), round(0.0000005e0, 2), round(0.0000005e0, 3), round(0.0000005e0, 4), round(0.0000005e0) from rdb$database
-- @case
select trunc(0.0000005e0, -2), trunc(0.0000005e0, -1), trunc(0.0000005e0, 0), trunc(0.0000005e0, 1), trunc(0.0000005e0, 2), trunc(0.0000005e0, 3), trunc(0.0000005e0, 4), trunc(0.0000005e0) from rdb$database
-- @case
select ceil(0.0000005e0), floor(0.0000005e0), sign(0.0000005e0), abs(0.0000005e0) from rdb$database
-- @case
select round(123456.789012345, -2), round(123456.789012345, -1), round(123456.789012345, 0), round(123456.789012345, 1), round(123456.789012345, 2), round(123456.789012345, 3), round(123456.789012345, 4), round(123456.789012345) from rdb$database
-- @case
select trunc(123456.789012345, -2), trunc(123456.789012345, -1), trunc(123456.789012345, 0), trunc(123456.789012345, 1), trunc(123456.789012345, 2), trunc(123456.789012345, 3), trunc(123456.789012345, 4), trunc(123456.789012345) from rdb$database
-- @case
select ceil(123456.789012345), floor(123456.789012345), sign(123456.789012345), abs(123456.789012345) from rdb$database
-- @case
select round(123456.789012345e0, -2), round(123456.789012345e0, -1), round(123456.789012345e0, 0), round(123456.789012345e0, 1), round(123456.789012345e0, 2), round(123456.789012345e0, 3), round(123456.789012345e0, 4), round(123456.789012345e0) from rdb$database
-- @case
select trunc(123456.789012345e0, -2), trunc(123456.789012345e0, -1), trunc(123456.789012345e0, 0), trunc(123456.789012345e0, 1), trunc(123456.789012345e0, 2), trunc(123456.789012345e0, 3), trunc(123456.789012345e0, 4), trunc(123456.789012345e0) from rdb$database
-- @case
select ceil(123456.789012345e0), floor(123456.789012345e0), sign(123456.789012345e0), abs(123456.789012345e0) from rdb$database
-- @case
select round(0.0049999999, -2), round(0.0049999999, -1), round(0.0049999999, 0), round(0.0049999999, 1), round(0.0049999999, 2), round(0.0049999999, 3), round(0.0049999999, 4), round(0.0049999999) from rdb$database
-- @case
select trunc(0.0049999999, -2), trunc(0.0049999999, -1), trunc(0.0049999999, 0), trunc(0.0049999999, 1), trunc(0.0049999999, 2), trunc(0.0049999999, 3), trunc(0.0049999999, 4), trunc(0.0049999999) from rdb$database
-- @case
select ceil(0.0049999999), floor(0.0049999999), sign(0.0049999999), abs(0.0049999999) from rdb$database
-- @case
select round(0.0049999999e0, -2), round(0.0049999999e0, -1), round(0.0049999999e0, 0), round(0.0049999999e0, 1), round(0.0049999999e0, 2), round(0.0049999999e0, 3), round(0.0049999999e0, 4), round(0.0049999999e0) from rdb$database
-- @case
select trunc(0.0049999999e0, -2), trunc(0.0049999999e0, -1), trunc(0.0049999999e0, 0), trunc(0.0049999999e0, 1), trunc(0.0049999999e0, 2), trunc(0.0049999999e0, 3), trunc(0.0049999999e0, 4), trunc(0.0049999999e0) from rdb$database
-- @case
select ceil(0.0049999999e0), floor(0.0049999999e0), sign(0.0049999999e0), abs(0.0049999999e0) from rdb$database
-- @case
select round(-0.0000001, -2), round(-0.0000001, -1), round(-0.0000001, 0), round(-0.0000001, 1), round(-0.0000001, 2), round(-0.0000001, 3), round(-0.0000001, 4), round(-0.0000001) from rdb$database
-- @case
select trunc(-0.0000001, -2), trunc(-0.0000001, -1), trunc(-0.0000001, 0), trunc(-0.0000001, 1), trunc(-0.0000001, 2), trunc(-0.0000001, 3), trunc(-0.0000001, 4), trunc(-0.0000001) from rdb$database
-- @case
select ceil(-0.0000001), floor(-0.0000001), sign(-0.0000001), abs(-0.0000001) from rdb$database
-- @case
select round(-0.0000001e0, -2), round(-0.0000001e0, -1), round(-0.0000001e0, 0), round(-0.0000001e0, 1), round(-0.0000001e0, 2), round(-0.0000001e0, 3), round(-0.0000001e0, 4), round(-0.0000001e0) from rdb$database
-- @case
select trunc(-0.0000001e0, -2), trunc(-0.0000001e0, -1), trunc(-0.0000001e0, 0), trunc(-0.0000001e0, 1), trunc(-0.0000001e0, 2), trunc(-0.0000001e0, 3), trunc(-0.0000001e0, 4), trunc(-0.0000001e0) from rdb$database
-- @case
select ceil(-0.0000001e0), floor(-0.0000001e0), sign(-0.0000001e0), abs(-0.0000001e0) from rdb$database
-- @case
select 1 / 3, cast(1 / 3 as numeric(18,2)), cast(1 / 3 as numeric(9,0)), 1 / 3e0 from rdb$database
-- @case
select 1 / -3, cast(1 / -3 as numeric(18,2)), cast(1 / -3 as numeric(9,0)), 1 / -3e0 from rdb$database
-- @case
select 1 / 3.0, cast(1 / 3.0 as numeric(18,2)), cast(1 / 3.0 as numeric(9,0)), 1 / 3.0e0 from rdb$database
-- @case
select 1 / 0.3, cast(1 / 0.3 as numeric(18,2)), cast(1 / 0.3 as numeric(9,0)), 1 / 0.3e0 from rdb$database
-- @case
select 1 / 7, cast(1 / 7 as numeric(18,2)), cast(1 / 7 as numeric(9,0)), 1 / 7e0 from rdb$database
-- @case
select 1 / 2.50, cast(1 / 2.50 as numeric(18,2)), cast(1 / 2.50 as numeric(9,0)), 1 / 2.50e0 from rdb$database
-- @case
select 1 / 0.07, cast(1 / 0.07 as numeric(18,2)), cast(1 / 0.07 as numeric(9,0)), 1 / 0.07e0 from rdb$database
-- @case
select 1 / 1.0000, cast(1 / 1.0000 as numeric(18,2)), cast(1 / 1.0000 as numeric(9,0)), 1 / 1.0000e0 from rdb$database
-- @case
select 1 / -0.333, cast(1 / -0.333 as numeric(18,2)), cast(1 / -0.333 as numeric(9,0)), 1 / -0.333e0 from rdb$database
-- @case
select -1 / 3, cast(-1 / 3 as numeric(18,2)), cast(-1 / 3 as numeric(9,0)), -1 / 3e0 from rdb$database
-- @case
select -1 / -3, cast(-1 / -3 as numeric(18,2)), cast(-1 / -3 as numeric(9,0)), -1 / -3e0 from rdb$database
-- @case
select -1 / 3.0, cast(-1 / 3.0 as numeric(18,2)), cast(-1 / 3.0 as numeric(9,0)), -1 / 3.0e0 from rdb$database
-- @case
select -1 / 0.3, cast(-1 / 0.3 as numeric(18,2)), cast(-1 / 0.3 as numeric(9,0)), -1 / 0.3e0 from rdb$database
-- @case
select -1 / 7, cast(-1 / 7 as numeric(18,2)), cast(-1 / 7 as numeric(9,0)), -1 / 7e0 from rdb$database
-- @case
select -1 / 2.50, cast(-1 / 2.50 as numeric(18,2)), cast(-1 / 2.50 as numeric(9,0)), -1 / 2.50e0 from rdb$database
-- @case
select -1 / 0.07, cast(-1 / 0.07 as numeric(18,2)), cast(-1 / 0.07 as numeric(9,0)), -1 / 0.07e0 from rdb$database
-- @case
select -1 / 1.0000, cast(-1 / 1.0000 as numeric(18,2)), cast(-1 / 1.0000 as numeric(9,0)), -1 / 1.0000e0 from rdb$database
-- @case
select -1 / -0.333, cast(-1 / -0.333 as numeric(18,2)), cast(-1 / -0.333 as numeric(9,0)), -1 / -0.333e0 from rdb$database
-- @case
select 2.00 / 3, cast(2.00 / 3 as numeric(18,2)), cast(2.00 / 3 as numeric(9,0)), 2.00 / 3e0 from rdb$database
-- @case
select 2.00 / -3, cast(2.00 / -3 as numeric(18,2)), cast(2.00 / -3 as numeric(9,0)), 2.00 / -3e0 from rdb$database
-- @case
select 2.00 / 3.0, cast(2.00 / 3.0 as numeric(18,2)), cast(2.00 / 3.0 as numeric(9,0)), 2.00 / 3.0e0 from rdb$database
-- @case
select 2.00 / 0.3, cast(2.00 / 0.3 as numeric(18,2)), cast(2.00 / 0.3 as numeric(9,0)), 2.00 / 0.3e0 from rdb$database
-- @case
select 2.00 / 7, cast(2.00 / 7 as numeric(18,2)), cast(2.00 / 7 as numeric(9,0)), 2.00 / 7e0 from rdb$database
-- @case
select 2.00 / 2.50, cast(2.00 / 2.50 as numeric(18,2)), cast(2.00 / 2.50 as numeric(9,0)), 2.00 / 2.50e0 from rdb$database
-- @case
select 2.00 / 0.07, cast(2.00 / 0.07 as numeric(18,2)), cast(2.00 / 0.07 as numeric(9,0)), 2.00 / 0.07e0 from rdb$database
-- @case
select 2.00 / 1.0000, cast(2.00 / 1.0000 as numeric(18,2)), cast(2.00 / 1.0000 as numeric(9,0)), 2.00 / 1.0000e0 from rdb$database
-- @case
select 2.00 / -0.333, cast(2.00 / -0.333 as numeric(18,2)), cast(2.00 / -0.333 as numeric(9,0)), 2.00 / -0.333e0 from rdb$database
-- @case
select -2.00 / 3, cast(-2.00 / 3 as numeric(18,2)), cast(-2.00 / 3 as numeric(9,0)), -2.00 / 3e0 from rdb$database
-- @case
select -2.00 / -3, cast(-2.00 / -3 as numeric(18,2)), cast(-2.00 / -3 as numeric(9,0)), -2.00 / -3e0 from rdb$database
-- @case
select -2.00 / 3.0, cast(-2.00 / 3.0 as numeric(18,2)), cast(-2.00 / 3.0 as numeric(9,0)), -2.00 / 3.0e0 from rdb$database
-- @case
select -2.00 / 0.3, cast(-2.00 / 0.3 as numeric(18,2)), cast(-2.00 / 0.3 as numeric(9,0)), -2.00 / 0.3e0 from rdb$database
-- @case
select -2.00 / 7, cast(-2.00 / 7 as numeric(18,2)), cast(-2.00 / 7 as numeric(9,0)), -2.00 / 7e0 from rdb$database
-- @case
select -2.00 / 2.50, cast(-2.00 / 2.50 as numeric(18,2)), cast(-2.00 / 2.50 as numeric(9,0)), -2.00 / 2.50e0 from rdb$database
-- @case
select -2.00 / 0.07, cast(-2.00 / 0.07 as numeric(18,2)), cast(-2.00 / 0.07 as numeric(9,0)), -2.00 / 0.07e0 from rdb$database
-- @case
select -2.00 / 1.0000, cast(-2.00 / 1.0000 as numeric(18,2)), cast(-2.00 / 1.0000 as numeric(9,0)), -2.00 / 1.0000e0 from rdb$database
-- @case
select -2.00 / -0.333, cast(-2.00 / -0.333 as numeric(18,2)), cast(-2.00 / -0.333 as numeric(9,0)), -2.00 / -0.333e0 from rdb$database
-- @case
select 10.5 / 3, cast(10.5 / 3 as numeric(18,2)), cast(10.5 / 3 as numeric(9,0)), 10.5 / 3e0 from rdb$database
-- @case
select 10.5 / -3, cast(10.5 / -3 as numeric(18,2)), cast(10.5 / -3 as numeric(9,0)), 10.5 / -3e0 from rdb$database
-- @case
select 10.5 / 3.0, cast(10.5 / 3.0 as numeric(18,2)), cast(10.5 / 3.0 as numeric(9,0)), 10.5 / 3.0e0 from rdb$database
-- @case
select 10.5 / 0.3, cast(10.5 / 0.3 as numeric(18,2)), cast(10.5 / 0.3 as numeric(9,0)), 10.5 / 0.3e0 from rdb$database
-- @case
select 10.5 / 7, cast(10.5 / 7 as numeric(18,2)), cast(10.5 / 7 as numeric(9,0)), 10.5 / 7e0 from rdb$database
-- @case
select 10.5 / 2.50, cast(10.5 / 2.50 as numeric(18,2)), cast(10.5 / 2.50 as numeric(9,0)), 10.5 / 2.50e0 from rdb$database
-- @case
select 10.5 / 0.07, cast(10.5 / 0.07 as numeric(18,2)), cast(10.5 / 0.07 as numeric(9,0)), 10.5 / 0.07e0 from rdb$database
-- @case
select 10.5 / 1.0000, cast(10.5 / 1.0000 as numeric(18,2)), cast(10.5 / 1.0000 as numeric(9,0)), 10.5 / 1.0000e0 from rdb$database
-- @case
select 10.5 / -0.333, cast(10.5 / -0.333 as numeric(18,2)), cast(10.5 / -0.333 as numeric(9,0)), 10.5 / -0.333e0 from rdb$database
-- @case
select 0.001 / 3, cast(0.001 / 3 as numeric(18,2)), cast(0.001 / 3 as numeric(9,0)), 0.001 / 3e0 from rdb$database
-- @case
select 0.001 / -3, cast(0.001 / -3 as numeric(18,2)), cast(0.001 / -3 as numeric(9,0)), 0.001 / -3e0 from rdb$database
-- @case
select 0.001 / 3.0, cast(0.001 / 3.0 as numeric(18,2)), cast(0.001 / 3.0 as numeric(9,0)), 0.001 / 3.0e0 from rdb$database
-- @case
select 0.001 / 0.3, cast(0.001 / 0.3 as numeric(18,2)), cast(0.001 / 0.3 as numeric(9,0)), 0.001 / 0.3e0 from rdb$database
-- @case
select 0.001 / 7, cast(0.001 / 7 as numeric(18,2)), cast(0.001 / 7 as numeric(9,0)), 0.001 / 7e0 from rdb$database
-- @case
select 0.001 / 2.50, cast(0.001 / 2.50 as numeric(18,2)), cast(0.001 / 2.50 as numeric(9,0)), 0.001 / 2.50e0 from rdb$database
-- @case
select 0.001 / 0.07, cast(0.001 / 0.07 as numeric(18,2)), cast(0.001 / 0.07 as numeric(9,0)), 0.001 / 0.07e0 from rdb$database
-- @case
select 0.001 / 1.0000, cast(0.001 / 1.0000 as numeric(18,2)), cast(0.001 / 1.0000 as numeric(9,0)), 0.001 / 1.0000e0 from rdb$database
-- @case
select 0.001 / -0.333, cast(0.001 / -0.333 as numeric(18,2)), cast(0.001 / -0.333 as numeric(9,0)), 0.001 / -0.333e0 from rdb$database
-- @case
select 7 / 3, cast(7 / 3 as numeric(18,2)), cast(7 / 3 as numeric(9,0)), 7 / 3e0 from rdb$database
-- @case
select 7 / -3, cast(7 / -3 as numeric(18,2)), cast(7 / -3 as numeric(9,0)), 7 / -3e0 from rdb$database
-- @case
select 7 / 3.0, cast(7 / 3.0 as numeric(18,2)), cast(7 / 3.0 as numeric(9,0)), 7 / 3.0e0 from rdb$database
-- @case
select 7 / 0.3, cast(7 / 0.3 as numeric(18,2)), cast(7 / 0.3 as numeric(9,0)), 7 / 0.3e0 from rdb$database
-- @case
select 7 / 7, cast(7 / 7 as numeric(18,2)), cast(7 / 7 as numeric(9,0)), 7 / 7e0 from rdb$database
-- @case
select 7 / 2.50, cast(7 / 2.50 as numeric(18,2)), cast(7 / 2.50 as numeric(9,0)), 7 / 2.50e0 from rdb$database
-- @case
select 7 / 0.07, cast(7 / 0.07 as numeric(18,2)), cast(7 / 0.07 as numeric(9,0)), 7 / 0.07e0 from rdb$database
-- @case
select 7 / 1.0000, cast(7 / 1.0000 as numeric(18,2)), cast(7 / 1.0000 as numeric(9,0)), 7 / 1.0000e0 from rdb$database
-- @case
select 7 / -0.333, cast(7 / -0.333 as numeric(18,2)), cast(7 / -0.333 as numeric(9,0)), 7 / -0.333e0 from rdb$database
-- @case
select -7 / 3, cast(-7 / 3 as numeric(18,2)), cast(-7 / 3 as numeric(9,0)), -7 / 3e0 from rdb$database
-- @case
select -7 / -3, cast(-7 / -3 as numeric(18,2)), cast(-7 / -3 as numeric(9,0)), -7 / -3e0 from rdb$database
-- @case
select -7 / 3.0, cast(-7 / 3.0 as numeric(18,2)), cast(-7 / 3.0 as numeric(9,0)), -7 / 3.0e0 from rdb$database
-- @case
select -7 / 0.3, cast(-7 / 0.3 as numeric(18,2)), cast(-7 / 0.3 as numeric(9,0)), -7 / 0.3e0 from rdb$database
-- @case
select -7 / 7, cast(-7 / 7 as numeric(18,2)), cast(-7 / 7 as numeric(9,0)), -7 / 7e0 from rdb$database
-- @case
select -7 / 2.50, cast(-7 / 2.50 as numeric(18,2)), cast(-7 / 2.50 as numeric(9,0)), -7 / 2.50e0 from rdb$database
-- @case
select -7 / 0.07, cast(-7 / 0.07 as numeric(18,2)), cast(-7 / 0.07 as numeric(9,0)), -7 / 0.07e0 from rdb$database
-- @case
select -7 / 1.0000, cast(-7 / 1.0000 as numeric(18,2)), cast(-7 / 1.0000 as numeric(9,0)), -7 / 1.0000e0 from rdb$database
-- @case
select -7 / -0.333, cast(-7 / -0.333 as numeric(18,2)), cast(-7 / -0.333 as numeric(9,0)), -7 / -0.333e0 from rdb$database
-- @case
select 1.000000 / 3, cast(1.000000 / 3 as numeric(18,2)), cast(1.000000 / 3 as numeric(9,0)), 1.000000 / 3e0 from rdb$database
-- @case
select 1.000000 / -3, cast(1.000000 / -3 as numeric(18,2)), cast(1.000000 / -3 as numeric(9,0)), 1.000000 / -3e0 from rdb$database
-- @case
select 1.000000 / 3.0, cast(1.000000 / 3.0 as numeric(18,2)), cast(1.000000 / 3.0 as numeric(9,0)), 1.000000 / 3.0e0 from rdb$database
-- @case
select 1.000000 / 0.3, cast(1.000000 / 0.3 as numeric(18,2)), cast(1.000000 / 0.3 as numeric(9,0)), 1.000000 / 0.3e0 from rdb$database
-- @case
select 1.000000 / 7, cast(1.000000 / 7 as numeric(18,2)), cast(1.000000 / 7 as numeric(9,0)), 1.000000 / 7e0 from rdb$database
-- @case
select 1.000000 / 2.50, cast(1.000000 / 2.50 as numeric(18,2)), cast(1.000000 / 2.50 as numeric(9,0)), 1.000000 / 2.50e0 from rdb$database
-- @case
select 1.000000 / 0.07, cast(1.000000 / 0.07 as numeric(18,2)), cast(1.000000 / 0.07 as numeric(9,0)), 1.000000 / 0.07e0 from rdb$database
-- @case
select 1.000000 / 1.0000, cast(1.000000 / 1.0000 as numeric(18,2)), cast(1.000000 / 1.0000 as numeric(9,0)), 1.000000 / 1.0000e0 from rdb$database
-- @case
select 1.000000 / -0.333, cast(1.000000 / -0.333 as numeric(18,2)), cast(1.000000 / -0.333 as numeric(9,0)), 1.000000 / -0.333e0 from rdb$database
-- @case
select 123.45 / 3, cast(123.45 / 3 as numeric(18,2)), cast(123.45 / 3 as numeric(9,0)), 123.45 / 3e0 from rdb$database
-- @case
select 123.45 / -3, cast(123.45 / -3 as numeric(18,2)), cast(123.45 / -3 as numeric(9,0)), 123.45 / -3e0 from rdb$database
-- @case
select 123.45 / 3.0, cast(123.45 / 3.0 as numeric(18,2)), cast(123.45 / 3.0 as numeric(9,0)), 123.45 / 3.0e0 from rdb$database
-- @case
select 123.45 / 0.3, cast(123.45 / 0.3 as numeric(18,2)), cast(123.45 / 0.3 as numeric(9,0)), 123.45 / 0.3e0 from rdb$database
-- @case
select 123.45 / 7, cast(123.45 / 7 as numeric(18,2)), cast(123.45 / 7 as numeric(9,0)), 123.45 / 7e0 from rdb$database
-- @case
select 123.45 / 2.50, cast(123.45 / 2.50 as numeric(18,2)), cast(123.45 / 2.50 as numeric(9,0)), 123.45 / 2.50e0 from rdb$database
-- @case
select 123.45 / 0.07, cast(123.45 / 0.07 as numeric(18,2)), cast(123.45 / 0.07 as numeric(9,0)), 123.45 / 0.07e0 from rdb$database
-- @case
select 123.45 / 1.0000, cast(123.45 / 1.0000 as numeric(18,2)), cast(123.45 / 1.0000 as numeric(9,0)), 123.45 / 1.0000e0 from rdb$database
-- @case
select 123.45 / -0.333, cast(123.45 / -0.333 as numeric(18,2)), cast(123.45 / -0.333 as numeric(9,0)), 123.45 / -0.333e0 from rdb$database
-- @case
select -0.10 / 3, cast(-0.10 / 3 as numeric(18,2)), cast(-0.10 / 3 as numeric(9,0)), -0.10 / 3e0 from rdb$database
-- @case
select -0.10 / -3, cast(-0.10 / -3 as numeric(18,2)), cast(-0.10 / -3 as numeric(9,0)), -0.10 / -3e0 from rdb$database
-- @case
select -0.10 / 3.0, cast(-0.10 / 3.0 as numeric(18,2)), cast(-0.10 / 3.0 as numeric(9,0)), -0.10 / 3.0e0 from rdb$database
-- @case
select -0.10 / 0.3, cast(-0.10 / 0.3 as numeric(18,2)), cast(-0.10 / 0.3 as numeric(9,0)), -0.10 / 0.3e0 from rdb$database
-- @case
select -0.10 / 7, cast(-0.10 / 7 as numeric(18,2)), cast(-0.10 / 7 as numeric(9,0)), -0.10 / 7e0 from rdb$database
-- @case
select -0.10 / 2.50, cast(-0.10 / 2.50 as numeric(18,2)), cast(-0.10 / 2.50 as numeric(9,0)), -0.10 / 2.50e0 from rdb$database
-- @case
select -0.10 / 0.07, cast(-0.10 / 0.07 as numeric(18,2)), cast(-0.10 / 0.07 as numeric(9,0)), -0.10 / 0.07e0 from rdb$database
-- @case
select -0.10 / 1.0000, cast(-0.10 / 1.0000 as numeric(18,2)), cast(-0.10 / 1.0000 as numeric(9,0)), -0.10 / 1.0000e0 from rdb$database
-- @case
select -0.10 / -0.333, cast(-0.10 / -0.333 as numeric(18,2)), cast(-0.10 / -0.333 as numeric(9,0)), -0.10 / -0.333e0 from rdb$database
-- @case
select 1.15 * 1.15, cast(1.15 * 1.15 as numeric(9,2)), cast(1.15 * 1.15 as numeric(9,1)), cast(1.15 * 1.15 as integer), 1.15e0 * 1.15 from rdb$database
-- @case
select -1.15 * 1.15, cast(-1.15 * 1.15 as numeric(9,2)), cast(-1.15 * 1.15 as numeric(9,1)), cast(-1.15 * 1.15 as integer), -1.15e0 * 1.15 from rdb$database
-- @case
select 2.5 * 0.5, cast(2.5 * 0.5 as numeric(9,2)), cast(2.5 * 0.5 as numeric(9,1)), cast(2.5 * 0.5 as integer), 2.5e0 * 0.5 from rdb$database
-- @case
select 0.005 * 100, cast(0.005 * 100 as numeric(9,2)), cast(0.005 * 100 as numeric(9,1)), cast(0.005 * 100 as integer), 0.005e0 * 100 from rdb$database
-- @case
select 1.0049 * 1.0049, cast(1.0049 * 1.0049 as numeric(9,2)), cast(1.0049 * 1.0049 as numeric(9,1)), cast(1.0049 * 1.0049 as integer), 1.0049e0 * 1.0049 from rdb$database
-- @case
select 12.345 * 0.1, cast(12.345 * 0.1 as numeric(9,2)), cast(12.345 * 0.1 as numeric(9,1)), cast(12.345 * 0.1 as integer), 12.345e0 * 0.1 from rdb$database
-- @case
select -12.345 * 0.1, cast(-12.345 * 0.1 as numeric(9,2)), cast(-12.345 * 0.1 as numeric(9,1)), cast(-12.345 * 0.1 as integer), -12.345e0 * 0.1 from rdb$database
-- @case
select 0.1 * 0.1, cast(0.1 * 0.1 as numeric(9,2)), cast(0.1 * 0.1 as numeric(9,1)), cast(0.1 * 0.1 as integer), 0.1e0 * 0.1 from rdb$database
-- @case
update or insert into r (id, n0) values (1, 2.5) matching (id) returning n0
-- @case
update or insert into r (id, n0) values (1, -2.5) matching (id) returning n0
-- @case
update or insert into r (id, n0) values (1, 0.5) matching (id) returning n0
-- @case
update or insert into r (id, n1) values (1, 1.25) matching (id) returning n1
-- @case
update or insert into r (id, n1) values (1, -1.25) matching (id) returning n1
-- @case
update or insert into r (id, n1) values (1, 1.35) matching (id) returning n1
-- @case
update or insert into r (id, n2) values (1, 1.005) matching (id) returning n2
-- @case
update or insert into r (id, n2) values (1, -1.005) matching (id) returning n2
-- @case
update or insert into r (id, n2) values (1, 2.675e0) matching (id) returning n2
-- @case
update or insert into r (id, n2) values (1, 1.005e0) matching (id) returning n2
-- @case
update or insert into r (id, n4) values (1, 99.995) matching (id) returning n4
-- @case
update or insert into r (id, n4) values (1, -99.994) matching (id) returning n4
-- @case
update or insert into r (id, d2) values (1, 0.125) matching (id) returning d2
-- @case
update or insert into r (id, d6) values (1, 1.23456789) matching (id) returning d6
-- @case
update or insert into r (id, i) values (1, 2.5) matching (id) returning i
-- @case
update or insert into r (id, i) values (1, -2.5) matching (id) returning i
-- @case
update or insert into r (id, i) values (1, 2.4999999e0) matching (id) returning i
-- @case
update or insert into r (id, sm) values (1, 32767.4) matching (id) returning sm
-- @case
update or insert into r (id, sm) values (1, 32767.5) matching (id) returning sm
-- @case
update or insert into r (id, b) values (1, 9.5) matching (id) returning b
-- @case
update or insert into r (id, f) values (1, 1.005) matching (id) returning f
-- @case
update or insert into r (id, dp) values (1, 1.005) matching (id) returning dp
-- @case
update or insert into r (id, n2) values (1, '1.005') matching (id) returning n2
-- @case
update or insert into r (id, n2) values (1, ' 2.345 ') matching (id) returning n2
-- @case
update or insert into r (id, i) values (1, '2.5') matching (id) returning i
-- @case
update or insert into r (id, i) values (1, '-2.5') matching (id) returning i
-- @case
update or insert into r (id, n0) values (1, '1e2') matching (id) returning n0
-- @case
select n2 / 3, n4 / 3, n2 * n4, n4 * n4, dp / 3, cast(n4 as numeric(9,2)), cast(n4 as numeric(9,3)), cast(dp as numeric(9,1)) from src order by id
-- @case
select avg(n2), avg(n4), avg(dp), avg(cast(n2 * 100 as integer)), sum(n2) / count(*), sum(n4) / 7 from src
-- @case
select avg(n2), avg(n4) from src where id in (1, 3)
-- @case
select avg(n2), avg(n4) from src where id in (2, 4)
-- @case
select avg(x) from (select 1 as x from rdb$database union all select 2 from rdb$database)
-- @case
select avg(x) from (select -1 as x from rdb$database union all select -2 from rdb$database)
-- @case
select avg(x) from (select 1.00 as x from rdb$database union all select 2.00 from rdb$database union all select 2.00 from rdb$database)
-- @case
select round(n2, 1), round(n4, 2), round(n4, 3), round(dp, 1), trunc(n4, 2), round(n2, -1), round(n4) from src order by id
-- @case
select round(sum(n2) / 3, 2), round(avg(n4), 2), round(sum(dp) / 3, 2) from src
-- @case
select cast(1e0 / 3 as numeric(18,17)), cast(2e0 / 3 as numeric(18,16)), cast(0.1e0 + 0.2e0 as numeric(18,17)) from rdb$database
-- @case
select 0.1e0 + 0.2e0 = 0.3e0, 0.1 + 0.2 = 0.3, cast(0.1e0 + 0.2e0 as varchar(30)), cast(1e0 / 3 as varchar(30)) from rdb$database
-- @case
select cast(cast(1.005 as float) as varchar(30)), cast(cast(2.675 as float) as numeric(9,2)), cast(cast(0.1 as float) as double precision) from rdb$database
-- @case
select cast(2.675e0 as varchar(30)), cast(1.005e0 as varchar(30)), cast(0.285e0 as numeric(9,2)), cast(1.4999999999999999e0 as integer) from rdb$database
-- @case
select cast(4.35e0 * 100 as integer), cast(4.35 * 100 as integer), cast(1.15e0 * 100 as integer), cast(0.29e0 * 100 as integer) from rdb$database
-- @case
select cast(9007199254740993 as double precision), cast(9007199254740993e0 as bigint), cast(1e16 + 1 as bigint) from rdb$database
-- @case
select cast(-0.5 as integer), cast(-0.4999 as integer), cast(-1.5e0 as integer), cast(-0.5e0 as smallint), cast(0.5e0 as bigint) from rdb$database
-- @case
select cast(2147483647.5 as integer) from rdb$database
-- @case
select cast(2147483647.4 as integer) from rdb$database
-- @case
select cast(-2147483648.5 as integer) from rdb$database
-- @case
select cast(2147483647.5e0 as integer) from rdb$database
-- @case
select cast(2147483647.49e0 as integer) from rdb$database
-- @case
select cast(9223372036854775807.4 as bigint) from rdb$database
-- @case
select cast(32767.5 as smallint) from rdb$database
-- @case
select cast(-32768.5 as smallint) from rdb$database
-- @case params: [1.005, 1.005, 1.005]
select cast(? as numeric(9,2)), cast(? as integer), cast(? as double precision) from rdb$database
-- @case params: [1.005, 1.005, 1.005]
update or insert into r (id, n2, i, n0) values (2, ?, ?, ?) matching (id) returning n2, i, n0
-- @case params: [1.005, 1.005, 1.005]
select n2 + ?, n2 * ?, n2 / ? from src where id = 1
-- @case params: [2.675, 2.675, 2.675]
select cast(? as numeric(9,2)), cast(? as integer), cast(? as double precision) from rdb$database
-- @case params: [2.675, 2.675, 2.675]
update or insert into r (id, n2, i, n0) values (2, ?, ?, ?) matching (id) returning n2, i, n0
-- @case params: [2.675, 2.675, 2.675]
select n2 + ?, n2 * ?, n2 / ? from src where id = 1
-- @case params: [-1.005, -1.005, -1.005]
select cast(? as numeric(9,2)), cast(? as integer), cast(? as double precision) from rdb$database
-- @case params: [-1.005, -1.005, -1.005]
update or insert into r (id, n2, i, n0) values (2, ?, ?, ?) matching (id) returning n2, i, n0
-- @case params: [-1.005, -1.005, -1.005]
select n2 + ?, n2 * ?, n2 / ? from src where id = 1
-- @case params: [0.125, 0.125, 0.125]
select cast(? as numeric(9,2)), cast(? as integer), cast(? as double precision) from rdb$database
-- @case params: [0.125, 0.125, 0.125]
update or insert into r (id, n2, i, n0) values (2, ?, ?, ?) matching (id) returning n2, i, n0
-- @case params: [0.125, 0.125, 0.125]
select n2 + ?, n2 * ?, n2 / ? from src where id = 1
-- @case params: [1e-3, 1e-3, 1e-3]
select cast(? as numeric(9,2)), cast(? as integer), cast(? as double precision) from rdb$database
-- @case params: [1e-3, 1e-3, 1e-3]
update or insert into r (id, n2, i, n0) values (2, ?, ?, ?) matching (id) returning n2, i, n0
-- @case params: [1e-3, 1e-3, 1e-3]
select n2 + ?, n2 * ?, n2 / ? from src where id = 1
-- @case params: [123.4567, 123.4567, 123.4567]
select cast(? as numeric(9,2)), cast(? as integer), cast(? as double precision) from rdb$database
-- @case params: [123.4567, 123.4567, 123.4567]
update or insert into r (id, n2, i, n0) values (2, ?, ?, ?) matching (id) returning n2, i, n0
-- @case params: [123.4567, 123.4567, 123.4567]
select n2 + ?, n2 * ?, n2 / ? from src where id = 1
