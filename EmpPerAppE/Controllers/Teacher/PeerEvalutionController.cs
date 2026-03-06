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
                // 1. Pehle check karein ke kya ye Teacher table mein 'Permanent' mark hai?
                var teacher = db.Teacher.FirstOrDefault(t => t.userID.Trim().ToLower() == userId.Trim().ToLower());

                // 2. Phir check karein ke kya ye PeerEvaluator table mein (session-based) add hai?
                var sessionEvaluator = db.PeerEvaluator.FirstOrDefault(e => e.teacherID.Trim().ToLower() == userId.Trim().ToLower());

                // Agar dono mein se kahin bhi mil jaye
                if (teacher != null && teacher.isPermanentEvaluator == 1)
                {
                    return Ok(new
                    {
                        isEvaluator = true,
                        isPermanent = true,
                        sessionID = sessionEvaluator?.sessionID // Permanent ke liye ye null ho sakta hai
                    });
                }

                if (sessionEvaluator != null)
                {
                    return Ok(new
                    {
                        isEvaluator = true,
                        isPermanent = false,
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
                // 1. Database se check karein ke login user 'Permanent Evaluator' hai ya nahi
                var teacherProfile = db.Teacher.FirstOrDefault(t => t.userID.Trim().ToLower() == userId.Trim().ToLower());

                // Agar teacher record database mein nahi milta
                if (teacherProfile == null) return BadRequest("Teacher record not found.");

                IQueryable<Enrollment> enrollmentQuery = db.Enrollment;

                // 2. Agar user Permanent nahi hai (isPermanentEvaluator == 0)
                if (teacherProfile.isPermanentEvaluator != 1)
                {
                    // Check karein ke kya HOD ne ise kisi session ke liye 'PeerEvaluator' table mein add kiya hai?
                    var sessionAuth = db.PeerEvaluator
                        .FirstOrDefault(pe => pe.teacherID.Trim().ToLower() == userId.Trim().ToLower());

                    // Agar user na Permanent hai aur na hi kisi session ke liye authorize hai
                    if (sessionAuth == null)
                    {
                        // Access deny karein kyunke user evaluator nahi hai
                        return Ok(new List<object>());
                    }

                    // Agar session evaluator hai, to sirf us session ke teachers ka data filter karein
                    enrollmentQuery = enrollmentQuery.Where(e => e.sessionID == sessionAuth.sessionID);
                }

                // 3. Agar user 'Permanent Evaluator' hai (isPermanentEvaluator == 1)
                // To query filter nahi hogi aur tamam sessions ke teachers load honge.

                var data = enrollmentQuery
                    .GroupBy(e => e.teacherID)
                    .Select(g => new
                    {
                        TeacherID = g.Key,
                        TeacherName = db.Teacher
                            .Where(t => t.userID == g.Key)
                            .Select(t => t.name)
                            .FirstOrDefault(),
                        // Frontend ke liye session ID bhi sath bhej rahe hain
                        SessionID = g.FirstOrDefault().sessionID,
                        Courses = g.Select(x => x.courseCode).Distinct().ToList()
                    }).ToList();

                return Ok(data);
            }
            catch (Exception ex)
            {
                // Kisi bhi error ki surat mein internal server error return karein
                return InternalServerError(ex);
            }
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