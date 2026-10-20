-- Aggregate functions over every type (result types, NULLs, DISTINCT, empty input, overflow), GROUP BY by column,
-- expression, position and alias, HAVING, grouping of NULLs/collations/padding, DISTINCT, and the related errors.
-- @setup
create table a (id integer not null primary key, g integer, h varchar(5), i integer, sm smallint, bi bigint,
    n numeric(9,2), n4 numeric(18,4), d double precision, f float, s varchar(10), c char(5), dt date,
    ts timestamp, t time, b boolean, bl blob sub_type text, ci varchar(10) character set utf8 collate unicode_ci);
insert into a values (1, 1, 'x', 10, 1, 9000000000000000000, 1.25, 1.0001, 1.5, 0.5, 'abc', 'a', '2024-01-01', '2024-01-01 10:00:00', '10:00:00', true, 'one', 'Abc');
insert into a values (2, 1, 'x', 20, 2, 9000000000000000000, 2.50, 2.0002, 2.25, 1.25, 'abc', 'a  ', '2024-02-01', '2024-02-01 11:00:00', '11:00:00', false, 'two', 'abc');
insert into a values (3, 1, 'y', null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
insert into a values (4, 2, 'y', 7, -3, -5, -0.01, 3.3333, -1e300, -2.5, 'b', 'bb', '2023-12-31', '2023-12-31 23:59:59.9999', '23:59:59', true, 'three', 'ABC');
insert into a values (5, 2, null, 7, -3, -5, 0.01, 3.3333, 1e300, 2.5, 'B', 'b', '2023-12-31', '2023-12-31 00:00:00', '00:00:00', null, 'four', 'x');
insert into a values (6, null, null, 1, 32767, 1, 99999.99, 0.0001, 1e-300, 3.4e38, 'z', 'z', '2100-01-01', '2100-01-01 00:00:00', '12:00:00', false, null, 'X');
insert into a values (7, null, 'x', 2147483647, 32767, 9223372036854775807, 9999999.99, 99999999999999.9999, 0, 0, '', '', '0001-01-01', '0001-01-01 00:00:00', '00:00:00.0001', true, '', '');
insert into a values (8, 3, 'z', 2147483647, 1, 1, 0.33, 0.3333, 0.1, 0.1, 'abc ', 'abc', '2024-01-01', '2024-01-01 10:00:00', '10:00:00', false, 'eight', 'abc ');
create table z (x integer, s varchar(5), n numeric(5,2));
create table big (x bigint, y integer, n numeric(18,2));
insert into big values (9223372036854775807, 2147483647, 9999999999999999.99);
insert into big values (9223372036854775807, 2147483647, 9999999999999999.99);
insert into big values (-1, -2147483647, -0.01);
-- @case
select count(i), count(distinct i), min(i), max(i) from a
-- @case
select sum(i), avg(i) from a
-- @case
select sum(distinct i), avg(distinct i) from a
-- @case
select g, count(i), min(i), max(i) from a group by g order by g
-- @case
select g, sum(i), avg(i) from a where id <> 7 group by g order by g
-- @case
select count(*), count(i), sum(i), avg(i), min(i), max(i) from a where 1 = 0
-- @case
select i, count(*) from a group by i order by 1
-- @case
select distinct i from a order by 1
-- @case
select count(sm), count(distinct sm), min(sm), max(sm) from a
-- @case
select sum(sm), avg(sm) from a
-- @case
select sum(distinct sm), avg(distinct sm) from a
-- @case
select g, count(sm), min(sm), max(sm) from a group by g order by g
-- @case
select g, sum(sm), avg(sm) from a where id <> 7 group by g order by g
-- @case
select count(*), count(sm), sum(sm), avg(sm), min(sm), max(sm) from a where 1 = 0
-- @case
select sm, count(*) from a group by sm order by 1
-- @case
select distinct sm from a order by 1
-- @case
select count(bi), count(distinct bi), min(bi), max(bi) from a
-- @case
select sum(bi), avg(bi) from a
-- @case
select sum(distinct bi), avg(distinct bi) from a
-- @case
select g, count(bi), min(bi), max(bi) from a group by g order by g
-- @case
select g, sum(bi), avg(bi) from a where id <> 7 group by g order by g
-- @case
select count(*), count(bi), sum(bi), avg(bi), min(bi), max(bi) from a where 1 = 0
-- @case
select bi, count(*) from a group by bi order by 1
-- @case
select distinct bi from a order by 1
-- @case
select count(n), count(distinct n), min(n), max(n) from a
-- @case
select sum(n), avg(n) from a
-- @case
select sum(distinct n), avg(distinct n) from a
-- @case
select g, count(n), min(n), max(n) from a group by g order by g
-- @case
select g, sum(n), avg(n) from a where id <> 7 group by g order by g
-- @case
select count(*), count(n), sum(n), avg(n), min(n), max(n) from a where 1 = 0
-- @case
select n, count(*) from a group by n order by 1
-- @case
select distinct n from a order by 1
-- @case
select count(n4), count(distinct n4), min(n4), max(n4) from a
-- @case
select sum(n4), avg(n4) from a
-- @case
select sum(distinct n4), avg(distinct n4) from a
-- @case
select g, count(n4), min(n4), max(n4) from a group by g order by g
-- @case
select g, sum(n4), avg(n4) from a where id <> 7 group by g order by g
-- @case
select count(*), count(n4), sum(n4), avg(n4), min(n4), max(n4) from a where 1 = 0
-- @case
select n4, count(*) from a group by n4 order by 1
-- @case
select distinct n4 from a order by 1
-- @case
select count(d), count(distinct d), min(d), max(d) from a
-- @case
select sum(d), avg(d) from a
-- @case
select sum(distinct d), avg(distinct d) from a
-- @case
select g, count(d), min(d), max(d) from a group by g order by g
-- @case
select g, sum(d), avg(d) from a where id <> 7 group by g order by g
-- @case
select count(*), count(d), sum(d), avg(d), min(d), max(d) from a where 1 = 0
-- @case
select d, count(*) from a group by d order by 1
-- @case
select distinct d from a order by 1
-- @case
select count(f), count(distinct f), min(f), max(f) from a
-- @case
select sum(f), avg(f) from a
-- @case
select sum(distinct f), avg(distinct f) from a
-- @case
select g, count(f), min(f), max(f) from a group by g order by g
-- @case
select g, sum(f), avg(f) from a where id <> 7 group by g order by g
-- @case
select count(*), count(f), sum(f), avg(f), min(f), max(f) from a where 1 = 0
-- @case
select f, count(*) from a group by f order by 1
-- @case
select distinct f from a order by 1
-- @case
select count(s), count(distinct s), min(s), max(s) from a
-- @case
select sum(s), avg(s) from a
-- @case
select sum(distinct s), avg(distinct s) from a
-- @case
select g, count(s), min(s), max(s) from a group by g order by g
-- @case
select g, sum(s), avg(s) from a where id <> 7 group by g order by g
-- @case
select count(*), count(s), sum(s), avg(s), min(s), max(s) from a where 1 = 0
-- @case
select s, count(*) from a group by s order by 1
-- @case
select distinct s from a order by 1
-- @case
select count(c), count(distinct c), min(c), max(c) from a
-- @case
select sum(c), avg(c) from a
-- @case
select sum(distinct c), avg(distinct c) from a
-- @case
select g, count(c), min(c), max(c) from a group by g order by g
-- @case
select g, sum(c), avg(c) from a where id <> 7 group by g order by g
-- @case
select count(*), count(c), sum(c), avg(c), min(c), max(c) from a where 1 = 0
-- @case
select c, count(*) from a group by c order by 1
-- @case
select distinct c from a order by 1
-- @case
select count(dt), count(distinct dt), min(dt), max(dt) from a
-- @case
select sum(dt), avg(dt) from a
-- @case
select sum(distinct dt), avg(distinct dt) from a
-- @case
select g, count(dt), min(dt), max(dt) from a group by g order by g
-- @case
select g, sum(dt), avg(dt) from a where id <> 7 group by g order by g
-- @case
select count(*), count(dt), sum(dt), avg(dt), min(dt), max(dt) from a where 1 = 0
-- @case
select dt, count(*) from a group by dt order by 1
-- @case
select distinct dt from a order by 1
-- @case
select count(ts), count(distinct ts), min(ts), max(ts) from a
-- @case
select sum(ts), avg(ts) from a
-- @case
select sum(distinct ts), avg(distinct ts) from a
-- @case
select g, count(ts), min(ts), max(ts) from a group by g order by g
-- @case
select g, sum(ts), avg(ts) from a where id <> 7 group by g order by g
-- @case
select count(*), count(ts), sum(ts), avg(ts), min(ts), max(ts) from a where 1 = 0
-- @case
select ts, count(*) from a group by ts order by 1
-- @case
select distinct ts from a order by 1
-- @case
select count(t), count(distinct t), min(t), max(t) from a
-- @case
select sum(t), avg(t) from a
-- @case
select sum(distinct t), avg(distinct t) from a
-- @case
select g, count(t), min(t), max(t) from a group by g order by g
-- @case
select g, sum(t), avg(t) from a where id <> 7 group by g order by g
-- @case
select count(*), count(t), sum(t), avg(t), min(t), max(t) from a where 1 = 0
-- @case
select t, count(*) from a group by t order by 1
-- @case
select distinct t from a order by 1
-- @case
select count(b), count(distinct b), min(b), max(b) from a
-- @case
select sum(b), avg(b) from a
-- @case
select sum(distinct b), avg(distinct b) from a
-- @case
select g, count(b), min(b), max(b) from a group by g order by g
-- @case
select g, sum(b), avg(b) from a where id <> 7 group by g order by g
-- @case
select count(*), count(b), sum(b), avg(b), min(b), max(b) from a where 1 = 0
-- @case
select b, count(*) from a group by b order by 1
-- @case
select distinct b from a order by 1
-- @case
select count(bl), count(distinct bl), min(bl), max(bl) from a
-- @case
select sum(bl), avg(bl) from a
-- @case
select sum(distinct bl), avg(distinct bl) from a
-- @case
select g, count(bl), min(bl), max(bl) from a group by g order by g
-- @case
select g, sum(bl), avg(bl) from a where id <> 7 group by g order by g
-- @case
select count(*), count(bl), sum(bl), avg(bl), min(bl), max(bl) from a where 1 = 0
-- @case
select bl, count(*) from a group by bl order by 1
-- @case
select distinct bl from a order by 1
-- @case
select count(ci), count(distinct ci), min(ci), max(ci) from a
-- @case
select sum(ci), avg(ci) from a
-- @case
select sum(distinct ci), avg(distinct ci) from a
-- @case
select g, count(ci), min(ci), max(ci) from a group by g order by g
-- @case
select g, sum(ci), avg(ci) from a where id <> 7 group by g order by g
-- @case
select count(*), count(ci), sum(ci), avg(ci), min(ci), max(ci) from a where 1 = 0
-- @case
select ci, count(*) from a group by ci order by 1
-- @case
select distinct ci from a order by 1
-- @case
select count(g), count(distinct g), min(g), max(g) from a
-- @case
select sum(g), avg(g) from a
-- @case
select sum(distinct g), avg(distinct g) from a
-- @case
select g, count(g), min(g), max(g) from a group by g order by g
-- @case
select g, sum(g), avg(g) from a where id <> 7 group by g order by g
-- @case
select count(*), count(g), sum(g), avg(g), min(g), max(g) from a where 1 = 0
-- @case
select g, count(*) from a group by g order by 1
-- @case
select distinct g from a order by 1
-- @case
select count(h), count(distinct h), min(h), max(h) from a
-- @case
select sum(h), avg(h) from a
-- @case
select sum(distinct h), avg(distinct h) from a
-- @case
select g, count(h), min(h), max(h) from a group by g order by g
-- @case
select g, sum(h), avg(h) from a where id <> 7 group by g order by g
-- @case
select count(*), count(h), sum(h), avg(h), min(h), max(h) from a where 1 = 0
-- @case
select h, count(*) from a group by h order by 1
-- @case
select distinct h from a order by 1
-- @case
select sum(x) from big
-- @case
select avg(x) from big
-- @case
select sum(x) from big where x > 0
-- @case
select avg(x) from big where x > 0
-- @case
select sum(y) from big
-- @case
select avg(y) from big
-- @case
select sum(y) from big where x > 0
-- @case
select avg(y) from big where x > 0
-- @case
select sum(n) from big
-- @case
select avg(n) from big
-- @case
select sum(n) from big where x > 0
-- @case
select avg(n) from big where x > 0
-- @case
select sum(x * 2) from big
-- @case
select avg(x * 2) from big
-- @case
select sum(x * 2) from big where x > 0
-- @case
select avg(x * 2) from big where x > 0
-- @case
select sum(y * 2) from big
-- @case
select avg(y * 2) from big
-- @case
select sum(y * 2) from big where x > 0
-- @case
select avg(y * 2) from big where x > 0
-- @case
select sum(y + 0) from big
-- @case
select avg(y + 0) from big
-- @case
select sum(y + 0) from big where x > 0
-- @case
select avg(y + 0) from big where x > 0
-- @case
select sum(n * n) from big
-- @case
select avg(n * n) from big
-- @case
select sum(n * n) from big where x > 0
-- @case
select avg(n * n) from big where x > 0
-- @case
select sum(cast(y as smallint)) from big
-- @case
select avg(cast(y as smallint)) from big
-- @case
select sum(cast(y as smallint)) from big where x > 0
-- @case
select avg(cast(y as smallint)) from big where x > 0
-- @case
select sum(x / 2) from big
-- @case
select avg(x / 2) from big
-- @case
select sum(x / 2) from big where x > 0
-- @case
select avg(x / 2) from big where x > 0
-- @case
select sum(cast(x as double precision)) from big
-- @case
select avg(cast(x as double precision)) from big
-- @case
select sum(cast(x as double precision)) from big where x > 0
-- @case
select avg(cast(x as double precision)) from big where x > 0
-- @case
select sum(i), avg(i), min(i), max(i), count(i) from a where id not in (6, 7)
-- @case
select sum(i + 0), avg(i + 0), min(i + 0), max(i + 0), count(i + 0) from a where id not in (6, 7)
-- @case
select sum(i * 1), avg(i * 1), min(i * 1), max(i * 1), count(i * 1) from a where id not in (6, 7)
-- @case
select sum(i / 2), avg(i / 2), min(i / 2), max(i / 2), count(i / 2) from a where id not in (6, 7)
-- @case
select sum(n * 2), avg(n * 2), min(n * 2), max(n * 2), count(n * 2) from a where id not in (6, 7)
-- @case
select sum(n / 3), avg(n / 3), min(n / 3), max(n / 3), count(n / 3) from a where id not in (6, 7)
-- @case
select sum(i * n), avg(i * n), min(i * n), max(i * n), count(i * n) from a where id not in (6, 7)
-- @case
select sum(sm + sm), avg(sm + sm), min(sm + sm), max(sm + sm), count(sm + sm) from a where id not in (6, 7)
-- @case
select sum(bi / 1), avg(bi / 1), min(bi / 1), max(bi / 1), count(bi / 1) from a where id not in (6, 7)
-- @case
select sum(cast(i as numeric(18,3))), avg(cast(i as numeric(18,3))), min(cast(i as numeric(18,3))), max(cast(i as numeric(18,3))), count(cast(i as numeric(18,3))) from a where id not in (6, 7)
-- @case
select sum(cast(i as smallint)), avg(cast(i as smallint)), min(cast(i as smallint)), max(cast(i as smallint)), count(cast(i as smallint)) from a where id not in (6, 7)
-- @case
select sum(d * 2), avg(d * 2), min(d * 2), max(d * 2), count(d * 2) from a where id not in (6, 7)
-- @case
select sum(i * 1.5), avg(i * 1.5), min(i * 1.5), max(i * 1.5), count(i * 1.5) from a where id not in (6, 7)
-- @case
select sum(i / 1e0), avg(i / 1e0), min(i / 1e0), max(i / 1e0), count(i / 1e0) from a where id not in (6, 7)
-- @case
select count(1), min(1), max(1) from a
-- @case
select sum(1), avg(1) from a
-- @case
select count(1.5), min(1.5), max(1.5) from a
-- @case
select sum(1.5), avg(1.5) from a
-- @case
select count(1e0), min(1e0), max(1e0) from a
-- @case
select sum(1e0), avg(1e0) from a
-- @case
select count(null), min(null), max(null) from a
-- @case
select sum(null), avg(null) from a
-- @case
select count('a'), min('a'), max('a') from a
-- @case
select sum('a'), avg('a') from a
-- @case
select count(true), min(true), max(true) from a
-- @case
select sum(true), avg(true) from a
-- @case
select count(date '2024-01-01'), min(date '2024-01-01'), max(date '2024-01-01') from a
-- @case
select sum(date '2024-01-01'), avg(date '2024-01-01') from a
-- @case
select count(cast(null as integer)), min(cast(null as integer)), max(cast(null as integer)) from a
-- @case
select sum(cast(null as integer)), avg(cast(null as integer)) from a
-- @case
select count(*), sum(x), avg(x), min(x), max(x), list(s), count(distinct x) from z
-- @case
select count(*) from z group by x
-- @case
select x, count(*) from z group by x
-- @case
select count(*) from z having count(*) = 0
-- @case
select count(*) from z having count(*) > 0
-- @case
select sum(n), avg(n), max(s) from z where x = 1
-- @case
select (select count(*) from z), (select sum(x) from z), (select max(s) from z) from rdb$database
-- @case
select coalesce(sum(x), 0), coalesce(max(s), '-') from z
-- @case
select count(*) + 1, sum(x) + 1, max(x) * 2 from z
-- @case
select ci, count(*) from a group by ci
-- @case
select distinct ci from a
-- @case
select g, count(*) from a group by g
-- @case
select h, g, count(*) from a group by h, g
-- @case
select distinct h from a
-- @case
select distinct g, h from a
-- @case
select b, count(*) from a group by b
-- @case
select s, count(*) from a group by s
-- @case
select c, count(*) from a group by c
-- @case
select ci, min(ci), max(ci), list(ci, ';'), list(id) from a group by ci
-- @case
select g, min(ci), max(ci), list(ci, ';') from a group by g
-- @case
select g, list(s, ';'), list(n), list(i) from a group by g
-- @case
select upper(ci), list(ci, ';') from a group by upper(ci)
-- @case
select distinct s, c from a order by 1, 2
-- @case
select upper(ci), min(ci), max(ci), count(distinct ci) from a group by upper(ci) order by 1
-- @case
select distinct upper(ci) from a order by 1
-- @case
select s, count(distinct ci) from a group by s order by 1
-- @case
select g, count(*) from a group by g order by g
-- @case
select g, count(*) from a group by 1 order by 1
-- @case
select g as k, count(*) from a group by k order by k
-- @case
select g + 1, count(*) from a group by g + 1 order by 1
-- @case
select g + 1 as k, count(*) from a group by g + 1 order by k
-- @case
select g * 2, sum(i) from a group by g order by 1
-- @case
select h, g, count(*) from a group by h, g order by h, g
-- @case
select h, g, count(*) from a group by g, h order by 1, 2
-- @case
select upper(h), count(*) from a group by upper(h) order by 1
-- @case
select ci, count(*) from a group by ci order by 1
-- @case
select c, count(*) from a group by c order by 1
-- @case
select s, count(*) from a group by s order by 1
-- @case
select upper(s), count(*) from a group by 1 order by 1
-- @case
select extract(year from dt), count(*) from a group by extract(year from dt) order by 1
-- @case
select cast(ts as date), count(*) from a group by cast(ts as date) order by 1
-- @case
select b, count(*), sum(i) from a group by b order by b
-- @case
select g, count(*) from a group by g having count(*) > 1 order by g
-- @case
select g, count(*) from a group by g having g > 1 order by g
-- @case
select g, count(*) from a group by g having sum(i) > 20 order by g
-- @case
select g from a group by g having max(s) = 'abc' order by g
-- @case
select g, max(i) - min(i) from a group by g having count(distinct h) = 1 order by g
-- @case
select count(*) from a having count(*) > 1
-- @case
select count(*) from a having 1 = 0
-- @case
select 1 from a having count(*) = 8
-- @case
select g, count(*) from a where i > 5 group by g having count(*) >= 1 order by g
-- @case
select g, (select count(*) from a a2 where a2.g = a.g) from a group by g order by g
-- @case
select g, count(*), (select max(i) from a a2 where a2.g = a.g) from a group by g order by g
-- @case
select g, sum(i) * 100 / (select sum(i) from a where id <> 7 and id <> 8) from a where id <> 7 and id <> 8 group by g order by g
-- @case
select x.g, x.cnt from (select g, count(*) cnt from a group by g) x order by x.g
-- @case
select max(cnt), min(cnt) from (select g, count(*) cnt from a group by g)
-- @case
select count(*) from (select distinct g from a)
-- @case
select count(distinct g), count(distinct h), count(distinct g || h) from a
-- @case
select count(distinct c), count(distinct s), count(distinct ci) from a
-- @case
select distinct g, h from a order by g, h
-- @case
select distinct c from a order by c
-- @case
select distinct ci from a order by ci
-- @case
select distinct b from a order by b
-- @case
select distinct g, count(*) from a group by g order by g
-- @case
select g, list(id) from (select g, id from a order by id) group by g order by g
-- @case
select list(s, '-') from (select s from a order by id)
-- @case
select list(distinct h) from a
-- @case
select list(c) from (select c from a where id < 4 order by id)
-- @case
select list(i, ', '), list(n), list(dt), list(b) from (select * from a where id in (1, 2, 4) order by id)
-- @case
select list(bl) from (select bl from a where id < 3 order by id)
-- @case
select list(s, null) from a
-- @case
select list(s, s) from (select s from a where id < 3 order by id)
-- @case
select list(id, ', ') from (select id from a where 1 = 0)
-- @case
select min(id), max(id) from a where g is null
-- @case
select g, min(id), max(id) from a group by g order by g nulls last
-- @case
select g, count(*) from a group by g order by 2 desc, 1
-- @case
select g, count(*) c from a group by g order by c, g
-- @case
select g, count(*) from a group by g order by count(*), g
-- @case
select g, count(*) from a group by g order by sum(i) desc nulls last, g
-- @case
select sum(i) from a group by g order by g
-- @case
select avg(cast(i as double precision)) from a where id < 6
-- @case
select avg(n4), sum(n4) from a
-- @case
select avg(sm), sum(sm) from a
-- @case
select sum(f), avg(f), sum(d), avg(d) from a where id <> 4 and id <> 5
-- @case
select sum(d) from a
-- @case
select max(bl) from a
-- @case
select min(bl), max(bl) from a where id < 3
-- @case
select sum(b) from a
-- @case
select sum(s) from a
-- @case
select avg(dt) from a
-- @case
select sum(dt), sum(t), sum(ts) from a
-- @case
select count(*), count(1), count(id), count(g) from a
-- @case
select count(all g), count(distinct g), sum(all g), sum(distinct g) from a
-- @case
select count(*) from a a1 join a a2 on a1.g = a2.g
-- @case
select a1.g, count(*) from a a1 left join a a2 on a1.g = a2.g and a2.id > a1.id group by a1.g order by 1
-- @case
select a1.g, count(a2.id) from a a1 left join a a2 on a1.g = a2.g and a2.id > a1.id group by a1.g order by 1
-- @case
select g, i from a group by g
-- @case
select g, count(*) from a group by 3
-- @case
select g, count(*) from a group by 0
-- @case
select g, count(*) from a group by count(*)
-- @case
select g, count(*) from a group by 2
-- @case
select i from a where count(*) > 1
-- @case
select sum(count(*)) from a
-- @case
select sum(max(i)) from a group by g
-- @case
select count(*) from a having i > 1
-- @case
select g from a group by g having i > 1
-- @case
select g, count(*) from a group by g order by i
-- @case
select i, count(*) from a
-- @case
select *, count(*) from a
-- @case
select count(*) from a group by k
-- @case
select g k, count(*) from a group by k + 1
-- @case
select sum(*) from a
-- @case
select count(distinct *) from a
-- @case
select sum(i, n) from a
-- @case
select min() from a
-- @case
select list(i, ',', ',') from a
-- @case
select count() from a
-- @case
select count(*), * from a
-- @case
select a.*, count(*) from a
-- @case
select g from a order by 2
-- @case
select g, count(*) from a group by g order by 3
-- @case
select g, count(*) from a group by g having count(*) > 1 and h = 'x'
-- @case
select g, count(*) from a group by g having g + i > 0
-- @case
select g, count(*) from a group by g order by h
-- @case
select count(*) from a group by g order by count(*) desc, max(i)
-- @case
select avg(distinct i), sum(distinct i * 2), count(distinct i + 0) from a
-- @case
select max(i) over () from a order by id
-- @case
select g, sum(i) over (partition by g) from a order by id
-- @case
select g, sum(sum(i)) over () from a group by g order by g
-- @case
select i from a where i = (select max(i) from a) order by id
-- @case
select i from a where i > (select avg(i) from a where id < 6) order by id
-- @case
select g, count(*) from a where g = (select min(g) from a) group by g
-- @case
select (select sum(i) from a a2 where a2.g = a1.g) from a a1 order by id
-- @case
select count(*) from a where exists (select 1 from a a2 group by a2.g having count(*) > 2)
-- @case
select g, count(*) from a group by g having count(*) = (select max(c) from (select count(*) c from a group by g)) order by g
-- @case
select sum(1), sum(1.5), sum(1e0), avg(1), avg(1.5), avg(2) from a
-- @case
select sum(-i), sum(i) * -1 from a where id < 6
-- @case
select max(i) + min(i), max(s) || min(s), max(dt) - min(dt), max(ts) - min(ts) from a
-- @case
select count(*) * 1.0 / 3, sum(i) / count(i), cast(sum(i) as double precision) / count(i) from a where id < 6
-- @case
select max(case when g = 1 then i end), sum(case when g = 2 then 1 else 0 end), count(case when b then 1 end) from a
-- @case
select sum(iif(b, 1, 0)), sum(coalesce(i, 0)), max(coalesce(s, '?')) from a
-- @case
select first 2 g, count(*) from a group by g order by g
-- @case
select g, count(*) from a group by g order by g rows 2 to 3
-- @case
select g, count(*) from a group by g order by g offset 1 rows fetch next 1 rows only
