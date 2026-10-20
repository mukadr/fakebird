-- BLOBs (text and binary: functions, comparisons, conversions, NULL and empty values, sorting and grouping),
-- BOOLEAN, OCTETS/UUID values and literals, identifiers (quoting, case, reserved words, lengths) and context
-- variables with deterministic values.
-- @setup
create table b (id integer not null primary key, t blob sub_type text, bn blob sub_type binary, v varchar(10), o char(4) character set octets, f boolean);
insert into b values (1, 'hello world', x'0102', 'abc', x'41424344', true);
insert into b values (2, '', x'', '', x'00', false);
insert into b values (3, null, null, null, null, null);
insert into b values (4, 'Hello', x'ff', 'ABC', x'41', true);
-- @case
select id, t, char_length(t), octet_length(t), bit_length(t) from b order by id
-- @case
select id, bn, octet_length(bn), char_length(bn) from b order by id
-- @case
select id, substring(t from 2 for 3), substring(t from 7), position('o' in t), position('o', t, 6) from b order by id
-- @case
select id, t || '!', '>' || t, t || t, t || v, upper(t), lower(t), trim(t), reverse(t) from b order by id
-- @case
select id, replace(t, 'l', 'L'), overlay(t placing 'XX' from 2 for 3), lpad(t, 13, '*'), rpad(t, 13), left(t, 2), right(t, 2) from b order by id
-- @case
select id, t = 'hello world', t > 'H', t like 'h%', t containing 'WORLD', t starting 'he', t similar to 'h%' from b order by id
-- @case
select id, t is null, t is distinct from '', coalesce(t, 'none'), nullif(t, '') from b order by id
-- @case
select id, cast(t as varchar(5)) from b order by id
-- @case
select id, cast(t as varchar(20)), cast(t as char(12)) from b where id <> 1 order by id
-- @case
select id, cast(v as blob sub_type text), cast(t as blob sub_type binary) from b order by id
-- @case
select id, cast(bn as blob sub_type text) from b where id = 1
-- @case
select id, cast(t as integer) from b where id = 1
-- @case
select cast('12' as blob sub_type text) + 1, cast('2024-01-01' as blob sub_type text) from rdb$database
-- @case
select id from b order by t, id
-- @case
select id from b order by bn desc, id
-- @case
select t, count(*) from b group by t order by 2, 1
-- @case
select distinct t from b order by 1
-- @case
select min(t), max(t), count(t), count(distinct t), list(t, '|') from b
-- @case
select id, t from b where t = (select max(t) from b) order by id
-- @case
select id, hash(t), hash(bn) from b order by id
-- @case
select id, hex_encode(bn), base64_encode(bn), crypt_hash(t using sha256) is null from b order by id
-- @case
select base64_decode('aGVsbG8='), hex_decode('48656c6c6f'), base64_encode('hello'), hex_encode('Hi') from rdb$database
-- @case
select blob_append(t, '+', v) from b order by id
-- @case
select blob_append(null, 'a', null, 'b'), blob_append('x') from rdb$database
-- @case
insert into b (id, t, bn) values (5, 'x', 'binary text')
-- @case
insert into b (id, t) values (6, repeat('ab', 20000))
-- @case
select id, char_length(t), substring(t from 39999) from b where id = 6
-- @case
update b set t = t || 'z' where id = 6
-- @case
select id, octet_length(t), right(t, 3) from b where id = 6
-- @case
select id, f, not f, f and true, f or false, f is true, f is not false, f is unknown, f = true, f <> false, f > false from b order by id
-- @case
select id, cast(f as varchar(5)), cast(f as char(5)), iif(f, 'y', 'n'), case f when true then 1 when false then 0 end from b order by id
-- @case
select cast('true' as boolean), cast('FALSE' as boolean), cast(' True ' as boolean), cast('unknown' as boolean) from rdb$database
-- @case
select cast('yes' as boolean) from rdb$database
-- @case
select cast(1 as boolean) from rdb$database
-- @case
select id from b order by f, id
-- @case
select id from b order by f desc nulls last, id
-- @case
select f, count(*) from b group by f order by f
-- @case
select min(f), max(f), count(distinct f) from b
-- @case
select true, false, unknown, true and unknown, false or unknown, not unknown, unknown is null from rdb$database
-- @case
select (1 = 1) = (2 = 2), (1 = 1) is distinct from (1 = 2), coalesce(null, true), nullif(true, true) from rdb$database
-- @case
select sum(f) from b
-- @case
select f + 1 from b
-- @case
select id, o, octet_length(o), char_length(o), hex_encode(o) from b order by id
-- @case
select id, o = x'41', o = x'41000000', o = 'A', o > x'40', o like x'41%' from b order by id
-- @case
select x'414243', x'', x'0a0B', x'abc' from rdb$database
-- @case
select x'zz' from rdb$database
-- @case
select uuid_to_char(char_to_uuid('A0BF4E45-3029-2A44-D493-4998C9B439A3')), char_to_uuid('a0bf4e45-3029-2a44-d493-4998c9b439a3') from rdb$database
-- @case
select char_to_uuid('not a uuid') from rdb$database
-- @case
select char_length(gen_uuid()), octet_length(gen_uuid()), char_length(uuid_to_char(gen_uuid())), gen_uuid() = gen_uuid() from rdb$database
-- @case
select uuid_to_char(x'000102030405060708090A0B0C0D0E0F') from rdb$database
-- @case
select uuid_to_char('short') from rdb$database
-- @case
select cast(x'41' as varchar(3)), cast('AB' as char(4) character set octets), cast(o as varchar(10) character set utf8) from b order by id
-- @case
create table "Mixed Case" ("Id" integer, "id" integer, "select" varchar(5), x_ integer, "x y" integer)
-- @case
create table t_us (_x integer)
-- @case
create table t_us ($x integer)
-- @commit
-- @case
insert into "Mixed Case" values (1, 2, 'kw', 4, 5)
-- @case
select "Id", "id", "select", x_, "x y" from "Mixed Case"
-- @case
select * from "Mixed Case"
-- @case
select id from "Mixed Case"
-- @case
select "ID" from "Mixed Case"
-- @case
select * from "mixed case"
-- @case
select * from mixed_case
-- @case
create table abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk (a integer)
-- @commit
-- @case
create table abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl (a integer)
-- @case
select a from abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk
-- @case
create table "Ünïcödé" ("çol" integer)
-- @commit
-- @case
select "çol" from "Ünïcödé"
-- @case
create table t_kw (key integer, value integer, "VALUE2" integer, position integer, type integer, count integer)
-- @commit
-- @case
select key, value, position, type, count from t_kw
-- @case
create table t_kw2 (window integer)
-- @case
create table t_kw3 (return integer)
-- @case
create table t_kw4 (offset integer)
-- @case
create table t_kw5 (rows integer)
-- @case
create table t_kw6 (over integer)
-- @case
create table t_kw7 ("" integer)
-- @case
select 1 as "", 2 as "a b" from rdb$database
-- @case
select 1 as select from rdb$database
-- @case
select 1 "Alias", 2 alias2, 3 "ALIAS3" from rdb$database
-- @case
select current_user, user, current_role, char_length(current_connection) > 0, current_transaction > 0 from rdb$database
-- @case
select rdb$get_context('SYSTEM', 'ENGINE_VERSION'), rdb$get_context('SYSTEM', 'CURRENT_USER'), rdb$get_context('SYSTEM', 'CURRENT_ROLE'), rdb$get_context('SYSTEM', 'NETWORK_PROTOCOL') from rdb$database
-- @case
select rdb$get_context('SYSTEM', 'NOSUCH') from rdb$database
-- @case
select rdb$get_context('NOSUCH', 'X') from rdb$database
-- @case
select rdb$set_context('USER_SESSION', 'k', 'v'), rdb$get_context('USER_SESSION', 'k'), rdb$set_context('USER_SESSION', 'k', null), rdb$get_context('USER_SESSION', 'k') from rdb$database
-- @case
select rdb$set_context('USER_TRANSACTION', 't1', 42), rdb$get_context('USER_TRANSACTION', 't1') from rdb$database
-- @case
select rdb$set_context('SYSTEM', 'x', 'y') from rdb$database
-- @case
select rdb$get_context('USER_SESSION', 'missing') from rdb$database
-- @case
select current_date = cast('today' as date), current_date - cast('yesterday' as date), cast('tomorrow' as date) - current_date from rdb$database
-- @case
select current_timestamp >= current_date, localtimestamp >= current_date, current_timestamp = current_timestamp from rdb$database
-- @case
select char_length(cast(localtimestamp as varchar(30))), char_length(cast(localtime as varchar(20))) from rdb$database
-- @case
select row_count from rdb$database
-- @case
select sqlcode from rdb$database
-- @case
select inserting from rdb$database
-- @case
select new.id from rdb$database
-- @case params: [7, "param text", "bin"]
insert into b (id, t, bn) values (?, ?, ?)
-- @case params: [7]
select id, t, bn from b where id = ?
-- @case params: ["param text"]
select id from b where t = ?
-- @case params: [8, true]
insert into b (id, f) values (?, ?)
-- @case params: [9, "false"]
insert into b (id, f) values (?, ?)
-- @case params: [true]
select id, f from b where f = ? order by id
-- @case params: [10, "xyz"]
insert into b (id, o) values (?, ?)
-- @case params: [10]
select id, o from b where id = ?
