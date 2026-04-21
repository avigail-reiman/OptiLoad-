# OptiLoad

מערכת לאופטימיזציה של אריזת קופסאות בקונטיינרים תוך שימוש באלגוריתמים מתקדמים.

## מה המערכת עושה?

מחשבת את האריזה האופטימלית של קופסאות בתוך קונטיינרים תוך מינימיזציה של מספר הקונטיינרים הנדרשים, תוך שמירה על האילוצים הבאים:
- משקל מקסימלי לקונטיינר
- קופסאות שבירות (לא ניתן לסדר עליהן קופסאות אחרות)
- אוריינטציות חוקיות לכל קופסה
- ניצולת מרחב מקסימלית

## טכנולוגיות

| שכבה | טכנולוגיה |
|------|-----------|
| Backend | ASP.NET Core 10, C# |
| Algorithms | Branch & Bound, Greedy, Corner Points 3D |
| Database | SQL Server (LocalDB), Entity Framework Core |
| Frontend | HTML, CSS, JavaScript, Three.js |

## מבנה הפרויקט

```
OptiLoad/
├── client/                  # ממשק משתמש (דפדפן)
│   ├── pages/               # קבצי HTML
│   ├── js/                  # JavaScript
│   └── css/                 # עיצוב
├── db/                      # סקריפטי SQL (יצירת טבלאות, seed data)
├── server/
│   ├── OptiLoad.API/        # שכבת HTTP – Controllers, CORS, Routing
│   ├── OptiLoad.Core/       # אלגוריתמים ומודלים עסקיים
│   └── OptiLoad.Data/       # גישה למסד הנתונים (EF Core)
└── OptiLoad.slnx            # קובץ solution
```

## הרצה מקומית

### דרישות
- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- SQL Server LocalDB (מגיע עם Visual Studio)

### שלבים

1. שכפל את הריפו:
   ```bash
   git clone https://github.com/YOUR_USERNAME/OptiLoad.git
   cd OptiLoad
   ```

2. צור קובץ `server/OptiLoad.API/appsettings.Development.json` עם:
   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=OptiLoadDB;Trusted_Connection=True;TrustServerCertificate=True;"
     }
   }
   ```

3. הרץ מיגרציות:
   ```bash
   cd server/OptiLoad.API
   dotnet ef database update
   ```

4. הפעל את השרת:
   ```bash
   dotnet run --launch-profile http
   ```

5. פתח בדפדפן את הקובץ:
   ```
   client/pages/01_main-packing-interface.html
   ```

## API

| Method | Endpoint | תיאור |
|--------|----------|-------|
| POST | `/api/visualization/run` | הרצת אלגוריתם אריזה וקבלת תוצאה עם ויזואליזציה |
| GET | `/api/boxes` | רשימת כל הקופסאות |
| GET | `/api/containers` | רשימת כל הקונטיינרים |
| GET/POST | `/api/packingjobs` | ניהול עבודות אריזה |
