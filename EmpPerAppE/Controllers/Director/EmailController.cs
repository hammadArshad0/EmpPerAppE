
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Helpers;
using System.Web.Http;
using EmpPerAppE.Models;
using MailKit;
using MailKit.Search;
using EmpPerAppE.Models.DTO;

namespace EmpPerAppE.Controllers.Director
{
    [RoutePrefix("api/email")]
    public class EmailController : ApiController
    {
        fypempEntities db = new fypempEntities();

        // 1. Get All Emails - List dikhane ke liye
        //[HttpGet]
        //[Route("getall")]
        //public IHttpActionResult GetAllEmails()
        //{
        //    var emails = db.Email.OrderByDescending(x => x.id).ToList();
        //    return Ok(emails);
        //}
        [HttpGet]
        [Route("getall")]
        public IHttpActionResult GetAllEmails()
        {
            var emails = db.Email
                .Select(x => new
                {
                    x.id,
                    x.mail,
                    x.isActive
                })
                .OrderByDescending(x => x.id)
                .ToList();

            return Ok(emails);
        }

        // 2. Add New Email - Nayi email save karne ke liye
        //[HttpPost]
        //[Route("add")]
        //public IHttpActionResult AddEmail(Email model)
        //{
        //    if (model == null || string.IsNullOrEmpty(model.mail))
        //        return BadRequest("Email address is required.");

        //    model.isActive = false; // By default inactive rakhein
        //    db.Email.Add(model);
        //    db.SaveChanges();

        //    return Ok(new { message = "Email added successfully", data = model });
        //}

        [HttpPost]
        [Route("add")]
        public IHttpActionResult AddEmail(Email model)
        {
            if (model == null || string.IsNullOrEmpty(model.mail) || string.IsNullOrEmpty(model.password))
                return BadRequest("Email and App Password are required.");

            model.isActive = false;

            db.Email.Add(model);
            db.SaveChanges();

            return Ok(model);
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




        //[HttpPost]
        //[Route("get-evaluations")]
        //public IHttpActionResult GetEvaluations([FromBody] Email request)
        //{
        //    try
        //    {
        //        var emailRecord = db.Email.FirstOrDefault(e => e.mail == request.mail);
        //        if (emailRecord == null)
        //            return BadRequest("Email not found");

        //        var evaluations = new List<object>();

        //        using (var client = new MailKit.Net.Imap.ImapClient())
        //        {
        //            // ✅ SSL certificate ignore karo (common error)
        //            client.ServerCertificateValidationCallback = (s, c, h, e) => true;

        //            client.Connect("imap.gmail.com", 993, true);
        //            client.Authenticate(emailRecord.mail, emailRecord.password);

        //            var inbox = client.Inbox;
        //            // 🔥 Dynamic filter
        //            SearchQuery query;

        //            if (request.filter == "unread")
        //            {
        //                query = SearchQuery.NotSeen;
        //            }
        //            else if (request.filter == "read")
        //            {
        //                query = SearchQuery.Seen;
        //            }
        //            else
        //            {
        //                query = SearchQuery.All;
        //            }

        //            inbox.Open(MailKit.FolderAccess.ReadOnly);

        //            // ✅ Sirf last 50 emails dekho - saari nahi
        //            var allUids = inbox.Search(MailKit.Search.SearchQuery.All);
        //            var recentUids = allUids.Reverse().Take(50).ToList();

        //            foreach (var uid in recentUids)
        //            {
        //                var message = inbox.GetMessage(uid);
        //                var body = message.TextBody ?? message.HtmlBody;

        //                if (string.IsNullOrEmpty(body)) continue;

        //                var start = body.IndexOf("START_EVAL");
        //                var end = body.IndexOf("END_EVAL");

        //                if (start == -1 || end == -1 || end <= start) continue;

        //                try
        //                {
        //                    var json = body.Substring(start + 10, end - (start + 10)).Trim();
        //                    var parsed = Newtonsoft.Json.Linq.JObject.Parse(json);

        //                    var studentNode = parsed["Student"] ?? parsed["student"];
        //                    var teacherEvalsNode = parsed["TeacherEvaluations"] ?? parsed["teacherEvaluations"];

        //                    if (teacherEvalsNode == null) continue;

        //                    foreach (var te in teacherEvalsNode)
        //                    {
        //                        var teacher = te["Teacher"] ?? te["teacher"];
        //                        var course = te["Course"] ?? te["course"];
        //                        var evals = te["Evaluations"] ?? te["evaluations"];

        //                        evaluations.Add(new
        //                        {
        //                            studentId = studentNode?["ID"]?.ToString() ?? studentNode?["id"]?.ToString() ?? "N/A",
        //                            studentName = studentNode?["Name"]?.ToString() ?? studentNode?["name"]?.ToString() ?? "N/A",
        //                            teacherId = teacher?["ID"]?.ToString() ?? teacher?["id"]?.ToString() ?? "N/A",
        //                            teacherName = teacher?["Name"]?.ToString() ?? teacher?["name"]?.ToString() ?? "N/A",
        //                            session = parsed["Session"]?.ToString() ?? parsed["session"]?.ToString() ?? "N/A",
        //                            subjectCode = course?["Code"]?.ToString() ?? course?["code"]?.ToString() ?? "N/A",
        //                            subjectTitle = course?["Title"]?.ToString() ?? course?["title"]?.ToString() ?? "N/A",
        //                            submittedOn = message.Date.DateTime,
        //                            evaluation = evals
        //                        });
        //                    }
        //                }
        //                catch (Exception parseEx)
        //                {
        //                    System.Diagnostics.Debug.WriteLine("PARSE ERROR: " + parseEx.Message);
        //                    continue;
        //                }
        //            }

        //            client.Disconnect(true);
        //        }

        //        return Ok(new
        //        {
        //            success = true,
        //            count = evaluations.Count,
        //            data = evaluations
        //        });
        //    }
        //    catch (Exception ex)
        //    {
        //        // ✅ Full error message return karo
        //        return Ok(new
        //        {
        //            success = false,
        //            error = ex.Message,
        //            detail = ex.InnerException?.Message ?? "No inner exception"
        //        });
        //    }
    
        [HttpPost]
        [Route("get-evaluations")]
        public IHttpActionResult GetEvaluations([FromBody] EmailRequestDTO request) // Make sure Email class has 'mail', 'password', and 'filter'
        {
            try
            {
                var emailRecord = db.Email.FirstOrDefault(e => e.mail == request.mail);
                if (emailRecord == null)
                    return BadRequest("Email not found");

                var evaluations = new List<object>();

                using (var client = new MailKit.Net.Imap.ImapClient())
                {
                    // SSL certificate ignore (common for development)
                    client.ServerCertificateValidationCallback = (s, c, h, e) => true;

                    client.Connect("imap.gmail.com", 993, true);
                    client.Authenticate(emailRecord.mail, emailRecord.password);

                    var inbox = client.Inbox;

                    // ✅ ReadWrite access zaroori hai agar aap "unread" ko "read" mark karna chahte hain
                    inbox.Open(MailKit.FolderAccess.ReadWrite);

                    //  1. Dynamic Filter Setup
                    SearchQuery query;
                    if (request.filter == "unread")
                        query = SearchQuery.NotSeen;
                    else if (request.filter == "read")
                        query = SearchQuery.Seen;
                    else
                        query = SearchQuery.All;

                    // ✅ Confidential subject filter bhi add kar sakte hain (Optional)
                    // query = query.And(SearchQuery.SubjectContains("Confidential"));

                    // 🔥 2. Search using the query
                    var filteredUids = inbox.Search(query);

                    // 🔥 3. Sirf recent 50 filtered emails uthao
                    var recentUids = filteredUids.Reverse().Take(50).ToList();

                    foreach (var uid in recentUids)
                    {
                        var message = inbox.GetMessage(uid);

                        // ✅ Agar filter "unread" hai, to fetch karne ke baad usay read mark kar dein
                        if (request.filter == "unread")
                        {
                            inbox.AddFlags(uid, MessageFlags.Seen, true);
                        }

                        var body = message.TextBody ?? message.HtmlBody;
                        if (string.IsNullOrEmpty(body)) continue;

                        var start = body.IndexOf("START_EVAL");
                        var end = body.IndexOf("END_EVAL");

                        if (start == -1 || end == -1 || end <= start) continue;

                        try
                        {
                            // Indexing fix (+10 for START_EVAL length)
                            var json = body.Substring(start + 10, end - (start + 10)).Trim();
                            var parsed = Newtonsoft.Json.Linq.JObject.Parse(json);

                            var studentNode = parsed["Student"] ?? parsed["student"];
                            var teacherEvalsNode = parsed["TeacherEvaluations"] ?? parsed["teacherEvaluations"];

                            if (teacherEvalsNode == null) continue;

                            foreach (var te in teacherEvalsNode)
                            {
                                var teacher = te["Teacher"] ?? te["teacher"];
                                var course = te["Course"] ?? te["course"];
                                var evals = te["Evaluations"] ?? te["evaluations"];

                                evaluations.Add(new
                                {
                                    studentId = studentNode?["ID"]?.ToString() ?? studentNode?["id"]?.ToString() ?? "N/A",
                                    studentName = studentNode?["Name"]?.ToString() ?? studentNode?["name"]?.ToString() ?? "N/A",
                                    teacherId = teacher?["ID"]?.ToString() ?? teacher?["id"]?.ToString() ?? "N/A",
                                    teacherName = teacher?["Name"]?.ToString() ?? teacher?["name"]?.ToString() ?? "N/A",
                                    session = parsed["Session"]?.ToString() ?? parsed["session"]?.ToString() ?? "N/A",
                                    subjectCode = course?["Code"]?.ToString() ?? course?["code"]?.ToString() ?? "N/A",
                                    subjectTitle = course?["Title"]?.ToString() ?? course?["title"]?.ToString() ?? "N/A",
                                    submittedOn = message.Date.DateTime,
                                    evaluation = evals
                                });
                            }
                        }
                        catch (Exception parseEx)
                        {
                            System.Diagnostics.Debug.WriteLine("PARSE ERROR: " + parseEx.Message);
                            continue;
                        }
                    }

                    client.Disconnect(true);
                }

                return Ok(new
                {
                    success = true,
                    count = evaluations.Count,
                    data = evaluations
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    success = false,
                    error = ex.Message,
                    detail = ex.InnerException?.Message ?? "No inner exception"
                });
            }
        }
    }
    }
    

        
    