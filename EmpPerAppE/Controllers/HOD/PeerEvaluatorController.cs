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

            ///---------------------
            ///[HttpGet]
            [Route("Teachers")]
            public IHttpActionResult GetTeachers()
            {
                var teachers = db.Teacher
                    .Select(t => new
                    {
                        t.userID,
                        t.name,
                        t.department
                    })
                    .ToList();

                return Ok(teachers);
            }


            [HttpGet]
            [Route("Sessions")]
            public IHttpActionResult GetSessions()
            {
                var sessions = db.Session
                    .Select(s => new
                    {
                        s.id,
                        s.name
                    })
                    .ToList();

                return Ok(sessions);
            }


            //[HttpPost]
            //[Route("Add")]
            //public IHttpActionResult AddPeerEvaluators(AddPeerEvaluatorDto model)
            //{
            //    if (model == null || model.TeacherIds == null || model.TeacherIds.Count == 0)
            //        return BadRequest("Invalid data");

            //    int sessionId = model.SessionId;

            //    foreach (var teacherId in model.TeacherIds)
            //    {
            //        string teacherIdStr = teacherId.ToString();

            //        bool alreadyExists = db.PeerEvaluator.Any(pe =>
            //            pe.teacherID == teacherIdStr &&
            //            pe.sessionID == sessionId
            //        );

            //        if (!alreadyExists)
            //        {
            //            db.PeerEvaluator.Add(new PeerEvaluator
            //            {
            //                teacherID = teacherIdStr,
            //                sessionID = sessionId
            //            });
            //        }
            //    }

            //    db.SaveChanges();
            //    return Ok("Peer evaluators added successfully");
            //}


            [HttpGet]
            [Route("BySession/{sessionId}")]
            public IHttpActionResult GetPeerEvaluatorsBySession(int sessionId)
            {
                var evaluators = (from pe in db.PeerEvaluator
                                  join t in db.Teacher on pe.teacherID equals t.userID
                                  where pe.sessionID == sessionId
                                  select new
                                  {
                                      t.userID,
                                      t.name,
                                      t.department
                                  }).ToList();


                if (evaluators.Count == 0) return BadRequest("No Teacher Found ");

                return Ok(evaluators);
            }

        }
    }


