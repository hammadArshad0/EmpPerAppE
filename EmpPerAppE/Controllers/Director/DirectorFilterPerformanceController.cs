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
            return Ok(db.Session.OrderByDescending(s => s.id).Select(s => new { s.id, s.name }).ToList());
        }

        [HttpGet]
        [Route("GetDepartments")]
        public IHttpActionResult GetDepartments()
        {
            return Ok(db.Teacher.Select(t => t.department).Distinct().ToList());
        }

        [HttpGet]
        [Route("GetCoursesBySession/{sessionId}")]
        public IHttpActionResult GetCoursesBySession(int sessionId)
        {
            return Ok(db.Enrollment.Where(e => e.sessionID == sessionId).Select(e => e.courseCode).Distinct().ToList());
        }

        [HttpGet]
        [Route("GetFilteredPerformance")]
        public IHttpActionResult GetFilteredPerformance(int? sessionId = null, string department = null, string courseCode = null)
        {
            try
            {
                // 1. Session Handle Karna
                int activeSessionId = (sessionId == null || sessionId == 0)
                                     ? (db.Session.Any() ? db.Session.Max(s => s.id) : 0)
                                     : sessionId.Value;

                // 2. Base Query: Teachers and Enrollments
                var query = from e in db.Enrollment
                            join t in db.Teacher on e.teacherID equals t.userID
                            where e.sessionID == activeSessionId
                            select new { e, t };

                // 3. Filters
                if (!string.IsNullOrEmpty(department) && department != "All")
                    query = query.Where(x => x.t.department == department);

                if (!string.IsNullOrEmpty(courseCode) && courseCode != "All")
                    query = query.Where(x => x.e.courseCode == courseCode);

                // 4. Grouping by Teacher and Course (Matching Detail Page granularity)
                var groupedData = query.ToList().GroupBy(x => new {
                    x.e.teacherID,
                    x.e.courseCode,
                    x.t.name,
                    x.t.department,
                    x.e.sessionID
                }).ToList();

                var result = groupedData.Select(g => {
                    var enrollmentIds = g.Select(x => x.e.id).Cast<int>().ToList();

                    // A. Student Average (Exactly like Detail Page)
                    var studentScores = db.StudentEvaluation.Where(se => enrollmentIds.Contains((int)se.enrollmentID))
                                          .Select(se => (double?)se.score).ToList();
                    double studentAvg = studentScores.Any() ? studentScores.Average() ?? 0 : 0;

                    // B. Peer Average (Exactly like Detail Page)
                    var peerScores = db.PeerEvaluation.Where(pe => pe.evaluateeID == g.Key.teacherID && pe.courseCode == g.Key.courseCode)
                                       .Select(pe => (double?)pe.score).ToList();
                    double peerAvg = peerScores.Any() ? peerScores.Average() ?? 0 : 0;

                    // C. KPI Data with Weights (Matching SeePerformance Logic)
                    // Fetch all KPIs for this session and this specific teacher
                    var kpiData = (from esk in db.EmployeSessionKPI
                                   join sk in db.SubKPI on esk.SubKPIID equals sk.id
                                   join sw in db.SessionKPIWeight on new { esk.SessionID, esk.KPIID, esk.SubKPIID }
                                                equals new { sw.SessionID, sw.KPIID, sw.SubKPIID }
                                   where esk.SessionID == g.Key.sessionID
                                   select new
                                   {
                                       esk.id,
                                       SubKPIName = sk.name,
                                       Weight = (double)sw.Weight
                                   }).ToList();

                    // Fetch actual scores saved in DB for this teacher
                    var teacherScores = db.KPIScore.Where(ks => ks.empID == g.Key.teacherID).ToList();

                    double totalAchievedPoints = 0;
                    double totalWeight = 0;

                    var kpiWeightsList = new List<object>();

                    foreach (var kpi in kpiData)
                    {
                        string subNameLower = (kpi.SubKPIName ?? "").ToLower();
                        double multiplier = 0;

                        // Same mapping as Detail Page
                        if (subNameLower.Contains("student"))
                            multiplier = studentAvg;
                        else if (subNameLower.Contains("peer"))
                            multiplier = peerAvg;
                        else
                        {
                            // For Confidential or other KPIs
                            var specificScore = teacherScores.FirstOrDefault(ts => ts.empKPIID == kpi.id);
                            multiplier = specificScore != null ? (double)specificScore.score : 0;
                        }

                        // Formula: (Multiplier / 4.0) * Weight
                        double achieved = (multiplier / 4.0) * kpi.Weight;

                        totalAchievedPoints += achieved;
                        totalWeight += kpi.Weight;

                        // Prepare for Frontend (Dexie logic sync)
                        kpiWeightsList.Add(new
                        {
                            SubName = kpi.SubKPIName,
                            Weight = kpi.Weight,
                            Score = multiplier
                        });
                    }

                    // Calculate Final Overall Percentage
                    double overallPercentage = totalWeight > 0
                        ? Math.Round((totalAchievedPoints / totalWeight) * 100, 2)
                        : 0;

                    return new
                    {
                        TeacherID = g.Key.teacherID,
                        TeacherName = g.Key.name,
                        CourseCode = g.Key.courseCode,
                        Department = g.Key.department,
                        StudentPercentage = Math.Round((studentAvg / 4.0) * 100, 2),
                        PeerPercentage = Math.Round((peerAvg / 4.0) * 100, 2),
                        // Breakdown to allow Frontend/Dexie to recalculate exactly
                        KPIWeights = kpiWeightsList,
                        OverallPercentage = overallPercentage
                    };
                }).OrderByDescending(x => x.OverallPercentage).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Sync Error: " + ex.Message));
            }
        }
    }
}