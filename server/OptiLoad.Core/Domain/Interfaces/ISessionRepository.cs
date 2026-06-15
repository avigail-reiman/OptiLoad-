using OptiLoad.Core.Models;

namespace OptiLoad.Core.Services
{
    public interface ISessionRepository//פונקציות של גישה למסד נתונים של עבודות שיבוץ
    {
        Task<List<PackingSession>> GetSessionsByAdminAsync(int adminId);//מחזירה רשימת סשנים של מנהל מסוים
        Task<PackingSession?>      GetSessionByIdAsync(int sessionId);//מחזירה סשן לפי מזהה הסשן שלו או null אם לא נמצא
        Task<PackingSession?>      GetSessionByTokenAsync(string token);//מחזירה סשן לפי טוקן ייחודי שלו או null אם לא נמצא
        Task<int>                  CreateSessionAsync(int adminId, string name, string? description, string linkToken);//יוצרת סשן חדש ומחזירה את מזהה הסשן
        Task                       UpdateSessionStatusAsync(int sessionId, string status);//מעדכנת את סטטוס הסשן
        Task                       DeleteSessionAsync(int sessionId);//מוחקת סשן

        Task<SessionUser?> GetSessionUserByTokenAsync(string token);//מחזירה משתמש סשן לפי טוקן ייחודי שלו או null אם לא נמצא
        Task<int>          CreateSessionUserAsync(int sessionId, string displayName, string? email, string token, string? previousToken = null);//יוצרת משתמש סשן חדש ומחזירה את מזהה המשתמש
        Task<string?>      GetRootTokenAsync(string userToken);//מחזירה את הטוקן השורש של המשתמש לפי טוקן המשתמש או null אם לא נמצא
        Task<int>          GetDeniedCountInChainAsync(string rootToken, int sessionId);//מחזירה את מספר הפעמים שהגישה נדחתה בשרשרת של טוקן שורש מסוים בעבודה מסוימת

        Task<List<AccessRequest>> GetRequestsBySessionAsync(int sessionId);//מחזירה רשימת בקשות גישה לסשן מסוים
        Task<AccessRequest?>      GetRequestByIdAsync(int requestId);//מחזירה בקשת גישה לפי מזהה הבקשה או null אם לא נמצא
        Task<AccessRequest?>      GetRequestByUserTokenAsync(string userToken);//מחזירה בקשת גישה לפי טוקן המשתמש או null אם לא נמצא
        Task<int>                 CreateAccessRequestAsync(int sessionId, int sessionUserId);//יוצרת בקשת גישה חדשה ומחזירה את מזהה הבקשה
        Task                      UpdateRequestStatusAsync(int requestId, string status);//מעדכנת את סטטוס בקשת הגישה

        Task<List<SessionBox>> GetSessionBoxesAsync(int sessionId);//מחזירה רשימת קופסאות בסשן מסוים
        Task<int>              AddSessionBoxAsync(int sessionId, int boxId, int quantity, string addedBy);//מוסיפה קופסה לסשן ומחזירה את מזהה הקופסה בסשן
        Task<bool>             DeleteSessionBoxAsync(int sessionBoxId, int sessionId);//מוחקת קופסה מסשן
        Task<bool>             UpdateSessionBoxQuantityAsync(int sessionBoxId, int sessionId, int newQuantity);//מעדכנת את הכמות של קופסה בסשן

        Task<List<BoxAuditLog>> GetAuditLogAsync(int sessionId);//מחזירה את יומן הביקורת של קופסאות בסשן מסוים
        Task                    AddAuditLogAsync(BoxAuditLog log);//מוסיפה רשומה ליומן הביקורת
    }
}
