-- Transactions between two connections: visibility under READ COMMITTED (READ CONSISTENCY) and SNAPSHOT,
-- NOWAIT and lock-timeout conflicts on updates, deletes, unique keys and foreign keys, WITH LOCK, savepoints,
-- read-only transactions, sequences outside transactions, and the isolation level as the context reports it.
-- @setup
create table acc (id integer not null primary key, bal integer, owner varchar(10));
create table par (id integer not null primary key);
create table chi (id integer not null primary key, pid integer references par (id));
create sequence sq;
insert into acc values (1, 100, 'ann');
insert into acc values (2, 50, 'bob');
insert into par values (1);
insert into par values (2);
-- @conn A
-- @tx read_committed nowait
-- @case
update acc set bal = bal + 1 where id = 1
-- @case
select id, bal from acc order by id
-- @conn B
-- @tx read_committed nowait
-- @case
select id, bal from acc order by id
-- @case
update acc set bal = 0 where id = 1
-- @case
delete from acc where id = 1
-- @case
update acc set bal = bal + 10 where id = 2
-- @case
select rdb$get_context('SYSTEM', 'ISOLATION_LEVEL') from rdb$database
-- @conn A
-- @case
update acc set bal = 7 where id = 2
-- @case
select id, bal from acc order by id
-- @commit
-- @conn B
-- @case
select id, bal from acc order by id
-- @case
update acc set bal = bal + 1 where id = 1
-- @commit
-- @case
select id, bal from acc order by id
-- @commit
-- snapshot reads keep the view of their start
-- @conn A
-- @tx snapshot nowait
-- @case
select id, bal from acc order by id
-- @case
select rdb$get_context('SYSTEM', 'ISOLATION_LEVEL') from rdb$database
-- @conn B
-- @tx read_committed nowait
-- @case
update acc set bal = 500 where id = 1
-- @case
insert into acc values (3, 30, 'cid')
-- @commit
-- @conn A
-- @case
select id, bal from acc order by id
-- @case
select count(*) from acc
-- @case
update acc set bal = 1 where id = 1
-- @case
update acc set bal = 1 where id = 2
-- @case
delete from acc where id = 3
-- @case
insert into acc values (3, 33, 'dup')
-- @commit
-- @case
select id, bal, owner from acc order by id
-- @commit
-- uncommitted keys and references
-- @conn A
-- @tx read_committed nowait
-- @case
insert into acc values (10, 0, 'new')
-- @case
delete from par where id = 2
-- @conn B
-- @tx read_committed nowait
-- @case
insert into acc values (10, 0, 'other')
-- @case
select count(*) from acc where id = 10
-- @case
insert into chi values (1, 2)
-- @case
insert into chi values (2, 1)
-- @conn A
-- @case
delete from par where id = 1
-- @rollback
-- @conn B
-- @case
insert into acc values (10, 0, 'other')
-- @case
insert into chi values (3, 2)
-- @commit
-- @case
select id, pid from chi order by id
-- @case
select id, owner from acc where id = 10
-- @commit
-- WITH LOCK
-- @conn A
-- @tx read_committed nowait
-- @case
select id from acc where id = 2 with lock
-- @conn B
-- @tx read_committed nowait
-- @case
update acc set bal = 2 where id = 2
-- @case
select id from acc where id = 2 with lock
-- @case
select id, bal from acc where id = 2
-- @conn A
-- @commit
-- @conn B
-- @case
update acc set bal = 2 where id = 2
-- @commit
-- lock timeout
-- @conn A
-- @tx read_committed wait
-- @case
update acc set bal = 3 where id = 2
-- @conn B
-- @tx read_committed timeout 1
-- @case
update acc set bal = 4 where id = 2
-- @conn A
-- @rollback
-- @conn B
-- @rollback
-- savepoints
-- @conn A
-- @tx read_committed nowait
-- @case
insert into acc values (20, 1, 's1')
-- @case
savepoint sp1
-- @case
insert into acc values (21, 1, 's2')
-- @case
savepoint sp2
-- @case
update acc set bal = 99 where id >= 20
-- @case
rollback to savepoint sp2
-- @case
select id, bal from acc where id >= 20 order by id
-- @case
rollback to savepoint sp1
-- @case
select id, bal from acc where id >= 20 order by id
-- @case
rollback to savepoint sp2
-- @case
savepoint sp1
-- @case
insert into acc values (22, 1, 's3')
-- @case
release savepoint sp1
-- @case
rollback to savepoint sp1
-- @case
release savepoint nosuch
-- @case
select id, bal from acc where id >= 20 order by id
-- @rollback
-- read-only transactions and sequences
-- @conn A
-- @tx read_committed read_only
-- @case
select count(*) from acc
-- @case
insert into acc values (30, 0, 'ro')
-- @case
update acc set bal = 0
-- @case
select gen_id(sq, 1) from rdb$database
-- @rollback
-- @conn B
-- @tx read_committed nowait
-- @case
select next value for sq from rdb$database
-- @rollback
-- @case
select gen_id(sq, 0) from rdb$database
-- @case
select rdb$get_context('SYSTEM', 'READ_ONLY'), rdb$get_context('SYSTEM', 'ISOLATION_LEVEL'), rdb$get_context('SYSTEM', 'LOCK_TIMEOUT') from rdb$database
-- @tx snapshot read_only
-- @case
select rdb$get_context('SYSTEM', 'READ_ONLY'), rdb$get_context('SYSTEM', 'ISOLATION_LEVEL'), rdb$get_context('SYSTEM', 'LOCK_TIMEOUT') from rdb$database
-- @tx consistency
-- @case
select rdb$get_context('SYSTEM', 'ISOLATION_LEVEL'), rdb$get_context('SYSTEM', 'LOCK_TIMEOUT') from rdb$database
-- @tx read_committed timeout 7
-- @case
select rdb$get_context('SYSTEM', 'ISOLATION_LEVEL'), rdb$get_context('SYSTEM', 'LOCK_TIMEOUT') from rdb$database
-- @rollback
