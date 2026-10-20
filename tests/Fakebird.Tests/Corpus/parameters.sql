-- Parameters in expressions: typed from their context (assignment target, other side of a comparison),
-- and the errors Firebird raises when an operator gets a parameter of unknown type.
-- @setup
create table src (id integer not null primary key, n2 numeric(9,2), i integer, b bigint, dp double precision, dt date, ts timestamp, s varchar(10));
insert into src values (1, 1.25, 7, 3000000000, 1.5, '2024-01-01', '2024-01-01 10:00:00', 'abc');
-- @case params: [2]
select n2 + ? from src
-- @case params: [2]
select ? + n2 from src
-- @case params: [2]
select n2 - ? from src
-- @case params: [2]
select n2 * ? from src
-- @case params: [2]
select ? * n2 from src
-- @case params: [2]
select n2 / ? from src
-- @case params: [2]
select i * ? from src
-- @case params: [2]
select b * ? from src
-- @case params: [2]
select dp * ? from src
-- @case params: [2]
select i / ? from src
-- @case params: [2]
select ? / i from src
-- @case params: [2]
select ? * 2 from src
-- @case params: [2]
select 2 * ? from src
-- @case params: [2]
select ? + 2 from src
-- @case params: [2, 3]
select ? + ? from src
-- @case params: [2, 3]
select ? * ? from src
-- @case params: [2]
select dt + ? from src
-- @case params: [2]
select dt - ? from src
-- @case params: ["2024-01-02"]
select ? - dt from src
-- @case params: [2]
select ts + ? from src
-- @case params: [2]
select ? + dt from src
-- @case params: [2]
select -? from src
-- @case params: [2]
select i + -? from src
-- @case params: [2]
select cast(? as integer) * n2 from src
-- @case params: [2]
select (n2 + 1) * ? from src
-- @case params: ["x"]
select s || ? from src
-- @case params: ["x"]
select ? || s from src
-- @case params: [2]
select abs(?) from src
-- @case params: [2]
select coalesce(?, n2) from src
-- @case params: [2]
select coalesce(n2, ?) from src
-- @case params: [2.5, 2.5, 2.5]
select n2 + ?, n2 - ?, ? - n2 from src
-- @case params: [2]
update src set n2 = n2 + ? where id = 1 returning n2
-- @case params: [2]
update src set n2 = n2 * ? where id = 1 returning n2
-- @case params: [2]
update src set i = i * ? where id = 1 returning i
-- @case params: ["x"]
update src set s = s || ? where id = 1 returning s
-- @case params: [2]
select id from src where i > ? + 1
-- @case params: [2]
select id from src where i > ? * 2
-- @case params: [2]
select id from src where i * ? > 1
-- @case params: [2]
select id from src where i + ? > 1
-- @case params: [2]
select id from src where n2 = ? / 2
-- @case params: [9, 2]
insert into src (id, i) values (?, 1 + ?) returning i
-- @case params: [10, 3]
insert into src (id, i) values (?, ? * 2) returning i
-- @case params: ["x"]
select id from src where s = ? || 'bc'
-- @case params: ["a"]
select id from src where s like ? || '%'
-- @case params: [2]
select id from src where dt > current_date - ?
-- @case params: [2]
select iif(? > 1, 'y', 'n') from src
-- @case params: [2, 2]
select n2 between ? and ? * 2 from src
