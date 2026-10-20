-- PSQL: procedures (selectable/executable), functions, triggers, EXECUTE BLOCK, variables and conversions,
-- control flow, cursors, user exceptions, WHEN handlers (SQLCODE/GDSCODE/SQLSTATE values, re-raise, undo),
-- ROW_COUNT, EXECUTE STATEMENT, autonomous transactions, sub-routines, and the errors with their stack traces.
-- @setup
create table t (id integer not null primary key, v varchar(10), n integer);
create table log (seq integer, msg varchar(60));
create sequence lg;
create exception ex1 'ex1 message';
create exception ex2 'value @1 is bad (@2)';
insert into t values (1, 'one', 10);
insert into t values (2, 'two', 20);
insert into t values (3, null, null);
-- @case
create procedure sel (a integer) returns (id integer, v varchar(10)) as
begin
  for select id, v from t where id >= :a order by id into :id, :v do
    suspend;
end
-- @commit
-- @case
create procedure exe (a integer, b varchar(5) = 'dflt') returns (r varchar(30)) as
begin
  r = a || '-' || b;
end
-- @commit
-- @case
create procedure boom (k integer) as
declare x integer;
begin
  if (k = 1) then exception ex1;
  if (k = 2) then exception ex2 using (k, 'two');
  if (k = 3) then x = 1 / 0;
  if (k = 4) then insert into t values (1, 'dup', 0);
  if (k = 5) then exception ex1 'custom ' || k;
end
-- @commit
-- @case
create procedure wrap (k integer) returns (msg varchar(100)) as
begin
  execute procedure boom(:k);
  msg = 'ok';
  suspend;
when any do
  begin
    msg = 'caught ' || sqlcode || ' ' || gdscode || ' ' || sqlstate;
    suspend;
  end
end
-- @commit
-- @case
create procedure counter returns (i integer) as
begin
  i = 0;
  while (i < 10) do
  begin
    i = i + 1;
    if (i = 3) then continue;
    if (i = 6) then leave;
    suspend;
  end
end
-- @commit
-- @case
create function f_add (a integer, b integer = 5) returns integer as
begin
  return a + b;
end
-- @commit
-- @case
create function f_str (s varchar(5)) returns varchar(3) as
begin
  return s;
end
-- @commit
-- @case
create trigger t_bi for t before insert position 0 as
begin
  if (new.v is null) then new.v = 'auto';
  insert into log values (gen_id(lg, 1), 'bi ' || new.id);
end
-- @commit
-- @case
create trigger t_bi2 for t before insert position 1 as
begin
  new.n = coalesce(new.n, 0) + 1;
end
-- @commit
-- @case
create trigger t_au for t after update as
begin
  insert into log values (gen_id(lg, 1), 'au ' || old.v || '>' || new.v || ' ' || iif(updating, 'U', '?') || iif(inserting, 'I', '-'));
end
-- @commit
-- @case
create trigger t_bd for t before delete as
begin
  if (old.id = 2) then exception ex2 using (old.id, 'no delete');
end
-- @commit
-- @case params: [1, "x"]
execute block (a integer = ?, b varchar(10) = ?) returns (r varchar(30)) as begin r = a || b; suspend; end
-- @case params: [null, "x"]
execute block (a integer = ?, b varchar(10) = ?) returns (r varchar(30)) as begin r = a || b; suspend; end
-- @case
select * from sel(2)
-- @case
select id from sel(1) where id < 3 order by id desc
-- @case
select * from sel
-- @case
select * from sel(1, 2)
-- @case
execute procedure exe(1)
-- @case
execute procedure exe(1, 'x')
-- @case
execute procedure exe(1, 'toolong')
-- @case
execute procedure sel(1)
-- @case
execute procedure nosuch
-- @case
select * from counter
-- @case
select f_add(1), f_add(1, 2), f_add(null) from rdb$database
-- @case
select f_str('ab'), f_str('abcd') from rdb$database
-- @case
select f_add() from rdb$database
-- @case
execute procedure boom(0)
-- @case
execute procedure boom(1)
-- @case
execute procedure boom(2)
-- @case
execute procedure boom(3)
-- @case
execute procedure boom(4)
-- @case
execute procedure boom(5)
-- @case
select * from wrap(0)
-- @case
select * from wrap(1)
-- @case
select * from wrap(2)
-- @case
select * from wrap(3)
-- @case
select * from wrap(4)
-- @case
insert into t (id) values (10)
-- @case
insert into t (id, v, n) values (11, 'x', 5)
-- @case
select id, v, n from t where id >= 10 order by id
-- @case
update t set v = 'ten' where id = 10
-- @case
delete from t where id = 2
-- @case
delete from t where id = 11
-- @case
select seq, msg from log order by seq
-- @case
execute block returns (a integer, b varchar(5), c numeric(5,2), d date) as begin a = '12'; b = 12345; c = 1.005; d = '2024-02-29'; suspend; end
-- @case
execute block returns (a smallint) as begin a = 32768; suspend; end
-- @case
execute block returns (b varchar(3)) as begin b = 'abcd'; suspend; end
-- @case
execute block returns (b varchar(3)) as begin b = 'abc   '; suspend; end
-- @case
execute block returns (x integer) as declare y integer; begin x = y; suspend; end
-- @case
execute block returns (x integer) as declare y integer = 5; declare z type of column t.n = 7; begin x = y + z; suspend; end
-- @case
execute block returns (x varchar(20)) as declare c cursor for (select v from t order by id); declare s varchar(10); begin open c; fetch c into s; x = s || ' ' || row_count; fetch c into s; x = x || ',' || s; close c; suspend; end
-- @case
execute block returns (x varchar(40)) as declare c cursor for (select id from t where id > 100); declare i integer; begin open c; fetch c into i; x = coalesce(i, -1) || ' ' || row_count; close c; suspend; end
-- @case
execute block returns (i integer, r integer) as begin update t set n = n where id < 3; r = row_count; i = 1; suspend; delete from t where id = 999; r = row_count; i = 2; suspend; select count(*) from t into :i; r = row_count; suspend; end
-- @case
execute block returns (i integer) as begin for select id from t order by id into :i do begin if (i = 3) then break; suspend; end end
-- @case
execute block returns (i integer) as begin i = 0; lbl: while (i < 5) do begin i = i + 1; if (i = 2) then continue lbl; if (i = 4) then leave lbl; suspend; end end
-- @case
execute block returns (i integer) as begin i = 1; suspend; exit; i = 2; suspend; end
-- @case
execute block as begin suspend; end
-- @case
execute block returns (s varchar(60)) as begin begin exception ex1; when exception ex1 do s = 'ex1 ' || sqlcode || ' ' || gdscode; end suspend; end
-- @case
execute block returns (s varchar(60)) as begin begin exception ex2 using (1, 'x'); when exception ex1 do s = 'wrong'; when exception ex2 do s = 'ex2 ' || sqlstate; end suspend; end
-- @case
execute block returns (s varchar(60)) as begin begin x: begin exception ex1; end when any do begin s = 'outer'; end end suspend; end
-- @case
execute block returns (s varchar(60)) as begin begin begin s = 1/0; when any do exception; end when gdscode arith_except do s = 're-raised ' || gdscode; end suspend; end
-- @case
execute block returns (s varchar(60)) as begin begin insert into t values (1, 'x', 1); when sqlcode -803 do s = 'dup -803'; end suspend; end
-- @case
execute block returns (s varchar(60)) as begin begin insert into t values (1, 'x', 1); when gdscode unique_key_violation do s = 'dup gds'; end suspend; end
-- @case
execute block returns (s varchar(60)) as begin begin insert into t values (1, 'x', 1); when sqlstate '23000' do s = 'dup state'; end suspend; end
-- @case
execute block returns (s varchar(80)) as begin begin s = cast('x' as integer); when any do s = sqlcode || ' ' || gdscode || ' ' || sqlstate; end suspend; end
-- @case
execute block returns (s varchar(80)) as begin begin s = cast('abcdef' as varchar(2)); when any do s = sqlcode || ' ' || gdscode || ' ' || sqlstate; end suspend; end
-- @case
execute block returns (s varchar(80)) as begin begin execute procedure nosuch_at_runtime; when any do s = sqlcode || ' ' || gdscode; end suspend; end
-- @case
execute block returns (s varchar(80)) as begin begin execute statement 'select 1 from nosuch' into :s; when any do s = sqlcode || ' ' || gdscode || ' ' || sqlstate; end suspend; end
-- @case
execute block returns (n integer) as begin begin insert into log values (100, 'undone'); n = 1 / 0; when any do n = (select count(*) from log where seq = 100); end suspend; end
-- @case
execute block returns (n integer) as begin insert into log values (101, 'kept'); begin n = 1 / 0; when any do n = (select count(*) from log where seq = 101); end suspend; end
-- @case
execute block returns (s varchar(60)) as begin begin exception ex1; when any do begin s = 'handler'; s = 1/0; end end suspend; end
-- @case
execute block as begin exception ex2 using ('abc'); end
-- @case
execute block as begin exception ex2 using (1, 2, 3); end
-- @case
execute block as begin exception; end
-- @case
execute block returns (s varchar(60)) as begin execute statement 'select v from t where id = 1' into :s; suspend; end
-- @case
execute block returns (s varchar(60)) as begin execute statement ('select v from t where id = ?') (3) into :s; suspend; end
-- @case
execute block returns (s varchar(60)) as begin execute statement ('select v from t where id = :i') (i := 1) into :s; suspend; end
-- @case
execute block returns (i integer) as begin for execute statement 'select id from t order by id' into :i do suspend; end
-- @case
execute block returns (s varchar(60)) as begin execute statement 'select v from t' into :s; suspend; end
-- @case
execute block returns (n integer) as begin in autonomous transaction do insert into log values (200, 'auto'); n = (select count(*) from log where seq = 200); suspend; end
-- @case
execute block returns (r integer) as declare function sq (x integer) returns integer as begin return x * x; end begin r = sq(7); suspend; end
-- @case
execute block returns (r integer) as declare procedure p returns (o integer) as begin o = 3; suspend; o = 4; suspend; end begin for select o from p into :r do suspend; end
-- @case
execute block returns (r varchar(30)) as begin r = current_user; r = r || ' ' || (select count(*) from rdb$database); suspend; end
-- @case
execute block returns (x integer) as begin x = undefined_var; suspend; end
-- @case
execute block returns (x integer) as begin y = 1; suspend; end
-- @case
execute block returns (x integer) as declare x integer; begin suspend; end
-- @case
execute block as declare c cursor for (select id from t); begin fetch c into :c; end
-- @case
execute block as declare c cursor for (select id from t); declare i integer; begin fetch c into :i; end
-- @case
execute block as declare c cursor for (select id from t); begin open c; open c; end
-- @case
execute block returns (s varchar(20)) as begin if (1 = 1) then s = 'a'; else s = 'b'; suspend; if (null = null) then s = 'n'; else s = 'not'; suspend; end
-- @case
select * from wrap(5)
-- @case
execute block returns (r integer) as declare i integer = 1; begin select count(*) from rdb$database where 1 = i into :r; suspend; end
-- @case
execute block returns (r integer) as declare i integer = 1; begin update t set n = n where id = i; r = row_count; suspend; end
-- @case
execute block returns (r integer) as declare i integer = 1; begin insert into log values (300, 'x' || i); r = row_count; suspend; end
-- @case
execute block returns (r integer) as declare i integer = 1; begin insert into log values (301, 'x' || :i); r = row_count; suspend; end
-- @case
execute block returns (r integer) as declare v integer = 2; begin select count(*) from t where id = v into :r; suspend; end
-- @case
execute block returns (r integer) as declare id integer = 2; begin select count(*) from t where id = id into :r; suspend; end
-- @case
execute block returns (r integer) as declare id integer = 2; begin select count(*) from t where id = :id into :r; suspend; end
-- @case
execute block returns (r integer) as declare i integer = 1; begin r = (select count(*) from t where id = i); suspend; end
-- @case
execute block returns (r integer) as declare i integer = 1; begin r = (select count(*) from t where id = :i); suspend; end
-- @case
execute block returns (r integer) as declare i integer = 1; declare c cursor for (select id from t where id = i); begin open c; fetch c into :r; close c; suspend; end
-- @case
execute block returns (r varchar(30)) as declare i integer = 1; begin execute procedure exe(i, 'q') returning_values :r; suspend; end
-- @case
execute block returns (r varchar(30)) as declare i integer = 1; begin select r from exe(i) into :r; suspend; end
