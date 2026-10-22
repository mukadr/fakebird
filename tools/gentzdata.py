#!/usr/bin/env python3
"""Generates src/Fakebird/Types/TimeZoneData.bin: the rules of Firebird's time zones (TimeZoneList.g.cs), so they
do not depend on the host's tz database. The input is a zoneinfo directory compiled by zic from the main IANA data,
as Firebird's ICU uses: not Debian/Ubuntu's, whose zic build includes backzone (older local mean times).

  curl -LO https://data.iana.org/time-zones/releases/tzdata2026e.tar.gz && tar xzf tzdata2026e.tar.gz
  zic -b fat -d zoneinfo africa antarctica asia australasia europe northamerica southamerica etcetera backward factory
  tools/gentzdata.py zoneinfo > src/Fakebird/Types/TimeZoneData.bin

Format (deflate-compressed, little endian): u16 name count, then per name: u8 length, UTF-8 name, u16 rule index;
u16 rule count, then per rule: i32 length and the version 2+ part of a TZif file (RFC 8536: header, 64-bit data
block and footer). The tz database is in the public domain.
"""
import os, re, struct, sys, zlib

# ICU's names that the tz database does not carry (old Java ids and SystemV zones).
ALIASES = {
    "ACT": "Australia/Darwin", "AET": "Australia/Sydney", "AGT": "America/Argentina/Buenos_Aires",
    "ART": "Africa/Cairo", "AST": "America/Anchorage", "BET": "America/Sao_Paulo", "BST": "Asia/Dhaka",
    "CAT": "Africa/Harare", "CNT": "America/St_Johns", "CST": "America/Chicago", "CTT": "Asia/Shanghai",
    "EAT": "Africa/Addis_Ababa", "ECT": "Europe/Paris", "IET": "America/Indiana/Indianapolis",
    "IST": "Asia/Kolkata", "JST": "Asia/Tokyo", "MIT": "Pacific/Apia", "NET": "Asia/Yerevan",
    "NST": "Pacific/Auckland", "PLT": "Asia/Karachi", "PNT": "America/Phoenix", "PRT": "America/Puerto_Rico",
    "PST": "America/Los_Angeles", "SST": "Pacific/Guadalcanal", "VST": "Asia/Ho_Chi_Minh",
    "Canada/East-Saskatchewan": "America/Regina", "US/Pacific-New": "America/Los_Angeles",
    "SystemV/AST4": "America/Puerto_Rico", "SystemV/AST4ADT": "America/Halifax", "SystemV/CST6": "America/Regina",
    "SystemV/CST6CDT": "America/Chicago", "SystemV/EST5": "America/Panama", "SystemV/EST5EDT": "America/New_York",
    "SystemV/HST10": "Pacific/Honolulu", "SystemV/MST7": "America/Phoenix", "SystemV/MST7MDT": "America/Denver",
    "SystemV/PST8": "Pacific/Pitcairn", "SystemV/PST8PDT": "America/Los_Angeles", "SystemV/YST9": "Pacific/Gambier",
    "SystemV/YST9YDT": "America/Anchorage",
}


def v2_part(data, name):
    """The second header, 64-bit data block and footer of a TZif file."""
    if data[:4] != b"TZif" or data[4] < 0x32:
        sys.exit(f"{name}: not a TZif file of version 2 or later")
    isut, isstd, leap, time, typ, char = struct.unpack(">6l", data[20:44])
    start = 44 + time * 5 + typ * 6 + char + leap * 8 + isstd + isut
    if data[start:start + 4] != b"TZif":
        sys.exit(f"{name}: bad TZif file")
    return data[start:]


zoneinfo = sys.argv[1]
here = os.path.dirname(os.path.abspath(__file__))
src = open(os.path.join(here, "../src/Fakebird/Types/TimeZoneList.g.cs")).read()
names = re.findall(r'"([^"]+)",', src.split("Names =", 1)[1])

rules, index, entries = [], {}, []
for name in names:
    if name == "GMT":  # built in
        continue
    path = next((p for p in (os.path.join(zoneinfo, name), os.path.join(zoneinfo, ALIASES.get(name, name)))
                 if os.path.isfile(p)), None)
    if path is None:
        print(f"warning: no rules for {name} (read as UTC)", file=sys.stderr)
        continue
    rule = v2_part(open(path, "rb").read(), name)
    if rule not in index:
        index[rule] = len(rules)
        rules.append(rule)
    entries.append((name, index[rule]))

out = bytearray(struct.pack("<H", len(entries)))
for name, i in entries:
    b = name.encode()
    out += struct.pack("<B", len(b)) + b + struct.pack("<H", i)
out += struct.pack("<H", len(rules))
for rule in rules:
    out += struct.pack("<i", len(rule)) + rule

c = zlib.compressobj(9, zlib.DEFLATED, -15)
sys.stdout.buffer.write(c.compress(bytes(out)) + c.flush())
