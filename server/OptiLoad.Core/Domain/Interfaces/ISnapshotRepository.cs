using OptiLoad.Core.Models;

namespace OptiLoad.Core.Services
{
    public interface ISnapshotRepository
    {
        Task SaveSnapshotsAsync(int jobId, IEnumerable<ContainerSnapshot> snapshots);
        Task<List<ContainerSnapshot>> GetSnapshotsAsync(int jobId);
        Task DeleteSnapshotsAsync(int jobId);
    }
}
