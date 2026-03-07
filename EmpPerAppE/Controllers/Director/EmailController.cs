
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Helpers;
using System.Web.Http;
using EmpPerAppE.Models;

namespace EmpPerAppE.Controllers.Director
{
    [RoutePrefix("api/email")]
    public class EmailController : ApiController
    {
        fypempEntities db = new fypempEntities();

        // 1. Get All Emails - List dikhane ke liye
        [HttpGet]
        [Route("getall")]
        public IHttpActionResult GetAllEmails()
        {
            var emails = db.Email.OrderByDescending(x => x.id).ToList();
            return Ok(emails);
        }

        // 2. Add New Email - Nayi email save karne ke liye
        [HttpPost]
        [Route("add")]
        public IHttpActionResult AddEmail(Email model)
        {
            if (model == null || string.IsNullOrEmpty(model.mail))
                return BadRequest("Email address is required.");

            model.isActive = false; // By default inactive rakhein
            db.Email.Add(model);
            db.SaveChanges();

            return Ok(new { message = "Email added successfully", data = model });
        }

        // 3. Activate Email (Toggle Logic) - Aik ko active karein baaki sab ko inactive
        [HttpPut]
        [Route("activate/{id}")]
        public IHttpActionResult ActivateEmail(int id)
        {
            var allEmails = db.Email.ToList();
            var targetEmail = allEmails.FirstOrDefault(x => x.id == id);

            if (targetEmail == null)
                return NotFound();

            // Loop chala kar sab ko 0 aur selected ko 1 kar dein
            foreach (var email in allEmails)
            {
                if (email.id == id)
                    email.isActive = true;
                else
                    email.isActive = false;
            }

            db.SaveChanges();
            return Ok("Email activated. Others have been deactivated.");
        }

        // 4. Delete Email
        [HttpDelete]
        [Route("delete/{id}")]
        public IHttpActionResult DeleteEmail(int id)
        {
            var email = db.Email.Find(id);
            if (email == null) return NotFound();

            db.Email.Remove(email);
            db.SaveChanges();

            return Ok("Email deleted successfully.");
        }

        // 5. Get Current Active Recipient (For Frontend Banner)
        [HttpGet]
        [Route("active")]
        public IHttpActionResult GetActiveEmail()
        {
            var active = db.Email.FirstOrDefault(x => x.isActive == true);
            if (active == null) return NotFound();
            return Ok(active);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }


        // ============================================
        // 6. Update Email - Purani email ko edit karne ke liye
        // ============================================
        [HttpPut]
        [Route("edit/{id}")]
        public IHttpActionResult UpdateEmail(int id, Email model)
        {
            if (model == null || string.IsNullOrEmpty(model.mail))
                return BadRequest("Email address is required.");

            var emailToUpdate = db.Email.Find(id);

            if (emailToUpdate == null)
                return NotFound();

            // Sirf mail column ko update karein
            emailToUpdate.mail = model.mail;

            // Agar aap chahte hain ke edit ke waqt status change na ho to sirf mail save karein
            db.Entry(emailToUpdate).State = System.Data.Entity.EntityState.Modified;
            db.SaveChanges();

            return Ok(new { message = "Email updated successfully", data = emailToUpdate });
        }
    }
}