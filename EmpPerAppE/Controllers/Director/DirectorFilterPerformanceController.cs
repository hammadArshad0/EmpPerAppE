using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using EmpPerAppE.Models;

namespace EmpPerAppE.Controllers.Director
{
    [RoutePrefix("api/DirectorFilter")]
    public class DirectorFilterPerformanceController : ApiController
    {
        private readonly fypempEntities db = new fypempEntities();

        [HttpGet]
        [Route("GetSessions")]
        public IHttpActionResult GetSessions()
        {
            return Ok(db.Session
                .OrderByDescending(s => s.id)
                .Select(s => new { s.id, s.name })
                .ToList());
        }

        [HttpGet]
        [Route("GetDepartments")]
        public IHttpActionResult GetDepartments()
        {
            return Ok(db.Teacher
                .Select(t => t.department)
                .Distinct()
                .ToList());
        }

        [HttpGet]
        [Route("GetCoursesBySession/{sessionId}")]
        public IHttpActionResult GetCoursesBySession(int sessionId)
        {
            return Ok(db.Enrollment
                .Where(e => e.sessionID == sessionId)
                .Select(e => e.courseCode)
                .Distinct()
                .ToList());
        }

        [HttpGet]
        [Route("GetFilteredPerformance")]
        public IHttpActionResult GetFilteredPerformance(
    int? sessionId = null,
    string department = null,
    string courseCode = null)
        {
            try
            {
                int activeSessionId = (sessionId == null || sessionId == 0)
                    ? (db.Session.Any() ? db.Session.Max(s => s.id) : 0)
                    : sessionId.Value;

                var query = from e in db.Enrollment
                            join t in db.Teacher on e.teacherID equals t.userID
                            where e.sessionID == activeSessionId
                            select new { e, t };

                if (!string.IsNullOrEmpty(department) && department != "All")
                    query = query.Where(x => x.t.department == department);

                if (!string.IsNullOrEmpty(courseCode) && courseCode != "All")
                    query = query.Where(x => x.e.courseCode == courseCode);

                var groupedData = query.ToList()
                    .GroupBy(x => new {
                        x.e.teacherID,
                        x.e.courseCode,
                        x.t.name,
                        x.t.department,
                        x.e.sessionID
                    }).ToList();

                // KPI structure ek baar fetch karo
                var kpiStructure = (from esk in db.EmployeSessionKPI
                                    join sk in db.SubKPI on esk.SubKPIID equals sk.id
                                    join sw in db.SessionKPIWeight
                                        on new { esk.SessionID, esk.KPIID, esk.SubKPIID }
                                        equals new { sw.SessionID, sw.KPIID, sw.SubKPIID }
                                    where esk.SessionID == activeSessionId
                                    select new
                                    {
                                        esk.id,
                                        SubKPIName = sk.name,
                                        Weight = (double)(sw.Weight ?? 0)
                                    }).ToList();

                var result = groupedData.Select(g => {

                    var enrollmentIds = g.Select(x => x.e.id).Cast<int>().ToList();

                    // ✅ Society Check — same as performance controller
                    var isSocietyMember = db.SocietyAssignments
                        .Any(sa => sa.TeacherId == g.Key.teacherID && sa.SessionId == activeSessionId);

                    // ✅ CHR Check — Enrollment se session verify
                    var isEnrolledInSession = db.Enrollment
                        .Any(e => e.teacherID == g.Key.teacherID && e.sessionID == activeSessionId);

                    // A. Student Avg (0-4 scale)
                    var studentScores = db.StudentEvaluation
                        .Where(se => enrollmentIds.Contains((int)se.enrollmentID))
                        .Select(se => (double?)se.score).ToList();
                    double studentAvg = studentScores.Any()
                        ? studentScores.Average() ?? 0 : 0;

                    // B. Peer Avg (0-4 scale)
                    var peerScores = db.PeerEvaluation
                        .Where(pe => pe.evaluateeID == g.Key.teacherID
                                  && pe.courseCode == g.Key.courseCode)
                        .Select(pe => (double?)pe.score).ToList();
                    double peerAvg = peerScores.Any()
                        ? peerScores.Average() ?? 0 : 0;

                    // ✅ Society Avg — sirf tab calculate karo jab member ho
                    double societyAvg = 0;
                    if (isSocietyMember)
                    {
                        var societyScores = db.SocietyEvaluation
                            .Where(se => se.EvaluateeId == g.Key.teacherID && se.SessionId == activeSessionId)
                            .Select(x => (double?)x.Score).ToList();
                        societyAvg = societyScores.Any()
                            ? societyScores.Average() ?? 0 : 0;
                    }

                    // ✅ CHR Avg — sab teachers ko, Enrollment se session filter
                    double chrAvg = 0.0;
                    if (isEnrolledInSession)
                    {
                        var chrRawData = db.CHR
                            .Where(c => c.TeacherID == g.Key.teacherID)
                            .Select(x => new { LateIn = x.LateIn ?? 0, LeftEarly = x.LeftEarly ?? 0 })
                            .ToList();

                        chrAvg = chrRawData.Any()
                            ? chrRawData.Select(x => {
                                int total = x.LateIn + x.LeftEarly;
                                if (total >= 10) return 0.0;
                                if (total >= 6) return 3.0;
                                if (total >= 1) return 4.0;
                                return 5.0;
                            }).Average()
                            : 0.0;
                    }

                    // C. KPIScore table
                    var teacherKpiScores = db.KPIScore
                        .Where(ks => ks.empID == g.Key.teacherID)
                        .ToList();

                    double totalAchieved = 0;
                    double totalWeight = 0;
                    var kpiWeightsList = new List<object>();

                    foreach (var kpi in kpiStructure)
                    {
                        string subNameLower = (kpi.SubKPIName ?? "").ToLower();
                        double multiplier = 0;
                        double maxScale = 4.0;

                        // ✅ Society KPI — member nahi toh skip karo
                        if (subNameLower.Contains("society"))
                        {
                            if (!isSocietyMember) continue; // ✅ skip — weight bhi nahi ayegi
                            multiplier = societyAvg;
                            maxScale = 4.0;
                        }
                        else if (subNameLower.Contains("student"))
                        {
                            multiplier = studentAvg;
                            maxScale = 4.0;
                        }
                        else if (subNameLower.Contains("peer"))
                        {
                            multiplier = peerAvg;
                            maxScale = 4.0;
                        }
                        else if (subNameLower.Contains("confidential"))
                        {
                            multiplier = 0;
                            maxScale = 4.0;
                        }
                        // ✅ CHR — sab teachers ko show ho
                        else if (subNameLower.Contains("chr") || subNameLower.Contains("class held report"))
                        {
                            multiplier = chrAvg;
                            maxScale = 5.0;
                        }
                        else
                        {
                            var specificScore = teacherKpiScores
                                .FirstOrDefault(ts => ts.empKPIID == kpi.id);
                            multiplier = specificScore != null
                                ? (double)specificScore.score : 0;
                            maxScale = 5.0;
                        }

                        double achieved = (multiplier / maxScale) * kpi.Weight;
                        totalAchieved += achieved;
                        totalWeight += kpi.Weight;

                        kpiWeightsList.Add(new
                        {
                            SubName = kpi.SubKPIName,
                            Weight = kpi.Weight,
                            Score = multiplier,
                            MaxScale = maxScale
                        });
                    }

                    double overallPerc = totalWeight > 0
                        ? Math.Min(100, Math.Round((totalAchieved / totalWeight) * 100, 2))
                        : 0;

                    return new
                    {
                        TeacherID = g.Key.teacherID,
                        TeacherName = g.Key.name,
                        CourseCode = g.Key.courseCode,
                        Department = g.Key.department,
                        IsSocietyMember = isSocietyMember,   // ✅ frontend ko bhi bhejo
                        StudentPercentage = Math.Round((studentAvg / 4.0) * 100, 2),
                        PeerPercentage = Math.Round((peerAvg / 4.0) * 100, 2),
                        ChrAvgScore = Math.Round(chrAvg, 2),  // ✅ frontend ko bhi bhejo
                        KPIWeights = kpiWeightsList,
                        OverallPercentage = overallPerc
                    };

                }).OrderByDescending(x => x.OverallPercentage).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error: " + ex.Message));
            }
        }
    }
}