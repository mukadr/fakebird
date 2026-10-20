-- String functions, padding, comparisons, LIKE/SIMILAR TO/CONTAINING/STARTING, collations and lengths.
-- @setup
create table st (id integer not null primary key, c5 char(5), v10 varchar(10), v1 varchar(1), c1 char(1),
    ci varchar(10) collate unicode_ci, cai varchar(10) collate unicode_ci_ai, o8 varchar(8) character set octets,
    w varchar(10) character set win1252, a varchar(10) character set ascii, n varchar(10) character set none, bt blob sub_type text);
insert into st values (1, 'ab', 'ab', 'a', 'a', 'Ab', 'Ábc', 'ab', 'àb', 'ab', 'ab', 'blob text');
insert into st values (2, 'ab   ', 'ab   ', ' ', ' ', 'aB', 'abc', 'AB', 'ÀB', 'AB', 'AB', '');
insert into st values (3, '', '', '', '', '', '', '', '', '', '', null);
insert into st values (4, null, null, null, null, null, null, null, null, null, null, null);
insert into st values (5, 'çãé', 'çãé€😀', 'é', 'é', 'ÇÃÉ', 'cae', 'x', 'çã', 'xy', 'xy', 'çãé€😀');
insert into st values (6, ' x', ' x ', 'x', 'x', 'X', 'X', ' x', ' x', ' x', ' x', ' x ');
-- @case
select id, c5, char_length(c5), octet_length(c5), bit_length(c5), c5 || '|' from st order by id
-- @case
select id, upper(c5), lower(c5), trim(c5), trim(leading from c5), trim(trailing from c5), reverse(c5) from st order by id
-- @case
select id, substring(c5 from 2), substring(c5 from 2 for 2), substring(c5 from 0 for 2), substring(c5 from -1 for 3), left(c5, 2), right(c5, 2) from st order by id
-- @case
select id, position('b' in c5), position('b', c5, 2), lpad(c5, 6, '*'), rpad(c5, 6, '-'), lpad(c5, 1), rpad(c5, 0) from st order by id
-- @case
select id, replace(c5, 'b', 'XYZ'), replace(c5, '', 'x') from st order by id
-- @case
select id, overlay(c5 placing 'ZZ' from 2), overlay(c5 placing 'ZZ' from 2 for 0) from st order by id
-- @case
select id, c5 = 'ab', c5 = 'ab  ', c5 < 'b', c5 > 'AB', c5 like 'a%', c5 like '_b', c5 starting with 'a', c5 containing 'B' from st order by id
-- @case
select id, c5 similar to 'a[bB]%', c5 like '%', c5 is distinct from 'ab', coalesce(c5, '?') from st order by id
-- @case
select max(c5), min(c5), count(distinct c5), list(c5, ';') from st
-- @case
select id from st order by c5, id
-- @case
select id from st order by c5 desc, id
-- @case
select c5, count(*) from st group by c5 order by 1
-- @case
select id, v10, char_length(v10), octet_length(v10), bit_length(v10), v10 || '|' from st order by id
-- @case
select id, upper(v10), lower(v10), trim(v10), trim(leading from v10), trim(trailing from v10), reverse(v10) from st order by id
-- @case
select id, substring(v10 from 2), substring(v10 from 2 for 2), substring(v10 from 0 for 2), substring(v10 from -1 for 3), left(v10, 2), right(v10, 2) from st order by id
-- @case
select id, position('b' in v10), position('b', v10, 2), lpad(v10, 6, '*'), rpad(v10, 6, '-'), lpad(v10, 1), rpad(v10, 0) from st order by id
-- @case
select id, replace(v10, 'b', 'XYZ'), replace(v10, '', 'x') from st order by id
-- @case
select id, overlay(v10 placing 'ZZ' from 2), overlay(v10 placing 'ZZ' from 2 for 0) from st order by id
-- @case
select id, v10 = 'ab', v10 = 'ab  ', v10 < 'b', v10 > 'AB', v10 like 'a%', v10 like '_b', v10 starting with 'a', v10 containing 'B' from st order by id
-- @case
select id, v10 similar to 'a[bB]%', v10 like '%', v10 is distinct from 'ab', coalesce(v10, '?') from st order by id
-- @case
select max(v10), min(v10), count(distinct v10), list(v10, ';') from st
-- @case
select id from st order by v10, id
-- @case
select id from st order by v10 desc, id
-- @case
select v10, count(*) from st group by v10 order by 1
-- @case
select id, v1, char_length(v1), octet_length(v1), bit_length(v1), v1 || '|' from st order by id
-- @case
select id, upper(v1), lower(v1), trim(v1), trim(leading from v1), trim(trailing from v1), reverse(v1) from st order by id
-- @case
select id, substring(v1 from 2), substring(v1 from 2 for 2), substring(v1 from 0 for 2), substring(v1 from -1 for 3), left(v1, 2), right(v1, 2) from st order by id
-- @case
select id, position('b' in v1), position('b', v1, 2), lpad(v1, 6, '*'), rpad(v1, 6, '-'), lpad(v1, 1), rpad(v1, 0) from st order by id
-- @case
select id, replace(v1, 'b', 'XYZ'), replace(v1, '', 'x') from st order by id
-- @case
select id, overlay(v1 placing 'ZZ' from 2), overlay(v1 placing 'ZZ' from 2 for 0) from st order by id
-- @case
select id, v1 = 'ab', v1 = 'ab  ', v1 < 'b', v1 > 'AB', v1 like 'a%', v1 like '_b', v1 starting with 'a', v1 containing 'B' from st order by id
-- @case
select id, v1 similar to 'a[bB]%', v1 like '%', v1 is distinct from 'ab', coalesce(v1, '?') from st order by id
-- @case
select max(v1), min(v1), count(distinct v1), list(v1, ';') from st
-- @case
select id from st order by v1, id
-- @case
select id from st order by v1 desc, id
-- @case
select v1, count(*) from st group by v1 order by 1
-- @case
select id, c1, char_length(c1), octet_length(c1), bit_length(c1), c1 || '|' from st order by id
-- @case
select id, upper(c1), lower(c1), trim(c1), trim(leading from c1), trim(trailing from c1), reverse(c1) from st order by id
-- @case
select id, substring(c1 from 2), substring(c1 from 2 for 2), substring(c1 from 0 for 2), substring(c1 from -1 for 3), left(c1, 2), right(c1, 2) from st order by id
-- @case
select id, position('b' in c1), position('b', c1, 2), lpad(c1, 6, '*'), rpad(c1, 6, '-'), lpad(c1, 1), rpad(c1, 0) from st order by id
-- @case
select id, replace(c1, 'b', 'XYZ'), replace(c1, '', 'x') from st order by id
-- @case
select id, overlay(c1 placing 'ZZ' from 2), overlay(c1 placing 'ZZ' from 2 for 0) from st order by id
-- @case
select id, c1 = 'ab', c1 = 'ab  ', c1 < 'b', c1 > 'AB', c1 like 'a%', c1 like '_b', c1 starting with 'a', c1 containing 'B' from st order by id
-- @case
select id, c1 similar to 'a[bB]%', c1 like '%', c1 is distinct from 'ab', coalesce(c1, '?') from st order by id
-- @case
select max(c1), min(c1), count(distinct c1), list(c1, ';') from st
-- @case
select id from st order by c1, id
-- @case
select id from st order by c1 desc, id
-- @case
select c1, count(*) from st group by c1 order by 1
-- @case
select id, ci, char_length(ci), octet_length(ci), bit_length(ci), ci || '|' from st order by id
-- @case
select id, upper(ci), lower(ci), trim(ci), trim(leading from ci), trim(trailing from ci), reverse(ci) from st order by id
-- @case
select id, substring(ci from 2), substring(ci from 2 for 2), substring(ci from 0 for 2), substring(ci from -1 for 3), left(ci, 2), right(ci, 2) from st order by id
-- @case
select id, position('b' in ci), position('b', ci, 2), lpad(ci, 6, '*'), rpad(ci, 6, '-'), lpad(ci, 1), rpad(ci, 0) from st order by id
-- @case
select id, replace(ci, 'b', 'XYZ'), replace(ci, '', 'x') from st order by id
-- @case
select id, overlay(ci placing 'ZZ' from 2), overlay(ci placing 'ZZ' from 2 for 0) from st order by id
-- @case
select id, ci = 'ab', ci = 'ab  ', ci < 'b', ci > 'AB', ci like 'a%', ci like '_b', ci starting with 'a', ci containing 'B' from st order by id
-- @case
select id, ci similar to 'a[bB]%', ci like '%', ci is distinct from 'ab', coalesce(ci, '?') from st order by id
-- @case
select max(ci), min(ci), count(distinct ci), list(ci, ';') from st
-- @case
select id from st order by ci, id
-- @case
select id from st order by ci desc, id
-- @case
select ci, count(*) from st group by ci order by 1
-- @case
select id, cai, char_length(cai), octet_length(cai), bit_length(cai), cai || '|' from st order by id
-- @case
select id, upper(cai), lower(cai), trim(cai), trim(leading from cai), trim(trailing from cai), reverse(cai) from st order by id
-- @case
select id, substring(cai from 2), substring(cai from 2 for 2), substring(cai from 0 for 2), substring(cai from -1 for 3), left(cai, 2), right(cai, 2) from st order by id
-- @case
select id, position('b' in cai), position('b', cai, 2), lpad(cai, 6, '*'), rpad(cai, 6, '-'), lpad(cai, 1), rpad(cai, 0) from st order by id
-- @case
select id, replace(cai, 'b', 'XYZ'), replace(cai, '', 'x') from st order by id
-- @case
select id, overlay(cai placing 'ZZ' from 2), overlay(cai placing 'ZZ' from 2 for 0) from st order by id
-- @case
select id, cai = 'ab', cai = 'ab  ', cai < 'b', cai > 'AB', cai like 'a%', cai like '_b', cai starting with 'a', cai containing 'B' from st order by id
-- @case
select id, cai similar to 'a[bB]%', cai like '%', cai is distinct from 'ab', coalesce(cai, '?') from st order by id
-- @case
select max(cai), min(cai), count(distinct cai), list(cai, ';') from st
-- @case
select id from st order by cai, id
-- @case
select id from st order by cai desc, id
-- @case
select cai, count(*) from st group by cai order by 1
-- @case
select id, o8, char_length(o8), octet_length(o8), bit_length(o8), o8 || '|' from st order by id
-- @case
select id, upper(o8), lower(o8), trim(o8), trim(leading from o8), trim(trailing from o8), reverse(o8) from st order by id
-- @case
select id, substring(o8 from 2), substring(o8 from 2 for 2), substring(o8 from 0 for 2), substring(o8 from -1 for 3), left(o8, 2), right(o8, 2) from st order by id
-- @case
select id, position('b' in o8), position('b', o8, 2), lpad(o8, 6, '*'), rpad(o8, 6, '-'), lpad(o8, 1), rpad(o8, 0) from st order by id
-- @case
select id, replace(o8, 'b', 'XYZ'), replace(o8, '', 'x') from st order by id
-- @case
select id, o8 = 'ab', o8 = 'ab  ', o8 < 'b', o8 > 'AB', o8 like 'a%', o8 like '_b', o8 starting with 'a', o8 containing 'B' from st order by id
-- @case
select id, o8 similar to 'a[bB]%', o8 like '%', o8 is distinct from 'ab', coalesce(o8, '?') from st order by id
-- @case
select max(o8), min(o8), count(distinct o8), list(o8, ';') from st
-- @case
select id from st order by o8, id
-- @case
select id from st order by o8 desc, id
-- @case
select o8, count(*) from st group by o8 order by 1
-- @case
select id, w, char_length(w), octet_length(w), bit_length(w), w || '|' from st order by id
-- @case
select id, upper(w), lower(w), trim(w), trim(leading from w), trim(trailing from w), reverse(w) from st order by id
-- @case
select id, substring(w from 2), substring(w from 2 for 2), substring(w from 0 for 2), substring(w from -1 for 3), left(w, 2), right(w, 2) from st order by id
-- @case
select id, position('b' in w), position('b', w, 2), lpad(w, 6, '*'), rpad(w, 6, '-'), lpad(w, 1), rpad(w, 0) from st order by id
-- @case
select id, replace(w, 'b', 'XYZ'), replace(w, '', 'x') from st order by id
-- @case
select id, overlay(w placing 'ZZ' from 2), overlay(w placing 'ZZ' from 2 for 0) from st order by id
-- @case
select id, w = 'ab', w = 'ab  ', w < 'b', w > 'AB', w like 'a%', w like '_b', w starting with 'a', w containing 'B' from st order by id
-- @case
select id, w similar to 'a[bB]%', w like '%', w is distinct from 'ab', coalesce(w, '?') from st order by id
-- @case
select max(w), min(w), count(distinct w), list(w, ';') from st
-- @case
select id from st order by w, id
-- @case
select id from st order by w desc, id
-- @case
select w, count(*) from st group by w order by 1
-- @case
select id, a, char_length(a), octet_length(a), bit_length(a), a || '|' from st order by id
-- @case
select id, upper(a), lower(a), trim(a), trim(leading from a), trim(trailing from a), reverse(a) from st order by id
-- @case
select id, substring(a from 2), substring(a from 2 for 2), substring(a from 0 for 2), substring(a from -1 for 3), left(a, 2), right(a, 2) from st order by id
-- @case
select id, position('b' in a), position('b', a, 2), lpad(a, 6, '*'), rpad(a, 6, '-'), lpad(a, 1), rpad(a, 0) from st order by id
-- @case
select id, replace(a, 'b', 'XYZ'), replace(a, '', 'x') from st order by id
-- @case
select id, overlay(a placing 'ZZ' from 2), overlay(a placing 'ZZ' from 2 for 0) from st order by id
-- @case
select id, a = 'ab', a = 'ab  ', a < 'b', a > 'AB', a like 'a%', a like '_b', a starting with 'a', a containing 'B' from st order by id
-- @case
select id, a similar to 'a[bB]%', a like '%', a is distinct from 'ab', coalesce(a, '?') from st order by id
-- @case
select max(a), min(a), count(distinct a), list(a, ';') from st
-- @case
select id from st order by a, id
-- @case
select id from st order by a desc, id
-- @case
select a, count(*) from st group by a order by 1
-- @case
select id, n, char_length(n), octet_length(n), bit_length(n), n || '|' from st order by id
-- @case
select id, upper(n), lower(n), trim(n), trim(leading from n), trim(trailing from n), reverse(n) from st order by id
-- @case
select id, substring(n from 2), substring(n from 2 for 2), substring(n from 0 for 2), substring(n from -1 for 3), left(n, 2), right(n, 2) from st order by id
-- @case
select id, position('b' in n), position('b', n, 2), lpad(n, 6, '*'), rpad(n, 6, '-'), lpad(n, 1), rpad(n, 0) from st order by id
-- @case
select id, replace(n, 'b', 'XYZ'), replace(n, '', 'x') from st order by id
-- @case
select id, overlay(n placing 'ZZ' from 2), overlay(n placing 'ZZ' from 2 for 0) from st order by id
-- @case
select id, n = 'ab', n = 'ab  ', n < 'b', n > 'AB', n like 'a%', n like '_b', n starting with 'a', n containing 'B' from st order by id
-- @case
select id, n similar to 'a[bB]%', n like '%', n is distinct from 'ab', coalesce(n, '?') from st order by id
-- @case
select max(n), min(n), count(distinct n), list(n, ';') from st
-- @case
select id from st order by n, id
-- @case
select id from st order by n desc, id
-- @case
select n, count(*) from st group by n order by 1
-- @case
select id, bt, char_length(bt), octet_length(bt), bit_length(bt), bt || '|' from st order by id
-- @case
select id, upper(bt), lower(bt), trim(bt), trim(leading from bt), trim(trailing from bt), reverse(bt) from st order by id
-- @case
select id, substring(bt from 2), substring(bt from 2 for 2), substring(bt from 0 for 2), substring(bt from -1 for 3), left(bt, 2), right(bt, 2) from st order by id
-- @case
select id, position('b' in bt), position('b', bt, 2), lpad(bt, 6, '*'), rpad(bt, 6, '-'), lpad(bt, 1), rpad(bt, 0) from st order by id
-- @case
select id, replace(bt, 'b', 'XYZ'), replace(bt, '', 'x') from st order by id
-- @case
select id, overlay(bt placing 'ZZ' from 2), overlay(bt placing 'ZZ' from 2 for 0) from st order by id
-- @case
select id, bt = 'ab', bt = 'ab  ', bt < 'b', bt > 'AB', bt like 'a%', bt like '_b', bt starting with 'a', bt containing 'B' from st order by id
-- @case
select id, bt similar to 'a[bB]%', bt like '%', bt is distinct from 'ab', coalesce(bt, '?') from st order by id
-- @case
select max(bt), min(bt), count(distinct bt), list(bt, ';') from st
-- @case
select id from st order by bt, id
-- @case
select id from st order by bt desc, id
-- @case
select bt, count(*) from st group by bt order by 1
-- @case
select ascii_val('A') from rdb$database
-- @case
select ascii_val('') from rdb$database
-- @case
select ascii_val('é') from rdb$database
-- @case
select ascii_val('ab') from rdb$database
-- @case
select ascii_val(null) from rdb$database
-- @case
select ascii_char(65) from rdb$database
-- @case
select ascii_char(0) from rdb$database
-- @case
select ascii_char(255) from rdb$database
-- @case
select ascii_char(256) from rdb$database
-- @case
select ascii_char(-1) from rdb$database
-- @case
select ascii_char(null) from rdb$database
-- @case
select hash('abc') from rdb$database
-- @case
select hash('') from rdb$database
-- @case
select hash(null) from rdb$database
-- @case
select hash(1) from rdb$database
-- @case
select unicode_val('é') from rdb$database
-- @case
select lower('ÀÉÎÕÜ') from rdb$database
-- @case
select lower('İ') from rdb$database
-- @case
select lower('ß') from rdb$database
-- @case
select upper('àéîõü') from rdb$database
-- @case
select upper('ß') from rdb$database
-- @case
select upper('ﬁ') from rdb$database
-- @case
select upper('ı') from rdb$database
-- @case
select '' = '', '' < '', '' like '', '' starting with '', '' containing '', '' || '' from rdb$database
-- @case
select '' = ' ', '' < ' ', '' like ' ', '' starting with ' ', '' containing ' ', '' || ' ' from rdb$database
-- @case
select '' = 'a', '' < 'a', '' like 'a', '' starting with 'a', '' containing 'a', '' || 'a' from rdb$database
-- @case
select '' = 'a ', '' < 'a ', '' like 'a ', '' starting with 'a ', '' containing 'a ', '' || 'a ' from rdb$database
-- @case
select '' = ' a', '' < ' a', '' like ' a', '' starting with ' a', '' containing ' a', '' || ' a' from rdb$database
-- @case
select '' = '%', '' < '%', '' like '%', '' starting with '%', '' containing '%', '' || '%' from rdb$database
-- @case
select '' = '_', '' < '_', '' like '_', '' starting with '_', '' containing '_', '' || '_' from rdb$database
-- @case
select '' = '\', '' < '\', '' like '\', '' starting with '\', '' containing '\', '' || '\' from rdb$database
-- @case
select '' = 'é', '' < 'é', '' like 'é', '' starting with 'é', '' containing 'é', '' || 'é' from rdb$database
-- @case
select '' = 'É', '' < 'É', '' like 'É', '' starting with 'É', '' containing 'É', '' || 'É' from rdb$database
-- @case
select '' = '😀', '' < '😀', '' like '😀', '' starting with '😀', '' containing '😀', '' || '😀' from rdb$database
-- @case
select ' ' = '', ' ' < '', ' ' like '', ' ' starting with '', ' ' containing '', ' ' || '' from rdb$database
-- @case
select ' ' = ' ', ' ' < ' ', ' ' like ' ', ' ' starting with ' ', ' ' containing ' ', ' ' || ' ' from rdb$database
-- @case
select ' ' = 'a', ' ' < 'a', ' ' like 'a', ' ' starting with 'a', ' ' containing 'a', ' ' || 'a' from rdb$database
-- @case
select ' ' = 'a ', ' ' < 'a ', ' ' like 'a ', ' ' starting with 'a ', ' ' containing 'a ', ' ' || 'a ' from rdb$database
-- @case
select ' ' = ' a', ' ' < ' a', ' ' like ' a', ' ' starting with ' a', ' ' containing ' a', ' ' || ' a' from rdb$database
-- @case
select ' ' = '%', ' ' < '%', ' ' like '%', ' ' starting with '%', ' ' containing '%', ' ' || '%' from rdb$database
-- @case
select ' ' = '_', ' ' < '_', ' ' like '_', ' ' starting with '_', ' ' containing '_', ' ' || '_' from rdb$database
-- @case
select ' ' = '\', ' ' < '\', ' ' like '\', ' ' starting with '\', ' ' containing '\', ' ' || '\' from rdb$database
-- @case
select ' ' = 'é', ' ' < 'é', ' ' like 'é', ' ' starting with 'é', ' ' containing 'é', ' ' || 'é' from rdb$database
-- @case
select ' ' = 'É', ' ' < 'É', ' ' like 'É', ' ' starting with 'É', ' ' containing 'É', ' ' || 'É' from rdb$database
-- @case
select ' ' = '😀', ' ' < '😀', ' ' like '😀', ' ' starting with '😀', ' ' containing '😀', ' ' || '😀' from rdb$database
-- @case
select 'a' = '', 'a' < '', 'a' like '', 'a' starting with '', 'a' containing '', 'a' || '' from rdb$database
-- @case
select 'a' = ' ', 'a' < ' ', 'a' like ' ', 'a' starting with ' ', 'a' containing ' ', 'a' || ' ' from rdb$database
-- @case
select 'a' = 'a', 'a' < 'a', 'a' like 'a', 'a' starting with 'a', 'a' containing 'a', 'a' || 'a' from rdb$database
-- @case
select 'a' = 'a ', 'a' < 'a ', 'a' like 'a ', 'a' starting with 'a ', 'a' containing 'a ', 'a' || 'a ' from rdb$database
-- @case
select 'a' = ' a', 'a' < ' a', 'a' like ' a', 'a' starting with ' a', 'a' containing ' a', 'a' || ' a' from rdb$database
-- @case
select 'a' = '%', 'a' < '%', 'a' like '%', 'a' starting with '%', 'a' containing '%', 'a' || '%' from rdb$database
-- @case
select 'a' = '_', 'a' < '_', 'a' like '_', 'a' starting with '_', 'a' containing '_', 'a' || '_' from rdb$database
-- @case
select 'a' = '\', 'a' < '\', 'a' like '\', 'a' starting with '\', 'a' containing '\', 'a' || '\' from rdb$database
-- @case
select 'a' = 'é', 'a' < 'é', 'a' like 'é', 'a' starting with 'é', 'a' containing 'é', 'a' || 'é' from rdb$database
-- @case
select 'a' = 'É', 'a' < 'É', 'a' like 'É', 'a' starting with 'É', 'a' containing 'É', 'a' || 'É' from rdb$database
-- @case
select 'a' = '😀', 'a' < '😀', 'a' like '😀', 'a' starting with '😀', 'a' containing '😀', 'a' || '😀' from rdb$database
-- @case
select 'a ' = '', 'a ' < '', 'a ' like '', 'a ' starting with '', 'a ' containing '', 'a ' || '' from rdb$database
-- @case
select 'a ' = ' ', 'a ' < ' ', 'a ' like ' ', 'a ' starting with ' ', 'a ' containing ' ', 'a ' || ' ' from rdb$database
-- @case
select 'a ' = 'a', 'a ' < 'a', 'a ' like 'a', 'a ' starting with 'a', 'a ' containing 'a', 'a ' || 'a' from rdb$database
-- @case
select 'a ' = 'a ', 'a ' < 'a ', 'a ' like 'a ', 'a ' starting with 'a ', 'a ' containing 'a ', 'a ' || 'a ' from rdb$database
-- @case
select 'a ' = ' a', 'a ' < ' a', 'a ' like ' a', 'a ' starting with ' a', 'a ' containing ' a', 'a ' || ' a' from rdb$database
-- @case
select 'a ' = '%', 'a ' < '%', 'a ' like '%', 'a ' starting with '%', 'a ' containing '%', 'a ' || '%' from rdb$database
-- @case
select 'a ' = '_', 'a ' < '_', 'a ' like '_', 'a ' starting with '_', 'a ' containing '_', 'a ' || '_' from rdb$database
-- @case
select 'a ' = '\', 'a ' < '\', 'a ' like '\', 'a ' starting with '\', 'a ' containing '\', 'a ' || '\' from rdb$database
-- @case
select 'a ' = 'é', 'a ' < 'é', 'a ' like 'é', 'a ' starting with 'é', 'a ' containing 'é', 'a ' || 'é' from rdb$database
-- @case
select 'a ' = 'É', 'a ' < 'É', 'a ' like 'É', 'a ' starting with 'É', 'a ' containing 'É', 'a ' || 'É' from rdb$database
-- @case
select 'a ' = '😀', 'a ' < '😀', 'a ' like '😀', 'a ' starting with '😀', 'a ' containing '😀', 'a ' || '😀' from rdb$database
-- @case
select ' a' = '', ' a' < '', ' a' like '', ' a' starting with '', ' a' containing '', ' a' || '' from rdb$database
-- @case
select ' a' = ' ', ' a' < ' ', ' a' like ' ', ' a' starting with ' ', ' a' containing ' ', ' a' || ' ' from rdb$database
-- @case
select ' a' = 'a', ' a' < 'a', ' a' like 'a', ' a' starting with 'a', ' a' containing 'a', ' a' || 'a' from rdb$database
-- @case
select ' a' = 'a ', ' a' < 'a ', ' a' like 'a ', ' a' starting with 'a ', ' a' containing 'a ', ' a' || 'a ' from rdb$database
-- @case
select ' a' = ' a', ' a' < ' a', ' a' like ' a', ' a' starting with ' a', ' a' containing ' a', ' a' || ' a' from rdb$database
-- @case
select ' a' = '%', ' a' < '%', ' a' like '%', ' a' starting with '%', ' a' containing '%', ' a' || '%' from rdb$database
-- @case
select ' a' = '_', ' a' < '_', ' a' like '_', ' a' starting with '_', ' a' containing '_', ' a' || '_' from rdb$database
-- @case
select ' a' = '\', ' a' < '\', ' a' like '\', ' a' starting with '\', ' a' containing '\', ' a' || '\' from rdb$database
-- @case
select ' a' = 'é', ' a' < 'é', ' a' like 'é', ' a' starting with 'é', ' a' containing 'é', ' a' || 'é' from rdb$database
-- @case
select ' a' = 'É', ' a' < 'É', ' a' like 'É', ' a' starting with 'É', ' a' containing 'É', ' a' || 'É' from rdb$database
-- @case
select ' a' = '😀', ' a' < '😀', ' a' like '😀', ' a' starting with '😀', ' a' containing '😀', ' a' || '😀' from rdb$database
-- @case
select '%' = '', '%' < '', '%' like '', '%' starting with '', '%' containing '', '%' || '' from rdb$database
-- @case
select '%' = ' ', '%' < ' ', '%' like ' ', '%' starting with ' ', '%' containing ' ', '%' || ' ' from rdb$database
-- @case
select '%' = 'a', '%' < 'a', '%' like 'a', '%' starting with 'a', '%' containing 'a', '%' || 'a' from rdb$database
-- @case
select '%' = 'a ', '%' < 'a ', '%' like 'a ', '%' starting with 'a ', '%' containing 'a ', '%' || 'a ' from rdb$database
-- @case
select '%' = ' a', '%' < ' a', '%' like ' a', '%' starting with ' a', '%' containing ' a', '%' || ' a' from rdb$database
-- @case
select '%' = '%', '%' < '%', '%' like '%', '%' starting with '%', '%' containing '%', '%' || '%' from rdb$database
-- @case
select '%' = '_', '%' < '_', '%' like '_', '%' starting with '_', '%' containing '_', '%' || '_' from rdb$database
-- @case
select '%' = '\', '%' < '\', '%' like '\', '%' starting with '\', '%' containing '\', '%' || '\' from rdb$database
-- @case
select '%' = 'é', '%' < 'é', '%' like 'é', '%' starting with 'é', '%' containing 'é', '%' || 'é' from rdb$database
-- @case
select '%' = 'É', '%' < 'É', '%' like 'É', '%' starting with 'É', '%' containing 'É', '%' || 'É' from rdb$database
-- @case
select '%' = '😀', '%' < '😀', '%' like '😀', '%' starting with '😀', '%' containing '😀', '%' || '😀' from rdb$database
-- @case
select '_' = '', '_' < '', '_' like '', '_' starting with '', '_' containing '', '_' || '' from rdb$database
-- @case
select '_' = ' ', '_' < ' ', '_' like ' ', '_' starting with ' ', '_' containing ' ', '_' || ' ' from rdb$database
-- @case
select '_' = 'a', '_' < 'a', '_' like 'a', '_' starting with 'a', '_' containing 'a', '_' || 'a' from rdb$database
-- @case
select '_' = 'a ', '_' < 'a ', '_' like 'a ', '_' starting with 'a ', '_' containing 'a ', '_' || 'a ' from rdb$database
-- @case
select '_' = ' a', '_' < ' a', '_' like ' a', '_' starting with ' a', '_' containing ' a', '_' || ' a' from rdb$database
-- @case
select '_' = '%', '_' < '%', '_' like '%', '_' starting with '%', '_' containing '%', '_' || '%' from rdb$database
-- @case
select '_' = '_', '_' < '_', '_' like '_', '_' starting with '_', '_' containing '_', '_' || '_' from rdb$database
-- @case
select '_' = '\', '_' < '\', '_' like '\', '_' starting with '\', '_' containing '\', '_' || '\' from rdb$database
-- @case
select '_' = 'é', '_' < 'é', '_' like 'é', '_' starting with 'é', '_' containing 'é', '_' || 'é' from rdb$database
-- @case
select '_' = 'É', '_' < 'É', '_' like 'É', '_' starting with 'É', '_' containing 'É', '_' || 'É' from rdb$database
-- @case
select '_' = '😀', '_' < '😀', '_' like '😀', '_' starting with '😀', '_' containing '😀', '_' || '😀' from rdb$database
-- @case
select '\' = '', '\' < '', '\' like '', '\' starting with '', '\' containing '', '\' || '' from rdb$database
-- @case
select '\' = ' ', '\' < ' ', '\' like ' ', '\' starting with ' ', '\' containing ' ', '\' || ' ' from rdb$database
-- @case
select '\' = 'a', '\' < 'a', '\' like 'a', '\' starting with 'a', '\' containing 'a', '\' || 'a' from rdb$database
-- @case
select '\' = 'a ', '\' < 'a ', '\' like 'a ', '\' starting with 'a ', '\' containing 'a ', '\' || 'a ' from rdb$database
-- @case
select '\' = ' a', '\' < ' a', '\' like ' a', '\' starting with ' a', '\' containing ' a', '\' || ' a' from rdb$database
-- @case
select '\' = '%', '\' < '%', '\' like '%', '\' starting with '%', '\' containing '%', '\' || '%' from rdb$database
-- @case
select '\' = '_', '\' < '_', '\' like '_', '\' starting with '_', '\' containing '_', '\' || '_' from rdb$database
-- @case
select '\' = '\', '\' < '\', '\' like '\', '\' starting with '\', '\' containing '\', '\' || '\' from rdb$database
-- @case
select '\' = 'é', '\' < 'é', '\' like 'é', '\' starting with 'é', '\' containing 'é', '\' || 'é' from rdb$database
-- @case
select '\' = 'É', '\' < 'É', '\' like 'É', '\' starting with 'É', '\' containing 'É', '\' || 'É' from rdb$database
-- @case
select '\' = '😀', '\' < '😀', '\' like '😀', '\' starting with '😀', '\' containing '😀', '\' || '😀' from rdb$database
-- @case
select 'é' = '', 'é' < '', 'é' like '', 'é' starting with '', 'é' containing '', 'é' || '' from rdb$database
-- @case
select 'é' = ' ', 'é' < ' ', 'é' like ' ', 'é' starting with ' ', 'é' containing ' ', 'é' || ' ' from rdb$database
-- @case
select 'é' = 'a', 'é' < 'a', 'é' like 'a', 'é' starting with 'a', 'é' containing 'a', 'é' || 'a' from rdb$database
-- @case
select 'é' = 'a ', 'é' < 'a ', 'é' like 'a ', 'é' starting with 'a ', 'é' containing 'a ', 'é' || 'a ' from rdb$database
-- @case
select 'é' = ' a', 'é' < ' a', 'é' like ' a', 'é' starting with ' a', 'é' containing ' a', 'é' || ' a' from rdb$database
-- @case
select 'é' = '%', 'é' < '%', 'é' like '%', 'é' starting with '%', 'é' containing '%', 'é' || '%' from rdb$database
-- @case
select 'é' = '_', 'é' < '_', 'é' like '_', 'é' starting with '_', 'é' containing '_', 'é' || '_' from rdb$database
-- @case
select 'é' = '\', 'é' < '\', 'é' like '\', 'é' starting with '\', 'é' containing '\', 'é' || '\' from rdb$database
-- @case
select 'é' = 'é', 'é' < 'é', 'é' like 'é', 'é' starting with 'é', 'é' containing 'é', 'é' || 'é' from rdb$database
-- @case
select 'é' = 'É', 'é' < 'É', 'é' like 'É', 'é' starting with 'É', 'é' containing 'É', 'é' || 'É' from rdb$database
-- @case
select 'é' = '😀', 'é' < '😀', 'é' like '😀', 'é' starting with '😀', 'é' containing '😀', 'é' || '😀' from rdb$database
-- @case
select 'É' = '', 'É' < '', 'É' like '', 'É' starting with '', 'É' containing '', 'É' || '' from rdb$database
-- @case
select 'É' = ' ', 'É' < ' ', 'É' like ' ', 'É' starting with ' ', 'É' containing ' ', 'É' || ' ' from rdb$database
-- @case
select 'É' = 'a', 'É' < 'a', 'É' like 'a', 'É' starting with 'a', 'É' containing 'a', 'É' || 'a' from rdb$database
-- @case
select 'É' = 'a ', 'É' < 'a ', 'É' like 'a ', 'É' starting with 'a ', 'É' containing 'a ', 'É' || 'a ' from rdb$database
-- @case
select 'É' = ' a', 'É' < ' a', 'É' like ' a', 'É' starting with ' a', 'É' containing ' a', 'É' || ' a' from rdb$database
-- @case
select 'É' = '%', 'É' < '%', 'É' like '%', 'É' starting with '%', 'É' containing '%', 'É' || '%' from rdb$database
-- @case
select 'É' = '_', 'É' < '_', 'É' like '_', 'É' starting with '_', 'É' containing '_', 'É' || '_' from rdb$database
-- @case
select 'É' = '\', 'É' < '\', 'É' like '\', 'É' starting with '\', 'É' containing '\', 'É' || '\' from rdb$database
-- @case
select 'É' = 'é', 'É' < 'é', 'É' like 'é', 'É' starting with 'é', 'É' containing 'é', 'É' || 'é' from rdb$database
-- @case
select 'É' = 'É', 'É' < 'É', 'É' like 'É', 'É' starting with 'É', 'É' containing 'É', 'É' || 'É' from rdb$database
-- @case
select 'É' = '😀', 'É' < '😀', 'É' like '😀', 'É' starting with '😀', 'É' containing '😀', 'É' || '😀' from rdb$database
-- @case
select '😀' = '', '😀' < '', '😀' like '', '😀' starting with '', '😀' containing '', '😀' || '' from rdb$database
-- @case
select '😀' = ' ', '😀' < ' ', '😀' like ' ', '😀' starting with ' ', '😀' containing ' ', '😀' || ' ' from rdb$database
-- @case
select '😀' = 'a', '😀' < 'a', '😀' like 'a', '😀' starting with 'a', '😀' containing 'a', '😀' || 'a' from rdb$database
-- @case
select '😀' = 'a ', '😀' < 'a ', '😀' like 'a ', '😀' starting with 'a ', '😀' containing 'a ', '😀' || 'a ' from rdb$database
-- @case
select '😀' = ' a', '😀' < ' a', '😀' like ' a', '😀' starting with ' a', '😀' containing ' a', '😀' || ' a' from rdb$database
-- @case
select '😀' = '%', '😀' < '%', '😀' like '%', '😀' starting with '%', '😀' containing '%', '😀' || '%' from rdb$database
-- @case
select '😀' = '_', '😀' < '_', '😀' like '_', '😀' starting with '_', '😀' containing '_', '😀' || '_' from rdb$database
-- @case
select '😀' = '\', '😀' < '\', '😀' like '\', '😀' starting with '\', '😀' containing '\', '😀' || '\' from rdb$database
-- @case
select '😀' = 'é', '😀' < 'é', '😀' like 'é', '😀' starting with 'é', '😀' containing 'é', '😀' || 'é' from rdb$database
-- @case
select '😀' = 'É', '😀' < 'É', '😀' like 'É', '😀' starting with 'É', '😀' containing 'É', '😀' || 'É' from rdb$database
-- @case
select '😀' = '😀', '😀' < '😀', '😀' like '😀', '😀' starting with '😀', '😀' containing '😀', '😀' || '😀' from rdb$database
-- @case
select 'abc' like 'a%' from rdb$database
-- @case
select 'abc' not like 'a%' from rdb$database
-- @case
select 'abc' like '%c' from rdb$database
-- @case
select 'abc' not like '%c' from rdb$database
-- @case
select 'abc' like '_b_' from rdb$database
-- @case
select 'abc' not like '_b_' from rdb$database
-- @case
select 'abc' like 'a__c' from rdb$database
-- @case
select 'abc' not like 'a__c' from rdb$database
-- @case
select 'a%c' like 'a\%c' escape '\' from rdb$database
-- @case
select 'a%c' not like 'a\%c' escape '\' from rdb$database
-- @case
select 'a_c' like 'a#_c' escape '#' from rdb$database
-- @case
select 'a_c' not like 'a#_c' escape '#' from rdb$database
-- @case
select 'abc' like 'a#bc' escape '#' from rdb$database
-- @case
select 'abc' not like 'a#bc' escape '#' from rdb$database
-- @case
select 'abc' like 'abc' escape '' from rdb$database
-- @case
select 'abc' not like 'abc' escape '' from rdb$database
-- @case
select 'abc' like 'ab' escape 'xy' from rdb$database
-- @case
select 'abc' not like 'ab' escape 'xy' from rdb$database
-- @case
select 'ABC' like 'abc' from rdb$database
-- @case
select 'ABC' not like 'abc' from rdb$database
-- @case
select 'abc ' like 'abc' from rdb$database
-- @case
select 'abc ' not like 'abc' from rdb$database
-- @case
select 'abc' like 'abc ' from rdb$database
-- @case
select 'abc' not like 'abc ' from rdb$database
-- @case
select 'a
b' like 'a_b' from rdb$database
-- @case
select 'a
b' not like 'a_b' from rdb$database
-- @case
select 'aé' like 'a_' from rdb$database
-- @case
select 'aé' not like 'a_' from rdb$database
-- @case
select 'a😀' like 'a_' from rdb$database
-- @case
select 'a😀' not like 'a_' from rdb$database
-- @case
select '' like '' from rdb$database
-- @case
select '' not like '' from rdb$database
-- @case
select '' like '%' from rdb$database
-- @case
select '' not like '%' from rdb$database
-- @case
select null like '%' from rdb$database
-- @case
select null not like '%' from rdb$database
-- @case
select 'x' like null from rdb$database
-- @case
select 'x' not like null from rdb$database
-- @case
select 'abc' like '%%%' from rdb$database
-- @case
select 'abc' not like '%%%' from rdb$database
-- @case
select 'abc' like 'a%%c' from rdb$database
-- @case
select 'abc' not like 'a%%c' from rdb$database
-- @case
select '100%' like '100!%' escape '!' from rdb$database
-- @case
select '100%' not like '100!%' escape '!' from rdb$database
-- @case
select 'abc' similar to 'abc' from rdb$database
-- @case
select 'abc' similar to 'a.c' from rdb$database
-- @case
select 'abc' similar to 'a_c' from rdb$database
-- @case
select 'abc' similar to 'a%' from rdb$database
-- @case
select 'abc' similar to '[a-c]+' from rdb$database
-- @case
select 'abc' similar to '[^x]*' from rdb$database
-- @case
select 'aaa' similar to 'a{3}' from rdb$database
-- @case
select 'aa' similar to 'a{3}' from rdb$database
-- @case
select 'ab' similar to 'a|b' from rdb$database
-- @case
select 'a' similar to 'a|b' from rdb$database
-- @case
select 'abc' similar to '(ab|cd)c' from rdb$database
-- @case
select 'abc' similar to 'ABC' from rdb$database
-- @case
select 'a+b' similar to 'a\+b' escape '\' from rdb$database
-- @case
select 'abc' similar to 'a[[:alpha:]]c' from rdb$database
-- @case
select 'a1c' similar to 'a[[:digit:]]c' from rdb$database
-- @case
select 'abc' similar to '%(b)%' from rdb$database
-- @case
select '' similar to '' from rdb$database
-- @case
select 'abc' similar to 'ab?c' from rdb$database
-- @case
select 'abbbc' similar to 'ab*c' from rdb$database
-- @case
select 'abc' similar to '(a' from rdb$database
-- @case
select 'abc' similar to 'a[c' from rdb$database
-- @case
select 'é' similar to '_' from rdb$database
-- @case
select 'abc' similar to 'a[b-]c' from rdb$database
-- @case
select cast('abc' as char(5)) || '|', cast('abc' as varchar(5)) || '|', cast('abc  ' as varchar(3)), cast('abcd ' as char(4)) from rdb$database
-- @case
select cast('abcd' as char(3)) from rdb$database
-- @case
select cast('ab  ' as char(2)), cast('ab  ' as varchar(2)) from rdb$database
-- @case
select 'a' || null, null || null, 'a' || 1, 1 || 'a', 1.5 || 'x', 1e0 || 'x', date '2024-01-01' || '', true || '' from rdb$database
-- @case
select char_length('😀'), octet_length('😀'), octet_length(_octets 'abc'), char_length(_win1252 'àb'), octet_length(_win1252 'àb') from rdb$database
-- @case
select _utf8 'a' collate unicode_ci = 'A', 'a' = 'A' collate unicode_ci, 'á' collate unicode_ci_ai = 'A' from rdb$database
-- @case
select 'b' collate unicode_ci < 'A', 'é' collate unicode < 'f', 'Z' < 'a' from rdb$database
-- @case
select trim(both 'x' from 'xxaxx'), trim(leading 'xy' from 'xyxyaxy'), trim('' from ' a '), trim(trailing ' ' from ' a ') || '|' from rdb$database
-- @case
select trim(null from 'a'), trim('a' from null) from rdb$database
-- @case
select substring('abcdef' from 3 for -1) from rdb$database
-- @case
select substring('abcdef' from null), substring('abcdef' from 2 for null) from rdb$database
-- @case
select substring('abc' similar 'a"b"c' escape '"') from rdb$database
-- @case
select left('abc', -1) from rdb$database
-- @case
select right('abc', -1) from rdb$database
-- @case
select lpad('abc', -1) from rdb$database
-- @case
select lpad('abc', 10, ''), rpad('abc', 10, '') , lpad('', 3, 'ab'), rpad('abc', 2) from rdb$database
-- @case
select replace('aaa', 'a', 'aa'), replace('abc', 'b', ''), replace('abc', null, 'x'), replace(null, 'a', 'b') from rdb$database
-- @case
select position('' in 'abc'), position('c' in 'abc', 4), position('a', 'aaa', 2), position('x' in ''), position('a' in null) from rdb$database
-- @case
select position('a', 'abc', 0) from rdb$database
-- @case
select overlay('abcdef' placing '' from 3 for 2), overlay('abc' placing 'xyz' from 10) from rdb$database
-- @case
select reverse(''), reverse('😀a'), reverse(null) from rdb$database
-- @case
select upper(null), lower(null), char_length(null), octet_length(null) from rdb$database
-- @case
select char_length(cast('ab' as char(10))), octet_length(cast('ab' as char(10))), char_length(cast('ab' as varchar(10))) from rdb$database
-- @case
select 'abc' > 'abd', 'abc' > 'ab', 'ab' = 'ab   ', 'ab' < 'ab ' || 'a' from rdb$database
-- @case
select max('a'), min(''), list(distinct 'x' || '') from rdb$database
-- @case
select list(v10) from (select v10 from st order by id)
-- @case
select list(v10, '') from st
-- @case
select list(v10, null) from st
-- @case
select cast('x' as varchar(32765)) from rdb$database
-- @case
select cast('x' as varchar(32766)) from rdb$database
-- @case
select cast('x' as char(32767)) from rdb$database
-- @case
select cast('x' as varchar(8191) character set utf8), cast('x' as varchar(8192) character set utf8) from rdb$database
-- @case
select _octets 'ab' = x'6162', x'6162' || x'63', cast(x'00ff' as varchar(4)), char_length(x'00ff') from rdb$database
-- @case
select x'4' from rdb$database
-- @case
select x'g0' from rdb$database
-- @case
select 'it''s', '''', '' from rdb$database
-- @case
select q'{it's}', q'[a]]', q'<x>' from rdb$database
-- @case
select n'abc', _none 'abc', _ascii 'abc' from rdb$database
-- @case
select 'a' 'b' from rdb$database
