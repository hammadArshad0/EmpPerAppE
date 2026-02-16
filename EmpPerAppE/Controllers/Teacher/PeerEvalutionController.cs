using EmpPerAppE.Models;
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
        private fypempEntities db = new fypempEntities();

        public PeerEvalutionController()
        {
            db.Configuration.ProxyCreationEnabled = false;
            db.Configuration.LazyLoadingEnabled = false;
        }

        // 1. FIXED: userId changed from int to string
        [HttpGet]
        [Route("IsEvaluator/{userId}")]
        public IHttpActionResult IsEvaluator(string userId)
        {
            try
            {
                bool exists = db.PeerEvaluator.Any(e => e.teacherID == userId);
                return Ok(new { isEvaluator = exists });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 2. FIXED: evaluatorID changed from int to string to accept "T002"
        [HttpGet]
        [Route("GetSubmittedEvaluations/{evaluatorID}")]
        public IHttpActionResult GetSubmittedEvaluations(string evaluatorID)
        {
            try
            {
                // Note: evaluatorID should be compared as string if it's alphanumeric
                var submitted = db.PeerEvaluation
                    .Where(p => p.evaluatorID.ToString() == evaluatorID)
                    .Select(p => new
                    {
                        TeacherID = p.evaluateeID,
                        CourseCode = p.courseCode
                    })
                    .Distinct()
                    .ToList();
                return Ok(submitted);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet]
        [Route("GetTeachersWithCourses")]
        public IHttpActionResult GetTeachersWithCourses()
        {
            try
            {
                var data = db.Enrollment
                    .GroupBy(e => e.teacherID)
                    .Select(g => new
                    {
                        TeacherID = g.Key,
                        TeacherName = db.Teacher.Where(t => t.userID == g.Key).Select(t => t.name).FirstOrDefault(),
                        Courses = g.Select(x => x.courseCode).Distinct().ToList()
                    })
                    .ToList();
                return Ok(data);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpGet]
        [Route("GetActiveQuestionnaire/{type}")]
        public IHttpActionResult GetActiveQuestionnaire(string type)
        {
            try
            {
                var questionnaire = db.Questionare
                    .Include(q => q.Questions)
                    .Where(q => q.flag == "1" && q.type == type)
                    .Select(q => new
                    {
                        QuestionareID = q.id,
                        Type = q.type,
                        Flag = q.flag,
                        Questions = q.Questions.Select(ques => new
                        {
                            ques.QuestionID,
                            ques.QuestionText
                        }).ToList()
                    })
                    .FirstOrDefault();

                if (questionnaire == null)
                    return Ok(new { Message = "No active questionnaire found" });

                return Ok(questionnaire);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        [HttpPost]
        [Route("SubmitEvaluation")]
        public IHttpActionResult SubmitEvaluation([FromBody] List<PeerEvaluation> evaluations)
        {
            if (evaluations == null || !evaluations.Any())
                return BadRequest("Invalid submission");

            try
            {
                foreach (var eval in evaluations)
                {
                    db.PeerEvaluation.Add(new PeerEvaluation
                    {
                        evaluatorID = eval.evaluatorID,
                        evaluateeID = eval.evaluateeID,
                        questionID = eval.questionID,
                        courseCode = eval.courseCode,
                        score = eval.score
                    });
                }
                db.SaveChanges();
                return Ok(new { success = true });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}