# מבנה הפרויקט — OptiLoad

מדריך מלא למטרת כל תיקייה וקובץ בפרויקט.

---

## שורש הפרויקט

| קובץ/תיקייה | מטרה |
|-------------|------|
| `OptiLoad.slnx` | קובץ ה-Solution של Visual Studio — מחבר את כל הפרויקטים (API, Core, Data, Tests) ליחידה אחת |
| `README.md` | תיעוד ראשוני: מה המערכת עושה, טכנולוגיות, הוראות הרצה, טבלת API |
| `PROJECT_STRUCTURE.md` | הקובץ הזה — הסבר מבנה הפרויקט |
| `.gitignore` | רשימת קבצים ותיקיות שGit מתעלם מהם (bin/, obj/, secrets וכו') |

---

## `client/` — ממשק המשתמש (Frontend)

הקבצים שרצים **בדפדפן**. מוגשים ישירות מהשרת כ-static files.

```
client/
├── index.html              ← דף ברירת מחדל (redirect ללוגין)
├── css/
│   └── styles.css          ← כל עיצוב המערכת (צבעים, פריסה, כפתורים)
├── js/
│   └── api.js              ← פונקציות JavaScript לתקשורת עם ה-API (fetch, auth headers)
└── pages/
    ├── login.html           ← דף כניסה: טופס שם משתמש + סיסמה, שולח JWT
    ├── 01_main-packing-interface.html  ← ממשק ראשי: בחירת קופסות וקונטיינר, הפעלת אלגוריתם
    ├── 02_3d-result-viewer.html        ← תצוגת תוצאה תלת-ממדית עם Three.js
    └── 03_snapshot-viewer.html         ← צפייה בסנאפשוטים של שלבי הטעינה
```

### `client/index.html`
דף ריק שמפנה אוטומטית לדף הלוגין. נקודת הכניסה לאפליקציה.

### `client/css/styles.css`
כל הסגנון הויזואלי: צבעי רקע, כפתורים, טבלאות, תצוגת 3D. קובץ CSS יחיד לכל האפליקציה.

### `client/js/api.js`
פונקציות JavaScript שכל הדפים משתמשים בהן:
- שליחת בקשות HTTP עם Authorization header (JWT token)
- ניהול token (שמירה ב-localStorage, קריאה, מחיקה בהתנתקות)
- פונקציות עזר לקריאות GET/POST לנקודות ה-API

### `client/pages/login.html`
דף הלוגין. שולח POST ל-`/api/auth/login`, מקבל JWT token ושומר אותו ב-localStorage.

### `client/pages/01_main-packing-interface.html`
הממשק הראשי של המערכת:
- טעינת רשימת קופסות מה-DB (GET /api/boxes)
- טעינת רשימת קונטיינרים (GET /api/containers)
- בחירת כמויות לכל קופסה
- הפעלת חישוב ה-Packing (POST /api/visualization/run)
- הצגת תוצאות: כמה מכלים, ניצולת

### `client/pages/02_3d-result-viewer.html`
תצוגה תלת-ממדית של תוצאת האריזה:
- משתמש ב-Three.js לרינדור 3D
- כל קופסה = BoxGeometry בצבע שונה
- ניתן לסובב ולזוז עם העכבר (OrbitControls)
- מקבל נתוני placement מה-API

### `client/pages/03_snapshot-viewer.html`
צופה בתמונות/סנאפשוטים של שלבי הטעינה — מאפשר לראות בסדר כרונולוגי איך הקופסות נטענו לקונטיינר.

---

## `db/` — סקריפטי מסד נתונים

קבצי SQL להקמת ה-DB ומילויו בנתוני ניסיון. **מריצים פעם אחת בהתקנה.**

### `db/migrate-admin.sql`
יצירת טבלת `Admin` ב-SQL Server. מריצים לפני הפעלה ראשונה.

### `db/seed-boxes.sql`
הכנסת 16 קופסות לדוגמה לטבלת `Box`: גדלים שונים (TINY, SMALL, MEDIUM, LARGE), קופסות שבירות, קופסות עם/בלי אפשרות סיבוב. מאפשר עבודה עם נתונים מציאותיים מיד.

---

## `server/` — Backend (שרת)

מחולק ל-4 פרויקטי C# עצמאיים:

```
server/
├── OptiLoad.API/     ← שכבת HTTP: Controllers, Auth, Routing
├── OptiLoad.Core/    ← הלב: אלגוריתמים, מודלים, לוגיקה עסקית
├── OptiLoad.Data/    ← גישה למסד הנתונים
└── OptiLoad.Tests/   ← בדיקות יחידה
```

---

## `server/OptiLoad.API/` — שכבת HTTP

**מה היא עושה:** מקבלת בקשות HTTP, מאמתת JWT, מפנה לשירותים המתאימים, מחזירה JSON.  
**לא אמורה להכיל לוגיקה עסקית** — רק "ניתוב".

### `Program.cs`
נקודת הכניסה של השרת. מגדיר הכל:
- **Dependency Injection**: רישום DatabaseService, PackingService, AdminService
- **JWT Authentication**: קביעת מפתח חתימה (≥32 תווים), אימות token בכל בקשה
- **CORS**: אישור גישה מ-localhost:5098 ו-localhost:5500 (Live Server)
- **Static Files**: הגשת תיקיית `client/` כאתר סטטי
- **Swagger**: ממשק תיעוד ובדיקת API (בסביבת פיתוח בלבד)
- **Seed**: יצירת אדמין ברירת מחדל אם אין אף אחד ב-DB

### `appsettings.json`
הגדרות ברירת מחדל: Logging, AllowedHosts. **לא** מכיל connection string (זה ב-Development).

### `appsettings.Development.json`
הגדרות לסביבת פיתוח בלבד (מוחרג מ-Git):
- `ConnectionStrings:DefaultConnection` — מחרוזת חיבור ל-SQL Server LocalDB
- `Jwt:Key` — המפתח הסודי לחתימת JWT tokens

### `OptiLoad.API.csproj`
קובץ הגדרות הפרויקט: גרסת .NET (10), חבילות NuGet (JWT, Swagger וכו'), תלויות בפרויקטים אחרים.

### `OptiLoad.API.http`
קובץ בדיקות HTTP של VS Code — כולל בקשות לדוגמה לכל endpoint. שימושי לבדיקה מהירה בלי Postman.

### `TestDataMain.cs`
מחלקה עזר להרצת נתוני ניסיון (Test Data) ישירות מהשרת — לצורך debugging ופיתוח.

### `Properties/launchSettings.json`
הגדרות הפעלה: פרופילי `http`/`https`, פורטים (5098/7098), האם לפתוח דפדפן.

### `DTOs/VisualizationDtos.cs`
**DTO = Data Transfer Object** — מחלקות שמייצגות את מבנה ה-JSON שנשלח/מתקבל ב-API.  
ספציפי לנקודות הקצה של הויזואליזציה: request עם רשימת קופסות, response עם placement data.

### `wwwroot/`
תיקייה סטטית חלופית לשרת — כוללת כפילויות של כמה דפי HTML. ב-Production ייתכן שמשמשת במקום `client/`.

---

## `server/OptiLoad.API/Controllers/` — בקרי HTTP

כל Controller = קבוצת endpoints לנושא אחד. כולם מאובטחים ב-JWT (למעט Login).

### `AuthController.cs`
`POST /api/auth/login`  
מקבל שם משתמש + סיסמה, בודק מול ה-DB, מחזיר JWT token חתום עם תוקף.

### `BoxController.cs`
ניהול קופסות:
- `GET /api/boxes` — כל הקופסות
- `GET /api/boxes/{id}` — קופסה ספציפית
- `POST /api/boxes` — יצירת קופסה חדשה
- `PUT /api/boxes/{id}` — עדכון
- `DELETE /api/boxes/{id}` — מחיקה

### `ContainerController.cs`
`GET /api/containers` — רשימת תבניות הקונטיינרים הזמינות מה-DB.

### `PackingJobController.cs`
ניהול עבודות אריזה:
- `POST /api/packingjobs` — יצירת עבודה חדשה + הרצת אלגוריתם
- `GET /api/packingjobs` — כל העבודות
- `GET /api/packingjobs/{id}` — עבודה ספציפית + תוצאות
- `GET /api/packingjobs/{id}/placements` — פרטי מיקום כל קופסה

### `SnapshotController.cs`
ניהול סנאפשוטים של שלבי הטעינה:
- `POST /api/snapshots` — שמירת סנאפשוט (תמונה + מטאדטה)
- `GET /api/snapshots/{jobId}` — כל הסנאפשוטים לעבודה מסוימת

### `VisualizationController.cs`
`POST /api/visualization/run`  
הנקודה הראשית: מקבל רשימת קופסות + קונטיינר, מריץ את אלגוריתם ה-Packing, מחזיר JSON מלא עם כל ה-placements לרינדור 3D.

---

## `server/OptiLoad.Core/` — הלב העסקי

**הפרויקט הכי חשוב.** לא יודע כלום על HTTP או SQL — רק לוגיקה.

---

## `server/OptiLoad.Core/Domain/Models/` — מודלי הנתונים

### `Box.cs`
ישות קופסה כפי שנשמרת ב-DB ומשמשת בחישוב:
- מידות (W×H×D), משקל, שביר/לא-שביר, אפשרות סיבוב
- **`GetAllowedRotations()`** — מחזיר עד 6 סיבובים חוקיים (מסנן כפולות עם HashSet)

### `Container.cs`
מופע קונטיינר ספציפי שנמצא ב-DB (עם ID, שם וכו').

### `ContainerTemplate.cs`
תבנית קונטיינר — ממדים ומשקל מקסימלי שממנה ניתן ליצור מופעים.

### `ComputationModels.cs`
כל המודלים שמשמשים **רק בזמן חישוב** (לא ב-DB):

| מחלקה | תיאור |
|-------|--------|
| `ContainerDimensions` | ממדי הקונטיינר + משקל מקס' לחישוב |
| `BoxInstance` | עותק ספציפי של קופסה בעבודה (Box#0, Box#1…) |
| `Rotation` | `record struct` — קופסה לאחר סיבוב. בלתי-ניתן לשינוי |
| `Position3D` | `record struct` — נקודת מיקום (X,Y,Z) |
| `PlacedBox` | קופסה שהונחה: מיקום + סיבוב + X1/X2/Y1/Y2/Z1/Z2 |
| `PackingState` | מצב מיכל אחד: רשימת PlacedBoxes + משקל כולל |
| `BinStats` | סטטיסטיקות מיכל: נפח שנוצל, בזבוז |
| `PackingResult` | תוצאה סופית: מכלים בשימוש, ניצולת, קופסות שלא נכנסו |

### `PackingJob.cs`
ישות עבודת אריזה ב-DB: מצב (Pending/Running/Done), זמן התחלה/סיום, תוצאות כלליות.

### `PackingJobBox.cs`
טבלת קשר רבים-לרבים בין עבודה לקופסות — קופסה X בכמות Y לעבודה Z.

### `PlacementResult.cs`
רשומת מיקום קופסה שנשמרת ב-DB: מזהה עבודה, מזהה קופסה, X/Y/Z, סיבוב, מספר מיכל.

### `ContainerSnapshot.cs`
סנאפשוט של שלב טעינה: תמונה (Base64/URL), מספר שלב, מזהה עבודה.

### `Admin.cs`
ישות משתמש מנהל: שם משתמש + סיסמה מגובבת (hashed).

### `ErrorLog.cs`
רשומת שגיאה ב-DB: הודעה, stack trace, timestamp.

---

## `server/OptiLoad.Core/Domain/Interfaces/` — חוזי גישה לנתונים

ממשקים שמגדירים **מה** ניתן לעשות עם ה-DB — בלי לדעת **איך**. מאפשרים להחליף את שכבת ה-DB בקלות.

### `IPackingRepository.cs`
פעולות על קופסות, קונטיינרים ועבודות אריזה:
- `GetBoxesAsync()`, `GetBoxByIdAsync()`, `SaveBoxAsync()`, `DeleteBoxAsync()`
- `GetContainersAsync()`, `GetContainerTemplatesAsync()`
- `SavePackingJobAsync()`, `SavePlacementsAsync()`, `GetJobResultsAsync()`

### `IAdminRepository.cs`
פעולות על טבלת Admin:
- `GetAdminByUsernameAsync()` — לאימות בהתחברות
- `CreateAdminAsync()` — יצירת אדמין חדש

### `ISnapshotRepository.cs`
שמירה וקריאה של סנאפשוטים:
- `SaveSnapshotAsync()`, `GetSnapshotsByJobAsync()`

---

## `server/OptiLoad.Core/Application/Algorithms/` — אלגוריתמי האריזה

### `BranchAndBoundSolver.cs`
**האלגוריתם הראשי.** פותר את בעיית ה-Bin Packing ל-3D באופן אופטימלי (כמעט).

**זרימה:**
1. חישוב LowerBound (המינימום האפשרי תיאורטית)
2. HeuristicH1 נותן Upper Bound ראשוני
3. Branch & Bound: מחפש עץ של הקצאות קופסה→מיכל
4. Pruning: גוזם ענפים שלא יכולים להשתפר
5. שלב שבירות: מריץ שנית רק על שבירות
6. Post-processing: repack + brute-force למקסום ניצולת

**גבולות:** מקסימום 10,000,000 nodes ו/או time limit → fallback ל-Greedy.

### `GreedySolver.cs`
**אלגוריתם Greedy מהיר.** לכל קופסה (מהגדולה לקטנה): מנסה להכניס למיכל הכי מלא. אם לא → פותח מיכל חדש. לא אופטימלי אבל מהיר מאוד.

### `SingleBinFiller.cs`
**Branch & Bound לוקלי למיכל אחד.** מנסה למלא מיכל בודד כמה שיותר. משמש כ"subroutine" ב-BranchAndBoundSolver. גם כן עם מגבלת nodes.

### `CornerPointsFinder.cs`
**Corner Points Method.** מוצא את כל נקודות ה-"פינה" החוקיות שבהן ניתן להניח קופסה הבאה.  
**רעיון:** כל פתרון אופטימלי ניתן לייצוג כך שכל קופסה יושבת בנקודת פינה → מצמצם חיפוש ל-∞ ל-O(n²).

### `LowerBoundCalculator.cs`
**חישוב גבול תחתון** — המינימום התיאורטי של מכלים נדרשים.  
- **L0**: חלוקת נפח כולל בנפח מיכל  
- **L1**: מתחשב בממדים בודדים (קופסות שגדולות מ-W/2 לא יכולות לחלוק שורה)  
- **L2**: L1 מוחזק עם תמחור נפח נוסף  
- **ComputeBestLowerBound**: מחזיר max(L0, L1, L2)

### `HeuristicH1.cs`
**Slice Packing Heuristic.** יוצר פתרון ראשוני מהיר (Upper Bound) ל-B&B.  
מנסה 3 ציר עומק (X/Y/Z), מחלק לפרוסות לפי עומק, בכל פרוסה ממלא ב-2D Strip Packing. מחזיר הכי טוב מ-3 ניסיונות.

### `GravitySettler.cs`
**כיווץ פיזיקלי.** אחרי הנחת קופסות, "מושך" אותן לפינות:  
Compact X → כלפי שמאל, Compact Z → קדימה, Compact Y → למטה (כבידה).  
חוזר עד שאין תזוזה. מכבד כלל שבירות: קופסה רגילה לא תשב על שבירה.

### `LoadingSequencer.cs`
**מחשב סדר טעינה** (Loading Order) לפי היגיון LIFO (אחרון-נכנס-ראשון-יוצא).  
בונה גרף תלות (DAG): קופסה שנמצאת מתחת/מאחורי אחרת חייבת להיטען קודם.  
מריץ Topological Sort ומחזיר סדר עם מספרי שלב.

---

## `server/OptiLoad.Core/Application/Services/` — שירותים עסקיים

### `PackingService.cs`
**Facade** — שירות שמרכז את כל ה-Packing API:
- `RunPackingJobInMemory()` — מריץ B&B ומחזיר תוצאה בזיכרון (בלי DB)
- `RunPackingJob()` — מריץ ושומר ב-DB
- `RunPackingJobWithTimeLimit()` — עם מגבלת זמן
- `PrintReport()` — הדפסת דוח לקונסולה

### `AdminService.cs`
ניהול משתמשי אדמין:
- `AuthenticateAsync()` — בדיקת שם משתמש + סיסמה → מחזיר אדמין או null
- `SeedDefaultAdminIfEmptyAsync()` — יוצר אדמין ברירת מחדל אם הטבלה ריקה

### `PasswordHasher.cs`
גיבוב (Hashing) סיסמאות לפני שמירה ב-DB ובדיקה בהתחברות. משתמש ב-PBKDF2/BCrypt.

### `TestDataRunner.cs`
מריץ תרחישי ניסיון מ-`SampleTestData.json` — שימושי לבדיקת ביצועי האלגוריתמים.

---

## `server/OptiLoad.Core/TestData/`

### `SampleTestData.json`
קובץ JSON עם תרחישי ניסיון מוכנים: קונטיינרים ורשימות קופסות בגדלים שונים. נטען על-ידי `TestDataRunner`.

---

## `server/OptiLoad.Data/` — שכבת מסד הנתונים

**מימוש** של ממשקי ה-Repository. כל הקוד שנוגע ב-SQL Server נמצא כאן.

### `DatabaseService.cs`
מימוש של `IPackingRepository`, `IAdminRepository`, `ISnapshotRepository` בכיתה אחת.  
20+ מתודות async: כל אחת בונה SQL command, מריצה, ממפה תוצאה לאובייקטים.  
משתמש ב-`SqlConnection` ישיר (לא EF Core) עם Parameterized Queries (הגנה מ-SQL Injection).

### `OptiLoad.Data.csproj`
הגדרות פרויקט: תלות ב-`Microsoft.Data.SqlClient`.

### `Migrations/AddAdminsTable.sql`
Migration מגובה ב-SQL — יצירת טבלת `Admin` עם עמודות: Id, Username, PasswordHash, CreatedAt.

---

## `server/OptiLoad.Tests/` — בדיקות יחידה

בדיקות xUnit לאלגוריתמי הליבה. לא בודקות HTTP או DB — רק לוגיקה.

### `BoxTests.cs`
5 בדיקות ל-`Box.GetAllowedRotations()`:
- קוביה מחזירה סיבוב אחד (לא 6 כפולים)
- קופסה ללא סיבוב מחזירה סיבוב אחד
- נפח נשמר בכל הסיבובים
- מספר הסיבובים הנכון לקופסה עם ממדים שונים

### `BranchAndBoundTests.cs`
4 בדיקות ל-`BranchAndBoundSolver.Solve()`:
- קלט ריק → 0 מכלים
- קופסה אחת → מיכל אחד
- 2 קופסות שנכנסות יחד → מיכל אחד
- קופסה גדולה מהמיכל → לא נכנסת, UnplacedBoxes ≠ ריק

### `GreedySolverTests.cs`
4 בדיקות ל-`GreedySolver.FillRemaining()`:
- קופסה אחת שנכנסת
- מספר קופסות, כולן נכנסות
- קופסה גדולה מדי → נשארת ב-unplaced
- שימוש במכלים פתוחים קיימים

### `LowerBoundTests.cs`
4 בדיקות ל-`LowerBoundCalculator.ComputeL0()`:
- נפח כולל = בדיוק נפח מיכל → L0=1
- נפח כולל = פי 2 → L0=2
- ריק → L0=0
- נפח חלקי → עיגול כלפי מעלה

### `OptiLoad.Tests.csproj`
הגדרות פרויקט הבדיקות: תלות ב-xUnit, Microsoft.NET.Test.Sdk, ו-OptiLoad.Core.

---

## סיכום תרשים תלויות

```
OptiLoad.Tests
    └── depends on → OptiLoad.Core

OptiLoad.API
    ├── depends on → OptiLoad.Core
    └── depends on → OptiLoad.Data

OptiLoad.Data
    └── depends on → OptiLoad.Core

OptiLoad.Core
    └── (אין תלויות חיצוניות — רק .NET standard)
```

**כלל מרכזי:** OptiLoad.Core לא מכיר אף פרויקט אחר. זה מה שמאפשר לבדוק אותו בנפרד ולהחליף שכבות (DB, HTTP) בלי לגעת בלוגיקה.
