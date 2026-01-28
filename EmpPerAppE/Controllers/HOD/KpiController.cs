using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/kpi")]
    public class KpiController : ApiController
    {
        fypempEntities db = new fypempEntities();

        [HttpGet]
        [Route("list")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var kpis = db.KPI.Select(k => new
                {
                    k.id,
                    k.name
                }).ToList();

                return Ok(kpis);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("create")]
        public IHttpActionResult Create(KPI model)
        {
            try
            {
                db.KPI.Add(model);
                db.SaveChanges();
                return Ok("KPI added successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPut]
        [Route("update/{id}")]
        public IHttpActionResult Update(int id, KPI model)
        {
            try
            {
                var kpi = db.KPI.Find(id);
                if (kpi == null) return NotFound();

                kpi.name = model.name;
                db.SaveChanges();

                return Ok("KPI updated successfully");
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
                var kpi = db.KPI.Find(id);
                if (kpi == null) return NotFound();

                db.KPI.Remove(kpi);
                db.SaveChanges();

                return Ok("KPI deleted successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

}
