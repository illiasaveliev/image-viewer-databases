using System.Collections.Generic;
using System.Threading.Tasks;
using ImageViewer.API.Models;

namespace ImageViewer.API.Services
{
    public interface IImagesRepository
    {
        Task DeleteAsync(string id, string key);
        Task<IEnumerable<ImageModel>> GetAllAsync();
    }
}