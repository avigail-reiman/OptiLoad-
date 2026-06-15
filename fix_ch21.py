# -*- coding: utf-8 -*-
import sys

FILE = r'c:\Users\1\Desktop\פרויקט שיבוץ\OptiLoad\ספר_סקירה_ופרק10.txt'

with open(FILE, encoding='utf-8') as f:
    content = f.read()

# Find the separator line just before chapter 21
SEP = '=' * 56
lines = content.split('\n')

start_idx = -1
for i, line in enumerate(lines):
    if line.strip() == SEP and i + 1 < len(lines) and 'פרק 21' in lines[i + 1]:
        start_idx = i
        break

if start_idx == -1:
    print('ERROR: chapter 21 not found')
    sys.exit(1)

print(f'Found chapter 21 at line {start_idx}')

# Keep everything up to (not including) the chapter 21 separator
before = '\n'.join(lines[:start_idx]) + '\n'

NEW_CHAPTER = """\
========================================================
  פרק 21: תיאור תהליכי אבטחת המידע_REPLACE_MARKER
========================================================


21. תיאור תהליכי אבטחת המידע

פרק זה מתאר את מנגנוני אבטחת המידע המיושמים במערכת OptiLoad \u2014 החל
מאימות הזהות ועד לשמירת שלמות הנתונים במסד. האבטחה תוכננה בהתאם
לעקרון Defense in Depth: כל שכבה מגינה על השכבה שמתחתיה, כך
שפרצה באחת מהן אינה חושפת את כלל המערכת.


21.1 תיאור ההתקפות וההגנות

מערכת OptiLoad חשופה לכמה וקטורי תקיפה אפשריים; לכל אחד מהם פותח
מנגנון הגנה ייעודי המיושם ישירות בשכבת ה-API.

א. Brute Force \u2014 ניסיונות כניסה חוזרים

תוקף שאינו יודע את הסיסמה עשוי לנסות שילובים רבים בקצב מהיר. ההגנה
מיושמת על ידי LoginRateLimiter \u2014 מחלקה Singleton הרשומה ב-Program.cs
המנהלת ConcurrentDictionary שמפתחו הוא כתובת ה-IP המנורמלת. לאחר
3 ניסיונות כושלים רצופים מאותה IP, החשבון ננעל ל-5 דקות. ה-API
מחזיר 429 Too Many Requests עם כותרת Retry-After המציינת שניות עד
לשחרור, וממשק הכניסה מציג ספירה לאחור ומנעל את כפתור הכניסה.

כדי למנוע עקיפה פשוטה של הנעילה, מתבצעת נורמליזציה של ה-IP ב-
AuthController: כתובות IPv6 ממופות ל-IPv4 באמצעות MapToIPv4() \u2014 כך
ש-::ffff:127.0.0.1 ו-127.0.0.1 ייחשבו לאותה כתובת ולא לשתי כתובות
נפרדות ב-Dictionary.

ב. SQL Injection

הזרקת SQL מנצלת קלט משתמש שמשורשר ישירות לפקודת SQL. ב-OptiLoad,
כל גישה למסד הנתונים מתבצעת באמצעות ADO.NET SqlCommand עם SqlParameter
בלבד \u2014 ללא שרשור מחרוזות בשום שאילתה. לדוגמה:

  using var cmd = new SqlCommand(
      "SELECT * FROM Sessions WHERE LinkToken = @Token", conn);
  cmd.Parameters.AddWithValue("@Token", token);

הפרמטרים מועברים לשרת ה-DB בנפרד מטקסט השאילתה ולעולם אינם מפורשים
כקוד SQL \u2014 מה שמבטל את וקטור ה-Injection לחלוטין.

ג. IDOR \u2014 גישה למשאבים של אחרים

IDOR (Insecure Direct Object Reference) מאפשר לתוקף לשנות מזהה ב-URL
ולקרוא נתוני מנהל אחר. ב-OptiLoad כל בקשה לסשן, ל-Snapshot או ל-JobId
עוברת דרך SessionController.AuthorizeAccess() הבודקת:

  if (session.AdminId != GetAdminId()) return (false, Forbid());

AdminId נשלף מה-Claim בתוך ה-JWT Token \u2014 ערך שנחתם על ידי השרת ואינו
ניתן לשינוי על ידי הלקוח ללא ידיעת המפתח הסימטרי. בקרי הייצוא
(ExportController) מבצעים בדיקה זהה לפני כל הורדת קובץ.

ד. גישה ללא אימות \u2014 Unauthorized Access

כל ה-Endpoints המוגנים מקושטים ב-[Authorize]. גישה ישירה ל-URL של
ממשק המנהל ללא טוקן תקין מסתיימת ב-401; ממשק הלקוח מזהה קוד זה
ומנתב אוטומטית לדף הכניסה. הטוקן נשמר ב-sessionStorage (ולא ב-
localStorage) ומוחק אוטומטית עם סגירת הלשונית.

ה. Information Leakage \u2014 דליפת מידע על הארכיטקטורה

UseExceptionHandler ב-Program.cs מטפל בכל חריגה בלתי-צפויה ומחזיר
תגובה גנרית בלבד:
  { "error": "Internal server error." }
שורות Stack Trace, שמות מחלקות ופרטי חיבור למסד לעולם אינם מוחזרים
ללקוח \u2014 ומונעים מתוקף ללמוד על המבנה הפנימי של המערכת.

ו. Cross-Origin Attacks \u2014 בקשות ממקורות זרים

מדיניות CORS ב-Program.cs מגבילה קבלת בקשות ל-4 מקורות בלבד:
localhost:5098, 127.0.0.1:5098, localhost:5500, ו-127.0.0.1:5500. בקשות
מכל דומיין אחר נדחות ברמת ה-Preflight. בנוסף, appsettings.json מגדיר
AllowedHosts: "localhost;127.0.0.1" \u2014 שכבת הגנה נוספת ברמת ה-Host Header.

ז. ניחוש טוקן גישה לשטח

ה-LinkToken שמשמש משתמשי שטח נוצר על ידי RandomNumberGenerator.Fill()
\u2014 גנרטור אקראי קריפטוגרפי (CSPRNG) של ה-CLR. הטוקן הוא 16 בתים אקראיים
המוצגים כ-GUID (36 תווים). הסתברות ניחוש: 1 / 2\u00b9\u00b2\u2078 \u2248 3.4 \u00d7 10\u207b\u00b3\u2079 \u2014
בלתי-ישים מבחינה חישובית. הטוקן אטום לחלוטין ואינו מכיל מידע על
המנהל, שם הסשן או מזהה ה-DB.


21.2 תיאור ההצפנות

א. גיבוב סיסמאות \u2014 HMAC-SHA256 עם Salt

סיסמאות מנהלים אינן נשמרות בטקסט ברור. מחלקת PasswordHasher ב-Core
מבצעת גיבוב (Hashing) באמצעות HMAC-SHA256 עם Salt אקראי ייחודי לכל מנהל:

  יצירת Hash:
    using var hmac = new HMACSHA256();       // מפתח אקראי = ה-Salt
    salt = Convert.ToBase64String(hmac.Key);
    hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));

  אימות:
    using var hmac = new HMACSHA256(Convert.FromBase64String(salt));
    return Convert.ToBase64String(hmac.ComputeHash(...)) == storedHash;

ה-Salt הייחודי לכל מנהל מבטיח שאותה סיסמה תניב Hash שונה לכל חשבון \u2014
מה שמגן מפני התקפות Rainbow Table. PasswordHash ו-PasswordSalt נשמרים
כעמודות נפרדות בטבלת Admins במסד הנתונים.

ב. JWT \u2014 חתימה דיגיטלית HMAC-SHA256

לאחר כניסה מוצלחת, AuthController מנפיק JWT Token חתום ב-HMAC-SHA256.
המפתח הסימטרי מוגדר ב-appsettings.json תחת Jwt:Key. בזמן הפעלת השרת,
Program.cs מוודא שאורך המפתח לפחות 32 תווים:

  if (jwtKey.Length < 32)
      throw new InvalidOperationException("JWT key must be at least 32 characters.");

הטוקן מכיל את Claims: שם המנהל (ClaimTypes.Name) ומזהה המנהל
(ClaimTypes.NameIdentifier). החתימה מבטיחה שהלקוח אינו יכול לזייף
Claims ולהגדיל את הרשאותיו. תוקף הטוקן מוגבל לשעתיים
(Expires = DateTime.UtcNow.AddHours(2)) ולאחר פקיעתו כל בקשה מוגנת
מחזירה 401.

כל בקשה נכנסת מאומתת על ידי JwtBearerMiddleware הרשום ב-Program.cs:
מאומתת חתימת ה-HMAC-SHA256 (IssuerSigningKey), ומאומתת תפוגת הטוקן
(ValidateLifetime = true כברירת מחדל).


========================================================
  סוף הקובץ
========================================================
"""

result = before + NEW_CHAPTER
with open(FILE, 'w', encoding='utf-8') as f:
    f.write(result)

print(f'SUCCESS: wrote {len(result)} chars, {result.count(chr(10))} lines')
