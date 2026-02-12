using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using EmpPerAppE.Models.DTO.peerEvalution;

namespace EmpPerAppE.Controllers.Teacher
{
    [RoutePrefix("api/Peer")]
    public class PeerEvalutionController : ApiController
    {
        fypempEntities db = new fypempEntities();

        public PeerEvalutionController()
        {
            db.Configuration.ProxyCreationEnabled = false;
            db.Configuration.LazyLoadingEnabled = false;
        }

        // --- STEP 1: LOGIN & ACCESS CHECK ---
        // Ab hum current session nahi uthayenge, balkay check karenge ke ye teacher
        // kisi bhi active/latest session mein Peer Evaluator hai ya nahi.
        [HttpGet]
        [Route("CheckPeerStatus/{userId}")]
        public IHttpActionResult CheckPeerStatus(string userId)
        {
            // Sabse pehle wo session dhoondein jahan ye teacher Peer Evaluator hai
            // OrderByDescending isliye taake agar multiple sessions mein ho toh latest wala milay
            var assignment = db.PeerEvaluator
                               .Where(p => p.teacherID == userId)
                               .OrderByDescending(p => p.sessionID)
                               .FirstOrDefault();

            if (assignment == null)
            {
                return Ok(new { isPeerEvaluator = false, message = "You are not assigned as a Peer Evaluator." });
            }

            return Ok(new
            {
                isPeerEvaluator = true,
                sessionId = assignment.sessionID,
                assignmentId = assignment.id
            });
        }

        // --- STEP 2: GET TEACHERS LIST ---
        [HttpGet]
        [Route("GetTeachersToEvaluate/{evaluatorUserId}")]
        public IHttpActionResult GetTeachersToEvaluate(string evaluatorUserId)
        {
            try
            {
                // 1. Teacher ki latest assignment uthayen
                var assignment = db.PeerEvaluator
                                   .Where(p => p.teacherID == evaluatorUserId)
                                   .OrderByDescending(p => p.sessionID)
                                   .FirstOrDefault();

                if (assignment == null) return Unauthorized();

                int currentSessionId = (int)assignment.sessionID;

                // 2. Usi SPECIFIC Session ke teachers Enrollment se layen
                var teachersList = db.Enrollment
                    .Where(e => e.sessionID == currentSessionId && e.teacherID != evaluatorUserId)
                    .Select(e => new
                    {
                        TeacherID = e.teacherID,
                        TeacherName = e.Teacher.name,
                        CourseCode = e.courseCode,
                        CourseTitle = e.Course.title,
                        // Status Check: Kya is evaluator ne is teacher ko is course mein evaluate kar liya hai?
                        IsEvaluated = db.PeerEvaluation.Any(pe =>
                            pe.evaluatorID == assignment.id &&
                            pe.evaluateeID == e.teacherID &&
                            pe.courseCode == e.courseCode)
                    })
                    .Distinct()
                    .ToList();

                return Ok(teachersList);
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error fetching colleagues: " + ex.Message));
            }
        }

        // --- STEP 3: GET PEER QUESTIONS ---
        [HttpGet]
        [Route("GetPeerQuestions")]
        public IHttpActionResult GetPeerQuestions()
        {
            var questions = db.Questions
                .Where(q => q.Questionare.type == "Peer" && q.Questionare.flag == "1")
                .Select(q => new { q.QuestionID, q.QuestionText })
                .ToList();

            return Ok(questions);
        }

        // --- STEP 4: SUBMIT DATA ---
        [HttpPost]
        [Route("SubmitPeerResults")]
        public IHttpActionResult SubmitPeerResults(PeerSubmissionModel model)
        {
            if (model == null || model.Answers == null) return BadRequest("Invalid Data");

            try
            {
                // Teacher ki assignment ID nikaalna zaroori hai foreign key ke liye
                var assignment = db.PeerEvaluator
                                   .Where(p => p.teacherID == model.EvaluatorUserId)
                                   .OrderByDescending(p => p.sessionID)
                                   .FirstOrDefault();

                if (assignment == null) return BadRequest("Unauthorized Evaluator.");

                foreach (var ans in model.Answers)
                {
                    EmpPerAppE.Models.PeerEvaluation pe = new EmpPerAppE.Models.PeerEvaluation
                    {
                        evaluatorID = assignment.id,
                        evaluateeID = model.EvaluateeId,
                        questionID = ans.QuestionId,
                        courseCode = model.CourseCode,
                        score = ans.Score
                    };
                    db.PeerEvaluation.Add(pe);
                }

                db.SaveChanges();
                return Ok(new { Message = "Peer Evaluation Submitted Successfully!" });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
    }

    
    
}