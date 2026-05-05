using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;

namespace EmpPerAppE.Controllers.Director
{
    [RoutePrefix("api/AcademicPerformance")]
    public class AcademicPerformanceController : ApiController
    {
        private fypempEntities db = new fypempEntities();


        [HttpGet]
        [Route("GetTeacherQuestionStatsFull")]
        public IHttpActionResult GetTeacherQuestionStatsFull(string teacherId, int sessionId, string evaluationType, string courseCode = null)
        {
            var result = new List<object>();

            // =========================
            // 🔹 STUDENT EVALUATION
            // =========================
            if (evaluationType == "student" || evaluationType == "both")
            {
                //var studentQuery = db.StudentEvaluation
                //    .Where(s =>
                //        s.SessionID == sessionId &&
                //        s.Enrollment.teacherID == teacherId &&
                //        (courseCode == null || s.Enrollment.courseCode == courseCode)
                //    );
                var studentQuery = db.StudentEvaluation
                                          .Where(s =>
                                          // 1. Session check enrollment ke zariye karein
                                          s.Enrollment.sessionID == sessionId &&

                                          // 2. Teacher check
                                          s.Enrollment.teacherID == teacherId &&

                                  // 3. Course code filter (null safety ke saath)
                                  (string.IsNullOrEmpty(courseCode) || s.Enrollment.courseCode == courseCode)
    );

                var studentData = studentQuery
                    .GroupBy(s => s.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,

                        QuestionText = db.Questions
                            .Where(q => q.QuestionID == g.Key)
                            .Select(q => q.QuestionText)
                            .FirstOrDefault(),

                        AverageScore = g.Average(x => (double?)x.score) ?? 0,

                        TotalResponses = g.Count(),

                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),

                        Type = "Student"
                    })
                    .ToList();

                result.AddRange(studentData);
            }

            // =========================
            // 🔹 PEER EVALUATION
            // =========================
            if (evaluationType == "peer" || evaluationType == "both")
            {
                //var peerQuery = db.PeerEvaluation
                //    .Where(p =>
                //        p.SessionId == sessionId &&
                //        p.evaluateeID == teacherId &&
                //        (courseCode == null || p.courseCode == courseCode)
                //    );
                //////////////////////////
                var peerQuery = db.PeerEvaluation
                         .Where(p =>
                               // 1. SessionId direct nahi hai, isay PeerEvaluator navigation property se nikalain
                               p.PeerEvaluator.sessionID == sessionId &&

                                 // 2. Evaluatee (jis teacher ki evaluation ho rahi hai)
                                 p.evaluateeID == teacherId &&

                                 // 3. Course Code filter (null safety ke sath)
                                 (string.IsNullOrEmpty(courseCode) || p.courseCode == courseCode)
                             );
                /////////////////////////
                var peerData = peerQuery
                    .GroupBy(p => p.questionID)
                    .Select(g => new
                    {
                        QuestionId = g.Key,

                        QuestionText = db.Questions
                            .Where(q => q.QuestionID == g.Key)
                            .Select(q => q.QuestionText)
                            .FirstOrDefault(),

                        AverageScore = g.Average(x => (double?)x.score) ?? 0,

                        TotalResponses = g.Count(),

                        Score1 = g.Count(x => x.score == 1),
                        Score2 = g.Count(x => x.score == 2),
                        Score3 = g.Count(x => x.score == 3),
                        Score4 = g.Count(x => x.score == 4),

                        Type = "Peer"
                    })
                    .ToList();

                result.AddRange(peerData);
            }

            return Ok(result);
        }

        [HttpGet]
        [Route("GetTeacherResultByCourse")]
        public IHttpActionResult GetTeachersResultByCourse(string teacherId, string courseCode, int sessionId)
        {
            const double MAX_SCORE_PER_QUESTION = 4.0;
            const double SCALE_TO_TEN = 10.0;

            // --- Peer Evaluation ---
            //var peerList = db.PeerEvaluation
            //    .Where(p => p.evaluateeID == teacherId &&
            //           (courseCode == null || p.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
            //           (sessionId == null || p.SessionId == sessionId))
            //    .ToList();
            ////////////////////////////////////
            var peerList = db.PeerEvaluation
               .Where(p => p.evaluateeID == teacherId &&
                      // Course code filter with null safety
                      (string.IsNullOrEmpty(courseCode) || p.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
                      // Simplified SessionId filter (Works for both int and int?)
                      (sessionId == null || p.PeerEvaluator.sessionID == sessionId))
               .ToList();

            double peerTotalScore = peerList.Sum(p => (double)p.score);
            double peerMaxTotal = peerList.Count * MAX_SCORE_PER_QUESTION;
            double peerAverageOutOfTen = peerMaxTotal > 0
                ? (peerTotalScore / peerMaxTotal) * SCALE_TO_TEN
                : 0;

            // --- Student Evaluation ---
            //var studentList = db.StudentEvaluation
            //    .Where(s =>
            //        (courseCode == null || s.Enrollment.courseCode.ToUpper().Trim() == courseCode.ToUpper().Trim()) &&
            //        (sessionId == null || s.SessionID == sessionId) &&
            //        s.Enrollment.teacherID == teacherId
            //    )
            //    .ToList();
            var studentList = db.StudentEvaluation
    .Where(s =>
        // 1. Teacher ID match karein (Must condition)
        s.Enrollment.teacherID == teacherId &&

        // 2. Session ID Enrollment table se nikalain (Relation Fix)
        (sessionId == null || s.Enrollment.sessionID == sessionId) &&

        // 3. Course Code safety ke sath check karein
        (string.IsNullOrEmpty(courseCode) || s.Enrollment.courseCode == courseCode)
    )
    .ToList();

            double studentTotalScore = studentList.Sum(s => (double)s.score);
            double studentMaxTotal = studentList.Count * MAX_SCORE_PER_QUESTION;
            double studentAverageOutOfTen = studentMaxTotal > 0
                ? (studentTotalScore / studentMaxTotal) * SCALE_TO_TEN
                : 0;

            // --- Overall ---
            double totalScore = peerTotalScore + studentTotalScore;
            double totalMax = peerMaxTotal + studentMaxTotal;

            double overallPercentage = totalMax > 0
                ? (totalScore / totalMax) * 100
                : 0;

            var name = db.Teacher
                .Where(t => t.userID == teacherId)
                .Select(t => t.name)
                .FirstOrDefault();

            return Ok(new
            {
                Name = name,
                PeerAverage = Math.Round(peerAverageOutOfTen, 2),
                StudentAverage = Math.Round(studentAverageOutOfTen, 2),
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
        [Route("GetSessions")]
        public IHttpActionResult GetSessions()
        {
            return Ok(db.Session
                .OrderByDescending(s => s.id)
                .Select(s => new { s.id, s.name })
                .ToList());
        }

        //[HttpGet]
        //[Route("GetDepartments")]
        //public IHttpActionResult GetDepartments()
        //{
        //    return Ok(db.Teacher
        //        .Select(t => t.department)
        //        .Distinct()
        //        .ToList());
        //}
        [HttpGet]
        [Route("GetDepartments")]
        public IHttpActionResult GetDepartments()
        {
            // Teacher table mein mojud unique departments ki list
            var departments = db.Teacher
                .Where(t => t.department != null && t.department != "")
                .Select(t => t.department)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            return Ok(departments);
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



            // 1. Get All Teachers Summary for a Session (For the Graph/Cards)
            [HttpGet]
            [Route("GetAllTeachersSummary")]
            public IHttpActionResult GetAllTeachersSummary(int sessionId)
            {
                var teachers = db.Teacher.ToList();
                var summaryList = new List<object>();

                foreach (var teacher in teachers)
                {
                    // Apka purana calculation logic yahan apply hoga 
                    // (StudentAvg, PeerAvg, Overall%)
                    // Mazeed behtar hai ke aap is logic ko ek private method mein rakh lein
                    var result = CalculatePerformance(teacher.userID, sessionId);
                    summaryList.Add(result);
                }

                return Ok(summaryList);
            }
        [HttpGet]
        [Route("GetTeachersPerformanceList")]
        public IHttpActionResult GetTeachersPerformanceList(int sessionId, string department = "All", string courseCode = "All")
        {
            var query = db.Enrollment.Where(e => e.sessionID == sessionId);

            if (department != "All") query = query.Where(e => e.Teacher.department == department);
            if (courseCode != "All") query = query.Where(e => e.courseCode == courseCode);

            var teacherIds = query.Select(e => e.teacherID).Distinct().ToList();
            var finalData = new List<object>();

            foreach (var tid in teacherIds)
            {
                // Yahan aap apna CalculatePerformance logic call karein
                // Isme ConfidentialEvaluation ka logic bhi add karein
                var perf = CalculatePerformance(tid, sessionId);
                finalData.Add(perf);
            }
            return Ok(finalData);
        }

        // Helper method (Taake code duplicate na ho)
        private object CalculatePerformance(string teacherId, int sessionId)
        {
            const double MAX = 4.0;
            const double SCALE = 10.0;

            // 1. Student Evaluations
            var studentList = db.StudentEvaluation
                .Where(s => s.Enrollment.teacherID == teacherId && s.Enrollment.sessionID == sessionId)
                .ToList();
            double sTotal = studentList.Sum(s => (double)s.score);
            double sMax = studentList.Count * MAX;
            double sAvg = sMax > 0 ? (sTotal / sMax) * SCALE : 0;

            // 2. Peer Evaluations
            var peerList = db.PeerEvaluation
                .Where(p => p.evaluateeID == teacherId && p.PeerEvaluator.sessionID == sessionId)
                .ToList();
            double pTotal = peerList.Sum(p => (double)p.score);
            double pMax = peerList.Count * MAX;
            double pAvg = pMax > 0 ? (pTotal / pMax) * SCALE : 0;

            // ✅ 3. CHR — Enrollment se session verify
            var isEnrolled = db.Enrollment
                .Any(e => e.teacherID == teacherId && e.sessionID == sessionId);

            // 3. CHR Average Score — Session filter ke saath
            // Sirf us session ki CHR records consider hongi
            var chrAvg = 0.0;

            var chrRawData = db.CHR
                .Where(c => c.TeacherID == teacherId && c.sessionID == sessionId)
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

            // CHR ko 10 scale pe convert karo (baaki scores ki tarah)
            double chrPerc = Math.Round((chrAvg / 5.0) * SCALE, 2);

            var teacher = db.Teacher.FirstOrDefault(t => t.userID == teacherId);

            return new
            {
                TeacherID = teacherId,
                Name = teacher?.name,
                StudentAverage = Math.Round(sAvg, 2),
                PeerAverage = Math.Round(pAvg, 2),
                ChrAverage = chrPerc,           // ✅ CHR score 0-10 scale
                ChrRawScore = Math.Round(chrAvg, 2), // ✅ Raw score 0-5 scale
                CourseCode = db.Enrollment
                    .Where(e => e.teacherID == teacherId && e.sessionID == sessionId)
                    .Select(e => e.courseCode).FirstOrDefault()
            };
        }

    }
    }

