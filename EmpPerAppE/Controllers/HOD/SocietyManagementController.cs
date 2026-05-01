using EmpPerAppE.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using EmpPerAppE.Models.DTO;
using System.Data.Entity;
using static System.Net.WebRequestMethods;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/SocietyManagement")]
    public class SocietyManagementController : ApiController
    {
        private fypempEntities db = new fypempEntities();

        // 1. GET ALL SOCIETIES (Filtered by Session)
        [HttpGet]
        [Route("GetAll")]
        public IHttpActionResult GetAll(int? sessionId = null)
        {
            try
            {
                if (sessionId == null) return BadRequest("Session ID is required.");

                // ToList() memory mein data laane ke liye taake complex joins crash na hon
                var societies = db.Societies.AsNoTracking().ToList();
                var assignments = db.SocietyAssignments.Where(a => a.SessionId == sessionId).AsNoTracking().ToList();
                var teachers = db.Teacher.AsNoTracking().ToList();

                var result = societies.Select(s => new
                {
                    s.SocietyId,
                    s.SocietyName,
                    s.Description,
                    Chairpersons = (from a in assignments
                                    join t in teachers on a.TeacherId equals t.userID
                                    where a.SocietyId == s.SocietyId && a.IsChairperson == true
                                    select new { a.AssignmentId, TeacherName = t.name, TeacherId = t.userID }).ToList(),
                    Mentors = (from a in assignments
                               join t in teachers on a.TeacherId equals t.userID
                               where a.SocietyId == s.SocietyId && a.IsMentor == true
                               select new { a.AssignmentId, TeacherName = t.name, TeacherId = t.userID }).ToList()
                }).ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                return InternalServerError(new Exception(ex.Message));
            }
        }

        // 2. ADD NEW SOCIETY
        [HttpPost]
        [Route("AddSociety")]
        public IHttpActionResult AddSociety([FromBody] SocietyDTO model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.SocietyName))
                return BadRequest("Society Name is required.");

            try
            {
                var society = new Societies
                {
                    SocietyName = model.SocietyName,
                    Description = model.Description ?? ""
                };
                db.Societies.Add(society);
                db.SaveChanges();

                return Ok(new { message = "Society added successfully", societyId = society.SocietyId });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 3. UPDATE SOCIETY
        [HttpPut]
        [Route("UpdateSociety/{id}")]
        public IHttpActionResult UpdateSociety(int id, [FromBody] SocietyDTO model)
        {
            try
            {
                var society = db.Societies.Find(id);
                if (society == null) return NotFound();

                society.SocietyName = model.SocietyName;
                society.Description = model.Description;
                db.SaveChanges();

                return Ok(new { message = "Society updated successfully" });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 4. DELETE SOCIETY (Includes Cleanup)
        [HttpDelete]
        [Route("DeleteSociety/{id}")]
        public IHttpActionResult DeleteSociety(int id)
        {
            try
            {
                var society = db.Societies.Find(id);
                if (society == null) return NotFound();

                // Delete related assignments first
                var related = db.SocietyAssignments.Where(a => a.SocietyId == id).ToList();
                db.SocietyAssignments.RemoveRange(related);

                db.Societies.Remove(society);
                db.SaveChanges();
                return Ok(new { message = "Society deleted successfully" });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 5. ASSIGN CHAIRPERSON (Unique per Society/Session)
        //[HttpPost]
        //[Route("AssignTeacher")]
        //public IHttpActionResult AssignTeacher([FromBody] SocietyAssignmentDTO model)
        //{
        //    try
        //    {
        //        if (model == null) return BadRequest();

        //        // Remove existing chairperson for this society in this session
        //        var existing = db.SocietyAssignments.Where(x => x.SocietyId == model.SocietyId && x.SessionId == model.SessionId && x.IsChairperson == true).ToList();
        //        db.SocietyAssignments.RemoveRange(existing);

        //        db.SocietyAssignments.Add(new SocietyAssignments
        //        {
        //            TeacherId = model.TeacherId,
        //            SocietyId = model.SocietyId,
        //            SessionId = model.SessionId,
        //            IsChairperson = true,
        //            IsMentor = false
        //        });
        //        db.SaveChanges();
        //        return Ok(new { message = "Chairperson assigned" });
        //    }
        //    catch (Exception ex) { return InternalServerError(ex); }
        //}
        [HttpPost]
        [Route("AssignTeacher")]
        public IHttpActionResult AssignTeacher([FromBody] SocietyAssignmentDTO model)
        {
            // Check if model is null or TeacherId is empty string
            if (model == null || string.IsNullOrEmpty(model.TeacherId))
                return BadRequest("Invalid data: Teacher selection is required.");

            try
            {
                // STEP 1: REMOVE ALL OLD CHAIRPERSONS
                var oldChairs = db.SocietyAssignments
                    .Where(x =>
                        x.SocietyId == model.SocietyId &&
                        x.SessionId == model.SessionId &&
                        x.IsChairperson == true)
                    .ToList();

                if (oldChairs.Any())
                {
                    db.SocietyAssignments.RemoveRange(oldChairs);
                }

                // STEP 2: ADD NEW CHAIRPERSON
                var newChair = new SocietyAssignments
                {
                    TeacherId = model.TeacherId, // Yeh string accept ho jayegi
                    SocietyId = model.SocietyId,
                    SessionId = model.SessionId,
                    IsChairperson = true,
                    IsMentor = false
                };

                db.SocietyAssignments.Add(newChair);
                db.SaveChanges();

                return Ok(new { message = "Chairperson updated successfully" });
            }
            catch (Exception ex)
            {
                // Database error check karne ke liye
                return InternalServerError(ex);
            }
        }

        [HttpPost]
        [Route("AssignMentorsBulk")]
        public IHttpActionResult AssignMentorsBulk([FromBody] List<SocietyAssignmentDTO> models)
        {
            try
            {
                if (models == null || !models.Any()) return BadRequest();

                foreach (var m in models)
                {
                    bool exists = db.SocietyAssignments.Any(x => x.SocietyId == m.SocietyId && x.SessionId == m.SessionId && x.TeacherId == m.TeacherId && x.IsMentor == true);
                    if (!exists)
                    {
                        db.SocietyAssignments.Add(new SocietyAssignments
                        {
                            TeacherId = m.TeacherId,
                            SocietyId = m.SocietyId,
                            SessionId = m.SessionId,
                            IsChairperson = false,
                            IsMentor = true
                        });
                    }
                }
                db.SaveChanges();
                return Ok(new { message = "Mentors assigned" });
            }
            catch (Exception ex) { return InternalServerError(ex); }
        }

        // 7. REMOVE ASSIGNMENT
        [HttpDelete]
        [Route("DeleteAssignment/{id}")]
        public IHttpActionResult DeleteAssignment(int id)
        {
            var assignment = db.SocietyAssignments.Find(id);
            if (assignment == null) return NotFound();

            db.SocietyAssignments.Remove(assignment);
            db.SaveChanges();
            return Ok(new { message = "Removed successfully" });
        }

        // 8. HELPERS
        [HttpGet]
        [Route("GetTeachers")]
        public IHttpActionResult GetTeachers()
        {
            return Ok(db.Teacher.Select(t => new { t.userID, t.name }).ToList());
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}