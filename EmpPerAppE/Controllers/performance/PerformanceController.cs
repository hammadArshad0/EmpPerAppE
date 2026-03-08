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

                // 1. Fetch Active KPIs for this Session from EmployeSessionKPI table
                // Isse humein pata chalay ga ke actually is session mein kya kya evaluate hona hai
                var activeKPIs = db.EmployeSessionKPI
                    .Where(esk => esk.SessionID == sessionId)
                    .Select(esk => new {
                        esk.id,
                        esk.KPIID,
                        esk.SubKPIID,
                        KPIName = db.KPI.Where(k => k.id == esk.KPIID).Select(k => k.name).FirstOrDefault(),
                        SubKPIName = db.SubKPI.Where(sk => sk.id == esk.SubKPIID).Select(sk => sk.name).FirstOrDefault()
                    }).ToList();

                if (!activeKPIs.Any()) return Ok(new { Message = "No KPIs configured for this session." });

                // 2. Fetch Averages (Student, Peer, and Confidential)
                var studentData = db.StudentEvaluation.Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId);
                double studentAvg = studentData.Any() ? studentData.Average(se => (double)se.score) : 0;

                var peerData = db.PeerEvaluation.Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId);
                double peerAvg = peerData.Any() ? peerData.Average(pe => (double)pe.score) : 0;

                // Yahan Confidential data direct KPIScore se aa raha hai jo EmployeSessionKPI se linked hai
                var confScores = db.KPIScore.Where(ks => ks.empID == teacherId && ks.EmployeSessionKPI.SessionID == sessionId).ToList();

                // 3. Process Final Breakdown
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
                        // Weight table se weight uthana
                        var weightEntry = db.SessionKPIWeight.FirstOrDefault(w =>
                            w.SessionID == sessionId && w.KPIID == item.KPIID && w.SubKPIID == item.SubKPIID);

                        double weight = weightEntry?.Weight ?? 0;
                        string subNameLower = (item.SubKPIName ?? "").ToLower();
                        double multiplier = 0;

                        // Logic to select average based on SubKPI name or type
                        if (subNameLower.Contains("student"))
                            multiplier = studentAvg;
                        else if (subNameLower.Contains("peer"))
                            multiplier = peerAvg;
                        else
                        {
                            // Agar Student/Peer nahi hai toh KPIScore table se specific subKPI ka score uthayein
                            var specificConfScore = confScores.Where(cs => cs.empKPIID == item.id).Average(cs => (double?)cs.score);
                            multiplier = specificConfScore ?? 0;
                        }

                        // Calculation: (Obtained / MaxScale) * Weightage
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

                return Ok(new
                {
                    Status = "Success",
                    SessionName = currentSession.name,
                    OverallPercentage = totalWeight > 0 ? Math.Round((totalAchieved / totalWeight) * 100, 2) : 0,
                    Breakdown = finalBreakdown
                });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }



        //[HttpPost]
        //[Route("SyncTeacherPerformance/{teacherId}/{sessionId}")]
        //public IHttpActionResult SyncTeacherPerformance(string teacherId, int sessionId)
        //{
        //    try
        //    {
        //        // 1. Calculate Averages (Nullable to avoid errors)
        //        double studentAvg = db.StudentEvaluation
        //            .Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId)
        //            .Select(se => (double?)se.score).Average() ?? 0;

        //        double peerAvg = db.PeerEvaluation
        //            .Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId)
        //            .Select(pe => (double?)pe.score).Average() ?? 0;

        //        // 2. Fetch all KPI Mappings
        //        var sessionKPIs = db.EmployeSessionKPI.Where(esk => esk.SessionID == sessionId).ToList();

        //        foreach (var mapping in sessionKPIs)
        //        {
        //            var kpiInfo = db.KPI.FirstOrDefault(k => k.id == mapping.KPIID);
        //            if (kpiInfo == null) continue;

        //            double finalScoreToSave = 0;
        //            string kpiName = kpiInfo.name.ToLower();

        //            if (kpiName.Contains("student")) finalScoreToSave = studentAvg;
        //            else if (kpiName.Contains("peer")) finalScoreToSave = peerAvg;
        //            else continue;

        //            var existingRecord = db.KPIScore.FirstOrDefault(ks => ks.empID == teacherId && ks.empKPIID == mapping.id);

        //            if (existingRecord != null)
        //            {
        //                // Score ko decimal/float mein hi rehne dein agar DB column support karta hai
        //                // Warna rounding ki wajah se 3.8 aur 4.2 mein farq khatam ho jayega
        //                existingRecord.score = (int)Math.Round(finalScoreToSave);
        //            }
        //            else
        //            {
        //                db.KPIScore.Add(new KPIScore
        //                {
        //                    empKPIID = mapping.id,
        //                    empID = teacherId,
        //                    score = (int)Math.Round(finalScoreToSave),
        //                    evaluatorID = "SYSTEM_SYNC"
        //                });
        //            }
        //        }
        //        db.SaveChanges();
        //        return Ok(new { Message = "Synced", StudentAverage = studentAvg, PeerAverage = peerAvg });
        //    }
        //    catch (Exception ex) { return InternalServerError(ex); }
        //}
    } }
