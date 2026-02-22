using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using EmpPerAppE.Models.DTO.studentevalution;

namespace EmpPerAppE.Controllers
{
    [RoutePrefix("api/Student")]
    public class StudentEvalutionController : ApiController
    {
        private fypempEntities db = new fypempEntities();

        public StudentEvalutionController()
        {
            // Inka false hona zaroori hai JSON serialization errors se bachne ke liye
            db.Configuration.ProxyCreationEnabled = false;
            db.Configuration.LazyLoadingEnabled = false;
        }

        // 1. GET: Enrolled courses for student
        [HttpGet]
        [Route("GetStudentEnrollments/{studentId}")]
        public IHttpActionResult GetStudentEnrollments(string studentId)
        {
            try
            {
                var data = db.Enrollment
                    .Where(e => e.studentID == studentId)
                    .Select(e => new
                    {
                        EnrollmentId = e.id,
                        CourseCode = e.courseCode,
                        CourseTitle = e.Course.title,
                        TeacherName = e.Teacher.name,
                        TeacherId = e.teacherID,
                        SessionName = e.Session.name,
                        IsEvaluated = db.StudentEvaluation.Any(se => se.enrollmentID == e.id)
                    })
                    .ToList();

                if (data.Count == 0)
                    return NotFound();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest("Error fetching courses: " + ex.Message);
            }
        }

        // 2. GET: Active Questionnaire (FIXED WITH .INCLUDE)
        [HttpGet]
        [Route("GetActiveQuestionnaire/{type}")]
        public IHttpActionResult GetActiveQuestionnaire(string type)
        {
            try
            {
                if (string.IsNullOrEmpty(type)) return BadRequest("Type is required");

                // .Include(q => q.Questions) lazmi hai jab LazyLoading band ho
                var questionnaire = db.Questionare
                    .Include(q => q.Questions)
                    .Where(q => q.flag == "1")
                    .ToList() // Memory mein layein taake Case-Insensitive comparison exact ho
                    .Where(q => q.type.Trim().Equals(type.Trim(), StringComparison.OrdinalIgnoreCase))
                    .Select(q => new
                    {
                        QuestionareID = q.id,
                        Type = q.type,
                        Flag = q.flag,
                        Questions = q.Questions.Select(ques => new
                        {
                            QuestionId = ques.QuestionID,
                            Text = ques.QuestionText
                        }).ToList()
                    })
                    .FirstOrDefault();

                if (questionnaire == null)
                    return Ok(new { Message = "No active questionnaire found for type: " + type, Questions = new List<object>() });

                return Ok(questionnaire);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // 3. POST: Submit Evaluation
        [HttpPost]
        [Route("SubmitEvaluation")]
        public IHttpActionResult SubmitEvaluation(EvaluationSubmissionDto model)
        {
            if (model == null || model.Answers == null || !model.Answers.Any())
                return BadRequest("Data is incomplete.");

            try
            {
                foreach (var ans in model.Answers)
                {
                    var evaluation = new StudentEvaluation
                    {
                        enrollmentID = model.EnrollmentId,
                        questionID = ans.QuestionId,
                        score = ans.Score
                    };
                    db.StudentEvaluation.Add(evaluation);
                }

                db.SaveChanges();
                return Ok(new { message = "Evaluation submitted successfully!" });
            }
            catch (Exception ex)
            {
                string innerMessage = ex.InnerException != null
                    ? ex.InnerException.InnerException?.Message ?? ex.InnerException.Message
                    : ex.Message;

                return BadRequest("Database Error: " + innerMessage);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { db.Dispose(); }
            base.Dispose(disposing);
        }
    }
}