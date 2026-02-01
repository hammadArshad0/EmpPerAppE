using EmpPerAppE.Models;
using EmpPerAppE.Models.kpi;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Http;
using System.Transactions; // for TransactionScope
using System.Data.Entity;

namespace EmpPerAppE.Controllers.HOD
{
    [RoutePrefix("api/kpi")]
    public class KpiController : ApiController
    {
        fypempEntities db = new fypempEntities();

        // ---------------------------------
        // GET: List all KPI
        // ---------------------------------
        [HttpGet]
        [Route("list")]
        public IHttpActionResult GetAll()
        {
            try
            {
                var kpis = db.KPI.Select(k => new
                {
                    k.id,
                    k.name
                }).ToList();

                return Ok(kpis);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ---------------------------------
        // POST: Create KPI (simple)
        // ---------------------------------
        [HttpPost]
        [Route("create")]
        public IHttpActionResult Create(KPI model)
        {
            try
            {
                db.KPI.Add(model);
                db.SaveChanges();
                return Ok("KPI added successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ---------------------------------
        // PUT: Update KPI
        // ---------------------------------
        [HttpPut]
        [Route("update/{id}")]
        public IHttpActionResult Update(int id, KPI model)
        {
            try
            {
                var kpi = db.KPI.Find(id);
                if (kpi == null) return NotFound();

                kpi.name = model.name;
                db.SaveChanges();

                return Ok("KPI updated successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ---------------------------------
        // DELETE: KPI
        // ---------------------------------
        [HttpDelete]
        [Route("delete/{id}")]
        public IHttpActionResult Delete(int id)
        {
            try
            {
                var kpi = db.KPI.Find(id);
                if (kpi == null) return NotFound();

                db.KPI.Remove(kpi);
                db.SaveChanges();

                return Ok("KPI deleted successfully");
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ---------------------------------
        // POST: Create KPI + SubKPI + Auto Adjust Weight (EF5-safe)
        // ---------------------------------
        [HttpPost]
        [Route("create-with-weight")]
        public IHttpActionResult CreateWithWeight(AddKPIDto dto)
        {
            try
            {
                using (var scope = new TransactionScope()) // EF5-compatible transaction
                {
                    // 1️⃣ Adjust KPI weight
                    int usedWeight = db.SessionKPIWeight
                        .Where(x => x.SessionID == dto.SessionId)
                        .Sum(x => (int?)x.Weight) ?? 0;

                    int remaining = 100 - usedWeight;
                    if (remaining <= 0)
                        return BadRequest("Total KPI weight for this session is already 100");

                    int finalKPIWeight = dto.RequestedKPIWeight > remaining ? remaining : dto.RequestedKPIWeight;

                    // 2️⃣ Create KPI
                    KPI kpi = new KPI { name = dto.KPIName };
                    db.KPI.Add(kpi);
                    db.SaveChanges(); // get KPI ID

                    // 3️⃣ Only process SubKPIs if present
                    List<int> adjustedSubWeights = new List<int>();
                    if (dto.SubKPIs != null && dto.SubKPIs.Count > 0)
                    {
                        var subWeights = dto.SubKPIs.Select(x => x.Weight).ToList();
                        int totalSubWeight = subWeights.Sum();
                        if (totalSubWeight == 0) totalSubWeight = 1; // avoid divide by zero

                        double factor = (double)finalKPIWeight / totalSubWeight;
                        adjustedSubWeights = subWeights.Select(w => (int)Math.Round(w * factor)).ToList();

                        // fix rounding difference
                        int diff = finalKPIWeight - adjustedSubWeights.Sum();
                        adjustedSubWeights[0] += diff;

                        // insert SubKPI + SessionKPIWeight
                        for (int i = 0; i < dto.SubKPIs.Count; i++)
                        {
                            SubKPI sub = new SubKPI
                            {
                                KPIID = kpi.id,
                                name = dto.SubKPIs[i].Name
                            };
                            db.SubKPI.Add(sub);
                            db.SaveChanges(); // get SubKPI ID

                            SessionKPIWeight skw = new SessionKPIWeight
                            {
                                SessionID = dto.SessionId,
                                KPIID = kpi.id,
                                SubKPIID = sub.id,
                                Weight = adjustedSubWeights[i]
                            };
                            db.SessionKPIWeight.Add(skw);
                        }
                    }

                    db.SaveChanges();
                    scope.Complete(); // commit transaction

                    return Ok(new
                    {
                        Message = "KPI & SubKPIs added successfully",
                        KPIWeight = finalKPIWeight,
                        SubKPIWeights = adjustedSubWeights
                    });
                }
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }

        // ---------------------------------
        // GET: View KPIs with SubKPIs + Weight for a session
        // ---------------------------------
        [HttpGet]
        [Route("viewkpisubkpiweight/{sessionId}")]
        public IHttpActionResult ViewKpiSubKpiWeight(int sessionId)
        {
            try
            {
                // 1️⃣ Get all KPIs
                var kpiList = db.KPI.ToList();

                // 2️⃣ Get all SubKPIs
                var subList = db.SubKPI.ToList();

                // 3️⃣ Get all SessionKPIWeights for this session
                var weights = db.SessionKPIWeight
                                .Where(sw => sw.SessionID == sessionId)
                                .ToList();

                // 4️⃣ Build response
                var data = kpiList.Select(k => new
                {
                    k.id,
                    k.name,
                    KPIWeight = weights.Where(w => w.KPIID == k.id).Sum(w => (int?)w.Weight) ?? 0,
                    SubKPIs = subList
                                .Where(sub => sub.KPIID == k.id)
                                .Select(sub => new
                                {
                                    sub.id,
                                    sub.name,
                                    SubWeight = weights
                                                 .Where(sw => sw.KPIID == k.id && sw.SubKPIID == sub.id)
                                                 .Select(sw => sw.Weight)
                                                 .FirstOrDefault()
                                }).ToList()
                }).ToList();

                return Ok(data);
            }
            catch (Exception ex)
            {
                return InternalServerError(ex);
            }
        }


        // ---------------------------------
        // GET: List all Sessions
        // ---------------------------------


        // ---------------------------------
        // GET: List all Employee Types
        // ---------------------------------


    }
}
