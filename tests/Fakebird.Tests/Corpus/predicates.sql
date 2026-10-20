-- Three-valued logic, comparisons between types, IN/BETWEEN/ANY/ALL/EXISTS/SINGULAR, IS [NOT] DISTINCT/TRUE/
-- FALSE/UNKNOWN, and the result types of CASE/COALESCE/IIF/DECODE/NULLIF/MAXVALUE/MINVALUE over type pairs.
-- @setup
create table p (id integer not null primary key, b boolean, i integer, n numeric(9,2), d double precision, s varchar(10),
    c char(4), dt date, ts timestamp, t time, bl blob sub_type text, bi bigint, sm smallint);
insert into p values (1, true, 1, 1.50, 1.5, '1', 'a', '2024-01-01', '2024-01-01 10:00:00', '10:00:00', 'x', 10000000000, 7);
insert into p values (2, false, 2, -2.25, -2.25, 'abc', 'b ', '2024-12-31', '2024-12-31 23:59:59', '23:59:59', 'y', -1, -7);
insert into p values (3, null, null, null, null, null, null, null, null, null, null, null, null);
create table e (x integer);
-- @case
select true and true, true or true, true and false, true or false, true and null, true or null, true and unknown, true or unknown, not true, true is true, true is false, true is unknown, true is not true from rdb$database
-- @case
select false and true, false or true, false and false, false or false, false and null, false or null, false and unknown, false or unknown, not false, false is true, false is false, false is unknown, false is not true from rdb$database
-- @case
select null and true, null or true, null and false, null or false, null and null, null or null, null and unknown, null or unknown, not null, null is true, null is false, null is unknown, null is not true from rdb$database
-- @case
select unknown and true, unknown or true, unknown and false, unknown or false, unknown and null, unknown or null, unknown and unknown, unknown or unknown, not unknown, unknown is true, unknown is false, unknown is unknown, unknown is not true from rdb$database
-- @case
select 1 = 1, 1 <> 1, 1 is distinct from 1, 1 is not distinct from 1, 1 < 1 from rdb$database
-- @case
select 1 = null, 1 <> null, 1 is distinct from null, 1 is not distinct from null, 1 < null from rdb$database
-- @case
select 1 = '1', 1 <> '1', 1 is distinct from '1', 1 is not distinct from '1', 1 < '1' from rdb$database
-- @case
select 1 = 1.00, 1 <> 1.00, 1 is distinct from 1.00, 1 is not distinct from 1.00, 1 < 1.00 from rdb$database
-- @case
select 1 = 'a', 1 <> 'a', 1 is distinct from 'a', 1 is not distinct from 'a', 1 < 'a' from rdb$database
-- @case
select 1 = true, 1 <> true, 1 is distinct from true, 1 is not distinct from true, 1 < true from rdb$database
-- @case
select 1 = '2024-01-01', 1 <> '2024-01-01', 1 is distinct from '2024-01-01', 1 is not distinct from '2024-01-01', 1 < '2024-01-01' from rdb$database
-- @case
select null = 1, null <> 1, null is distinct from 1, null is not distinct from 1, null < 1 from rdb$database
-- @case
select null = null, null <> null, null is distinct from null, null is not distinct from null, null < null from rdb$database
-- @case
select null = '1', null <> '1', null is distinct from '1', null is not distinct from '1', null < '1' from rdb$database
-- @case
select null = 1.00, null <> 1.00, null is distinct from 1.00, null is not distinct from 1.00, null < 1.00 from rdb$database
-- @case
select null = 'a', null <> 'a', null is distinct from 'a', null is not distinct from 'a', null < 'a' from rdb$database
-- @case
select null = true, null <> true, null is distinct from true, null is not distinct from true, null < true from rdb$database
-- @case
select null = '2024-01-01', null <> '2024-01-01', null is distinct from '2024-01-01', null is not distinct from '2024-01-01', null < '2024-01-01' from rdb$database
-- @case
select 'a' = 1, 'a' <> 1, 'a' is distinct from 1, 'a' is not distinct from 1, 'a' < 1 from rdb$database
-- @case
select 'a' = null, 'a' <> null, 'a' is distinct from null, 'a' is not distinct from null, 'a' < null from rdb$database
-- @case
select 'a' = '1', 'a' <> '1', 'a' is distinct from '1', 'a' is not distinct from '1', 'a' < '1' from rdb$database
-- @case
select 'a' = 1.00, 'a' <> 1.00, 'a' is distinct from 1.00, 'a' is not distinct from 1.00, 'a' < 1.00 from rdb$database
-- @case
select 'a' = 'a', 'a' <> 'a', 'a' is distinct from 'a', 'a' is not distinct from 'a', 'a' < 'a' from rdb$database
-- @case
select 'a' = true, 'a' <> true, 'a' is distinct from true, 'a' is not distinct from true, 'a' < true from rdb$database
-- @case
select 'a' = '2024-01-01', 'a' <> '2024-01-01', 'a' is distinct from '2024-01-01', 'a' is not distinct from '2024-01-01', 'a' < '2024-01-01' from rdb$database
-- @case
select 1.0 = 1, 1.0 <> 1, 1.0 is distinct from 1, 1.0 is not distinct from 1, 1.0 < 1 from rdb$database
-- @case
select 1.0 = null, 1.0 <> null, 1.0 is distinct from null, 1.0 is not distinct from null, 1.0 < null from rdb$database
-- @case
select 1.0 = '1', 1.0 <> '1', 1.0 is distinct from '1', 1.0 is not distinct from '1', 1.0 < '1' from rdb$database
-- @case
select 1.0 = 1.00, 1.0 <> 1.00, 1.0 is distinct from 1.00, 1.0 is not distinct from 1.00, 1.0 < 1.00 from rdb$database
-- @case
select 1.0 = 'a', 1.0 <> 'a', 1.0 is distinct from 'a', 1.0 is not distinct from 'a', 1.0 < 'a' from rdb$database
-- @case
select 1.0 = true, 1.0 <> true, 1.0 is distinct from true, 1.0 is not distinct from true, 1.0 < true from rdb$database
-- @case
select 1.0 = '2024-01-01', 1.0 <> '2024-01-01', 1.0 is distinct from '2024-01-01', 1.0 is not distinct from '2024-01-01', 1.0 < '2024-01-01' from rdb$database
-- @case
select 1e0 = 1, 1e0 <> 1, 1e0 is distinct from 1, 1e0 is not distinct from 1, 1e0 < 1 from rdb$database
-- @case
select 1e0 = null, 1e0 <> null, 1e0 is distinct from null, 1e0 is not distinct from null, 1e0 < null from rdb$database
-- @case
select 1e0 = '1', 1e0 <> '1', 1e0 is distinct from '1', 1e0 is not distinct from '1', 1e0 < '1' from rdb$database
-- @case
select 1e0 = 1.00, 1e0 <> 1.00, 1e0 is distinct from 1.00, 1e0 is not distinct from 1.00, 1e0 < 1.00 from rdb$database
-- @case
select 1e0 = 'a', 1e0 <> 'a', 1e0 is distinct from 'a', 1e0 is not distinct from 'a', 1e0 < 'a' from rdb$database
-- @case
select 1e0 = true, 1e0 <> true, 1e0 is distinct from true, 1e0 is not distinct from true, 1e0 < true from rdb$database
-- @case
select 1e0 = '2024-01-01', 1e0 <> '2024-01-01', 1e0 is distinct from '2024-01-01', 1e0 is not distinct from '2024-01-01', 1e0 < '2024-01-01' from rdb$database
-- @case
select date '2024-01-01' = 1, date '2024-01-01' <> 1, date '2024-01-01' is distinct from 1, date '2024-01-01' is not distinct from 1, date '2024-01-01' < 1 from rdb$database
-- @case
select date '2024-01-01' = null, date '2024-01-01' <> null, date '2024-01-01' is distinct from null, date '2024-01-01' is not distinct from null, date '2024-01-01' < null from rdb$database
-- @case
select date '2024-01-01' = '1', date '2024-01-01' <> '1', date '2024-01-01' is distinct from '1', date '2024-01-01' is not distinct from '1', date '2024-01-01' < '1' from rdb$database
-- @case
select date '2024-01-01' = 1.00, date '2024-01-01' <> 1.00, date '2024-01-01' is distinct from 1.00, date '2024-01-01' is not distinct from 1.00, date '2024-01-01' < 1.00 from rdb$database
-- @case
select date '2024-01-01' = 'a', date '2024-01-01' <> 'a', date '2024-01-01' is distinct from 'a', date '2024-01-01' is not distinct from 'a', date '2024-01-01' < 'a' from rdb$database
-- @case
select date '2024-01-01' = true, date '2024-01-01' <> true, date '2024-01-01' is distinct from true, date '2024-01-01' is not distinct from true, date '2024-01-01' < true from rdb$database
-- @case
select date '2024-01-01' = '2024-01-01', date '2024-01-01' <> '2024-01-01', date '2024-01-01' is distinct from '2024-01-01', date '2024-01-01' is not distinct from '2024-01-01', date '2024-01-01' < '2024-01-01' from rdb$database
-- @case
select true = 1, true <> 1, true is distinct from 1, true is not distinct from 1, true < 1 from rdb$database
-- @case
select true = null, true <> null, true is distinct from null, true is not distinct from null, true < null from rdb$database
-- @case
select true = '1', true <> '1', true is distinct from '1', true is not distinct from '1', true < '1' from rdb$database
-- @case
select true = 1.00, true <> 1.00, true is distinct from 1.00, true is not distinct from 1.00, true < 1.00 from rdb$database
-- @case
select true = 'a', true <> 'a', true is distinct from 'a', true is not distinct from 'a', true < 'a' from rdb$database
-- @case
select true = true, true <> true, true is distinct from true, true is not distinct from true, true < true from rdb$database
-- @case
select true = '2024-01-01', true <> '2024-01-01', true is distinct from '2024-01-01', true is not distinct from '2024-01-01', true < '2024-01-01' from rdb$database
-- @case
select 1 in (1, 2), 1 in (1, null), 1 not in (1, null), 1 not in (2, 3), 1 in (select x from e), 1 not in (select x from e) from rdb$database
-- @case
select 1 = any (select x from e), 1 = all (select x from e), 1 > some (select i from p), 1 > all (select i from p), 1 >= all (select i from p where i is not null) from rdb$database
-- @case
select 1 between 1 and 3, 1 between 3 and 1, 1 not between 1 and null, 1 between null and 5 from rdb$database
-- @case
select null in (1, 2), null in (1, null), null not in (1, null), null not in (2, 3), null in (select x from e), null not in (select x from e) from rdb$database
-- @case
select null = any (select x from e), null = all (select x from e), null > some (select i from p), null > all (select i from p), null >= all (select i from p where i is not null) from rdb$database
-- @case
select null between 1 and 3, null between 3 and 1, null not between 1 and null, null between null and 5 from rdb$database
-- @case
select 3 in (1, 2), 3 in (1, null), 3 not in (1, null), 3 not in (2, 3), 3 in (select x from e), 3 not in (select x from e) from rdb$database
-- @case
select 3 = any (select x from e), 3 = all (select x from e), 3 > some (select i from p), 3 > all (select i from p), 3 >= all (select i from p where i is not null) from rdb$database
-- @case
select 3 between 1 and 3, 3 between 3 and 1, 3 not between 1 and null, 3 between null and 5 from rdb$database
-- @case
select id, b is null, b is not null, b is distinct from null, b = b, b is not distinct from b, coalesce(b, b) from p order by id
-- @case
select id, i is null, i is not null, i is distinct from null, i = i, i is not distinct from i, coalesce(i, i) from p order by id
-- @case
select id, n is null, n is not null, n is distinct from null, n = n, n is not distinct from n, coalesce(n, n) from p order by id
-- @case
select id, d is null, d is not null, d is distinct from null, d = d, d is not distinct from d, coalesce(d, d) from p order by id
-- @case
select id, s is null, s is not null, s is distinct from null, s = s, s is not distinct from s, coalesce(s, s) from p order by id
-- @case
select id, c is null, c is not null, c is distinct from null, c = c, c is not distinct from c, coalesce(c, c) from p order by id
-- @case
select id, dt is null, dt is not null, dt is distinct from null, dt = dt, dt is not distinct from dt, coalesce(dt, dt) from p order by id
-- @case
select id, ts is null, ts is not null, ts is distinct from null, ts = ts, ts is not distinct from ts, coalesce(ts, ts) from p order by id
-- @case
select id, t is null, t is not null, t is distinct from null, t = t, t is not distinct from t, coalesce(t, t) from p order by id
-- @case
select id, bl is null, bl is not null, bl is distinct from null, bl = bl, bl is not distinct from bl, coalesce(bl, bl) from p order by id
-- @case
select id, bi is null, bi is not null, bi is distinct from null, bi = bi, bi is not distinct from bi, coalesce(bi, bi) from p order by id
-- @case
select id, sm is null, sm is not null, sm is distinct from null, sm = sm, sm is not distinct from sm, coalesce(sm, sm) from p order by id
-- @case
select coalesce(i, i), iif(id = 1, i, i), case id when 2 then i else i end from p order by id
-- @case
select coalesce(i, n), iif(id = 1, i, n), case id when 2 then i else n end from p order by id
-- @case
select coalesce(i, d), iif(id = 1, i, d), case id when 2 then i else d end from p order by id
-- @case
select coalesce(i, s), iif(id = 1, i, s), case id when 2 then i else s end from p order by id
-- @case
select coalesce(i, c), iif(id = 1, i, c), case id when 2 then i else c end from p order by id
-- @case
select coalesce(i, dt), iif(id = 1, i, dt), case id when 2 then i else dt end from p order by id
-- @case
select coalesce(i, ts), iif(id = 1, i, ts), case id when 2 then i else ts end from p order by id
-- @case
select coalesce(i, t), iif(id = 1, i, t), case id when 2 then i else t end from p order by id
-- @case
select coalesce(i, b), iif(id = 1, i, b), case id when 2 then i else b end from p order by id
-- @case
select coalesce(i, bl), iif(id = 1, i, bl), case id when 2 then i else bl end from p order by id
-- @case
select coalesce(i, bi), iif(id = 1, i, bi), case id when 2 then i else bi end from p order by id
-- @case
select coalesce(i, sm), iif(id = 1, i, sm), case id when 2 then i else sm end from p order by id
-- @case
select coalesce(i, null), iif(id = 1, i, null), case id when 2 then i else null end from p order by id
-- @case
select coalesce(i, 1), iif(id = 1, i, 1), case id when 2 then i else 1 end from p order by id
-- @case
select coalesce(i, 1.5), iif(id = 1, i, 1.5), case id when 2 then i else 1.5 end from p order by id
-- @case
select coalesce(i, 1e0), iif(id = 1, i, 1e0), case id when 2 then i else 1e0 end from p order by id
-- @case
select coalesce(i, 'x'), iif(id = 1, i, 'x'), case id when 2 then i else 'x' end from p order by id
-- @case
select coalesce(i, ''), iif(id = 1, i, ''), case id when 2 then i else '' end from p order by id
-- @case
select coalesce(n, i), iif(id = 1, n, i), case id when 2 then n else i end from p order by id
-- @case
select coalesce(n, n), iif(id = 1, n, n), case id when 2 then n else n end from p order by id
-- @case
select coalesce(n, d), iif(id = 1, n, d), case id when 2 then n else d end from p order by id
-- @case
select coalesce(n, s), iif(id = 1, n, s), case id when 2 then n else s end from p order by id
-- @case
select coalesce(n, c), iif(id = 1, n, c), case id when 2 then n else c end from p order by id
-- @case
select coalesce(n, dt), iif(id = 1, n, dt), case id when 2 then n else dt end from p order by id
-- @case
select coalesce(n, ts), iif(id = 1, n, ts), case id when 2 then n else ts end from p order by id
-- @case
select coalesce(n, t), iif(id = 1, n, t), case id when 2 then n else t end from p order by id
-- @case
select coalesce(n, b), iif(id = 1, n, b), case id when 2 then n else b end from p order by id
-- @case
select coalesce(n, bl), iif(id = 1, n, bl), case id when 2 then n else bl end from p order by id
-- @case
select coalesce(n, bi), iif(id = 1, n, bi), case id when 2 then n else bi end from p order by id
-- @case
select coalesce(n, sm), iif(id = 1, n, sm), case id when 2 then n else sm end from p order by id
-- @case
select coalesce(n, null), iif(id = 1, n, null), case id when 2 then n else null end from p order by id
-- @case
select coalesce(n, 1), iif(id = 1, n, 1), case id when 2 then n else 1 end from p order by id
-- @case
select coalesce(n, 1.5), iif(id = 1, n, 1.5), case id when 2 then n else 1.5 end from p order by id
-- @case
select coalesce(n, 1e0), iif(id = 1, n, 1e0), case id when 2 then n else 1e0 end from p order by id
-- @case
select coalesce(n, 'x'), iif(id = 1, n, 'x'), case id when 2 then n else 'x' end from p order by id
-- @case
select coalesce(n, ''), iif(id = 1, n, ''), case id when 2 then n else '' end from p order by id
-- @case
select coalesce(d, i), iif(id = 1, d, i), case id when 2 then d else i end from p order by id
-- @case
select coalesce(d, n), iif(id = 1, d, n), case id when 2 then d else n end from p order by id
-- @case
select coalesce(d, d), iif(id = 1, d, d), case id when 2 then d else d end from p order by id
-- @case
select coalesce(d, s), iif(id = 1, d, s), case id when 2 then d else s end from p order by id
-- @case
select coalesce(d, c), iif(id = 1, d, c), case id when 2 then d else c end from p order by id
-- @case
select coalesce(d, dt), iif(id = 1, d, dt), case id when 2 then d else dt end from p order by id
-- @case
select coalesce(d, ts), iif(id = 1, d, ts), case id when 2 then d else ts end from p order by id
-- @case
select coalesce(d, t), iif(id = 1, d, t), case id when 2 then d else t end from p order by id
-- @case
select coalesce(d, b), iif(id = 1, d, b), case id when 2 then d else b end from p order by id
-- @case
select coalesce(d, bl), iif(id = 1, d, bl), case id when 2 then d else bl end from p order by id
-- @case
select coalesce(d, bi), iif(id = 1, d, bi), case id when 2 then d else bi end from p order by id
-- @case
select coalesce(d, sm), iif(id = 1, d, sm), case id when 2 then d else sm end from p order by id
-- @case
select coalesce(d, null), iif(id = 1, d, null), case id when 2 then d else null end from p order by id
-- @case
select coalesce(d, 1), iif(id = 1, d, 1), case id when 2 then d else 1 end from p order by id
-- @case
select coalesce(d, 1.5), iif(id = 1, d, 1.5), case id when 2 then d else 1.5 end from p order by id
-- @case
select coalesce(d, 1e0), iif(id = 1, d, 1e0), case id when 2 then d else 1e0 end from p order by id
-- @case
select coalesce(d, 'x'), iif(id = 1, d, 'x'), case id when 2 then d else 'x' end from p order by id
-- @case
select coalesce(d, ''), iif(id = 1, d, ''), case id when 2 then d else '' end from p order by id
-- @case
select coalesce(s, i), iif(id = 1, s, i), case id when 2 then s else i end from p order by id
-- @case
select coalesce(s, n), iif(id = 1, s, n), case id when 2 then s else n end from p order by id
-- @case
select coalesce(s, d), iif(id = 1, s, d), case id when 2 then s else d end from p order by id
-- @case
select coalesce(s, s), iif(id = 1, s, s), case id when 2 then s else s end from p order by id
-- @case
select coalesce(s, c), iif(id = 1, s, c), case id when 2 then s else c end from p order by id
-- @case
select coalesce(s, dt), iif(id = 1, s, dt), case id when 2 then s else dt end from p order by id
-- @case
select coalesce(s, ts), iif(id = 1, s, ts), case id when 2 then s else ts end from p order by id
-- @case
select coalesce(s, t), iif(id = 1, s, t), case id when 2 then s else t end from p order by id
-- @case
select coalesce(s, b), iif(id = 1, s, b), case id when 2 then s else b end from p order by id
-- @case
select coalesce(s, bl), iif(id = 1, s, bl), case id when 2 then s else bl end from p order by id
-- @case
select coalesce(s, bi), iif(id = 1, s, bi), case id when 2 then s else bi end from p order by id
-- @case
select coalesce(s, sm), iif(id = 1, s, sm), case id when 2 then s else sm end from p order by id
-- @case
select coalesce(s, null), iif(id = 1, s, null), case id when 2 then s else null end from p order by id
-- @case
select coalesce(s, 1), iif(id = 1, s, 1), case id when 2 then s else 1 end from p order by id
-- @case
select coalesce(s, 1.5), iif(id = 1, s, 1.5), case id when 2 then s else 1.5 end from p order by id
-- @case
select coalesce(s, 1e0), iif(id = 1, s, 1e0), case id when 2 then s else 1e0 end from p order by id
-- @case
select coalesce(s, 'x'), iif(id = 1, s, 'x'), case id when 2 then s else 'x' end from p order by id
-- @case
select coalesce(s, ''), iif(id = 1, s, ''), case id when 2 then s else '' end from p order by id
-- @case
select coalesce(c, i), iif(id = 1, c, i), case id when 2 then c else i end from p order by id
-- @case
select coalesce(c, n), iif(id = 1, c, n), case id when 2 then c else n end from p order by id
-- @case
select coalesce(c, d), iif(id = 1, c, d), case id when 2 then c else d end from p order by id
-- @case
select coalesce(c, s), iif(id = 1, c, s), case id when 2 then c else s end from p order by id
-- @case
select coalesce(c, c), iif(id = 1, c, c), case id when 2 then c else c end from p order by id
-- @case
select coalesce(c, dt), iif(id = 1, c, dt), case id when 2 then c else dt end from p order by id
-- @case
select coalesce(c, ts), iif(id = 1, c, ts), case id when 2 then c else ts end from p order by id
-- @case
select coalesce(c, t), iif(id = 1, c, t), case id when 2 then c else t end from p order by id
-- @case
select coalesce(c, b), iif(id = 1, c, b), case id when 2 then c else b end from p order by id
-- @case
select coalesce(c, bl), iif(id = 1, c, bl), case id when 2 then c else bl end from p order by id
-- @case
select coalesce(c, bi), iif(id = 1, c, bi), case id when 2 then c else bi end from p order by id
-- @case
select coalesce(c, sm), iif(id = 1, c, sm), case id when 2 then c else sm end from p order by id
-- @case
select coalesce(c, null), iif(id = 1, c, null), case id when 2 then c else null end from p order by id
-- @case
select coalesce(c, 1), iif(id = 1, c, 1), case id when 2 then c else 1 end from p order by id
-- @case
select coalesce(c, 1.5), iif(id = 1, c, 1.5), case id when 2 then c else 1.5 end from p order by id
-- @case
select coalesce(c, 1e0), iif(id = 1, c, 1e0), case id when 2 then c else 1e0 end from p order by id
-- @case
select coalesce(c, 'x'), iif(id = 1, c, 'x'), case id when 2 then c else 'x' end from p order by id
-- @case
select coalesce(c, ''), iif(id = 1, c, ''), case id when 2 then c else '' end from p order by id
-- @case
select coalesce(dt, i), iif(id = 1, dt, i), case id when 2 then dt else i end from p order by id
-- @case
select coalesce(dt, n), iif(id = 1, dt, n), case id when 2 then dt else n end from p order by id
-- @case
select coalesce(dt, d), iif(id = 1, dt, d), case id when 2 then dt else d end from p order by id
-- @case
select coalesce(dt, s), iif(id = 1, dt, s), case id when 2 then dt else s end from p order by id
-- @case
select coalesce(dt, c), iif(id = 1, dt, c), case id when 2 then dt else c end from p order by id
-- @case
select coalesce(dt, dt), iif(id = 1, dt, dt), case id when 2 then dt else dt end from p order by id
-- @case
select coalesce(dt, ts), iif(id = 1, dt, ts), case id when 2 then dt else ts end from p order by id
-- @case
select coalesce(dt, t), iif(id = 1, dt, t), case id when 2 then dt else t end from p order by id
-- @case
select coalesce(dt, b), iif(id = 1, dt, b), case id when 2 then dt else b end from p order by id
-- @case
select coalesce(dt, bl), iif(id = 1, dt, bl), case id when 2 then dt else bl end from p order by id
-- @case
select coalesce(dt, bi), iif(id = 1, dt, bi), case id when 2 then dt else bi end from p order by id
-- @case
select coalesce(dt, sm), iif(id = 1, dt, sm), case id when 2 then dt else sm end from p order by id
-- @case
select coalesce(dt, null), iif(id = 1, dt, null), case id when 2 then dt else null end from p order by id
-- @case
select coalesce(dt, 1), iif(id = 1, dt, 1), case id when 2 then dt else 1 end from p order by id
-- @case
select coalesce(dt, 1.5), iif(id = 1, dt, 1.5), case id when 2 then dt else 1.5 end from p order by id
-- @case
select coalesce(dt, 1e0), iif(id = 1, dt, 1e0), case id when 2 then dt else 1e0 end from p order by id
-- @case
select coalesce(dt, 'x'), iif(id = 1, dt, 'x'), case id when 2 then dt else 'x' end from p order by id
-- @case
select coalesce(dt, ''), iif(id = 1, dt, ''), case id when 2 then dt else '' end from p order by id
-- @case
select coalesce(ts, i), iif(id = 1, ts, i), case id when 2 then ts else i end from p order by id
-- @case
select coalesce(ts, n), iif(id = 1, ts, n), case id when 2 then ts else n end from p order by id
-- @case
select coalesce(ts, d), iif(id = 1, ts, d), case id when 2 then ts else d end from p order by id
-- @case
select coalesce(ts, s), iif(id = 1, ts, s), case id when 2 then ts else s end from p order by id
-- @case
select coalesce(ts, c), iif(id = 1, ts, c), case id when 2 then ts else c end from p order by id
-- @case
select coalesce(ts, dt), iif(id = 1, ts, dt), case id when 2 then ts else dt end from p order by id
-- @case
select coalesce(ts, ts), iif(id = 1, ts, ts), case id when 2 then ts else ts end from p order by id
-- @case
select coalesce(ts, t), iif(id = 1, ts, t), case id when 2 then ts else t end from p order by id
-- @case
select coalesce(ts, b), iif(id = 1, ts, b), case id when 2 then ts else b end from p order by id
-- @case
select coalesce(ts, bl), iif(id = 1, ts, bl), case id when 2 then ts else bl end from p order by id
-- @case
select coalesce(ts, bi), iif(id = 1, ts, bi), case id when 2 then ts else bi end from p order by id
-- @case
select coalesce(ts, sm), iif(id = 1, ts, sm), case id when 2 then ts else sm end from p order by id
-- @case
select coalesce(ts, null), iif(id = 1, ts, null), case id when 2 then ts else null end from p order by id
-- @case
select coalesce(ts, 1), iif(id = 1, ts, 1), case id when 2 then ts else 1 end from p order by id
-- @case
select coalesce(ts, 1.5), iif(id = 1, ts, 1.5), case id when 2 then ts else 1.5 end from p order by id
-- @case
select coalesce(ts, 1e0), iif(id = 1, ts, 1e0), case id when 2 then ts else 1e0 end from p order by id
-- @case
select coalesce(ts, 'x'), iif(id = 1, ts, 'x'), case id when 2 then ts else 'x' end from p order by id
-- @case
select coalesce(ts, ''), iif(id = 1, ts, ''), case id when 2 then ts else '' end from p order by id
-- @case
select coalesce(t, i), iif(id = 1, t, i), case id when 2 then t else i end from p order by id
-- @case
select coalesce(t, n), iif(id = 1, t, n), case id when 2 then t else n end from p order by id
-- @case
select coalesce(t, d), iif(id = 1, t, d), case id when 2 then t else d end from p order by id
-- @case
select coalesce(t, s), iif(id = 1, t, s), case id when 2 then t else s end from p order by id
-- @case
select coalesce(t, c), iif(id = 1, t, c), case id when 2 then t else c end from p order by id
-- @case
select coalesce(t, dt), iif(id = 1, t, dt), case id when 2 then t else dt end from p order by id
-- @case
select coalesce(t, ts), iif(id = 1, t, ts), case id when 2 then t else ts end from p order by id
-- @case
select coalesce(t, t), iif(id = 1, t, t), case id when 2 then t else t end from p order by id
-- @case
select coalesce(t, b), iif(id = 1, t, b), case id when 2 then t else b end from p order by id
-- @case
select coalesce(t, bl), iif(id = 1, t, bl), case id when 2 then t else bl end from p order by id
-- @case
select coalesce(t, bi), iif(id = 1, t, bi), case id when 2 then t else bi end from p order by id
-- @case
select coalesce(t, sm), iif(id = 1, t, sm), case id when 2 then t else sm end from p order by id
-- @case
select coalesce(t, null), iif(id = 1, t, null), case id when 2 then t else null end from p order by id
-- @case
select coalesce(t, 1), iif(id = 1, t, 1), case id when 2 then t else 1 end from p order by id
-- @case
select coalesce(t, 1.5), iif(id = 1, t, 1.5), case id when 2 then t else 1.5 end from p order by id
-- @case
select coalesce(t, 1e0), iif(id = 1, t, 1e0), case id when 2 then t else 1e0 end from p order by id
-- @case
select coalesce(t, 'x'), iif(id = 1, t, 'x'), case id when 2 then t else 'x' end from p order by id
-- @case
select coalesce(t, ''), iif(id = 1, t, ''), case id when 2 then t else '' end from p order by id
-- @case
select coalesce(b, i), iif(id = 1, b, i), case id when 2 then b else i end from p order by id
-- @case
select coalesce(b, n), iif(id = 1, b, n), case id when 2 then b else n end from p order by id
-- @case
select coalesce(b, d), iif(id = 1, b, d), case id when 2 then b else d end from p order by id
-- @case
select coalesce(b, s), iif(id = 1, b, s), case id when 2 then b else s end from p order by id
-- @case
select coalesce(b, c), iif(id = 1, b, c), case id when 2 then b else c end from p order by id
-- @case
select coalesce(b, dt), iif(id = 1, b, dt), case id when 2 then b else dt end from p order by id
-- @case
select coalesce(b, ts), iif(id = 1, b, ts), case id when 2 then b else ts end from p order by id
-- @case
select coalesce(b, t), iif(id = 1, b, t), case id when 2 then b else t end from p order by id
-- @case
select coalesce(b, b), iif(id = 1, b, b), case id when 2 then b else b end from p order by id
-- @case
select coalesce(b, bl), iif(id = 1, b, bl), case id when 2 then b else bl end from p order by id
-- @case
select coalesce(b, bi), iif(id = 1, b, bi), case id when 2 then b else bi end from p order by id
-- @case
select coalesce(b, sm), iif(id = 1, b, sm), case id when 2 then b else sm end from p order by id
-- @case
select coalesce(b, null), iif(id = 1, b, null), case id when 2 then b else null end from p order by id
-- @case
select coalesce(b, 1), iif(id = 1, b, 1), case id when 2 then b else 1 end from p order by id
-- @case
select coalesce(b, 1.5), iif(id = 1, b, 1.5), case id when 2 then b else 1.5 end from p order by id
-- @case
select coalesce(b, 1e0), iif(id = 1, b, 1e0), case id when 2 then b else 1e0 end from p order by id
-- @case
select coalesce(b, 'x'), iif(id = 1, b, 'x'), case id when 2 then b else 'x' end from p order by id
-- @case
select coalesce(b, ''), iif(id = 1, b, ''), case id when 2 then b else '' end from p order by id
-- @case
select coalesce(bl, i), iif(id = 1, bl, i), case id when 2 then bl else i end from p order by id
-- @case
select coalesce(bl, n), iif(id = 1, bl, n), case id when 2 then bl else n end from p order by id
-- @case
select coalesce(bl, d), iif(id = 1, bl, d), case id when 2 then bl else d end from p order by id
-- @case
select coalesce(bl, s), iif(id = 1, bl, s), case id when 2 then bl else s end from p order by id
-- @case
select coalesce(bl, c), iif(id = 1, bl, c), case id when 2 then bl else c end from p order by id
-- @case
select coalesce(bl, dt), iif(id = 1, bl, dt), case id when 2 then bl else dt end from p order by id
-- @case
select coalesce(bl, ts), iif(id = 1, bl, ts), case id when 2 then bl else ts end from p order by id
-- @case
select coalesce(bl, t), iif(id = 1, bl, t), case id when 2 then bl else t end from p order by id
-- @case
select coalesce(bl, b), iif(id = 1, bl, b), case id when 2 then bl else b end from p order by id
-- @case
select coalesce(bl, bl), iif(id = 1, bl, bl), case id when 2 then bl else bl end from p order by id
-- @case
select coalesce(bl, bi), iif(id = 1, bl, bi), case id when 2 then bl else bi end from p order by id
-- @case
select coalesce(bl, sm), iif(id = 1, bl, sm), case id when 2 then bl else sm end from p order by id
-- @case
select coalesce(bl, null), iif(id = 1, bl, null), case id when 2 then bl else null end from p order by id
-- @case
select coalesce(bl, 1), iif(id = 1, bl, 1), case id when 2 then bl else 1 end from p order by id
-- @case
select coalesce(bl, 1.5), iif(id = 1, bl, 1.5), case id when 2 then bl else 1.5 end from p order by id
-- @case
select coalesce(bl, 1e0), iif(id = 1, bl, 1e0), case id when 2 then bl else 1e0 end from p order by id
-- @case
select coalesce(bl, 'x'), iif(id = 1, bl, 'x'), case id when 2 then bl else 'x' end from p order by id
-- @case
select coalesce(bl, ''), iif(id = 1, bl, ''), case id when 2 then bl else '' end from p order by id
-- @case
select coalesce(bi, i), iif(id = 1, bi, i), case id when 2 then bi else i end from p order by id
-- @case
select coalesce(bi, n), iif(id = 1, bi, n), case id when 2 then bi else n end from p order by id
-- @case
select coalesce(bi, d), iif(id = 1, bi, d), case id when 2 then bi else d end from p order by id
-- @case
select coalesce(bi, s), iif(id = 1, bi, s), case id when 2 then bi else s end from p order by id
-- @case
select coalesce(bi, c), iif(id = 1, bi, c), case id when 2 then bi else c end from p order by id
-- @case
select coalesce(bi, dt), iif(id = 1, bi, dt), case id when 2 then bi else dt end from p order by id
-- @case
select coalesce(bi, ts), iif(id = 1, bi, ts), case id when 2 then bi else ts end from p order by id
-- @case
select coalesce(bi, t), iif(id = 1, bi, t), case id when 2 then bi else t end from p order by id
-- @case
select coalesce(bi, b), iif(id = 1, bi, b), case id when 2 then bi else b end from p order by id
-- @case
select coalesce(bi, bl), iif(id = 1, bi, bl), case id when 2 then bi else bl end from p order by id
-- @case
select coalesce(bi, bi), iif(id = 1, bi, bi), case id when 2 then bi else bi end from p order by id
-- @case
select coalesce(bi, sm), iif(id = 1, bi, sm), case id when 2 then bi else sm end from p order by id
-- @case
select coalesce(bi, null), iif(id = 1, bi, null), case id when 2 then bi else null end from p order by id
-- @case
select coalesce(bi, 1), iif(id = 1, bi, 1), case id when 2 then bi else 1 end from p order by id
-- @case
select coalesce(bi, 1.5), iif(id = 1, bi, 1.5), case id when 2 then bi else 1.5 end from p order by id
-- @case
select coalesce(bi, 1e0), iif(id = 1, bi, 1e0), case id when 2 then bi else 1e0 end from p order by id
-- @case
select coalesce(bi, 'x'), iif(id = 1, bi, 'x'), case id when 2 then bi else 'x' end from p order by id
-- @case
select coalesce(bi, ''), iif(id = 1, bi, ''), case id when 2 then bi else '' end from p order by id
-- @case
select coalesce(sm, i), iif(id = 1, sm, i), case id when 2 then sm else i end from p order by id
-- @case
select coalesce(sm, n), iif(id = 1, sm, n), case id when 2 then sm else n end from p order by id
-- @case
select coalesce(sm, d), iif(id = 1, sm, d), case id when 2 then sm else d end from p order by id
-- @case
select coalesce(sm, s), iif(id = 1, sm, s), case id when 2 then sm else s end from p order by id
-- @case
select coalesce(sm, c), iif(id = 1, sm, c), case id when 2 then sm else c end from p order by id
-- @case
select coalesce(sm, dt), iif(id = 1, sm, dt), case id when 2 then sm else dt end from p order by id
-- @case
select coalesce(sm, ts), iif(id = 1, sm, ts), case id when 2 then sm else ts end from p order by id
-- @case
select coalesce(sm, t), iif(id = 1, sm, t), case id when 2 then sm else t end from p order by id
-- @case
select coalesce(sm, b), iif(id = 1, sm, b), case id when 2 then sm else b end from p order by id
-- @case
select coalesce(sm, bl), iif(id = 1, sm, bl), case id when 2 then sm else bl end from p order by id
-- @case
select coalesce(sm, bi), iif(id = 1, sm, bi), case id when 2 then sm else bi end from p order by id
-- @case
select coalesce(sm, sm), iif(id = 1, sm, sm), case id when 2 then sm else sm end from p order by id
-- @case
select coalesce(sm, null), iif(id = 1, sm, null), case id when 2 then sm else null end from p order by id
-- @case
select coalesce(sm, 1), iif(id = 1, sm, 1), case id when 2 then sm else 1 end from p order by id
-- @case
select coalesce(sm, 1.5), iif(id = 1, sm, 1.5), case id when 2 then sm else 1.5 end from p order by id
-- @case
select coalesce(sm, 1e0), iif(id = 1, sm, 1e0), case id when 2 then sm else 1e0 end from p order by id
-- @case
select coalesce(sm, 'x'), iif(id = 1, sm, 'x'), case id when 2 then sm else 'x' end from p order by id
-- @case
select coalesce(sm, ''), iif(id = 1, sm, ''), case id when 2 then sm else '' end from p order by id
-- @case
select coalesce(null, i), iif(id = 1, null, i), case id when 2 then null else i end from p order by id
-- @case
select coalesce(null, n), iif(id = 1, null, n), case id when 2 then null else n end from p order by id
-- @case
select coalesce(null, d), iif(id = 1, null, d), case id when 2 then null else d end from p order by id
-- @case
select coalesce(null, s), iif(id = 1, null, s), case id when 2 then null else s end from p order by id
-- @case
select coalesce(null, c), iif(id = 1, null, c), case id when 2 then null else c end from p order by id
-- @case
select coalesce(null, dt), iif(id = 1, null, dt), case id when 2 then null else dt end from p order by id
-- @case
select coalesce(null, ts), iif(id = 1, null, ts), case id when 2 then null else ts end from p order by id
-- @case
select coalesce(null, t), iif(id = 1, null, t), case id when 2 then null else t end from p order by id
-- @case
select coalesce(null, b), iif(id = 1, null, b), case id when 2 then null else b end from p order by id
-- @case
select coalesce(null, bl), iif(id = 1, null, bl), case id when 2 then null else bl end from p order by id
-- @case
select coalesce(null, bi), iif(id = 1, null, bi), case id when 2 then null else bi end from p order by id
-- @case
select coalesce(null, sm), iif(id = 1, null, sm), case id when 2 then null else sm end from p order by id
-- @case
select coalesce(null, null), iif(id = 1, null, null), case id when 2 then null else null end from p order by id
-- @case
select coalesce(null, 1), iif(id = 1, null, 1), case id when 2 then null else 1 end from p order by id
-- @case
select coalesce(null, 1.5), iif(id = 1, null, 1.5), case id when 2 then null else 1.5 end from p order by id
-- @case
select coalesce(null, 1e0), iif(id = 1, null, 1e0), case id when 2 then null else 1e0 end from p order by id
-- @case
select coalesce(null, 'x'), iif(id = 1, null, 'x'), case id when 2 then null else 'x' end from p order by id
-- @case
select coalesce(null, ''), iif(id = 1, null, ''), case id when 2 then null else '' end from p order by id
-- @case
select coalesce(1, i), iif(id = 1, 1, i), case id when 2 then 1 else i end from p order by id
-- @case
select coalesce(1, n), iif(id = 1, 1, n), case id when 2 then 1 else n end from p order by id
-- @case
select coalesce(1, d), iif(id = 1, 1, d), case id when 2 then 1 else d end from p order by id
-- @case
select coalesce(1, s), iif(id = 1, 1, s), case id when 2 then 1 else s end from p order by id
-- @case
select coalesce(1, c), iif(id = 1, 1, c), case id when 2 then 1 else c end from p order by id
-- @case
select coalesce(1, dt), iif(id = 1, 1, dt), case id when 2 then 1 else dt end from p order by id
-- @case
select coalesce(1, ts), iif(id = 1, 1, ts), case id when 2 then 1 else ts end from p order by id
-- @case
select coalesce(1, t), iif(id = 1, 1, t), case id when 2 then 1 else t end from p order by id
-- @case
select coalesce(1, b), iif(id = 1, 1, b), case id when 2 then 1 else b end from p order by id
-- @case
select coalesce(1, bl), iif(id = 1, 1, bl), case id when 2 then 1 else bl end from p order by id
-- @case
select coalesce(1, bi), iif(id = 1, 1, bi), case id when 2 then 1 else bi end from p order by id
-- @case
select coalesce(1, sm), iif(id = 1, 1, sm), case id when 2 then 1 else sm end from p order by id
-- @case
select coalesce(1, null), iif(id = 1, 1, null), case id when 2 then 1 else null end from p order by id
-- @case
select coalesce(1, 1), iif(id = 1, 1, 1), case id when 2 then 1 else 1 end from p order by id
-- @case
select coalesce(1, 1.5), iif(id = 1, 1, 1.5), case id when 2 then 1 else 1.5 end from p order by id
-- @case
select coalesce(1, 1e0), iif(id = 1, 1, 1e0), case id when 2 then 1 else 1e0 end from p order by id
-- @case
select coalesce(1, 'x'), iif(id = 1, 1, 'x'), case id when 2 then 1 else 'x' end from p order by id
-- @case
select coalesce(1, ''), iif(id = 1, 1, ''), case id when 2 then 1 else '' end from p order by id
-- @case
select coalesce(1.5, i), iif(id = 1, 1.5, i), case id when 2 then 1.5 else i end from p order by id
-- @case
select coalesce(1.5, n), iif(id = 1, 1.5, n), case id when 2 then 1.5 else n end from p order by id
-- @case
select coalesce(1.5, d), iif(id = 1, 1.5, d), case id when 2 then 1.5 else d end from p order by id
-- @case
select coalesce(1.5, s), iif(id = 1, 1.5, s), case id when 2 then 1.5 else s end from p order by id
-- @case
select coalesce(1.5, c), iif(id = 1, 1.5, c), case id when 2 then 1.5 else c end from p order by id
-- @case
select coalesce(1.5, dt), iif(id = 1, 1.5, dt), case id when 2 then 1.5 else dt end from p order by id
-- @case
select coalesce(1.5, ts), iif(id = 1, 1.5, ts), case id when 2 then 1.5 else ts end from p order by id
-- @case
select coalesce(1.5, t), iif(id = 1, 1.5, t), case id when 2 then 1.5 else t end from p order by id
-- @case
select coalesce(1.5, b), iif(id = 1, 1.5, b), case id when 2 then 1.5 else b end from p order by id
-- @case
select coalesce(1.5, bl), iif(id = 1, 1.5, bl), case id when 2 then 1.5 else bl end from p order by id
-- @case
select coalesce(1.5, bi), iif(id = 1, 1.5, bi), case id when 2 then 1.5 else bi end from p order by id
-- @case
select coalesce(1.5, sm), iif(id = 1, 1.5, sm), case id when 2 then 1.5 else sm end from p order by id
-- @case
select coalesce(1.5, null), iif(id = 1, 1.5, null), case id when 2 then 1.5 else null end from p order by id
-- @case
select coalesce(1.5, 1), iif(id = 1, 1.5, 1), case id when 2 then 1.5 else 1 end from p order by id
-- @case
select coalesce(1.5, 1.5), iif(id = 1, 1.5, 1.5), case id when 2 then 1.5 else 1.5 end from p order by id
-- @case
select coalesce(1.5, 1e0), iif(id = 1, 1.5, 1e0), case id when 2 then 1.5 else 1e0 end from p order by id
-- @case
select coalesce(1.5, 'x'), iif(id = 1, 1.5, 'x'), case id when 2 then 1.5 else 'x' end from p order by id
-- @case
select coalesce(1.5, ''), iif(id = 1, 1.5, ''), case id when 2 then 1.5 else '' end from p order by id
-- @case
select coalesce(1e0, i), iif(id = 1, 1e0, i), case id when 2 then 1e0 else i end from p order by id
-- @case
select coalesce(1e0, n), iif(id = 1, 1e0, n), case id when 2 then 1e0 else n end from p order by id
-- @case
select coalesce(1e0, d), iif(id = 1, 1e0, d), case id when 2 then 1e0 else d end from p order by id
-- @case
select coalesce(1e0, s), iif(id = 1, 1e0, s), case id when 2 then 1e0 else s end from p order by id
-- @case
select coalesce(1e0, c), iif(id = 1, 1e0, c), case id when 2 then 1e0 else c end from p order by id
-- @case
select coalesce(1e0, dt), iif(id = 1, 1e0, dt), case id when 2 then 1e0 else dt end from p order by id
-- @case
select coalesce(1e0, ts), iif(id = 1, 1e0, ts), case id when 2 then 1e0 else ts end from p order by id
-- @case
select coalesce(1e0, t), iif(id = 1, 1e0, t), case id when 2 then 1e0 else t end from p order by id
-- @case
select coalesce(1e0, b), iif(id = 1, 1e0, b), case id when 2 then 1e0 else b end from p order by id
-- @case
select coalesce(1e0, bl), iif(id = 1, 1e0, bl), case id when 2 then 1e0 else bl end from p order by id
-- @case
select coalesce(1e0, bi), iif(id = 1, 1e0, bi), case id when 2 then 1e0 else bi end from p order by id
-- @case
select coalesce(1e0, sm), iif(id = 1, 1e0, sm), case id when 2 then 1e0 else sm end from p order by id
-- @case
select coalesce(1e0, null), iif(id = 1, 1e0, null), case id when 2 then 1e0 else null end from p order by id
-- @case
select coalesce(1e0, 1), iif(id = 1, 1e0, 1), case id when 2 then 1e0 else 1 end from p order by id
-- @case
select coalesce(1e0, 1.5), iif(id = 1, 1e0, 1.5), case id when 2 then 1e0 else 1.5 end from p order by id
-- @case
select coalesce(1e0, 1e0), iif(id = 1, 1e0, 1e0), case id when 2 then 1e0 else 1e0 end from p order by id
-- @case
select coalesce(1e0, 'x'), iif(id = 1, 1e0, 'x'), case id when 2 then 1e0 else 'x' end from p order by id
-- @case
select coalesce(1e0, ''), iif(id = 1, 1e0, ''), case id when 2 then 1e0 else '' end from p order by id
-- @case
select coalesce('x', i), iif(id = 1, 'x', i), case id when 2 then 'x' else i end from p order by id
-- @case
select coalesce('x', n), iif(id = 1, 'x', n), case id when 2 then 'x' else n end from p order by id
-- @case
select coalesce('x', d), iif(id = 1, 'x', d), case id when 2 then 'x' else d end from p order by id
-- @case
select coalesce('x', s), iif(id = 1, 'x', s), case id when 2 then 'x' else s end from p order by id
-- @case
select coalesce('x', c), iif(id = 1, 'x', c), case id when 2 then 'x' else c end from p order by id
-- @case
select coalesce('x', dt), iif(id = 1, 'x', dt), case id when 2 then 'x' else dt end from p order by id
-- @case
select coalesce('x', ts), iif(id = 1, 'x', ts), case id when 2 then 'x' else ts end from p order by id
-- @case
select coalesce('x', t), iif(id = 1, 'x', t), case id when 2 then 'x' else t end from p order by id
-- @case
select coalesce('x', b), iif(id = 1, 'x', b), case id when 2 then 'x' else b end from p order by id
-- @case
select coalesce('x', bl), iif(id = 1, 'x', bl), case id when 2 then 'x' else bl end from p order by id
-- @case
select coalesce('x', bi), iif(id = 1, 'x', bi), case id when 2 then 'x' else bi end from p order by id
-- @case
select coalesce('x', sm), iif(id = 1, 'x', sm), case id when 2 then 'x' else sm end from p order by id
-- @case
select coalesce('x', null), iif(id = 1, 'x', null), case id when 2 then 'x' else null end from p order by id
-- @case
select coalesce('x', 1), iif(id = 1, 'x', 1), case id when 2 then 'x' else 1 end from p order by id
-- @case
select coalesce('x', 1.5), iif(id = 1, 'x', 1.5), case id when 2 then 'x' else 1.5 end from p order by id
-- @case
select coalesce('x', 1e0), iif(id = 1, 'x', 1e0), case id when 2 then 'x' else 1e0 end from p order by id
-- @case
select coalesce('x', 'x'), iif(id = 1, 'x', 'x'), case id when 2 then 'x' else 'x' end from p order by id
-- @case
select coalesce('x', ''), iif(id = 1, 'x', ''), case id when 2 then 'x' else '' end from p order by id
-- @case
select coalesce('', i), iif(id = 1, '', i), case id when 2 then '' else i end from p order by id
-- @case
select coalesce('', n), iif(id = 1, '', n), case id when 2 then '' else n end from p order by id
-- @case
select coalesce('', d), iif(id = 1, '', d), case id when 2 then '' else d end from p order by id
-- @case
select coalesce('', s), iif(id = 1, '', s), case id when 2 then '' else s end from p order by id
-- @case
select coalesce('', c), iif(id = 1, '', c), case id when 2 then '' else c end from p order by id
-- @case
select coalesce('', dt), iif(id = 1, '', dt), case id when 2 then '' else dt end from p order by id
-- @case
select coalesce('', ts), iif(id = 1, '', ts), case id when 2 then '' else ts end from p order by id
-- @case
select coalesce('', t), iif(id = 1, '', t), case id when 2 then '' else t end from p order by id
-- @case
select coalesce('', b), iif(id = 1, '', b), case id when 2 then '' else b end from p order by id
-- @case
select coalesce('', bl), iif(id = 1, '', bl), case id when 2 then '' else bl end from p order by id
-- @case
select coalesce('', bi), iif(id = 1, '', bi), case id when 2 then '' else bi end from p order by id
-- @case
select coalesce('', sm), iif(id = 1, '', sm), case id when 2 then '' else sm end from p order by id
-- @case
select coalesce('', null), iif(id = 1, '', null), case id when 2 then '' else null end from p order by id
-- @case
select coalesce('', 1), iif(id = 1, '', 1), case id when 2 then '' else 1 end from p order by id
-- @case
select coalesce('', 1.5), iif(id = 1, '', 1.5), case id when 2 then '' else 1.5 end from p order by id
-- @case
select coalesce('', 1e0), iif(id = 1, '', 1e0), case id when 2 then '' else 1e0 end from p order by id
-- @case
select coalesce('', 'x'), iif(id = 1, '', 'x'), case id when 2 then '' else 'x' end from p order by id
-- @case
select coalesce('', ''), iif(id = 1, '', ''), case id when 2 then '' else '' end from p order by id
-- @case
select nullif(i, i), decode(id, 1, i, i), maxvalue(i, i), minvalue(i, i) from p order by id
-- @case
select nullif(i, n), decode(id, 1, i, n), maxvalue(i, n), minvalue(i, n) from p order by id
-- @case
select nullif(i, s), decode(id, 1, i, s), maxvalue(i, s), minvalue(i, s) from p order by id
-- @case
select nullif(i, dt), decode(id, 1, i, dt), maxvalue(i, dt), minvalue(i, dt) from p order by id
-- @case
select nullif(i, b), decode(id, 1, i, b), maxvalue(i, b), minvalue(i, b) from p order by id
-- @case
select nullif(i, null), decode(id, 1, i, null), maxvalue(i, null), minvalue(i, null) from p order by id
-- @case
select nullif(n, i), decode(id, 1, n, i), maxvalue(n, i), minvalue(n, i) from p order by id
-- @case
select nullif(n, n), decode(id, 1, n, n), maxvalue(n, n), minvalue(n, n) from p order by id
-- @case
select nullif(n, s), decode(id, 1, n, s), maxvalue(n, s), minvalue(n, s) from p order by id
-- @case
select nullif(n, dt), decode(id, 1, n, dt), maxvalue(n, dt), minvalue(n, dt) from p order by id
-- @case
select nullif(n, b), decode(id, 1, n, b), maxvalue(n, b), minvalue(n, b) from p order by id
-- @case
select nullif(n, null), decode(id, 1, n, null), maxvalue(n, null), minvalue(n, null) from p order by id
-- @case
select nullif(s, i), decode(id, 1, s, i), maxvalue(s, i), minvalue(s, i) from p order by id
-- @case
select nullif(s, n), decode(id, 1, s, n), maxvalue(s, n), minvalue(s, n) from p order by id
-- @case
select nullif(s, s), decode(id, 1, s, s), maxvalue(s, s), minvalue(s, s) from p order by id
-- @case
select nullif(s, dt), decode(id, 1, s, dt), maxvalue(s, dt), minvalue(s, dt) from p order by id
-- @case
select nullif(s, b), decode(id, 1, s, b), maxvalue(s, b), minvalue(s, b) from p order by id
-- @case
select nullif(s, null), decode(id, 1, s, null), maxvalue(s, null), minvalue(s, null) from p order by id
-- @case
select nullif(dt, i), decode(id, 1, dt, i), maxvalue(dt, i), minvalue(dt, i) from p order by id
-- @case
select nullif(dt, n), decode(id, 1, dt, n), maxvalue(dt, n), minvalue(dt, n) from p order by id
-- @case
select nullif(dt, s), decode(id, 1, dt, s), maxvalue(dt, s), minvalue(dt, s) from p order by id
-- @case
select nullif(dt, dt), decode(id, 1, dt, dt), maxvalue(dt, dt), minvalue(dt, dt) from p order by id
-- @case
select nullif(dt, b), decode(id, 1, dt, b), maxvalue(dt, b), minvalue(dt, b) from p order by id
-- @case
select nullif(dt, null), decode(id, 1, dt, null), maxvalue(dt, null), minvalue(dt, null) from p order by id
-- @case
select nullif(b, i), decode(id, 1, b, i), maxvalue(b, i), minvalue(b, i) from p order by id
-- @case
select nullif(b, n), decode(id, 1, b, n), maxvalue(b, n), minvalue(b, n) from p order by id
-- @case
select nullif(b, s), decode(id, 1, b, s), maxvalue(b, s), minvalue(b, s) from p order by id
-- @case
select nullif(b, dt), decode(id, 1, b, dt), maxvalue(b, dt), minvalue(b, dt) from p order by id
-- @case
select nullif(b, b), decode(id, 1, b, b), maxvalue(b, b), minvalue(b, b) from p order by id
-- @case
select nullif(b, null), decode(id, 1, b, null), maxvalue(b, null), minvalue(b, null) from p order by id
-- @case
select nullif(null, i), decode(id, 1, null, i), maxvalue(null, i), minvalue(null, i) from p order by id
-- @case
select nullif(null, n), decode(id, 1, null, n), maxvalue(null, n), minvalue(null, n) from p order by id
-- @case
select nullif(null, s), decode(id, 1, null, s), maxvalue(null, s), minvalue(null, s) from p order by id
-- @case
select nullif(null, dt), decode(id, 1, null, dt), maxvalue(null, dt), minvalue(null, dt) from p order by id
-- @case
select nullif(null, b), decode(id, 1, null, b), maxvalue(null, b), minvalue(null, b) from p order by id
-- @case
select nullif(null, null), decode(id, 1, null, null), maxvalue(null, null), minvalue(null, null) from p order by id
-- @case
select case when b then 'yes' when not b then 'no' end, case b when true then 1 when false then 0 else -1 end from p order by id
-- @case
select case i when 1 then 'one' when null then 'null' else 'other' end, case when i is null then 'n' end from p order by id
-- @case
select case 1 when 1.0 then 'a' end, case 'a' when 'a ' then 'pad' end, case date '2024-01-01' when '2024-01-01' then 'd' end from rdb$database
-- @case
select case when 1 = 1 then 1 when 1 = 1 then 1.5 else 2e0 end, case when 1 = 0 then 'x' else 12345 end from rdb$database
-- @case
select case when 1 = 1 then 'a' else 12345 end, case when 1 = 1 then date '2024-01-01' else 'x' end from rdb$database
-- @case
select case when 1 = 1 then date '2024-01-01' else timestamp '2024-01-01 00:00:00' end from rdb$database
-- @case
select case when 1 = 1 then 1 else true end from rdb$database
-- @case
select case when 1 = 1 then 1 else date '2024-01-01' end from rdb$database
-- @case
select coalesce(null, null) from rdb$database
-- @case
select coalesce(null, null, 1), coalesce(1, 'a'), coalesce('a', 1), coalesce(1.5, 2), coalesce(2, 1.555) from rdb$database
-- @case
select iif(null, 1, 2), iif(1 = 1, null, 2), iif(1 = 0, 1, null), iif(true, 'abc', 'de') from rdb$database
-- @case
select iif(1, 1, 2) from rdb$database
-- @case
select nullif(null, 1), nullif(1, null), nullif('a', 'a '), nullif(1, 1.0), nullif(1e0, 1) from rdb$database
-- @case
select decode(null, null, 'match', 'nomatch'), decode(1, 1, 'a'), decode(2, 1, 'a'), decode('x', 'x', 1, 'y', 2.5, 3e0) from rdb$database
-- @case
select maxvalue(1, null), minvalue(null, 1), maxvalue('a', 'B'), maxvalue(1, 2.5, 3e0), minvalue(date '2024-01-01', '2023-01-01') from rdb$database
-- @case
select exists(select 1 from e), exists(select 1 from p), exists(select null from p where 1 = 0), not exists(select * from e) from rdb$database
-- @case
select singular(select 1 from p where id = 1), singular(select 1 from p), singular(select 1 from e), singular(select null from p where id < 3) from rdb$database
-- @case
select 1 from rdb$database where null
-- @case
select 1 from rdb$database where not null
-- @case
select count(*) from p where b
-- @case
select count(*) from p where not b
-- @case
select count(*) from p where b is not true
-- @case
select count(*) from p where i in (1, 2, null)
-- @case
select count(*) from p where i not in (1, null)
-- @case
select count(*) from p where s like 'a%' or s is null
-- @case
select count(*) from p where (i > 1) is unknown
-- @case
select b = 'true', b = 'TRUE', b = 'yes', b = 1 from p where id = 1
-- @case
select i = '1', i = ' 1 ', i = '1.0', i = 'x', n = '1.5', d = '1.5e0' from p where id = 1
-- @case
select dt = '2024-01-01', dt = '1.1.2024', dt = 'x', ts = '2024-01-01 10:00', t = '10:00' from p where id = 1
-- @case
select s = 1, s > 0, c = 'a', c = 'a   ', c < 'a!' from p where id = 1
-- @case
select bl = 'x', bl like 'x%', bl containing 'X', bl starting 'x', bl is null from p where id = 1
-- @case
select bl = bl from p where id = 1
-- @case
select bl < 'y' from p where id = 1
-- @case
select dt = ts, dt < ts, cast(ts as date) = dt, t = cast(ts as time) from p order by id
-- @case
select dt = t from p
-- @case
select b = i from p
-- @case
select b > false, true > false, null > false from p where id = 1
-- @case
select 1 in (1.0), 1 in ('1'), '1' in (1), 1.5 in (1.50, 2), 'a' in ('a ', 'b') from rdb$database
-- @case
select 1 between 0.5 and 1.5, 'b' between 'a' and 'c', date '2024-06-01' between '2024-01-01' and '2024-12-31' from rdb$database
-- @case
select i from p where i = any (select i from p where i > 1) order by i
-- @case
select i from p where i > all (select i from p where i < 0) order by i
-- @case
select i from p where i in (select i from p where i is not null) order by i
-- @case
select i from p where (i, n) in (select 1, 1.5 from rdb$database)
-- @case
select 1 from rdb$database where 1 = (select 1 from p)
-- @case
select (select i from p where id = 4), (select i from p where id = 1) + 1 from rdb$database
