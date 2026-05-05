using EmpPerAppE.Models;
using EmpPerAppE.Models.DTO.peerEvalution;
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

        // Helper: Designation Rank logic (Common for all methods)
        private int GetDesignationRank(string designation)
        {
            if (string.IsNullOrWhiteSpace(designation)) return 0;

            switch (designation.Trim().ToLower())
            {
                case "professor": return 4;
                case "assistant professor": return 3;
                case "senior lecturer": return 2;
                case "lecturer": return 1;
                case "junior lecturer": return 1;
                case "teacher": return 1;
                default: return 0;
            }
        }

        // 1. Check Evaluator & Hierarchy
        [HttpGet]
        [Route("IsEvaluator/{userId}/{targetUserId?}")]
        public IHttpActionResult IsEvaluator(string userId, string targetUserId = null)
        {
            try
            {
                string normId = userId.Trim().ToLower();
                var evaluator = db.Teacher.FirstOrDefault(t => t.userID.Trim().ToLower() == normId);
                if (evaluator == null) return Ok(new { isEvaluator = false, message = "User not found" });

                var sessionEvaluator = db.PeerEvaluator.FirstOrDefault(e => e.teacherID.Trim().ToLower() == normId);
                bool isPerm = evaluator.isPermanentEvaluator == 1;

                if (!isPerm && sessionEvaluator == null)
                    return Ok(new { isEvaluator = false });

                // Hierarchy Check
                if (!string.IsNullOrEmpty(targetUserId))
                {
                    var target = db.Teacher.FirstOrDefault(t => t.userID.Trim().ToLower() == targetUserId.Trim().ToLower());
                    if (target != null)
                    {
                        if (GetDesignationRank(evaluator.designation) < GetDesignationRank(target.designation))
                        {
                            return Ok(new { isEvaluator = false, message = "Hierarchy Violation: Juniors cannot evaluate Seniors." });
                        }
                    }
                }

                return Ok(new
                {
                    isEvaluator = true,
                    isPermanent = isPerm,
                    designation = evaluator.designation,
                    sessionID = sessionEvaluator?.sessionID
                });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 2. Get Teachers list based on Rank
        [HttpGet]
        [Route("GetTeachersWithCourses")]
        public IHttpActionResult GetTeachersWithCourses(string userId)
        {
            try
            {
                string normId = userId.Trim().ToLower();

                // 1. Current Teacher (Evaluator) ki details aur rank nikalna
                var currentTeacher = db.Teacher.FirstOrDefault(t => t.userID.Trim().ToLower() == normId);
                if (currentTeacher == null) return Ok(new List<object>());

                int currentRank = GetDesignationRank(currentTeacher.designation);

                // 2. Query: Enrollment table ko use karte hue Teachers aur unke Courses fetch karna
                var teachersData = db.Enrollment
                    .GroupBy(e => e.teacherID)
                    .Select(g => new
                    {
                        TeacherID = g.Key,
                        // Teacher info fetch karna including designation and department
                        TeacherDetails = db.Teacher.Where(t => t.userID == g.Key)
                                                  .Select(t => new {
                                                      t.name,
                                                      t.designation,
                                                      // Agar Teacher table mein DepartmentName hai to wo use karein
                                                      Department = t.department
                                                  }).FirstOrDefault(),
                        Courses = g.Select(x => x.courseCode).Distinct().ToList()
                    }).ToList();

                // 3. Filtering Logic: Rank comparison aur Self-filter
                var filtered = teachersData
                    .Where(t => t.TeacherDetails != null &&
                                t.TeacherID.Trim().ToLower() != normId &&
                                GetDesignationRank(t.TeacherDetails.designation) <= currentRank)
                    .Select(t => new {
                        t.TeacherID,
                        TeacherName = t.TeacherDetails.name,
                        Designation = t.TeacherDetails.designation, // Front-end badge ke liye
                        Department = t.TeacherDetails.Department ?? "Faculty", // Front-end badge ke liye
                        Courses = t.Courses
                    }).ToList();

                return Ok(filtered);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }
        // 3. Get Active Questionnaire
        [HttpGet]
        [Route("GetActiveQuestionnaire/{type}")]
        public IHttpActionResult GetActiveQuestionnaire(string type)
        {
            try
            {
                string normType = type.Trim().ToLower();
                var questionnaire = db.Questionare
                    .Where(q => q.flag == "1" && q.type.Trim().ToLower() == normType)
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

        // 4. Submit Evaluation (Handling both Permanent and Session Evaluators)
        [HttpPost]
        [Route("SubmitEvaluation")]
        public IHttpActionResult SubmitEvaluation([FromBody] PeerSubmissionModel model)
        {
            if (model == null || model.Answers == null)
                return BadRequest("Invalid Data");

            try
            {
                string normEvalId = model.EvaluatorUserId.Trim().ToLower();

                var teacher = db.Teacher
                    .FirstOrDefault(t => t.userID.Trim().ToLower() == normEvalId);
                bool isPerm = teacher?.isPermanentEvaluator == 1;

                // PeerEvaluator table mein entry dhundo
                var sessionEvaluator = db.PeerEvaluator
                    .FirstOrDefault(pe => pe.teacherID.Trim().ToLower() == normEvalId);

                if (!isPerm && sessionEvaluator == null)
                    return BadRequest("Unauthorized: Not an assigned evaluator.");

                // ── KEY FIX ──
                // Permanent hai lekin PeerEvaluator table mein entry nahi
                // → pehle entry banao, phir id use karo
                if (sessionEvaluator == null)
                {
                    var activeSession = db.Session
                        .OrderByDescending(s => s.id)
                        .FirstOrDefault();

                    sessionEvaluator = new PeerEvaluator
                    {
                        teacherID = teacher.userID,
                        sessionID = activeSession?.id
                    };
                    db.PeerEvaluator.Add(sessionEvaluator);
                    db.SaveChanges(); // ← pehle save karo taake id mile
                }

                // Ab evaluatorID hamesha valid hoga — NULL kabhi nahi aayega
                foreach (var ans in model.Answers)
                {
                    db.PeerEvaluation.Add(new PeerEvaluation
                    {
                        evaluatorID = sessionEvaluator.id,  // ✅ guaranteed value
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

        // 5. Get History (Crucial Fix for Permanent Evaluators)
        [HttpGet]
        [Route("GetSubmittedEvaluations/{userId}")]
        public IHttpActionResult GetSubmittedEvaluations(string userId)
        {
            try
            {
                string normId = userId.Trim().ToLower();
                var sessionEval = db.PeerEvaluator.FirstOrDefault(e => e.teacherID.Trim().ToLower() == normId);

                // Agar session evaluator hai toh uski ID se filter karein
                if (sessionEval != null)
                {
                    var history = db.PeerEvaluation
                        .Where(p => p.evaluatorID == sessionEval.id)
                        .Select(p => new { TeacherID = p.evaluateeID, CourseCode = p.courseCode })
                        .Distinct().ToList();
                    return Ok(history);
                }

                // Agar Permanent Evaluator hai (PeerEvaluator table mein entry nahi hai)
                // Toh hume PeerEvaluation table mein check karna hoga. 
                // NOTE: Agar aapke PeerEvaluation table mein "EvaluatorUserID" column nahi hai, 
                // toh permanent evaluator ki history nikalna mushkil hoga jab tak evaluatorID primary key null hai.
                // Recommendation: PeerEvaluation table mein EvaluateeID aur CourseCode ke logic se check karein.

                return Ok(new List<object>());
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}