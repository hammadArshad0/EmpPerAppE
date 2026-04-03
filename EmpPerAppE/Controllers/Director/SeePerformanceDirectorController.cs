using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using EmpPerAppE.Models;

namespace EmpPerAppE.Controllers.Director
{
    [RoutePrefix("api/Director")]
    public class SeePerformanceDirectorController : ApiController
    {
        fypempEntities db = new fypempEntities();

        [HttpGet]
        [Route("GetSpecificTeacherPerformance/{teacherId}/{sessionId}")]
        public IHttpActionResult GetSpecificTeacherPerformance(string teacherId, int sessionId)
        {
            try
            {
                var currentSession = db.Session.FirstOrDefault(s => s.id == sessionId);
                var teacher = db.Teacher.FirstOrDefault(t => t.userID == teacherId);

                if (currentSession == null || teacher == null)
                    return BadRequest("Invalid Session or Teacher ID.");

                var activeKPIs = db.EmployeSessionKPI
                    .Where(esk => esk.SessionID == sessionId)
                    .Select(esk => new
                    {
                        esk.id,
                        esk.KPIID,
                        esk.SubKPIID,
                        KPIName = db.KPI.Where(k => k.id == esk.KPIID)
                                          .Select(k => k.name).FirstOrDefault(),
                        SubKPIName = db.SubKPI.Where(sk => sk.id == esk.SubKPIID)
                                             .Select(sk => sk.name).FirstOrDefault()
                    }).ToList();

                if (!activeKPIs.Any())
                    return Ok(new { Message = "No KPIs configured for this session." });

                // Student Avg (0-4 scale)
                var studentData = db.StudentEvaluation
                    .Where(se => se.Enrollment.teacherID == teacherId
                              && se.Enrollment.sessionID == sessionId);
                double studentAvg = studentData.Any()
                    ? studentData.Average(se => (double)se.score) : 0;

                // Peer Avg (0-4 scale)
                var peerData = db.PeerEvaluation
                    .Where(pe => pe.evaluateeID == teacherId
                              && pe.PeerEvaluator.sessionID == sessionId);
                double peerAvg = peerData.Any()
                    ? peerData.Average(pe => (double)pe.score) : 0;

                // KPIScore table — Paper/Folder (0-5 scale)
                var kpiScores = db.KPIScore
                    .Where(ks => ks.empID == teacherId
                              && ks.EmployeSessionKPI.SessionID == sessionId)
                    .ToList();

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
                        var weightEntry = db.SessionKPIWeight.FirstOrDefault(w =>
                            w.SessionID == sessionId
                            && w.KPIID == item.KPIID
                            && w.SubKPIID == item.SubKPIID);

                        double weight = weightEntry?.Weight ?? 0;
                        string subNameLower = (item.SubKPIName ?? "").ToLower();
                        double multiplier = 0;
                        double maxScale = 4.0; // default

                        if (subNameLower.Contains("student"))
                        {
                            multiplier = studentAvg; // 0-4 scale
                            maxScale = 4.0;
                        }
                        else if (subNameLower.Contains("peer"))
                        {
                            multiplier = peerAvg; // 0-4 scale
                            maxScale = 4.0;
                        }
                        else if (subNameLower.Contains("confidential"))
                        {
                            // ✅ Dexie only — backend 0 bheje
                            // Frontend Dexie avg se replace karega
                            multiplier = 0;
                            maxScale = 4.0;
                        }
                        else
                        {
                            // ✅ Paper/Folder — KPIScore table (0-5 scale)
                            var specificList = kpiScores
                                .Where(cs => cs.empKPIID == item.id).ToList();
                            multiplier = specificList.Any()
                                ? specificList.Average(cs => (double)cs.score) : 0;
                            maxScale = 5.0; // ✅ KPIScore max scale = 5
                        }

                        // ✅ Scale-aware formula: (multiplier / maxScale) * weight
                        double achieved = Math.Round((multiplier / maxScale) * weight, 2);

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
                    TeacherName = teacher.name,
                    Department = teacher.department,
                    SessionName = currentSession.name,
                    OverallPercentage = totalWeight > 0
                        ? Math.Round((totalAchieved / totalWeight) * 100, 2) : 0,
                    Breakdown = finalBreakdown
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        [HttpGet]
        [Route("GetTeachersBySession/{sessionId}")]
        public IHttpActionResult GetTeachersBySession(int sessionId)
        {
            try
            {
                var enrolledTeachers = db.Enrollment
                    .Where(e => e.sessionID == sessionId)
                    .Select(e => new {
                        UserID = e.Teacher.userID,
                        Name = e.Teacher.name
                    })
                    .Distinct()
                    .ToList();

                return Ok(enrolledTeachers);
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }
    }
}