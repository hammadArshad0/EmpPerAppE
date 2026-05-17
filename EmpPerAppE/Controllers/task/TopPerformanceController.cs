using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EmpPerAppE.Controllers.task
{
    [RoutePrefix("api/TopPerformance")]
    public class TopPerformanceController : ApiController
    {
        fypempEntities db = new fypempEntities();
        [HttpGet]
        [Route("GetBestPerformerTeacher")]
        public IHttpActionResult GetBestPerformerTeacher(int sessionId)
        {
            try
            {
                // Get all teachers in session
                var teachers = db.Enrollment
                    .Where(e => e.sessionID == sessionId)
                    .Select(e => new
                    {
                        e.teacherID,
                        TeacherName = e.Teacher.name,
                        Department = e.Teacher.department
                    })
                    .Distinct()
                    .ToList();

                if (!teachers.Any())
                    return Ok(new { Message = "No teachers found." });

                double highestScore = -1;

                object bestTeacher = null;

                foreach (var t in teachers)
                {
                    double percentage = CalculateOverallPerformance(
                        t.teacherID,
                        sessionId
                    );

                    if (percentage > highestScore)
                    {
                        highestScore = percentage;

                        bestTeacher = new
                        {
                            TeacherID = t.teacherID,
                            TeacherName = t.TeacherName,
                            Department = t.Department,
                            Percentage = Math.Round(percentage, 2)
                        };
                    }
                }

                return Ok(bestTeacher);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        private double CalculateOverallPerformance(
    string teacherId,
    int sessionId
)
        {
            try
            {
                // =========================
                // Society Membership Check
                // =========================
                var isSocietyMember = db.SocietyAssignments
                    .Any(sa =>
                        sa.TeacherId == teacherId &&
                        sa.SessionId == sessionId);

                // =========================
                // Active KPIs
                // =========================
                var activeKPIs = db.EmployeSessionKPI
                    .Where(esk => esk.SessionID == sessionId)
                    .Select(esk => new
                    {
                        esk.id,
                        esk.KPIID,
                        esk.SubKPIID,

                        KPIName = db.KPI
                            .Where(k => k.id == esk.KPIID)
                            .Select(k => k.name)
                            .FirstOrDefault(),

                        SubKPIName = db.SubKPI
                            .Where(sk => sk.id == esk.SubKPIID)
                            .Select(sk => sk.name)
                            .FirstOrDefault()
                    })
                    .ToList();

                // =========================
                // Remove Society KPI
                // if teacher not society member
                // =========================
                activeKPIs = activeKPIs
                    .Where(item =>
                    {
                        string subName =
                            (item.SubKPIName ?? "").ToLower();

                        if (subName.Contains("society")
                            && !isSocietyMember)
                            return false;

                        return true;
                    })
                    .ToList();

                // =========================
                // Student Average
                // =========================
                var studentAvg = db.StudentEvaluation
                    .Where(se =>
                        se.Enrollment.teacherID == teacherId &&
                        se.Enrollment.sessionID == sessionId)
                    .Select(x => (double?)x.score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;

                // =========================
                // Peer Average
                // =========================
                var peerAvg = db.PeerEvaluation
                    .Where(pe =>
                        pe.evaluateeID == teacherId &&
                        pe.PeerEvaluator.sessionID == sessionId)
                    .Select(x => (double?)x.score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;

                // =========================
                // Society Average
                // =========================
                var societyAvg = db.SocietyEvaluation
                    .Where(se =>
                        se.EvaluateeId == teacherId &&
                        se.SessionId == sessionId)
                    .Select(x => (double?)x.Score)
                    .DefaultIfEmpty()
                    .Average() ?? 0;

                // =========================
                // CHR Average
                // =========================
                var chrRawData = db.CHR
                    .Where(c =>
                        c.TeacherID == teacherId &&
                        c.sessionID == sessionId)
                    .Select(x => new
                    {
                        LateIn = x.LateIn ?? 0,
                        LeftEarly = x.LeftEarly ?? 0
                    })
                    .ToList();

                double chrAvg = chrRawData.Any()
                    ? chrRawData.Select(x =>
                    {
                        int total =
                            x.LateIn + x.LeftEarly;

                        if (total >= 10) return 0.0;

                        if (total >= 6) return 3.0;

                        if (total >= 1) return 4.0;

                        return 5.0;

                    }).Average()
                    : 0.0;

                // =========================
                // KPI Scores
                // =========================
                var confScores = db.KPIScore
                    .Where(ks =>
                        ks.empID == teacherId &&
                        ks.EmployeSessionKPI.SessionID == sessionId)
                    .ToList();

                // =========================
                // Final KPI Calculation
                // =========================
                double totalAchieved = 0;
                double totalWeight = 0;

                foreach (var item in activeKPIs)
                {
                    var weightEntry =
                        db.SessionKPIWeight
                        .FirstOrDefault(w =>
                            w.SessionID == sessionId &&
                            w.KPIID == item.KPIID &&
                            w.SubKPIID == item.SubKPIID);

                    double weight =
                        weightEntry?.Weight ?? 0;

                    string subName =
                        (item.SubKPIName ?? "")
                        .ToLower();

                    double multiplier = 0;
                    double maxScale = 4.0;

                    // =========================
                    // Student Evaluation
                    // =========================
                    if (subName.Contains("student"))
                    {
                        multiplier = studentAvg;
                    }

                    // =========================
                    // Peer Evaluation
                    // =========================
                    else if (subName.Contains("peer"))
                    {
                        multiplier = peerAvg;
                    }

                    // =========================
                    // Society
                    // =========================
                    else if (subName.Contains("society"))
                    {
                        multiplier = isSocietyMember
                            ? societyAvg
                            : 0;
                    }

                    // =========================
                    // Confidential
                    // =========================
                    else if (subName.Contains("confidential"))
                    {
                        multiplier = 0;
                    }

                    // =========================
                    // CHR
                    // =========================
                    else if (
                        subName.Contains("chr") ||
                        subName.Contains("class held report"))
                    {
                        multiplier = chrAvg;
                        maxScale = 5.0;
                    }

                    // =========================
                    // Other KPI Scores
                    // =========================
                    else
                    {
                        var specificScore = confScores
                            .Where(cs => cs.empKPIID == item.id)
                            .Average(cs => (double?)cs.score);

                        multiplier = specificScore ?? 0;

                        maxScale = 5.0;
                    }

                    // =========================
                    // Weighted Achieved
                    // =========================
                    double achieved =
                        (multiplier / maxScale)
                        * weight;

                    totalAchieved += achieved;
                    totalWeight += weight;
                }

                // =========================
                // Final Percentage
                // =========================
                double overallPercentage =
                    totalWeight > 0
                    ? (totalAchieved / totalWeight) * 100
                    : 0;

                return Math.Round(overallPercentage, 2);
            }
            catch
            {
                return 0;
            }
        }

        [HttpGet]
        [Route("GetTeachersCount")]
        public IHttpActionResult GetTeachersCount(int sessionId)
        {
            try
            {
                var totalTeachers = db.Enrollment
                    .Where(e => e.sessionID == sessionId)
                    .Select(e => e.teacherID)
                    .Distinct()
                    .Count();

                return Ok(new
                {
                    SessionId = sessionId,
                    TotalTeachers = totalTeachers
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }


        [HttpGet]
        [Route("GetDetailedInsights")]
        public IHttpActionResult GetDetailedInsights(int sessionId)
        {
            try
            {
                // 1. Get unique teachers in session
                var teachers = db.Enrollment
                    .Where(e => e.sessionID == sessionId)
                    .Select(e => new
                    {
                        e.teacherID,
                        TeacherName = e.Teacher.name,
                        Department = e.Teacher.department
                    })
                    .Distinct()
                    .ToList();

                if (!teachers.Any())
                    return Ok(new { Message = "No teachers found." });

                // 2. Calculate scores for ALL teachers
                var facultyList = new List<object>();
                var totalScoresSum = 0.0;

                foreach (var t in teachers)
                {
                    double percentage = CalculateOverallPerformance(t.teacherID, sessionId);
                    totalScoresSum += percentage;

                    // Anonymous objects create kar rahay hain sorting ke liye
                    dynamic teacherObj = new System.Dynamic.ExpandoObject();
                    teacherObj.TeacherID = t.teacherID;
                    teacherObj.TeacherName = t.TeacherName;
                    teacherObj.Department = t.Department;
                    teacherObj.Percentage = Math.Round(percentage, 2);

                    facultyList.Add(teacherObj);
                }

                // Strongly typed casting for LINQ sorting
                var sortedFaculty = facultyList
                    .Cast<dynamic>()
                    .OrderByDescending(f => f.Percentage)
                    .ToList();

                // 3. Extract Insights
                var top1 = sortedFaculty.FirstOrDefault();
                var lower1 = sortedFaculty.LastOrDefault();
                var top3 = sortedFaculty.Take(3).ToList();
                var lower3 = sortedFaculty.AsEnumerable().Reverse().Take(3).ToList();
                var overallAverage = Math.Round(totalScoresSum / sortedFaculty.Count, 2);

                // 4. Return Data Package
                return Ok(new
                {
                    Top1 = top1,
                    Lower1 = lower1,
                    Top3 = top3,
                    Lower3 = lower3,
                    OverallAverage = overallAverage
                });
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }


    }
}
