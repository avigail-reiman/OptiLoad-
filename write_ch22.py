import json

content = """
================================================================================
                 פרק 22 - תיאור מסד הנתונים
================================================================================

הסכמה הכללית של מסד הנתונים במערכת בנויה באופן המפריד בין ישויות הבסיס, ישויות 
משימות השיבוץ, וישויות ניהול הסשנים והמשתמשים. מסד הנתונים OptiLoadDB הוא מסד יחסי 
מבוסס SQL Server, המותאם לשמירת שלמות נתונים (Data Integrity) ושליפה מהירה.

המערכת לא מבצעת שימוש בפרוצדורות מאוחסנות (Stored Procedures) או תצוגות (Views)
מפאת השימוש ב-Entity Framework Core וב-ADO.NET מודרני המייצר שאילתות דינמיות.

הערה: תרשימי ה-ERD וה-DSD הנלווים לפרק זה סופקו כקובץ תמונה/HTML נפרד 
לתצוגה על רקע לבן להעתקה למסמך המסכם.

22.1 פירוט טבלאות בסיס הנתונים
------------------------------------------------------------------------

1. טבלת Admin (מנהלי מערכת)
טבלה המנהלת את מנהלי המערכת שיש להם הרשאה לבצע שיבוצים ולפתוח סשנים.
- Id: שלם (INT), מפתח ראשי (PK). חובה. מספור אוטומטי.
- Username: מחרוזת (NVARCHAR), חובה. שם משתמש, ייחודי (UNIQUE).
- PasswordHash: מחרוזת (NVARCHAR), חובה. גיבוב של הסיסמה.
- PasswordSalt: מחרוזת (NVARCHAR), חובה. מפתח ממליח לסיסמה.
- CreatedAt: תאריך (DATETIME2), חובה. זמן יצירת החשבון.

2. טבלת ContainerTemplate (תבניות מכולה)
מידות מכולות גנריות לשימוש חוזר.
- TemplateId: שלם (INT), מפתח ראשי (PK). חובה.
- TemplateName: מחרוזת (NVARCHAR), חובה. שם המכולה (למשל 'מכולה 20 רגל').
- Width, Height, Depth: ממשי (FLOAT), חובה. מידות פנימיות.
- MaxWeightKg: ממשי (FLOAT), חובה. משקל נשיאה מקסימלי.
- CreatedAt: תאריך (DATETIME2), חובה. זמן יצירה.

3. טבלת Container (אינסטנס של מכולה)
מכולה ספציפית לשימוש (הנגזרת מתבנית).
- ContainerId: שלם (INT), מפתח ראשי (PK). חובה.
- TemplateId: שלם (INT), מפתח זר (FK). חובה. קישור לתבנית אב.
- ContainerCode: מחרוזת (NVARCHAR), חובה. מספר או קוד זיהוי מכולה.
- Status: מחרוזת (NVARCHAR), חובה. זמינות המכולה.
- CreatedAt: תאריך, חובה.

4. טבלת Box (ארגזים/מוצרים)
מידות המוצרים הקיימים במערכת לפני העמסה.
- BoxId: שלם (INT), מפתח ראשי (PK). חובה.
- BoxName: מחרוזת (NVARCHAR), חובה. שם או קוד מוצר.
- Width, Height, Depth: ממשי (FLOAT), חובה. מידות פיזיות.
- WeightKg: ממשי (FLOAT), חובה. משקל ברוטו.
- IsFragile: בוליאני (BIT), חובה. האם שביר (מועמס רק למעלה מעל כולם).
- AllowRotation: בוליאני (BIT), חובה. האם ניתן לסובב את הארגז במרחב ה-3D.
- CreatedAt: תאריך (DATETIME2), חובה.

5. טבלת PackingJob (משימות העמסה ושיבוץ)
כל ריצה של האלגוריתם מתועדת כג'וב יחד עם התוצאות הסטטיסטיות שלו.
- JobId: שלם (INT), מפתח ראשי (PK). חובה.
- ContainerId: שלם (INT), מפתח זר (FK). חובה. מקושר למכולה עליה אנו עובדים.
- AdminId: שלם (INT), מפתח זר (FK). חובה. מזהה מנהל ההרצה.
- Status: מחרוזת (NVARCHAR), חובה. מצב הריצה (Completed/Failed).
- BinsUsed: שלם (INT), רשות (NULL). כמות המכולות/מיכלים שנצרכה.
- VolumeUtilization: ממשי (FLOAT), רשות (NULL). אחוז ניצולת נפחית.
- TotalWeightKg: ממשי (FLOAT), רשות (NULL). המשקל הכולל ששובץ.
- SolveTimeSeconds: ממשי (FLOAT), רשות (NULL). זמן ריצת האלגוריתם בשניות.
- IsOptimal: בוליאני (BIT), רשות (NULL). מנטר האם הפתרון נמצא אופטימלי (Branch & Bound).
- StatusMessage: מחרוזת, רשות (NULL). תיעוד שגיאה אם קרתה בריצה.
- CreatedAt, CompletedAt: תאריכים (DATETIME2). זמן התחלה וסיום.

6. טבלת PackingJobBox (ארגזים מבוקשים בג'וב)
רשימת הדרישה: כמות מכל סוג ארגז שהוגדרה להרצה בג'וב.
- JobBoxId: שלם (INT), מפתח ראשי (PK). חובה.
- JobId: שלם (INT), מפתח זר (FK). חובה.
- BoxId: שלם (INT), מפתח זר (FK). חובה.
- Quantity: שלם (INT), חובה. הכמות הנדרשת להעמסה מסוג זה.

7. טבלת PlacementResult (תוצאות המיקום המרחבי)
שומרת פרטנית את ציר ה-(x,y,z) לכל תיבה בודדת שמוקמה בתוך המכולה.
- PlacementId: שלם (INT), מפתח ראשי (PK). חובה.
- JobId: שלם (INT), מפתח זר (FK). חובה. להרצה שהפיקה תוצאה זו.
- BoxId: שלם (INT), מפתח זר (FK). חובה. איזה סוג ארגז שובץ כאן.
- InstanceIndex: שלם (INT), חובה. עבור מספר ארגזים זהים.
- BinIndex: שלם (INT), חובה. לאיזו מכולה שויך (אם קיימות מכולות רבות).
- PosX, PosY, PosZ: ממשי (FLOAT), חובה. נ"צ מרחבי 3D לעוגן (Corner).
- PlacedWidth, PlacedHeight, PlacedDepth: ממשי (FLOAT), חובה. המידות במרחב לאחר כל סיבוב (Rotation).
- RotationIndex: שלם (INT), חובה. זיהוי אופרציית הסיבוב מתוך 6 האופציות האפשריות.
- CreatedAt: תאריך, חובה.

8. טבלת ContainerSnapshot (תיעוד ויזואלי היסטורי במערכת)
שמירת תמונה (Base 64) מה-3D על מנת לקטלג היסטוריה השוואתית.
- Id: שלם (INT), מפתח ראשי (PK). חובה.
- JobId: שלם (INT), מפתח זר (FK). חובה. לאיזה ריצה (Job).
- LoadingStep: שלם (INT), חובה. שלב התיעוד בתהליך.
- BoxName: מחרוזת (NVARCHAR), חובה. תיוג.
- ImageData: מחרוזת טקסט ובינארי ארוכה מאד (VARCHAR MAX). חובה. התמונה עצמה.
- CreatedAt: תאריך (DATETIME2), רשות (NULL).

9. טבלת PackingSession (סשן עסקי רב-משתתפים בממשק המערכת)
ניהול מנגנון קבלת החלטות לייב, עריכות בסל המוצרים במקביל ע"י נציגים למפעלי יבוא/יצוא.
- SessionId: שלם (INT), מפתח ראשי (PK). חובה.
- AdminId: שלם (INT), מפתח זר (FK). חובה. יוזם ובעל הסשן.
- Name, Description: מחרוזת (NVARCHAR), חובה (שם) ורשות (תיאור).
- LinkToken: מחרוזת באורך קבוע (CHAR 36), חובה. טוקן זיהוי ייחודי להזמנות משתמשי קצה (GUID).
- Status: מחרוזת, חובה. הסשן הפעיל/סגור.
- CreatedAt: תאריך, חובה.

10. טבלת SessionUser (משתמשים זמניים המחוברים לסשנים)
עובדים מרחוק המוזמנים לסשן ופועלים בסביבתו ללא הרשמה מקדימה.
- SessionUserId: שלם (INT), מפתח ראשי (PK). חובה.
- SessionId: שלם (INT), מפתח זר (FK). חובה.
- DisplayName: מחרוזת (NVARCHAR), חובה. הכינוי הציבורי שדרשו בדיאלוג.
- Email: מחרוזת (NVARCHAR), רשות (NULL).
- Token: מחרוזת (CHAR 36), חובה. זיהוי אסימון הרשאות המצומד לסשן. ייחודי.
- RootToken: מחרוזת קבועה, חובה. כלי מעקב.
- CreatedAt: תאריך, חובה.

11. טבלת AccessRequest (אישורי כניסה לחדרי סשנים אונליין)
- RequestId: שלם (INT), מפתח ראשי (PK). חובה.
- SessionId, SessionUserId: מפתחות זרים של סשן ומשתמש. חובה.
- Status: מחרוזת (NVARCHAR), חובה. יכול להיות: Pending (ממתין), Approved (מאושר), Denied.
- RequestedAt: תאריך, חובה.
- RespondedAt: תאריך, רשות. זמן המענה לקבלת הבקשה.

12. טבלת SessionBox (ארגזים בסשן זמני - הסל)
הרכיב המאפשר למספר נציגים לבנות ביחד סל ארגזים עתידי להרצת אופטימיזציה בהשהייה.
- SessionBoxId: שלם (INT), מפתח ראשי (PK). חובה.
- SessionId: שלם (INT), מפתח זר (FK). חובה.
- BoxId: שלם (INT), מפתח זר (FK). חובה.
- Quantity: שלם (INT), חובה. כמה פריטים התבקשו.
- AddedBy: מחרוזת, חובה. איזה נציג הוסיף את הכמות.
- AddedAt: תאריך, חובה.

13. טבלת BoxAuditLog (יומן שינויים ושליטה בסשן)
יצירת עקבות ביקורת (Audit Trails) למניעת מחיקות ועריכות ארגזים לא שקופות בין הלקוחות במערכת.
- LogId: שלם (INT), מפתח ראשי (PK). חובה.
- SessionId: שלם (INT), מפתח זר (FK). חובה.
- Action: מחרוזת (NVARCHAR), חובה. סוג הפעולה כגון: Addition, Deletion.
- BoxId: שלם (INT), רשות. רלוונטי במקרה של מחיקות ארגז.
- BoxName: מחרוזת (NVARCHAR), חובה. נתון ארכיוני.
- Quantity: שלם, רשות.
- ChangedBy, ChangedByType: מחרוזת, חובה. שם המבצע והתפקיד שלו (Admin / User).
- ChangedAt: תאריך, חובה.

14. טבלת ErrorLog (ניטור כשלים ואלגוריתמיקה)
מנגנון תחזוקתי לתחקור באגים, קריסות מנוע, והליכי הרצת אלגוריתמי 3D שונים.
- ErrorId: שלם (INT), מפתח ראשי (PK). חובה.
- JobId: שלם (INT), מפתח זר (FK), רשות (NULL) מתעד באג שארע בקונטקסט של ג'וב במידה וקיים.
- Context, Message, StackTrace: מחרוזות (NVARCHAR), חובה ורשות. דיווח נפילה, התראות קוד, מעקב.
- CreatedAt: תאריך, חובה.

================================================================================
"""

with open(r'c:\Users\1\Desktop\פרויקט שיבוץ\OptiLoad\ספר_סקירה_ופרק10.txt', 'a', encoding='utf-8') as f:
    f.write(content)

html_content = """<!DOCTYPE html>
<html lang="he" dir="rtl">
<head>
    <meta charset="UTF-8">
    <title>תרשימי מסד הנתונים OptiLoad - מוכנים להעתקה</title>
    <!-- Load Mermaid -->
    <script src="https://cdn.jsdelivr.net/npm/mermaid/dist/mermaid.min.js"></script>
    <style>
        body { font-family: Arial, sans-serif; background-color: #f4f4f4; padding: 20px; }
        .diagram-container {
            background-color: #ffffff;  /* רקע לבן כפי שביקשת */
            padding: 30px;
            margin: 20px auto;
            border-radius: 8px;
            box-shadow: 0 0 10px rgba(0,0,0,0.1);
            max-width: 1000px;
            text-align: center;
        }
        h2 { text-align: center; color: #333; margin-bottom: 20px;}
        .instructions { text-align: center; color: #666; margin-bottom: 20px; }
        .mermaid { margin: 0 auto; display: flex; justify-content: center; }
    </style>
    <script>
        mermaid.initialize({ startOnLoad: true, theme: 'default', backgroundColor: '#ffffff' });
    </script>
</head>
<body>

    <div class="instructions">
        <h1>תרשימי מסד הנתונים - פרויקט OptiLoad (פרק 22)</h1>
        <p>תרשימים אלו מעוצבים על רקע לבן נקי לחלוטין. להדבקה בוורד, גזרו אותם באמצעות "כלי החיתוך" (Snipping Tool) של Windows או שמרו כתמונה.</p>
    </div>

    <!-- DSD Diagram -->
    <div class="diagram-container">
        <h2>תרשים DSD קלאסי (רמת ישויות ותכונות)</h2>
        <div class="mermaid">
        classDiagram
            class Admin {
                +int Id [PK]
                +string Username
            }
            class PackingSession {
                +int SessionId [PK]
                +int AdminId [FK]
                +string Name
                +string Status
            }
            class SessionUser {
                +int SessionUserId [PK]
                +int SessionId [FK]
                +string DisplayName
            }
            class Box {
                +int BoxId [PK]
                +string BoxName
                +float Measurements
                +float WeightKg
                +bool IsFragile
                +bool AllowRotation
            }
            class Container {
                +int ContainerId [PK]
                +int TemplateId [FK]
                +string ContainerCode
            }
            class PackingJob {
                +int JobId [PK]
                +int ContainerId [FK]
                +int AdminId [FK]
                +string Status
                +float VolumeUtilization
            }
            class PlacementResult {
                +int PlacementId [PK]
                +int JobId [FK]
                +int BoxId [FK]
                +float PosX
                +float PosY
                +float PosZ
                +int RotationIndex
            }
            
            Admin "1" -- "*" PackingSession : מנהל
            Admin "1" -- "*" PackingJob : יוזם
            PackingSession "1" -- "*" SessionUser : מארח
            Container "1" -- "*" PackingJob : רץ עליה
            PackingJob "1" -- "*" PlacementResult : מפיק
            Box "1" -- "*" PlacementResult : ממוקם בתוך
        </div>
    </div>

    <!-- ERD Diagram -->
    <div class="diagram-container">
        <h2>תרשים ERD (מערכות יחסים בין כל טבלאות המערכת)</h2>
        <div class="mermaid">
        erDiagram
            ADMIN ||--o{ PACKINGSESSION : creates
            ADMIN ||--o{ PACKINGJOB : initiates
            PACKINGSESSION ||--o{ SESSIONUSER : hosts
            PACKINGSESSION ||--o{ SESSIONBOX : contains
            PACKINGSESSION ||--o{ ACCESSREQUEST : receives
            PACKINGSESSION ||--o{ BOXAUDITLOG : logs
            SESSIONUSER ||--o{ ACCESSREQUEST : requests
            BOX ||--o{ SESSIONBOX : "is_added_to"
            BOX ||--o{ PACKINGJOBBOX : "demanded_by"
            BOX ||--o{ PLACEMENTRESULT : "positioned_as"
            CONTAINERTEMPLATE ||--o{ CONTAINER : blueprints
            CONTAINER ||--o{ PACKINGJOB : receives
            PACKINGJOB ||--o{ PACKINGJOBBOX : "defines_requirements"
            PACKINGJOB ||--o{ PLACEMENTRESULT : yields
            PACKINGJOB ||--o{ CONTAINERSNAPSHOT : captures
            PACKINGJOB ||--o{ ERRORLOG : "might_generate"
        </div>
    </div>
</body>
</html>
"""

with open(r'c:\Users\1\Desktop\פרויקט שיבוץ\OptiLoad\db_diagrams.html', 'w', encoding='utf-8') as f:
    f.write(html_content)

print("done")
