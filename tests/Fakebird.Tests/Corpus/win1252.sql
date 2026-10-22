-- A database whose default character set is WIN1252 used over a UTF8 connection: columns without a character set,
-- transliteration, lengths, ordering and comparisons.
-- @setup
alter database set default character set win1252;
create domain dw as varchar(5);
create table w (id integer not null primary key, v varchar(10), c char(5), u varchar(10) character set utf8, d dw, b blob sub_type text);
insert into w values (1, 'àéçãõ', 'àé', 'àéçãõ', 'ção', 'àéçãõ€');
insert into w values (2, '€ŠŒŽ', '€', '€ŠŒŽ', '€', '€ŠŒŽ');
insert into w values (3, 'abc', 'abc', 'abc', 'abc', 'abc');
insert into w values (4, 'ABC', 'ABC', 'ABC', 'ABC', 'ABC');
insert into w values (5, 'zz', 'zz', 'zz', 'zz', 'zz');
insert into w values (6, 'Àb', 'Àb', 'Àb', 'Àb', 'Àb');
insert into w (id, u) values (7, 'ł😀');
-- @case
select rdb$character_set_name from rdb$database
-- @case
select r.rdb$field_name, f.rdb$character_set_id, f.rdb$collation_id, f.rdb$field_length, f.rdb$character_length
from rdb$relation_fields r join rdb$fields f on f.rdb$field_name = r.rdb$field_source
where r.rdb$relation_name = 'W' order by r.rdb$field_position
-- @case
select f.rdb$character_set_id, f.rdb$field_length, f.rdb$character_length from rdb$fields f where f.rdb$field_name = 'DW'
-- @case
select * from w order by id
-- @case
select id, char_length(v), octet_length(v), octet_length(c), octet_length(u), octet_length(d), octet_length(b), char_length(b) from w order by id
-- @case
select id, v || '|', c || '|', v || u, u || v from w order by id
-- @case
select id from w order by v, id
-- @case
select id from w order by v desc, id
-- @case
select id from w order by u, id
-- @case
select id from w order by c, id
-- @case
select min(v), max(v), min(c), max(c), min(u), max(u), count(distinct v) from w
-- @case
select v, count(*) from w group by v order by v
-- @case
select id, v < 'à', v > 'Z', v = u, v < u, v > u, c = 'àé', c < 'b' from w order by id
-- @case
select a.id, b.id from w a join w b on a.v = b.u order by 1, 2
-- @case
select id from w where v = 'ÀB' or upper(v) = 'ÀB' order by id
-- @case
select id from w where v between 'a' and 'z' order by id
-- @case
select id, v in ('abc', '€ŠŒŽ'), v = 'àéçãõ', v = 'abc  ' from w order by id
-- @case
select id, upper(v), lower(v), upper(c), lower(u) from w order by id
-- @case
select id, v like '%ç%', v like 'À_', v containing 'Ç', v starting with '€', v similar to '%[àé]%' from w order by id
-- @case
select id, position('ç' in v), substring(v from 2 for 2), reverse(v), lpad(v, 7, '€'), rpad(c, 6, '.'), replace(v, 'ç', 'c') from w where id in (1, 2) order by id
-- @case
select id, cast(v as varchar(20) character set octets), cast(c as char(6) character set octets) from w where id in (1, 2) order by id
-- @case
select id, cast(v as varchar(10) character set utf8), octet_length(cast(v as varchar(10) character set utf8)) from w where id in (1, 2) order by id
-- @case
select cast(u as varchar(10) character set win1252) from w where id = 7
-- @case
select cast(u as varchar(10)) from w where id = 7
-- @case
select octet_length(cast('ação' as varchar(10))), octet_length(cast('ação' as char(5))), octet_length('ação'), char_length('ação') from rdb$database
-- @case
select cast('ação' as varchar(10)), cast('😀' as varchar(10) character set utf8) from rdb$database
-- @case
select cast('😀' as varchar(10)) from rdb$database
-- @case
select octet_length(_win1252 'àb'), char_length(_win1252 'àb') from rdb$database
-- @case
select v || 'ł', octet_length(v || 'ł') from w where id = 3
-- @case
select v || '😀' from w where id = 3
-- @case
insert into w (id, v) values (10, '😀')
-- @case
insert into w (id, v) values (11, 'ł')
-- @case
insert into w (id, c) values (12, '→')
-- @case params: [13, "→"]
insert into w (id, v) values (?, ?)
-- @case params: [14, "ação€ŠŒŽ"]
insert into w (id, v) values (?, ?)
-- @case
select id, v, octet_length(v) from w where id = 14
-- @case
insert into w (id, c) values (15, 'àéçãõx')
-- @case
insert into w (id, c) values (16, 'àéçãõ')
-- @case
insert into w (id, v) values (17, 'àéçãõàéçãõ')
-- @case
insert into w (id, v) values (18, 'àéçãõàéçãõ€')
-- @case
select id, c, octet_length(c), char_length(c) from w where id = 16
-- @case
insert into w (id, d) values (19, '😀')
-- @case
insert into w (id, d) values (20, 'àéçãõ')
-- @case
insert into w (id, d) values (21, 'àéçãõa')
-- @case
update w set b = 'ł😀' where id = 3
-- @case
update w set b = 'ação€' where id = 4
-- @case
select id, b, octet_length(b) from w where id = 4
-- @case
update w set v = u where id = 7
-- @case
update w set v = u where id = 1
-- @case params: ["àéçãõ"]
select id from w where v = ?
-- @case params: ["😀"]
select id from w where v = ?
-- @case params: ["😀"]
select id from w where u = ?
-- @case
create procedure p (x varchar(5)) returns (y varchar(5), l integer) as
begin
  y = x;
  l = octet_length(x);
  suspend;
end
-- @commit
-- @case
select y, l from p('ação')
-- @case
select y from p('😀')
-- @case
execute block returns (r varchar(10), o integer) as declare x varchar(10); begin x = 'ação'; r = x; o = octet_length(x); suspend; end
-- @case
execute block returns (r varchar(10)) as declare x varchar(10); begin x = 'ł'; r = x; suspend; end
-- @case
execute block returns (r varchar(10) character set utf8) as begin r = 'ł'; suspend; end
-- @case
select a.id, b.id from w a join w b on b.u = a.v order by 1, 2
-- @case
select id, coalesce(v, u), octet_length(coalesce(v, u)) from w where id in (1, 7) order by id
-- @case
select coalesce(u, v) from w where id = 7
-- @case
select iif(id = 7, u, v) from w order by id
-- @case
select v from w where id = 1 union all select u from w where id = 7
-- @case
select u from w where id = 7 union all select v from w where id = 1
-- @case
select id, case when id = 1 then 'ł' else v end from w where id in (1, 2) order by id
-- @case
select id from w where v in (select u from w where id < 7) order by id
-- @case
select id from w where v = 'ł' order by id
-- @case
select id from w where 'ł' = v order by id
-- @case
select id from w where u = v order by id
-- @case
select id from w where v like 'ł%' order by id
-- @case
select id from w where v containing 'ł' order by id
-- @case
select id from w where v starting with 'ł' order by id
-- @case
select id from w where u = coalesce(v, 'x') order by id
-- @case
select id from w where coalesce(v, 'x') = u order by id
-- @case
select id from w where u > coalesce(v, 'x') order by id
-- @case
select id from w where u like coalesce(v, 'x') order by id
-- @case
select id from w where cast(v as varchar(10) character set ascii) = u order by id
-- @case
select max(u || v) from w
-- @case
select id, upper(cast('µÿ' as varchar(5) character set win1252)) from w where id = 1
-- @case
create procedure q returns (a integer, b integer, r varchar(10)) as
declare x varchar(10);
begin
  x = 'ação';
  a = octet_length(x);
  b = octet_length(cast('ação' as varchar(10)));
  r = cast('ł' as varchar(10));
  suspend;
end
-- @commit
-- @case
select a, b, r from q
-- @case
create procedure q3 returns (a integer, b integer, c integer) as
begin
  a = octet_length(cast('ação' as varchar(10)));
  b = octet_length('ação');
  c = octet_length(cast('ação' as varchar(10)) || 'x');
  suspend;
end
-- @commit
-- @case
select a, b, c from q3
-- @case
create procedure q2 returns (r varchar(10)) as
declare x varchar(10);
begin
  x = 'ł';
  r = 'ok';
  suspend;
end
-- @commit
-- @case
select r from q2
-- @case
create table tl (id integer, s varchar(10), n integer)
-- @commit
-- @case
create trigger tl_bi for tl before insert as
declare x varchar(10);
begin
  x = new.s;
  new.n = octet_length(x);
end
-- @commit
-- @case
insert into tl (id, s) values (1, 'ação')
-- @case
select id, s, n from tl
-- @case
alter table w alter v type varchar(20)
-- @commit
-- @case
select r.rdb$field_name, f.rdb$character_set_id, f.rdb$field_length, f.rdb$character_length
from rdb$relation_fields r join rdb$fields f on f.rdb$field_name = r.rdb$field_source
where r.rdb$relation_name = 'W' and r.rdb$field_name = 'V'
-- @case
alter table w add e varchar(3), add e2 varchar(3) character set utf8
-- @commit
-- @case
select r.rdb$field_name, f.rdb$character_set_id from rdb$relation_fields r join rdb$fields f on f.rdb$field_name = r.rdb$field_source
where r.rdb$relation_name = 'W' and r.rdb$field_name starting with 'E' order by 1
-- @case
create global temporary table g (s varchar(3))
-- @commit
-- @case
create view vw as select v, u, v || u as vu from w
-- @commit
-- @case
select r.rdb$field_name, f.rdb$character_set_id, f.rdb$field_length from rdb$relation_fields r join rdb$fields f on f.rdb$field_name = r.rdb$field_source
where r.rdb$relation_name in ('G', 'VW') order by r.rdb$relation_name, r.rdb$field_position
-- @case
create table bad (s varchar(3) character set nosuch)
-- @case
alter database set default character set nosuch
-- @case
select cast('a' as varchar(3) character set nosuch) from rdb$database
