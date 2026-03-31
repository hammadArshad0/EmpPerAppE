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

        // 1. GET: Enrollment Courses for HOD Screen
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

        // 2. POST: Evaluate Submission (Handles Paper and Folder)
        [HttpPost]
        [Route("SaveEvaluation")] // Route matching your frontend: /CourseManagement/SaveEvaluation
        public IHttpActionResult SaveEvaluation(EvaluationRequestDTO dto)
        {
            if (dto == null || dto.Evaluations == null) return BadRequest("Invalid Data");

            try
            {
                using (var scope = new TransactionScope())
                {
                    foreach (var eval in dto.Evaluations)
                    {
                        // Logic: OnTime = 5, Late = 2
                        // Yahan hum string comparison case-insensitive kar rahe hain (on-time vs OnTime)
                        int paperScore = eval.PaperStatus.ToLower().Contains("on-time") ? 5 : 2;
                        int folderScore = eval.FolderStatus.ToLower().Contains("on-time") ? 5 : 2;

                        // 1. Save Paper Submission Score for this specific course
                        UpsertScore(dto.TeacherID, dto.SessionID, "Paper Submission", paperScore, dto.HODID, eval.CourseCode);

                        // 2. Save Folder Submission Score for this specific course
                        UpsertScore(dto.TeacherID, dto.SessionID, "Folder Submission", folderScore, dto.HODID, eval.CourseCode);
                    }

                    db.SaveChanges();
                    scope.Complete();
                    return Ok(new { message = "Evaluation saved/updated successfully for all courses!" });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Backend Error: " + ex.Message));
            }
        }

        // Updated Helper Method (Added CourseCode support)
        private void UpsertScore(string tid, int sid, string subKpiName, int scoreValue, string hodId, string courseCode)
        {
            var mapping = db.EmployeSessionKPI.FirstOrDefault(m =>
                          m.SubKPI.name.Contains(subKpiName) && m.SessionID == sid);

            if (mapping != null)
            {
                // Yahan hum check kar rahe hain ke is Teacher ka, is Course ka, is Session mein record pehle se hai ya nahi
                // Note: Agar aapke KPIScore table mein courseCode ka column nahi hai, to ye global save hoga.
                var existingScore = db.KPIScore.FirstOrDefault(s =>
                                    s.empKPIID == mapping.id &&
                                    s.empID == tid);
                // s.CourseCode == courseCode // Agar column hai to ye line uncomment karein

                if (existingScore != null)
                {
                    existingScore.score = scoreValue;
                    existingScore.evaluatorID = hodId;
                }
                else
                {
                    db.KPIScore.Add(new KPIScore
                    {
                        empKPIID = mapping.id,
                        empID = tid,
                        score = scoreValue,
                        evaluatorID = hodId
                        // CourseCode = courseCode // Agar column hai to
                    });
                }
            }
        }
        // 4. GET: Teacher Performance/Remarks for Teacher Login
        [HttpGet]
        [Route("my-Courseperformance/{tid}/{sid}")]
        public IHttpActionResult GetTeacherRemarks(string tid, int sid)
        {
            try
            {
                var performance = (from s in db.KPIScore
                                   join m in db.EmployeSessionKPI on s.empKPIID equals m.id
                                   join sub in db.SubKPI on m.SubKPIID equals sub.id
                                   where s.empID == tid && m.SessionID == sid
                                   select new
                                   {
                                       Activity = sub.name,
                                       ObtainedScore = s.score,
                                       Status = s.score == 5 ? "On Time" : "Late",
                                       Remarks = s.score == 5 ? "Excellent! Submitted on time." : "Delayed submission recorded."
                                   }).ToList();

                if (!performance.Any())
                    return NotFound();

                return Ok(performance);
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception("Fetch performance error: " + ex.Message));
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