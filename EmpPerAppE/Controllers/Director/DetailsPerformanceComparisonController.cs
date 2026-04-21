
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using EmpPerAppE.Models;
using EmpPerAppE.Models.DTO;


namespace EmpPerAppE.Controllers.Director
{
    [RoutePrefix("api/DetailsPerformanceComparison")]
    public class DetailsPerformanceComparisonController : ApiController
    {
        // Model namespace ke mutabiq change karein agar zaroorat ho (e.g., FYPEntities ya EmpPerAppEEntities)
        private fypempEntities db = new fypempEntities();

        // 1. Get All Sessions (Dropdown)
        [HttpGet]
        [Route("GetSessions")]
        public IHttpActionResult GetSessions()
        {
            var sessions = db.Session
                .Select(s => new
                {
                    id = s.id,
                    name = s.name
                }).ToList();

            return Ok(sessions);
        }

        // 2. Get Employee Types (Tabs)
        [HttpGet]
        [Route("GetEmployeeTypes")]
        public IHttpActionResult GetEmployeeTypes()
        {
            var types = db.EmployeeType
                .Select(e => new
                {
                    id = e.id,
                    type = e.type
                }).ToList();

            return Ok(types);
        }

        // 3. Get Courses based on Session
        [HttpGet]
        [Route("GetCoursesBySession")]
        public IHttpActionResult GetCoursesBySession(int sessionId)
        {
            var courses = db.Enrollment
                .Where(e => e.sessionID == sessionId)
                .Select(e => e.courseCode)
                .Distinct()
                .ToList();

            return Ok(courses);
        }

        // 4. Get Teachers By Course (Comparison Filter)
        [HttpGet]
        [Route("GetTeachersByCourse")]
        public IHttpActionResult GetTeachersByCourse(string courseCode)
        {
            if (string.IsNullOrEmpty(courseCode))
                return Ok(new List<object>());

            courseCode = courseCode.Trim().ToUpper();

            var latestSessionId = db.Enrollment
                .Where(e => e.courseCode.ToUpper().Trim() == courseCode)
                .OrderByDescending(e => e.sessionID)
                .Select(e => e.sessionID)
                .FirstOrDefault();

            var teachers = db.Enrollment
                .Where(e => e.courseCode.ToUpper().Trim() == courseCode && e.sessionID == latestSessionId)
                .Select(e => new
                {
                    id = e.teacherID,
                    name = db.Teacher
                                .Where(t => t.userID == e.teacherID)
                                .Select(t => t.name)
                                .FirstOrDefault()
                })
                .Distinct()
                .ToList();

            return Ok(teachers);
        }

        // 5. Comparison Endpoint
        [HttpPost]
        [Route("CompareTeachers")]
        public IHttpActionResult CompareTeachers(CompareDTO dto)
        {
            var result = new List<object>();

            if (dto.mode == "course")
            {
                var latestSessionId = db.Enrollment
                    .Where(e => e.courseCode == dto.courseCode)
                    .OrderByDescending(e => e.sessionID)
                    .Select(e => e.sessionID)
                    .FirstOrDefault();

                result.Add(GetTeacherScore(dto.teacherA, dto.courseCode, latestSessionId));
                result.Add(GetTeacherScore(dto.teacherB, dto.courseCode, latestSessionId));
            }
            else
            {
                result.Add(GetTeacherScore(dto.teacherA, null, dto.session1));
                result.Add(GetTeacherScore(dto.teacherA, null, dto.session2));
            }

            return Ok(result);
        }

        // Helper Method: Calculation Logic
    private object GetTeacherScore(string teacherId, string courseCode, int? sessionId)
{
    const double MAX_SCORE_PER_QUESTION = 4.0;
    const double SCALE_TO_TEN = 10.0;

            // --- Peer Evaluation ---
            // Note: sessionID ka 's' small kar diya hai model ke mutabiq
            var validEnrollments = db.Enrollment
        .Where(e => e.teacherID == teacherId &&
                    (sessionId == null || e.sessionID == sessionId) &&
                    (string.IsNullOrEmpty(courseCode) || e.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()))
        .Select(e => e.courseCode.ToUpper().Trim())
        .Distinct()
        .ToList();

            // --- Peer Evaluation ---
            // Ab hum PeerEvaluation sirf un courses ki nikalenge jo validEnrollments mein hain
            var peerList = db.PeerEvaluation
                .Where(p => p.evaluateeID == teacherId &&
                            validEnrollments.Contains(p.courseCode.ToUpper().Trim()))
                .ToList();
            double peerTotalScore = peerList.Sum(p => (double)p.score);
    // Fixed: Count() brackets lagaye hain
    double peerMaxTotal = peerList.Count() * MAX_SCORE_PER_QUESTION; 
    double peerAverageOutOfTen = peerMaxTotal > 0 ? (peerTotalScore / peerMaxTotal) * SCALE_TO_TEN : 0;
              
            // --- Student Evaluation ---
            var studentList = db.StudentEvaluation
                .Where(s =>
                    (courseCode == null || s.Enrollment.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
                    (sessionId == null || s.Enrollment.sessionID == sessionId) && // ✅ Enrollment table se link kiya
                    s.Enrollment.teacherID == teacherId
                )
                .ToList();

    double studentTotalScore = studentList.Sum(s => (double)s.score);
    // Fixed: Count() brackets lagaye hain
    double studentMaxTotal = studentList.Count() * MAX_SCORE_PER_QUESTION;
    double studentAverageOutOfTen = studentMaxTotal > 0 ? (studentTotalScore / studentMaxTotal) * SCALE_TO_TEN : 0;

    // --- Overall Average Out of 100 ---
    double overallAveragePercentage = 0;
    double totalScore = peerTotalScore + studentTotalScore;
    double totalMax = peerMaxTotal + studentMaxTotal;
    if (totalMax > 0)
        overallAveragePercentage = (totalScore / totalMax) * 100;

    var name = db.Teacher
        .Where(t => t.userID == teacherId)
        .Select(t => t.name)
        .FirstOrDefault();

    return new
    {
        Name = name,
        PeerAverageOutOfTen = Math.Round(peerAverageOutOfTen, 2),
        StudentAverageOutOfTen = Math.Round(studentAverageOutOfTen, 2),
        OverallAverageOutOfHundred = Math.Round(overallAveragePercentage, 2),
        PeerTotalScore = Math.Round(peerTotalScore, 2),
        PeerMaxTotal = peerMaxTotal,
        StudentTotalScore = Math.Round(studentTotalScore, 2),
        StudentMaxTotal = studentMaxTotal,
        TotalScore = Math.Round(totalScore, 2),
        TotalMax = totalMax,
    };
}
    }
}