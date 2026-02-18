using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EmpPerAppE.Controllers.performance
{
    [RoutePrefix("api/Performance")]
    public class PerformanceController : ApiController
    {
        fypempEntities db = new fypempEntities();

        [HttpGet]
        [Route("GetTeacherPerformanceAnalytics/{teacherId}/{sessionId}")]
        public IHttpActionResult GetTeacherPerformanceAnalytics(string teacherId, int sessionId)
        {
            try
            {
                // 1. Session se Weights fetch karein aur SubKPIs ko KPI wise Group kar ke sum karein
                // Screenshot ke mutabiq aapka weight 'int' hai, is liye hum yahan sum karte hain
                var groupedWeights = db.SessionKPIWeight
                    .Where(w => w.SessionID == sessionId)
                    .GroupBy(w => w.KPIID)
                    .Select(g => new {
                        KPIID = g.Key,
                        // Null check aur explicit cast double mein taake calculation accurate ho
                        TotalWeight = (double?)g.Sum(x => x.Weight) ?? 0,
                        KPIName = db.KPI.Where(k => k.id == g.Key).Select(k => k.name).FirstOrDefault()
                    }).ToList();

                if (!groupedWeights.Any())
                    return BadRequest("Is Session ke liye koi weights set nahi hain (Check SessionID: " + sessionId + ")");

                // 2. Evaluation Averages (Out of 4)
                // Student Evaluation data
                var studentData = db.StudentEvaluation
                    .Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId);
                double studentAvg = studentData.Any() ? studentData.Average(se => (double)se.score) : 0;

                // Peer Evaluation data
                var peerData = db.PeerEvaluation
                    .Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId);
                double peerAvg = peerData.Any() ? peerData.Average(pe => (double)pe.score) : 0;

                // KPIScore (Confidential/Management Evaluation)
                var confData = db.KPIScore
                    .Where(ks => ks.empID == teacherId && ks.EmployeSessionKPI.SessionID == sessionId);
                double confAvg = confData.Any() ? confData.Average(ks => (double)ks.score) : 0;

                // 3. Dynamic Calculation based on Grouped Weights from Database
                double studentFinal = 0, peerFinal = 0, confFinal = 0;
                double studentMax = 0, peerMax = 0, confMax = 0;

                foreach (var kpi in groupedWeights)
                {
                    // Null safety check for KPIName
                    string name = (kpi.KPIName ?? "").ToLower();

                    if (name.Contains("student"))
                    {
                        studentMax = kpi.TotalWeight;
                        studentFinal = (studentAvg / 4.0) * studentMax;
                    }
                    else if (name.Contains("peer") || name.Contains("teacher"))
                    {
                        peerMax = kpi.TotalWeight;
                        peerFinal = (peerAvg / 4.0) * peerMax;
                    }
                    else if (name.Contains("confidential") || name.Contains("management"))
                    {
                        confMax = kpi.TotalWeight;
                        confFinal = (confAvg / 4.0) * confMax;
                    }
                }

                // 4. Final Aggregation
                double totalAchieved = studentFinal + peerFinal + confFinal;
                double totalPossible = studentMax + peerMax + confMax;

                // Percentage logic with Zero division check
                double percentage = totalPossible > 0 ? (totalAchieved / totalPossible) * 100 : 0;

                return Ok(new
                {
                    Status = "Success",
                    TeacherID = teacherId,
                    SessionID = sessionId,
                    OverallPercentage = Math.Round(percentage, 2),
                    GraphData = new[] {
                        new { name = "Student Eval", score = Math.Round(studentFinal, 2), max = studentMax, color = "#16a34a" },
                        new { name = "Peer Eval", score = Math.Round(peerFinal, 2), max = peerMax, color = "#1d4ed8" },
                        new { name = "Confidential", score = Math.Round(confFinal, 2), max = confMax, color = "#b91c1c" }
                    }
                });
            }
            catch (Exception ex)
            {
                // Detailed error for debugging
                return InternalServerError(new Exception("Error: " + ex.Message));
            }
        }
    }
}