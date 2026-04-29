using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using System.Data.Entity; // Ensure this is added for advanced LINQ if needed

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/session")]
    public class SessionController : ApiController
    {
        fypempEntities db = new fypempEntities();

        // 1. NAYA FUNCTION: Latest Active Session get karne ke liye
        [HttpGet]
        [Route("GetActiveSession")]
        public IHttpActionResult GetActiveSession()
        {
            try
            {
                // Sabse latest session uthayega (Ordered by ID Descending)
                var session = db.Session
                    .OrderByDescending(s => s.id)
                    .Select(s => new
                    {
                        SessionID = s.id,
                        SessionName = s.name
                    })
                    .FirstOrDefault();

                if (session == null)
                {
                    return NotFound();
                }

                return Ok(session);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("list")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var sessions = db.Session
                    .Select(s => new
                    {
                        s.id,
                        s.name
                    }).ToList();

                return Ok(sessions);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("create")]
        public IHttpActionResult Create(Session model)
        {
            try
            {
                db.Session.Add(model);
                db.SaveChanges();
                return Ok("Session added successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpPut]
        [Route("update/{id}")]
        public IHttpActionResult Update(int id, Session model)
        {
            try
            {
                var session = db.Session.Find(id);
                if (session == null) return NotFound();

                session.name = model.name;
                db.SaveChanges();

                return Ok("Session updated successfully");
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
                var session = db.Session.Find(id);
                if (session == null) return NotFound();

                db.Session.Remove(session);
                db.SaveChanges();

                return Ok("Session deleted successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }
}