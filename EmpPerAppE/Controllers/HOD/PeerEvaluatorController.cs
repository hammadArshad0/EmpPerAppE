using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using EmpPerAppE.Models.DTO.peerEvalution;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/peerevaluator")]
    public class PeerEvaluatorController : ApiController
    {
        fypempEntities db = new fypempEntities();

        // 1. Get All Teachers for Selection
        [HttpGet]
        [Route("Teachers")]
        public IHttpActionResult GetTeachers()
        {
            var teachers = db.Teacher.Select(t => new { t.userID, t.name, t.department }).ToList();
            return Ok(teachers);
        }

        // 2. Get Sessions
        [HttpGet]
        [Route("Sessions")]
        public IHttpActionResult GetSessions()
        {
            var sessions = db.Session.Select(s => new { s.id, s.name }).ToList();
            return Ok(sessions);
        }

        // 3. Add Evaluators to a Specific Session
        [HttpPost]
        [Route("Add")]
        public IHttpActionResult AddPeerEvaluators(AddPeerEvaluatorDto model)
        {
            if (model == null || model.TeacherIds == null || model.SessionId <= 0)
                return BadRequest("Invalid data: Please select session and teachers.");

            try
            {
                foreach (var tid in model.TeacherIds)
                {
                    string tidStr = tid.ToString();

                    // Check if already exists in this session to avoid duplicates
                    bool exists = db.PeerEvaluator.Any(pe => pe.teacherID == tidStr && pe.sessionID == model.SessionId);

                    if (!exists)
                    {
                        // Use full model path to avoid 'Namespace but used like Type' error
                        db.PeerEvaluator.Add(new EmpPerAppE.Models.PeerEvaluator
                        {
                            teacherID = tidStr,
                            sessionID = model.SessionId
                        });
                    }
                }
                db.SaveChanges();
                return Ok(new { message = "Evaluators assigned successfully for the selected session." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 4. Get Current Evaluators for a Session (Listing & Delete purpose)
        [HttpGet]
        [Route("BySession/{sessionId}")]
        public IHttpActionResult GetPeerEvaluatorsBySession(int sessionId)
        {
            var evaluators = (from pe in db.PeerEvaluator
                              join t in db.Teacher on pe.teacherID equals t.userID
                              where pe.sessionID == sessionId
                              select new
                              {
                                  assignmentId = pe.id, // Primary Key of PeerEvaluator table
                                  t.userID,
                                  t.name,
                                  t.department
                              }).ToList();

            return Ok(evaluators);
        }

        // 5. Delete One by One
        [HttpDelete]
        [Route("delete/{id}")]
        public IHttpActionResult Delete(int id)
        {
            var pe = db.PeerEvaluator.Find(id);
            if (pe == null) return NotFound();

            db.PeerEvaluator.Remove(pe);
            db.SaveChanges();
            return Ok(new { message = "Evaluator removed from this session." });
        }
    }


}