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

        // 1. Check Evaluator & Get SessionID (Updated for Session Logic)
        [HttpGet]
        [Route("IsEvaluator/{userId}")]
        public IHttpActionResult IsEvaluator(string userId)
        {
            try
            {
                var evaluator = db.PeerEvaluator
                    .FirstOrDefault(e => e.teacherID.Trim().ToLower() == userId.Trim().ToLower());

                if (evaluator == null)
                    return Ok(new { isEvaluator = false });

                return Ok(new
                {
                    isEvaluator = true,
                    sessionID = evaluator.sessionID // Frontend need this to fetch teachers
                });
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

        // 3. Get Colleagues (Filtered by Session via Enrollment)
        [HttpGet]
        [Route("GetTeachersWithCourses/{sessionId}")]
        public IHttpActionResult GetTeachersWithCourses(int sessionId)
        {
            try
            {
                var data = db.Enrollment
                    .Where(e => e.sessionID == sessionId) // Only teachers in this session
                    .GroupBy(e => e.teacherID)
                    .Select(g => new
                    {
                        TeacherID = g.Key,
                        TeacherName = db.Teacher
                            .Where(t => t.userID == g.Key)
                            .Select(t => t.name)
                            .FirstOrDefault(),
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
                // Find INT id for PeerEvaluator table
                var evaluatorRecord = db.PeerEvaluator
                    .FirstOrDefault(pe => pe.teacherID.Trim().ToLower() == model.EvaluatorUserId.Trim().ToLower());

                if (evaluatorRecord == null) return BadRequest("Evaluator record not found.");

                foreach (var ans in model.Answers)
                {
                    db.PeerEvaluation.Add(new PeerEvaluation
                    {
                        evaluatorID = evaluatorRecord.id, // Primary key INT
                        evaluateeID = model.EvaluateeId,   // String ID (e.g. T002)
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