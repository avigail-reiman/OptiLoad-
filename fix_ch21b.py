# -*- coding: utf-8 -*-
import sys

FILE = r'c:\Users\1\Desktop\פרויקט שיבוץ\OptiLoad\ספר_סקירה_ופרק10.txt'

with open(FILE, encoding='utf-8') as f:
    content = f.read()

lines = content.split('\n')
SEP = '=' * 56

start_idx = -1
for i, line in enumerate(lines):
    if line.strip() == SEP and i + 1 < len(lines) and '\u05e4\u05e8\u05e7 21' in lines[i + 1]:
        start_idx = i
        break

if start_idx == -1:
    print('ERROR: chapter 21 not found'); sys.exit(1)

before = '\n'.join(lines[:start_idx]) + '\n'

NEW = (
    '=' * 56 + '\n'
    '  \u05e4\u05e8\u05e7 21: \u05ea\u05d9\u05d0\u05d5\u05e8 \u05ea\u05d4\u05dc\u05d9\u05db\u05d9 \u05d0\u05d1\u05d8\u05d7\u05ea \u05d4\u05de\u05d9\u05d3\u05e2\n'
    + '=' * 56 + '\n'
    '\n\n'
    '21. \u05ea\u05d9\u05d0\u05d5\u05e8 \u05ea\u05d4\u05dc\u05d9\u05db\u05d9 \u05d0\u05d1\u05d8\u05d7\u05ea \u05d4\u05de\u05d9\u05d3\u05e2\n'
    '\n'
    '\u05e4\u05e8\u05e7 \u05d6\u05d4 \u05de\u05ea\u05d0\u05e8 \u05d0\u05ea \u05de\u05e0\u05d2\u05e0\u05d5\u05e0\u05d9 \u05d0\u05d1\u05d8\u05d7\u05ea \u05d4\u05de\u05d9\u05d3\u05e2 \u05d4\u05de\u05d9\u05d5\u05e9\u05de\u05d9\u05dd \u05d1\u05de\u05e2\u05e8\u05db\u05ea OptiLoad,\n'
    '\u05d1\u05d4\u05ea\u05d0\u05dd \u05dc\u05e2\u05e7\u05e8\u05d5\u05df Defense in Depth.\n'
    '\n\n'
    '21.1 \u05ea\u05d9\u05d0\u05d5\u05e8 \u05d4\u05d4\u05ea\u05e7\u05e4\u05d5\u05ea \u05d5\u05d4\u05d4\u05d2\u05e0\u05d5\u05ea\n'
    '\n'
    '  \u05d5\u05e7\u05d8\u05d5\u05e8 \u05ea\u05e7\u05d9\u05e4\u05d4           | \u05d4\u05d2\u05e0\u05d4\n'
    '  ----------------------|----------------------------------------------------------\n'
    '  Brute Force           | LoginRateLimiter: \u05e0\u05e2\u05d9\u05dc\u05d4 \u05dc-5 \u05d3\u05e7\u05d5\u05ea \u05dc\u05d0\u05d7\u05e8 3 \u05db\u05e9\u05dc\u05d5\u05e0\u05d5\u05ea;\n'
    '                        | 429 + Retry-After; \u05e1\u05e4\u05d9\u05e8\u05d4 \u05dc\u05d0\u05d7\u05d5\u05e8 \u05d1\u05de\u05de\u05e9\u05e7\n'
    '  SQL Injection         | SqlCommand \u05e2\u05dd SqlParameter \u05d1\u05dc\u05d1\u05d3 \u2014 \u05d0\u05d9\u05df \u05e9\u05e8\u05e9\u05d5\u05e8 \u05de\u05d7\u05e8\u05d5\u05d6\u05d5\u05ea\n'
    '  IDOR                  | \u05d1\u05d3\u05d9\u05e7\u05ea AdminId \u05d1\u05db\u05dc \u05d2\u05d9\u05e9\u05d4 \u05dc\u05e1\u05e9\u05df/Snapshot/Job \u2014 Forbid \u05d0\u05dd \u05d0\u05d9\u05e0\u05d5 \u05d4\u05d1\u05e2\u05dc\u05d9\u05dd\n'
    '  \u05d2\u05d9\u05e9\u05d4 \u05dc\u05dc\u05d0 \u05d0\u05d9\u05de\u05d5\u05ea       | [Authorize] \u05e2\u05dc \u05db\u05dc \u05d4-Controllers; \u05e0\u05d9\u05ea\u05d5\u05d1 \u05d0\u05d5\u05d8\u05d5\u05de\u05d8\u05d9 \u05dc-login \u05d1-401\n'
    '  \u05e2\u05e7\u05d9\u05e4\u05ea \u05e0\u05e2\u05d9\u05dc\u05ea IP      | \u05e0\u05d5\u05e8\u05de\u05dc\u05d9\u05d6\u05e6\u05d9\u05d9\u05ea IPv6\u2192IPv4 (MapToIPv4) \u2014 \u05e2\u05d5\u05d3\u05e4\u05ea \u05db\u05ea\u05d5\u05d1\u05ea \u05d0\u05d7\u05ea \u05d1-Dictionary\n'
    '  Information Leakage   | UseExceptionHandler: \u05de\u05d7\u05d6\u05d9\u05e8 "Internal server error." \u05d1\u05dc\u05d1\u05d3\n'
    '  Cross-Origin Attacks  | CORS Whitelist: 4 \u05de\u05e7\u05d5\u05e8\u05d5\u05ea; AllowedHosts: localhost;127.0.0.1\n'
    '  \u05e0\u05d9\u05d7\u05d5\u05e9 LinkToken      | CSPRNG \u2014 RandomNumberGenerator.Fill() \u2014 2\u00b9\u00b2\u2078 \u05d0\u05e4\u05e9\u05e8\u05d5\u05d9\u05d5\u05ea\n'
    '\n\n'
    '21.2 \u05ea\u05d9\u05d0\u05d5\u05e8 \u05d4\u05d4\u05e6\u05e4\u05e0\u05d5\u05ea\n'
    '\n'
    '  \u05de\u05e0\u05d2\u05e0\u05d5\u05df                   | \u05ea\u05d9\u05d0\u05d5\u05e8\n'
    '  ----------------------|----------------------------------------------------------\n'
    '  \u05d2\u05d9\u05d1\u05d5\u05d1 \u05e1\u05d9\u05e1\u05de\u05d0\u05d5\u05ea         | HMAC-SHA256 + Salt \u05d0\u05e7\u05e8\u05d0\u05d9 \u05d9\u05d9\u05d7\u05d5\u05d3\u05d9 \u05dc\u05db\u05dc \u05de\u05e0\u05d4\u05dc (PasswordHasher.cs)\n'
    '  JWT                   | \u05d7\u05ea\u05d5\u05dd HMAC-SHA256; \u05de\u05e4\u05ea\u05d7 \u05de\u05d9\u05e0. 32 \u05ea\u05d5\u05d5\u05d9\u05dd; \u05ea\u05d5\u05e7\u05e3 \u05e9\u05e2\u05ea\u05d9\u05d9\u05dd\n'
    '  \u05d0\u05d7\u05e1\u05d5\u05df \u05d8\u05d5\u05e7\u05df \u05d1\u05dc\u05e7\u05d5\u05d7    | sessionStorage \u05d1\u05dc\u05d1\u05d3 (\u05dc\u05d0 localStorage) \u2014 \u05e0\u05de\u05d7\u05e7 \u05e2\u05dd \u05e1\u05d2\u05d9\u05e8\u05ea \u05d4\u05dc\u05e9\u05d5\u05e0\u05d9\u05ea\n'
    '\n\n'
    + '=' * 56 + '\n'
    '  \u05e1\u05d5\u05e3 \u05d4\u05e7\u05d5\u05d1\u05e5\n'
    + '=' * 56 + '\n'
)

result = before + NEW
with open(FILE, 'w', encoding='utf-8') as f:
    f.write(result)
print(f'SUCCESS: {len(result)} chars, {result.count(chr(10))} lines')
