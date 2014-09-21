using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Web.Http;

namespace Scurry.Executor.Service.Api.Controllers
{
    public class JobsController : ApiController
    {
        // GET api/Jobs
        public HttpResponseMessage Get()
        {
            try
            {
                // TODO: Get current status of all jobs
                return Request.CreateResponse(HttpStatusCode.OK, "TODO");
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }

        // GET api/Jobs/{name}
        public HttpResponseMessage Get(string name)
        {
            try
            {
                // TODO: Get current status of job in question
                return Request.CreateResponse(HttpStatusCode.OK, "TODO");
            }
            catch (Exception ex)
            {
                return Request.CreateErrorResponse(HttpStatusCode.BadRequest, ex);
            }
        }
    }
}
