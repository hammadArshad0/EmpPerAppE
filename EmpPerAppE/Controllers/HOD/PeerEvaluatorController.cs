using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Data.Entity; // Include for .Include()
using EmpPerAppE.Models.DTO.peerEvalution;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/peerevaluator")]
    public class PeerEvaluatorController : ApiController
    {
        fypempEntities db = new fypempEntities();

        // 1. Selection List: Sirf wo teachers jo permanent NAHI hain
        [HttpGet]
        [Route("Teachers")]
        public IHttpActionResult GetTeachers()
        {
            // Permanent teachers ko list se nikal diya kyunke wo automatically assigned hain
            var teachers = db.Teacher
                .Where(t => t.isPermanentEvaluator != 1)
                .Select(t => new { t.userID, t.name, t.department })
                .ToList();
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

        // 3. Add Evaluators (Manual)
        [HttpPost]
        [Route("Add")]
        public IHttpActionResult AddPeerEvaluators(AddPeerEvaluatorDto model)
        {
            if (model == null || model.TeacherIds == null || model.SessionId <= 0)
                return BadRequest("Invalid data.");

            try
            {
                foreach (var tid in model.TeacherIds)
                {
                    string tidStr = tid.ToString();
                    bool exists = db.PeerEvaluator.Any(pe => pe.teacherID == tidStr && pe.sessionID == model.SessionId);

                    if (!exists)
                    {
                        db.PeerEvaluator.Add(new EmpPerAppE.Models.PeerEvaluator
                        {
                            teacherID = tidStr,
                            sessionID = model.SessionId
                        });
                    }
                }
                db.SaveChanges();
                return Ok(new { message = "Evaluators assigned successfully." });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 4. Get Current Evaluators for a Session 
        // Logic: Ismein session-specific teachers + saare Permanent teachers nazar aayenge
        [HttpGet]
        [Route("BySession/{sessionId}")]
        public IHttpActionResult GetPeerEvaluatorsBySession(int sessionId)
        {
            // A. Get Manual Evaluators from PeerEvaluator table for this session
            var manualEvaluators = (from pe in db.PeerEvaluator
                                    join t in db.Teacher on pe.teacherID equals t.userID
                                    where pe.sessionID == sessionId
                                    select new
                                    {
                                        assignmentId = pe.id,
                                        t.userID,
                                        t.name,
                                        t.department,
                                        isPermanent = t.isPermanentEvaluator == 1
                                    }).ToList();

            // B. Get All Permanent Teachers (Kyunke wo har session mein hote hain)
            var permanentTeachers = db.Teacher
                .Where(t => t.isPermanentEvaluator == 1)
                .Select(t => new
                {
                    assignmentId = 0, // 0 because they are not coming from PeerEvaluator table
                    t.userID,
                    t.name,
                    t.department,
                    isPermanent = true
                }).ToList();

            // C. Combine and remove duplicates (if any permanent was manually added)
            var finalList = permanentTeachers
                .Concat(manualEvaluators.Where(m => !m.isPermanent))
                .OrderByDescending(x => x.isPermanent)
                .ThenBy(x => x.name)
                .ToList();

            return Ok(finalList);
        }

        // 5. Delete (With Protection)
        [HttpDelete]
        [Route("delete/{id}")]
        public IHttpActionResult Delete(int id)
        {
            // Agar assignmentId 0 hai (Permanent), toh delete block karein
            if (id == 0) return BadRequest("Permanent evaluators cannot be deleted.");

            var pe = db.PeerEvaluator.Include(p => p.Teacher).FirstOrDefault(x => x.id == id);
            if (pe == null) return NotFound();

            // Double check if teacher is permanent
            if (pe.Teacher != null && pe.Teacher.isPermanentEvaluator == 1)
            {
                return BadRequest("Action Denied: This is a permanent evaluator.");
            }

            db.PeerEvaluator.Remove(pe);
            db.SaveChanges();
            return Ok(new { message = "Evaluator removed successfully." });
        }

        // 6. Permanent Teachers for Card View
        [HttpGet]
        [Route("PermanentTeachers")]
        public IHttpActionResult GetPermanentTeachers()
        {
            var permanentOnes = db.Teacher
                .Where(t => t.isPermanentEvaluator == 1)
                .Select(t => new { t.userID, t.name, t.department, Role = "Permanent Evaluator" })
                .ToList();
            return Ok(permanentOnes);
        }

        // 7. Toggle Permanent Status (Add or Remove from Permanent list)
        // Isay Add aur Edit dono ke liye use kiya ja sakta hai
        [HttpPost]
        [Route("TogglePermanent")]
        public IHttpActionResult TogglePermanentStatus(TogglePermanentDto model)
        {
            if (model == null || string.IsNullOrEmpty(model.UserID))
                return BadRequest("Invalid user data.");

            try
            {
                var teacher = db.Teacher.FirstOrDefault(t => t.userID == model.UserID);
                if (teacher == null) return NotFound();

                // Status update karein: 1 for Permanent, 0 for Normal
                teacher.isPermanentEvaluator = model.IsPermanent ? 1 : 0;

                // Agar kisi ko Permanent banaya hai, toh usey PeerEvaluator table (manual list) se hatayein
                // Kyunke wo ab globally available hoga
                if (model.IsPermanent)
                {
                    var manualAssignments = db.PeerEvaluator.Where(pe => pe.teacherID == model.UserID).ToList();
                    if (manualAssignments.Any())
                    {
                        db.PeerEvaluator.RemoveRange(manualAssignments);
                    }
                }

                db.SaveChanges();

                string status = model.IsPermanent ? "Permanent" : "Normal";
                return Ok(new { message = $"Teacher marked as {status} evaluator successfully." });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 8. Bulk Permanent Assignment (Extra: Multiple teachers ke liye)
        [HttpPost]
        [Route("SetBulkPermanent")]
        public IHttpActionResult SetBulkPermanent(List<string> userIDs)
        {
            if (userIDs == null || !userIDs.Any()) return BadRequest("No User IDs provided.");

            try
            {
                var teachers = db.Teacher.Where(t => userIDs.Contains(t.userID)).ToList();
                foreach (var t in teachers)
                {
                    t.isPermanentEvaluator = 1;
                }
                db.SaveChanges();
                return Ok(new { message = "Selected teachers are now Permanent Evaluators." });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}