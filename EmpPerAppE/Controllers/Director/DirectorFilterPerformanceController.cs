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
                // 1. Current ya Selected Session fix karna
                int activeSessionId = (sessionId == null || sessionId == 0)
                                     ? (db.Session.Any() ? db.Session.Max(s => s.id) : 0)
                                     : sessionId.Value;

                // 2. Base Query: Enrollment aur Teacher ko join karein
                var query = from e in db.Enrollment
                            join t in db.Teacher on e.teacherID equals t.userID
                            where e.sessionID == activeSessionId
                            select new { e, t };

                // 3. Filters Apply karein
                if (!string.IsNullOrEmpty(department) && department != "All")
                    query = query.Where(x => x.t.department == department);

                if (!string.IsNullOrEmpty(courseCode) && courseCode != "All")
                    query = query.Where(x => x.e.courseCode == courseCode);

                // 4. GROUPING LOGIC (Teacher + Course)
                // Taake agar 20 students aik course mein hain, toh results group ho kar 1 row ban jaye
                var groupedData = query.ToList()
                                       .GroupBy(x => new {
                                           x.e.teacherID,
                                           x.e.courseCode,
                                           x.t.name,
                                           x.t.department,
                                           x.e.sessionID
                                       })
                                       .ToList();

                // 5. Final Calculation
                var result = groupedData.Select(g => {

                    // Is specific Teacher aur Course ke tamam students ki IDs (Fixes casting error)


                    // A. Student Evaluation: In tamam (e.g. 20) students ke scores ka average
                    // 1. Pehle Enrollment IDs ko safe list mein convert karein (Nulls nikal kar)
                    // 1. Enrollment IDs ki list nikalen (Ensure karein ke ye List<int> hai)
                    // 1. Enrollment IDs ki list nikalen aur confirm karein ke ye List<int> hai
                    var enrollmentIds = g.Select(x => x.e.id).Cast<int>().ToList();

                    // 2. Student Evaluation Query (Type Casting ke sath)
                    var studentScores = db.StudentEvaluation
                                         .Where(se => enrollmentIds.Contains((int)se.enrollmentID)) // (int) cast match karega list se
                                         .Select(se => (double?)se.score)
                                         .ToList();

                    double studentAvg = studentScores.Any() ? studentScores.Average() ?? 0 : 0;

                    // B. Peer Evaluation: Teacher + Course specific
                    var peerScores = db.PeerEvaluation
                                       .Where(pe => pe.evaluateeID == g.Key.teacherID && pe.courseCode == g.Key.courseCode)
                                       .Select(pe => (double?)pe.score).ToList();
                    double peerAvg = peerScores.Any() ? peerScores.Average() ?? 0 : 0;

                    // C. KPI Score: Teacher ka is session ka record
                    var kpiScores = (from ks in db.KPIScore
                                     join esk in db.EmployeSessionKPI on ks.empKPIID equals esk.id
                                     where ks.empID == g.Key.teacherID && esk.SessionID == g.Key.sessionID
                                     select (double?)ks.score).ToList();
                    double kpiAvg = kpiScores.Any() ? kpiScores.Average() ?? 0 : 0;

                    return new
                    {
                        TeacherID = g.Key.teacherID,
                        TeacherName = g.Key.name,
                        CourseCode = g.Key.courseCode,
                        Department = g.Key.department,

                        // Scale 4 to Percentage (For Progress Bars)
                        StudentPercentage = Math.Round((studentAvg / 4.0) * 100, 2),
                        PeerPercentage = Math.Round((peerAvg / 4.0) * 100, 2),
                        KPIPercentage = Math.Round((kpiAvg / 4.0) * 100, 2),

                        // Final Average (Out of 4)
                        OverallAvg = Math.Round((studentAvg + peerAvg + kpiAvg) / 3.0, 2)
                    };
                }).OrderByDescending(x => x.OverallAvg).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error: " + ex.Message));
            }
        }
    }
}