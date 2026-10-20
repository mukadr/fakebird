-- Window functions: ranking (ROW_NUMBER/RANK/DENSE_RANK/PERCENT_RANK/CUME_DIST/NTILE), navigation (LAG/LEAD/
-- FIRST_VALUE/LAST_VALUE/NTH_VALUE), aggregates over partitions with and without ORDER BY (the default frame),
-- explicit ROWS/RANGE frames, ties, NULLs, result types, and the errors.
-- @setup
create table w (id integer not null primary key, g varchar(2), v integer, n numeric(9,2), d double precision, s varchar(5));
insert into w values (1, 'a', 10, 1.50, 1e0, 'x');
insert into w values (2, 'a', 20, 2.25, 2e0, 'y');
insert into w values (3, 'a', 20, null, 3e0, null);
insert into w values (4, 'b', 5, 0.10, null, 'z');
insert into w values (5, 'b', null, -1.00, 5e0, 'x');
insert into w values (6, 'c', 7, 3.33, 6e0, 'w');
insert into w values (7, null, 1, 9.99, 7e0, 'v');
-- @case
select id, row_number() over () from w order by id
-- @case
select id, row_number() over (order by v) from w order by id
-- @case
select id, row_number() over (order by v desc nulls last, id) from w order by id
-- @case
select id, row_number() over (partition by g) from w order by id
-- @case
select id, row_number() over (partition by g order by v, id) from w order by id
-- @case
select id, row_number() over (partition by g order by id desc) from w order by id
-- @case
select id, row_number() over (order by id) from w order by id
-- @case
select id, rank() over () from w order by id
-- @case
select id, rank() over (order by v) from w order by id
-- @case
select id, rank() over (order by v desc nulls last, id) from w order by id
-- @case
select id, rank() over (partition by g) from w order by id
-- @case
select id, rank() over (partition by g order by v, id) from w order by id
-- @case
select id, rank() over (partition by g order by id desc) from w order by id
-- @case
select id, rank() over (order by id) from w order by id
-- @case
select id, dense_rank() over () from w order by id
-- @case
select id, dense_rank() over (order by v) from w order by id
-- @case
select id, dense_rank() over (order by v desc nulls last, id) from w order by id
-- @case
select id, dense_rank() over (partition by g) from w order by id
-- @case
select id, dense_rank() over (partition by g order by v, id) from w order by id
-- @case
select id, dense_rank() over (partition by g order by id desc) from w order by id
-- @case
select id, dense_rank() over (order by id) from w order by id
-- @case
select id, percent_rank() over () from w order by id
-- @case
select id, percent_rank() over (order by v) from w order by id
-- @case
select id, percent_rank() over (order by v desc nulls last, id) from w order by id
-- @case
select id, percent_rank() over (partition by g) from w order by id
-- @case
select id, percent_rank() over (partition by g order by v, id) from w order by id
-- @case
select id, percent_rank() over (partition by g order by id desc) from w order by id
-- @case
select id, percent_rank() over (order by id) from w order by id
-- @case
select id, cume_dist() over () from w order by id
-- @case
select id, cume_dist() over (order by v) from w order by id
-- @case
select id, cume_dist() over (order by v desc nulls last, id) from w order by id
-- @case
select id, cume_dist() over (partition by g) from w order by id
-- @case
select id, cume_dist() over (partition by g order by v, id) from w order by id
-- @case
select id, cume_dist() over (partition by g order by id desc) from w order by id
-- @case
select id, cume_dist() over (order by id) from w order by id
-- @case
select id, ntile(2) over () from w order by id
-- @case
select id, ntile(2) over (order by v) from w order by id
-- @case
select id, ntile(2) over (order by v desc nulls last, id) from w order by id
-- @case
select id, ntile(2) over (partition by g) from w order by id
-- @case
select id, ntile(2) over (partition by g order by v, id) from w order by id
-- @case
select id, ntile(2) over (partition by g order by id desc) from w order by id
-- @case
select id, ntile(2) over (order by id) from w order by id
-- @case
select id, ntile(4) over () from w order by id
-- @case
select id, ntile(4) over (order by v) from w order by id
-- @case
select id, ntile(4) over (order by v desc nulls last, id) from w order by id
-- @case
select id, ntile(4) over (partition by g) from w order by id
-- @case
select id, ntile(4) over (partition by g order by v, id) from w order by id
-- @case
select id, ntile(4) over (partition by g order by id desc) from w order by id
-- @case
select id, ntile(4) over (order by id) from w order by id
-- @case
select id, lag(v) over () from w order by id
-- @case
select id, lag(v) over (order by v) from w order by id
-- @case
select id, lag(v) over (order by v desc nulls last, id) from w order by id
-- @case
select id, lag(v) over (partition by g) from w order by id
-- @case
select id, lag(v) over (partition by g order by v, id) from w order by id
-- @case
select id, lag(v) over (partition by g order by id desc) from w order by id
-- @case
select id, lag(v) over (order by id) from w order by id
-- @case
select id, lead(v) over () from w order by id
-- @case
select id, lead(v) over (order by v) from w order by id
-- @case
select id, lead(v) over (order by v desc nulls last, id) from w order by id
-- @case
select id, lead(v) over (partition by g) from w order by id
-- @case
select id, lead(v) over (partition by g order by v, id) from w order by id
-- @case
select id, lead(v) over (partition by g order by id desc) from w order by id
-- @case
select id, lead(v) over (order by id) from w order by id
-- @case
select id, lag(v, 2) over () from w order by id
-- @case
select id, lag(v, 2) over (order by v) from w order by id
-- @case
select id, lag(v, 2) over (order by v desc nulls last, id) from w order by id
-- @case
select id, lag(v, 2) over (partition by g) from w order by id
-- @case
select id, lag(v, 2) over (partition by g order by v, id) from w order by id
-- @case
select id, lag(v, 2) over (partition by g order by id desc) from w order by id
-- @case
select id, lag(v, 2) over (order by id) from w order by id
-- @case
select id, lead(v, 2, -1) over () from w order by id
-- @case
select id, lead(v, 2, -1) over (order by v) from w order by id
-- @case
select id, lead(v, 2, -1) over (order by v desc nulls last, id) from w order by id
-- @case
select id, lead(v, 2, -1) over (partition by g) from w order by id
-- @case
select id, lead(v, 2, -1) over (partition by g order by v, id) from w order by id
-- @case
select id, lead(v, 2, -1) over (partition by g order by id desc) from w order by id
-- @case
select id, lead(v, 2, -1) over (order by id) from w order by id
-- @case
select id, lag(s, 1, 'none') over () from w order by id
-- @case
select id, lag(s, 1, 'none') over (order by v) from w order by id
-- @case
select id, lag(s, 1, 'none') over (order by v desc nulls last, id) from w order by id
-- @case
select id, lag(s, 1, 'none') over (partition by g) from w order by id
-- @case
select id, lag(s, 1, 'none') over (partition by g order by v, id) from w order by id
-- @case
select id, lag(s, 1, 'none') over (partition by g order by id desc) from w order by id
-- @case
select id, lag(s, 1, 'none') over (order by id) from w order by id
-- @case
select id, first_value(v) over () from w order by id
-- @case
select id, first_value(v) over (order by v) from w order by id
-- @case
select id, first_value(v) over (order by v desc nulls last, id) from w order by id
-- @case
select id, first_value(v) over (partition by g) from w order by id
-- @case
select id, first_value(v) over (partition by g order by v, id) from w order by id
-- @case
select id, first_value(v) over (partition by g order by id desc) from w order by id
-- @case
select id, first_value(v) over (order by id) from w order by id
-- @case
select id, last_value(v) over () from w order by id
-- @case
select id, last_value(v) over (order by v) from w order by id
-- @case
select id, last_value(v) over (order by v desc nulls last, id) from w order by id
-- @case
select id, last_value(v) over (partition by g) from w order by id
-- @case
select id, last_value(v) over (partition by g order by v, id) from w order by id
-- @case
select id, last_value(v) over (partition by g order by id desc) from w order by id
-- @case
select id, last_value(v) over (order by id) from w order by id
-- @case
select id, nth_value(v, 2) over () from w order by id
-- @case
select id, nth_value(v, 2) over (order by v) from w order by id
-- @case
select id, nth_value(v, 2) over (order by v desc nulls last, id) from w order by id
-- @case
select id, nth_value(v, 2) over (partition by g) from w order by id
-- @case
select id, nth_value(v, 2) over (partition by g order by v, id) from w order by id
-- @case
select id, nth_value(v, 2) over (partition by g order by id desc) from w order by id
-- @case
select id, nth_value(v, 2) over (order by id) from w order by id
-- @case
select id, nth_value(v, 2) from last over () from w order by id
-- @case
select id, nth_value(v, 2) from last over (order by v) from w order by id
-- @case
select id, nth_value(v, 2) from last over (order by v desc nulls last, id) from w order by id
-- @case
select id, nth_value(v, 2) from last over (partition by g) from w order by id
-- @case
select id, nth_value(v, 2) from last over (partition by g order by v, id) from w order by id
-- @case
select id, nth_value(v, 2) from last over (partition by g order by id desc) from w order by id
-- @case
select id, nth_value(v, 2) from last over (order by id) from w order by id
-- @case
select id, sum(v) over () from w order by id
-- @case
select id, sum(v) over (order by v) from w order by id
-- @case
select id, sum(v) over (order by v desc nulls last, id) from w order by id
-- @case
select id, sum(v) over (partition by g) from w order by id
-- @case
select id, sum(v) over (partition by g order by v, id) from w order by id
-- @case
select id, sum(v) over (partition by g order by id desc) from w order by id
-- @case
select id, sum(v) over (order by id) from w order by id
-- @case
select id, avg(v) over () from w order by id
-- @case
select id, avg(v) over (order by v) from w order by id
-- @case
select id, avg(v) over (order by v desc nulls last, id) from w order by id
-- @case
select id, avg(v) over (partition by g) from w order by id
-- @case
select id, avg(v) over (partition by g order by v, id) from w order by id
-- @case
select id, avg(v) over (partition by g order by id desc) from w order by id
-- @case
select id, avg(v) over (order by id) from w order by id
-- @case
select id, count(v) over () from w order by id
-- @case
select id, count(v) over (order by v) from w order by id
-- @case
select id, count(v) over (order by v desc nulls last, id) from w order by id
-- @case
select id, count(v) over (partition by g) from w order by id
-- @case
select id, count(v) over (partition by g order by v, id) from w order by id
-- @case
select id, count(v) over (partition by g order by id desc) from w order by id
-- @case
select id, count(v) over (order by id) from w order by id
-- @case
select id, count(*) over () from w order by id
-- @case
select id, count(*) over (order by v) from w order by id
-- @case
select id, count(*) over (order by v desc nulls last, id) from w order by id
-- @case
select id, count(*) over (partition by g) from w order by id
-- @case
select id, count(*) over (partition by g order by v, id) from w order by id
-- @case
select id, count(*) over (partition by g order by id desc) from w order by id
-- @case
select id, count(*) over (order by id) from w order by id
-- @case
select id, min(s) over () from w order by id
-- @case
select id, min(s) over (order by v) from w order by id
-- @case
select id, min(s) over (order by v desc nulls last, id) from w order by id
-- @case
select id, min(s) over (partition by g) from w order by id
-- @case
select id, min(s) over (partition by g order by v, id) from w order by id
-- @case
select id, min(s) over (partition by g order by id desc) from w order by id
-- @case
select id, min(s) over (order by id) from w order by id
-- @case
select id, max(n) over () from w order by id
-- @case
select id, max(n) over (order by v) from w order by id
-- @case
select id, max(n) over (order by v desc nulls last, id) from w order by id
-- @case
select id, max(n) over (partition by g) from w order by id
-- @case
select id, max(n) over (partition by g order by v, id) from w order by id
-- @case
select id, max(n) over (partition by g order by id desc) from w order by id
-- @case
select id, max(n) over (order by id) from w order by id
-- @case
select id, avg(n) over () from w order by id
-- @case
select id, avg(n) over (order by v) from w order by id
-- @case
select id, avg(n) over (order by v desc nulls last, id) from w order by id
-- @case
select id, avg(n) over (partition by g) from w order by id
-- @case
select id, avg(n) over (partition by g order by v, id) from w order by id
-- @case
select id, avg(n) over (partition by g order by id desc) from w order by id
-- @case
select id, avg(n) over (order by id) from w order by id
-- @case
select id, sum(d) over () from w order by id
-- @case
select id, sum(d) over (order by v) from w order by id
-- @case
select id, sum(d) over (order by v desc nulls last, id) from w order by id
-- @case
select id, sum(d) over (partition by g) from w order by id
-- @case
select id, sum(d) over (partition by g order by v, id) from w order by id
-- @case
select id, sum(d) over (partition by g order by id desc) from w order by id
-- @case
select id, sum(d) over (order by id) from w order by id
-- @case
select id, sum(v) over (order by v, id rows between unbounded preceding and current row) from w order by id
-- @case
select id, count(*) over (order by v, id rows between unbounded preceding and current row) from w order by id
-- @case
select id, first_value(v) over (order by v, id rows between unbounded preceding and current row) from w order by id
-- @case
select id, last_value(v) over (order by v, id rows between unbounded preceding and current row) from w order by id
-- @case
select id, min(v) over (order by v, id rows between unbounded preceding and current row) from w order by id
-- @case
select id, sum(v) over (partition by g order by v rows between unbounded preceding and current row) from w order by id
-- @case
select id, sum(v) over (order by v, id rows between 1 preceding and 1 following) from w order by id
-- @case
select id, count(*) over (order by v, id rows between 1 preceding and 1 following) from w order by id
-- @case
select id, first_value(v) over (order by v, id rows between 1 preceding and 1 following) from w order by id
-- @case
select id, last_value(v) over (order by v, id rows between 1 preceding and 1 following) from w order by id
-- @case
select id, min(v) over (order by v, id rows between 1 preceding and 1 following) from w order by id
-- @case
select id, sum(v) over (partition by g order by v rows between 1 preceding and 1 following) from w order by id
-- @case
select id, sum(v) over (order by v, id rows between current row and unbounded following) from w order by id
-- @case
select id, count(*) over (order by v, id rows between current row and unbounded following) from w order by id
-- @case
select id, first_value(v) over (order by v, id rows between current row and unbounded following) from w order by id
-- @case
select id, last_value(v) over (order by v, id rows between current row and unbounded following) from w order by id
-- @case
select id, min(v) over (order by v, id rows between current row and unbounded following) from w order by id
-- @case
select id, sum(v) over (partition by g order by v rows between current row and unbounded following) from w order by id
-- @case
select id, sum(v) over (order by v, id rows between unbounded preceding and unbounded following) from w order by id
-- @case
select id, count(*) over (order by v, id rows between unbounded preceding and unbounded following) from w order by id
-- @case
select id, first_value(v) over (order by v, id rows between unbounded preceding and unbounded following) from w order by id
-- @case
select id, last_value(v) over (order by v, id rows between unbounded preceding and unbounded following) from w order by id
-- @case
select id, min(v) over (order by v, id rows between unbounded preceding and unbounded following) from w order by id
-- @case
select id, sum(v) over (partition by g order by v rows between unbounded preceding and unbounded following) from w order by id
-- @case
select id, sum(v) over (order by v, id rows 1 preceding) from w order by id
-- @case
select id, count(*) over (order by v, id rows 1 preceding) from w order by id
-- @case
select id, first_value(v) over (order by v, id rows 1 preceding) from w order by id
-- @case
select id, last_value(v) over (order by v, id rows 1 preceding) from w order by id
-- @case
select id, min(v) over (order by v, id rows 1 preceding) from w order by id
-- @case
select id, sum(v) over (partition by g order by v rows 1 preceding) from w order by id
-- @case
select id, sum(v) over (order by v, id rows between 2 following and 3 following) from w order by id
-- @case
select id, count(*) over (order by v, id rows between 2 following and 3 following) from w order by id
-- @case
select id, first_value(v) over (order by v, id rows between 2 following and 3 following) from w order by id
-- @case
select id, last_value(v) over (order by v, id rows between 2 following and 3 following) from w order by id
-- @case
select id, min(v) over (order by v, id rows between 2 following and 3 following) from w order by id
-- @case
select id, sum(v) over (partition by g order by v rows between 2 following and 3 following) from w order by id
-- @case
select id, sum(v) over (order by v, id rows between 1 preceding and 2 preceding) from w order by id
-- @case
select id, count(*) over (order by v, id rows between 1 preceding and 2 preceding) from w order by id
-- @case
select id, first_value(v) over (order by v, id rows between 1 preceding and 2 preceding) from w order by id
-- @case
select id, last_value(v) over (order by v, id rows between 1 preceding and 2 preceding) from w order by id
-- @case
select id, min(v) over (order by v, id rows between 1 preceding and 2 preceding) from w order by id
-- @case
select id, sum(v) over (partition by g order by v rows between 1 preceding and 2 preceding) from w order by id
-- @case
select id, sum(v) over (order by v, id range between unbounded preceding and current row) from w order by id
-- @case
select id, count(*) over (order by v, id range between unbounded preceding and current row) from w order by id
-- @case
select id, first_value(v) over (order by v, id range between unbounded preceding and current row) from w order by id
-- @case
select id, last_value(v) over (order by v, id range between unbounded preceding and current row) from w order by id
-- @case
select id, min(v) over (order by v, id range between unbounded preceding and current row) from w order by id
-- @case
select id, sum(v) over (partition by g order by v range between unbounded preceding and current row) from w order by id
-- @case
select id, sum(v) over (order by v, id range between current row and unbounded following) from w order by id
-- @case
select id, count(*) over (order by v, id range between current row and unbounded following) from w order by id
-- @case
select id, first_value(v) over (order by v, id range between current row and unbounded following) from w order by id
-- @case
select id, last_value(v) over (order by v, id range between current row and unbounded following) from w order by id
-- @case
select id, min(v) over (order by v, id range between current row and unbounded following) from w order by id
-- @case
select id, sum(v) over (partition by g order by v range between current row and unbounded following) from w order by id
-- @case
select id, sum(v) over (order by v, id range between 5 preceding and 5 following) from w order by id
-- @case
select id, count(*) over (order by v, id range between 5 preceding and 5 following) from w order by id
-- @case
select id, first_value(v) over (order by v, id range between 5 preceding and 5 following) from w order by id
-- @case
select id, last_value(v) over (order by v, id range between 5 preceding and 5 following) from w order by id
-- @case
select id, min(v) over (order by v, id range between 5 preceding and 5 following) from w order by id
-- @case
select id, sum(v) over (partition by g order by v range between 5 preceding and 5 following) from w order by id
-- @case
select id, sum(v) over (order by v, id range between unbounded preceding and unbounded following) from w order by id
-- @case
select id, count(*) over (order by v, id range between unbounded preceding and unbounded following) from w order by id
-- @case
select id, first_value(v) over (order by v, id range between unbounded preceding and unbounded following) from w order by id
-- @case
select id, last_value(v) over (order by v, id range between unbounded preceding and unbounded following) from w order by id
-- @case
select id, min(v) over (order by v, id range between unbounded preceding and unbounded following) from w order by id
-- @case
select id, sum(v) over (partition by g order by v range between unbounded preceding and unbounded following) from w order by id
-- @case
select id, sum(v) over (order by v, id range 10 preceding) from w order by id
-- @case
select id, count(*) over (order by v, id range 10 preceding) from w order by id
-- @case
select id, first_value(v) over (order by v, id range 10 preceding) from w order by id
-- @case
select id, last_value(v) over (order by v, id range 10 preceding) from w order by id
-- @case
select id, min(v) over (order by v, id range 10 preceding) from w order by id
-- @case
select id, sum(v) over (partition by g order by v range 10 preceding) from w order by id
-- @case
select id, row_number() over (order by v) from w order by id
-- @case
select id, rank() over (order by g) , dense_rank() over (order by g nulls last) from w order by id
-- @case
select g, sum(v), rank() over (order by sum(v) desc) from w group by g order by g
-- @case
select g, count(*) over (), count(*) from w group by g order by g
-- @case
select id, sum(v) over (partition by g) - v from w order by id
-- @case
select id, v, v * 100 / sum(v) over () from w order by id
-- @case
select id, (select max(v) from w) - max(v) over (partition by g) from w order by id
-- @case
select * from (select id, row_number() over (partition by g order by v desc) rn from w) where rn = 1 order by id
-- @case
select id, row_number() over w1, sum(v) over w1 from w window w1 as (partition by g order by id) order by id
-- @case
select id, sum(v) over (w1 rows between unbounded preceding and current row) from w window w1 as (order by id) order by id
-- @case
select id, ntile(0) over (order by id) from w
-- @case
select id, ntile(null) over (order by id) from w
-- @case
select id, lag(v, -1) over (order by id) from w
-- @case
select id, nth_value(v, 0) over (order by id) from w
-- @case
select id, lag(v, 1, 'x') over (order by id) from w
-- @case
select id, lead(n, 1, 0) over (order by id), lag(d, 1, 0) over (order by id) from w order by id
-- @case
select id, row_number() over (order by v) from w where row_number() over (order by v) > 1
-- @case
select id from w group by row_number() over (order by id)
-- @case
select id, sum(sum(v)) over () from w
-- @case
select id, rank(1) over (order by id) from w
-- @case
select id, row_number() from w
-- @case
select id, sum(v) over (order by v range between 1 preceding and current row) from w order by id
-- @case
select id, sum(v) over (order by v, id range between 1 preceding and current row) from w order by id
-- @case
select id, sum(v) over (rows between 1 preceding and current row) from w order by id
-- @case
select id, list(s) over (partition by g) from w order by id
-- @case
select id, count(distinct v) over (partition by g) from w order by id
-- @case
select id, percent_rank() over (partition by g order by v), cume_dist() over (partition by g order by v) from w order by id
