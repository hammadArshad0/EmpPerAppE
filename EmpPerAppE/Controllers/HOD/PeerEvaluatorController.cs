using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/peerevaluator")]
    public class PeerEvaluatorController : ApiController
    {
        fypempEntities db = new fypempEntities();

        [HttpGet]
        [Route("list")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var data = db.PeerEvaluator
                    .Select(p => new
                    {
                        p.id,
                        p.teacherID,
                        TeacherName = p.Teacher.name,
                        p.sessionID,
                        SessionName = p.Session.name
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
        public IHttpActionResult Create(PeerEvaluator model)
        {
            try
            {
                db.PeerEvaluator.Add(model);
                db.SaveChanges();
                return Ok("PeerEvaluator added successfully");
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
                var pe = db.PeerEvaluator.Find(id);
                if (pe == null) return NotFound();

                db.PeerEvaluator.Remove(pe);
                db.SaveChanges();

                return Ok("PeerEvaluator deleted successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

}
