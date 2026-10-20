-- Triggers: firing order (position, then name), BEFORE/AFTER x INSERT/UPDATE/DELETE and multi-event triggers,
-- NEW/OLD reads and writes, INSERTING/UPDATING/DELETING, triggers fired by cascades and by other triggers,
-- exceptions raised in triggers (with their stack), ALTER/inactive triggers; and EXECUTE STATEMENT variants.
-- @setup
create table tr (id integer not null primary key, v integer, s varchar(20));
create table lg (n integer, msg varchar(80));
create table par (id integer not null primary key);
create table chi (id integer not null primary key, pid integer references par (id) on delete cascade on update cascade, v integer);
create sequence sq;
create exception tr_ex 'blocked: @1';
insert into par values (1);
insert into par values (2);
-- @case
create trigger tr_b1 for tr before insert or update position 10 as begin new.s = coalesce(new.s, '') || 'b1'; insert into lg values (next value for sq, 'b1 ' || iif(inserting, 'I', 'U') || ' ' || new.id); end
-- @commit
-- @case
create trigger tr_b0 for tr before insert position 0 as begin new.s = coalesce(new.s, '') || 'b0'; insert into lg values (next value for sq, 'b0 ' || new.id); end
-- @commit
-- @case
create trigger tr_a for tr after insert or update or delete as begin insert into lg values (next value for sq, 'a ' || iif(inserting, 'I', iif(updating, 'U', 'D')) || ' ' || coalesce(old.v, -1) || '>' || coalesce(new.v, -1)); end
-- @commit
-- @case
create trigger tr_bb for tr before insert position 10 as begin new.s = new.s || 'bb'; end
-- @commit
-- @case
create trigger tr_bd for tr before delete as begin if (old.v = 666) then exception tr_ex using (old.id); end
-- @commit
-- @case
create trigger tr_bu for tr before update as begin if (new.v < 0) then new.v = 0; if (new.id <> old.id) then exception tr_ex using ('id change'); end
-- @commit
-- @case
create trigger chi_bd for chi before delete as begin insert into lg values (next value for sq, 'chi del ' || old.id || ' pid ' || old.pid); end
-- @commit
-- @case
create trigger chi_bu for chi before update as begin insert into lg values (next value for sq, 'chi upd ' || old.pid || '>' || new.pid); end
-- @commit
-- @case
insert into tr (id, v) values (1, 10)
-- @case
insert into tr (id, v, s) values (2, 20, 'x')
-- @case
update tr set v = v + 1
-- @case
update tr set v = -5 where id = 1
-- @case
update tr set id = 9 where id = 1
-- @case
insert into tr (id, v) values (3, 666)
-- @case
delete from tr where id = 3
-- @case
delete from tr where id = 2
-- @case
select id, v, s from tr order by id
-- @case
select n, msg from lg order by n
-- @case
insert into chi values (1, 1, 1)
-- @case
insert into chi values (2, 1, 2)
-- @case
update par set id = 10 where id = 1
-- @case
delete from par where id = 10
-- @case
select n, msg from lg where msg starting 'chi' order by n
-- @case
alter trigger tr_b0 inactive
-- @commit
-- @commit
-- @case
insert into tr (id, v) values (4, 40)
-- @case
select id, s from tr where id = 4
-- @case
alter trigger tr_b0 active position 20
-- @commit
-- @commit
-- @case
insert into tr (id, v) values (5, 50)
-- @case
select id, s from tr where id = 5
-- @case
create trigger tr_nope for nosuch before insert as begin end
-- @commit
-- @case
create trigger tr_old for tr before insert as begin old.v = 1; end
-- @commit
-- @case
create trigger tr_old2 for tr before insert as begin new.v = old.v; end
-- @commit
-- @case
create trigger tr_del for tr before delete as begin new.v = 1; end
-- @commit
-- @case
create trigger tr_ins for tr before insert as begin if (deleting) then new.v = 1; end
-- @commit
-- @case
select trim(rdb$trigger_name), rdb$trigger_sequence, rdb$trigger_type, rdb$trigger_inactive from rdb$triggers where rdb$system_flag = 0 order by 1
-- @case
execute block returns (r varchar(40)) as begin execute statement 'select s from tr where id = 4' into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement 'select count(*) from tr' into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement ('select v from tr where id = ?') (5) into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement ('select v from tr where id = :a and v > :b') (a := 5, b := 0) into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement ('select v from tr where id = :a') (a := 5, b := 0) into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement ('select v from tr where id = :a') (b := 5) into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement ('select v from tr where id = ?') (5, 6) into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement 'select v from tr' into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement 'select v from tr where id = 999' into :r; suspend; end
-- @case
execute block returns (r integer, s varchar(20)) as begin for execute statement 'select id, s from tr order by id' into :r, :s do suspend; end
-- @case
execute block returns (r integer) as begin execute statement 'update tr set v = v where id > 0'; r = row_count; suspend; end
-- @case
execute block as begin execute statement 'insert into lg values (-1, ''es'')'; end
-- @case
execute block returns (r integer) as begin execute statement 'selec 1 from rdb$database' into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement 'select 1, 2 from rdb$database' into :r; suspend; end
-- @case
execute block returns (r integer) as declare q varchar(100) = 'select 7 from rdb$database'; begin execute statement q into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement 'execute block returns (x integer) as begin x = 3; suspend; end' into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement 'select 1 from rdb$database' with autonomous transaction into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement ('select ? from rdb$database') (null) into :r; suspend; end
-- @case
execute block returns (r integer) as begin execute statement null into :r; suspend; end
-- @case
select n, msg from lg where n = -1
