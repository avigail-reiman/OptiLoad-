$file = 'c:\Users\1\Desktop\פרויקט שיבוץ\OptiLoad\ספר_סקירה_ופרק10.txt'
$content = [System.IO.File]::ReadAllText($file, [System.Text.Encoding]::UTF8)

$newChapter = @"
========================================================
  פרק 21: תיאור תהליכי אבטחת המידע
========================================================


21. תיאור תהליכי אבטחת המידע

פרק זה מתאר את מנגנוני אבטחת המידע המיושמים במערכת OptiLoad --- החל
מאימות הזהות ועד לשמירת שלמות הנתונים במסד. האבטחה תוכננה בהתאם
לעקרון Defense in Depth: כל שכבה מגינה על השכבה שמתחתיה, כך
שפרצה באחת מהן אינה חושפת את כלל המערכת.


21.1 תיאור ההתק וההגנות

מערכת OptiLoad חשופה לכמה וקטורי תקיפה אפשריים; לכל אחד מהם פותח
מנגנון הגנה ייעודי המיושם ישירות בשכבת ה-API.

א. Brute Force --- ניסיונות כניסה חוזרים

תוקף שאינו יודע את הסיסמה עשוי לנסות שילובים רבים בקצב מהיר. ההגנה
מיושמת על ידי LoginRateLimiter --- מחלקה Singleton הרשומה ב-Program.cs
המנהלת ConcurrentDictionary שמפתחו הוא כתובת ה-IP המנורמלת. לאחר
3 ניסיונות כושלים רצופים מאותה IP, החשבון ננעל ל-5 דקות. ה-API
מחזיר 429 Too Many Requests עם כותרת Retry-After המציינת שניות עד
לשחרור, וממשק הכניסה מציג ספירה לאחור ומנעל את כפתור הכניסה.

כדי למנוע עקיפה פשוטה של הנעילה, מתבצעת נורמליזציה של ה-IP ב-
AuthController: כתובות IPv6 ממופות ל-IPv4 באמצעות MapToIPv4() --- כך
ש-::ffff:127.0.0.1 ו-127.0.0.1 ייחשבו לאותה כתובת ולא לשתי כתובות
נפרדות ב-Dictionary.

ב. SQL Injection

הזרקת SQL מנצלת קלט משתמש שמשורשר ישירות לפקודת SQL. ב-OptiLoad,
כל גישה למסד הנתונים מתבצעת באמצעות ADO.NET SqlCommand עם SqlParameter
בלבד --- ללא שרשור מחרוזות בשום שאילתה. לדוגמה:

  using var cmd = new SqlCommand(
      "SELECT * FROM Sessions WHERE LinkToken = @Token", conn);
  cmd.Parameters.AddWithValue("@Token", token);

הפרמטרים מועברים לשרת ה-DB בנפרד מטקסט השאילתה ולעולם אינם מפורשים
כקוד SQL --- מה שמבטל את וקטור ה-Injection לחלוטין.

ג. IDOR --- גישה למשאבים של אחרים

IDOR (Insecure Direct Object Reference) מאפשר לתוקף לשנות מזהה ב-URL
ולקרוא נתוני מנהל אחר. ב-OptiLoad כל בקשה לסשן, ל-Snapshot או ל-JobId
עוברת דרך SessionController.AuthorizeAccess() הבודקת:

  if (session.AdminId != GetAdminId()) return (false, Forbid());

AdminId נשלף מה-Claim בתוך ה-JWT Token --- ערך שנחתם על ידי השרת ואינו
ניתן לשינוי על ידי הלקוח ללא ידיעת המפתח הסימטרי. בקרי הייצוא
(ExportController) מבצעים בדיקה זהה לפני כל הורדת קובץ.

ד. גישה ללא אימות --- Unauthorized Access

כל ה-Endpoints המוגנים מקושטים ב-[Authorize]. גישה ישירה ל-URL של
ממשק המנהל ללא טוקן תקין מסתיימת ב-401; ממשק הלקוח מזהה קוד זה
ומנתב אוטומטית לדף הכניסה. הטוקן נשמר ב-sessionStorage (ולא ב-
localStorage) ומוחק אוטומטית עם סגירת הלשונית.

ה. Information Leakage --- דליפת מידע על הארכיטקטורה

UseExceptionHandler ב-Program.cs מטפל בכל חריגה בלתי-צפויה ומחזיר
תגובה גנרית בלבד:
  { "error": "Internal server error." }
שורות Stack Trace, שמות מחלקות ופרטי חיבור למסד לעולם אינם מוחזרים
ללקוח --- ומונעים מתוקף ללמוד על המבנה הפנימי של המערכת.

ו. Cross-Origin Attacks --- בקשות ממקורות זרים

מדיניות CORS ב-Program.cs מגבילה קבלת בקשות ל-4 מקורות בלבד:
localhost:5098, 127.0.0.1:5098, localhost:5500, ו-127.0.0.1:5500. בקשות
מכל דומיין אחר נדחות ברמת ה-Preflight. בנוסף, appsettings.json מגדיר
AllowedHosts: "localhost;127.0.0.1" --- שכבת הגנה נוספת ברמת ה-Host Header.

ז. ניחוש טוקן גישה לשטח

ה-LinkToken שמשמש משתמשי שטח נוצר על ידי RandomNumberGenerator.Fill()
--- גנרטור אקראי קריפטוגרפי (CSPRNG) של ה-CLR. הטוקן הוא 16 בתים אקראיים
המוצגים כ-GUID (36 תווים). הסתברות ניחוש: 1 / 2^128 = 3.4 x 10^-39 ---
בלתי-ישים מבחינה חישובית. הטוקן אטום לחלוטין ואינו מכיל מידע על
המנהל, שם הסשן או מזהה ה-DB.


21.2 תיאור ההצפנות

א. גיבוב סיסמאות --- HMAC-SHA256 עם Salt

סיסמאות מנהלים אינן נשמרות בטקסט ברור. מחלקת PasswordHasher ב-Core
מבצעת גיבוב (Hashing) באמצעות HMAC-SHA256 עם Salt אקראי ייחודי לכל מנהל:

  יצירת Hash:
    using var hmac = new HMACSHA256();       // מפתח אקראי = ה-Salt
    salt = Convert.ToBase64String(hmac.Key);
    hash = Convert.ToBase64String(hmac.ComputeHash(Encoding.UTF8.GetBytes(password)));

  אימות:
    using var hmac = new HMACSHA256(Convert.FromBase64String(salt));
    return Convert.ToBase64String(hmac.ComputeHash(...)) == storedHash;

ה-Salt הייחודי לכל מנהל מבטיח שאותה סיסמה תניב Hash שונה לכל חשבון ---
מה שמגן מפני התקפות Rainbow Table. PasswordHash ו-PasswordSalt נשמרים
כעמודות נפרדות בטבלת Admins במסד הנתונים.

ב. JWT --- חתימה דיגיטלית HMAC-SHA256

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
"@

# Find the chapter 21 separator line
$sepLine = "=" * 56
$chapterHeader = "  פרק 21:"

# Find position of the separator just before chapter 21
$lines = $content -split "`n"
$startLineIdx = -1
for ($i = 0; $i -lt $lines.Count; $i++) {
    if ($lines[$i].TrimEnd() -eq $sepLine -and $i+1 -lt $lines.Count -and $lines[$i+1].TrimStart().StartsWith("פרק 21")) {
        $startLineIdx = $i
        break
    }
}

if ($startLineIdx -lt 0) {
    # Try alternate: find "21.1" line
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -match "פרק 21:") {
            $startLineIdx = $i - 1
            break
        }
    }
}

"startLineIdx=$startLineIdx"

if ($startLineIdx -ge 0) {
    $before = ($lines[0..($startLineIdx-1)] -join "`n") + "`n"
    $result = $before + $newChapter
    [System.IO.File]::WriteAllText($file, $result, [System.Text.Encoding]::UTF8)
    "SUCCESS: wrote $($result.Length) chars"
} else {
    "ERROR: chapter 21 header not found"
}
