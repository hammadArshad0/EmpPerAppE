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

                // 1. Fetch Active KPIs for this Session
                var activeKPIs = db.EmployeSessionKPI
                    .Where(esk => esk.SessionID == sessionId)
                    .Select(esk => new
                    {
                        esk.id,
                        esk.KPIID,
                        esk.SubKPIID,
                        KPIName = db.KPI.Where(k => k.id == esk.KPIID).Select(k => k.name).FirstOrDefault(),
                        SubKPIName = db.SubKPI.Where(sk => sk.id == esk.SubKPIID).Select(sk => sk.name).FirstOrDefault()
                    }).ToList();

                if (!activeKPIs.Any())
                    return Ok(new { Message = "No KPIs configured for this session." });

                // 2. Fetch Base Data (Student & Peer)
                var studentData = db.StudentEvaluation.Where(se => se.Enrollment.teacherID == teacherId && se.Enrollment.sessionID == sessionId);
                double studentAvg = studentData.Any() ? studentData.Average(se => (double)se.score) : 0;

                var peerData = db.PeerEvaluation.Where(pe => pe.evaluateeID == teacherId && pe.PeerEvaluator.sessionID == sessionId);
                double peerAvg = peerData.Any() ? peerData.Average(pe => (double)pe.score) : 0;

                // 3. Fetch Confidential Scores
                var confScores = db.KPIScore.Where(ks => ks.empID == teacherId && ks.EmployeSessionKPI.SessionID == sessionId).ToList();

                // 4. Grouping & Calculations
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
                            w.SessionID == sessionId && w.KPIID == item.KPIID && w.SubKPIID == item.SubKPIID);

                        double weight = weightEntry?.Weight ?? 0;
                        string subNameLower = (item.SubKPIName ?? "").ToLower();
                        double multiplier = 0;

                        if (subNameLower.Contains("student"))
                            multiplier = studentAvg;
                        else if (subNameLower.Contains("peer"))
                            multiplier = peerAvg;
                        else
                        {
                            var specificList = confScores.Where(cs => cs.empKPIID == item.id).ToList();
                            multiplier = specificList.Any() ? specificList.Average(cs => (double)cs.score) : 0;
                        }

                        // Formula: (Obtained / MaxScale 4) * Weight
                        double achieved = Math.Round((multiplier / 4.0) * weight, 2);

                        subDetails.Add(new
                        {
                            SubName = item.SubKPIName, // Graph label ke liye
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
                    OverallPercentage = totalWeight > 0 ? Math.Round((totalAchieved / totalWeight) * 100, 2) : 0,
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
                    // Sirf wahi teachers jo is session mein enrolled hain (via Enrollment table)
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
