using System.ComponentModel.DataAnnotations;

namespace OptiLoad.Core.Models
{
    public class Admin//מודל שמייצג מנהל מערכת, כולל שם משתמש, סיסמה מוצפנת ומלח להצפנה
    {
        public int Id { get; set; }//מזהה ייחודי למנהל, משמש כמפתח ראשי במסד הנתונים
        [Required]//חייב להיות ייחודי במסד הנתונים
        public string Username { get; set; }//שם המשתמש של המנהל, משמש לזיהוי והתחברות למערכת
        [Required]
        public string PasswordHash { get; set; }//הסיסמה המוצפנת של המנהל, מאוחסנת בצורה מוצפנת כדי להגן על אבטחת המידע
        [Required]
        public string PasswordSalt { get; set; }//המלח המשמש להצפנת הסיסמה, משמש להגנה נוספת על אבטחת המידע
    }
}
