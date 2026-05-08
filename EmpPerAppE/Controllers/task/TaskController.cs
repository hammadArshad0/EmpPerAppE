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
                // 1. Base query: Sirf Critical Questions
                var query = db.Questions.Where(q => q.isCritical == true);

                // 2. Mapping Logic: Frontend "Student/Peer" ko DB "s/p" mein convert karein
                string dbSearchType = "";
                if (type.Trim().ToLower() == "student") dbSearchType = "s";
                else if (type.Trim().ToLower() == "peer") dbSearchType = "t";

                // Global Filter for Questions table
                if (!string.IsNullOrEmpty(dbSearchType))
                {
                    // Questionare table mein 'type' column 's' ya 'p' check karega
                    query = query.Where(q => q.Questionare.type.Trim().ToLower() == dbSearchType);
                }

                var criticalQuestions = query.ToList();
                var result = new List<object>();

                foreach (var q in criticalQuestions)
                {
                    var allResponses = new List<dynamic>();

                    // Database se actual type uthayen (kyunki 'All' ke case mein humein pata hona chahiye question kis ka hai)
                    string actualTypeInDB = q.Questionare.type.Trim().ToLower();

                    // 3. Student Data logic (Agar DB mein 's' hai)
                    if (actualTypeInDB == "s")
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

                    // 4. Peer Data logic (Agar DB mein 'p' hai)
                    else if (actualTypeInDB == "t")
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