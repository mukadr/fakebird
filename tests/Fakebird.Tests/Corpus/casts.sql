-- CAST of every kind of literal to every type: values, describe info and conversion errors.
-- @case
select cast(0 as smallint) from rdb$database
-- @case
select cast(0 as integer) from rdb$database
-- @case
select cast(0 as bigint) from rdb$database
-- @case
select cast(0 as numeric(4,2)) from rdb$database
-- @case
select cast(0 as numeric(9,3)) from rdb$database
-- @case
select cast(0 as numeric(18,4)) from rdb$database
-- @case
select cast(0 as decimal(5,0)) from rdb$database
-- @case
select cast(0 as decimal(18,18)) from rdb$database
-- @case
select cast(0 as float) from rdb$database
-- @case
select cast(0 as double precision) from rdb$database
-- @case
select cast(0 as char(5)) from rdb$database
-- @case
select cast(0 as varchar(5)) from rdb$database
-- @case
select cast(0 as varchar(30)) from rdb$database
-- @case
select cast(0 as date) from rdb$database
-- @case
select cast(0 as time) from rdb$database
-- @case
select cast(0 as timestamp) from rdb$database
-- @case
select cast(0 as boolean) from rdb$database
-- @case
select cast(0 as char(16) character set octets) from rdb$database
-- @case
select cast(0 as blob sub_type text) from rdb$database
-- @case
select cast(0 as varchar(10) character set octets) from rdb$database
-- @case
select cast(1 as smallint) from rdb$database
-- @case
select cast(1 as integer) from rdb$database
-- @case
select cast(1 as bigint) from rdb$database
-- @case
select cast(1 as numeric(4,2)) from rdb$database
-- @case
select cast(1 as numeric(9,3)) from rdb$database
-- @case
select cast(1 as numeric(18,4)) from rdb$database
-- @case
select cast(1 as decimal(5,0)) from rdb$database
-- @case
select cast(1 as decimal(18,18)) from rdb$database
-- @case
select cast(1 as float) from rdb$database
-- @case
select cast(1 as double precision) from rdb$database
-- @case
select cast(1 as char(5)) from rdb$database
-- @case
select cast(1 as varchar(5)) from rdb$database
-- @case
select cast(1 as varchar(30)) from rdb$database
-- @case
select cast(1 as date) from rdb$database
-- @case
select cast(1 as time) from rdb$database
-- @case
select cast(1 as timestamp) from rdb$database
-- @case
select cast(1 as boolean) from rdb$database
-- @case
select cast(1 as char(16) character set octets) from rdb$database
-- @case
select cast(1 as blob sub_type text) from rdb$database
-- @case
select cast(1 as varchar(10) character set octets) from rdb$database
-- @case
select cast(-1 as smallint) from rdb$database
-- @case
select cast(-1 as integer) from rdb$database
-- @case
select cast(-1 as bigint) from rdb$database
-- @case
select cast(-1 as numeric(4,2)) from rdb$database
-- @case
select cast(-1 as numeric(9,3)) from rdb$database
-- @case
select cast(-1 as numeric(18,4)) from rdb$database
-- @case
select cast(-1 as decimal(5,0)) from rdb$database
-- @case
select cast(-1 as decimal(18,18)) from rdb$database
-- @case
select cast(-1 as float) from rdb$database
-- @case
select cast(-1 as double precision) from rdb$database
-- @case
select cast(-1 as char(5)) from rdb$database
-- @case
select cast(-1 as varchar(5)) from rdb$database
-- @case
select cast(-1 as varchar(30)) from rdb$database
-- @case
select cast(-1 as date) from rdb$database
-- @case
select cast(-1 as time) from rdb$database
-- @case
select cast(-1 as timestamp) from rdb$database
-- @case
select cast(-1 as boolean) from rdb$database
-- @case
select cast(-1 as char(16) character set octets) from rdb$database
-- @case
select cast(-1 as blob sub_type text) from rdb$database
-- @case
select cast(-1 as varchar(10) character set octets) from rdb$database
-- @case
select cast(32767 as smallint) from rdb$database
-- @case
select cast(32767 as integer) from rdb$database
-- @case
select cast(32767 as bigint) from rdb$database
-- @case
select cast(32767 as numeric(4,2)) from rdb$database
-- @case
select cast(32767 as numeric(9,3)) from rdb$database
-- @case
select cast(32767 as numeric(18,4)) from rdb$database
-- @case
select cast(32767 as decimal(5,0)) from rdb$database
-- @case
select cast(32767 as decimal(18,18)) from rdb$database
-- @case
select cast(32767 as float) from rdb$database
-- @case
select cast(32767 as double precision) from rdb$database
-- @case
select cast(32767 as char(5)) from rdb$database
-- @case
select cast(32767 as varchar(5)) from rdb$database
-- @case
select cast(32767 as varchar(30)) from rdb$database
-- @case
select cast(32767 as date) from rdb$database
-- @case
select cast(32767 as time) from rdb$database
-- @case
select cast(32767 as timestamp) from rdb$database
-- @case
select cast(32767 as boolean) from rdb$database
-- @case
select cast(32767 as char(16) character set octets) from rdb$database
-- @case
select cast(32767 as blob sub_type text) from rdb$database
-- @case
select cast(32767 as varchar(10) character set octets) from rdb$database
-- @case
select cast(32768 as smallint) from rdb$database
-- @case
select cast(32768 as integer) from rdb$database
-- @case
select cast(32768 as bigint) from rdb$database
-- @case
select cast(32768 as numeric(4,2)) from rdb$database
-- @case
select cast(32768 as numeric(9,3)) from rdb$database
-- @case
select cast(32768 as numeric(18,4)) from rdb$database
-- @case
select cast(32768 as decimal(5,0)) from rdb$database
-- @case
select cast(32768 as decimal(18,18)) from rdb$database
-- @case
select cast(32768 as float) from rdb$database
-- @case
select cast(32768 as double precision) from rdb$database
-- @case
select cast(32768 as char(5)) from rdb$database
-- @case
select cast(32768 as varchar(5)) from rdb$database
-- @case
select cast(32768 as varchar(30)) from rdb$database
-- @case
select cast(32768 as date) from rdb$database
-- @case
select cast(32768 as time) from rdb$database
-- @case
select cast(32768 as timestamp) from rdb$database
-- @case
select cast(32768 as boolean) from rdb$database
-- @case
select cast(32768 as char(16) character set octets) from rdb$database
-- @case
select cast(32768 as blob sub_type text) from rdb$database
-- @case
select cast(32768 as varchar(10) character set octets) from rdb$database
-- @case
select cast(-32769 as smallint) from rdb$database
-- @case
select cast(-32769 as integer) from rdb$database
-- @case
select cast(-32769 as bigint) from rdb$database
-- @case
select cast(-32769 as numeric(4,2)) from rdb$database
-- @case
select cast(-32769 as numeric(9,3)) from rdb$database
-- @case
select cast(-32769 as numeric(18,4)) from rdb$database
-- @case
select cast(-32769 as decimal(5,0)) from rdb$database
-- @case
select cast(-32769 as decimal(18,18)) from rdb$database
-- @case
select cast(-32769 as float) from rdb$database
-- @case
select cast(-32769 as double precision) from rdb$database
-- @case
select cast(-32769 as char(5)) from rdb$database
-- @case
select cast(-32769 as varchar(5)) from rdb$database
-- @case
select cast(-32769 as varchar(30)) from rdb$database
-- @case
select cast(-32769 as date) from rdb$database
-- @case
select cast(-32769 as time) from rdb$database
-- @case
select cast(-32769 as timestamp) from rdb$database
-- @case
select cast(-32769 as boolean) from rdb$database
-- @case
select cast(-32769 as char(16) character set octets) from rdb$database
-- @case
select cast(-32769 as blob sub_type text) from rdb$database
-- @case
select cast(-32769 as varchar(10) character set octets) from rdb$database
-- @case
select cast(2147483647 as smallint) from rdb$database
-- @case
select cast(2147483647 as integer) from rdb$database
-- @case
select cast(2147483647 as bigint) from rdb$database
-- @case
select cast(2147483647 as numeric(4,2)) from rdb$database
-- @case
select cast(2147483647 as numeric(9,3)) from rdb$database
-- @case
select cast(2147483647 as numeric(18,4)) from rdb$database
-- @case
select cast(2147483647 as decimal(5,0)) from rdb$database
-- @case
select cast(2147483647 as decimal(18,18)) from rdb$database
-- @case
select cast(2147483647 as float) from rdb$database
-- @case
select cast(2147483647 as double precision) from rdb$database
-- @case
select cast(2147483647 as char(5)) from rdb$database
-- @case
select cast(2147483647 as varchar(5)) from rdb$database
-- @case
select cast(2147483647 as varchar(30)) from rdb$database
-- @case
select cast(2147483647 as date) from rdb$database
-- @case
select cast(2147483647 as time) from rdb$database
-- @case
select cast(2147483647 as timestamp) from rdb$database
-- @case
select cast(2147483647 as boolean) from rdb$database
-- @case
select cast(2147483647 as char(16) character set octets) from rdb$database
-- @case
select cast(2147483647 as blob sub_type text) from rdb$database
-- @case
select cast(2147483647 as varchar(10) character set octets) from rdb$database
-- @case
select cast(2147483648 as smallint) from rdb$database
-- @case
select cast(2147483648 as integer) from rdb$database
-- @case
select cast(2147483648 as bigint) from rdb$database
-- @case
select cast(2147483648 as numeric(4,2)) from rdb$database
-- @case
select cast(2147483648 as numeric(9,3)) from rdb$database
-- @case
select cast(2147483648 as numeric(18,4)) from rdb$database
-- @case
select cast(2147483648 as decimal(5,0)) from rdb$database
-- @case
select cast(2147483648 as decimal(18,18)) from rdb$database
-- @case
select cast(2147483648 as float) from rdb$database
-- @case
select cast(2147483648 as double precision) from rdb$database
-- @case
select cast(2147483648 as char(5)) from rdb$database
-- @case
select cast(2147483648 as varchar(5)) from rdb$database
-- @case
select cast(2147483648 as varchar(30)) from rdb$database
-- @case
select cast(2147483648 as date) from rdb$database
-- @case
select cast(2147483648 as time) from rdb$database
-- @case
select cast(2147483648 as timestamp) from rdb$database
-- @case
select cast(2147483648 as boolean) from rdb$database
-- @case
select cast(2147483648 as char(16) character set octets) from rdb$database
-- @case
select cast(2147483648 as blob sub_type text) from rdb$database
-- @case
select cast(2147483648 as varchar(10) character set octets) from rdb$database
-- @case
select cast(-2147483649 as smallint) from rdb$database
-- @case
select cast(-2147483649 as integer) from rdb$database
-- @case
select cast(-2147483649 as bigint) from rdb$database
-- @case
select cast(-2147483649 as numeric(4,2)) from rdb$database
-- @case
select cast(-2147483649 as numeric(9,3)) from rdb$database
-- @case
select cast(-2147483649 as numeric(18,4)) from rdb$database
-- @case
select cast(-2147483649 as decimal(5,0)) from rdb$database
-- @case
select cast(-2147483649 as decimal(18,18)) from rdb$database
-- @case
select cast(-2147483649 as float) from rdb$database
-- @case
select cast(-2147483649 as double precision) from rdb$database
-- @case
select cast(-2147483649 as char(5)) from rdb$database
-- @case
select cast(-2147483649 as varchar(5)) from rdb$database
-- @case
select cast(-2147483649 as varchar(30)) from rdb$database
-- @case
select cast(-2147483649 as date) from rdb$database
-- @case
select cast(-2147483649 as time) from rdb$database
-- @case
select cast(-2147483649 as timestamp) from rdb$database
-- @case
select cast(-2147483649 as boolean) from rdb$database
-- @case
select cast(-2147483649 as char(16) character set octets) from rdb$database
-- @case
select cast(-2147483649 as blob sub_type text) from rdb$database
-- @case
select cast(-2147483649 as varchar(10) character set octets) from rdb$database
-- @case
select cast(9223372036854775807 as smallint) from rdb$database
-- @case
select cast(9223372036854775807 as integer) from rdb$database
-- @case
select cast(9223372036854775807 as bigint) from rdb$database
-- @case
select cast(9223372036854775807 as numeric(4,2)) from rdb$database
-- @case
select cast(9223372036854775807 as numeric(9,3)) from rdb$database
-- @case
select cast(9223372036854775807 as numeric(18,4)) from rdb$database
-- @case
select cast(9223372036854775807 as decimal(5,0)) from rdb$database
-- @case
select cast(9223372036854775807 as decimal(18,18)) from rdb$database
-- @case
select cast(9223372036854775807 as float) from rdb$database
-- @case
select cast(9223372036854775807 as double precision) from rdb$database
-- @case
select cast(9223372036854775807 as char(5)) from rdb$database
-- @case
select cast(9223372036854775807 as varchar(5)) from rdb$database
-- @case
select cast(9223372036854775807 as varchar(30)) from rdb$database
-- @case
select cast(9223372036854775807 as date) from rdb$database
-- @case
select cast(9223372036854775807 as time) from rdb$database
-- @case
select cast(9223372036854775807 as timestamp) from rdb$database
-- @case
select cast(9223372036854775807 as boolean) from rdb$database
-- @case
select cast(9223372036854775807 as char(16) character set octets) from rdb$database
-- @case
select cast(9223372036854775807 as blob sub_type text) from rdb$database
-- @case
select cast(9223372036854775807 as varchar(10) character set octets) from rdb$database
-- @case
select cast(-9223372036854775808 as smallint) from rdb$database
-- @case
select cast(-9223372036854775808 as integer) from rdb$database
-- @case
select cast(-9223372036854775808 as bigint) from rdb$database
-- @case
select cast(-9223372036854775808 as numeric(4,2)) from rdb$database
-- @case
select cast(-9223372036854775808 as numeric(9,3)) from rdb$database
-- @case
select cast(-9223372036854775808 as numeric(18,4)) from rdb$database
-- @case
select cast(-9223372036854775808 as decimal(5,0)) from rdb$database
-- @case
select cast(-9223372036854775808 as decimal(18,18)) from rdb$database
-- @case
select cast(-9223372036854775808 as float) from rdb$database
-- @case
select cast(-9223372036854775808 as double precision) from rdb$database
-- @case
select cast(-9223372036854775808 as char(5)) from rdb$database
-- @case
select cast(-9223372036854775808 as varchar(5)) from rdb$database
-- @case
select cast(-9223372036854775808 as varchar(30)) from rdb$database
-- @case
select cast(-9223372036854775808 as date) from rdb$database
-- @case
select cast(-9223372036854775808 as time) from rdb$database
-- @case
select cast(-9223372036854775808 as timestamp) from rdb$database
-- @case
select cast(-9223372036854775808 as boolean) from rdb$database
-- @case
select cast(-9223372036854775808 as char(16) character set octets) from rdb$database
-- @case
select cast(-9223372036854775808 as blob sub_type text) from rdb$database
-- @case
select cast(-9223372036854775808 as varchar(10) character set octets) from rdb$database
-- @case
select cast(1.5 as smallint) from rdb$database
-- @case
select cast(1.5 as integer) from rdb$database
-- @case
select cast(1.5 as bigint) from rdb$database
-- @case
select cast(1.5 as numeric(4,2)) from rdb$database
-- @case
select cast(1.5 as numeric(9,3)) from rdb$database
-- @case
select cast(1.5 as numeric(18,4)) from rdb$database
-- @case
select cast(1.5 as decimal(5,0)) from rdb$database
-- @case
select cast(1.5 as decimal(18,18)) from rdb$database
-- @case
select cast(1.5 as float) from rdb$database
-- @case
select cast(1.5 as double precision) from rdb$database
-- @case
select cast(1.5 as char(5)) from rdb$database
-- @case
select cast(1.5 as varchar(5)) from rdb$database
-- @case
select cast(1.5 as varchar(30)) from rdb$database
-- @case
select cast(1.5 as date) from rdb$database
-- @case
select cast(1.5 as time) from rdb$database
-- @case
select cast(1.5 as timestamp) from rdb$database
-- @case
select cast(1.5 as boolean) from rdb$database
-- @case
select cast(1.5 as char(16) character set octets) from rdb$database
-- @case
select cast(1.5 as blob sub_type text) from rdb$database
-- @case
select cast(1.5 as varchar(10) character set octets) from rdb$database
-- @case
select cast(-1.5 as smallint) from rdb$database
-- @case
select cast(-1.5 as integer) from rdb$database
-- @case
select cast(-1.5 as bigint) from rdb$database
-- @case
select cast(-1.5 as numeric(4,2)) from rdb$database
-- @case
select cast(-1.5 as numeric(9,3)) from rdb$database
-- @case
select cast(-1.5 as numeric(18,4)) from rdb$database
-- @case
select cast(-1.5 as decimal(5,0)) from rdb$database
-- @case
select cast(-1.5 as decimal(18,18)) from rdb$database
-- @case
select cast(-1.5 as float) from rdb$database
-- @case
select cast(-1.5 as double precision) from rdb$database
-- @case
select cast(-1.5 as char(5)) from rdb$database
-- @case
select cast(-1.5 as varchar(5)) from rdb$database
-- @case
select cast(-1.5 as varchar(30)) from rdb$database
-- @case
select cast(-1.5 as date) from rdb$database
-- @case
select cast(-1.5 as time) from rdb$database
-- @case
select cast(-1.5 as timestamp) from rdb$database
-- @case
select cast(-1.5 as boolean) from rdb$database
-- @case
select cast(-1.5 as char(16) character set octets) from rdb$database
-- @case
select cast(-1.5 as blob sub_type text) from rdb$database
-- @case
select cast(-1.5 as varchar(10) character set octets) from rdb$database
-- @case
select cast(2.5 as smallint) from rdb$database
-- @case
select cast(2.5 as integer) from rdb$database
-- @case
select cast(2.5 as bigint) from rdb$database
-- @case
select cast(2.5 as numeric(4,2)) from rdb$database
-- @case
select cast(2.5 as numeric(9,3)) from rdb$database
-- @case
select cast(2.5 as numeric(18,4)) from rdb$database
-- @case
select cast(2.5 as decimal(5,0)) from rdb$database
-- @case
select cast(2.5 as decimal(18,18)) from rdb$database
-- @case
select cast(2.5 as float) from rdb$database
-- @case
select cast(2.5 as double precision) from rdb$database
-- @case
select cast(2.5 as char(5)) from rdb$database
-- @case
select cast(2.5 as varchar(5)) from rdb$database
-- @case
select cast(2.5 as varchar(30)) from rdb$database
-- @case
select cast(2.5 as date) from rdb$database
-- @case
select cast(2.5 as time) from rdb$database
-- @case
select cast(2.5 as timestamp) from rdb$database
-- @case
select cast(2.5 as boolean) from rdb$database
-- @case
select cast(2.5 as char(16) character set octets) from rdb$database
-- @case
select cast(2.5 as blob sub_type text) from rdb$database
-- @case
select cast(2.5 as varchar(10) character set octets) from rdb$database
-- @case
select cast(-2.5 as smallint) from rdb$database
-- @case
select cast(-2.5 as integer) from rdb$database
-- @case
select cast(-2.5 as bigint) from rdb$database
-- @case
select cast(-2.5 as numeric(4,2)) from rdb$database
-- @case
select cast(-2.5 as numeric(9,3)) from rdb$database
-- @case
select cast(-2.5 as numeric(18,4)) from rdb$database
-- @case
select cast(-2.5 as decimal(5,0)) from rdb$database
-- @case
select cast(-2.5 as decimal(18,18)) from rdb$database
-- @case
select cast(-2.5 as float) from rdb$database
-- @case
select cast(-2.5 as double precision) from rdb$database
-- @case
select cast(-2.5 as char(5)) from rdb$database
-- @case
select cast(-2.5 as varchar(5)) from rdb$database
-- @case
select cast(-2.5 as varchar(30)) from rdb$database
-- @case
select cast(-2.5 as date) from rdb$database
-- @case
select cast(-2.5 as time) from rdb$database
-- @case
select cast(-2.5 as timestamp) from rdb$database
-- @case
select cast(-2.5 as boolean) from rdb$database
-- @case
select cast(-2.5 as char(16) character set octets) from rdb$database
-- @case
select cast(-2.5 as blob sub_type text) from rdb$database
-- @case
select cast(-2.5 as varchar(10) character set octets) from rdb$database
-- @case
select cast(0.005 as smallint) from rdb$database
-- @case
select cast(0.005 as integer) from rdb$database
-- @case
select cast(0.005 as bigint) from rdb$database
-- @case
select cast(0.005 as numeric(4,2)) from rdb$database
-- @case
select cast(0.005 as numeric(9,3)) from rdb$database
-- @case
select cast(0.005 as numeric(18,4)) from rdb$database
-- @case
select cast(0.005 as decimal(5,0)) from rdb$database
-- @case
select cast(0.005 as decimal(18,18)) from rdb$database
-- @case
select cast(0.005 as float) from rdb$database
-- @case
select cast(0.005 as double precision) from rdb$database
-- @case
select cast(0.005 as char(5)) from rdb$database
-- @case
select cast(0.005 as varchar(5)) from rdb$database
-- @case
select cast(0.005 as varchar(30)) from rdb$database
-- @case
select cast(0.005 as date) from rdb$database
-- @case
select cast(0.005 as time) from rdb$database
-- @case
select cast(0.005 as timestamp) from rdb$database
-- @case
select cast(0.005 as boolean) from rdb$database
-- @case
select cast(0.005 as char(16) character set octets) from rdb$database
-- @case
select cast(0.005 as blob sub_type text) from rdb$database
-- @case
select cast(0.005 as varchar(10) character set octets) from rdb$database
-- @case
select cast(123.456 as smallint) from rdb$database
-- @case
select cast(123.456 as integer) from rdb$database
-- @case
select cast(123.456 as bigint) from rdb$database
-- @case
select cast(123.456 as numeric(4,2)) from rdb$database
-- @case
select cast(123.456 as numeric(9,3)) from rdb$database
-- @case
select cast(123.456 as numeric(18,4)) from rdb$database
-- @case
select cast(123.456 as decimal(5,0)) from rdb$database
-- @case
select cast(123.456 as decimal(18,18)) from rdb$database
-- @case
select cast(123.456 as float) from rdb$database
-- @case
select cast(123.456 as double precision) from rdb$database
-- @case
select cast(123.456 as char(5)) from rdb$database
-- @case
select cast(123.456 as varchar(5)) from rdb$database
-- @case
select cast(123.456 as varchar(30)) from rdb$database
-- @case
select cast(123.456 as date) from rdb$database
-- @case
select cast(123.456 as time) from rdb$database
-- @case
select cast(123.456 as timestamp) from rdb$database
-- @case
select cast(123.456 as boolean) from rdb$database
-- @case
select cast(123.456 as char(16) character set octets) from rdb$database
-- @case
select cast(123.456 as blob sub_type text) from rdb$database
-- @case
select cast(123.456 as varchar(10) character set octets) from rdb$database
-- @case
select cast(99999.995 as smallint) from rdb$database
-- @case
select cast(99999.995 as integer) from rdb$database
-- @case
select cast(99999.995 as bigint) from rdb$database
-- @case
select cast(99999.995 as numeric(4,2)) from rdb$database
-- @case
select cast(99999.995 as numeric(9,3)) from rdb$database
-- @case
select cast(99999.995 as numeric(18,4)) from rdb$database
-- @case
select cast(99999.995 as decimal(5,0)) from rdb$database
-- @case
select cast(99999.995 as decimal(18,18)) from rdb$database
-- @case
select cast(99999.995 as float) from rdb$database
-- @case
select cast(99999.995 as double precision) from rdb$database
-- @case
select cast(99999.995 as char(5)) from rdb$database
-- @case
select cast(99999.995 as varchar(5)) from rdb$database
-- @case
select cast(99999.995 as varchar(30)) from rdb$database
-- @case
select cast(99999.995 as date) from rdb$database
-- @case
select cast(99999.995 as time) from rdb$database
-- @case
select cast(99999.995 as timestamp) from rdb$database
-- @case
select cast(99999.995 as boolean) from rdb$database
-- @case
select cast(99999.995 as char(16) character set octets) from rdb$database
-- @case
select cast(99999.995 as blob sub_type text) from rdb$database
-- @case
select cast(99999.995 as varchar(10) character set octets) from rdb$database
-- @case
select cast(0.0 as smallint) from rdb$database
-- @case
select cast(0.0 as integer) from rdb$database
-- @case
select cast(0.0 as bigint) from rdb$database
-- @case
select cast(0.0 as numeric(4,2)) from rdb$database
-- @case
select cast(0.0 as numeric(9,3)) from rdb$database
-- @case
select cast(0.0 as numeric(18,4)) from rdb$database
-- @case
select cast(0.0 as decimal(5,0)) from rdb$database
-- @case
select cast(0.0 as decimal(18,18)) from rdb$database
-- @case
select cast(0.0 as float) from rdb$database
-- @case
select cast(0.0 as double precision) from rdb$database
-- @case
select cast(0.0 as char(5)) from rdb$database
-- @case
select cast(0.0 as varchar(5)) from rdb$database
-- @case
select cast(0.0 as varchar(30)) from rdb$database
-- @case
select cast(0.0 as date) from rdb$database
-- @case
select cast(0.0 as time) from rdb$database
-- @case
select cast(0.0 as timestamp) from rdb$database
-- @case
select cast(0.0 as boolean) from rdb$database
-- @case
select cast(0.0 as char(16) character set octets) from rdb$database
-- @case
select cast(0.0 as blob sub_type text) from rdb$database
-- @case
select cast(0.0 as varchar(10) character set octets) from rdb$database
-- @case
select cast(-0.004 as smallint) from rdb$database
-- @case
select cast(-0.004 as integer) from rdb$database
-- @case
select cast(-0.004 as bigint) from rdb$database
-- @case
select cast(-0.004 as numeric(4,2)) from rdb$database
-- @case
select cast(-0.004 as numeric(9,3)) from rdb$database
-- @case
select cast(-0.004 as numeric(18,4)) from rdb$database
-- @case
select cast(-0.004 as decimal(5,0)) from rdb$database
-- @case
select cast(-0.004 as decimal(18,18)) from rdb$database
-- @case
select cast(-0.004 as float) from rdb$database
-- @case
select cast(-0.004 as double precision) from rdb$database
-- @case
select cast(-0.004 as char(5)) from rdb$database
-- @case
select cast(-0.004 as varchar(5)) from rdb$database
-- @case
select cast(-0.004 as varchar(30)) from rdb$database
-- @case
select cast(-0.004 as date) from rdb$database
-- @case
select cast(-0.004 as time) from rdb$database
-- @case
select cast(-0.004 as timestamp) from rdb$database
-- @case
select cast(-0.004 as boolean) from rdb$database
-- @case
select cast(-0.004 as char(16) character set octets) from rdb$database
-- @case
select cast(-0.004 as blob sub_type text) from rdb$database
-- @case
select cast(-0.004 as varchar(10) character set octets) from rdb$database
-- @case
select cast(1e0 as smallint) from rdb$database
-- @case
select cast(1e0 as integer) from rdb$database
-- @case
select cast(1e0 as bigint) from rdb$database
-- @case
select cast(1e0 as numeric(4,2)) from rdb$database
-- @case
select cast(1e0 as numeric(9,3)) from rdb$database
-- @case
select cast(1e0 as numeric(18,4)) from rdb$database
-- @case
select cast(1e0 as decimal(5,0)) from rdb$database
-- @case
select cast(1e0 as decimal(18,18)) from rdb$database
-- @case
select cast(1e0 as float) from rdb$database
-- @case
select cast(1e0 as double precision) from rdb$database
-- @case
select cast(1e0 as char(5)) from rdb$database
-- @case
select cast(1e0 as varchar(5)) from rdb$database
-- @case
select cast(1e0 as varchar(30)) from rdb$database
-- @case
select cast(1e0 as date) from rdb$database
-- @case
select cast(1e0 as time) from rdb$database
-- @case
select cast(1e0 as timestamp) from rdb$database
-- @case
select cast(1e0 as boolean) from rdb$database
-- @case
select cast(1e0 as char(16) character set octets) from rdb$database
-- @case
select cast(1e0 as blob sub_type text) from rdb$database
-- @case
select cast(1e0 as varchar(10) character set octets) from rdb$database
-- @case
select cast(1.5e0 as smallint) from rdb$database
-- @case
select cast(1.5e0 as integer) from rdb$database
-- @case
select cast(1.5e0 as bigint) from rdb$database
-- @case
select cast(1.5e0 as numeric(4,2)) from rdb$database
-- @case
select cast(1.5e0 as numeric(9,3)) from rdb$database
-- @case
select cast(1.5e0 as numeric(18,4)) from rdb$database
-- @case
select cast(1.5e0 as decimal(5,0)) from rdb$database
-- @case
select cast(1.5e0 as decimal(18,18)) from rdb$database
-- @case
select cast(1.5e0 as float) from rdb$database
-- @case
select cast(1.5e0 as double precision) from rdb$database
-- @case
select cast(1.5e0 as char(5)) from rdb$database
-- @case
select cast(1.5e0 as varchar(5)) from rdb$database
-- @case
select cast(1.5e0 as varchar(30)) from rdb$database
-- @case
select cast(1.5e0 as date) from rdb$database
-- @case
select cast(1.5e0 as time) from rdb$database
-- @case
select cast(1.5e0 as timestamp) from rdb$database
-- @case
select cast(1.5e0 as boolean) from rdb$database
-- @case
select cast(1.5e0 as char(16) character set octets) from rdb$database
-- @case
select cast(1.5e0 as blob sub_type text) from rdb$database
-- @case
select cast(1.5e0 as varchar(10) character set octets) from rdb$database
-- @case
select cast(-2.5e0 as smallint) from rdb$database
-- @case
select cast(-2.5e0 as integer) from rdb$database
-- @case
select cast(-2.5e0 as bigint) from rdb$database
-- @case
select cast(-2.5e0 as numeric(4,2)) from rdb$database
-- @case
select cast(-2.5e0 as numeric(9,3)) from rdb$database
-- @case
select cast(-2.5e0 as numeric(18,4)) from rdb$database
-- @case
select cast(-2.5e0 as decimal(5,0)) from rdb$database
-- @case
select cast(-2.5e0 as decimal(18,18)) from rdb$database
-- @case
select cast(-2.5e0 as float) from rdb$database
-- @case
select cast(-2.5e0 as double precision) from rdb$database
-- @case
select cast(-2.5e0 as char(5)) from rdb$database
-- @case
select cast(-2.5e0 as varchar(5)) from rdb$database
-- @case
select cast(-2.5e0 as varchar(30)) from rdb$database
-- @case
select cast(-2.5e0 as date) from rdb$database
-- @case
select cast(-2.5e0 as time) from rdb$database
-- @case
select cast(-2.5e0 as timestamp) from rdb$database
-- @case
select cast(-2.5e0 as boolean) from rdb$database
-- @case
select cast(-2.5e0 as char(16) character set octets) from rdb$database
-- @case
select cast(-2.5e0 as blob sub_type text) from rdb$database
-- @case
select cast(-2.5e0 as varchar(10) character set octets) from rdb$database
-- @case
select cast(1e10 as smallint) from rdb$database
-- @case
select cast(1e10 as integer) from rdb$database
-- @case
select cast(1e10 as bigint) from rdb$database
-- @case
select cast(1e10 as numeric(4,2)) from rdb$database
-- @case
select cast(1e10 as numeric(9,3)) from rdb$database
-- @case
select cast(1e10 as numeric(18,4)) from rdb$database
-- @case
select cast(1e10 as decimal(5,0)) from rdb$database
-- @case
select cast(1e10 as decimal(18,18)) from rdb$database
-- @case
select cast(1e10 as float) from rdb$database
-- @case
select cast(1e10 as double precision) from rdb$database
-- @case
select cast(1e10 as char(5)) from rdb$database
-- @case
select cast(1e10 as varchar(5)) from rdb$database
-- @case
select cast(1e10 as varchar(30)) from rdb$database
-- @case
select cast(1e10 as date) from rdb$database
-- @case
select cast(1e10 as time) from rdb$database
-- @case
select cast(1e10 as timestamp) from rdb$database
-- @case
select cast(1e10 as boolean) from rdb$database
-- @case
select cast(1e10 as char(16) character set octets) from rdb$database
-- @case
select cast(1e10 as blob sub_type text) from rdb$database
-- @case
select cast(1e10 as varchar(10) character set octets) from rdb$database
-- @case
select cast(1.7976931348623157e308 as smallint) from rdb$database
-- @case
select cast(1.7976931348623157e308 as integer) from rdb$database
-- @case
select cast(1.7976931348623157e308 as bigint) from rdb$database
-- @case
select cast(1.7976931348623157e308 as numeric(4,2)) from rdb$database
-- @case
select cast(1.7976931348623157e308 as numeric(9,3)) from rdb$database
-- @case
select cast(1.7976931348623157e308 as numeric(18,4)) from rdb$database
-- @case
select cast(1.7976931348623157e308 as decimal(5,0)) from rdb$database
-- @case
select cast(1.7976931348623157e308 as decimal(18,18)) from rdb$database
-- @case
select cast(1.7976931348623157e308 as float) from rdb$database
-- @case
select cast(1.7976931348623157e308 as double precision) from rdb$database
-- @case
select cast(1.7976931348623157e308 as char(5)) from rdb$database
-- @case
select cast(1.7976931348623157e308 as varchar(5)) from rdb$database
-- @case
select cast(1.7976931348623157e308 as varchar(30)) from rdb$database
-- @case
select cast(1.7976931348623157e308 as date) from rdb$database
-- @case
select cast(1.7976931348623157e308 as time) from rdb$database
-- @case
select cast(1.7976931348623157e308 as timestamp) from rdb$database
-- @case
select cast(1.7976931348623157e308 as boolean) from rdb$database
-- @case
select cast(1.7976931348623157e308 as char(16) character set octets) from rdb$database
-- @case
select cast(1.7976931348623157e308 as blob sub_type text) from rdb$database
-- @case
select cast(1.7976931348623157e308 as varchar(10) character set octets) from rdb$database
-- @case
select cast(1e-5 as smallint) from rdb$database
-- @case
select cast(1e-5 as integer) from rdb$database
-- @case
select cast(1e-5 as bigint) from rdb$database
-- @case
select cast(1e-5 as numeric(4,2)) from rdb$database
-- @case
select cast(1e-5 as numeric(9,3)) from rdb$database
-- @case
select cast(1e-5 as numeric(18,4)) from rdb$database
-- @case
select cast(1e-5 as decimal(5,0)) from rdb$database
-- @case
select cast(1e-5 as decimal(18,18)) from rdb$database
-- @case
select cast(1e-5 as float) from rdb$database
-- @case
select cast(1e-5 as double precision) from rdb$database
-- @case
select cast(1e-5 as char(5)) from rdb$database
-- @case
select cast(1e-5 as varchar(5)) from rdb$database
-- @case
select cast(1e-5 as varchar(30)) from rdb$database
-- @case
select cast(1e-5 as date) from rdb$database
-- @case
select cast(1e-5 as time) from rdb$database
-- @case
select cast(1e-5 as timestamp) from rdb$database
-- @case
select cast(1e-5 as boolean) from rdb$database
-- @case
select cast(1e-5 as char(16) character set octets) from rdb$database
-- @case
select cast(1e-5 as blob sub_type text) from rdb$database
-- @case
select cast(1e-5 as varchar(10) character set octets) from rdb$database
-- @case
select cast(0.1e0 as smallint) from rdb$database
-- @case
select cast(0.1e0 as integer) from rdb$database
-- @case
select cast(0.1e0 as bigint) from rdb$database
-- @case
select cast(0.1e0 as numeric(4,2)) from rdb$database
-- @case
select cast(0.1e0 as numeric(9,3)) from rdb$database
-- @case
select cast(0.1e0 as numeric(18,4)) from rdb$database
-- @case
select cast(0.1e0 as decimal(5,0)) from rdb$database
-- @case
select cast(0.1e0 as decimal(18,18)) from rdb$database
-- @case
select cast(0.1e0 as float) from rdb$database
-- @case
select cast(0.1e0 as double precision) from rdb$database
-- @case
select cast(0.1e0 as char(5)) from rdb$database
-- @case
select cast(0.1e0 as varchar(5)) from rdb$database
-- @case
select cast(0.1e0 as varchar(30)) from rdb$database
-- @case
select cast(0.1e0 as date) from rdb$database
-- @case
select cast(0.1e0 as time) from rdb$database
-- @case
select cast(0.1e0 as timestamp) from rdb$database
-- @case
select cast(0.1e0 as boolean) from rdb$database
-- @case
select cast(0.1e0 as char(16) character set octets) from rdb$database
-- @case
select cast(0.1e0 as blob sub_type text) from rdb$database
-- @case
select cast(0.1e0 as varchar(10) character set octets) from rdb$database
-- @case
select cast(123456789.123456789e0 as smallint) from rdb$database
-- @case
select cast(123456789.123456789e0 as integer) from rdb$database
-- @case
select cast(123456789.123456789e0 as bigint) from rdb$database
-- @case
select cast(123456789.123456789e0 as numeric(4,2)) from rdb$database
-- @case
select cast(123456789.123456789e0 as numeric(9,3)) from rdb$database
-- @case
select cast(123456789.123456789e0 as numeric(18,4)) from rdb$database
-- @case
select cast(123456789.123456789e0 as decimal(5,0)) from rdb$database
-- @case
select cast(123456789.123456789e0 as decimal(18,18)) from rdb$database
-- @case
select cast(123456789.123456789e0 as float) from rdb$database
-- @case
select cast(123456789.123456789e0 as double precision) from rdb$database
-- @case
select cast(123456789.123456789e0 as char(5)) from rdb$database
-- @case
select cast(123456789.123456789e0 as varchar(5)) from rdb$database
-- @case
select cast(123456789.123456789e0 as varchar(30)) from rdb$database
-- @case
select cast(123456789.123456789e0 as date) from rdb$database
-- @case
select cast(123456789.123456789e0 as time) from rdb$database
-- @case
select cast(123456789.123456789e0 as timestamp) from rdb$database
-- @case
select cast(123456789.123456789e0 as boolean) from rdb$database
-- @case
select cast(123456789.123456789e0 as char(16) character set octets) from rdb$database
-- @case
select cast(123456789.123456789e0 as blob sub_type text) from rdb$database
-- @case
select cast(123456789.123456789e0 as varchar(10) character set octets) from rdb$database
-- @case
select cast(-0e0 as smallint) from rdb$database
-- @case
select cast(-0e0 as integer) from rdb$database
-- @case
select cast(-0e0 as bigint) from rdb$database
-- @case
select cast(-0e0 as numeric(4,2)) from rdb$database
-- @case
select cast(-0e0 as numeric(9,3)) from rdb$database
-- @case
select cast(-0e0 as numeric(18,4)) from rdb$database
-- @case
select cast(-0e0 as decimal(5,0)) from rdb$database
-- @case
select cast(-0e0 as decimal(18,18)) from rdb$database
-- @case
select cast(-0e0 as float) from rdb$database
-- @case
select cast(-0e0 as double precision) from rdb$database
-- @case
select cast(-0e0 as char(5)) from rdb$database
-- @case
select cast(-0e0 as varchar(5)) from rdb$database
-- @case
select cast(-0e0 as varchar(30)) from rdb$database
-- @case
select cast(-0e0 as date) from rdb$database
-- @case
select cast(-0e0 as time) from rdb$database
-- @case
select cast(-0e0 as timestamp) from rdb$database
-- @case
select cast(-0e0 as boolean) from rdb$database
-- @case
select cast(-0e0 as char(16) character set octets) from rdb$database
-- @case
select cast(-0e0 as blob sub_type text) from rdb$database
-- @case
select cast(-0e0 as varchar(10) character set octets) from rdb$database
-- @case
select cast('0' as smallint) from rdb$database
-- @case
select cast('0' as integer) from rdb$database
-- @case
select cast('0' as bigint) from rdb$database
-- @case
select cast('0' as numeric(4,2)) from rdb$database
-- @case
select cast('0' as numeric(9,3)) from rdb$database
-- @case
select cast('0' as numeric(18,4)) from rdb$database
-- @case
select cast('0' as decimal(5,0)) from rdb$database
-- @case
select cast('0' as decimal(18,18)) from rdb$database
-- @case
select cast('0' as float) from rdb$database
-- @case
select cast('0' as double precision) from rdb$database
-- @case
select cast('0' as char(5)) from rdb$database
-- @case
select cast('0' as varchar(5)) from rdb$database
-- @case
select cast('0' as varchar(30)) from rdb$database
-- @case
select cast('0' as time) from rdb$database
-- @case
select cast('0' as boolean) from rdb$database
-- @case
select cast('0' as char(16) character set octets) from rdb$database
-- @case
select cast('0' as blob sub_type text) from rdb$database
-- @case
select cast('0' as varchar(10) character set octets) from rdb$database
-- @case
select cast(' 42 ' as smallint) from rdb$database
-- @case
select cast(' 42 ' as integer) from rdb$database
-- @case
select cast(' 42 ' as bigint) from rdb$database
-- @case
select cast(' 42 ' as numeric(4,2)) from rdb$database
-- @case
select cast(' 42 ' as numeric(9,3)) from rdb$database
-- @case
select cast(' 42 ' as numeric(18,4)) from rdb$database
-- @case
select cast(' 42 ' as decimal(5,0)) from rdb$database
-- @case
select cast(' 42 ' as decimal(18,18)) from rdb$database
-- @case
select cast(' 42 ' as float) from rdb$database
-- @case
select cast(' 42 ' as double precision) from rdb$database
-- @case
select cast(' 42 ' as char(5)) from rdb$database
-- @case
select cast(' 42 ' as varchar(5)) from rdb$database
-- @case
select cast(' 42 ' as varchar(30)) from rdb$database
-- @case
select cast(' 42 ' as time) from rdb$database
-- @case
select cast(' 42 ' as boolean) from rdb$database
-- @case
select cast(' 42 ' as char(16) character set octets) from rdb$database
-- @case
select cast(' 42 ' as blob sub_type text) from rdb$database
-- @case
select cast(' 42 ' as varchar(10) character set octets) from rdb$database
-- @case
select cast('1.5' as smallint) from rdb$database
-- @case
select cast('1.5' as integer) from rdb$database
-- @case
select cast('1.5' as bigint) from rdb$database
-- @case
select cast('1.5' as numeric(4,2)) from rdb$database
-- @case
select cast('1.5' as numeric(9,3)) from rdb$database
-- @case
select cast('1.5' as numeric(18,4)) from rdb$database
-- @case
select cast('1.5' as decimal(5,0)) from rdb$database
-- @case
select cast('1.5' as decimal(18,18)) from rdb$database
-- @case
select cast('1.5' as float) from rdb$database
-- @case
select cast('1.5' as double precision) from rdb$database
-- @case
select cast('1.5' as char(5)) from rdb$database
-- @case
select cast('1.5' as varchar(5)) from rdb$database
-- @case
select cast('1.5' as varchar(30)) from rdb$database
-- @case
select cast('1.5' as time) from rdb$database
-- @case
select cast('1.5' as boolean) from rdb$database
-- @case
select cast('1.5' as char(16) character set octets) from rdb$database
-- @case
select cast('1.5' as blob sub_type text) from rdb$database
-- @case
select cast('1.5' as varchar(10) character set octets) from rdb$database
-- @case
select cast('1e3' as smallint) from rdb$database
-- @case
select cast('1e3' as integer) from rdb$database
-- @case
select cast('1e3' as bigint) from rdb$database
-- @case
select cast('1e3' as numeric(4,2)) from rdb$database
-- @case
select cast('1e3' as numeric(9,3)) from rdb$database
-- @case
select cast('1e3' as numeric(18,4)) from rdb$database
-- @case
select cast('1e3' as decimal(5,0)) from rdb$database
-- @case
select cast('1e3' as decimal(18,18)) from rdb$database
-- @case
select cast('1e3' as float) from rdb$database
-- @case
select cast('1e3' as double precision) from rdb$database
-- @case
select cast('1e3' as char(5)) from rdb$database
-- @case
select cast('1e3' as varchar(5)) from rdb$database
-- @case
select cast('1e3' as varchar(30)) from rdb$database
-- @case
select cast('1e3' as time) from rdb$database
-- @case
select cast('1e3' as boolean) from rdb$database
-- @case
select cast('1e3' as char(16) character set octets) from rdb$database
-- @case
select cast('1e3' as blob sub_type text) from rdb$database
-- @case
select cast('1e3' as varchar(10) character set octets) from rdb$database
-- @case
select cast('abc' as smallint) from rdb$database
-- @case
select cast('abc' as integer) from rdb$database
-- @case
select cast('abc' as bigint) from rdb$database
-- @case
select cast('abc' as numeric(4,2)) from rdb$database
-- @case
select cast('abc' as numeric(9,3)) from rdb$database
-- @case
select cast('abc' as numeric(18,4)) from rdb$database
-- @case
select cast('abc' as decimal(5,0)) from rdb$database
-- @case
select cast('abc' as decimal(18,18)) from rdb$database
-- @case
select cast('abc' as float) from rdb$database
-- @case
select cast('abc' as double precision) from rdb$database
-- @case
select cast('abc' as char(5)) from rdb$database
-- @case
select cast('abc' as varchar(5)) from rdb$database
-- @case
select cast('abc' as varchar(30)) from rdb$database
-- @case
select cast('abc' as date) from rdb$database
-- @case
select cast('abc' as time) from rdb$database
-- @case
select cast('abc' as timestamp) from rdb$database
-- @case
select cast('abc' as boolean) from rdb$database
-- @case
select cast('abc' as char(16) character set octets) from rdb$database
-- @case
select cast('abc' as blob sub_type text) from rdb$database
-- @case
select cast('abc' as varchar(10) character set octets) from rdb$database
-- @case
select cast('' as smallint) from rdb$database
-- @case
select cast('' as integer) from rdb$database
-- @case
select cast('' as bigint) from rdb$database
-- @case
select cast('' as numeric(4,2)) from rdb$database
-- @case
select cast('' as numeric(9,3)) from rdb$database
-- @case
select cast('' as numeric(18,4)) from rdb$database
-- @case
select cast('' as decimal(5,0)) from rdb$database
-- @case
select cast('' as decimal(18,18)) from rdb$database
-- @case
select cast('' as float) from rdb$database
-- @case
select cast('' as double precision) from rdb$database
-- @case
select cast('' as char(5)) from rdb$database
-- @case
select cast('' as varchar(5)) from rdb$database
-- @case
select cast('' as varchar(30)) from rdb$database
-- @case
select cast('' as date) from rdb$database
-- @case
select cast('' as time) from rdb$database
-- @case
select cast('' as timestamp) from rdb$database
-- @case
select cast('' as boolean) from rdb$database
-- @case
select cast('' as char(16) character set octets) from rdb$database
-- @case
select cast('' as blob sub_type text) from rdb$database
-- @case
select cast('' as varchar(10) character set octets) from rdb$database
-- @case
select cast(' ' as smallint) from rdb$database
-- @case
select cast(' ' as integer) from rdb$database
-- @case
select cast(' ' as bigint) from rdb$database
-- @case
select cast(' ' as numeric(4,2)) from rdb$database
-- @case
select cast(' ' as numeric(9,3)) from rdb$database
-- @case
select cast(' ' as numeric(18,4)) from rdb$database
-- @case
select cast(' ' as decimal(5,0)) from rdb$database
-- @case
select cast(' ' as decimal(18,18)) from rdb$database
-- @case
select cast(' ' as float) from rdb$database
-- @case
select cast(' ' as double precision) from rdb$database
-- @case
select cast(' ' as char(5)) from rdb$database
-- @case
select cast(' ' as varchar(5)) from rdb$database
-- @case
select cast(' ' as varchar(30)) from rdb$database
-- @case
select cast(' ' as date) from rdb$database
-- @case
select cast(' ' as time) from rdb$database
-- @case
select cast(' ' as timestamp) from rdb$database
-- @case
select cast(' ' as boolean) from rdb$database
-- @case
select cast(' ' as char(16) character set octets) from rdb$database
-- @case
select cast(' ' as blob sub_type text) from rdb$database
-- @case
select cast(' ' as varchar(10) character set octets) from rdb$database
-- @case
select cast('-0' as smallint) from rdb$database
-- @case
select cast('-0' as integer) from rdb$database
-- @case
select cast('-0' as bigint) from rdb$database
-- @case
select cast('-0' as numeric(4,2)) from rdb$database
-- @case
select cast('-0' as numeric(9,3)) from rdb$database
-- @case
select cast('-0' as numeric(18,4)) from rdb$database
-- @case
select cast('-0' as decimal(5,0)) from rdb$database
-- @case
select cast('-0' as decimal(18,18)) from rdb$database
-- @case
select cast('-0' as float) from rdb$database
-- @case
select cast('-0' as double precision) from rdb$database
-- @case
select cast('-0' as char(5)) from rdb$database
-- @case
select cast('-0' as varchar(5)) from rdb$database
-- @case
select cast('-0' as varchar(30)) from rdb$database
-- @case
select cast('-0' as time) from rdb$database
-- @case
select cast('-0' as boolean) from rdb$database
-- @case
select cast('-0' as char(16) character set octets) from rdb$database
-- @case
select cast('-0' as blob sub_type text) from rdb$database
-- @case
select cast('-0' as varchar(10) character set octets) from rdb$database
-- @case
select cast('+5' as smallint) from rdb$database
-- @case
select cast('+5' as integer) from rdb$database
-- @case
select cast('+5' as bigint) from rdb$database
-- @case
select cast('+5' as numeric(4,2)) from rdb$database
-- @case
select cast('+5' as numeric(9,3)) from rdb$database
-- @case
select cast('+5' as numeric(18,4)) from rdb$database
-- @case
select cast('+5' as decimal(5,0)) from rdb$database
-- @case
select cast('+5' as decimal(18,18)) from rdb$database
-- @case
select cast('+5' as float) from rdb$database
-- @case
select cast('+5' as double precision) from rdb$database
-- @case
select cast('+5' as char(5)) from rdb$database
-- @case
select cast('+5' as varchar(5)) from rdb$database
-- @case
select cast('+5' as varchar(30)) from rdb$database
-- @case
select cast('+5' as time) from rdb$database
-- @case
select cast('+5' as boolean) from rdb$database
-- @case
select cast('+5' as char(16) character set octets) from rdb$database
-- @case
select cast('+5' as blob sub_type text) from rdb$database
-- @case
select cast('+5' as varchar(10) character set octets) from rdb$database
-- @case
select cast('1,5' as smallint) from rdb$database
-- @case
select cast('1,5' as integer) from rdb$database
-- @case
select cast('1,5' as bigint) from rdb$database
-- @case
select cast('1,5' as numeric(4,2)) from rdb$database
-- @case
select cast('1,5' as numeric(9,3)) from rdb$database
-- @case
select cast('1,5' as numeric(18,4)) from rdb$database
-- @case
select cast('1,5' as decimal(5,0)) from rdb$database
-- @case
select cast('1,5' as decimal(18,18)) from rdb$database
-- @case
select cast('1,5' as float) from rdb$database
-- @case
select cast('1,5' as double precision) from rdb$database
-- @case
select cast('1,5' as char(5)) from rdb$database
-- @case
select cast('1,5' as varchar(5)) from rdb$database
-- @case
select cast('1,5' as varchar(30)) from rdb$database
-- @case
select cast('1,5' as time) from rdb$database
-- @case
select cast('1,5' as boolean) from rdb$database
-- @case
select cast('1,5' as char(16) character set octets) from rdb$database
-- @case
select cast('1,5' as blob sub_type text) from rdb$database
-- @case
select cast('1,5' as varchar(10) character set octets) from rdb$database
-- @case
select cast('0x10' as smallint) from rdb$database
-- @case
select cast('0x10' as integer) from rdb$database
-- @case
select cast('0x10' as bigint) from rdb$database
-- @case
select cast('0x10' as numeric(4,2)) from rdb$database
-- @case
select cast('0x10' as numeric(9,3)) from rdb$database
-- @case
select cast('0x10' as numeric(18,4)) from rdb$database
-- @case
select cast('0x10' as decimal(5,0)) from rdb$database
-- @case
select cast('0x10' as decimal(18,18)) from rdb$database
-- @case
select cast('0x10' as float) from rdb$database
-- @case
select cast('0x10' as double precision) from rdb$database
-- @case
select cast('0x10' as char(5)) from rdb$database
-- @case
select cast('0x10' as varchar(5)) from rdb$database
-- @case
select cast('0x10' as varchar(30)) from rdb$database
-- @case
select cast('0x10' as time) from rdb$database
-- @case
select cast('0x10' as boolean) from rdb$database
-- @case
select cast('0x10' as char(16) character set octets) from rdb$database
-- @case
select cast('0x10' as blob sub_type text) from rdb$database
-- @case
select cast('0x10' as varchar(10) character set octets) from rdb$database
-- @case
select cast('12345678901234567890' as smallint) from rdb$database
-- @case
select cast('12345678901234567890' as integer) from rdb$database
-- @case
select cast('12345678901234567890' as bigint) from rdb$database
-- @case
select cast('12345678901234567890' as numeric(4,2)) from rdb$database
-- @case
select cast('12345678901234567890' as numeric(9,3)) from rdb$database
-- @case
select cast('12345678901234567890' as numeric(18,4)) from rdb$database
-- @case
select cast('12345678901234567890' as decimal(5,0)) from rdb$database
-- @case
select cast('12345678901234567890' as decimal(18,18)) from rdb$database
-- @case
select cast('12345678901234567890' as float) from rdb$database
-- @case
select cast('12345678901234567890' as double precision) from rdb$database
-- @case
select cast('12345678901234567890' as char(5)) from rdb$database
-- @case
select cast('12345678901234567890' as varchar(5)) from rdb$database
-- @case
select cast('12345678901234567890' as varchar(30)) from rdb$database
-- @case
select cast('12345678901234567890' as date) from rdb$database
-- @case
select cast('12345678901234567890' as time) from rdb$database
-- @case
select cast('12345678901234567890' as timestamp) from rdb$database
-- @case
select cast('12345678901234567890' as boolean) from rdb$database
-- @case
select cast('12345678901234567890' as char(16) character set octets) from rdb$database
-- @case
select cast('12345678901234567890' as blob sub_type text) from rdb$database
-- @case
select cast('12345678901234567890' as varchar(10) character set octets) from rdb$database
-- @case
select cast('2024-02-29' as smallint) from rdb$database
-- @case
select cast('2024-02-29' as integer) from rdb$database
-- @case
select cast('2024-02-29' as bigint) from rdb$database
-- @case
select cast('2024-02-29' as numeric(4,2)) from rdb$database
-- @case
select cast('2024-02-29' as numeric(9,3)) from rdb$database
-- @case
select cast('2024-02-29' as numeric(18,4)) from rdb$database
-- @case
select cast('2024-02-29' as decimal(5,0)) from rdb$database
-- @case
select cast('2024-02-29' as decimal(18,18)) from rdb$database
-- @case
select cast('2024-02-29' as float) from rdb$database
-- @case
select cast('2024-02-29' as double precision) from rdb$database
-- @case
select cast('2024-02-29' as char(5)) from rdb$database
-- @case
select cast('2024-02-29' as varchar(5)) from rdb$database
-- @case
select cast('2024-02-29' as varchar(30)) from rdb$database
-- @case
select cast('2024-02-29' as date) from rdb$database
-- @case
select cast('2024-02-29' as time) from rdb$database
-- @case
select cast('2024-02-29' as timestamp) from rdb$database
-- @case
select cast('2024-02-29' as boolean) from rdb$database
-- @case
select cast('2024-02-29' as char(16) character set octets) from rdb$database
-- @case
select cast('2024-02-29' as blob sub_type text) from rdb$database
-- @case
select cast('2024-02-29' as varchar(10) character set octets) from rdb$database
-- @case
select cast('2024-02-30' as smallint) from rdb$database
-- @case
select cast('2024-02-30' as integer) from rdb$database
-- @case
select cast('2024-02-30' as bigint) from rdb$database
-- @case
select cast('2024-02-30' as numeric(4,2)) from rdb$database
-- @case
select cast('2024-02-30' as numeric(9,3)) from rdb$database
-- @case
select cast('2024-02-30' as numeric(18,4)) from rdb$database
-- @case
select cast('2024-02-30' as decimal(5,0)) from rdb$database
-- @case
select cast('2024-02-30' as decimal(18,18)) from rdb$database
-- @case
select cast('2024-02-30' as float) from rdb$database
-- @case
select cast('2024-02-30' as double precision) from rdb$database
-- @case
select cast('2024-02-30' as char(5)) from rdb$database
-- @case
select cast('2024-02-30' as varchar(5)) from rdb$database
-- @case
select cast('2024-02-30' as varchar(30)) from rdb$database
-- @case
select cast('2024-02-30' as date) from rdb$database
-- @case
select cast('2024-02-30' as time) from rdb$database
-- @case
select cast('2024-02-30' as timestamp) from rdb$database
-- @case
select cast('2024-02-30' as boolean) from rdb$database
-- @case
select cast('2024-02-30' as char(16) character set octets) from rdb$database
-- @case
select cast('2024-02-30' as blob sub_type text) from rdb$database
-- @case
select cast('2024-02-30' as varchar(10) character set octets) from rdb$database
-- @case
select cast('29.02.2024' as smallint) from rdb$database
-- @case
select cast('29.02.2024' as integer) from rdb$database
-- @case
select cast('29.02.2024' as bigint) from rdb$database
-- @case
select cast('29.02.2024' as numeric(4,2)) from rdb$database
-- @case
select cast('29.02.2024' as numeric(9,3)) from rdb$database
-- @case
select cast('29.02.2024' as numeric(18,4)) from rdb$database
-- @case
select cast('29.02.2024' as decimal(5,0)) from rdb$database
-- @case
select cast('29.02.2024' as decimal(18,18)) from rdb$database
-- @case
select cast('29.02.2024' as float) from rdb$database
-- @case
select cast('29.02.2024' as double precision) from rdb$database
-- @case
select cast('29.02.2024' as char(5)) from rdb$database
-- @case
select cast('29.02.2024' as varchar(5)) from rdb$database
-- @case
select cast('29.02.2024' as varchar(30)) from rdb$database
-- @case
select cast('29.02.2024' as date) from rdb$database
-- @case
select cast('29.02.2024' as time) from rdb$database
-- @case
select cast('29.02.2024' as timestamp) from rdb$database
-- @case
select cast('29.02.2024' as boolean) from rdb$database
-- @case
select cast('29.02.2024' as char(16) character set octets) from rdb$database
-- @case
select cast('29.02.2024' as blob sub_type text) from rdb$database
-- @case
select cast('29.02.2024' as varchar(10) character set octets) from rdb$database
-- @case
select cast('2/29/2024' as smallint) from rdb$database
-- @case
select cast('2/29/2024' as integer) from rdb$database
-- @case
select cast('2/29/2024' as bigint) from rdb$database
-- @case
select cast('2/29/2024' as numeric(4,2)) from rdb$database
-- @case
select cast('2/29/2024' as numeric(9,3)) from rdb$database
-- @case
select cast('2/29/2024' as numeric(18,4)) from rdb$database
-- @case
select cast('2/29/2024' as decimal(5,0)) from rdb$database
-- @case
select cast('2/29/2024' as decimal(18,18)) from rdb$database
-- @case
select cast('2/29/2024' as float) from rdb$database
-- @case
select cast('2/29/2024' as double precision) from rdb$database
-- @case
select cast('2/29/2024' as char(5)) from rdb$database
-- @case
select cast('2/29/2024' as varchar(5)) from rdb$database
-- @case
select cast('2/29/2024' as varchar(30)) from rdb$database
-- @case
select cast('2/29/2024' as date) from rdb$database
-- @case
select cast('2/29/2024' as time) from rdb$database
-- @case
select cast('2/29/2024' as timestamp) from rdb$database
-- @case
select cast('2/29/2024' as boolean) from rdb$database
-- @case
select cast('2/29/2024' as char(16) character set octets) from rdb$database
-- @case
select cast('2/29/2024' as blob sub_type text) from rdb$database
-- @case
select cast('2/29/2024' as varchar(10) character set octets) from rdb$database
-- @case
select cast('13:14:15.1234' as smallint) from rdb$database
-- @case
select cast('13:14:15.1234' as integer) from rdb$database
-- @case
select cast('13:14:15.1234' as bigint) from rdb$database
-- @case
select cast('13:14:15.1234' as numeric(4,2)) from rdb$database
-- @case
select cast('13:14:15.1234' as numeric(9,3)) from rdb$database
-- @case
select cast('13:14:15.1234' as numeric(18,4)) from rdb$database
-- @case
select cast('13:14:15.1234' as decimal(5,0)) from rdb$database
-- @case
select cast('13:14:15.1234' as decimal(18,18)) from rdb$database
-- @case
select cast('13:14:15.1234' as float) from rdb$database
-- @case
select cast('13:14:15.1234' as double precision) from rdb$database
-- @case
select cast('13:14:15.1234' as char(5)) from rdb$database
-- @case
select cast('13:14:15.1234' as varchar(5)) from rdb$database
-- @case
select cast('13:14:15.1234' as varchar(30)) from rdb$database
-- @case
select cast('13:14:15.1234' as date) from rdb$database
-- @case
select cast('13:14:15.1234' as time) from rdb$database
-- @case
select cast('13:14:15.1234' as timestamp) from rdb$database
-- @case
select cast('13:14:15.1234' as boolean) from rdb$database
-- @case
select cast('13:14:15.1234' as char(16) character set octets) from rdb$database
-- @case
select cast('13:14:15.1234' as blob sub_type text) from rdb$database
-- @case
select cast('13:14:15.1234' as varchar(10) character set octets) from rdb$database
-- @case
select cast('25:00:00' as smallint) from rdb$database
-- @case
select cast('25:00:00' as integer) from rdb$database
-- @case
select cast('25:00:00' as bigint) from rdb$database
-- @case
select cast('25:00:00' as numeric(4,2)) from rdb$database
-- @case
select cast('25:00:00' as numeric(9,3)) from rdb$database
-- @case
select cast('25:00:00' as numeric(18,4)) from rdb$database
-- @case
select cast('25:00:00' as decimal(5,0)) from rdb$database
-- @case
select cast('25:00:00' as decimal(18,18)) from rdb$database
-- @case
select cast('25:00:00' as float) from rdb$database
-- @case
select cast('25:00:00' as double precision) from rdb$database
-- @case
select cast('25:00:00' as char(5)) from rdb$database
-- @case
select cast('25:00:00' as varchar(5)) from rdb$database
-- @case
select cast('25:00:00' as varchar(30)) from rdb$database
-- @case
select cast('25:00:00' as date) from rdb$database
-- @case
select cast('25:00:00' as time) from rdb$database
-- @case
select cast('25:00:00' as timestamp) from rdb$database
-- @case
select cast('25:00:00' as boolean) from rdb$database
-- @case
select cast('25:00:00' as char(16) character set octets) from rdb$database
-- @case
select cast('25:00:00' as blob sub_type text) from rdb$database
-- @case
select cast('25:00:00' as varchar(10) character set octets) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as smallint) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as integer) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as bigint) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as numeric(4,2)) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as numeric(9,3)) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as numeric(18,4)) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as decimal(5,0)) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as decimal(18,18)) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as float) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as double precision) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as char(5)) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as varchar(5)) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as varchar(30)) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as date) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as time) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as timestamp) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as boolean) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as char(16) character set octets) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as blob sub_type text) from rdb$database
-- @case
select cast('2024-02-29 13:14:15.1234' as varchar(10) character set octets) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as smallint) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as integer) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as bigint) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as numeric(4,2)) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as numeric(9,3)) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as numeric(18,4)) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as decimal(5,0)) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as decimal(18,18)) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as float) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as double precision) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as char(5)) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as varchar(5)) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as varchar(30)) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as date) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as time) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as timestamp) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as boolean) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as char(16) character set octets) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as blob sub_type text) from rdb$database
-- @case
select cast('2024-02-29T13:14:15' as varchar(10) character set octets) from rdb$database
-- @case
select cast('TRUE' as smallint) from rdb$database
-- @case
select cast('TRUE' as integer) from rdb$database
-- @case
select cast('TRUE' as bigint) from rdb$database
-- @case
select cast('TRUE' as numeric(4,2)) from rdb$database
-- @case
select cast('TRUE' as numeric(9,3)) from rdb$database
-- @case
select cast('TRUE' as numeric(18,4)) from rdb$database
-- @case
select cast('TRUE' as decimal(5,0)) from rdb$database
-- @case
select cast('TRUE' as decimal(18,18)) from rdb$database
-- @case
select cast('TRUE' as float) from rdb$database
-- @case
select cast('TRUE' as double precision) from rdb$database
-- @case
select cast('TRUE' as char(5)) from rdb$database
-- @case
select cast('TRUE' as varchar(5)) from rdb$database
-- @case
select cast('TRUE' as varchar(30)) from rdb$database
-- @case
select cast('TRUE' as date) from rdb$database
-- @case
select cast('TRUE' as time) from rdb$database
-- @case
select cast('TRUE' as timestamp) from rdb$database
-- @case
select cast('TRUE' as boolean) from rdb$database
-- @case
select cast('TRUE' as char(16) character set octets) from rdb$database
-- @case
select cast('TRUE' as blob sub_type text) from rdb$database
-- @case
select cast('TRUE' as varchar(10) character set octets) from rdb$database
-- @case
select cast('false' as smallint) from rdb$database
-- @case
select cast('false' as integer) from rdb$database
-- @case
select cast('false' as bigint) from rdb$database
-- @case
select cast('false' as numeric(4,2)) from rdb$database
-- @case
select cast('false' as numeric(9,3)) from rdb$database
-- @case
select cast('false' as numeric(18,4)) from rdb$database
-- @case
select cast('false' as decimal(5,0)) from rdb$database
-- @case
select cast('false' as decimal(18,18)) from rdb$database
-- @case
select cast('false' as float) from rdb$database
-- @case
select cast('false' as double precision) from rdb$database
-- @case
select cast('false' as char(5)) from rdb$database
-- @case
select cast('false' as varchar(5)) from rdb$database
-- @case
select cast('false' as varchar(30)) from rdb$database
-- @case
select cast('false' as date) from rdb$database
-- @case
select cast('false' as time) from rdb$database
-- @case
select cast('false' as timestamp) from rdb$database
-- @case
select cast('false' as boolean) from rdb$database
-- @case
select cast('false' as char(16) character set octets) from rdb$database
-- @case
select cast('false' as blob sub_type text) from rdb$database
-- @case
select cast('false' as varchar(10) character set octets) from rdb$database
-- @case
select cast('yes' as smallint) from rdb$database
-- @case
select cast('yes' as integer) from rdb$database
-- @case
select cast('yes' as bigint) from rdb$database
-- @case
select cast('yes' as numeric(4,2)) from rdb$database
-- @case
select cast('yes' as numeric(9,3)) from rdb$database
-- @case
select cast('yes' as numeric(18,4)) from rdb$database
-- @case
select cast('yes' as decimal(5,0)) from rdb$database
-- @case
select cast('yes' as decimal(18,18)) from rdb$database
-- @case
select cast('yes' as float) from rdb$database
-- @case
select cast('yes' as double precision) from rdb$database
-- @case
select cast('yes' as char(5)) from rdb$database
-- @case
select cast('yes' as varchar(5)) from rdb$database
-- @case
select cast('yes' as varchar(30)) from rdb$database
-- @case
select cast('yes' as date) from rdb$database
-- @case
select cast('yes' as time) from rdb$database
-- @case
select cast('yes' as timestamp) from rdb$database
-- @case
select cast('yes' as boolean) from rdb$database
-- @case
select cast('yes' as char(16) character set octets) from rdb$database
-- @case
select cast('yes' as blob sub_type text) from rdb$database
-- @case
select cast('yes' as varchar(10) character set octets) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as smallint) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as integer) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as bigint) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as numeric(4,2)) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as numeric(9,3)) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as numeric(18,4)) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as decimal(5,0)) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as decimal(18,18)) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as float) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as double precision) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as char(5)) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as varchar(5)) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as varchar(30)) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as date) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as time) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as timestamp) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as boolean) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as char(16) character set octets) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as blob sub_type text) from rdb$database
-- @case
select cast('A0EEBC99-9C0B-4EF8-BB6D-6BB9BD380A11' as varchar(10) character set octets) from rdb$database
-- @case
select cast(date '2024-02-29' as smallint) from rdb$database
-- @case
select cast(date '2024-02-29' as integer) from rdb$database
-- @case
select cast(date '2024-02-29' as bigint) from rdb$database
-- @case
select cast(date '2024-02-29' as numeric(4,2)) from rdb$database
-- @case
select cast(date '2024-02-29' as numeric(9,3)) from rdb$database
-- @case
select cast(date '2024-02-29' as numeric(18,4)) from rdb$database
-- @case
select cast(date '2024-02-29' as decimal(5,0)) from rdb$database
-- @case
select cast(date '2024-02-29' as decimal(18,18)) from rdb$database
-- @case
select cast(date '2024-02-29' as float) from rdb$database
-- @case
select cast(date '2024-02-29' as double precision) from rdb$database
-- @case
select cast(date '2024-02-29' as char(5)) from rdb$database
-- @case
select cast(date '2024-02-29' as varchar(5)) from rdb$database
-- @case
select cast(date '2024-02-29' as varchar(30)) from rdb$database
-- @case
select cast(date '2024-02-29' as date) from rdb$database
-- @case
select cast(date '2024-02-29' as time) from rdb$database
-- @case
select cast(date '2024-02-29' as timestamp) from rdb$database
-- @case
select cast(date '2024-02-29' as boolean) from rdb$database
-- @case
select cast(date '2024-02-29' as char(16) character set octets) from rdb$database
-- @case
select cast(date '2024-02-29' as blob sub_type text) from rdb$database
-- @case
select cast(date '2024-02-29' as varchar(10) character set octets) from rdb$database
-- @case
select cast(time '23:59:59.9999' as smallint) from rdb$database
-- @case
select cast(time '23:59:59.9999' as integer) from rdb$database
-- @case
select cast(time '23:59:59.9999' as bigint) from rdb$database
-- @case
select cast(time '23:59:59.9999' as numeric(4,2)) from rdb$database
-- @case
select cast(time '23:59:59.9999' as numeric(9,3)) from rdb$database
-- @case
select cast(time '23:59:59.9999' as numeric(18,4)) from rdb$database
-- @case
select cast(time '23:59:59.9999' as decimal(5,0)) from rdb$database
-- @case
select cast(time '23:59:59.9999' as decimal(18,18)) from rdb$database
-- @case
select cast(time '23:59:59.9999' as float) from rdb$database
-- @case
select cast(time '23:59:59.9999' as double precision) from rdb$database
-- @case
select cast(time '23:59:59.9999' as char(5)) from rdb$database
-- @case
select cast(time '23:59:59.9999' as varchar(5)) from rdb$database
-- @case
select cast(time '23:59:59.9999' as varchar(30)) from rdb$database
-- @case
select cast(time '23:59:59.9999' as date) from rdb$database
-- @case
select cast(time '23:59:59.9999' as time) from rdb$database
-- @case
select cast(time '23:59:59.9999' as timestamp) from rdb$database where 1 = 0
-- @case
select cast(time '23:59:59.9999' as timestamp) = current_date + time '23:59:59.9999' from rdb$database
-- @case
select cast(time '23:59:59.9999' as boolean) from rdb$database
-- @case
select cast(time '23:59:59.9999' as char(16) character set octets) from rdb$database
-- @case
select cast(time '23:59:59.9999' as blob sub_type text) from rdb$database
-- @case
select cast(time '23:59:59.9999' as varchar(10) character set octets) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as smallint) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as integer) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as bigint) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as numeric(4,2)) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as numeric(9,3)) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as numeric(18,4)) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as decimal(5,0)) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as decimal(18,18)) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as float) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as double precision) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as char(5)) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as varchar(5)) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as varchar(30)) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as date) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as time) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as timestamp) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as boolean) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as char(16) character set octets) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as blob sub_type text) from rdb$database
-- @case
select cast(timestamp '2024-02-29 13:14:15.1234' as varchar(10) character set octets) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as smallint) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as integer) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as bigint) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as numeric(4,2)) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as numeric(9,3)) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as numeric(18,4)) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as decimal(5,0)) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as decimal(18,18)) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as float) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as double precision) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as char(5)) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as varchar(5)) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as varchar(30)) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as date) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as time) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as timestamp) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as boolean) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as char(16) character set octets) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as blob sub_type text) from rdb$database
-- @case
select cast(timestamp '0001-01-01 00:00:00' as varchar(10) character set octets) from rdb$database
-- @case
select cast(true as smallint) from rdb$database
-- @case
select cast(true as integer) from rdb$database
-- @case
select cast(true as bigint) from rdb$database
-- @case
select cast(true as numeric(4,2)) from rdb$database
-- @case
select cast(true as numeric(9,3)) from rdb$database
-- @case
select cast(true as numeric(18,4)) from rdb$database
-- @case
select cast(true as decimal(5,0)) from rdb$database
-- @case
select cast(true as decimal(18,18)) from rdb$database
-- @case
select cast(true as float) from rdb$database
-- @case
select cast(true as double precision) from rdb$database
-- @case
select cast(true as char(5)) from rdb$database
-- @case
select cast(true as varchar(5)) from rdb$database
-- @case
select cast(true as varchar(30)) from rdb$database
-- @case
select cast(true as date) from rdb$database
-- @case
select cast(true as time) from rdb$database
-- @case
select cast(true as timestamp) from rdb$database
-- @case
select cast(true as boolean) from rdb$database
-- @case
select cast(true as char(16) character set octets) from rdb$database
-- @case
select cast(true as blob sub_type text) from rdb$database
-- @case
select cast(true as varchar(10) character set octets) from rdb$database
-- @case
select cast(false as smallint) from rdb$database
-- @case
select cast(false as integer) from rdb$database
-- @case
select cast(false as bigint) from rdb$database
-- @case
select cast(false as numeric(4,2)) from rdb$database
-- @case
select cast(false as numeric(9,3)) from rdb$database
-- @case
select cast(false as numeric(18,4)) from rdb$database
-- @case
select cast(false as decimal(5,0)) from rdb$database
-- @case
select cast(false as decimal(18,18)) from rdb$database
-- @case
select cast(false as float) from rdb$database
-- @case
select cast(false as double precision) from rdb$database
-- @case
select cast(false as char(5)) from rdb$database
-- @case
select cast(false as varchar(5)) from rdb$database
-- @case
select cast(false as varchar(30)) from rdb$database
-- @case
select cast(false as date) from rdb$database
-- @case
select cast(false as time) from rdb$database
-- @case
select cast(false as timestamp) from rdb$database
-- @case
select cast(false as boolean) from rdb$database
-- @case
select cast(false as char(16) character set octets) from rdb$database
-- @case
select cast(false as blob sub_type text) from rdb$database
-- @case
select cast(false as varchar(10) character set octets) from rdb$database
-- @case
select cast(null as smallint) from rdb$database
-- @case
select cast(null as integer) from rdb$database
-- @case
select cast(null as bigint) from rdb$database
-- @case
select cast(null as numeric(4,2)) from rdb$database
-- @case
select cast(null as numeric(9,3)) from rdb$database
-- @case
select cast(null as numeric(18,4)) from rdb$database
-- @case
select cast(null as decimal(5,0)) from rdb$database
-- @case
select cast(null as decimal(18,18)) from rdb$database
-- @case
select cast(null as float) from rdb$database
-- @case
select cast(null as double precision) from rdb$database
-- @case
select cast(null as char(5)) from rdb$database
-- @case
select cast(null as varchar(5)) from rdb$database
-- @case
select cast(null as varchar(30)) from rdb$database
-- @case
select cast(null as date) from rdb$database
-- @case
select cast(null as time) from rdb$database
-- @case
select cast(null as timestamp) from rdb$database
-- @case
select cast(null as boolean) from rdb$database
-- @case
select cast(null as char(16) character set octets) from rdb$database
-- @case
select cast(null as blob sub_type text) from rdb$database
-- @case
select cast(null as varchar(10) character set octets) from rdb$database
-- @case
select cast(x'414243' as smallint) from rdb$database
-- @case
select cast(x'414243' as integer) from rdb$database
-- @case
select cast(x'414243' as bigint) from rdb$database
-- @case
select cast(x'414243' as numeric(4,2)) from rdb$database
-- @case
select cast(x'414243' as numeric(9,3)) from rdb$database
-- @case
select cast(x'414243' as numeric(18,4)) from rdb$database
-- @case
select cast(x'414243' as decimal(5,0)) from rdb$database
-- @case
select cast(x'414243' as decimal(18,18)) from rdb$database
-- @case
select cast(x'414243' as float) from rdb$database
-- @case
select cast(x'414243' as double precision) from rdb$database
-- @case
select cast(x'414243' as char(5)) from rdb$database
-- @case
select cast(x'414243' as varchar(5)) from rdb$database
-- @case
select cast(x'414243' as varchar(30)) from rdb$database
-- @case
select cast(x'414243' as date) from rdb$database
-- @case
select cast(x'414243' as time) from rdb$database
-- @case
select cast(x'414243' as timestamp) from rdb$database
-- @case
select cast(x'414243' as boolean) from rdb$database
-- @case
select cast(x'414243' as char(16) character set octets) from rdb$database
-- @case
select cast(x'414243' as blob sub_type text) from rdb$database
-- @case
select cast(x'414243' as varchar(10) character set octets) from rdb$database
-- @case
select cast(_utf8 'äöü' as smallint) from rdb$database
-- @case
select cast(_utf8 'äöü' as integer) from rdb$database
-- @case
select cast(_utf8 'äöü' as bigint) from rdb$database
-- @case
select cast(_utf8 'äöü' as numeric(4,2)) from rdb$database
-- @case
select cast(_utf8 'äöü' as numeric(9,3)) from rdb$database
-- @case
select cast(_utf8 'äöü' as numeric(18,4)) from rdb$database
-- @case
select cast(_utf8 'äöü' as decimal(5,0)) from rdb$database
-- @case
select cast(_utf8 'äöü' as decimal(18,18)) from rdb$database
-- @case
select cast(_utf8 'äöü' as float) from rdb$database
-- @case
select cast(_utf8 'äöü' as double precision) from rdb$database
-- @case
select cast(_utf8 'äöü' as char(5)) from rdb$database
-- @case
select cast(_utf8 'äöü' as varchar(5)) from rdb$database
-- @case
select cast(_utf8 'äöü' as varchar(30)) from rdb$database
-- @case
select cast(_utf8 'äöü' as date) from rdb$database
-- @case
select cast(_utf8 'äöü' as time) from rdb$database
-- @case
select cast(_utf8 'äöü' as timestamp) from rdb$database
-- @case
select cast(_utf8 'äöü' as boolean) from rdb$database
-- @case
select cast(_utf8 'äöü' as char(16) character set octets) from rdb$database
-- @case
select cast(_utf8 'äöü' as blob sub_type text) from rdb$database
-- @case
select cast(_utf8 'äöü' as varchar(10) character set octets) from rdb$database
