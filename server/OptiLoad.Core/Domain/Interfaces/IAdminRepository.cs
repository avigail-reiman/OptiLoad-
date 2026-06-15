using OptiLoad.Core.Models;

namespace OptiLoad.Core.Services
{
    public interface IAdminRepository//מספק פונקציות של גישה למסד הנתונים של המנהלים
    {
        Task<Admin?> GetAdminByUsername(string username);//פונקצייה שמחזירה את המנהל לפי שם המשתמש שלו או null אם לא נמצא, משמשת לאימות המנהל בעת ההתחברות ולבדיקת קיום שם המשתמש בעת הרישום
        Task<bool>   AdminsExist();//פונקצייה שמחזירה true אם יש לפחות מנהל אחד במסד הנתונים, משמשת לזריעת מנהל ברירת מחדל אם אין מנהלים קיימים
        Task CreateAdmin(string username, string passwordHash, string passwordSalt);//פונקצייה שיוצרת מנהל חדש במסד הנתונים
    }
}
