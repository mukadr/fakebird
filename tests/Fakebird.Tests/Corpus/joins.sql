-- Joins of every kind (inner, left, right, full, cross, natural, USING), the columns they describe (names, relations,
-- nullability of outer sides, USING/NATURAL merged columns, SELECT *), conditions in ON vs WHERE, ambiguity and
-- unknown-name errors, subqueries (scalar, correlated, IN/EXISTS/SINGULAR, derived tables, LATERAL-free), and UNION.
-- @setup
create table l (id integer not null primary key, k integer, s varchar(5), n numeric(5,2));
create table r (id integer not null primary key, k integer, t varchar(5), n numeric(9,3));
create table e (id integer, k integer);
create table m (id integer not null, k integer not null, x char(2));
insert into l values (1, 10, 'a', 1.5);
insert into l values (2, 20, 'b', null);
insert into l values (3, null, 'c', 2.25);
insert into l values (4, 20, null, 0);
insert into r values (1, 10, 'x', 1.5);
insert into r values (2, 30, 'y', 2);
insert into r values (5, 20, 'z', null);
insert into r values (6, null, null, 7.125);
insert into m values (1, 10, 'p');
insert into m values (2, 20, 'q');
insert into m values (3, 10, 'r');
-- @case
select l.id, r.id from l join r on l.k = r.k order by 1, 2
-- @case
select l.id, l.s, r.id, r.t from l join r on l.id = r.id order by l.id, r.id
-- @case
select * from l join r on l.id = r.id and r.t <> 'y' order by l.id, r.id
-- @case
select l.*, r.k from l join r on l.k = r.k and l.id < r.id order by l.id, r.id
-- @case
select count(*), count(l.id), count(r.id) from l join r on l.k = r.k
-- @case
select l.id, r.id from l join r on 1 = 1 order by 1, 2
-- @case
select l.id, r.id from l join r on 1 = 0 order by 1, 2
-- @case
select l.id, r.id from l join r on l.k = r.k where r.id is null order by 1, 2
-- @case
select l.id, r.id from l join r on l.k = r.k where l.id > 1 order by 1, 2
-- @case
select l.id, e.id from l join e on l.id = e.id order by 1, 2
-- @case
select * from l join r using (id) order by 1
-- @case
select * from l join r using (id, k) order by 1
-- @case
select id, k, s, t from l join r using (id) order by 1
-- @case
select l.id, r.id, id from l join r using (id) order by 1, 2
-- @case
select * from l natural join r order by 1
-- @case
select id from l natural join m order by 1
-- @case
select l.id, r.id from l inner join r on l.k = r.k order by 1, 2
-- @case
select l.id, l.s, r.id, r.t from l inner join r on l.id = r.id order by l.id, r.id
-- @case
select * from l inner join r on l.id = r.id and r.t <> 'y' order by l.id, r.id
-- @case
select l.*, r.k from l inner join r on l.k = r.k and l.id < r.id order by l.id, r.id
-- @case
select count(*), count(l.id), count(r.id) from l inner join r on l.k = r.k
-- @case
select l.id, r.id from l inner join r on 1 = 1 order by 1, 2
-- @case
select l.id, r.id from l inner join r on 1 = 0 order by 1, 2
-- @case
select l.id, r.id from l inner join r on l.k = r.k where r.id is null order by 1, 2
-- @case
select l.id, r.id from l inner join r on l.k = r.k where l.id > 1 order by 1, 2
-- @case
select l.id, e.id from l inner join e on l.id = e.id order by 1, 2
-- @case
select * from l inner join r using (id) order by 1
-- @case
select * from l inner join r using (id, k) order by 1
-- @case
select id, k, s, t from l inner join r using (id) order by 1
-- @case
select l.id, r.id, id from l inner join r using (id) order by 1, 2
-- @case
select * from l natural inner join r order by 1
-- @case
select id from l natural inner join m order by 1
-- @case
select l.id, r.id from l left join r on l.k = r.k order by 1, 2
-- @case
select l.id, l.s, r.id, r.t from l left join r on l.id = r.id order by l.id, r.id
-- @case
select * from l left join r on l.id = r.id and r.t <> 'y' order by l.id, r.id
-- @case
select l.*, r.k from l left join r on l.k = r.k and l.id < r.id order by l.id, r.id
-- @case
select count(*), count(l.id), count(r.id) from l left join r on l.k = r.k
-- @case
select l.id, r.id from l left join r on 1 = 1 order by 1, 2
-- @case
select l.id, r.id from l left join r on 1 = 0 order by 1, 2
-- @case
select l.id, r.id from l left join r on l.k = r.k where r.id is null order by 1, 2
-- @case
select l.id, r.id from l left join r on l.k = r.k where l.id > 1 order by 1, 2
-- @case
select l.id, e.id from l left join e on l.id = e.id order by 1, 2
-- @case
select * from l left join r using (id) order by 1
-- @case
select * from l left join r using (id, k) order by 1
-- @case
select id, k, s, t from l left join r using (id) order by 1
-- @case
select l.id, r.id, id from l left join r using (id) order by 1, 2
-- @case
select * from l natural left join r order by 1
-- @case
select id from l natural left join m order by 1
-- @case
select l.id, r.id from l left outer join r on l.k = r.k order by 1, 2
-- @case
select l.id, l.s, r.id, r.t from l left outer join r on l.id = r.id order by l.id, r.id
-- @case
select * from l left outer join r on l.id = r.id and r.t <> 'y' order by l.id, r.id
-- @case
select l.*, r.k from l left outer join r on l.k = r.k and l.id < r.id order by l.id, r.id
-- @case
select count(*), count(l.id), count(r.id) from l left outer join r on l.k = r.k
-- @case
select l.id, r.id from l left outer join r on 1 = 1 order by 1, 2
-- @case
select l.id, r.id from l left outer join r on 1 = 0 order by 1, 2
-- @case
select l.id, r.id from l left outer join r on l.k = r.k where r.id is null order by 1, 2
-- @case
select l.id, r.id from l left outer join r on l.k = r.k where l.id > 1 order by 1, 2
-- @case
select l.id, e.id from l left outer join e on l.id = e.id order by 1, 2
-- @case
select * from l left outer join r using (id) order by 1
-- @case
select * from l left outer join r using (id, k) order by 1
-- @case
select id, k, s, t from l left outer join r using (id) order by 1
-- @case
select l.id, r.id, id from l left outer join r using (id) order by 1, 2
-- @case
select * from l natural left outer join r order by 1
-- @case
select id from l natural left outer join m order by 1
-- @case
select l.id, r.id from l right join r on l.k = r.k order by 1, 2
-- @case
select l.id, l.s, r.id, r.t from l right join r on l.id = r.id order by l.id, r.id
-- @case
select * from l right join r on l.id = r.id and r.t <> 'y' order by l.id, r.id
-- @case
select l.*, r.k from l right join r on l.k = r.k and l.id < r.id order by l.id, r.id
-- @case
select count(*), count(l.id), count(r.id) from l right join r on l.k = r.k
-- @case
select l.id, r.id from l right join r on 1 = 1 order by 1, 2
-- @case
select l.id, r.id from l right join r on 1 = 0 order by 1, 2
-- @case
select l.id, r.id from l right join r on l.k = r.k where r.id is null order by 1, 2
-- @case
select l.id, r.id from l right join r on l.k = r.k where l.id > 1 order by 1, 2
-- @case
select l.id, e.id from l right join e on l.id = e.id order by 1, 2
-- @case
select * from l right join r using (id) order by 1
-- @case
select * from l right join r using (id, k) order by 1
-- @case
select id, k, s, t from l right join r using (id) order by 1
-- @case
select l.id, r.id, id from l right join r using (id) order by 1, 2
-- @case
select * from l natural right join r order by 1
-- @case
select id from l natural right join m order by 1
-- @case
select l.id, r.id from l right outer join r on l.k = r.k order by 1, 2
-- @case
select l.id, l.s, r.id, r.t from l right outer join r on l.id = r.id order by l.id, r.id
-- @case
select * from l right outer join r on l.id = r.id and r.t <> 'y' order by l.id, r.id
-- @case
select l.*, r.k from l right outer join r on l.k = r.k and l.id < r.id order by l.id, r.id
-- @case
select count(*), count(l.id), count(r.id) from l right outer join r on l.k = r.k
-- @case
select l.id, r.id from l right outer join r on 1 = 1 order by 1, 2
-- @case
select l.id, r.id from l right outer join r on 1 = 0 order by 1, 2
-- @case
select l.id, r.id from l right outer join r on l.k = r.k where r.id is null order by 1, 2
-- @case
select l.id, r.id from l right outer join r on l.k = r.k where l.id > 1 order by 1, 2
-- @case
select l.id, e.id from l right outer join e on l.id = e.id order by 1, 2
-- @case
select * from l right outer join r using (id) order by 1
-- @case
select * from l right outer join r using (id, k) order by 1
-- @case
select id, k, s, t from l right outer join r using (id) order by 1
-- @case
select l.id, r.id, id from l right outer join r using (id) order by 1, 2
-- @case
select * from l natural right outer join r order by 1
-- @case
select id from l natural right outer join m order by 1
-- @case
select l.id, r.id from l full join r on l.k = r.k order by 1, 2
-- @case
select l.id, l.s, r.id, r.t from l full join r on l.id = r.id order by l.id, r.id
-- @case
select * from l full join r on l.id = r.id and r.t <> 'y' order by l.id, r.id
-- @case
select l.*, r.k from l full join r on l.k = r.k and l.id < r.id order by l.id, r.id
-- @case
select count(*), count(l.id), count(r.id) from l full join r on l.k = r.k
-- @case
select l.id, r.id from l full join r on 1 = 1 order by 1, 2
-- @case
select l.id, r.id from l full join r on 1 = 0 order by 1, 2
-- @case
select l.id, r.id from l full join r on l.k = r.k where r.id is null order by 1, 2
-- @case
select l.id, r.id from l full join r on l.k = r.k where l.id > 1 order by 1, 2
-- @case
select l.id, e.id from l full join e on l.id = e.id order by 1, 2
-- @case
select * from l full join r using (id) order by 1
-- @case
select * from l full join r using (id, k) order by 1
-- @case
select id, k, s, t from l full join r using (id) order by 1
-- @case
select l.id, r.id, id from l full join r using (id) order by 1, 2
-- @case
select * from l natural full join r order by 1
-- @case
select id from l natural full join m order by 1
-- @case
select l.id, r.id from l full outer join r on l.k = r.k order by 1, 2
-- @case
select l.id, l.s, r.id, r.t from l full outer join r on l.id = r.id order by l.id, r.id
-- @case
select * from l full outer join r on l.id = r.id and r.t <> 'y' order by l.id, r.id
-- @case
select l.*, r.k from l full outer join r on l.k = r.k and l.id < r.id order by l.id, r.id
-- @case
select count(*), count(l.id), count(r.id) from l full outer join r on l.k = r.k
-- @case
select l.id, r.id from l full outer join r on 1 = 1 order by 1, 2
-- @case
select l.id, r.id from l full outer join r on 1 = 0 order by 1, 2
-- @case
select l.id, r.id from l full outer join r on l.k = r.k where r.id is null order by 1, 2
-- @case
select l.id, r.id from l full outer join r on l.k = r.k where l.id > 1 order by 1, 2
-- @case
select l.id, e.id from l full outer join e on l.id = e.id order by 1, 2
-- @case
select * from l full outer join r using (id) order by 1
-- @case
select * from l full outer join r using (id, k) order by 1
-- @case
select id, k, s, t from l full outer join r using (id) order by 1
-- @case
select l.id, r.id, id from l full outer join r using (id) order by 1, 2
-- @case
select * from l natural full outer join r order by 1
-- @case
select id from l natural full outer join m order by 1
-- @case
select * from l cross join r order by l.id, r.id
-- @case
select l.id, r.id from l, r where l.k = r.k order by 1, 2
-- @case
select l.id, r.id from l, r where l.k = r.k or l.id = r.id order by 1, 2
-- @case
select l.id, r.id from l, r where l.k > r.k order by 1, 2
-- @case
select a.id, b.id from l a join l b on a.k = b.k and a.id < b.id order by 1, 2
-- @case
select a.id, b.id, c.id from l a join r b on a.id = b.id join m c on c.id = a.id order by 1
-- @case
select a.id, b.id, c.id from l a left join r b on a.id = b.id left join m c on c.id = b.id order by 1
-- @case
select a.id, b.id, c.id from l a left join r b on a.id = b.id join m c on c.id = a.id order by 1
-- @case
select a.id, b.id, c.id from l a join r b on a.id = b.id right join m c on c.k = a.k order by 3, 1
-- @case
select a.id, b.id, c.id from l a full join r b on a.id = b.id full join m c on c.id = coalesce(a.id, b.id) order by 1, 2, 3
-- @case
select a.id, b.id, c.id from l a left join (r b join m c on c.id = b.id) on a.id = b.id order by 1
-- @case
select a.id, b.id from l a left join r b on a.k = b.k and b.t = 'z' order by 1, 2
-- @case
select a.id, b.id from l a left join r b on a.k = b.k where b.t = 'z' order by 1, 2
-- @case
select a.id, b.id from l a left join r b on a.k = b.k where b.t = 'z' or b.t is null order by 1, 2
-- @case
select a.id, b.id from l a left join r b on a.k = b.k and a.s = 'b' order by 1, 2
-- @case
select a.id, b.id from l a left join r b on b.k = 20 order by 1, 2
-- @case
select a.id, coalesce(b.t, '-') from l a left join r b on a.id = b.id order by 1
-- @case
select a.id, b.n + a.n from l a full join r b on a.id = b.id order by a.id, b.id
-- @case
select a.k, count(b.id) from l a left join r b on a.k = b.k group by a.k order by 1
-- @case
select b.k, sum(a.n) from l a right join r b on a.k = b.k group by b.k order by 1
-- @case
select x.id, x.cnt from (select l.id, count(r.id) cnt from l left join r on r.k = l.k group by l.id) x order by 1
-- @case
select a.id, b.id from l a join (select id, k from r where k is not null) b on a.k = b.k order by 1, 2
-- @case
select a.id, b.id from l a left join (select first 1 id, k from r order by id) b on a.k = b.k order by 1, 2
-- @case
select a.id, b.mx from l a left join (select k, max(id) mx from r group by k) b on b.k = a.k order by 1
-- @case
select a.id, b.id from l a join r b on a.id = b.id join l c on c.id = b.id order by 1
-- @case
select id from l join r on l.id = r.id
-- @case
select k from l join r using (id)
-- @case
select l.k, r.k from l join r using (k) order by 1, 2
-- @case
select k from l join r using (k) order by 1
-- @case
select l.id from l natural join r
-- @case
select * from l join r using (s)
-- @case
select * from l join r using (x)
-- @case
select * from l join r using (id, id)
-- @case
select * from l natural join e order by 1
-- @case
select * from e natural join e order by 1
-- @case
select * from l a join l a on a.id = a.id
-- @case
select * from l join l on l.id = l.id
-- @case
select l.id from l a
-- @case
select a.id from l a join r a on 1 = 1
-- @case
select x.id from l join r on x.id = l.id
-- @case
select l.id from l join r on l.zz = r.id
-- @case
select l.id from l join r on l.k = r.k join m on m.k = q.k
-- @case
select l.id from l left join r on r.id = m.id join m on m.id = l.id
-- @case
select 1 from l join r
-- @case
select 1 from l join r on l.k
-- @case
select 1 from l join r on (select 1 from rdb$database) = 1 order by 1 rows 1
-- @case
select l.id, r.id from l join r on l.id = r.id and exists (select 1 from m where m.id = r.id) order by 1
-- @case
select l.id from l where exists (select 1 from r where r.k = l.k) order by 1
-- @case
select l.id from l where not exists (select 1 from r where r.k = l.k) order by 1
-- @case
select l.id from l where l.k in (select k from r) order by 1
-- @case
select l.id from l where l.k not in (select k from r) order by 1
-- @case
select l.id from l where l.k not in (select k from r where k is not null) order by 1
-- @case
select l.id from l where singular (select 1 from r where r.k = l.k) order by 1
-- @case
select l.id from l where not singular (select 1 from m where m.k = l.k) order by 1
-- @case
select l.id, (select t from r where r.id = l.id) from l order by 1
-- @case
select l.id, (select t from r where r.k = l.k) from l order by 1
-- @case
select l.id, (select count(*) from r where r.k = l.k), (select max(r.n) from r where r.k = l.k) from l order by 1
-- @case
select l.id, (select first 1 r.t from r where r.k >= l.k order by r.t desc) from l order by 1
-- @case
select l.id, (select r.t from r where r.id = l.id + 0) || '!' from l order by 1
-- @case
select (select id from r) from rdb$database
-- @case
select (select id, k from r where id = 1) from rdb$database
-- @case
select l.id from l where l.k = (select k from r where r.id = 1) order by 1
-- @case
select l.id from l where l.k = (select k from r) order by 1
-- @case
select l.id from l where l.k = (select k from r where r.id = l.id) order by 1
-- @case
select l.id from l where l.k > all (select k from r where r.k is not null) order by 1
-- @case
select l.id from l where l.k > any (select k from r) order by 1
-- @case
select l.id from l where (select count(*) from r where r.k = l.k) > 0 order by 1
-- @case
select id, (select count(*) from l l2 where l2.id <= l.id) from l order by 1
-- @case
select id from l where id = (select max(id) from l)
-- @case
select id, (select max(id) from l l2 where l2.k = l.k and l2.id <> l.id) from l order by 1
-- @case
select * from (select id, k from l) x join (select id, t from r) y on x.id = y.id order by 1
-- @case
select * from (select id, k from l) x (a, b) order by 1
-- @case
select * from (select id, k from l) x (a) order by 1
-- @case
select x.* from (select l.id, r.id from l join r on l.id = r.id) x
-- @case
select x.* from (select l.id, r.id rid from l join r on l.id = r.id) x order by 1
-- @case
select * from (select * from (select id from l) a) b order by 1
-- @case
select a.id from (select id from l) a where a.id in (select id from (select id from r) b) order by 1
-- @case
select x.c from (select count(*) c from l) x
-- @case
select * from (select id from l union select id from r) u order by 1
-- @case
select * from (select id from l union all select id from r) u order by 1
-- @case
select id from l union select id from r order by 1
-- @case
select id from l union all select id from r order by 1
-- @case
select id, s from l union select id, t from r order by 1, 2
-- @case
select k from l union select k from r order by 1
-- @case
select k from l union all select k from r order by 1 nulls last
-- @case
select n from l union select n from r order by 1
-- @case
select s from l union select t from r union select x from m order by 1
-- @case
select id from l union all select id from r union select id from m order by 1
-- @case
select id from l union select id from r union all select id from m order by 1
-- @case
select 1 from l union select 1.5 from r order by 1
-- @case
select 1, 'a' from rdb$database union select 2.5, 'bcd' from rdb$database union select 1e0, null from rdb$database order by 1
-- @case
select null from rdb$database union select 1 from rdb$database order by 1
-- @case
select id from l union select s from l order by 1
-- @case
select id from l union select id, k from r
-- @case
select dt from (select current_date dt from rdb$database) union select 'x' from rdb$database
-- @case
select first 1 id from l union select first 1 id from r order by 1
-- @case
select count(*) from (select k from l union select k from r)
-- @case
select count(*) from (select k from l union all select k from r)
-- @case
select k, count(*) from (select k from l union all select k from r) group by k order by 1
-- @case
select id from l where id in (select id from r union select id from m) order by 1
-- @case
select (select max(id) from (select id from l union select id from r)) from rdb$database
-- @case
with c as (select id, k from l) select c.id, r.id from c left join r on r.k = c.k order by 1, 2
-- @case
with c as (select id from l), d as (select id from c where id > 1) select * from d order by 1
-- @case
with c (a, b) as (select id, k from l) select a, b from c order by 1
-- @case
with c as (select id from l) select * from c a join c b on a.id = b.id order by 1
-- @case
with recursive n (i) as (select 1 from rdb$database union all select i + 1 from n where i < 5) select i from n
-- @case
with recursive n (i) as (select 1 from rdb$database union all select i + 1 from n where i < 5) select sum(i), count(*) from n
-- @case
with recursive t (id, depth) as (select id, 0 from m where id = 1 union all select m.id, t.depth + 1 from m join t on m.k = t.id * 10 where t.depth < 3) select * from t order by 1, 2
-- @case
with c as (select id from l) select * from c where id in (select id from c where id < 3) order by 1
-- @case
with c as (select id from l) select id from l where id not in (select id from c) order by 1
-- @case
with c as (select id from x) select * from c
-- @case
with c as (select id from l), c as (select id from r) select * from c
