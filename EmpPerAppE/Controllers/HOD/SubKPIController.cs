using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/subkpi")]
    public class SubKPIController : ApiController
    {
        fypempEntities db = new fypempEntities();

        [HttpGet]
        [Route("list")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var data = db.SubKPI
                    .Select(s => new
                    {
                        s.id,
                        s.name,
                        s.KPIID,
                        KPIName = s.KPI.name
                    }).ToList();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("create")]
        public IHttpActionResult Create(SubKPI model)
        {
            try
            {
                db.SubKPI.Add(model);
                db.SaveChanges();
                return Ok("SubKPI added successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPut]
        [Route("update/{id}")]
        public IHttpActionResult Update(int id, SubKPI model)
        {
            try
            {
                var sub = db.SubKPI.Find(id);
                if (sub == null) return NotFound();

                sub.name = model.name;
                sub.KPIID = model.KPIID;
                db.SaveChanges();

                return Ok("SubKPI updated successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpDelete]
        [Route("delete/{id}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var sub = db.SubKPI.Find(id);
                if (sub == null) return NotFound();

                db.SubKPI.Remove(sub);
                db.SaveChanges();

                return Ok("SubKPI deleted successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

}
