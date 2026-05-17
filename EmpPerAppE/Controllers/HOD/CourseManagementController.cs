using EmpPerAppE.Models;
using EmpPerAppE.Models.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Transactions;
using System.Web.Http;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/CourseManagement")]
    public class CourseManagementController : ApiController
    {
        private readonly fypempEntities db = new fypempEntities();

         //1. GET: Enrollment Courses for HOD Screen
        [HttpGet]
        [Route("EnrollmentCourses/{sessionId}")]
        public IHttpActionResult GetEnrollmentCourses(int sessionId)
        {
            try
            {
                var data = (from e in db.Enrollment
                            join t in db.Teacher on e.teacherID equals t.userID
                            join c in db.Course on e.courseCode equals c.code
                            where e.sessionID == sessionId
                            select new { e.teacherID, t.name, e.courseCode, c.title })
                            .Distinct()
                            .ToList()
                            .GroupBy(x => new { x.teacherID, x.name })
                            .Select(g => new TeacherCourseResponse
                            {
                                TeacherID = g.Key.teacherID,
                                TeacherName = g.Key.name,
                                EnrolledCourses = g.Select(c => new EnrolledCourseDTO
                                {
                                    Id = c.courseCode,
                                    Course = c.title,
                                    Code = c.courseCode
                                }).ToList()
                            }).ToList();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Courses fetch error: " + ex.Message));
            }
        }
        //[HttpGet]
        //[Route("EnrollmentCourses/{sid}")]
        //public IHttpActionResult GetEnrollmentCourses(int sid)
        //{
        //    var data = (from e in db.Enrollment
        //                join t in db.Teacher on e.teacherID equals t.userID
        //                join c in db.Course on e.courseCode equals c.code
        //                where e.sessionID == sid
        //                group new { e, c } by new {t.userID, t.name } into g
        //                select new
        //                {
        //                    TeacherID = g.Key.userID,
        //                    TeacherName = g.Key.name,
        //                    // Check if scores already exist for this teacher in this session
        //                    IsEvaluated = db.KPIScore.Any(ks => ks.empID == g.Key.userID &&
        //                                  db.EmployeSessionKPI.Any(ek => ek.id == ks.empKPIID && ek.SessionID == sid)),
        //                    EnrolledCourses = g.Select(x => new
        //                    {
        //                        Code = x.e.courseCode,
        //                        Course = x.c.title
        //                    }).ToList()
        //                }).ToList();

        //    return Ok(data);
        //}
        [HttpPost]
        [Route("SaveEvaluation")]
        public IHttpActionResult SaveEvaluation(EvaluationRequestDTO dto)
        {
            if (dto == null || dto.Evaluations == null)
                return BadRequest("Payload data is missing.");

            try
            {
                using (var scope = new TransactionScope())
                {
                    foreach (var eval in dto.Evaluations)
                    {
                        // Score Logic
                        int paperScore = eval.PaperStatus.ToLower().Contains("on-time") ? 5 : 2;
                        int folderScore = eval.FolderStatus.ToLower().Contains("on-time") ? 5 : 2;

                        // Remarks Formatting
                        string pRemarks = $"Paper: {eval.PaperStatus} | Note: {eval.Remarks}";
                        string fRemarks = $"Folder: {eval.FolderStatus} | Note: {eval.Remarks}";

                        // Call Helper for both KPIs
                        UpsertScore(dto.TeacherID, dto.SessionID, "Paper Submission", paperScore, dto.HODID, eval.CourseCode, pRemarks);
                        UpsertScore(dto.TeacherID, dto.SessionID, "Folder Submission", folderScore, dto.HODID, eval.CourseCode, fRemarks);
                    }

                    db.SaveChanges();
                    scope.Complete();
                    return Ok(new { Message = "Teacher evaluation saved successfully for all courses!" });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Database Save Error: " + ex.Message));
            }
        }
        // 2. POST: Evaluate Submission (Handles Paper and Folder)
        private void UpsertScore(string tid, int sid, string subKpiName, int score, string hodId, string courseCode, string remarks)
        {
            // 1. Enrollment ID dhoondain (Schema ke mutabiq names use kiye hain)
            // Teacher table mein 'userID' aur Course table mein 'code' hai
            var enrollment = db.Enrollment.FirstOrDefault(e =>
                e.teacherID == tid &&
                e.courseCode == courseCode &&
                e.sessionID == sid);

            if (enrollment == null) return;

            // 2. SubKPI mapping dhoondain
            var mapping = db.EmployeSessionKPI.FirstOrDefault(m =>
                m.SubKPI.name.Contains(subKpiName) &&
                m.SessionID == sid);

            if (mapping == null) return;

            // 3. KPIScore mein check karein
            int enrollId = enrollment.id;
            var existing = db.KPIScore.FirstOrDefault(s =>
                s.empKPIID == mapping.id &&
                s.enrollmentID == enrollId);

            if (existing != null)
            {
                existing.score = score;
                existing.evaluatorID = hodId;
                existing.remarks = remarks;
            }
            else
            {
                db.KPIScore.Add(new KPIScore
                {
                    empKPIID = mapping.id,
                    empID = tid,
                    score = score,
                    evaluatorID = hodId,
                    enrollmentID = enrollId,
                    remarks = remarks
                });
            }
        }
        // 4. GET: Teacher Performance/Remarks for Teacher Login
        //[HttpGet]
        //[Route("my-Courseperformance/{tid}/{sid}")]
        //public IHttpActionResult GetTeacherRemarks(string tid, int sid)
        //{
        //    try
        //    {
        //        var performance = (from s in db.KPIScore
        //                           join m in db.EmployeSessionKPI on s.empKPIID equals m.id
        //                           join sub in db.SubKPI on m.SubKPIID equals sub.id
        //                           where s.empID == tid && m.SessionID == sid
        //                           select new
        //                           {
        //                               Activity = sub.name,
        //                               ObtainedScore = s.score,
        //                               Status = s.score == 5 ? "On Time" : "Late",
        //                               Remarks = s.score == 5
        //                                   ? "Excellent! Submitted on time."
        //                                   : "Delayed submission recorded."
        //                           }).ToList();

        //        if (!performance.Any())
        //            return NotFound();

        //        return Ok(performance);
        //    }
        //    catch (Exception ex)
        //    {
        //        return InternalServerError(new Exception("Fetch performance error: " + ex.Message));
        //    }
        //}



        [HttpGet]
        [Route("my-Courseperformance/{tid}/{sid}")]
        public IHttpActionResult GetTeacherRemarks(string tid, int sid)
        {
            try
            {
                var performance = (from s in db.KPIScore
                                   join m in db.EmployeSessionKPI on s.empKPIID equals m.id
                                   join sub in db.SubKPI on m.SubKPIID equals sub.id
                                   join e in db.Enrollment on s.enrollmentID equals e.id
                                   where s.empID == tid && m.SessionID == sid
                                   select new
                                   {
                                       CourseCode = e.courseCode,
                                       Activity = sub.name,
                                       ObtainedScore = s.score,
                                       Status = s.score == 5 ? "On Time" : "Late",
                                       // Default Remark + Database se manual Remark dono ko jor diya
                                       Remarks = (s.score == 5 ? "Excellent! Submitted on time." : "Delayed submission recorded.")
                                                 + " | HOD Note: " + (s.remarks ?? "No additional notes.")
                                   }).ToList();

                var courseDetails = (from e in db.Enrollment
                                     join c in db.Course on e.courseCode equals c.code
                                     where e.teacherID == tid && e.sessionID == sid
                                     select new
                                     {
                                         CourseCode = e.courseCode,
                                         CourseName = c.title
                                     }).Distinct().ToList();

                return Ok(new { Performance = performance, Courses = courseDetails });
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Error: " + ex.Message));
            }
        }
        // Enrollment Controller mein add karo
        [HttpGet]
        [Route("TeacherSessions/{teacherId}")]
        public IHttpActionResult GetTeacherSessions(string teacherId)
        {
            try
            {
                var sessions = (from e in db.Enrollment
                                join s in db.Session on e.sessionID equals s.id
                                where e.teacherID == teacherId
                                select new { s.id, s.name })
                                .Distinct()
                                .OrderBy(s => s.id)  // Latest last mein
                                .ToList();

                return Ok(sessions);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }









        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}