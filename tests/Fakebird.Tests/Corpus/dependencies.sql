-- RDB$DEPENDENCIES of procedures, functions and triggers (tables, columns, views, procedures, functions,
-- sequences, exceptions, domains), and the DROP / ALTER TABLE ... DROP that a dependency refuses:
-- "cannot delete COLUMN T.C, there are n dependencies", checked column by column, then the whole object.
-- Firebird checks them at commit (@ddl commits each statement, Fakebird fails the statement itself).
-- @setup
create domain dm integer;
create sequence g;
create exception ex 'boom';
create table t (zz integer, aa integer, mm integer, k integer);
create table t2 (id integer, s integer, d integer, e integer, g integer, free integer);
create table t3 (id integer, h integer);
create table t4 (id integer, h integer);
create table t5 (id integer, a integer);
create table t6 (id integer, a integer);
create table tz (id integer, q integer);
create table tw (id integer, y integer);
create table tt (id integer, x integer, other integer);
create table tc (id integer, c integer);
create view v4 as select h from t4;
create view v6 as select count(*) c from t6;
create table tv (id integer, n integer);
create view va as select id, n from tv;
create table tr (id integer, a integer, b integer, c integer);
create view vr as select id from tr;
create view vb as select n from va;
create view vcnt as select count(*) c from va;
set term ^;
create procedure p1 returns (r integer) as begin select mm from t into :r; suspend; end^
create procedure p2 returns (r integer) as begin select mm from t into :r; select zz from t into :r; suspend; end^
create procedure p3 as begin insert into t2 (s) values (1); update t2 set d = 2 where e = 3; delete from t2 where g = 1; end^
create procedure p4 returns (r integer) as begin for select * from t3 into :r, :r do suspend; end^
create procedure p5 returns (r integer) as begin for select r from p1 into :r do suspend; end^
create function f1 (x integer) returns integer as begin return x; end^
create procedure p6 returns (r integer) as begin r = f1(1); execute procedure p3; suspend; end^
create procedure p7 returns (r integer) as begin select h from t4 into :r; suspend; end^
create procedure pz as declare v integer; begin select count(*) from tz into :v; end^
create function fw returns integer as begin return (select max(y) from tw); end^
create trigger trt for tt before insert as begin new.x = (select max(c) from tc); end^
create procedure q1 (x dm, y type of column t5.a) returns (r integer) as
  declare z type of dm;
begin
  r = gen_id(g, 1);
  r = next value for g;
  if (r < 0) then exception ex;
  insert into t5 values (1, 2);
  suspend;
end^
create procedure pv returns (r integer) as begin select id from va into :r; suspend; end^
create procedure zr returns (r integer) as begin select a from tr into :r; suspend; end^
create procedure pr returns (r integer) as begin select r from zr into :r; suspend; end^
create procedure pvr returns (r integer) as begin select id from vr into :r; suspend; end^
create trigger trr for tr before insert as begin new.b = 1; end^
create function fr returns integer as begin return 1; end^
create procedure pfr returns (r integer) as begin r = fr(); suspend; end^
create procedure q2 as begin update t6 set a = a + 1; end^
create trigger tr6 for t6 after update as begin insert into t5 (id) values (new.a + old.id); end^
set term ;^
-- @case
select trim(rdb$dependent_name), trim(rdb$depended_on_name), trim(rdb$field_name), rdb$dependent_type, rdb$depended_on_type from rdb$dependencies where rdb$dependent_name not starting 'RDB$' order by 1, 2, 3
-- @ddl
drop table t
-- @ddl
alter table t drop mm
-- @ddl
alter table t drop aa
-- @ddl
drop table t2
-- @ddl
alter table t2 drop s
-- @ddl
alter table t2 drop d
-- @ddl
alter table t2 drop e
-- @ddl
alter table t2 drop g
-- @ddl
alter table t2 drop free
-- @ddl
drop table t3
-- @ddl
alter table t3 drop h
-- @ddl
drop procedure p1
-- @ddl
drop procedure p3
-- @ddl
drop function f1
-- @ddl
drop sequence g
-- @ddl
drop exception ex
-- @ddl
drop table t4
-- @ddl
alter table t4 drop h
-- @ddl
drop view v4
-- @ddl
drop table tz
-- @ddl
alter table tz drop q
-- @ddl
drop table tw
-- @ddl
alter table tw drop y
-- @ddl
drop table tc
-- @ddl
alter table tc drop c
-- @ddl
alter table tt drop x
-- @ddl
alter table tt drop other
-- @ddl
drop table tt
-- @ddl
drop table tc
-- @ddl
alter table t5 drop a
-- @ddl
drop table t6
-- @ddl
drop trigger tr6
-- @ddl
drop table t6
-- @ddl
drop procedure q2
-- @ddl
drop table t6
-- @ddl
drop view v6
-- @ddl
drop table t6
-- @ddl
drop procedure pz
-- @ddl
drop table tz
-- @ddl
alter procedure p2 returns (r integer) as begin select zz from t into :r; suspend; end
-- @ddl
alter table t drop mm
-- @ddl
drop procedure p2
-- @ddl
drop procedure p5
-- @ddl
drop procedure p1
-- @ddl
alter table t drop mm
-- @ddl
drop table t
-- @ddl
drop view va
-- @ddl
alter table tv drop id
-- @ddl
drop view vb
-- @ddl
drop view va
-- @ddl
drop procedure pv
-- @ddl
drop view va
-- @ddl
drop view vcnt
-- @ddl
drop view va
-- @ddl
drop table tv
-- @ddl
recreate procedure zr returns (r integer) as begin r = 1; suspend; end
-- @ddl
recreate function fr returns integer as begin return 2; end
-- @ddl
recreate view vr as select id from tr
-- @ddl
recreate table tr (id integer, a integer, b integer, c integer)
-- @ddl
recreate sequence g
-- @ddl
recreate exception ex 'again'
-- @ddl
alter table tr alter id to id2
-- @ddl
alter table tr alter a to a2
-- @ddl
alter table tr alter b to b2
-- @ddl
alter table tr alter c to c2
-- @ddl
alter table tr alter a type bigint
-- @ddl
alter table tr alter a position 1
-- @case
select trim(rdb$dependent_name), trim(rdb$depended_on_name), trim(rdb$field_name), rdb$dependent_type, rdb$depended_on_type from rdb$dependencies where rdb$dependent_name not starting 'RDB$' order by 1, 2, 3
