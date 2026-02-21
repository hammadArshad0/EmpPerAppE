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
                var currentSession = db.Session.FirstOrDefault(s => s.id == sessionId);
                if (currentSession == null) return BadRequest("Invalid Session ID.");

                // 1. Get Session Weights grouped by KPI and then by SubKPI
                var dynamicWeights = db.SessionKPIWeight
                    .Where(w => w.SessionID == sessionId)
                    .ToList()
                    .GroupBy(w => new { w.KPIID, KPIName = db.KPI.Where(k => k.id == w.KPIID).Select(k => k.name).FirstOrDefault() })
                    .Select(kpiGroup => new
                    {
                        KPIName = kpiGroup.Key.KPIName,
                        TotalKPIWeight = (double)kpiGroup.Sum(x => x.Weight),
                        SubKPIData = kpiGroup.Select(sk => new
                        {
                            SubKPIName = db.SubKPI.Where(s => s.id == sk.SubKPIID).Select(s => s.name).FirstOrDefault(),
                            Weight = (double)sk.Weight
                        }).ToList()
                    }).ToList();

                // 2. Fetch Averages
                var studentData = db.StudentEvaluation.Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId);
                double studentAvg = studentData.Any() ? studentData.Average(se => (double)se.score) : 0;

                var peerData = db.PeerEvaluation.Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId);
                double peerAvg = peerData.Any() ? peerData.Average(pe => (double)pe.score) : 0;

                var confData = db.KPIScore.Where(ks => ks.empID == teacherId && ks.EmployeSessionKPI.SessionID == sessionId);
                double confAvg = confData.Any() ? confData.Average(ks => (double)ks.score) : 0;

                // 3. Process Data
                var finalBreakdown = new List<object>();
                double totalAchieved = 0;
                double totalPossibleWeight = 0;
                string[] colors = { "#16a34a", "#1d4ed8", "#b91c1c", "#f59e0b" };
                int colorIdx = 0;

                foreach (var kpi in dynamicWeights)
                {
                    var processedSubKPIs = kpi.SubKPIData.Select(sk =>
                    {
                        string subName = (sk.SubKPIName ?? "").ToLower();
                        // Logic based on SubKPI name
                        double multiplier = subName.Contains("student") ? studentAvg :
                                           (subName.Contains("peer") ? peerAvg : confAvg);

                        double achieved = Math.Round((multiplier / 4.0) * sk.Weight, 2);
                        return new
                        {
                            SubName = sk.SubKPIName,
                            SubMax = sk.Weight,
                            SubAchieved = achieved
                        };
                    }).ToList();

                    double kpiAchieved = processedSubKPIs.Sum(x => x.SubAchieved);
                    totalAchieved += kpiAchieved;
                    totalPossibleWeight += kpi.TotalKPIWeight;

                    finalBreakdown.Add(new
                    {
                        KPIName = kpi.KPIName,
                        KPIWeight = kpi.TotalKPIWeight,
                        KPIAchieved = Math.Round(kpiAchieved, 2),
                        Color = colors[colorIdx % colors.Length],
                        SubDetails = processedSubKPIs
                    });
                    colorIdx++;
                }

                return Ok(new
                {
                    Status = "Success",
                    SessionName = currentSession.name,
                    TeacherID = teacherId,
                    OverallPercentage = totalPossibleWeight > 0 ? Math.Round((totalAchieved / totalPossibleWeight) * 100, 2) : 0,
                    Breakdown = finalBreakdown
                });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}
