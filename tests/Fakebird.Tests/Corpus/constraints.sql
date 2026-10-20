-- Constraints: primary/unique keys (NULLs, multi-column, case-insensitive collations), foreign keys with every
-- action, CHECK (NULL passes, referencing several columns), NOT NULL, domains with defaults/checks/NOT NULL,
-- the violation errors and their arguments, and constraints added or dropped on tables with data.
-- @setup
create domain dpos as integer check (value > 0);
create domain dnn as varchar(5) default 'dflt' not null;
create domain dci as varchar(10) character set utf8 collate unicode_ci;
create table pk1 (id integer not null primary key, v varchar(5));
create table pk2 (a integer not null, b varchar(3) not null, v integer, constraint pk2_pk primary key (a, b));
create table uq (id integer not null, u varchar(5), u2 integer, w dci, constraint uq_u unique (u), unique (u2, w));
create table ci (id integer not null primary key, k varchar(5) character set utf8 unique collate unicode_ci);
create table par (id integer not null primary key, code varchar(3) not null unique);
create table chc (id integer not null primary key, pid integer references par (id) on delete cascade on update cascade);
create table chn (id integer not null primary key, pid integer references par (id) on delete set null on update set null);
create table chd (id integer not null primary key, pid integer default 1 references par (id) on delete set default);
create table chx (id integer not null primary key, pcode varchar(3), constraint fk_code foreign key (pcode) references par (code));
create table ck (id integer not null primary key, lo integer, hi integer, p dpos, n dnn, constraint ck_range check (lo <= hi), check (hi < 100));
create table self (id integer not null primary key, parent integer references self (id) on delete cascade);
insert into par values (1, 'a');
insert into par values (2, 'b');
insert into par values (3, 'c');
-- @case
insert into pk1 values (1, 'a')
-- @case
insert into pk1 values (1, 'b')
-- @case
insert into pk1 values (null, 'b')
-- @case
update pk1 set id = 2 where id = 1
-- @case
insert into pk1 values (1, 'c')
-- @case
update pk1 set id = id + 1
-- @case
update pk1 set id = 3 - id
-- @case
select id, v from pk1 order by id
-- @case
insert into pk2 values (1, 'x', 1)
-- @case
insert into pk2 values (1, 'y', 1)
-- @case
insert into pk2 values (1, 'x', 2)
-- @case
insert into pk2 values (1, 'x ', 2)
-- @case
insert into pk2 values (1, null, 2)
-- @case
insert into uq values (1, 'a', null, null)
-- @case
insert into uq values (2, 'a', null, null)
-- @case
insert into uq values (3, null, null, null)
-- @case
insert into uq values (4, null, null, null)
-- @case
insert into uq values (5, 'b', 1, 'X')
-- @case
insert into uq values (6, 'c', 1, 'x')
-- @case
insert into uq values (7, 'd', 1, null)
-- @case
insert into uq values (8, 'e', 1, null)
-- @case
insert into uq values (9, 'a ', null, null)
-- @case
insert into ci values (1, 'Abc')
-- @case
insert into ci values (2, 'aBC')
-- @case
insert into ci values (3, 'abc ')
-- @case
insert into ci values (4, 'abd')
-- @case
update ci set k = 'ABD' where id = 1
-- @case
update ci set k = 'ABC' where id = 1
-- @case
insert into chc values (1, 1)
-- @case
insert into chc values (2, 9)
-- @case
insert into chc values (3, null)
-- @case
insert into chn values (1, 2)
-- @case
insert into chd values (1, 3)
-- @case
insert into chd (id) values (2)
-- @case
insert into chx values (1, 'b')
-- @case
insert into chx values (2, 'z')
-- @case
insert into chx values (3, 'b ')
-- @case
update par set id = 10 where id = 1
-- @case
select id, pid from chc order by id
-- @case
update par set id = 20 where id = 2
-- @case
select id, pid from chn order by id
-- @case
update par set code = 'bb' where id = 20
-- @case
delete from par where id = 10
-- @case
select id, pid from chc order by id
-- @case
delete from par where id = 3
-- @case
select id, pid from chd order by id
-- @case
update chc set pid = 99 where id = 3
-- @case
update chx set pcode = 'q'
-- @case
delete from par where code = 'bb'
-- @case
insert into par values (1, 'a')
-- @case
delete from par where id = 1
-- @case
select id, pid from chd order by id
-- @case
insert into self values (1, null)
-- @case
insert into self values (2, 1)
-- @case
insert into self values (3, 2)
-- @case
insert into self values (4, 4)
-- @case
insert into self values (5, 6)
-- @case
delete from self where id = 1
-- @case
select id, parent from self order by id
-- @case
insert into ck (id, lo, hi, p) values (1, 1, 2, 1)
-- @case
insert into ck (id, lo, hi, p) values (2, 3, 2, 1)
-- @case
insert into ck (id, lo, hi, p) values (3, 1, 200, 1)
-- @case
insert into ck (id, lo, hi, p) values (4, null, 2, 1)
-- @case
insert into ck (id, lo, hi, p) values (5, 1, null, 1)
-- @case
insert into ck (id, p) values (6, 0)
-- @case
insert into ck (id, p) values (7, null)
-- @case
insert into ck (id, p, n) values (8, 1, null)
-- @case
insert into ck (id) values (9)
-- @case
update ck set hi = 0 where id = 1
-- @case
update ck set lo = 5, hi = 6 where id = 1
-- @case
update ck set p = -1
-- @case
update ck set n = null where id = 9
-- @case
update ck set n = 'toolong' where id = 9
-- @case
select id, lo, hi, p, n from ck order by id
-- @case
alter table pk1 add constraint pk1_v unique (v)
-- @commit
-- @case
insert into pk1 values (5, 'b')
-- @case
alter table pk1 drop constraint pk1_v
-- @commit
-- @case
insert into pk1 values (5, 'b')
-- @case
alter table pk1 add constraint pk1_v unique (v)
-- @commit
-- @case
alter table ck add constraint ck_lo check (lo > 3)
-- @commit
-- @case
alter table ck add constraint ck_lo check (lo > 0)
-- @commit
-- @case
insert into ck (id, lo, p) values (10, 0, 1)
-- @case
alter table ck drop constraint ck_lo
-- @commit
-- @case
insert into ck (id, lo, p) values (10, 0, 1)
-- @case
alter table ck drop constraint nope
-- @commit
-- @case
alter table chx add constraint fk2 foreign key (pcode) references par (code) on delete cascade
-- @commit
-- @case
alter table chc drop constraint integ_99
-- @commit
-- @case
alter table pk1 add constraint pk1_v2 unique (id)
-- @commit
-- @case
alter table pk1 add primary key (v)
-- @commit
-- @case
alter table uq add constraint uq_u unique (u2)
-- @commit
-- @case
alter table uq alter column u type varchar(2)
-- @commit
-- @case
alter table uq alter column u type varchar(9)
-- @commit
-- @case
insert into uq values (20, 'abcdefg', null, null)
-- @case
drop table par
-- @commit
-- @case
alter table par drop code
-- @commit
-- @case
alter table par drop constraint integ_9
-- @commit
-- @case
delete from chc
-- @case
delete from chn
-- @case
delete from chd
-- @case
delete from chx
-- @case
delete from par
-- @case
select count(*) from par
-- @case
select trim(rdb$constraint_name), trim(rdb$constraint_type), trim(rdb$relation_name), trim(rdb$index_name) from rdb$relation_constraints where rdb$relation_name in ('PK1', 'PK2', 'UQ', 'CI', 'PAR', 'CHC', 'CHN', 'CHD', 'CHX', 'CK', 'SELF') order by 3, 1
-- @case
select trim(rdb$constraint_name), trim(rdb$const_name_uq), rdb$match_option, trim(rdb$update_rule), trim(rdb$delete_rule) from rdb$ref_constraints order by 1
-- @case
select trim(rdb$constraint_name), trim(rdb$trigger_name) from rdb$check_constraints order by 2
-- @case
select trim(rdb$index_name), trim(rdb$relation_name), rdb$unique_flag, rdb$segment_count, trim(rdb$foreign_key) from rdb$indices where rdb$system_flag = 0 order by 1
-- @case
select trim(rdb$index_name), trim(rdb$field_name), rdb$field_position from rdb$index_segments where rdb$index_name not starting with 'RDB$' order by 1, 3
