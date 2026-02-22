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

                // 1. Get Session Weights
                var dynamicWeights = db.SessionKPIWeight
                    .Where(w => w.SessionID == sessionId)
                    .ToList()
                    .GroupBy(w => new { w.KPIID, KPIName = db.KPI.Where(k => k.id == w.KPIID).Select(k => k.name).FirstOrDefault() })
                    .Select(kpiGroup => new {
                        KPIName = kpiGroup.Key.KPIName,
                        TotalKPIWeight = (double)kpiGroup.Sum(x => x.Weight),
                        SubKPIData = kpiGroup.Select(sk => new {
                            SubKPIName = db.SubKPI.Where(s => s.id == sk.SubKPIID).Select(s => s.name).FirstOrDefault(),
                            Weight = (double)sk.Weight
                        }).ToList()
                    }).ToList();

                // 2. Fetch Averages (SYNCED DATA SE UTHANA HAI)
                var studentData = db.StudentEvaluation.Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId);
                double studentAvg = studentData.Any() ? studentData.Average(se => (double)se.score) : 0;

                var peerData = db.PeerEvaluation.Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId);
                double peerAvg = peerData.Any() ? peerData.Average(pe => (double)pe.score) : 0;

                // Yahan se Confidential data KPIScore se aaye ga
                var confData = db.KPIScore.Where(ks => ks.empID == teacherId && ks.EmployeSessionKPI.SessionID == sessionId);
                double confAvg = confData.Any() ? confData.Average(ks => (double)ks.score) : 0;

                // 3. Process Data for Frontend
                var finalBreakdown = new List<object>();
                double totalAchieved = 0;
                double totalPossibleWeight = 0;

                foreach (var kpi in dynamicWeights)
                {
                    var processedSubKPIs = kpi.SubKPIData.Select(sk => {
                        string subName = (sk.SubKPIName ?? "").ToLower();
                        // Match logic with your Sync method
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
                        SubDetails = processedSubKPIs
                    });
                }

                return Ok(new
                {
                    Status = "Success",
                    SessionName = currentSession.name,
                    OverallPercentage = totalPossibleWeight > 0 ? Math.Round((totalAchieved / totalPossibleWeight) * 100, 2) : 0,
                    Breakdown = finalBreakdown
                });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }




        [HttpPost]
        [Route("SyncTeacherPerformance/{teacherId}/{sessionId}")]
        public IHttpActionResult SyncTeacherPerformance(string teacherId, int sessionId)
        {
            try
            {
                // 1. Calculate Averages (Nullable to avoid errors)
                double studentAvg = db.StudentEvaluation
                    .Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId)
                    .Select(se => (double?)se.score).Average() ?? 0;

                double peerAvg = db.PeerEvaluation
                    .Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId)
                    .Select(pe => (double?)pe.score).Average() ?? 0;

                // 2. Fetch all KPI Mappings
                var sessionKPIs = db.EmployeSessionKPI.Where(esk => esk.SessionID == sessionId).ToList();

                foreach (var mapping in sessionKPIs)
                {
                    var kpiInfo = db.KPI.FirstOrDefault(k => k.id == mapping.KPIID);
                    if (kpiInfo == null) continue;

                    double finalScoreToSave = 0;
                    string kpiName = kpiInfo.name.ToLower();

                    if (kpiName.Contains("student")) finalScoreToSave = studentAvg;
                    else if (kpiName.Contains("peer")) finalScoreToSave = peerAvg;
                    else continue;

                    var existingRecord = db.KPIScore.FirstOrDefault(ks => ks.empID == teacherId && ks.empKPIID == mapping.id);

                    if (existingRecord != null)
                    {
                        // Score ko decimal/float mein hi rehne dein agar DB column support karta hai
                        // Warna rounding ki wajah se 3.8 aur 4.2 mein farq khatam ho jayega
                        existingRecord.score = (int)Math.Round(finalScoreToSave);
                    }
                    else
                    {
                        db.KPIScore.Add(new KPIScore
                        {
                            empKPIID = mapping.id,
                            empID = teacherId,
                            score = (int)Math.Round(finalScoreToSave),
                            evaluatorID = "SYSTEM_SYNC"
                        });
                    }
                }
                db.SaveChanges();
                return Ok(new { Message = "Synced", StudentAverage = studentAvg, PeerAverage = peerAvg });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    } }
