using EmpPerAppE.Models;
using EmpPerAppE.Models.DTO.peerEvalution; // Ensure this is correct
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;

namespace EmpPerAppE.Controllers.Teacher
{
    [RoutePrefix("api/TeacherDashboard")]
    public class PeerEvalutionController : ApiController
    {
        // Use your actual DB Context name here
        private fypempEntities db = new fypempEntities();

        public PeerEvalutionController()
        {
            db.Configuration.ProxyCreationEnabled = false;
            db.Configuration.LazyLoadingEnabled = false;
        }

        // //1. Check Evaluator & Get SessionID(Updated for Session Logic)
        //[HttpGet]
        //[Route("IsEvaluator/{userId}")]
        //public IHttpActionResult IsEvaluator(string userId)
        //{
        //    try
        //    {
        //        // 1. Pehle check karein ke kya ye Teacher table mein 'Permanent' mark hai?
        //        var teacher = db.Teacher.FirstOrDefault(t => t.userID.Trim().ToLower() == userId.Trim().ToLower());

        //        // 2. Phir check karein ke kya ye PeerEvaluator table mein (session-based) add hai?
        //        var sessionEvaluator = db.PeerEvaluator.FirstOrDefault(e => e.teacherID.Trim().ToLower() == userId.Trim().ToLower());

        //        // Agar dono mein se kahin bhi mil jaye
        //        if (teacher != null && teacher.isPermanentEvaluator == 1)
        //        {
        //            return Ok(new
        //            {
        //                isEvaluator = true,
        //                isPermanent = true,
        //                sessionID = sessionEvaluator?.sessionID // Permanent ke liye ye null ho sakta hai
        //            });
        //        }

        //        if (sessionEvaluator != null)
        //        {
        //            return Ok(new
        //            {
        //                isEvaluator = true,
        //                isPermanent = false,
        //                sessionID = sessionEvaluator.sessionID
        //            });
        //        }

        //        return Ok(new { isEvaluator = false });
        //    }
        //    catch (Exception ex) { return InternalServerError(ex); }
        //}

        [HttpGet]
        [Route("IsEvaluator/{userId}/{targetUserId?}")]
        public IHttpActionResult IsEvaluator(string userId, string targetUserId = null)
        {
            try
            {
                // 1. Evaluator aur Evaluatee (Target) ka data nikalain
                var evaluator = db.Teacher.FirstOrDefault(t => t.userID.Trim().ToLower() == userId.Trim().ToLower());

                if (evaluator == null) return Ok(new { isEvaluator = false, message = "User not found" });

                // 2. Designation Levels Define Karein (Rank System)
                Dictionary<string, int> ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "professor", 3 },
            { "assistant professor", 2 },
            { "senior lecturer", 1 },
            { "junior lecturer", 0 }
        };

                // Current evaluator ka rank nikalain
                int evaluatorRank = ranks.ContainsKey(evaluator.designation) ? ranks[evaluator.designation] : -1;

                // 3. Hierarchy Check (Agar targetUserId provide kiya gaya hai)
                if (!string.IsNullOrEmpty(targetUserId))
                {
                    var target = db.Teacher.FirstOrDefault(t => t.userID.Trim().ToLower() == targetUserId.Trim().ToLower());
                    if (target != null)
                    {
                        int targetRank = ranks.ContainsKey(target.designation) ? ranks[target.designation] : -1;

                        // Hierarchy Rule: Assistant Professor (Rank 2) Professor (Rank 3) ki evaluation nahi kar sakta
                        if (evaluatorRank < targetRank)
                        {
                            return Ok(new
                            {
                                isEvaluator = false,
                                message = "Hierarchy Violation: Juniors cannot evaluate Seniors."
                            });
                        }
                    }
                }

               // 4.Baki existing logic(Permanent or Session-based)
        var sessionEvaluator = db.PeerEvaluator.FirstOrDefault(e => e.teacherID.Trim().ToLower() == userId.Trim().ToLower());

                if (evaluator.isPermanentEvaluator == 1)
                {
                    return Ok(new
                    {
                        isEvaluator = true,
                        isPermanent = true,
                        designation = evaluator.designation,
                        sessionID = sessionEvaluator?.sessionID
                    });
                }

                if (sessionEvaluator != null)
                {
                    return Ok(new
                    {
                        isEvaluator = true,
                        isPermanent = false,
                        designation = evaluator.designation,
                        sessionID = sessionEvaluator.sessionID
                    });
                }

                return Ok(new { isEvaluator = false });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 2. Get Questionnaire (Type 'P' or 'T' from frontend)
        [HttpGet]
        [Route("GetActiveQuestionnaire/{type}")]
        public IHttpActionResult GetActiveQuestionnaire(string type)
        {
            try
            {
                var questionnaire = db.Questionare
                    .Where(q => q.flag == "1" && q.type.Trim().ToLower() == type.Trim().ToLower())
                    .Select(q => new
                    {
                        QuestionareID = q.id,
                        Questions = db.Questions
                            .Where(ques => ques.QuestionareID == q.id)
                            .Select(ques => new { ques.QuestionID, ques.QuestionText })
                            .ToList()
                    }).FirstOrDefault();

                if (questionnaire == null) return Ok(new { Message = "No active questionnaire found" });
                return Ok(questionnaire);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet]
        [Route("GetTeachersWithCourses/{userId}")]
        public IHttpActionResult GetTeachersWithCourses(string userId)
        {
            try
            {
                var teacherProfile = db.Teacher.FirstOrDefault(t => t.userID.Trim().ToLower() == userId.Trim().ToLower());
                if (teacherProfile == null) return BadRequest("Teacher record not found.");

                IQueryable<Enrollment> enrollmentQuery = db.Enrollment;

                // Session filtering for non-permanent evaluators
                if (teacherProfile.isPermanentEvaluator != 1)
                {
                    var sessionAuth = db.PeerEvaluator.FirstOrDefault(pe => pe.teacherID.Trim().ToLower() == userId.Trim().ToLower());
                    if (sessionAuth == null) return Ok(new List<object>());
                    enrollmentQuery = enrollmentQuery.Where(e => e.sessionID == sessionAuth.sessionID);
                }

                var data = enrollmentQuery
                    .GroupBy(e => e.teacherID)
                    .Select(g => new
                    {
                        TeacherID = g.Key,
                        TeacherName = db.Teacher.Where(t => t.userID == g.Key).Select(t => t.name).FirstOrDefault(),
                        Designation = db.Teacher.Where(t => t.userID == g.Key).Select(t => t.designation).FirstOrDefault(), // Rank check ke liye
                        SessionID = g.FirstOrDefault().sessionID,
                        Courses = g.Select(x => x.courseCode).Distinct().ToList()
                    }).ToList();

                return Ok(data);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
        // 4. Submit Evaluation (Using PeerSubmissionModel DTO)
        [HttpPost]
        [Route("SubmitEvaluation")]
        public IHttpActionResult SubmitEvaluation([FromBody] PeerSubmissionModel model)
        {
            if (model == null || model.Answers == null) return BadRequest("Invalid Data");

            try
            {
                // 1. Check karein ke kya user PeerEvaluator table mein hai?
                var sessionEvaluator = db.PeerEvaluator
                    .FirstOrDefault(pe => pe.teacherID.Trim().ToLower() == model.EvaluatorUserId.Trim().ToLower());

                // 2. Check karein ke kya user Teacher table mein Permanent hai?
                var permanentTeacher = db.Teacher
                    .FirstOrDefault(t => t.userID.Trim().ToLower() == model.EvaluatorUserId.Trim().ToLower()
                                    && t.isPermanentEvaluator == 1);

                // Agar dono jagah record nahi milta, toh hi error dein
                if (sessionEvaluator == null && permanentTeacher == null)
                {
                    return BadRequest("Unauthorized: You are not an assigned evaluator.");
                }

                foreach (var ans in model.Answers)
                {
                    db.PeerEvaluation.Add(new PeerEvaluation
                    {
                        // Agar session-based hai toh uski ID lagayein, 
                        // Agar Permanent hai toh yahan logic dekhein (evaluatorID agar null allow karta hai toh null bhejain)
                        evaluatorID = sessionEvaluator?.id,
                        evaluateeID = model.EvaluateeId,
                        questionID = ans.QuestionId,
                        courseCode = model.CourseCode,
                        score = ans.Score
                    });
                }

                db.SaveChanges();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
        // 5. Get History
        [HttpGet]
        [Route("GetSubmittedEvaluations/{userId}")]
        public IHttpActionResult GetSubmittedEvaluations(string userId)
        {
            try
            {
                var evaluator = db.PeerEvaluator.FirstOrDefault(e => e.teacherID == userId);
                if (evaluator == null) return Ok(new List<object>());

                var submitted = db.PeerEvaluation
                    .Where(p => p.evaluatorID == evaluator.id)
                    .Select(p => new { TeacherID = p.evaluateeID, CourseCode = p.courseCode })
                    .Distinct().ToList();

                return Ok(submitted);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}