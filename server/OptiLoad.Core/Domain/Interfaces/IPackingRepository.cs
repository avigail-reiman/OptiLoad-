using OptiLoad.Core.Models;

namespace OptiLoad.Core.Services
{

    public interface IPackingRepository
    {
        Task<ContainerDimensions> GetContainerDimensions(int jobId);
        Task<List<BoxInstance>>   GetJobBoxInstances(int jobId);
        Task                      SavePlacementResults(int jobId, PackingResult result);
        Task                      CompleteJob(int jobId, PackingResult result);
        Task                      LogError(int jobId, string context, Exception ex);
    }
}
