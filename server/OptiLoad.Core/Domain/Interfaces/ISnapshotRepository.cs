using OptiLoad.Core.Models;

namespace OptiLoad.Core.Services
{
    public interface ISnapshotRepository//פונקציות לגישה למסד הנתונים לאיזור של תמונות
    {
        Task SaveSnapshotsAsync(int jobId, IEnumerable<ContainerSnapshot> snapshots);//שומר תמונות של מכולה עבור עבודה מסוימת
        Task<List<ContainerSnapshot>> GetSnapshotsAsync(int jobId);//מחזירה רשימת תמונות של מכולה עבור עבודה מסוימת
        Task DeleteSnapshotsAsync(int jobId);//מוחקת את כל התמונות של מכולה עבור עבודה מסוימת
    }
}
