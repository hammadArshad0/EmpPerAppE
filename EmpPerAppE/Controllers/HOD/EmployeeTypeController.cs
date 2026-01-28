using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Web.Http;
using EmpPerAppE.Models;

namespace EmpPerAppE.Controllers.HOD
{
        [RoutePrefix("api/employeetype")]
        public class EmployeeTypeController : ApiController
        {
            fypempEntities db = new fypempEntities();

          
            [HttpGet]
            [Route("list")]
            public IHttpActionResult GetAll()
            {
                try
                {
                    var data = db.EmployeeType
                        .Select(e => new
                        {
                            e.id,
                            e.type
                        }).ToList();

                    return Ok(data);
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }

         
            [HttpPost]
            [Route("create")]
            public IHttpActionResult Create(EmployeeType model)
            {
                try
                {
                    if (string.IsNullOrWhiteSpace(model.type))
                        return BadRequest("Employee type is required");

                    db.EmployeeType.Add(model);
                    db.SaveChanges();

                    return Ok("Employee Type added successfully");
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }

          
            [HttpPut]
            [Route("update/{id}")]
            public IHttpActionResult Update(int id, EmployeeType model)
            {
                try
                {
                    var et = db.EmployeeType.Find(id);
                    if (et == null) return NotFound();

                    et.type = model.type;
                    db.SaveChanges();

                    return Ok("Employee Type updated successfully");
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }

          
            [HttpDelete]
            [Route("delete/{id}")]
            public IHttpActionResult Delete(int id)
            {
                try
                {
                    var et = db.EmployeeType.Find(id);
                    if (et == null) return NotFound();

                    db.EmployeeType.Remove(et);
                    db.SaveChanges();

                    return Ok("Employee Type deleted successfully");
                }
                catch (Exception ex)
                {
                    return InternalServerError(ex);
                }
            }
        }
    }


