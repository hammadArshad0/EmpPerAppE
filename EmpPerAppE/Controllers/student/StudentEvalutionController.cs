using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Http;
using EmpPerAppE.Models.DTO.studentevalution;

namespace EmpPerAppE.Controllers
{
    [RoutePrefix("api/StudentEvaluation")]
    public class StudentEvalutionController : ApiController
    {
        private fypempEntities db = new fypempEntities();

        public StudentEvalutionController()
        {
            db.Configuration.ProxyCreationEnabled = false;
            db.Configuration.LazyLoadingEnabled = false;
        }

        // 1. GET: Student ke enrolled courses dikhane ke liye
        [HttpGet]
        [Route("GetCourses/{studentId}")]
        public IHttpActionResult GetCourses(string studentId)
        {
            try
            {
                var data = db.Enrollment
                    .Where(e => e.studentID == studentId)
                    .Select(e => new
                    {
                        studentName = e.Student.name,
                        EnrollmentId = e.id,
                        CourseCode = e.courseCode,
                        CourseTitle = e.Course.title,
                        TeacherName = e.Teacher.name,
                        TeacherId = e.teacherID,
                        // Logic: Agar is enrollment ID ke against pehle se evaluation exist karti hai toh status change kar dein
                        IsEvaluated = db.StudentEvaluation.Any(se => se.enrollmentID == e.id)
                    })
                    .ToList();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

  
[HttpGet]
    [Route("GetEvaluationForm")]
    public IHttpActionResult GetEvaluationForm()
    {
        try
        {
            // 1. Database se Questionnaire aur uske sawalat sath hi load karein
            var questionnaire = db.Questionare
                .Include(q => q.Questions) // Yeh sawalat ko khich kar layega
                .Where(q => q.type == "S" && q.flag == "1")
                .FirstOrDefault();

            // 2. Agar null hai ya sawalat khali hain toh error handle karein
            if (questionnaire == null)
                return BadRequest("Active Student Questionnaire not found.");

            if (questionnaire.Questions == null || !questionnaire.Questions.Any())
                return BadRequest("Questionnaire found but it has no questions.");

            // 3. Simple JSON object return karein
            var response = new
            {
                QuestionnaireId = questionnaire.id,
                Questions = questionnaire.Questions.Select(qq => new
                {
                    QuestionId = qq.QuestionID,
                    Text = qq.QuestionText
                }).ToList()
            };

            return Ok(response);
        }
        catch (Exception ex)
        {
            // Exception details check karne ke liye
            return BadRequest("Server Error: " + ex.Message);
        }
    }

    // 3. POST: Evaluation Submit karne ke liye
    [HttpPost]
        [Route("SubmitEvaluation")]
        public IHttpActionResult SubmitEvaluation(EvaluationSubmissionDto model)
        {
            if (model == null || model.Answers == null)
                return BadRequest("Invalid submission data.");

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
                return BadRequest("Error saving evaluation: " + ex.Message);
            }
        }
    }

    
}