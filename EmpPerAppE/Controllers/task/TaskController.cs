using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using EmpPerAppE.Models;

namespace EmpPerAppE.Controllers.task
{
    [RoutePrefix("api/Analytics")]
    public class TaskController : ApiController
    {
        private fypempEntities db = new fypempEntities();

        [HttpGet]
        [Route("GetCriticalAnalysis")]
        public IHttpActionResult GetCriticalAnalysis(string type = "All")
        {
            try
            {
                // 1. Critical questions filter karein aur unka type bhi check karein
                var query = db.Questions.Where(q => q.isCritical == true);

                if (!string.IsNullOrEmpty(type) && type != "All")
                {
                    // Yahan ensure karein ke "type" DB ke Questionare.type se exact match kare
                    query = query.Where(q => q.Questionare.type.Trim() == type.Trim());
                }

                var criticalQuestions = query.ToList();
                var result = new List<object>();

                foreach (var q in criticalQuestions)
                {
                    var allResponses = new List<dynamic>();

                    // 2. Student Data (Sirf tab jab type All ho ya Student ho)
                    if (type == "All" || type == "Student")
                    {
                        var sEval = db.StudentEvaluation.Where(e => e.questionID == q.QuestionID)
                            .Select(e => new {
                                EvaluatorID = e.Enrollment.studentID,
                                EvaluatorName = e.Enrollment.Student.name,
                                EvaluateeID = e.Enrollment.teacherID,
                                EvaluateeName = e.Enrollment.Teacher.name,
                                Score = e.score ?? 0,
                                Role = "Student"
                            }).ToList();
                        allResponses.AddRange(sEval);
                    }

                    // 3. Peer Data (Sirf tab jab type All ho ya Peer ho)
                    if (type == "All" || type == "Peer")
                    {
                        var pEval = db.PeerEvaluation.Where(e => e.questionID == q.QuestionID)
                            .Select(e => new {
                                EvaluatorID = e.PeerEvaluator.teacherID,
                                EvaluatorName = e.PeerEvaluator.Teacher.name,
                                EvaluateeID = e.evaluateeID,
                                EvaluateeName = db.Teacher.Where(t => t.userID == e.evaluateeID).Select(t => t.name).FirstOrDefault(),
                                Score = e.score ?? 0,
                                Role = "Peer"
                            }).ToList();
                        allResponses.AddRange(pEval);
                    }

                    // Sirf wahi sawal result mein dalein jin ka data mila hai
                    if (allResponses.Any())
                    {
                        result.Add(new
                        {
                            QuestionId = q.QuestionID,
                            QuestionText = q.QuestionText,
                            AverageScore = Math.Round(allResponses.Average(r => (double)r.Score), 1),
                            Distribution = new[] {
                        new { Label = "Poor", Count = allResponses.Count(r => r.Score == 1), Score = 1 },
                        new { Label = "Average", Count = allResponses.Count(r => r.Score == 2), Score = 2 },
                        new { Label = "Good", Count = allResponses.Count(r => r.Score == 3), Score = 3 },
                        new { Label = "Excellent", Count = allResponses.Count(r => r.Score == 4), Score = 4 }
                    },
                            Details = allResponses
                        });
                    }
                }
                return Ok(result);
            }
            catch (Exception ex) { return BadRequest(ex.Message); }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}