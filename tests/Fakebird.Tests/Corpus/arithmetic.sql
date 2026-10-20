-- Dialect 3 arithmetic: result types, values, overflow and division rules for every numeric type pair.
-- @setup
create table nums (id integer not null primary key, sm smallint, i integer, b bigint, n42 numeric(4,2), n92 numeric(9,2),
    n184 numeric(18,4), d52 decimal(5,2), f float, dp double precision);
insert into nums values (1, 7, 100, 3000000000, 12.34, 1234567.89, 12345678901234.5678, 123.45, 1.5, 2.25);
insert into nums values (2, -3, -7, -9, -0.05, -0.01, -0.0001, -0.10, -0.25, -1e-3);
insert into nums values (3, 32767, 2147483647, 9223372036854775807, 99.99, 9999999.99, 99999999999999.9999, 999.99, 3.4e38, 1.7e308);
insert into nums values (4, -32768, -2147483648, -9223372036854775808, -99.99, -9999999.99, -99999999999999.9999, -999.99, -3.4e38, -1.7e308);
insert into nums values (5, 0, 0, 0, 0, 0, 0, 0, 0, 0);
insert into nums values (6, null, null, null, null, null, null, null, null, null);
-- @case
select id, sm + sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm - sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm * sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm / sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm + i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm - i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm * i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm / i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm + b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm - b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm * b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm / b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm + n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm - n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm * n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm / n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm + n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm - n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm * n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm / n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm + n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm - n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm * n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm / n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm + d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm - d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm * d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm / d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm + f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm - f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm * f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm / f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm + dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm - dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm * dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, sm / dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i + sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i - sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i * sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i / sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i + i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i - i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i * i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i / i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i + b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i - b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i * b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i / b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i + n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i - n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i * n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i / n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i + n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i - n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i * n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i / n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i + n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i - n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i * n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i / n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i + d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i - d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i * d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i / d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i + f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i - f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i * f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i / f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i + dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i - dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i * dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, i / dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b + sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b - sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b * sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b / sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b + i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b - i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b * i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b / i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b + b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b - b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b * b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b / b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b + n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b - n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b * n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b / n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b + n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b - n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b * n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b / n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b + n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b - n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b * n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b / n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b + d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b - d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b * d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b / d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b + f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b - f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b * f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b / f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b + dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b - dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b * dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, b / dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 + sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 - sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 * sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 / sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 + i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 - i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 * i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 / i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 + b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 - b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 * b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 / b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 + n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 - n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 * n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 / n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 + n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 - n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 * n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 / n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 + n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 - n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 * n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 / n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 + d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 - d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 * d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 / d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 + f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 - f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 * f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 / f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 + dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 - dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 * dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n42 / dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 + sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 - sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 * sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 / sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 + i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 - i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 * i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 / i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 + b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 - b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 * b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 / b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 + n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 - n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 * n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 / n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 + n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 - n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 * n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 / n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 + n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 - n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 * n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 / n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 + d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 - d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 * d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 / d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 + f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 - f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 * f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 / f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 + dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 - dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 * dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n92 / dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 + sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 - sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 * sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 / sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 + i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 - i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 * i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 / i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 + b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 - b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 * b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 / b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 + n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 - n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 * n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 / n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 + n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 - n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 * n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 / n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 + n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 - n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 * n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 / n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 + d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 - d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 * d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 / d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 + f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 - f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 * f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 / f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 + dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 - dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 * dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, n184 / dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 + sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 - sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 * sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 / sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 + i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 - i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 * i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 / i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 + b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 - b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 * b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 / b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 + n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 - n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 * n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 / n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 + n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 - n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 * n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 / n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 + n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 - n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 * n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 / n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 + d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 - d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 * d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 / d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 + f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 - f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 * f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 / f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 + dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 - dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 * dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, d52 / dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f + sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f - sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f * sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f / sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f + i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f - i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f * i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f / i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f + b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f - b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f * b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f / b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f + n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f - n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f * n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f / n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f + n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f - n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f * n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f / n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f + n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f - n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f * n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f / n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f + d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f - d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f * d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f / d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f + f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f - f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f * f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f / f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f + dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f - dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f * dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, f / dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp + sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp - sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp * sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp / sm from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp + i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp - i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp * i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp / i from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp + b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp - b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp * b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp / b from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp + n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp - n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp * n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp / n42 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp + n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp - n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp * n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp / n92 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp + n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp - n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp * n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp / n184 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp + d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp - d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp * d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp / d52 from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp + f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp - f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp * f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp / f from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp + dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp - dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp * dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select id, dp / dp from nums where id in (1, 2, 5, 6) order by id
-- @case
select sm + sm from nums where id = 3
-- @case
select sm + sm from nums where id = 4
-- @case
select sm - sm from nums where id = 3
-- @case
select sm - sm from nums where id = 4
-- @case
select sm * sm from nums where id = 3
-- @case
select sm * sm from nums where id = 4
-- @case
select sm + i from nums where id = 3
-- @case
select sm + i from nums where id = 4
-- @case
select sm - i from nums where id = 3
-- @case
select sm - i from nums where id = 4
-- @case
select sm * i from nums where id = 3
-- @case
select sm * i from nums where id = 4
-- @case
select sm + b from nums where id = 3
-- @case
select sm + b from nums where id = 4
-- @case
select sm - b from nums where id = 3
-- @case
select sm - b from nums where id = 4
-- @case
select sm * b from nums where id = 3
-- @case
select sm * b from nums where id = 4
-- @case
select sm + n42 from nums where id = 3
-- @case
select sm + n42 from nums where id = 4
-- @case
select sm - n42 from nums where id = 3
-- @case
select sm - n42 from nums where id = 4
-- @case
select sm * n42 from nums where id = 3
-- @case
select sm * n42 from nums where id = 4
-- @case
select sm + n92 from nums where id = 3
-- @case
select sm + n92 from nums where id = 4
-- @case
select sm - n92 from nums where id = 3
-- @case
select sm - n92 from nums where id = 4
-- @case
select sm * n92 from nums where id = 3
-- @case
select sm * n92 from nums where id = 4
-- @case
select sm + n184 from nums where id = 3
-- @case
select sm + n184 from nums where id = 4
-- @case
select sm - n184 from nums where id = 3
-- @case
select sm - n184 from nums where id = 4
-- @case
select sm * n184 from nums where id = 3
-- @case
select sm * n184 from nums where id = 4
-- @case
select sm + d52 from nums where id = 3
-- @case
select sm + d52 from nums where id = 4
-- @case
select sm - d52 from nums where id = 3
-- @case
select sm - d52 from nums where id = 4
-- @case
select sm * d52 from nums where id = 3
-- @case
select sm * d52 from nums where id = 4
-- @case
select sm + f from nums where id = 3
-- @case
select sm + f from nums where id = 4
-- @case
select sm - f from nums where id = 3
-- @case
select sm - f from nums where id = 4
-- @case
select sm * f from nums where id = 3
-- @case
select sm * f from nums where id = 4
-- @case
select sm + dp from nums where id = 3
-- @case
select sm + dp from nums where id = 4
-- @case
select sm - dp from nums where id = 3
-- @case
select sm - dp from nums where id = 4
-- @case
select sm * dp from nums where id = 3
-- @case
select sm * dp from nums where id = 4
-- @case
select i + sm from nums where id = 3
-- @case
select i + sm from nums where id = 4
-- @case
select i - sm from nums where id = 3
-- @case
select i - sm from nums where id = 4
-- @case
select i * sm from nums where id = 3
-- @case
select i * sm from nums where id = 4
-- @case
select i + i from nums where id = 3
-- @case
select i + i from nums where id = 4
-- @case
select i - i from nums where id = 3
-- @case
select i - i from nums where id = 4
-- @case
select i * i from nums where id = 3
-- @case
select i * i from nums where id = 4
-- @case
select i + b from nums where id = 3
-- @case
select i + b from nums where id = 4
-- @case
select i - b from nums where id = 3
-- @case
select i - b from nums where id = 4
-- @case
select i * b from nums where id = 3
-- @case
select i * b from nums where id = 4
-- @case
select i + n42 from nums where id = 3
-- @case
select i + n42 from nums where id = 4
-- @case
select i - n42 from nums where id = 3
-- @case
select i - n42 from nums where id = 4
-- @case
select i * n42 from nums where id = 3
-- @case
select i * n42 from nums where id = 4
-- @case
select i + n92 from nums where id = 3
-- @case
select i + n92 from nums where id = 4
-- @case
select i - n92 from nums where id = 3
-- @case
select i - n92 from nums where id = 4
-- @case
select i * n92 from nums where id = 3
-- @case
select i * n92 from nums where id = 4
-- @case
select i + n184 from nums where id = 3
-- @case
select i + n184 from nums where id = 4
-- @case
select i - n184 from nums where id = 3
-- @case
select i - n184 from nums where id = 4
-- @case
select i * n184 from nums where id = 3
-- @case
select i * n184 from nums where id = 4
-- @case
select i + d52 from nums where id = 3
-- @case
select i + d52 from nums where id = 4
-- @case
select i - d52 from nums where id = 3
-- @case
select i - d52 from nums where id = 4
-- @case
select i * d52 from nums where id = 3
-- @case
select i * d52 from nums where id = 4
-- @case
select i + f from nums where id = 3
-- @case
select i + f from nums where id = 4
-- @case
select i - f from nums where id = 3
-- @case
select i - f from nums where id = 4
-- @case
select i * f from nums where id = 3
-- @case
select i * f from nums where id = 4
-- @case
select i + dp from nums where id = 3
-- @case
select i + dp from nums where id = 4
-- @case
select i - dp from nums where id = 3
-- @case
select i - dp from nums where id = 4
-- @case
select i * dp from nums where id = 3
-- @case
select i * dp from nums where id = 4
-- @case
select b + sm from nums where id = 3
-- @case
select b + sm from nums where id = 4
-- @case
select b - sm from nums where id = 3
-- @case
select b - sm from nums where id = 4
-- @case
select b * sm from nums where id = 3
-- @case
select b * sm from nums where id = 4
-- @case
select b + i from nums where id = 3
-- @case
select b + i from nums where id = 4
-- @case
select b - i from nums where id = 3
-- @case
select b - i from nums where id = 4
-- @case
select b * i from nums where id = 3
-- @case
select b * i from nums where id = 4
-- @case
select b + b from nums where id = 3
-- @case
select b + b from nums where id = 4
-- @case
select b - b from nums where id = 3
-- @case
select b - b from nums where id = 4
-- @case
select b * b from nums where id = 3
-- @case
select b * b from nums where id = 4
-- @case
select b + n42 from nums where id = 3
-- @case
select b + n42 from nums where id = 4
-- @case
select b - n42 from nums where id = 3
-- @case
select b - n42 from nums where id = 4
-- @case
select b * n42 from nums where id = 3
-- @case
select b * n42 from nums where id = 4
-- @case
select b + n92 from nums where id = 3
-- @case
select b + n92 from nums where id = 4
-- @case
select b - n92 from nums where id = 3
-- @case
select b - n92 from nums where id = 4
-- @case
select b * n92 from nums where id = 3
-- @case
select b * n92 from nums where id = 4
-- @case
select b + n184 from nums where id = 3
-- @case
select b + n184 from nums where id = 4
-- @case
select b - n184 from nums where id = 3
-- @case
select b - n184 from nums where id = 4
-- @case
select b * n184 from nums where id = 3
-- @case
select b * n184 from nums where id = 4
-- @case
select b + d52 from nums where id = 3
-- @case
select b + d52 from nums where id = 4
-- @case
select b - d52 from nums where id = 3
-- @case
select b - d52 from nums where id = 4
-- @case
select b * d52 from nums where id = 3
-- @case
select b * d52 from nums where id = 4
-- @case
select b + f from nums where id = 3
-- @case
select b + f from nums where id = 4
-- @case
select b - f from nums where id = 3
-- @case
select b - f from nums where id = 4
-- @case
select b * f from nums where id = 3
-- @case
select b * f from nums where id = 4
-- @case
select b + dp from nums where id = 3
-- @case
select b + dp from nums where id = 4
-- @case
select b - dp from nums where id = 3
-- @case
select b - dp from nums where id = 4
-- @case
select b * dp from nums where id = 3
-- @case
select b * dp from nums where id = 4
-- @case
select n42 + sm from nums where id = 3
-- @case
select n42 + sm from nums where id = 4
-- @case
select n42 - sm from nums where id = 3
-- @case
select n42 - sm from nums where id = 4
-- @case
select n42 * sm from nums where id = 3
-- @case
select n42 * sm from nums where id = 4
-- @case
select n42 + i from nums where id = 3
-- @case
select n42 + i from nums where id = 4
-- @case
select n42 - i from nums where id = 3
-- @case
select n42 - i from nums where id = 4
-- @case
select n42 * i from nums where id = 3
-- @case
select n42 * i from nums where id = 4
-- @case
select n42 + b from nums where id = 3
-- @case
select n42 + b from nums where id = 4
-- @case
select n42 - b from nums where id = 3
-- @case
select n42 - b from nums where id = 4
-- @case
select n42 * b from nums where id = 3
-- @case
select n42 * b from nums where id = 4
-- @case
select n42 + n42 from nums where id = 3
-- @case
select n42 + n42 from nums where id = 4
-- @case
select n42 - n42 from nums where id = 3
-- @case
select n42 - n42 from nums where id = 4
-- @case
select n42 * n42 from nums where id = 3
-- @case
select n42 * n42 from nums where id = 4
-- @case
select n42 + n92 from nums where id = 3
-- @case
select n42 + n92 from nums where id = 4
-- @case
select n42 - n92 from nums where id = 3
-- @case
select n42 - n92 from nums where id = 4
-- @case
select n42 * n92 from nums where id = 3
-- @case
select n42 * n92 from nums where id = 4
-- @case
select n42 + n184 from nums where id = 3
-- @case
select n42 + n184 from nums where id = 4
-- @case
select n42 - n184 from nums where id = 3
-- @case
select n42 - n184 from nums where id = 4
-- @case
select n42 * n184 from nums where id = 3
-- @case
select n42 * n184 from nums where id = 4
-- @case
select n42 + d52 from nums where id = 3
-- @case
select n42 + d52 from nums where id = 4
-- @case
select n42 - d52 from nums where id = 3
-- @case
select n42 - d52 from nums where id = 4
-- @case
select n42 * d52 from nums where id = 3
-- @case
select n42 * d52 from nums where id = 4
-- @case
select n42 + f from nums where id = 3
-- @case
select n42 + f from nums where id = 4
-- @case
select n42 - f from nums where id = 3
-- @case
select n42 - f from nums where id = 4
-- @case
select n42 * f from nums where id = 3
-- @case
select n42 * f from nums where id = 4
-- @case
select n42 + dp from nums where id = 3
-- @case
select n42 + dp from nums where id = 4
-- @case
select n42 - dp from nums where id = 3
-- @case
select n42 - dp from nums where id = 4
-- @case
select n42 * dp from nums where id = 3
-- @case
select n42 * dp from nums where id = 4
-- @case
select n92 + sm from nums where id = 3
-- @case
select n92 + sm from nums where id = 4
-- @case
select n92 - sm from nums where id = 3
-- @case
select n92 - sm from nums where id = 4
-- @case
select n92 * sm from nums where id = 3
-- @case
select n92 * sm from nums where id = 4
-- @case
select n92 + i from nums where id = 3
-- @case
select n92 + i from nums where id = 4
-- @case
select n92 - i from nums where id = 3
-- @case
select n92 - i from nums where id = 4
-- @case
select n92 * i from nums where id = 3
-- @case
select n92 * i from nums where id = 4
-- @case
select n92 + b from nums where id = 3
-- @case
select n92 + b from nums where id = 4
-- @case
select n92 - b from nums where id = 3
-- @case
select n92 - b from nums where id = 4
-- @case
select n92 * b from nums where id = 3
-- @case
select n92 * b from nums where id = 4
-- @case
select n92 + n42 from nums where id = 3
-- @case
select n92 + n42 from nums where id = 4
-- @case
select n92 - n42 from nums where id = 3
-- @case
select n92 - n42 from nums where id = 4
-- @case
select n92 * n42 from nums where id = 3
-- @case
select n92 * n42 from nums where id = 4
-- @case
select n92 + n92 from nums where id = 3
-- @case
select n92 + n92 from nums where id = 4
-- @case
select n92 - n92 from nums where id = 3
-- @case
select n92 - n92 from nums where id = 4
-- @case
select n92 * n92 from nums where id = 3
-- @case
select n92 * n92 from nums where id = 4
-- @case
select n92 + n184 from nums where id = 3
-- @case
select n92 + n184 from nums where id = 4
-- @case
select n92 - n184 from nums where id = 3
-- @case
select n92 - n184 from nums where id = 4
-- @case
select n92 * n184 from nums where id = 3
-- @case
select n92 * n184 from nums where id = 4
-- @case
select n92 + d52 from nums where id = 3
-- @case
select n92 + d52 from nums where id = 4
-- @case
select n92 - d52 from nums where id = 3
-- @case
select n92 - d52 from nums where id = 4
-- @case
select n92 * d52 from nums where id = 3
-- @case
select n92 * d52 from nums where id = 4
-- @case
select n92 + f from nums where id = 3
-- @case
select n92 + f from nums where id = 4
-- @case
select n92 - f from nums where id = 3
-- @case
select n92 - f from nums where id = 4
-- @case
select n92 * f from nums where id = 3
-- @case
select n92 * f from nums where id = 4
-- @case
select n92 + dp from nums where id = 3
-- @case
select n92 + dp from nums where id = 4
-- @case
select n92 - dp from nums where id = 3
-- @case
select n92 - dp from nums where id = 4
-- @case
select n92 * dp from nums where id = 3
-- @case
select n92 * dp from nums where id = 4
-- @case
select n184 + sm from nums where id = 3
-- @case
select n184 + sm from nums where id = 4
-- @case
select n184 - sm from nums where id = 3
-- @case
select n184 - sm from nums where id = 4
-- @case
select n184 * sm from nums where id = 3
-- @case
select n184 * sm from nums where id = 4
-- @case
select n184 + i from nums where id = 3
-- @case
select n184 + i from nums where id = 4
-- @case
select n184 - i from nums where id = 3
-- @case
select n184 - i from nums where id = 4
-- @case
select n184 * i from nums where id = 3
-- @case
select n184 * i from nums where id = 4
-- @case
select n184 + b from nums where id = 3
-- @case
select n184 + b from nums where id = 4
-- @case
select n184 - b from nums where id = 3
-- @case
select n184 - b from nums where id = 4
-- @case
select n184 * b from nums where id = 3
-- @case
select n184 * b from nums where id = 4
-- @case
select n184 + n42 from nums where id = 3
-- @case
select n184 + n42 from nums where id = 4
-- @case
select n184 - n42 from nums where id = 3
-- @case
select n184 - n42 from nums where id = 4
-- @case
select n184 * n42 from nums where id = 3
-- @case
select n184 * n42 from nums where id = 4
-- @case
select n184 + n92 from nums where id = 3
-- @case
select n184 + n92 from nums where id = 4
-- @case
select n184 - n92 from nums where id = 3
-- @case
select n184 - n92 from nums where id = 4
-- @case
select n184 * n92 from nums where id = 3
-- @case
select n184 * n92 from nums where id = 4
-- @case
select n184 + n184 from nums where id = 3
-- @case
select n184 + n184 from nums where id = 4
-- @case
select n184 - n184 from nums where id = 3
-- @case
select n184 - n184 from nums where id = 4
-- @case
select n184 * n184 from nums where id = 3
-- @case
select n184 * n184 from nums where id = 4
-- @case
select n184 + d52 from nums where id = 3
-- @case
select n184 + d52 from nums where id = 4
-- @case
select n184 - d52 from nums where id = 3
-- @case
select n184 - d52 from nums where id = 4
-- @case
select n184 * d52 from nums where id = 3
-- @case
select n184 * d52 from nums where id = 4
-- @case
select n184 + f from nums where id = 3
-- @case
select n184 + f from nums where id = 4
-- @case
select n184 - f from nums where id = 3
-- @case
select n184 - f from nums where id = 4
-- @case
select n184 * f from nums where id = 3
-- @case
select n184 * f from nums where id = 4
-- @case
select n184 + dp from nums where id = 3
-- @case
select n184 + dp from nums where id = 4
-- @case
select n184 - dp from nums where id = 3
-- @case
select n184 - dp from nums where id = 4
-- @case
select n184 * dp from nums where id = 3
-- @case
select n184 * dp from nums where id = 4
-- @case
select d52 + sm from nums where id = 3
-- @case
select d52 + sm from nums where id = 4
-- @case
select d52 - sm from nums where id = 3
-- @case
select d52 - sm from nums where id = 4
-- @case
select d52 * sm from nums where id = 3
-- @case
select d52 * sm from nums where id = 4
-- @case
select d52 + i from nums where id = 3
-- @case
select d52 + i from nums where id = 4
-- @case
select d52 - i from nums where id = 3
-- @case
select d52 - i from nums where id = 4
-- @case
select d52 * i from nums where id = 3
-- @case
select d52 * i from nums where id = 4
-- @case
select d52 + b from nums where id = 3
-- @case
select d52 + b from nums where id = 4
-- @case
select d52 - b from nums where id = 3
-- @case
select d52 - b from nums where id = 4
-- @case
select d52 * b from nums where id = 3
-- @case
select d52 * b from nums where id = 4
-- @case
select d52 + n42 from nums where id = 3
-- @case
select d52 + n42 from nums where id = 4
-- @case
select d52 - n42 from nums where id = 3
-- @case
select d52 - n42 from nums where id = 4
-- @case
select d52 * n42 from nums where id = 3
-- @case
select d52 * n42 from nums where id = 4
-- @case
select d52 + n92 from nums where id = 3
-- @case
select d52 + n92 from nums where id = 4
-- @case
select d52 - n92 from nums where id = 3
-- @case
select d52 - n92 from nums where id = 4
-- @case
select d52 * n92 from nums where id = 3
-- @case
select d52 * n92 from nums where id = 4
-- @case
select d52 + n184 from nums where id = 3
-- @case
select d52 + n184 from nums where id = 4
-- @case
select d52 - n184 from nums where id = 3
-- @case
select d52 - n184 from nums where id = 4
-- @case
select d52 * n184 from nums where id = 3
-- @case
select d52 * n184 from nums where id = 4
-- @case
select d52 + d52 from nums where id = 3
-- @case
select d52 + d52 from nums where id = 4
-- @case
select d52 - d52 from nums where id = 3
-- @case
select d52 - d52 from nums where id = 4
-- @case
select d52 * d52 from nums where id = 3
-- @case
select d52 * d52 from nums where id = 4
-- @case
select d52 + f from nums where id = 3
-- @case
select d52 + f from nums where id = 4
-- @case
select d52 - f from nums where id = 3
-- @case
select d52 - f from nums where id = 4
-- @case
select d52 * f from nums where id = 3
-- @case
select d52 * f from nums where id = 4
-- @case
select d52 + dp from nums where id = 3
-- @case
select d52 + dp from nums where id = 4
-- @case
select d52 - dp from nums where id = 3
-- @case
select d52 - dp from nums where id = 4
-- @case
select d52 * dp from nums where id = 3
-- @case
select d52 * dp from nums where id = 4
-- @case
select f + sm from nums where id = 3
-- @case
select f + sm from nums where id = 4
-- @case
select f - sm from nums where id = 3
-- @case
select f - sm from nums where id = 4
-- @case
select f * sm from nums where id = 3
-- @case
select f * sm from nums where id = 4
-- @case
select f + i from nums where id = 3
-- @case
select f + i from nums where id = 4
-- @case
select f - i from nums where id = 3
-- @case
select f - i from nums where id = 4
-- @case
select f * i from nums where id = 3
-- @case
select f * i from nums where id = 4
-- @case
select f + b from nums where id = 3
-- @case
select f + b from nums where id = 4
-- @case
select f - b from nums where id = 3
-- @case
select f - b from nums where id = 4
-- @case
select f * b from nums where id = 3
-- @case
select f * b from nums where id = 4
-- @case
select f + n42 from nums where id = 3
-- @case
select f + n42 from nums where id = 4
-- @case
select f - n42 from nums where id = 3
-- @case
select f - n42 from nums where id = 4
-- @case
select f * n42 from nums where id = 3
-- @case
select f * n42 from nums where id = 4
-- @case
select f + n92 from nums where id = 3
-- @case
select f + n92 from nums where id = 4
-- @case
select f - n92 from nums where id = 3
-- @case
select f - n92 from nums where id = 4
-- @case
select f * n92 from nums where id = 3
-- @case
select f * n92 from nums where id = 4
-- @case
select f + n184 from nums where id = 3
-- @case
select f + n184 from nums where id = 4
-- @case
select f - n184 from nums where id = 3
-- @case
select f - n184 from nums where id = 4
-- @case
select f * n184 from nums where id = 3
-- @case
select f * n184 from nums where id = 4
-- @case
select f + d52 from nums where id = 3
-- @case
select f + d52 from nums where id = 4
-- @case
select f - d52 from nums where id = 3
-- @case
select f - d52 from nums where id = 4
-- @case
select f * d52 from nums where id = 3
-- @case
select f * d52 from nums where id = 4
-- @case
select f + f from nums where id = 3
-- @case
select f + f from nums where id = 4
-- @case
select f - f from nums where id = 3
-- @case
select f - f from nums where id = 4
-- @case
select f * f from nums where id = 3
-- @case
select f * f from nums where id = 4
-- @case
select f + dp from nums where id = 3
-- @case
select f + dp from nums where id = 4
-- @case
select f - dp from nums where id = 3
-- @case
select f - dp from nums where id = 4
-- @case
select f * dp from nums where id = 3
-- @case
select f * dp from nums where id = 4
-- @case
select dp + sm from nums where id = 3
-- @case
select dp + sm from nums where id = 4
-- @case
select dp - sm from nums where id = 3
-- @case
select dp - sm from nums where id = 4
-- @case
select dp * sm from nums where id = 3
-- @case
select dp * sm from nums where id = 4
-- @case
select dp + i from nums where id = 3
-- @case
select dp + i from nums where id = 4
-- @case
select dp - i from nums where id = 3
-- @case
select dp - i from nums where id = 4
-- @case
select dp * i from nums where id = 3
-- @case
select dp * i from nums where id = 4
-- @case
select dp + b from nums where id = 3
-- @case
select dp + b from nums where id = 4
-- @case
select dp - b from nums where id = 3
-- @case
select dp - b from nums where id = 4
-- @case
select dp * b from nums where id = 3
-- @case
select dp * b from nums where id = 4
-- @case
select dp + n42 from nums where id = 3
-- @case
select dp + n42 from nums where id = 4
-- @case
select dp - n42 from nums where id = 3
-- @case
select dp - n42 from nums where id = 4
-- @case
select dp * n42 from nums where id = 3
-- @case
select dp * n42 from nums where id = 4
-- @case
select dp + n92 from nums where id = 3
-- @case
select dp + n92 from nums where id = 4
-- @case
select dp - n92 from nums where id = 3
-- @case
select dp - n92 from nums where id = 4
-- @case
select dp * n92 from nums where id = 3
-- @case
select dp * n92 from nums where id = 4
-- @case
select dp + n184 from nums where id = 3
-- @case
select dp + n184 from nums where id = 4
-- @case
select dp - n184 from nums where id = 3
-- @case
select dp - n184 from nums where id = 4
-- @case
select dp * n184 from nums where id = 3
-- @case
select dp * n184 from nums where id = 4
-- @case
select dp + d52 from nums where id = 3
-- @case
select dp + d52 from nums where id = 4
-- @case
select dp - d52 from nums where id = 3
-- @case
select dp - d52 from nums where id = 4
-- @case
select dp * d52 from nums where id = 3
-- @case
select dp * d52 from nums where id = 4
-- @case
select dp + f from nums where id = 3
-- @case
select dp + f from nums where id = 4
-- @case
select dp - f from nums where id = 3
-- @case
select dp - f from nums where id = 4
-- @case
select dp * f from nums where id = 3
-- @case
select dp * f from nums where id = 4
-- @case
select dp + dp from nums where id = 3
-- @case
select dp + dp from nums where id = 4
-- @case
select dp - dp from nums where id = 3
-- @case
select dp - dp from nums where id = 4
-- @case
select dp * dp from nums where id = 3
-- @case
select dp * dp from nums where id = 4
-- @case
select -sm, sm / 0 from nums where id = 4
-- @case
select sm / 0 from nums where id = 1
-- @case
select sm / 2, sm * 2, sm + 1, sm - 1, sm * sm from nums where id = 1
-- @case
select sum(sm), avg(sm), min(sm), max(sm), count(sm) from nums where id <> 3 and id <> 4
-- @case
select sum(sm) from nums where id in (3, 3)
-- @case
select sum(sm), avg(sm) from nums
-- @case
select -i, i / 0 from nums where id = 4
-- @case
select i / 0 from nums where id = 1
-- @case
select i / 2, i * 2, i + 1, i - 1, i * i from nums where id = 1
-- @case
select sum(i), avg(i), min(i), max(i), count(i) from nums where id <> 3 and id <> 4
-- @case
select sum(i) from nums where id in (3, 3)
-- @case
select sum(i), avg(i) from nums
-- @case
select -b, b / 0 from nums where id = 4
-- @case
select b / 0 from nums where id = 1
-- @case
select b / 2, b * 2, b + 1, b - 1, b * b from nums where id = 1
-- @case
select sum(b), avg(b), min(b), max(b), count(b) from nums where id <> 3 and id <> 4
-- @case
select sum(b) from nums where id in (3, 3)
-- @case
select sum(b), avg(b) from nums
-- @case
select -n42, n42 / 0 from nums where id = 4
-- @case
select n42 / 0 from nums where id = 1
-- @case
select n42 / 2, n42 * 2, n42 + 1, n42 - 1, n42 * n42 from nums where id = 1
-- @case
select sum(n42), avg(n42), min(n42), max(n42), count(n42) from nums where id <> 3 and id <> 4
-- @case
select sum(n42) from nums where id in (3, 3)
-- @case
select sum(n42), avg(n42) from nums
-- @case
select -n92, n92 / 0 from nums where id = 4
-- @case
select n92 / 0 from nums where id = 1
-- @case
select n92 / 2, n92 * 2, n92 + 1, n92 - 1, n92 * n92 from nums where id = 1
-- @case
select sum(n92), avg(n92), min(n92), max(n92), count(n92) from nums where id <> 3 and id <> 4
-- @case
select sum(n92) from nums where id in (3, 3)
-- @case
select sum(n92), avg(n92) from nums
-- @case
select -n184, n184 / 0 from nums where id = 4
-- @case
select n184 / 0 from nums where id = 1
-- @case
select n184 / 2, n184 * 2, n184 + 1, n184 - 1, n184 * n184 from nums where id = 1
-- @case
select sum(n184), avg(n184), min(n184), max(n184), count(n184) from nums where id <> 3 and id <> 4
-- @case
select sum(n184) from nums where id in (3, 3)
-- @case
select sum(n184), avg(n184) from nums
-- @case
select -d52, d52 / 0 from nums where id = 4
-- @case
select d52 / 0 from nums where id = 1
-- @case
select d52 / 2, d52 * 2, d52 + 1, d52 - 1, d52 * d52 from nums where id = 1
-- @case
select sum(d52), avg(d52), min(d52), max(d52), count(d52) from nums where id <> 3 and id <> 4
-- @case
select sum(d52) from nums where id in (3, 3)
-- @case
select sum(d52), avg(d52) from nums
-- @case
select -f, f / 0 from nums where id = 4
-- @case
select f / 0 from nums where id = 1
-- @case
select f / 2, f * 2, f + 1, f - 1, f * f from nums where id = 1
-- @case
select sum(f), avg(f), min(f), max(f), count(f) from nums where id <> 3 and id <> 4
-- @case
select sum(f) from nums where id in (3, 3)
-- @case
select sum(f), avg(f) from nums
-- @case
select -dp, dp / 0 from nums where id = 4
-- @case
select dp / 0 from nums where id = 1
-- @case
select dp / 2, dp * 2, dp + 1, dp - 1, dp * dp from nums where id = 1
-- @case
select sum(dp), avg(dp), min(dp), max(dp), count(dp) from nums where id <> 3 and id <> 4
-- @case
select sum(dp) from nums where id in (3, 3)
-- @case
select sum(dp), avg(dp) from nums
-- @case
select 7 / 2, -7 / 2, 7 / -2, 7.0 / 2, 7 / 2.0, 1 / 3.0, 2 / 3.00, 10 / 4.000 from rdb$database
-- @case
select 1.0 / 3, 1.00 / 3, 1.000000000 / 3, 1.0000000000000000 / 3 from rdb$database
-- @case
select 0.1 + 0.2, 0.1 * 0.1, 1e0 / 3, 2e0 / 3, 1e0 - 0.9 from rdb$database
-- @case
select 1.23456789 * 1.23456789, 1.234567890 * 1.234567890 from rdb$database
-- @case
select 123456789.123456789 * 10, 0.000000001 * 0.000000001 from rdb$database
-- @case
select 0.000000001 * 0.000000001 * 0.1 from rdb$database
-- @case
select 1.000000000000000001 * 2 from rdb$database
-- @case
select 9223372036854775807 + 1 from rdb$database
-- @case
select -9223372036854775808 - 1 from rdb$database
-- @case
select 9223372036854775807 * 2 from rdb$database
-- @case
select 4611686018427387904 * 2 from rdb$database
-- @case
select 3037000500 * 3037000500 from rdb$database
-- @case
select 2147483647 * 2147483647, 2147483647 + 2147483647 from rdb$database
-- @case
select -(-9223372036854775808) from rdb$database
-- @case
select -9223372036854775808 / -1 from rdb$database
-- @case
select cast(-9223372036854775808 as bigint) / -1 from rdb$database
-- @case
select mod(-7, 3), mod(7, -3), mod(7.5, 2), mod(-7.5, 2), mod(7, 2.5), mod(1e0, 0.3), mod(5, 0) from rdb$database
-- @case
select mod(9223372036854775807, 2), mod(-9223372036854775807, -1) from rdb$database
-- @case
select 1 + 1.5e0, 1.5 + 1e0, 1.5 * 2e0 from rdb$database
-- @case
select 1e308 * 10 from rdb$database
-- @case
select -1e308 * 10 from rdb$database
-- @case
select 1e-308 / 1e10 from rdb$database
-- @case
select 1e0 / 0 from rdb$database
-- @case
select 0e0 / 0 from rdb$database
-- @case
select 1 / 0e0 from rdb$database
-- @case
select 1.5 / 0 from rdb$database
-- @case
select cast(1 as float) / 3, cast(1 as float) * cast(3 as float), cast(2 as float) + 1 from rdb$database
-- @case
select (1 + 2) * 3 - 4 / 2, 2 * 3 + 4 * 5, 10 - 2 - 3, 100 / 10 / 2 from rdb$database
-- @case
select +1, -(-1), - -1, -(1 - 2) from rdb$database
-- @case
select 99999.99 * 100000000000000 from rdb$database
-- @case
select 99999.99 * 1000000000000000 from rdb$database
-- @case
select 1234567890.12345678 * 1234567890.12345678 from rdb$database
-- @case
select n184 * n184 from nums where id = 1
-- @case
select n184 * n184 * n184 from nums where id = 1
-- @case
select n184 * n184 * n184 * n184 * n184 from nums where id = 5
-- @case
select n92 * n92 * n92 * n92 from nums where id = 5
-- @case
select cast(n184 * 1000 as numeric(18,4)) from nums where id = 1
-- @case
select cast(99999999.99 * 10 as numeric(9,2)) from rdb$database
-- @case
select cast(9999999.99 * 10 as numeric(9,2)) from rdb$database
-- @case
select cast(99.99 * 10 as numeric(4,2)) from rdb$database
-- @case
select cast(b * 10 as bigint) from nums where id = 1
-- @case
select cast(b * 10 as bigint) from nums where id = 3
-- @case
select b * 10 from nums where id = 3
-- @case
select sum(b) from nums where id in (1, 3)
-- @case
select sum(b) + 0 from nums where id in (1, 3)
-- @case
select avg(b) from nums where id in (1, 3)
-- @case
select sum(i) from nums where id in (1, 3)
-- @case
select avg(i) from nums where id in (1, 3)
-- @case
select sum(n184) from nums where id in (1, 3)
-- @case
select sum(dp) from nums where id in (3, 3)
-- @case
select sum(f), avg(f) from nums where id = 3
-- @case
select abs(-9223372036854775808) from rdb$database
-- @case
select abs(sm), abs(i), abs(b) from nums where id = 4
-- @case
select sign(-0.0), sign(0e0), sign(-1e-300), sign(b) from nums where id = 4
-- @case
select round(2.5), round(-2.5), round(2.45, 1), round(-2.45, 1), round(1234.5678, -2), round(1.5e0), round(2.5e0), round(-2.5e0) from rdb$database
-- @case
select trunc(2.9), trunc(-2.9), trunc(2.99, 1), trunc(1234.5678, -2), trunc(2.9e0), trunc(-2.9e0) from rdb$database
-- @case
select ceil(2.1), ceil(-2.1), floor(2.9), floor(-2.9), ceil(2.1e0), floor(-2.1e0), ceil(sm), floor(i) from nums where id = 1
-- @case
select round(n42, 1), round(n92), round(n184, 2), round(d52, -1), trunc(n184), round(f, 1), round(dp, 1) from nums where id = 1
-- @case
select round(99.99, 1) from rdb$database
-- @case
select round(n42, 1) from nums where id = 3
-- @case
select round(sm, -1) from nums where id = 3
-- @case
select round(b, -1) from nums where id = 3
-- @case
select power(2, 10), power(2, -1), power(-8, 1e0/3), power(0, 0), power(2.5, 2), sqrt(2), sqrt(-1) from rdb$database
-- @case
select exp(1), ln(10), log10(1000), log(2, 8), ln(0) from rdb$database
-- @case
select ln(-1) from rdb$database
-- @case
select log(0, 1) from rdb$database
-- @case
select power(10, 400) from rdb$database
-- @case
select 1 = 1.0, 1 = 1.00e0, 0.1e0 = 0.1, 1.5 > 1, 2 < 1.99, 3 between 2.5 and 3.5 from rdb$database
-- @case
select n42 = 12.34, n92 > n42, dp > f, i = 100.0, b > 2999999999.5 from nums where id = 1
-- @case
select bin_and(6, 3), bin_or(6, 3), bin_xor(6, 3), bin_not(0), bin_shl(1, 62), bin_shr(-8, 1), bin_shl(1, 64) from rdb$database
-- @case
select bin_and(sm, i), bin_or(sm, b), bin_xor(i, i) from nums where id = 1
-- @case
select bin_and(1.5, 1) from rdb$database
-- @case
select pi(), rand() >= 0, acos(1), asin(0), atan(0), atan2(0, 1), cos(0), sin(0), tan(0), cot(1), cosh(0), sinh(0), tanh(0) from rdb$database
-- @case
select acos(2) from rdb$database
-- @case
select 0.0 * -1, -0e0, cast(-0e0 as varchar(30)), -0.0 from rdb$database
