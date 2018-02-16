using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace PercentArea.Controllers
{
    public class GridCellController : ApiController
    {
        /* GET api/GridCell
        public IEnumerable<Object> Get()
        {
            Models.GridCell cell = new Models.GridCell();
            return cell.CalculateDataTable();
        }*/

        // GET api/GridCell/01020003            Method for taking catchment number
        [Route("api/GridCell/{id:length(8)}")]
        public List<Object> Get(string id)
        {
            Models.GridCell cell = new Models.GridCell();
            return cell.CalculateDataTable(id);
        }

        //To be added later
        /*
        // GET api/GridCell/Shapefile           Method for taking zip file
        [Route("api/GridCell/{id:alpha}")]
        public List<Object> Get(string id)
        {
            Models.GridCell cell = new Models.GridCell();
            return cell.ShapefileCalculation(id);
        }*/
    }
}
