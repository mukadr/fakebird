-- Parameters inside expressions: Firebird describes the expression while its parameters are still of unknown
-- type (the known operand wins, INT64 becoming DOUBLE), then types them from the context: a system function
-- argument's own descriptor, the COALESCE/DECODE list, the other IIF/CASE branch. CAST, aggregates and plain
-- select items leave them unknown.
-- @setup
create table r (id integer not null primary key, s smallint, i integer, b bigint, n numeric(9,2), d numeric(15,2), f double precision, r float, dt date, ts timestamp, tm time, v varchar(10));
insert into r values (1, 1, 2, 3, 4.5, 5.25, 6.5, 7.5, '2024-01-15', '2024-01-15 10:00:00', '10:00:00', '7');
create table g (id integer, p integer, dmax numeric(15,2));
insert into g values (1, null, 10);
insert into g values (2, 5, 20);
-- @case params: [2]
select abs(s - ?) from r
-- @case params: [2]
select abs(? + s) from r
-- @case params: [2]
select abs(s + ?) from r
-- @case params: [2]
select abs(? - s) from r
-- @case params: [2]
select abs(i - ?) from r
-- @case params: [2]
select abs(? + i) from r
-- @case params: [2]
select abs(i + ?) from r
-- @case params: [2]
select abs(? - i) from r
-- @case params: [2]
select abs(b - ?) from r
-- @case params: [2]
select abs(? + b) from r
-- @case params: [2]
select abs(b + ?) from r
-- @case params: [2]
select abs(? - b) from r
-- @case params: [2]
select abs(n - ?) from r
-- @case params: [2]
select abs(? + n) from r
-- @case params: [2]
select abs(n + ?) from r
-- @case params: [2]
select abs(? - n) from r
-- @case params: [2]
select abs(d - ?) from r
-- @case params: [2]
select abs(? + d) from r
-- @case params: [2]
select abs(d + ?) from r
-- @case params: [2]
select abs(? - d) from r
-- @case params: [2]
select abs(f - ?) from r
-- @case params: [2]
select abs(? + f) from r
-- @case params: [2]
select abs(f + ?) from r
-- @case params: [2]
select abs(? - f) from r
-- @case params: [2]
select abs(r - ?) from r
-- @case params: [2]
select abs(? + r) from r
-- @case params: [2]
select abs(r + ?) from r
-- @case params: [2]
select abs(? - r) from r
-- @case params: [2]
select abs(dt - ?) from r
-- @case params: [2]
select abs(? + dt) from r
-- @case params: [2]
select abs(dt + ?) from r
-- @case params: [2]
select abs(? - dt) from r
-- @case params: [2]
select abs(ts - ?) from r
-- @case params: [2]
select abs(? + ts) from r
-- @case params: [2]
select abs(ts + ?) from r
-- @case params: [2]
select abs(? - ts) from r
-- @case params: [2]
select abs(tm - ?) from r
-- @case params: [2]
select abs(? + tm) from r
-- @case params: [2]
select abs(tm + ?) from r
-- @case params: [2]
select abs(? - tm) from r
-- @case params: [2]
select abs(v - ?) from r
-- @case params: [2]
select abs(? + v) from r
-- @case params: [2]
select abs(v + ?) from r
-- @case params: [2]
select abs(? - v) from r
-- @case params: [2]
select round(i - ?, 1) from r
-- @case params: [2]
select round(i - ?) from r
-- @case params: [2]
select trunc(i - ?) from r
-- @case params: [2]
select trunc(i - ?, 1) from r
-- @case params: [2]
select sign(i - ?) from r
-- @case params: [2]
select mod(i - ?, 2) from r
-- @case params: [2]
select mod(10, i - ?) from r
-- @case params: [2]
select sqrt(i - ?) from r
-- @case params: [2]
select power(i - ?, 2) from r
-- @case params: [2]
select power(2, i - ?) from r
-- @case params: [2]
select maxvalue(i - ?, 0) from r
-- @case params: [2]
select maxvalue(i - ?, 1.5) from r
-- @case params: [2]
select minvalue(1, i - ?) from r
-- @case params: [2]
select floor(i - ?) from r
-- @case params: [2]
select ceil(i - ?) from r
-- @case params: [2]
select ceiling(i - ?) from r
-- @case params: [2]
select exp(i - ?) from r
-- @case params: [2]
select ln(i - ?) from r
-- @case params: [2]
select log10(i - ?) from r
-- @case params: [2]
select log(10, i - ?) from r
-- @case params: [2]
select sin(i - ?) from r
-- @case params: [2]
select atan2(i - ?, 1) from r
-- @case params: [2]
select bin_and(i - ?, 1) from r
-- @case params: [2]
select bin_or(i - ?, 1) from r
-- @case params: [2]
select bin_shl(i - ?, 1) from r
-- @case params: [2]
select ascii_char(i - ? + 60) from r
-- @case params: [2]
select char_length(i - ?) from r
-- @case params: [2]
select octet_length(i - ?) from r
-- @case params: [2]
select upper(i - ?) from r
-- @case params: [2]
select trim(i - ?) from r
-- @case params: [2]
select reverse(i - ?) from r
-- @case params: [2]
select hex_encode(i - ?) from r
-- @case params: [2]
select substring('abcdef' from i - ?) from r
-- @case params: [2]
select lpad('a', i - ?) from r
-- @case params: [2]
select lpad('x', 3, i - ?) from r
-- @case params: [2]
select rpad('a', i - ?) from r
-- @case params: [2]
select left('abcdef', i - ?) from r
-- @case params: [2]
select right('abcdef', i - ?) from r
-- @case params: [2]
select position('a', 'abcabc', i - ?) from r
-- @case params: [2]
select replace('abc', 'a', i - ?) from r
-- @case params: [2]
select overlay('abcdef' placing 'x' from i - ?) from r
-- @case params: [2]
select dateadd(day, i - ?, dt) from r
-- @case params: [2]
select dateadd(i - ? day to dt) from r
-- @case params: [2]
select extract(year from dt + (i - ?)) from r
-- @case params: [2]
select coalesce(i - ?, 0) from r
-- @case params: [2]
select coalesce(null, i - ?) from r
-- @case params: [2]
select coalesce(i - ?, 1.5, 0) from r
-- @case params: [2, 2]
select coalesce(i - ?, ?) from r
-- @case params: [2, 2]
select coalesce(?, i - ?) from r
-- @case params: [2]
select coalesce(i - ?, f) from r
-- @case params: [2]
select nullif(i - ?, 0) from r
-- @case params: [2]
select nullif(1, i - ?) from r
-- @case params: [2]
select iif(1=1, i - ?, 0) from r
-- @case params: [2]
select iif(1=0, 0, i - ?) from r
-- @case params: [2]
select iif(1=1, i - ?, 1.5) from r
-- @case params: [2]
select iif(1=1, i - ?, f) from r
-- @case params: [2, 2]
select iif(1=1, i - ?, ?) from r
-- @case params: [2, 2]
select iif(1=1, ?, i - ?) from r
-- @case params: [2, 2]
select iif(1=1, i - ?, i - ?) from r
-- @case params: [2]
select iif(1=1, i - ?, null) from r
-- @case params: [2]
select iif(1=1, null, i - ?) from r
-- @case params: [2]
select case when 1=1 then i - ? else 0 end from r
-- @case params: [2]
select case when 1=1 then i - ? end from r
-- @case params: [2]
select case when 1=0 then 0 else i - ? end from r
-- @case params: [2]
select case when 1=0 then 1 when 1=1 then i - ? else 2.5 end from r
-- @case params: [2]
select case when 1=1 then i - ? when 1=0 then 2.5 else 0 end from r
-- @case params: [2, 2]
select case when 1=1 then i - ? when 1=0 then i - ? else 0 end from r
-- @case params: [2]
select case when 1=1 then i - ? when 1=0 then 2.5 end from r
-- @case params: [2, 2]
select case when 1=0 then ? else i - ? end from r
-- @case params: [2]
select case when 1=1 then i - ? else null end from r
-- @case params: [2]
select decode(1, 1, i - ?, 0) from r
-- @case params: [2]
select decode(1, 1, i - ?, 2, 1.5) from r
-- @case params: [2]
select decode(1, 1, i - ?) from r
-- @case params: [2]
select case 1 when 1 then i - ? else 0 end from r
-- @case params: [2]
select case 1 when 1 then i - ? end from r
-- @case params: [2]
select case 1 when 1 then i - ? when 2 then 1.5 else 0 end from r
-- @case params: [2]
select cast(i - ? as integer) from r
-- @case params: [2]
select cast(i - ? as varchar(10)) from r
-- @case params: [2]
select sum(i - ?) from r
-- @case params: [2]
select avg(i - ?) from r
-- @case params: [2]
select min(i - ?) from r
-- @case params: [2]
select max(i - ?) from r
-- @case params: [2]
select count(i - ?) from r
-- @case params: [2]
select list(i - ?) from r
-- @case params: [2]
select abs(abs(i - ?)) from r
-- @case params: [2]
select abs(coalesce(i - ?, 0)) from r
-- @case params: [2]
select coalesce(abs(i - ?), 0) from r
-- @case params: [2]
select iif(1=1, abs(i - ?), 0) from r
-- @case params: [2]
select abs(iif(1=1, i - ?, 0)) from r
-- @case params: [2]
select i - ? || 'x' from r
-- @case params: [2]
select i - ? from r
-- @case params: [2]
select (i - ?) + 0 from r
-- @case params: [2]
select -(i - ?) from r
-- @case params: [2]
select abs(-(i - ?)) from r
-- @case params: [2]
select round(s + ?, 1) from r
-- @case params: [2]
select round(s + ?) from r
-- @case params: [2]
select trunc(s + ?) from r
-- @case params: [2]
select trunc(s + ?, 1) from r
-- @case params: [2]
select sign(s + ?) from r
-- @case params: [2]
select mod(s + ?, 2) from r
-- @case params: [2]
select mod(10, s + ?) from r
-- @case params: [2]
select sqrt(s + ?) from r
-- @case params: [2]
select power(s + ?, 2) from r
-- @case params: [2]
select power(2, s + ?) from r
-- @case params: [2]
select maxvalue(s + ?, 0) from r
-- @case params: [2]
select maxvalue(s + ?, 1.5) from r
-- @case params: [2]
select minvalue(1, s + ?) from r
-- @case params: [2]
select floor(s + ?) from r
-- @case params: [2]
select ceil(s + ?) from r
-- @case params: [2]
select ceiling(s + ?) from r
-- @case params: [2]
select exp(s + ?) from r
-- @case params: [2]
select ln(s + ?) from r
-- @case params: [2]
select log10(s + ?) from r
-- @case params: [2]
select log(10, s + ?) from r
-- @case params: [2]
select sin(s + ?) from r
-- @case params: [2]
select atan2(s + ?, 1) from r
-- @case params: [2]
select bin_and(s + ?, 1) from r
-- @case params: [2]
select bin_or(s + ?, 1) from r
-- @case params: [2]
select bin_shl(s + ?, 1) from r
-- @case params: [2]
select ascii_char(s + ? + 60) from r
-- @case params: [2]
select char_length(s + ?) from r
-- @case params: [2]
select octet_length(s + ?) from r
-- @case params: [2]
select upper(s + ?) from r
-- @case params: [2]
select trim(s + ?) from r
-- @case params: [2]
select reverse(s + ?) from r
-- @case params: [2]
select hex_encode(s + ?) from r
-- @case params: [2]
select substring('abcdef' from s + ?) from r
-- @case params: [2]
select lpad('a', s + ?) from r
-- @case params: [2]
select lpad('x', 3, s + ?) from r
-- @case params: [2]
select rpad('a', s + ?) from r
-- @case params: [2]
select left('abcdef', s + ?) from r
-- @case params: [2]
select right('abcdef', s + ?) from r
-- @case params: [2]
select position('a', 'abcabc', s + ?) from r
-- @case params: [2]
select replace('abc', 'a', s + ?) from r
-- @case params: [2]
select overlay('abcdef' placing 'x' from s + ?) from r
-- @case params: [2]
select dateadd(day, s + ?, dt) from r
-- @case params: [2]
select dateadd(s + ? day to dt) from r
-- @case params: [2]
select extract(year from dt + (s + ?)) from r
-- @case params: [2]
select coalesce(s + ?, 0) from r
-- @case params: [2]
select coalesce(null, s + ?) from r
-- @case params: [2]
select coalesce(s + ?, 1.5, 0) from r
-- @case params: [2, 2]
select coalesce(s + ?, ?) from r
-- @case params: [2, 2]
select coalesce(?, s + ?) from r
-- @case params: [2]
select coalesce(s + ?, f) from r
-- @case params: [2]
select nullif(s + ?, 0) from r
-- @case params: [2]
select nullif(1, s + ?) from r
-- @case params: [2]
select iif(1=1, s + ?, 0) from r
-- @case params: [2]
select iif(1=0, 0, s + ?) from r
-- @case params: [2]
select iif(1=1, s + ?, 1.5) from r
-- @case params: [2]
select iif(1=1, s + ?, f) from r
-- @case params: [2, 2]
select iif(1=1, s + ?, ?) from r
-- @case params: [2, 2]
select iif(1=1, ?, s + ?) from r
-- @case params: [2, 2]
select iif(1=1, s + ?, s + ?) from r
-- @case params: [2]
select iif(1=1, s + ?, null) from r
-- @case params: [2]
select iif(1=1, null, s + ?) from r
-- @case params: [2]
select case when 1=1 then s + ? else 0 end from r
-- @case params: [2]
select case when 1=1 then s + ? end from r
-- @case params: [2]
select case when 1=0 then 0 else s + ? end from r
-- @case params: [2]
select case when 1=0 then 1 when 1=1 then s + ? else 2.5 end from r
-- @case params: [2]
select case when 1=1 then s + ? when 1=0 then 2.5 else 0 end from r
-- @case params: [2, 2]
select case when 1=1 then s + ? when 1=0 then s + ? else 0 end from r
-- @case params: [2]
select case when 1=1 then s + ? when 1=0 then 2.5 end from r
-- @case params: [2, 2]
select case when 1=0 then ? else s + ? end from r
-- @case params: [2]
select case when 1=1 then s + ? else null end from r
-- @case params: [2]
select decode(1, 1, s + ?, 0) from r
-- @case params: [2]
select decode(1, 1, s + ?, 2, 1.5) from r
-- @case params: [2]
select decode(1, 1, s + ?) from r
-- @case params: [2]
select case 1 when 1 then s + ? else 0 end from r
-- @case params: [2]
select case 1 when 1 then s + ? end from r
-- @case params: [2]
select case 1 when 1 then s + ? when 2 then 1.5 else 0 end from r
-- @case params: [2]
select cast(s + ? as integer) from r
-- @case params: [2]
select cast(s + ? as varchar(10)) from r
-- @case params: [2]
select sum(s + ?) from r
-- @case params: [2]
select avg(s + ?) from r
-- @case params: [2]
select min(s + ?) from r
-- @case params: [2]
select max(s + ?) from r
-- @case params: [2]
select count(s + ?) from r
-- @case params: [2]
select list(s + ?) from r
-- @case params: [2]
select abs(abs(s + ?)) from r
-- @case params: [2]
select abs(coalesce(s + ?, 0)) from r
-- @case params: [2]
select coalesce(abs(s + ?), 0) from r
-- @case params: [2]
select iif(1=1, abs(s + ?), 0) from r
-- @case params: [2]
select abs(iif(1=1, s + ?, 0)) from r
-- @case params: [2]
select s + ? || 'x' from r
-- @case params: [2]
select s + ? from r
-- @case params: [2]
select (s + ?) + 0 from r
-- @case params: [2]
select -(s + ?) from r
-- @case params: [2]
select abs(-(s + ?)) from r
-- @case params: [2]
select round(d - ?, 1) from r
-- @case params: [2]
select round(d - ?) from r
-- @case params: [2]
select trunc(d - ?) from r
-- @case params: [2]
select trunc(d - ?, 1) from r
-- @case params: [2]
select sign(d - ?) from r
-- @case params: [2]
select mod(d - ?, 2) from r
-- @case params: [2]
select mod(10, d - ?) from r
-- @case params: [2]
select sqrt(d - ?) from r
-- @case params: [2]
select power(d - ?, 2) from r
-- @case params: [2]
select power(2, d - ?) from r
-- @case params: [2]
select maxvalue(d - ?, 0) from r
-- @case params: [2]
select maxvalue(d - ?, 1.5) from r
-- @case params: [2]
select minvalue(1, d - ?) from r
-- @case params: [2]
select floor(d - ?) from r
-- @case params: [2]
select ceil(d - ?) from r
-- @case params: [2]
select ceiling(d - ?) from r
-- @case params: [2]
select exp(d - ?) from r
-- @case params: [2]
select ln(d - ?) from r
-- @case params: [2]
select log10(d - ?) from r
-- @case params: [2]
select log(10, d - ?) from r
-- @case params: [2]
select sin(d - ?) from r
-- @case params: [2]
select atan2(d - ?, 1) from r
-- @case params: [2]
select bin_and(d - ?, 1) from r
-- @case params: [2]
select bin_or(d - ?, 1) from r
-- @case params: [2]
select bin_shl(d - ?, 1) from r
-- @case params: [2]
select ascii_char(d - ? + 60) from r
-- @case params: [2]
select char_length(d - ?) from r
-- @case params: [2]
select octet_length(d - ?) from r
-- @case params: [2]
select upper(d - ?) from r
-- @case params: [2]
select trim(d - ?) from r
-- @case params: [2]
select reverse(d - ?) from r
-- @case params: [2]
select hex_encode(d - ?) from r
-- @case params: [2]
select substring('abcdef' from d - ?) from r
-- @case params: [2]
select lpad('a', d - ?) from r
-- @case params: [2]
select lpad('x', 3, d - ?) from r
-- @case params: [2]
select rpad('a', d - ?) from r
-- @case params: [2]
select left('abcdef', d - ?) from r
-- @case params: [2]
select right('abcdef', d - ?) from r
-- @case params: [2]
select position('a', 'abcabc', d - ?) from r
-- @case params: [2]
select replace('abc', 'a', d - ?) from r
-- @case params: [2]
select overlay('abcdef' placing 'x' from d - ?) from r
-- @case params: [2]
select dateadd(day, d - ?, dt) from r
-- @case params: [2]
select dateadd(d - ? day to dt) from r
-- @case params: [2]
select extract(year from dt + (d - ?)) from r
-- @case params: [2]
select coalesce(d - ?, 0) from r
-- @case params: [2]
select coalesce(null, d - ?) from r
-- @case params: [2]
select coalesce(d - ?, 1.5, 0) from r
-- @case params: [2, 2]
select coalesce(d - ?, ?) from r
-- @case params: [2, 2]
select coalesce(?, d - ?) from r
-- @case params: [2]
select coalesce(d - ?, f) from r
-- @case params: [2]
select nullif(d - ?, 0) from r
-- @case params: [2]
select nullif(1, d - ?) from r
-- @case params: [2]
select iif(1=1, d - ?, 0) from r
-- @case params: [2]
select iif(1=0, 0, d - ?) from r
-- @case params: [2]
select iif(1=1, d - ?, 1.5) from r
-- @case params: [2]
select iif(1=1, d - ?, f) from r
-- @case params: [2, 2]
select iif(1=1, d - ?, ?) from r
-- @case params: [2, 2]
select iif(1=1, ?, d - ?) from r
-- @case params: [2, 2]
select iif(1=1, d - ?, d - ?) from r
-- @case params: [2]
select iif(1=1, d - ?, null) from r
-- @case params: [2]
select iif(1=1, null, d - ?) from r
-- @case params: [2]
select case when 1=1 then d - ? else 0 end from r
-- @case params: [2]
select case when 1=1 then d - ? end from r
-- @case params: [2]
select case when 1=0 then 0 else d - ? end from r
-- @case params: [2]
select case when 1=0 then 1 when 1=1 then d - ? else 2.5 end from r
-- @case params: [2]
select case when 1=1 then d - ? when 1=0 then 2.5 else 0 end from r
-- @case params: [2, 2]
select case when 1=1 then d - ? when 1=0 then d - ? else 0 end from r
-- @case params: [2]
select case when 1=1 then d - ? when 1=0 then 2.5 end from r
-- @case params: [2, 2]
select case when 1=0 then ? else d - ? end from r
-- @case params: [2]
select case when 1=1 then d - ? else null end from r
-- @case params: [2]
select decode(1, 1, d - ?, 0) from r
-- @case params: [2]
select decode(1, 1, d - ?, 2, 1.5) from r
-- @case params: [2]
select decode(1, 1, d - ?) from r
-- @case params: [2]
select case 1 when 1 then d - ? else 0 end from r
-- @case params: [2]
select case 1 when 1 then d - ? end from r
-- @case params: [2]
select case 1 when 1 then d - ? when 2 then 1.5 else 0 end from r
-- @case params: [2]
select cast(d - ? as integer) from r
-- @case params: [2]
select cast(d - ? as varchar(10)) from r
-- @case params: [2]
select sum(d - ?) from r
-- @case params: [2]
select avg(d - ?) from r
-- @case params: [2]
select min(d - ?) from r
-- @case params: [2]
select max(d - ?) from r
-- @case params: [2]
select count(d - ?) from r
-- @case params: [2]
select list(d - ?) from r
-- @case params: [2]
select abs(abs(d - ?)) from r
-- @case params: [2]
select abs(coalesce(d - ?, 0)) from r
-- @case params: [2]
select coalesce(abs(d - ?), 0) from r
-- @case params: [2]
select iif(1=1, abs(d - ?), 0) from r
-- @case params: [2]
select abs(iif(1=1, d - ?, 0)) from r
-- @case params: [2]
select d - ? || 'x' from r
-- @case params: [2]
select d - ? from r
-- @case params: [2]
select (d - ?) + 0 from r
-- @case params: [2]
select -(d - ?) from r
-- @case params: [2]
select abs(-(d - ?)) from r
-- @case params: [2]
select round(n + ?, 1) from r
-- @case params: [2]
select round(n + ?) from r
-- @case params: [2]
select trunc(n + ?) from r
-- @case params: [2]
select trunc(n + ?, 1) from r
-- @case params: [2]
select sign(n + ?) from r
-- @case params: [2]
select mod(n + ?, 2) from r
-- @case params: [2]
select mod(10, n + ?) from r
-- @case params: [2]
select sqrt(n + ?) from r
-- @case params: [2]
select power(n + ?, 2) from r
-- @case params: [2]
select power(2, n + ?) from r
-- @case params: [2]
select maxvalue(n + ?, 0) from r
-- @case params: [2]
select maxvalue(n + ?, 1.5) from r
-- @case params: [2]
select minvalue(1, n + ?) from r
-- @case params: [2]
select floor(n + ?) from r
-- @case params: [2]
select ceil(n + ?) from r
-- @case params: [2]
select ceiling(n + ?) from r
-- @case params: [2]
select exp(n + ?) from r
-- @case params: [2]
select ln(n + ?) from r
-- @case params: [2]
select log10(n + ?) from r
-- @case params: [2]
select log(10, n + ?) from r
-- @case params: [2]
select sin(n + ?) from r
-- @case params: [2]
select atan2(n + ?, 1) from r
-- @case params: [2]
select bin_and(n + ?, 1) from r
-- @case params: [2]
select bin_or(n + ?, 1) from r
-- @case params: [2]
select bin_shl(n + ?, 1) from r
-- @case params: [2]
select ascii_char(n + ? + 60) from r
-- @case params: [2]
select char_length(n + ?) from r
-- @case params: [2]
select octet_length(n + ?) from r
-- @case params: [2]
select upper(n + ?) from r
-- @case params: [2]
select trim(n + ?) from r
-- @case params: [2]
select reverse(n + ?) from r
-- @case params: [2]
select hex_encode(n + ?) from r
-- @case params: [2]
select substring('abcdef' from n + ?) from r
-- @case params: [2]
select lpad('a', n + ?) from r
-- @case params: [2]
select lpad('x', 3, n + ?) from r
-- @case params: [2]
select rpad('a', n + ?) from r
-- @case params: [2]
select left('abcdef', n + ?) from r
-- @case params: [2]
select right('abcdef', n + ?) from r
-- @case params: [2]
select position('a', 'abcabc', n + ?) from r
-- @case params: [2]
select replace('abc', 'a', n + ?) from r
-- @case params: [2]
select overlay('abcdef' placing 'x' from n + ?) from r
-- @case params: [2]
select dateadd(day, n + ?, dt) from r
-- @case params: [2]
select dateadd(n + ? day to dt) from r
-- @case params: [2]
select extract(year from dt + (n + ?)) from r
-- @case params: [2]
select coalesce(n + ?, 0) from r
-- @case params: [2]
select coalesce(null, n + ?) from r
-- @case params: [2]
select coalesce(n + ?, 1.5, 0) from r
-- @case params: [2, 2]
select coalesce(n + ?, ?) from r
-- @case params: [2, 2]
select coalesce(?, n + ?) from r
-- @case params: [2]
select coalesce(n + ?, f) from r
-- @case params: [2]
select nullif(n + ?, 0) from r
-- @case params: [2]
select nullif(1, n + ?) from r
-- @case params: [2]
select iif(1=1, n + ?, 0) from r
-- @case params: [2]
select iif(1=0, 0, n + ?) from r
-- @case params: [2]
select iif(1=1, n + ?, 1.5) from r
-- @case params: [2]
select iif(1=1, n + ?, f) from r
-- @case params: [2, 2]
select iif(1=1, n + ?, ?) from r
-- @case params: [2, 2]
select iif(1=1, ?, n + ?) from r
-- @case params: [2, 2]
select iif(1=1, n + ?, n + ?) from r
-- @case params: [2]
select iif(1=1, n + ?, null) from r
-- @case params: [2]
select iif(1=1, null, n + ?) from r
-- @case params: [2]
select case when 1=1 then n + ? else 0 end from r
-- @case params: [2]
select case when 1=1 then n + ? end from r
-- @case params: [2]
select case when 1=0 then 0 else n + ? end from r
-- @case params: [2]
select case when 1=0 then 1 when 1=1 then n + ? else 2.5 end from r
-- @case params: [2]
select case when 1=1 then n + ? when 1=0 then 2.5 else 0 end from r
-- @case params: [2, 2]
select case when 1=1 then n + ? when 1=0 then n + ? else 0 end from r
-- @case params: [2]
select case when 1=1 then n + ? when 1=0 then 2.5 end from r
-- @case params: [2, 2]
select case when 1=0 then ? else n + ? end from r
-- @case params: [2]
select case when 1=1 then n + ? else null end from r
-- @case params: [2]
select decode(1, 1, n + ?, 0) from r
-- @case params: [2]
select decode(1, 1, n + ?, 2, 1.5) from r
-- @case params: [2]
select decode(1, 1, n + ?) from r
-- @case params: [2]
select case 1 when 1 then n + ? else 0 end from r
-- @case params: [2]
select case 1 when 1 then n + ? end from r
-- @case params: [2]
select case 1 when 1 then n + ? when 2 then 1.5 else 0 end from r
-- @case params: [2]
select cast(n + ? as integer) from r
-- @case params: [2]
select cast(n + ? as varchar(10)) from r
-- @case params: [2]
select sum(n + ?) from r
-- @case params: [2]
select avg(n + ?) from r
-- @case params: [2]
select min(n + ?) from r
-- @case params: [2]
select max(n + ?) from r
-- @case params: [2]
select count(n + ?) from r
-- @case params: [2]
select list(n + ?) from r
-- @case params: [2]
select abs(abs(n + ?)) from r
-- @case params: [2]
select abs(coalesce(n + ?, 0)) from r
-- @case params: [2]
select coalesce(abs(n + ?), 0) from r
-- @case params: [2]
select iif(1=1, abs(n + ?), 0) from r
-- @case params: [2]
select abs(iif(1=1, n + ?, 0)) from r
-- @case params: [2]
select n + ? || 'x' from r
-- @case params: [2]
select n + ? from r
-- @case params: [2]
select (n + ?) + 0 from r
-- @case params: [2]
select -(n + ?) from r
-- @case params: [2]
select abs(-(n + ?)) from r
-- @case params: [2]
select round(f - ?, 1) from r
-- @case params: [2]
select round(f - ?) from r
-- @case params: [2]
select trunc(f - ?) from r
-- @case params: [2]
select trunc(f - ?, 1) from r
-- @case params: [2]
select sign(f - ?) from r
-- @case params: [2]
select mod(f - ?, 2) from r
-- @case params: [2]
select mod(10, f - ?) from r
-- @case params: [2]
select sqrt(f - ?) from r
-- @case params: [2]
select power(f - ?, 2) from r
-- @case params: [2]
select power(2, f - ?) from r
-- @case params: [2]
select maxvalue(f - ?, 0) from r
-- @case params: [2]
select maxvalue(f - ?, 1.5) from r
-- @case params: [2]
select minvalue(1, f - ?) from r
-- @case params: [2]
select floor(f - ?) from r
-- @case params: [2]
select ceil(f - ?) from r
-- @case params: [2]
select ceiling(f - ?) from r
-- @case params: [2]
select exp(f - ?) from r
-- @case params: [2]
select ln(f - ?) from r
-- @case params: [2]
select log10(f - ?) from r
-- @case params: [2]
select log(10, f - ?) from r
-- @case params: [2]
select sin(f - ?) from r
-- @case params: [2]
select atan2(f - ?, 1) from r
-- @case params: [2]
select bin_and(f - ?, 1) from r
-- @case params: [2]
select bin_or(f - ?, 1) from r
-- @case params: [2]
select bin_shl(f - ?, 1) from r
-- @case params: [2]
select ascii_char(f - ? + 60) from r
-- @case params: [2]
select char_length(f - ?) from r
-- @case params: [2]
select octet_length(f - ?) from r
-- @case params: [2]
select upper(f - ?) from r
-- @case params: [2]
select trim(f - ?) from r
-- @case params: [2]
select reverse(f - ?) from r
-- @case params: [2]
select hex_encode(f - ?) from r
-- @case params: [2]
select substring('abcdef' from f - ?) from r
-- @case params: [2]
select lpad('a', f - ?) from r
-- @case params: [2]
select lpad('x', 3, f - ?) from r
-- @case params: [2]
select rpad('a', f - ?) from r
-- @case params: [2]
select left('abcdef', f - ?) from r
-- @case params: [2]
select right('abcdef', f - ?) from r
-- @case params: [2]
select position('a', 'abcabc', f - ?) from r
-- @case params: [2]
select replace('abc', 'a', f - ?) from r
-- @case params: [2]
select overlay('abcdef' placing 'x' from f - ?) from r
-- @case params: [2]
select dateadd(day, f - ?, dt) from r
-- @case params: [2]
select dateadd(f - ? day to dt) from r
-- @case params: [2]
select extract(year from dt + (f - ?)) from r
-- @case params: [2]
select coalesce(f - ?, 0) from r
-- @case params: [2]
select coalesce(null, f - ?) from r
-- @case params: [2]
select coalesce(f - ?, 1.5, 0) from r
-- @case params: [2, 2]
select coalesce(f - ?, ?) from r
-- @case params: [2, 2]
select coalesce(?, f - ?) from r
-- @case params: [2]
select coalesce(f - ?, f) from r
-- @case params: [2]
select nullif(f - ?, 0) from r
-- @case params: [2]
select nullif(1, f - ?) from r
-- @case params: [2]
select iif(1=1, f - ?, 0) from r
-- @case params: [2]
select iif(1=0, 0, f - ?) from r
-- @case params: [2]
select iif(1=1, f - ?, 1.5) from r
-- @case params: [2]
select iif(1=1, f - ?, f) from r
-- @case params: [2, 2]
select iif(1=1, f - ?, ?) from r
-- @case params: [2, 2]
select iif(1=1, ?, f - ?) from r
-- @case params: [2, 2]
select iif(1=1, f - ?, f - ?) from r
-- @case params: [2]
select iif(1=1, f - ?, null) from r
-- @case params: [2]
select iif(1=1, null, f - ?) from r
-- @case params: [2]
select case when 1=1 then f - ? else 0 end from r
-- @case params: [2]
select case when 1=1 then f - ? end from r
-- @case params: [2]
select case when 1=0 then 0 else f - ? end from r
-- @case params: [2]
select case when 1=0 then 1 when 1=1 then f - ? else 2.5 end from r
-- @case params: [2]
select case when 1=1 then f - ? when 1=0 then 2.5 else 0 end from r
-- @case params: [2, 2]
select case when 1=1 then f - ? when 1=0 then f - ? else 0 end from r
-- @case params: [2]
select case when 1=1 then f - ? when 1=0 then 2.5 end from r
-- @case params: [2, 2]
select case when 1=0 then ? else f - ? end from r
-- @case params: [2]
select case when 1=1 then f - ? else null end from r
-- @case params: [2]
select decode(1, 1, f - ?, 0) from r
-- @case params: [2]
select decode(1, 1, f - ?, 2, 1.5) from r
-- @case params: [2]
select decode(1, 1, f - ?) from r
-- @case params: [2]
select case 1 when 1 then f - ? else 0 end from r
-- @case params: [2]
select case 1 when 1 then f - ? end from r
-- @case params: [2]
select case 1 when 1 then f - ? when 2 then 1.5 else 0 end from r
-- @case params: [2]
select cast(f - ? as integer) from r
-- @case params: [2]
select cast(f - ? as varchar(10)) from r
-- @case params: [2]
select sum(f - ?) from r
-- @case params: [2]
select avg(f - ?) from r
-- @case params: [2]
select min(f - ?) from r
-- @case params: [2]
select max(f - ?) from r
-- @case params: [2]
select count(f - ?) from r
-- @case params: [2]
select list(f - ?) from r
-- @case params: [2]
select abs(abs(f - ?)) from r
-- @case params: [2]
select abs(coalesce(f - ?, 0)) from r
-- @case params: [2]
select coalesce(abs(f - ?), 0) from r
-- @case params: [2]
select iif(1=1, abs(f - ?), 0) from r
-- @case params: [2]
select abs(iif(1=1, f - ?, 0)) from r
-- @case params: [2]
select f - ? || 'x' from r
-- @case params: [2]
select f - ? from r
-- @case params: [2]
select (f - ?) + 0 from r
-- @case params: [2]
select -(f - ?) from r
-- @case params: [2]
select abs(-(f - ?)) from r
-- @case params: [2]
select round(? - b, 1) from r
-- @case params: [2]
select round(? - b) from r
-- @case params: [2]
select trunc(? - b) from r
-- @case params: [2]
select trunc(? - b, 1) from r
-- @case params: [2]
select sign(? - b) from r
-- @case params: [2]
select mod(? - b, 2) from r
-- @case params: [2]
select mod(10, ? - b) from r
-- @case params: [2]
select sqrt(? - b) from r
-- @case params: [2]
select power(? - b, 2) from r
-- @case params: [2]
select power(2, ? - b) from r
-- @case params: [2]
select maxvalue(? - b, 0) from r
-- @case params: [2]
select maxvalue(? - b, 1.5) from r
-- @case params: [2]
select minvalue(1, ? - b) from r
-- @case params: [2]
select floor(? - b) from r
-- @case params: [2]
select ceil(? - b) from r
-- @case params: [2]
select ceiling(? - b) from r
-- @case params: [2]
select exp(? - b) from r
-- @case params: [2]
select ln(? - b) from r
-- @case params: [2]
select log10(? - b) from r
-- @case params: [2]
select log(10, ? - b) from r
-- @case params: [2]
select sin(? - b) from r
-- @case params: [2]
select atan2(? - b, 1) from r
-- @case params: [2]
select bin_and(? - b, 1) from r
-- @case params: [2]
select bin_or(? - b, 1) from r
-- @case params: [2]
select bin_shl(? - b, 1) from r
-- @case params: [2]
select ascii_char(? - b + 60) from r
-- @case params: [2]
select char_length(? - b) from r
-- @case params: [2]
select octet_length(? - b) from r
-- @case params: [2]
select upper(? - b) from r
-- @case params: [2]
select trim(? - b) from r
-- @case params: [2]
select reverse(? - b) from r
-- @case params: [2]
select hex_encode(? - b) from r
-- @case params: [2]
select substring('abcdef' from ? - b) from r
-- @case params: [2]
select lpad('a', ? - b) from r
-- @case params: [2]
select lpad('x', 3, ? - b) from r
-- @case params: [2]
select rpad('a', ? - b) from r
-- @case params: [2]
select left('abcdef', ? - b) from r
-- @case params: [2]
select right('abcdef', ? - b) from r
-- @case params: [2]
select position('a', 'abcabc', ? - b) from r
-- @case params: [2]
select replace('abc', 'a', ? - b) from r
-- @case params: [2]
select overlay('abcdef' placing 'x' from ? - b) from r
-- @case params: [2]
select dateadd(day, ? - b, dt) from r
-- @case params: [2]
select dateadd(? - b day to dt) from r
-- @case params: [2]
select extract(year from dt + (? - b)) from r
-- @case params: [2]
select coalesce(? - b, 0) from r
-- @case params: [2]
select coalesce(null, ? - b) from r
-- @case params: [2]
select coalesce(? - b, 1.5, 0) from r
-- @case params: [2, 2]
select coalesce(? - b, ?) from r
-- @case params: [2, 2]
select coalesce(?, ? - b) from r
-- @case params: [2]
select coalesce(? - b, f) from r
-- @case params: [2]
select nullif(? - b, 0) from r
-- @case params: [2]
select nullif(1, ? - b) from r
-- @case params: [2]
select iif(1=1, ? - b, 0) from r
-- @case params: [2]
select iif(1=0, 0, ? - b) from r
-- @case params: [2]
select iif(1=1, ? - b, 1.5) from r
-- @case params: [2]
select iif(1=1, ? - b, f) from r
-- @case params: [2, 2]
select iif(1=1, ? - b, ?) from r
-- @case params: [2, 2]
select iif(1=1, ?, ? - b) from r
-- @case params: [2, 2]
select iif(1=1, ? - b, ? - b) from r
-- @case params: [2]
select iif(1=1, ? - b, null) from r
-- @case params: [2]
select iif(1=1, null, ? - b) from r
-- @case params: [2]
select case when 1=1 then ? - b else 0 end from r
-- @case params: [2]
select case when 1=1 then ? - b end from r
-- @case params: [2]
select case when 1=0 then 0 else ? - b end from r
-- @case params: [2]
select case when 1=0 then 1 when 1=1 then ? - b else 2.5 end from r
-- @case params: [2]
select case when 1=1 then ? - b when 1=0 then 2.5 else 0 end from r
-- @case params: [2, 2]
select case when 1=1 then ? - b when 1=0 then ? - b else 0 end from r
-- @case params: [2]
select case when 1=1 then ? - b when 1=0 then 2.5 end from r
-- @case params: [2, 2]
select case when 1=0 then ? else ? - b end from r
-- @case params: [2]
select case when 1=1 then ? - b else null end from r
-- @case params: [2]
select decode(1, 1, ? - b, 0) from r
-- @case params: [2]
select decode(1, 1, ? - b, 2, 1.5) from r
-- @case params: [2]
select decode(1, 1, ? - b) from r
-- @case params: [2]
select case 1 when 1 then ? - b else 0 end from r
-- @case params: [2]
select case 1 when 1 then ? - b end from r
-- @case params: [2]
select case 1 when 1 then ? - b when 2 then 1.5 else 0 end from r
-- @case params: [2]
select cast(? - b as integer) from r
-- @case params: [2]
select cast(? - b as varchar(10)) from r
-- @case params: [2]
select sum(? - b) from r
-- @case params: [2]
select avg(? - b) from r
-- @case params: [2]
select min(? - b) from r
-- @case params: [2]
select max(? - b) from r
-- @case params: [2]
select count(? - b) from r
-- @case params: [2]
select list(? - b) from r
-- @case params: [2]
select abs(abs(? - b)) from r
-- @case params: [2]
select abs(coalesce(? - b, 0)) from r
-- @case params: [2]
select coalesce(abs(? - b), 0) from r
-- @case params: [2]
select iif(1=1, abs(? - b), 0) from r
-- @case params: [2]
select abs(iif(1=1, ? - b, 0)) from r
-- @case params: [2]
select ? - b || 'x' from r
-- @case params: [2]
select ? - b from r
-- @case params: [2]
select (? - b) + 0 from r
-- @case params: [2]
select -(? - b) from r
-- @case params: [2]
select abs(-(? - b)) from r
-- @case params: [2]
select round(? + r, 1) from r
-- @case params: [2]
select round(? + r) from r
-- @case params: [2]
select trunc(? + r) from r
-- @case params: [2]
select trunc(? + r, 1) from r
-- @case params: [2]
select sign(? + r) from r
-- @case params: [2]
select mod(? + r, 2) from r
-- @case params: [2]
select mod(10, ? + r) from r
-- @case params: [2]
select sqrt(? + r) from r
-- @case params: [2]
select power(? + r, 2) from r
-- @case params: [2]
select power(2, ? + r) from r
-- @case params: [2]
select maxvalue(? + r, 0) from r
-- @case params: [2]
select maxvalue(? + r, 1.5) from r
-- @case params: [2]
select minvalue(1, ? + r) from r
-- @case params: [2]
select floor(? + r) from r
-- @case params: [2]
select ceil(? + r) from r
-- @case params: [2]
select ceiling(? + r) from r
-- @case params: [2]
select exp(? + r) from r
-- @case params: [2]
select ln(? + r) from r
-- @case params: [2]
select log10(? + r) from r
-- @case params: [2]
select log(10, ? + r) from r
-- @case params: [2]
select sin(? + r) from r
-- @case params: [2]
select atan2(? + r, 1) from r
-- @case params: [2]
select bin_and(? + r, 1) from r
-- @case params: [2]
select bin_or(? + r, 1) from r
-- @case params: [2]
select bin_shl(? + r, 1) from r
-- @case params: [2]
select ascii_char(? + r + 60) from r
-- @case params: [2]
select char_length(? + r) from r
-- @case params: [2]
select octet_length(? + r) from r
-- @case params: [2]
select upper(? + r) from r
-- @case params: [2]
select trim(? + r) from r
-- @case params: [2]
select reverse(? + r) from r
-- @case params: [2]
select hex_encode(? + r) from r
-- @case params: [2]
select substring('abcdef' from ? + r) from r
-- @case params: [2]
select lpad('a', ? + r) from r
-- @case params: [2]
select lpad('x', 3, ? + r) from r
-- @case params: [2]
select rpad('a', ? + r) from r
-- @case params: [2]
select left('abcdef', ? + r) from r
-- @case params: [2]
select right('abcdef', ? + r) from r
-- @case params: [2]
select position('a', 'abcabc', ? + r) from r
-- @case params: [2]
select replace('abc', 'a', ? + r) from r
-- @case params: [2]
select overlay('abcdef' placing 'x' from ? + r) from r
-- @case params: [2]
select dateadd(day, ? + r, dt) from r
-- @case params: [2]
select dateadd(? + r day to dt) from r
-- @case params: [2]
select extract(year from dt + (? + r)) from r
-- @case params: [2]
select coalesce(? + r, 0) from r
-- @case params: [2]
select coalesce(null, ? + r) from r
-- @case params: [2]
select coalesce(? + r, 1.5, 0) from r
-- @case params: [2, 2]
select coalesce(? + r, ?) from r
-- @case params: [2, 2]
select coalesce(?, ? + r) from r
-- @case params: [2]
select coalesce(? + r, f) from r
-- @case params: [2]
select nullif(? + r, 0) from r
-- @case params: [2]
select nullif(1, ? + r) from r
-- @case params: [2]
select iif(1=1, ? + r, 0) from r
-- @case params: [2]
select iif(1=0, 0, ? + r) from r
-- @case params: [2]
select iif(1=1, ? + r, 1.5) from r
-- @case params: [2]
select iif(1=1, ? + r, f) from r
-- @case params: [2, 2]
select iif(1=1, ? + r, ?) from r
-- @case params: [2, 2]
select iif(1=1, ?, ? + r) from r
-- @case params: [2, 2]
select iif(1=1, ? + r, ? + r) from r
-- @case params: [2]
select iif(1=1, ? + r, null) from r
-- @case params: [2]
select iif(1=1, null, ? + r) from r
-- @case params: [2]
select case when 1=1 then ? + r else 0 end from r
-- @case params: [2]
select case when 1=1 then ? + r end from r
-- @case params: [2]
select case when 1=0 then 0 else ? + r end from r
-- @case params: [2]
select case when 1=0 then 1 when 1=1 then ? + r else 2.5 end from r
-- @case params: [2]
select case when 1=1 then ? + r when 1=0 then 2.5 else 0 end from r
-- @case params: [2, 2]
select case when 1=1 then ? + r when 1=0 then ? + r else 0 end from r
-- @case params: [2]
select case when 1=1 then ? + r when 1=0 then 2.5 end from r
-- @case params: [2, 2]
select case when 1=0 then ? else ? + r end from r
-- @case params: [2]
select case when 1=1 then ? + r else null end from r
-- @case params: [2]
select decode(1, 1, ? + r, 0) from r
-- @case params: [2]
select decode(1, 1, ? + r, 2, 1.5) from r
-- @case params: [2]
select decode(1, 1, ? + r) from r
-- @case params: [2]
select case 1 when 1 then ? + r else 0 end from r
-- @case params: [2]
select case 1 when 1 then ? + r end from r
-- @case params: [2]
select case 1 when 1 then ? + r when 2 then 1.5 else 0 end from r
-- @case params: [2]
select cast(? + r as integer) from r
-- @case params: [2]
select cast(? + r as varchar(10)) from r
-- @case params: [2]
select sum(? + r) from r
-- @case params: [2]
select avg(? + r) from r
-- @case params: [2]
select min(? + r) from r
-- @case params: [2]
select max(? + r) from r
-- @case params: [2]
select count(? + r) from r
-- @case params: [2]
select list(? + r) from r
-- @case params: [2]
select abs(abs(? + r)) from r
-- @case params: [2]
select abs(coalesce(? + r, 0)) from r
-- @case params: [2]
select coalesce(abs(? + r), 0) from r
-- @case params: [2]
select iif(1=1, abs(? + r), 0) from r
-- @case params: [2]
select abs(iif(1=1, ? + r, 0)) from r
-- @case params: [2]
select ? + r || 'x' from r
-- @case params: [2]
select ? + r from r
-- @case params: [2]
select (? + r) + 0 from r
-- @case params: [2]
select -(? + r) from r
-- @case params: [2]
select abs(-(? + r)) from r
-- @case params: [2]
select abs(i * ?) from r
-- @case params: [2]
select abs(i / ?) from r
-- @case params: [2]
select abs(-i - ?) from r
-- @case params: [2, 2]
select abs(i - ? - ?) from r
-- @case params: [2]
select abs((i - ?) / 2) from r
-- @case params: [2]
select abs((i - ?) * 2) from r
-- @case params: [2]
select abs(i - (? - 1)) from r
-- @case params: [2]
select abs(i + (1 + ?)) from r
-- @case params: [2]
select abs(1 + i - ?) from r
-- @case params: [2, 2]
select abs(i - ? * ?) from r
-- @case params: [2, 2]
select abs(? - ? + i) from r
-- @case params: [2]
select abs(i - cast(? as integer)) from r
-- @case params: [2, 2]
select round(i - ?, ?) from r
-- @case params: [2, 2]
select mod(i - ?, ?) from r
-- @case params: [2]
select abs(n * ?) from r
-- @case params: [2]
select abs(n / ?) from r
-- @case params: [2]
select abs(-n - ?) from r
-- @case params: [2, 2]
select abs(n - ? - ?) from r
-- @case params: [2]
select abs((n - ?) / 2) from r
-- @case params: [2]
select abs((n - ?) * 2) from r
-- @case params: [2]
select abs(n - (? - 1)) from r
-- @case params: [2]
select abs(n + (1 + ?)) from r
-- @case params: [2]
select abs(1 + n - ?) from r
-- @case params: [2, 2]
select abs(n - ? * ?) from r
-- @case params: [2, 2]
select abs(? - ? + n) from r
-- @case params: [2]
select abs(n - cast(? as integer)) from r
-- @case params: [2, 2]
select round(n - ?, ?) from r
-- @case params: [2, 2]
select mod(n - ?, ?) from r
-- @case params: [2]
select abs(d * ?) from r
-- @case params: [2]
select abs(d / ?) from r
-- @case params: [2]
select abs(-d - ?) from r
-- @case params: [2, 2]
select abs(d - ? - ?) from r
-- @case params: [2]
select abs((d - ?) / 2) from r
-- @case params: [2]
select abs((d - ?) * 2) from r
-- @case params: [2]
select abs(d - (? - 1)) from r
-- @case params: [2]
select abs(d + (1 + ?)) from r
-- @case params: [2]
select abs(1 + d - ?) from r
-- @case params: [2, 2]
select abs(d - ? * ?) from r
-- @case params: [2, 2]
select abs(? - ? + d) from r
-- @case params: [2]
select abs(d - cast(? as integer)) from r
-- @case params: [2, 2]
select round(d - ?, ?) from r
-- @case params: [2, 2]
select mod(d - ?, ?) from r
-- @case params: [2]
select abs(f * ?) from r
-- @case params: [2]
select abs(f / ?) from r
-- @case params: [2]
select abs(-f - ?) from r
-- @case params: [2, 2]
select abs(f - ? - ?) from r
-- @case params: [2]
select abs((f - ?) / 2) from r
-- @case params: [2]
select abs((f - ?) * 2) from r
-- @case params: [2]
select abs(f - (? - 1)) from r
-- @case params: [2]
select abs(f + (1 + ?)) from r
-- @case params: [2]
select abs(1 + f - ?) from r
-- @case params: [2, 2]
select abs(f - ? * ?) from r
-- @case params: [2, 2]
select abs(? - ? + f) from r
-- @case params: [2]
select abs(f - cast(? as integer)) from r
-- @case params: [2, 2]
select round(f - ?, ?) from r
-- @case params: [2, 2]
select mod(f - ?, ?) from r
-- @case params: [2]
select abs(1 - ?) from r
-- @case params: [2]
select coalesce(1 - ?, 0) from r
-- @case params: [2]
select abs(1.5 - ?) from r
-- @case params: [2]
select coalesce(1.5 - ?, 0) from r
-- @case params: [2]
select abs(? - 1e0) from r
-- @case params: [2]
select coalesce(? - 1e0, 0) from r
-- @case params: [2]
select abs(? + 2147483648) from r
-- @case params: [2]
select coalesce(? + 2147483648, 0) from r
-- @case params: [2]
select abs(? - 0.001) from r
-- @case params: [2]
select coalesce(? - 0.001, 0) from r
-- @case params: [2]
select abs(cast(1 as smallint) + ?) from r
-- @case params: [2]
select coalesce(cast(1 as smallint) + ?, 0) from r
-- @case params: [2]
select abs(id + ?) from r
-- @case params: [2]
select coalesce(id + ?, 0) from r
-- @case params: [2]
select abs(char_length(v) - ?) from r
-- @case params: [2]
select coalesce(char_length(v) - ?, 0) from r
-- @case params: [2]
select abs(extract(day from dt) - ?) from r
-- @case params: [2]
select coalesce(extract(day from dt) - ?, 0) from r
-- @case params: [2]
select abs(datediff(day, dt, dt) - ?) from r
-- @case params: [2]
select coalesce(datediff(day, dt, dt) - ?, 0) from r
-- @case params: [2]
select abs((select max(i) from r) - ?) from r
-- @case params: [2]
select coalesce((select max(i) from r) - ?, 0) from r
-- @case params: [2]
select abs(cast(? as integer) - d) from r
-- @case params: [2]
select coalesce(cast(? as integer) - d, 0) from r
-- @case params: [2.5]
select round(i - ?, 2) from r
-- @case params: [2.555]
select round(d - ?, 2) from r
-- @case params: [1.005]
select round(n + ?, 2) from r
-- @case params: [0.1]
select round(f - ?, 2) from r
-- @case params: [1]
select 1 from r where d - ? > 0
-- @case params: [1]
select 1 from r where abs(d - ?) > 0
-- @case params: [1]
select 1 from r where abs(d - ?) < 100
-- @case params: [1, 1]
select abs(d - ?) from r where abs(d - ?) < 100
-- @case params: [1, 1]
select 1 from r where d between ? - 1 and ? + 1
-- @case params: [1, 1]
select 1 from r where d in (? - 1, ? + 1)
-- @case params: [1]
select 1 from r where coalesce(d - ?, 0) > 0
-- @case params: [1]
select 1 from r where iif(1=1, d - ?, 0) > 0
-- @case params: [1]
select d from r order by abs(d - ?)
-- @case params: [1, 1]
select abs(d - ?) from r group by abs(d - ?)
-- @case params: [1]
select abs(d - ?) from r group by 1
-- @case params: [1]
select (select abs(d - ?) from rdb$database) from r
-- @case params: [1]
select max(abs(d - ?)) from r
-- @case params: [1]
select sum(abs(i - ?)) from r
-- @case params: [1, 1]
select abs(d - ?) from r union all select abs(i - ?) from r
-- @case params: [1]
select cast(? as integer) from r
-- @case params: [1]
select sum(?) from r
-- @case params: [1]
select avg(?) from r
-- @case params: [1]
select min(?) from r
-- @case params: [1]
select max(?) from r
-- @case params: [1]
select count(?) from r
-- @case params: [1]
select list(?) from r
-- @case params: [1]
select abs(?) from r
-- @case params: [1]
select abs(-?) from r
-- @case params: [1]
select abs(+?) from r
-- @case params: [1, 1]
select abs(? - ?) from r
-- @case params: [1]
select coalesce(? - 1, 0) from r
-- @case params: [1]
with u as (select p from g where id = ?) select coalesce(u.p, 99) from r left join u on 1=1
-- @case params: [1, 1]
with u as (select p from g where id = ?) select u.p from r left join u on 1=1 where r.id = ?
-- @case params: [2, 15, 15, 1, 5]
with u as (select p from g where id = ?),
ga as (
  select iif(u.p = g.id, -1, coalesce(g.p, 99)) o, g.id, g.dmax, r.d,
    abs(g.dmax - ?) df,
    case when ? <= r.d then 'F' when u.p = g.id then 'F' else 'T' end ra
  from r join g on g.id >= r.id left join u on 1=1
  where r.id = ? and g.dmax >= ?)
select id, dmax, d, o, df, ra from ga order by df, o rows 1
-- @case params: []
select 1 from r union all select 1 from r
-- @case params: []
select -1 from r union all select -1 from r
-- @case params: []
select 'a' from r union all select 'a' from r
-- @case params: []
select null from r union all select null from r
-- @case params: []
select 1+1 from r union all select 1+1 from r
-- @case params: []
select 1.5 from r union all select 1.5 from r
-- @case params: []
select -1.5 from r union all select -1.5 from r
-- @case params: []
select 1e0 from r union all select 1e0 from r
-- @case params: []
select count(*) from r union all select count(*) from r
-- @case params: []
select sum(i) from r union all select sum(i) from r
-- @case params: []
select max(i) from r union all select max(i) from r
-- @case params: []
select max(i) + 1 from r union all select max(i) + 1 from r
-- @case params: []
select i from r union all select i from r
-- @case params: []
select i as x from r union all select i as x from r
-- @case params: []
select abs(i) as x from r union all select abs(i) as x from r
-- @case params: []
select abs(i) from r union all select abs(i) from r
-- @case params: []
select abs(d) from r union all select abs(d) from r
-- @case params: []
select f + 1 from r union all select f + 1 from r
-- @case params: []
select upper(v) from r union all select upper(v) from r
-- @case params: []
select current_date from r union all select current_date from r
-- @case params: []
select (select max(i) from r) from r union all select (select max(i) from r) from r
-- @case params: []
select cast(i as bigint) from r union all select cast(i as bigint) from r
-- @case params: []
select cast(i as integer) from r union all select cast(i as integer) from r
-- @case params: []
select coalesce(i, 0) from r union all select coalesce(i, 0) from r
-- @case params: []
select -i from r union all select -i from r
-- @case params: []
select true from r union all select true from r
-- @case params: []
select x'41' from r union all select x'41' from r
-- @case params: []
select date '2024-01-01' from r union all select date '2024-01-01' from r
-- @case params: []
select current_user from r union all select current_user from r
-- @case params: []
select i || '' from r union all select i || '' from r
-- @case params: [2, 2]
select abs(d - ?) from r union all select abs(d - ?) from r
-- @case params: [2, 2]
select abs(?) from r union all select abs(?) from r
-- @case params: [2, 2]
select cast(? as double precision) from r union all select cast(? as double precision) from r
-- @case params: []
select abs(d) from r union all select abs(i) from r
-- @case params: []
select abs(i) from r union all select abs(b) from r
-- @case params: []
select abs(f) from r union all select abs(i) from r
-- @case params: []
select f from r union all select i from r
-- @case params: []
select abs(f) from r union all select f from r
-- @case params: []
select upper(v) from r union all select v from r
-- @case params: [2]
select abs(d - ?) from r union all select abs(i) from r
-- @case params: [2]
select abs(d) from r union all select abs(i - ?) from r
-- @case params: []
select i from r union all select d from r
-- @case params: []
select i as x from r union all select d from r
-- @case params: [2]
select d - cast(? as double precision) from r union all select i from r
-- @case params: []
select d + 1e0 from r union all select i from r
-- @case params: []
select 1 from r union all select d from r
-- @case params: []
select count(*) from r union all select d from r
-- @case params: []
select y from (select i + 1 as y, i as z from r) union all select 1 from r
-- @case params: []
select z from (select i + 1 as y, i as z from r) union all select 1 from r
-- @case params: []
select dt.c from (select i + 1 from r) dt (c) union all select 1 from r
-- @case params: []
select abs(i) from r union select abs(i) from r union select abs(i) from r
-- @case params: []
select i from r union all select i from r union all select abs(i) from r
-- @case params: []
select x from (select abs(i) from r union all select abs(i) from r) t (x)
-- @case params: []
select * from (select abs(i), i from r union all select abs(i), i from r)
-- @case params: []
select * from (select abs(i), i from r union all select abs(i), i from r) t
-- @case params: []
select * from (select abs(i) a, i from r union all select abs(i), i from r)
-- @case params: []
select abs(i) from r group by abs(i)
-- @case params: []
select i + 1 from r group by i
-- @case params: []
select overlay('abcdef' placing 'x' from 0) from r
-- @case params: []
select overlay('abcdef' placing 'x' from -1) from r
-- @case params: []
select overlay('abcdef' placing 'x' from 1) from r
-- @case params: []
select overlay('abcdef' placing 'x' from 6) from r
-- @case params: []
select overlay('abcdef' placing 'x' from 7) from r
-- @case params: []
select overlay('abcdef' placing 'x' from 9) from r
-- @case params: []
select overlay('abcdef' placing 'x' from 1 for -1) from r
-- @case params: []
select overlay('abcdef' placing 'x' from 2 for 0) from r
-- @case params: []
select overlay('abcdef' placing 'x' from 2 for 9) from r
-- @case params: []
select overlay('abcdef' placing 'x' from 0 for -1) from r
-- @case params: []
select overlay('abcdef' placing 'x' from null for -1) from r
-- @case params: []
select overlay('abcdef' placing 'x' from 0 for null) from r
-- @case params: [2]
update r set d = d - ? where id = 1 returning d
-- @case params: [3]
update r set i = abs(d - ?) where id = 1 returning i
-- @case params: [1.5]
update r set n = iif(1=1, n + ?, 0) where id = 1 returning n
-- @case params: [2, 1]
insert into r (id, d) values (? + 1, coalesce(? - 1, 0)) returning id, d
-- @case params: [2]
execute block (x numeric(15,2) = ?) returns (y double precision) as begin y = abs(x - 0.5); suspend; end
-- @rollback
