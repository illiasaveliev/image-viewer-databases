using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ImageViewer.API.Models;
using ImageViewer.API.Services;
using Microsoft.AspNetCore.Mvc;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace ImageViewer.API.Controllers
{
    [Route("api/[controller]")]
    public class ImagesController : Controller
    {
        private readonly IImagesRepository imagesRepository;

        public ImagesController(IImagesRepository imagesRepository)
        {
            this.imagesRepository = imagesRepository;
        }

        [HttpGet]
        public async Task<JsonResult> Get()
        {
            try
            {
                List<ImageModel> images = (await imagesRepository.GetAllAsync()).ToList();

                return new JsonResult(images);
            }
            catch (Exception e)
            {
                Response.StatusCode = 500;
                return new JsonResult(e.Message);
            }
        }
    }
}
