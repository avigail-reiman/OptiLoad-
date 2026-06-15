using OptiLoad.Core.Models;

namespace OptiLoad.Core.Services
{

    public interface IPackingRepository//פונקציות של גישה למסד הנתונים עבוד עבודות שיבוץ
    {
        Task<ContainerDimensions> GetContainerDimensions(int jobId);//פונקציה שמחזירה מידות של מכולה עבוד עבודה מסוימת
        Task<List<BoxInstance>>   GetJobBoxInstances(int jobId);//פונקציה שמחזירה רשימת מופעים של קופסאות שיש לשבץ בעבודה מסוימת, כולל המידות והמשקל של כל קופסה
        Task  SavePlacementResults(int jobId, PackingResult result);//פונקציה ששומרת תוצאות שיבוץ של עבודה מסוימת
        Task  CompleteJob(int jobId, PackingResult result);//פונקציה שמסיימת עבודה מסוימת ושומרת את תוצאות השיבוץ שלה
        Task  LogError(int jobId, string context, Exception ex);//פונקציה ששומרת שגיאה שקרתה במהלך שיבוץ מסוים
    }
}
