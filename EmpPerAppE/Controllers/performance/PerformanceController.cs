using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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
                // 1. Basic Data Fetching (Session & Teacher)
                var currentSession = db.Session.FirstOrDefault(s => s.id == sessionId);

                if (currentSession == null) return BadRequest("Invalid Session ID.");

                var teacherData = db.Teacher.FirstOrDefault(t => t.userID == teacherId);
                if (teacherData == null) return BadRequest("Teacher not found.");

                // 2. Fetch Active KPIs for this Session
                var activeKPIs = db.EmployeSessionKPI
                    .Where(esk => esk.SessionID == sessionId)
                    .Select(esk => new {
                        esk.id,
                        esk.KPIID,
                        esk.SubKPIID,
                        KPIName = db.KPI.Where(k => k.id == esk.KPIID).Select(k => k.name).FirstOrDefault(),
                        SubKPIName = db.SubKPI.Where(sk => sk.id == esk.SubKPIID).Select(sk => sk.name).FirstOrDefault()
                    }).ToList();

                if (!activeKPIs.Any()) return Ok(new { Status = "Empty", Message = "No KPIs configured for this session." });

                // 3. Fetch Averages (Calculated once to optimize performance)
                var studentData = db.StudentEvaluation.Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId);
                double studentAvg = studentData.Any() ? studentData.Average(se => (double)se.score) : 0;

                var peerData = db.PeerEvaluation.Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId);
                double peerAvg = peerData.Any() ? peerData.Average(pe => (double)pe.score) : 0;

                var confScores = db.KPIScore.Where(ks => ks.empID == teacherId && ks.EmployeSessionKPI.SessionID == sessionId).ToList();

                // 4. Processing Breakdown
                var groupedKPIs = activeKPIs.GroupBy(k => new { k.KPIID, k.KPIName });
                var finalBreakdown = new List<object>();
                double totalAchieved = 0;
                double totalWeight = 0;

                foreach (var kpiGroup in groupedKPIs)
                {
                    var subDetails = new List<object>();
                    double kpiGroupAchieved = 0;
                    double kpiGroupWeight = 0;

                    foreach (var item in kpiGroup)
                    {
                        // Get individual weight for SubKPI
                        var weightEntry = db.SessionKPIWeight.FirstOrDefault(w =>
                            w.SessionID == sessionId && w.KPIID == item.KPIID && w.SubKPIID == item.SubKPIID);

                        double weight = weightEntry?.Weight ?? 0;
                        string subNameLower = (item.SubKPIName ?? "").ToLower();
                        double multiplier = 0;

                        // Selection logic
                        if (subNameLower.Contains("student"))
                            multiplier = studentAvg;
                        else if (subNameLower.Contains("peer"))
                            multiplier = peerAvg;
                        else
                        {
                            var specificConfScore = confScores
                                .Where(cs => cs.empKPIID == item.id)
                                .Average(cs => (double?)cs.score);
                            multiplier = specificConfScore ?? 0;
                        }

                        // Calculation: (AvgScore / MaxScale 4) * Weight
                        double achieved = Math.Round((multiplier / 4.0) * weight, 2);

                        subDetails.Add(new
                        {
                            SubName = item.SubKPIName,
                            SubMax = weight,
                            SubAchieved = achieved
                        });

                        kpiGroupAchieved += achieved;
                        kpiGroupWeight += weight;
                    }

                    finalBreakdown.Add(new
                    {
                        KPIName = kpiGroup.Key.KPIName,
                        KPIWeight = kpiGroupWeight,
                        KPIAchieved = Math.Round(kpiGroupAchieved, 2),
                        SubDetails = subDetails
                    });

                    totalAchieved += kpiGroupAchieved;
                    totalWeight += kpiGroupWeight;
                }

                // 5. Final Combined Response
                return Ok(new
                {
                    Status = "Success",
                    TeacherName = teacherData?.name,       // As per your SQL: [name]
                    Department = teacherData?.department, // As per your SQL: [department]
                    SessionName = currentSession.name,
                    OverallPercentage = totalWeight > 0 ? Math.Round((totalAchieved / totalWeight) * 100, 2) : 0,
                    Breakdown = finalBreakdown
                });
            }
            catch (Exception ex) 
            { 
                return InternalServerError(ex); 
            }
        }
    }
}