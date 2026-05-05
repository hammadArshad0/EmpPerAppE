using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;

namespace EmpPerAppE.Controllers.Teacher
{
    [RoutePrefix("api/TeacherSelf")]
    public class AcdenicPerformanceTeacherController : ApiController  // ✅ Direct class, no nesting
    {
        private fypempEntities db = new fypempEntities();

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
        [Route("GetMyCourses/{teacherId}/{sessionId}")]
        public IHttpActionResult GetMyCourses(string teacherId, int sessionId)
        {
            var courses = db.Enrollment
                .Where(e => e.teacherID == teacherId && e.sessionID == sessionId)
                .Select(e => e.courseCode)
                .Distinct()
                .ToList();
            return Ok(courses);
        }

        [HttpGet]
        [Route("GetMyPerformance/{teacherId}/{sessionId}")]
        public IHttpActionResult GetMyPerformance(string teacherId, int sessionId)
        {
            const double MAX = 4.0;
            const double SCALE = 10.0;

            var studentList = db.StudentEvaluation
                .Where(s => s.Enrollment.teacherID == teacherId
                         && s.Enrollment.sessionID == sessionId)
                .ToList();
            double sAvg = studentList.Any()
                ? (studentList.Sum(s => (double)s.score) / (studentList.Count * MAX)) * SCALE : 0;

            var peerList = db.PeerEvaluation
                .Where(p => p.evaluateeID == teacherId
                         && p.PeerEvaluator.sessionID == sessionId)
                .ToList();
            double pAvg = peerList.Any()
                ? (peerList.Sum(p => (double)p.score) / (peerList.Count * MAX)) * SCALE : 0;

            var isEnrolled = db.Enrollment
                .Any(e => e.teacherID == teacherId && e.sessionID == sessionId);

            double chrAvg = 0.0;
            if (isEnrolled)
            {
                var chrRawData = db.CHR
                    .Where(c => c.TeacherID == teacherId)
                    .Select(x => new { LateIn = x.LateIn ?? 0, LeftEarly = x.LeftEarly ?? 0 })
                    .ToList();
                chrAvg = chrRawData.Any()
                    ? chrRawData.Select(x => {
                        int total = x.LateIn + x.LeftEarly;
                        if (total >= 10) return 0.0;
                        if (total >= 6) return 3.0;
                        if (total >= 1) return 4.0;
                        return 5.0;
                    }).Average() : 0.0;
            }
            double chrPerc = Math.Round((chrAvg / 5.0) * SCALE, 2);

            var courses = db.Enrollment
                .Where(e => e.teacherID == teacherId && e.sessionID == sessionId)
                .Select(e => e.courseCode)
                .Distinct()
                .ToList();

            var teacher = db.Teacher.FirstOrDefault(t => t.userID == teacherId);

            return Ok(new
            {
                TeacherID = teacherId,
                Name = teacher?.name,
                Department = teacher?.department,
                StudentAverage = Math.Round(sAvg, 2),
                PeerAverage = Math.Round(pAvg, 2),
                ChrAverage = chrPerc,
                ChrRawScore = Math.Round(chrAvg, 2),
                Courses = courses,
                OverallPercentage = Math.Round(((sAvg + pAvg + chrPerc) / 30.0) * 100, 2)
            });
        }

        [HttpGet]
        [Route("GetCourseComparison/{teacherId}/{sessionId}")]
        public IHttpActionResult GetCourseComparison(string teacherId, int sessionId)
        {
            const double MAX = 4.0;
            const double SCALE = 10.0;

            var courses = db.Enrollment
                .Where(e => e.teacherID == teacherId && e.sessionID == sessionId)
                .Select(e => e.courseCode)
                .Distinct()
                .ToList();

            var result = courses.Select(course =>
            {
                var studentList = db.StudentEvaluation
                    .Where(s => s.Enrollment.teacherID == teacherId
                             && s.Enrollment.sessionID == sessionId
                             && s.Enrollment.courseCode == course)
                    .ToList();
                double sAvg = studentList.Any()
                    ? (studentList.Sum(s => (double)s.score) / (studentList.Count * MAX)) * SCALE : 0;

                var peerList = db.PeerEvaluation
                    .Where(p => p.evaluateeID == teacherId
                             && p.PeerEvaluator.sessionID == sessionId
                             && p.courseCode == course)
                    .ToList();
                double pAvg = peerList.Any()
                    ? (peerList.Sum(p => (double)p.score) / (peerList.Count * MAX)) * SCALE : 0;

                return new
                {
                    CourseCode = course,
                    StudentAverage = Math.Round(sAvg, 2),
                    PeerAverage = Math.Round(pAvg, 2),
                    Overall = Math.Round(((sAvg + pAvg) / 20.0) * 100, 2)
                };
            }).ToList();

            return Ok(result);
        }

        [HttpGet]
        [Route("GetMyQuestionStats/{teacherId}/{sessionId}")]
        public IHttpActionResult GetMyQuestionStats(string teacherId, int sessionId, string courseCode = null, string evaluationType = "both")
        {
            var result = new List<object>();

            if (evaluationType == "student" || evaluationType == "both")
            {
                var studentData = db.StudentEvaluation
                    .Where(s => s.Enrollment.teacherID == teacherId
                             && s.Enrollment.sessionID == sessionId
                             && (string.IsNullOrEmpty(courseCode) || s.Enrollment.courseCode == courseCode))
                    .GroupBy(s => s.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,
                        QuestionText = db.Questions
                            .Where(q => q.QuestionID == g.Key)
                            .Select(q => q.QuestionText).FirstOrDefault(),
                        AverageScore = g.Average(x => (double?)x.score) ?? 0,
                        TotalResponses = g.Count(),
                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),
                        Type = "Student"
                    }).ToList();
                result.AddRange(studentData);
            }

            if (evaluationType == "peer" || evaluationType == "both")
            {
                var peerData = db.PeerEvaluation
                    .Where(p => p.evaluateeID == teacherId
                             && p.PeerEvaluator.sessionID == sessionId
                             && (string.IsNullOrEmpty(courseCode) || p.courseCode == courseCode))
                    .GroupBy(p => p.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,
                        QuestionText = db.Questions
                            .Where(q => q.QuestionID == g.Key)
                            .Select(q => q.QuestionText).FirstOrDefault(),
                        AverageScore = g.Average(x => (double?)x.score) ?? 0,
                        TotalResponses = g.Count(),
                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),
                        Type = "Peer"
                    }).ToList();
                result.AddRange(peerData);
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("GetTeacherResultByCourse")]  // ✅ Sirf ek baar
        public IHttpActionResult GetTeachersResultByCourse(string teacherId, string courseCode, int sessionId)
        {
            const double MAX_SCORE_PER_QUESTION = 4.0;
            const double SCALE_TO_TEN = 10.0;

            var peerList = db.PeerEvaluation
                .Where(p => p.evaluateeID == teacherId &&
                       (string.IsNullOrEmpty(courseCode) || p.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
                       p.PeerEvaluator.sessionID == sessionId)
                .ToList();

            double peerTotalScore = peerList.Sum(p => (double)p.score);
            double peerMaxTotal = peerList.Count * MAX_SCORE_PER_QUESTION;
            double peerAvg = peerMaxTotal > 0 ? (peerTotalScore / peerMaxTotal) * SCALE_TO_TEN : 0;

            var studentList = db.StudentEvaluation
                .Where(s => s.Enrollment.teacherID == teacherId &&
                       s.Enrollment.sessionID == sessionId &&
                       (string.IsNullOrEmpty(courseCode) || s.Enrollment.courseCode == courseCode))
                .ToList();

            double studentTotalScore = studentList.Sum(s => (double)s.score);
            double studentMaxTotal = studentList.Count * MAX_SCORE_PER_QUESTION;
            double studentAvg = studentMaxTotal > 0 ? (studentTotalScore / studentMaxTotal) * SCALE_TO_TEN : 0;

            double totalScore = peerTotalScore + studentTotalScore;
            double totalMax = peerMaxTotal + studentMaxTotal;
            double overallPercentage = totalMax > 0 ? (totalScore / totalMax) * 100 : 0;

            var name = db.Teacher.Where(t => t.userID == teacherId).Select(t => t.name).FirstOrDefault();

            return Ok(new
            {
                Name = name,
                PeerAverage = Math.Round(peerAvg, 2),
                StudentAverage = Math.Round(studentAvg, 2),
                Percentage = Math.Round(overallPercentage, 2),
                PeerTotal = peerTotalScore,
                PeerMax = peerMaxTotal,
                StudentTotal = studentTotalScore,
                StudentMax = studentMaxTotal,
                Total = totalScore,
                TotalMax = totalMax
            });
        }

        [HttpGet]
        [Route("GetCourseQuestionDetail/{teacherId}/{sessionId}/{courseCode}")]
        public IHttpActionResult GetCourseQuestionDetail(string teacherId, int sessionId, string courseCode, string evaluationType = "both")
        {
            var result = new List<object>();

            if (evaluationType == "student" || evaluationType == "both")
            {
                var studentGroups = db.StudentEvaluation
                    .Where(s => s.Enrollment.teacherID == teacherId
                             && s.Enrollment.sessionID == sessionId
                             && s.Enrollment.courseCode == courseCode)
                    .GroupBy(s => s.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,
                        QuestionText = db.Questions
                            .Where(q => q.QuestionID == g.Key)
                            .Select(q => q.QuestionText).FirstOrDefault(),
                        AverageScore = g.Average(x => (double?)x.score) ?? 0,
                        TotalResponses = g.Count(),
                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),
                        Type = "Student",
                        // Per student detail
                        StudentDetails = g.Select(s => new
                        {
                            StudentName = db.Student
                                .Where(st => st.userID == s.Enrollment.studentID)
                                .Select(st => st.name).FirstOrDefault(),
                            RollNo = s.Enrollment.studentID,
                            Score = s.score
                        }).ToList()
                    }).ToList();
                result.AddRange(studentGroups);
            }

            if (evaluationType == "peer" || evaluationType == "both")
            {
                var peerGroups = db.PeerEvaluation
                    .Where(p => p.evaluateeID == teacherId
                             && p.PeerEvaluator.sessionID == sessionId
                             && p.courseCode == courseCode)
                    .GroupBy(p => p.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,
                        QuestionText = db.Questions
                            .Where(q => q.QuestionID == g.Key)
                            .Select(q => q.QuestionText).FirstOrDefault(),
                        AverageScore = g.Average(x => (double?)x.score) ?? 0,
                        TotalResponses = g.Count(),
                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),
                        Type = "Peer",
                        StudentDetails = g.Select(p => new
                        {
                            StudentName = db.Teacher
                                .Where(t => t.userID == p.PeerEvaluator.teacherID)
                                .Select(t => t.name).FirstOrDefault(),
                            RollNo = p.PeerEvaluator.teacherID,
                            Score = p.score
                        }).ToList()
                    }).ToList();
                result.AddRange(peerGroups);
            }

            return Ok(result);
        }


    }
}