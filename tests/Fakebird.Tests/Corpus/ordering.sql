-- ORDER BY over every type (NULL placement, collations, padding, ties between equal keys), positions, aliases and
-- expressions, FIRST/SKIP, ROWS, OFFSET/FETCH with literals, expressions and parameters, and the related errors.
-- @setup
create table o (id integer not null primary key, g integer, s varchar(10), ci varchar(10) collate unicode_ci,
    c char(5), d double precision, n numeric(9,2), dt date, ts timestamp, b boolean, bl blob sub_type text,
    bi bigint, sm smallint, t time, ai varchar(10) collate unicode_ci_ai, u varchar(10) collate unicode);
insert into o values (5, 2, 'b', 'B', 'b', 2.5, -1.00, '2024-03-01', '2024-03-01 10:00:00', true, 'e', -5, 3, '10:00:00', 'é', 'b');
insert into o values (3, 1, 'a', 'a', 'a', -0.5, 2.00, '2024-01-01', '2024-01-01 00:00:00', false, 'c', 3, -3, '00:00:00', 'e', 'B');
insert into o values (-1, 2, 'B', 'b', 'B', 1e10, 0.01, '1999-12-31', '1999-12-31 23:59:59', null, 'a', 9000000000, 0, '23:59:59', 'E', 'a');
insert into o values (8, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
insert into o values (1, 1, 'a ', 'A', 'a ', 0, -0.01, '2024-01-01', '2024-01-01 00:00:00.0001', true, 'b', -1, 1, '00:00:00.0001', 'ê', 'A');
insert into o values (2, 3, '', '', '', -1e10, 99.99, '0001-01-01', '0001-01-01 00:00:00', false, '', 0, -1, '12:00:00', '', '');
insert into o values (-7, 2, 'ab', 'Ab', 'ab', 2.5, -1.00, '2024-03-01', '2024-03-01 10:00:00', true, 'd', -5, 3, '10:00:00', 'ë', 'ab');
insert into o values (4, null, 'z', 'Z', 'z', null, null, '2100-01-01', null, null, 'f', null, null, null, 'Ê', 'Z');
insert into o values (6, 1, 'A', 'a ', 'A', -0.0, 2.00, '2024-01-01', '2024-01-01 00:00:00', false, 'g', 3, -3, '00:00:00', 'e ', 'b ');
-- @case
select id, g from o order by g
-- @case
select id, g from o order by g desc
-- @case
select id, g from o order by g nulls first
-- @case
select id, g from o order by g nulls last
-- @case
select id, g from o order by g desc nulls first
-- @case
select id, g from o order by g desc nulls last
-- @case
select id, g from o order by g asc nulls last
-- @case
select g, id from o order by g, id desc
-- @case
select g from o order by g desc, id
-- @case
select id, s from o order by s
-- @case
select id, s from o order by s desc
-- @case
select id, s from o order by s nulls first
-- @case
select id, s from o order by s nulls last
-- @case
select id, s from o order by s desc nulls first
-- @case
select id, s from o order by s desc nulls last
-- @case
select id, s from o order by s asc nulls last
-- @case
select s, id from o order by s, id desc
-- @case
select s from o order by s desc, id
-- @case
select id, ci from o order by ci
-- @case
select id, ci from o order by ci desc
-- @case
select id, ci from o order by ci nulls first
-- @case
select id, ci from o order by ci nulls last
-- @case
select id, ci from o order by ci desc nulls first
-- @case
select id, ci from o order by ci desc nulls last
-- @case
select id, ci from o order by ci asc nulls last
-- @case
select ci, id from o order by ci, id desc
-- @case
select ci from o order by ci desc, id
-- @case
select id, c from o order by c
-- @case
select id, c from o order by c desc
-- @case
select id, c from o order by c nulls first
-- @case
select id, c from o order by c nulls last
-- @case
select id, c from o order by c desc nulls first
-- @case
select id, c from o order by c desc nulls last
-- @case
select id, c from o order by c asc nulls last
-- @case
select c, id from o order by c, id desc
-- @case
select c from o order by c desc, id
-- @case
select id, d from o order by d
-- @case
select id, d from o order by d desc
-- @case
select id, d from o order by d nulls first
-- @case
select id, d from o order by d nulls last
-- @case
select id, d from o order by d desc nulls first
-- @case
select id, d from o order by d desc nulls last
-- @case
select id, d from o order by d asc nulls last
-- @case
select d, id from o order by d, id desc
-- @case
select d from o order by d desc, id
-- @case
select id, n from o order by n
-- @case
select id, n from o order by n desc
-- @case
select id, n from o order by n nulls first
-- @case
select id, n from o order by n nulls last
-- @case
select id, n from o order by n desc nulls first
-- @case
select id, n from o order by n desc nulls last
-- @case
select id, n from o order by n asc nulls last
-- @case
select n, id from o order by n, id desc
-- @case
select n from o order by n desc, id
-- @case
select id, dt from o order by dt
-- @case
select id, dt from o order by dt desc
-- @case
select id, dt from o order by dt nulls first
-- @case
select id, dt from o order by dt nulls last
-- @case
select id, dt from o order by dt desc nulls first
-- @case
select id, dt from o order by dt desc nulls last
-- @case
select id, dt from o order by dt asc nulls last
-- @case
select dt, id from o order by dt, id desc
-- @case
select dt from o order by dt desc, id
-- @case
select id, ts from o order by ts
-- @case
select id, ts from o order by ts desc
-- @case
select id, ts from o order by ts nulls first
-- @case
select id, ts from o order by ts nulls last
-- @case
select id, ts from o order by ts desc nulls first
-- @case
select id, ts from o order by ts desc nulls last
-- @case
select id, ts from o order by ts asc nulls last
-- @case
select ts, id from o order by ts, id desc
-- @case
select ts from o order by ts desc, id
-- @case
select id, b from o order by b
-- @case
select id, b from o order by b desc
-- @case
select id, b from o order by b nulls first
-- @case
select id, b from o order by b nulls last
-- @case
select id, b from o order by b desc nulls first
-- @case
select id, b from o order by b desc nulls last
-- @case
select id, b from o order by b asc nulls last
-- @case
select b, id from o order by b, id desc
-- @case
select b from o order by b desc, id
-- @case
select id, bl from o order by bl
-- @case
select id, bl from o order by bl desc
-- @case
select id, bl from o order by bl nulls first
-- @case
select id, bl from o order by bl nulls last
-- @case
select id, bl from o order by bl desc nulls first
-- @case
select id, bl from o order by bl desc nulls last
-- @case
select id, bl from o order by bl asc nulls last
-- @case
select bl, id from o order by bl, id desc
-- @case
select bl from o order by bl desc, id
-- @case
select id, bi from o order by bi
-- @case
select id, bi from o order by bi desc
-- @case
select id, bi from o order by bi nulls first
-- @case
select id, bi from o order by bi nulls last
-- @case
select id, bi from o order by bi desc nulls first
-- @case
select id, bi from o order by bi desc nulls last
-- @case
select id, bi from o order by bi asc nulls last
-- @case
select bi, id from o order by bi, id desc
-- @case
select bi from o order by bi desc, id
-- @case
select id, sm from o order by sm
-- @case
select id, sm from o order by sm desc
-- @case
select id, sm from o order by sm nulls first
-- @case
select id, sm from o order by sm nulls last
-- @case
select id, sm from o order by sm desc nulls first
-- @case
select id, sm from o order by sm desc nulls last
-- @case
select id, sm from o order by sm asc nulls last
-- @case
select sm, id from o order by sm, id desc
-- @case
select sm from o order by sm desc, id
-- @case
select id, t from o order by t
-- @case
select id, t from o order by t desc
-- @case
select id, t from o order by t nulls first
-- @case
select id, t from o order by t nulls last
-- @case
select id, t from o order by t desc nulls first
-- @case
select id, t from o order by t desc nulls last
-- @case
select id, t from o order by t asc nulls last
-- @case
select t, id from o order by t, id desc
-- @case
select t from o order by t desc, id
-- @case
select id, ai from o order by ai
-- @case
select id, ai from o order by ai desc
-- @case
select id, ai from o order by ai nulls first
-- @case
select id, ai from o order by ai nulls last
-- @case
select id, ai from o order by ai desc nulls first
-- @case
select id, ai from o order by ai desc nulls last
-- @case
select id, ai from o order by ai asc nulls last
-- @case
select ai, id from o order by ai, id desc
-- @case
select ai from o order by ai desc, id
-- @case
select id, u from o order by u
-- @case
select id, u from o order by u desc
-- @case
select id, u from o order by u nulls first
-- @case
select id, u from o order by u nulls last
-- @case
select id, u from o order by u desc nulls first
-- @case
select id, u from o order by u desc nulls last
-- @case
select id, u from o order by u asc nulls last
-- @case
select u, id from o order by u, id desc
-- @case
select u from o order by u desc, id
-- @case
select id from o order by g
-- @case
select id, g from o order by g
-- @case
select id, g, s from o order by g
-- @case
select g from o order by g
-- @case
select s, id from o order by g desc
-- @case
select id from o order by b
-- @case
select id from o order by dt
-- @case
select id, n from o order by n
-- @case
select id, d from o order by d
-- @case
select id, ci from o order by ci
-- @case
select ci from o order by ci
-- @case
select id from o order by ci desc
-- @case
select id, ai from o order by ai
-- @case
select id, u from o order by u
-- @case
select id, c from o order by c
-- @case
select id, s from o order by upper(s)
-- @case
select id, s from o order by s
-- @case
select id from o order by g, b
-- @case
select id, sm, bi from o order by sm
-- @case
select id, t from o order by t
-- @case
select * from o order by g
-- @case
select g, s from o order by g, s
-- @case
select id from o order by 1 + 0 - 1 * 0
-- @case
select id from o order by 'x'
-- @case
select id from o order by null
-- @case
select id from o order by ? 
-- @case
select id, g from o where g = 2 order by id - id
-- @case
select id, g from o order by 2, 1
-- @case
select id, g from o order by 2 desc, 1 desc
-- @case
select id x, g y from o order by y, x
-- @case
select id as g, g as id from o order by g
-- @case
select id as g, g as id from o order by o.g, o.id
-- @case
select id, g + 1 k from o order by k nulls last, id
-- @case
select id, g from o order by g + 0 nulls last, id
-- @case
select id, g from o order by -g, id
-- @case
select id, s from o order by s || 'x', id
-- @case
select id, s from o order by char_length(s) nulls first, id
-- @case
select id, s from o order by coalesce(s, '~'), id
-- @case
select id from o order by case when g is null then 1 else 0 end, g desc, id
-- @case
select id from o order by iif(b, 0, 1), id
-- @case
select id, s from o order by s collate unicode_ci, id
-- @case
select id, s from o order by s collate unicode_ci_ai, id
-- @case
select id, ai from o order by ai collate unicode, id
-- @case
select id, ci from o order by cast(ci as varchar(10) character set utf8), id
-- @case
select id from o order by (select max(id) from o o2 where o2.g = o.g), id
-- @case
select id, g from o order by (g), (id)
-- @case
select id from o order by id, id, id
-- @case
select g, count(*) from o group by g order by 2 desc, 1
-- @case
select g, count(*) c from o group by g order by c, g nulls last
-- @case
select distinct g from o order by g desc
-- @case
select distinct g from o order by 1 nulls last
-- @case
select g from o union select sm from o order by 1
-- @case
select g from o union all select sm from o order by 1 desc nulls last
-- @case
select id, g from o union select g, id from o order by 2, 1
-- @case
select s from o union select ci from o order by 1
-- @case
select id from (select id, g from o order by g desc) order by id
-- @case
select first 3 id from (select id from o order by id desc) order by id
-- @case
select * from (select first 2 id from o order by id) x
-- @case
select id from o where id in (select first 2 id from o order by id desc) order by id
-- @case
select id from o order by g desc, s desc nulls last, id
-- @case
select id from o order by rdb$db_key
-- @case
select id, row_number() over (order by rdb$db_key) from o order by rdb$db_key desc
-- @case
select id from o order by g nulls last, rdb$db_key desc
-- @case
select id from o order by 0
-- @case
select id from o order by 2
-- @case
select id from o order by -1
-- @case
select id from o order by 1.5
-- @case
select id, g from o order by x
-- @case
select id x, g x from o order by x
-- @case
select id from o order by count(*)
-- @case
select id from o order by sum(id)
-- @case
select id from o order by bl
-- @case
select g, count(*) from o group by g order by id
-- @case
select id from o union select g from o order by id
-- @case
select id from o union select g from o order by 2
-- @case
select id from o union select g from o order by o.id
-- @case
select id from o order by 1 collate unicode
-- @case
select id from o order by s collate win1252
-- @case
select first 2 id from o order by id
-- @case
select first 0 id from o order by id
-- @case
select first 1 skip 1 id from o order by id
-- @case
select skip 2 id from o order by id
-- @case
select skip 0 id from o order by id
-- @case
select first 100 id from o order by id
-- @case
select skip 100 id from o order by id
-- @case
select first (1 + 1) id from o order by id
-- @case
select first (1 + 1) skip (2 * 1) id from o order by id
-- @case
select first 2.5 id from o order by id
-- @case
select first '2' id from o order by id
-- @case
select first null id from o order by id
-- @case
select skip null id from o order by id
-- @case
select first -1 id from o order by id
-- @case
select skip -1 id from o order by id
-- @case
select first (select count(*) from o where g = 2) id from o order by id
-- @case
select first 9223372036854775807 id from o order by id
-- @case
select first 2147483648 id from o order by id
-- @case
select skip 9223372036854775807 id from o order by id
-- @case
select id from o order by id rows 2
-- @case
select id from o order by id rows 2 to 4
-- @case
select id from o order by id rows 0
-- @case
select id from o order by id rows 0 to 2
-- @case
select id from o order by id rows 3 to 1
-- @case
select id from o order by id rows 1 to 1
-- @case
select id from o order by id rows 4 to 100
-- @case
select id from o order by id rows 100
-- @case
select id from o order by id rows -1
-- @case
select id from o order by id rows 1 to -1
-- @case
select id from o order by id rows null
-- @case
select id from o order by id rows 2 to null
-- @case
select id from o order by id rows 1 + 1
-- @case
select id from o order by id rows 2.5
-- @case
select id from o order by id rows '2'
-- @case
select id from o order by id rows (select count(*) from o where g = 2)
-- @case
select id from o order by id offset 2 rows
-- @case
select id from o order by id offset 0 rows
-- @case
select id from o order by id offset 100 rows
-- @case
select id from o order by id fetch first 2 rows only
-- @case
select id from o order by id fetch next 1 row only
-- @case
select id from o order by id fetch first row only
-- @case
select id from o order by id offset 1 row fetch next 2 rows only
-- @case
select id from o order by id offset 2 rows fetch first 0 rows only
-- @case
select id from o order by id offset -1 rows
-- @case
select id from o order by id fetch first -1 rows only
-- @case
select id from o order by id fetch first 2.5 rows only
-- @case
select id from o order by id fetch first null rows only
-- @case
select id from o order by id offset null rows
-- @case
select id from o order by id offset 9223372036854775807 rows
-- @case params: [2]
select first ? id from o order by id
-- @case params: [3]
select skip ? id from o order by id
-- @case params: [2, 1]
select first ? skip ? id from o order by id
-- @case params: [0]
select first ? id from o order by id
-- @case params: [-1]
select first ? id from o order by id
-- @case params: [null]
select first ? id from o order by id
-- @case params: ["2"]
select first ? id from o order by id
-- @case params: [2.7]
select first ? id from o order by id
-- @case params: [null]
select skip ? id from o order by id
-- @case params: [2]
select id from o order by id rows ?
-- @case params: [2, 3]
select id from o order by id rows ? to ?
-- @case params: [null]
select id from o order by id rows ?
-- @case params: [3, 2]
select id from o order by id rows ? to ?
-- @case params: [-2]
select id from o order by id rows ?
-- @case params: [1]
select id from o order by id offset ? rows
-- @case params: [1, 2]
select id from o order by id offset ? rows fetch next ? rows only
-- @case params: [0]
select id from o order by id fetch first ? rows only
-- @case params: [null]
select id from o order by id offset ? rows
-- @case params: [-3]
select id from o order by id fetch first ? rows only
-- @case params: [2.5]
select id from o order by id rows ?
-- @case
select first 2 skip 1 id from o order by id rows 1
-- @case
select first 2 id from o order by id offset 1 rows
-- @case
select id from o order by id rows 1 offset 1 rows
-- @case
select first 1 g, count(*) from o group by g order by 2 desc, 1
-- @case
select first 2 distinct g from o order by g
-- @case
select distinct first 2 g from o order by g
-- @case
select first 2 id from o union select first 1 id from o order by 1
-- @case
select id from o union all select id from o order by 1 rows 3 to 5
-- @case
select id from o union all select id from o order by 1 offset 15 rows fetch next 5 rows only
-- @case
select id from o rows 2
-- @case
select first 1 id from o where 1 = 0
-- @case
select id from o order by id rows 2 for update
-- @case
select first 2 id from o order by id with lock
-- @case
select (select first 1 id from o order by id desc) from rdb$database
-- @case
select (select id from o order by id rows 2 to 2) from rdb$database
-- @case
select (select id from o order by id offset 3 rows fetch next 1 row only) from rdb$database
-- @case
select (select id from o order by id rows 2) from rdb$database
